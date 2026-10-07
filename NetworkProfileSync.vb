Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Diagnostics
Imports System.Text
Imports System.Threading.Tasks

''' <summary>
''' Rileva i percorsi di rete e conserva lo staging locale persistente.
''' Le copie complete sono consentite solo a browser chiuso; non garantiscono la validita della sessione.
''' </summary>
Public Class NetworkProfileSync

    Private Shared ReadOnly _leaseGate As New Object()
    Private Shared ReadOnly _leases As New Dictionary(Of String, FileStream)(StringComparer.OrdinalIgnoreCase)

    Private Shared ReadOnly _syncLock As New System.Threading.SemaphoreSlim(1, 1)

    Private Shared ReadOnly _isRunningOnNetwork As Lazy(Of Boolean) = New Lazy(Of Boolean)(Function()
        Return IsNetworkPath(AppDomain.CurrentDomain.BaseDirectory)
    End Function)

    ''' <summary>
    ''' Indica se l'applicazione è in esecuzione su una cartella o unità di rete (percorso UNC o mapped network drive).
    ''' </summary>
    Public Shared ReadOnly Property IsRunningOnNetwork As Boolean
        Get
            Return _isRunningOnNetwork.Value
        End Get
    End Property

    ''' <summary>
    ''' Determina se il percorso fornito risiede su una condivisione di rete (UNC o drive mappato SMB).
    ''' </summary>
    Public Shared Function IsNetworkPath(path As String) As Boolean
        If String.IsNullOrWhiteSpace(path) Then Return False
        Try
            ' 1. Verifica percorsi UNC (\\server\share\...)
            Dim uri As Uri = Nothing
            If Uri.TryCreate(path, UriKind.Absolute, uri) AndAlso uri.IsUnc Then
                Return True
            End If
            If path.StartsWith("\\", StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If

            ' 2. Verifica unità mappate (es. Z:\)
            Dim root = System.IO.Path.GetPathRoot(path)
            If Not String.IsNullOrEmpty(root) Then
                Dim driveInfo As New DriveInfo(root)
                If driveInfo.DriveType = DriveType.Network Then
                    Return True
                End If
            End If
        Catch ex As Exception
            Debug.WriteLine($"[NetworkProfileSync] IsNetworkPath check exception for '{path}': {ex.Message}")
        End Try
        Return False
    End Function

    ''' <summary>
    ''' Cartella di staging locale su macchina host (%LOCALAPPDATA%\HidaChat\NetworkProfiles).
    ''' </summary>
    Public Shared ReadOnly Property LocalStagingBaseDirectory As String
        Get
            Dim localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            Return Path.Combine(localApp, "HidaChat", "NetworkProfiles")
        End Get
    End Property

    ''' <summary>
    ''' Percorso master del profilo account sulla share di rete portabile (data/webview/WV2Profile_{id}).
    ''' </summary>
    Public Shared Function GetMasterProfileDir(accountId As String) As String
        ValidateAccountId(accountId)
        Return Path.Combine(AppAccounts.SharedDataDirectory, $"WV2Profile_{accountId}")
    End Function

    ''' <summary>
    ''' Percorso di staging locale del profilo account (%LOCALAPPDATA%\HidaChat\NetworkProfiles\WV2Profile_{id}).
    ''' </summary>
    Public Shared Function GetLocalStagingProfileDir(accountId As String) As String
        ValidateAccountId(accountId)
        Return Path.Combine(LocalStagingBaseDirectory, $"WV2Profile_{accountId}")
    End Function

    ''' <summary>
    ''' Restituisce il percorso effettivo da passare a CoreWebView2Environment.
    ''' Se in rete e staging abilitato, restituisce la cartella locale, altrimenti la cartella master.
    ''' </summary>
    Public Shared Function GetEffectiveProfileDir(accountId As String, enableStaging As Boolean) As String
        If enableStaging AndAlso IsRunningOnNetwork Then
            Return GetLocalStagingProfileDir(accountId)
        Else
            Return GetMasterProfileDir(accountId)
        End If
    End Function

    Private Shared Sub ValidateAccountId(accountId As String)
        If String.IsNullOrWhiteSpace(accountId) OrElse
           Not System.Text.RegularExpressions.Regex.IsMatch(accountId, "\A[A-Za-z0-9_-]+\z") Then
            Throw New ArgumentException("Invalid profile account ID.", NameOf(accountId))
        End If
    End Sub

    ''' <summary>
    ''' Bootstrap ONLY an empty staging directory. Existing local state always wins;
    ''' file size and timestamps cannot establish authentication or database integrity.
    ''' </summary>
    Public Shared Async Function SyncMasterToLocalStagingAsync(accountId As String) As Task(Of Boolean)
        If Not HasSessionLock(accountId) Then Return False
        Await _syncLock.WaitAsync()
        Try
            Dim staging = GetLocalStagingProfileDir(accountId)
            ColdProfileCopy.RecoverInterruptedPublication(staging)
            If ColdProfileCopy.HasData(staging) Then
                AppLogger.LogSync($"[LOCAL_PROFILE_PRESERVED] account={accountId}; no merge or automatic rollback.")
                Return True
            End If
            AppLogger.LogSync($"[COLD_IMPORT] account={accountId}; first import into empty local staging.")
            Return Await ColdProfileCopy.SeedIfEmptyAsync(GetMasterProfileDir(accountId), staging)
        Catch ex As Exception
            AppLogger.LogSync($"[COLD_IMPORT_FAILED] account={accountId}: {ex.Message}")
            Return False
        Finally
            _syncLock.Release()
        End Try
    End Function

    ''' <summary>
    ''' Export only after the caller has observed BrowserProcessExited for this profile.
    ''' A periodic/live copy is deliberately a no-op, including Cookies and Local State.
    ''' </summary>
    Public Shared Async Function SyncLocalStagingToMasterAsync(accountId As String,
            Optional isPeriodic As Boolean = False, Optional browserExited As Boolean = False) As Task(Of Boolean)
        If isPeriodic OrElse Not browserExited OrElse Not HasSessionLock(accountId) Then
            AppLogger.LogSync($"[LIVE_COPY_REFUSED] account={accountId}, periodic={isPeriodic}, exited={browserExited}")
            Return False
        End If
        Await _syncLock.WaitAsync()
        Try
            Dim source = GetLocalStagingProfileDir(accountId)
            If Not ColdProfileCopy.HasData(source) Then Return False
            AppLogger.LogSync($"[COLD_EXPORT] account={accountId}; browser exit confirmed.")
            Return Await ColdProfileCopy.PublishAsync(source, GetMasterProfileDir(accountId))
        Finally
            _syncLock.Release()
        End Try
    End Function

    ''' <summary>
    ''' OS-held lease, outside the profile. Retained through recovery and final export.
    ''' A timestamp file is not a lock, and never authorizes stealing an active lease.
    ''' </summary>
    Public Shared Function AcquireSessionLock(accountId As String) As String
        ValidateAccountId(accountId)
        SyncLock _leaseGate
            If _leases.ContainsKey(accountId) Then Return Nothing
            Dim lease As FileStream = Nothing
            Try
                Dim lockDirectory = Path.Combine(AppAccounts.SharedDataDirectory, "_profile_locks")
                Directory.CreateDirectory(lockDirectory)
                Dim lockPath = Path.Combine(lockDirectory, accountId & ".lock")
                lease = New FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)
                Dim record = JsonSerializer.Serialize(New With {
                    .machine = Environment.MachineName,
                    .pid = Environment.ProcessId,
                    .startedUtc = DateTime.UtcNow
                })
                Dim data = Encoding.UTF8.GetBytes(record)
                lease.SetLength(0)
                lease.Write(data, 0, data.Length)
                lease.Flush(flushToDisk:=True)
                _leases.Add(accountId, lease)
                Return Nothing
            Catch ex As Exception
                If lease IsNot Nothing Then lease.Dispose()
                AppLogger.LogSync($"[PROFILE_LEASE_DENIED] account={accountId}: {ex.Message}")
                Return "Profilo gia' in uso oppure condivisione non accessibile. " & ex.Message
            End Try
        End SyncLock
    End Function

    Private Shared Function HasSessionLock(accountId As String) As Boolean
        SyncLock _leaseGate
            Return _leases.ContainsKey(accountId)
        End SyncLock
    End Function

    Public Shared Sub ReleaseSessionLock(accountId As String)
        SyncLock _leaseGate
            Dim lease As FileStream = Nothing
            If _leases.TryGetValue(accountId, lease) Then
                _leases.Remove(accountId)
                lease.Dispose()
                ' Never delete: another process may already have opened the same file.
            End If
        End SyncLock
    End Sub

    ''' <summary>Only used for explicit account removal after the browser has exited.</summary>
    Public Shared Sub CleanLocalStaging(accountId As String)
        Dim staging = GetLocalStagingProfileDir(accountId)
        If Directory.Exists(staging) Then Directory.Delete(staging, recursive:=True)
    End Sub
End Class
