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

    Private Shared ReadOnly _staleGate As New Object()
    Private Shared ReadOnly _staleStagingAccounts As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>
    ''' Marks local staging as stale when master refresh fails or is skipped due to locks (TASK-0127).
    ''' </summary>
    Public Shared Sub MarkStagingStale(stagingDir As String, accountId As String)
        SyncLock _staleGate
            _staleStagingAccounts.Add(accountId)
        End SyncLock
        Try
            If Directory.Exists(stagingDir) Then
                Dim marker = Path.Combine(stagingDir, ".sync_stale")
                File.WriteAllText(marker, DateTime.UtcNow.ToString("o"), Encoding.UTF8)
            End If
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' Clears stale staging status after a successful import or synchronization (TASK-0127).
    ''' </summary>
    Public Shared Sub ClearStagingStale(stagingDir As String, accountId As String)
        SyncLock _staleGate
            _staleStagingAccounts.Remove(accountId)
        End SyncLock
        Try
            If Directory.Exists(stagingDir) Then
                Dim marker = Path.Combine(stagingDir, ".sync_stale")
                If File.Exists(marker) Then File.Delete(marker)
            End If
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' Returns True if local staging is marked stale and must not overwrite master (TASK-0127).
    ''' </summary>
    Public Shared Function IsStagingStale(stagingDir As String, accountId As String) As Boolean
        SyncLock _staleGate
            If _staleStagingAccounts.Contains(accountId) Then Return True
        End SyncLock
        Try
            If Directory.Exists(stagingDir) AndAlso File.Exists(Path.Combine(stagingDir, ".sync_stale")) Then
                Return True
            End If
        Catch
        End Try
        Return False
    End Function

    Private Shared ReadOnly _accountLocks As New System.Collections.Concurrent.ConcurrentDictionary(Of String, System.Threading.SemaphoreSlim)(StringComparer.OrdinalIgnoreCase)

    Private Shared Function GetAccountLock(accountId As String) As System.Threading.SemaphoreSlim
        Return _accountLocks.GetOrAdd(accountId, Function(k) New System.Threading.SemaphoreSlim(1, 1))
    End Function

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
        Return ProfileIsolationPolicy.ResolveEffectiveProfileDirectory(AppAccounts.SharedDataDirectory, accountId)
    End Function

    ''' <summary>
    ''' Percorso di staging locale del profilo account (%LOCALAPPDATA%\HidaChat\NetworkProfiles\WV2Profile_{id}).
    ''' </summary>
    Public Shared Function GetLocalStagingProfileDir(accountId As String) As String
        ValidateAccountId(accountId)
        Return ProfileIsolationPolicy.ResolveEffectiveProfileDirectory(LocalStagingBaseDirectory, accountId)
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
    ''' Sincronizza il profilo master sulla share di rete verso lo staging locale.
    ''' Se lo staging locale è vuoto, effettua il bootstrap iniziale.
    ''' Se lo staging locale esiste già ma il Master sulla rete è più recente (esportato da un altro PC),
    ''' aggiorna lo staging locale con la versione master per garantire la continuità della sessione (TODO #109).
    ''' </summary>
    Public Shared Async Function SyncMasterToLocalStagingAsync(accountId As String) As Task(Of Boolean)
        If Not HasSessionLock(accountId) Then Return False
        Dim accountLock = GetAccountLock(accountId)
        Await accountLock.WaitAsync()
        Try
            Dim staging = GetLocalStagingProfileDir(accountId)
            Dim master = GetMasterProfileDir(accountId)
            ColdProfileCopy.RecoverInterruptedPublication(staging)
            ColdProfileCopy.RecoverInterruptedPublication(master)

            If Not ColdProfileCopy.HasData(staging) Then
                AppLogger.LogSync($"[COLD_IMPORT] account={accountId}; first import into empty local staging.")
                Dim seeded = Await ColdProfileCopy.SeedIfEmptyAsync(master, staging)
                If seeded Then
                    ClearStagingStale(staging, accountId)
                Else
                    MarkStagingStale(staging, accountId)
                End If
                Return seeded
            End If

            If ShouldRefreshStagingFromMaster(master, staging, accountId) Then
                AppLogger.LogSync($"[COLD_IMPORT] account={accountId}; master has newer session data from network. Refreshing local staging.")
                Dim refreshed = Await ColdProfileCopy.PublishAsync(master, staging)
                If refreshed Then
                    ClearStagingStale(staging, accountId)
                    Return True
                Else
                    MarkStagingStale(staging, accountId)
                    AppLogger.LogSync($"[STAGING_REFRESH_FAILED] account={accountId}; master refresh failed or was blocked. Local staging marked as STALE to prevent poisoning master on exit.")
                    If ColdProfileCopy.HasData(staging) Then
                        AppLogger.LogSync($"[LOCAL_PROFILE_PRESERVED] account={accountId}; master refresh skipped (profile in use or locked). Retaining existing valid local staging.")
                        Return True
                    Else
                        Return False
                    End If
                End If
            Else
                ClearStagingStale(staging, accountId)
                AppLogger.LogSync($"[LOCAL_PROFILE_PRESERVED] account={accountId}; local staging is up to date with master.")
                Return True
            End If
        Catch ex As Exception
            AppLogger.LogSync($"[COLD_IMPORT_FAILED] account={accountId}: {ex.Message}")
            Dim stagingDir = GetLocalStagingProfileDir(accountId)
            MarkStagingStale(stagingDir, accountId)
            If ColdProfileCopy.HasData(stagingDir) Then
                AppLogger.LogSync($"[LOCAL_PROFILE_PRESERVED] account={accountId}; exception during import, but valid local staging exists. Marked STALE to prevent export.")
                Return True
            End If
            Return False
        Finally
            accountLock.Release()
        End Try
    End Function

    ''' <summary>
    ''' Determina se lo staging locale deve essere rinfrescato dal master su rete.
    ''' </summary>
    Public Shared Function ShouldRefreshStagingFromMaster(master As String, staging As String, accountId As String) As Boolean
        If String.IsNullOrWhiteSpace(master) OrElse Not Directory.Exists(master) OrElse Not ColdProfileCopy.HasData(master) Then
            Return False
        End If
        If String.IsNullOrWhiteSpace(staging) OrElse Not Directory.Exists(staging) OrElse Not ColdProfileCopy.HasData(staging) Then
            Return True
        End If

        ' 1. Verifica tramite metadati .sync_state.json
        Dim masterState = ReadSyncState(master)
        Dim stagingState = ReadSyncState(staging)

        If masterState IsNot Nothing AndAlso stagingState IsNot Nothing Then
            If masterState.ExportedUtc > stagingState.ExportedUtc Then
                Return True
            Else
                Return False
            End If
        ElseIf masterState IsNot Nothing AndAlso stagingState Is Nothing Then
            Return True
        End If

        ' 2. Controllo differenziale file-by-file se i metadati non sono disponibili
        If ColdProfileCopy.HasModifications(master, staging) Then
            Return True
        End If

        Return False
    End Function

    Private Shared Sub WriteSyncState(profileDir As String, accountId As String)
        Try
            If String.IsNullOrWhiteSpace(profileDir) OrElse Not Directory.Exists(profileDir) Then Return
            Dim statePath = Path.Combine(profileDir, ".sync_state.json")
            Dim state As New ProfileSyncState With {
                .AccountId = accountId,
                .ExportedUtc = DateTime.UtcNow,
                .Machine = Environment.MachineName,
                .User = Environment.UserName,
                .Generation = DateTime.UtcNow.Ticks
            }
            Dim json = JsonSerializer.Serialize(state, New JsonSerializerOptions With {.WriteIndented = True})
            ' Rimuove eventuali attributi Hidden o ReadOnly ereditati da share Linux/Samba o Win32 CopyFileEx
            If File.Exists(statePath) Then
                Dim attrs = File.GetAttributes(statePath)
                If (attrs And (FileAttributes.Hidden Or FileAttributes.ReadOnly)) <> 0 Then
                    File.SetAttributes(statePath, attrs And Not (FileAttributes.Hidden Or FileAttributes.ReadOnly))
                End If
            End If
            File.WriteAllText(statePath, json, Encoding.UTF8)
        Catch ex As Exception
            AppLogger.LogSync($"[SYNC_STATE_WRITE_FAILED] account={accountId}: {ex.Message}")
        End Try
    End Sub

    Private Shared Function ReadSyncState(profileDir As String) As ProfileSyncState
        Try
            If String.IsNullOrWhiteSpace(profileDir) OrElse Not Directory.Exists(profileDir) Then Return Nothing
            Dim statePath = Path.Combine(profileDir, ".sync_state.json")
            If Not File.Exists(statePath) Then Return Nothing
            Dim json = File.ReadAllText(statePath, Encoding.UTF8)
            Return JsonSerializer.Deserialize(Of ProfileSyncState)(json)
        Catch
            Return Nothing
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
        Dim accountLock = GetAccountLock(accountId)
        Await accountLock.WaitAsync()
        Try
            Dim source = GetLocalStagingProfileDir(accountId)
            Dim master = GetMasterProfileDir(accountId)
            If Not ColdProfileCopy.HasData(source) Then Return False

            ' Guard 1: Do not export if local staging was marked stale (TASK-0127)
            If IsStagingStale(source, accountId) Then
                AppLogger.LogSync($"[EXPORT_REFUSED_STALE_STAGING] account={accountId}; export to master aborted because local staging was not successfully refreshed from master.")
                Return False
            End If

            ' Guard 2: Do not export if master is newer than local baseline (TASK-0127)
            Dim masterState = ReadSyncState(master)
            Dim sourceState = ReadSyncState(source)
            If masterState IsNot Nothing AndAlso sourceState IsNot Nothing Then
                If masterState.ExportedUtc > sourceState.ExportedUtc Then
                    AppLogger.LogSync($"[EXPORT_REFUSED_MASTER_NEWER] account={accountId}; master was updated by {masterState.Machine} ({masterState.User}) at {masterState.ExportedUtc:o}, which is newer than local baseline ({sourceState.ExportedUtc:o}). Export aborted to prevent overwriting session.")
                    Return False
                End If
            End If

            ' Controllo differenziale rapido su disco locale: se il profilo non ha subito modifiche
            ' dall'ultimo export, salta l'intera copia di rete
            If Not ColdProfileCopy.HasModifications(source, master) Then
                AppLogger.LogSync($"[EXPORT_UNCHANGED] account={accountId}; no local changes, network sync skipped.")
                Return True
            End If

            ' Scrive lo stato di sync aggiornato prima dell'esportazione
            WriteSyncState(source, accountId)

            AppLogger.LogSync($"[COLD_EXPORT] account={accountId}; browser exit confirmed.")
            Return Await ColdProfileCopy.PublishAsync(source, master)
        Finally
            accountLock.Release()
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

''' <summary>
''' Metadati di stato di sincronizzazione del profilo salvati in .sync_state.json per coordinamento multi-PC.
''' </summary>
Public Class ProfileSyncState
    Public Property AccountId As String
    Public Property ExportedUtc As DateTime
    Public Property Machine As String
    Public Property User As String
    Public Property Generation As Long
End Class
