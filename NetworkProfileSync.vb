Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Diagnostics

''' <summary>
''' Gestisce il rilevamento di unità di rete (UNC o drive mappati SMB/CIFS) e lo staging locale temporaneo 
''' dei profili utente WebView2, consentendo la persistenza affidabile delle sessioni (WhatsApp Web, cookie, IndexedDB)
''' ed evitando le corruzioni di SQLite/LevelDB native dei filesystem di rete (TODO #73).
''' </summary>
Public Class NetworkProfileSync

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
        Return Path.Combine(AppAccounts.SharedDataDirectory, $"WV2Profile_{accountId}")
    End Function

    ''' <summary>
    ''' Percorso di staging locale del profilo account (%LOCALAPPDATA%\HidaChat\NetworkProfiles\WV2Profile_{id}).
    ''' </summary>
    Public Shared Function GetLocalStagingProfileDir(accountId As String) As String
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

    ''' <summary>
    ''' Determina se un file o directory relativo del profilo WebView2 debba essere escluso dalla sincronizzazione di rete.
    ''' Esclude cache volatili, shader, crash report e file di lock temporanei per massimizzare la velocità di trasferimento.
    ''' </summary>
    Public Shared Function ShouldSkipRelativePath(relativePath As String) As Boolean
        If String.IsNullOrEmpty(relativePath) Then Return False
        Dim normalized = relativePath.Replace("/"c, "\"c).TrimStart("\"c)

        ' Cartelle di cache volatile non rilevanti per il login o la sessione
        Dim excludedPrefixes As String() = {
            "EBWebView\Crashpad",
            "EBWebView\GrShaderCache",
            "EBWebView\ShaderCache",
            "EBWebView\component_crx_cache",
            "EBWebView\Subresource Filter",
            "EBWebView\Default\Cache",
            "EBWebView\Default\Code Cache",
            "EBWebView\Default\GPUCache",
            "EBWebView\Default\DawnGraphiteCache",
            "EBWebView\Default\DawnWebGPUCache",
            "EBWebView\Default\GPUPersistentCache",
            "EBWebView\Default\Service Worker\CacheStorage",
            "EBWebView\Default\Service Worker\ScriptCache",
            "EBWebView\Default\Extension Rules"
        }

        For Each prefix In excludedPrefixes
            If normalized.Equals(prefix, StringComparison.OrdinalIgnoreCase) OrElse
               normalized.StartsWith(prefix & "\", StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
        Next

        ' Esclude file di lock temporanei
        Dim fileName = Path.GetFileName(normalized)
        If fileName.Equals("LOCK", StringComparison.OrdinalIgnoreCase) OrElse
           fileName.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) OrElse
           fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) OrElse
           fileName.Equals(".active_session", StringComparison.OrdinalIgnoreCase) Then
            Return True
        End If

        Return False
    End Function

    ''' <summary>
    ''' Sincronizza una cartella sorgente verso una cartella di destinazione in modo incrementale.
    ''' Se isMirror è True, elimina dalla destinazione i file non più presenti nella sorgente (utile alla chiusura per LevelDB compact).
    ''' </summary>
    Public Shared Async Function SyncDirectoryAsync(sourceDir As String, targetDir As String, isMirror As Boolean) As Task(Of Boolean)
        If String.IsNullOrWhiteSpace(sourceDir) OrElse Not Directory.Exists(sourceDir) Then Return False

        Return Await Task.Run(Function() As Boolean
            Try
                If Not Directory.Exists(targetDir) Then
                    Directory.CreateDirectory(targetDir)
                End If

                Dim sourceDirUri = New Uri(sourceDir.TrimEnd(Path.DirectorySeparatorChar) & Path.DirectorySeparatorChar)

                ' 1. Copia incrementale di file nuovi o modificati
                For Each srcFile In Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories)
                    Try
                        Dim srcUri = New Uri(srcFile)
                        Dim relPath = Uri.UnescapeDataString(sourceDirUri.MakeRelativeUri(srcUri).ToString().Replace("/"c, "\"c))

                        If ShouldSkipRelativePath(relPath) Then Continue For

                        Dim dstFile = Path.Combine(targetDir, relPath)
                        Dim dstDir = Path.GetDirectoryName(dstFile)
                        If Not String.IsNullOrEmpty(dstDir) AndAlso Not Directory.Exists(dstDir) Then
                            Directory.CreateDirectory(dstDir)
                        End If

                        Dim needCopy = True
                        If File.Exists(dstFile) Then
                            Dim srcInfo As New FileInfo(srcFile)
                            Dim dstInfo As New FileInfo(dstFile)
                            If srcInfo.Length = dstInfo.Length AndAlso Math.Abs((srcInfo.LastWriteTimeUtc - dstInfo.LastWriteTimeUtc).TotalSeconds) < 2 Then
                                needCopy = False
                            End If
                        End If

                        If needCopy Then
                            CopyFileWithRetry(srcFile, dstFile, maxRetries:=3)
                        End If
                    Catch exFile As Exception
                        Debug.WriteLine($"[NetworkProfileSync] SyncDirectory file copy warning ({srcFile}): {exFile.Message}")
                    End Try
                Next

                ' 2. Pulizia mirror (rimozione file orfani nel target non più presenti in sorgente)
                If isMirror AndAlso Directory.Exists(targetDir) Then
                    Dim targetDirUri = New Uri(targetDir.TrimEnd(Path.DirectorySeparatorChar) & Path.DirectorySeparatorChar)
                    For Each dstFile In Directory.EnumerateFiles(targetDir, "*", SearchOption.AllDirectories)
                        Try
                            Dim dstUri = New Uri(dstFile)
                            Dim relPath = Uri.UnescapeDataString(targetDirUri.MakeRelativeUri(dstUri).ToString().Replace("/"c, "\"c))

                            If ShouldSkipRelativePath(relPath) Then Continue For

                            Dim srcFile = Path.Combine(sourceDir, relPath)
                            If Not File.Exists(srcFile) Then
                                Try
                                    File.Delete(dstFile)
                                    Debug.WriteLine($"[NetworkProfileSync] Mirror removed obsolete target file: {dstFile}")
                                Catch
                                End Try
                            End If
                        Catch
                        End Try
                    Next
                End If

                Return True
            Catch ex As Exception
                Debug.WriteLine($"[NetworkProfileSync] SyncDirectoryAsync error: {ex.Message}")
                Return False
            End Try
        End Function)
    End Function

    ''' <summary>
    ''' Copia un file gestendo eventuali lock temporanei tramite tentativi con ritardo.
    ''' </summary>
    Private Shared Sub CopyFileWithRetry(sourceFile As String, destFile As String, maxRetries As Integer)
        For attempt = 1 To maxRetries
            Try
                ' Usa FileShare.ReadWrite per permettere la lettura anche se il file è aperto con shared access
                Using srcStream As New FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                    Using dstStream As New FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None)
                        srcStream.CopyTo(dstStream)
                    End Using
                End Using
                File.SetLastWriteTimeUtc(destFile, File.GetLastWriteTimeUtc(sourceFile))
                Return
            Catch ex As IOException
                If attempt = maxRetries Then
                    Debug.WriteLine($"[NetworkProfileSync] CopyFileWithRetry failed after {maxRetries} attempts for '{sourceFile}': {ex.Message}")
                    Exit For
                End If
                System.Threading.Thread.Sleep(100 * attempt)
            Catch ex As Exception
                Debug.WriteLine($"[NetworkProfileSync] CopyFileWithRetry unexpected error for '{sourceFile}': {ex.Message}")
                Exit For
            End Try
        Next
    End Sub

    ''' <summary>
    ''' Sincronizza il profilo master dal drive di rete allo staging locale prima dell'avvio di WebView2.
    ''' </summary>
    Public Shared Async Function SyncMasterToLocalStagingAsync(accountId As String) As Task(Of Boolean)
        Dim masterDir = GetMasterProfileDir(accountId)
        Dim stagingDir = GetLocalStagingProfileDir(accountId)

        If Not Directory.Exists(masterDir) Then
            Debug.WriteLine($"[NetworkProfileSync] Master profile does not exist yet: {masterDir}")
            Return False
        End If

        Debug.WriteLine($"[NetworkProfileSync] Sincronizzazione Master -> Staging per account {accountId}...")
        Dim sw = Stopwatch.StartNew()
        Dim result = Await SyncDirectoryAsync(masterDir, stagingDir, isMirror:=False)
        sw.Stop()
        Debug.WriteLine($"[NetworkProfileSync] Completata sincronizzazione Master -> Staging per {accountId} in {sw.ElapsedMilliseconds} ms (Esito: {result})")
        Return result
    End Function

    ''' <summary>
    ''' Sincronizza il profilo aggiornato dallo staging locale al master su drive di rete alla chiusura dell'applicazione o periodicamente.
    ''' </summary>
    Public Shared Async Function SyncLocalStagingToMasterAsync(accountId As String) As Task(Of Boolean)
        Dim stagingDir = GetLocalStagingProfileDir(accountId)
        Dim masterDir = GetMasterProfileDir(accountId)

        If Not Directory.Exists(stagingDir) Then
            Return False
        End If

        Debug.WriteLine($"[NetworkProfileSync] Sincronizzazione Staging -> Master per account {accountId}...")
        Dim sw = Stopwatch.StartNew()
        Dim result = Await SyncDirectoryAsync(stagingDir, masterDir, isMirror:=True)
        sw.Stop()
        Debug.WriteLine($"[NetworkProfileSync] Completata sincronizzazione Staging -> Master per {accountId} in {sw.ElapsedMilliseconds} ms (Esito: {result})")
        Return result
    End Function

    ''' <summary>
    ''' Rimuove i dati di staging locale per un account eliminato.
    ''' </summary>
    Public Shared Sub CleanLocalStaging(accountId As String)
        Try
            Dim stagingDir = GetLocalStagingProfileDir(accountId)
            If Directory.Exists(stagingDir) Then
                Directory.Delete(stagingDir, recursive:=True)
                Debug.WriteLine($"[NetworkProfileSync] Rimossa cartella di staging locale per {accountId}: {stagingDir}")
            End If
        Catch ex As Exception
            Debug.WriteLine($"[NetworkProfileSync] CleanLocalStaging error: {ex.Message}")
        End Try
    End Sub

    ' --- Gestione Lock Sessione Multi-PC per percorsi di rete (TODO #70) ---

    Private Class SessionLockInfo
        <JsonPropertyName("machine")>
        Public Property Machine As String
        <JsonPropertyName("user")>
        Public Property User As String
        <JsonPropertyName("pid")>
        Public Property Pid As Integer
        <JsonPropertyName("timestampUtc")>
        Public Property TimestampUtc As DateTime
    End Class

    ''' <summary>
    ''' Tenta di acquisire il file di lock della sessione sulla share di rete.
    ''' Restituisce il nome della macchina in conflitto se l'account è già aperto su un altro PC, altrimenti Nothing.
    ''' </summary>
    Public Shared Function AcquireSessionLock(accountId As String) As String
        Try
            Dim masterDir = GetMasterProfileDir(accountId)
            If Not Directory.Exists(masterDir) Then
                Directory.CreateDirectory(masterDir)
            End If

            Dim lockFile = Path.Combine(masterDir, ".active_session")
            If File.Exists(lockFile) Then
                Try
                    Dim json = File.ReadAllText(lockFile)
                    Dim existing = JsonSerializer.Deserialize(Of SessionLockInfo)(json)
                    If existing IsNot Nothing Then
                        Dim isDifferentMachine = Not String.Equals(existing.Machine, Environment.MachineName, StringComparison.OrdinalIgnoreCase)
                        ' Se il timestamp è recente (< 3 minuti) ed appartiene ad un altro computer
                        If isDifferentMachine AndAlso (DateTime.UtcNow - existing.TimestampUtc) < TimeSpan.FromMinutes(3) Then
                            Return existing.Machine
                        End If
                    End If
                Catch
                End Try
            End If

            ' Scrive o rinnova il file di lock per la macchina corrente
            Dim info As New SessionLockInfo With {
                .Machine = Environment.MachineName,
                .User = Environment.UserName,
                .Pid = Process.GetCurrentProcess().Id,
                .TimestampUtc = DateTime.UtcNow
            }
            Dim serialized = JsonSerializer.Serialize(info)
            File.WriteAllText(lockFile, serialized)
        Catch ex As Exception
            Debug.WriteLine($"[NetworkProfileSync] AcquireSessionLock error: {ex.Message}")
        End Try
        Return Nothing
    End Function

    ''' <summary>
    ''' Rilascia il file di lock sulla share di rete se appartiene al processo corrente.
    ''' </summary>
    Public Shared Sub ReleaseSessionLock(accountId As String)
        Try
            Dim masterDir = GetMasterProfileDir(accountId)
            Dim lockFile = Path.Combine(masterDir, ".active_session")
            If File.Exists(lockFile) Then
                Try
                    Dim json = File.ReadAllText(lockFile)
                    Dim existing = JsonSerializer.Deserialize(Of SessionLockInfo)(json)
                    If existing IsNot Nothing AndAlso String.Equals(existing.Machine, Environment.MachineName, StringComparison.OrdinalIgnoreCase) Then
                        File.Delete(lockFile)
                        Debug.WriteLine($"[NetworkProfileSync] Rilasciato session lock per account {accountId}")
                    End If
                Catch
                    File.Delete(lockFile)
                End Try
            End If
        Catch ex As Exception
            Debug.WriteLine($"[NetworkProfileSync] ReleaseSessionLock error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Aggiorna il timestamp di heartbeat del file di lock sulla share di rete per confermare l'attività del processo.
    ''' </summary>
    Public Shared Sub HeartbeatSessionLock(accountId As String)
        Try
            Dim masterDir = GetMasterProfileDir(accountId)
            Dim lockFile = Path.Combine(masterDir, ".active_session")
            If File.Exists(lockFile) Then
                Dim info As New SessionLockInfo With {
                    .Machine = Environment.MachineName,
                    .User = Environment.UserName,
                    .Pid = Process.GetCurrentProcess().Id,
                    .TimestampUtc = DateTime.UtcNow
                }
                File.WriteAllText(lockFile, JsonSerializer.Serialize(info))
            End If
        Catch
        End Try
    End Sub

End Class
