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
    ''' Se isPeriodic è True, esclude categoricamente le cartelle LevelDB attive (IndexedDB e Local Storage) per evitare
    ''' lock contention e l'errore QuotaExceededError in Chromium mentre il browser è attivo.
    ''' </summary>
    Public Shared Function ShouldSkipRelativePath(relativePath As String, Optional isPeriodic As Boolean = False) As Boolean
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

        ' Durante la sincronizzazione periodica a caldo (mentre WebView2 è in esecuzione):
        ' ESCLUSIONE CRITICA LEVELDB: IndexedDB, Local Storage e Session Storage.
        ' La lettura sequenziale dei file .ldb e .log su share SMB con latenza di rete tiene aperti lock di lettura
        ' per minuti, provocando in Chromium: QuotaExceededError (AbortError) - dropping db read operation due to logout.
        ' Lo staging locale su SSD è la fonte primaria affidabile 24/7; il mirror integrale su master avviene alla chiusura.
        If isPeriodic Then
            Dim liveDbPrefixes As String() = {
                "EBWebView\Default\IndexedDB",
                "EBWebView\Default\Local Storage",
                "EBWebView\Default\Session Storage"
            }
            For Each prefix In liveDbPrefixes
                If normalized.Equals(prefix, StringComparison.OrdinalIgnoreCase) OrElse
                   normalized.StartsWith(prefix & "\", StringComparison.OrdinalIgnoreCase) Then
                    Return True
                End If
            Next
        End If

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
    ''' Se onlyIfSourceNewer è True, non sovrascrive mai file di destinazione che risultano più recenti della sorgente.
    ''' </summary>
    Public Shared Async Function SyncDirectoryAsync(sourceDir As String, targetDir As String, isMirror As Boolean, Optional onlyIfSourceNewer As Boolean = False, Optional isPeriodic As Boolean = False) As Task(Of Boolean)
        If String.IsNullOrWhiteSpace(sourceDir) OrElse Not Directory.Exists(sourceDir) Then Return False

        Return Await Task.Run(Function() As Boolean
            Try
                If Not Directory.Exists(targetDir) Then
                    Directory.CreateDirectory(targetDir)
                End If

                Dim sourceDirUri = New Uri(sourceDir.TrimEnd(Path.DirectorySeparatorChar) & Path.DirectorySeparatorChar)

                ' Indicizza preliminarmente i file esistenti nella destinazione per evitare migliaia di query di rete (File.Exists / FileInfo) individuali su SMB
                Dim dstMap As New Dictionary(Of String, (Length As Long, LastWriteTimeUtc As DateTime))(StringComparer.OrdinalIgnoreCase)
                If Directory.Exists(targetDir) Then
                    Dim targetDirUri = New Uri(targetDir.TrimEnd(Path.DirectorySeparatorChar) & Path.DirectorySeparatorChar)
                    For Each dstFile In Directory.EnumerateFiles(targetDir, "*", SearchOption.AllDirectories)
                        Try
                            Dim dstUri = New Uri(dstFile)
                            Dim relPath = Uri.UnescapeDataString(targetDirUri.MakeRelativeUri(dstUri).ToString().Replace("/"c, "\"c))
                            If Not ShouldSkipRelativePath(relPath, isPeriodic) Then
                                Dim fi As New FileInfo(dstFile)
                                dstMap(relPath) = (fi.Length, fi.LastWriteTimeUtc)
                            End If
                        Catch
                        End Try
                    Next
                End If

                ' 1. Copia incrementale di file nuovi o modificati
                For Each srcFile In Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories)
                    Try
                        Dim srcUri = New Uri(srcFile)
                        Dim relPath = Uri.UnescapeDataString(sourceDirUri.MakeRelativeUri(srcUri).ToString().Replace("/"c, "\"c))

                        If ShouldSkipRelativePath(relPath, isPeriodic) Then Continue For

                        Dim dstFile = Path.Combine(targetDir, relPath)
                        Dim dstDir = Path.GetDirectoryName(dstFile)
                        If Not String.IsNullOrEmpty(dstDir) AndAlso Not Directory.Exists(dstDir) Then
                            Directory.CreateDirectory(dstDir)
                        End If

                        Dim needCopy = True
                        Dim dstInfo As (Length As Long, LastWriteTimeUtc As DateTime) = Nothing
                        If dstMap.TryGetValue(relPath, dstInfo) Then
                            Dim srcInfo As New FileInfo(srcFile)
                            If srcInfo.Length = dstInfo.Length AndAlso Math.Abs((srcInfo.LastWriteTimeUtc - dstInfo.LastWriteTimeUtc).TotalSeconds) < 2 Then
                                needCopy = False
                            ElseIf onlyIfSourceNewer AndAlso srcInfo.LastWriteTimeUtc <= dstInfo.LastWriteTimeUtc Then
                                ' Non sovrascrivere mai file di destinazione più recenti con file sorgente obsoleti
                                needCopy = False
                            End If
                        End If

                        If needCopy Then
                            CopyFileWithRetry(srcFile, dstFile, maxRetries:=If(isPeriodic, 3, 6), isPeriodic:=isPeriodic)
                        End If
                    Catch exFile As Exception
                        Debug.WriteLine($"[NetworkProfileSync] SyncDirectory file copy warning ({srcFile}): {exFile.Message}")
                    End Try
                Next

                ' 2. Pulizia mirror (rimozione file orfani nel target non più presenti in sorgente)
                If isMirror AndAlso dstMap.Count > 0 Then
                    For Each relPath In dstMap.Keys
                        Try
                            Dim srcFile = Path.Combine(sourceDir, relPath)
                            If Not File.Exists(srcFile) Then
                                Dim dstFile = Path.Combine(targetDir, relPath)
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
    Private Shared Sub CopyFileWithRetry(sourceFile As String, destFile As String, maxRetries As Integer, Optional isPeriodic As Boolean = False)
        For attempt = 1 To maxRetries
            Try
                ' CRITICO: FileShare.ReadWrite Or FileShare.Delete
                ' L'aggiunta di FileShare.Delete è indispensabile: permette a Chromium LevelDB di eliminare o rinominare
                ' i file SST (.ldb) e i file di log (.log) durante la compattazione in background,
                ' prevenendo l'errore di condivisione ERROR_SHARING_VIOLATION (0x20) che corrompeva la sessione di WhatsApp.
                Using srcStream As New FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete)
                    Using dstStream As New FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite Or FileShare.Delete)
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
                System.Threading.Thread.Sleep(If(isPeriodic, 30 * attempt, 100 * attempt))
            Catch ex As UnauthorizedAccessException
                If attempt = maxRetries Then
                    Debug.WriteLine($"[NetworkProfileSync] CopyFileWithRetry access denied after {maxRetries} attempts for '{sourceFile}': {ex.Message}")
                    Exit For
                End If
                System.Threading.Thread.Sleep(If(isPeriodic, 30 * attempt, 100 * attempt))
            Catch ex As Exception
                Debug.WriteLine($"[NetworkProfileSync] CopyFileWithRetry unexpected error for '{sourceFile}': {ex.Message}")
                Exit For
            End Try
        Next
    End Sub

    ''' <summary>
    ''' Calcola la dimensione totale in byte dei database IndexedDB e Local Storage del profilo specificato.
    ''' </summary>
    Public Shared Function GetProfileStorageSize(profileDir As String) As Long
        Try
            If Not Directory.Exists(profileDir) Then Return 0
            Dim totalBytes As Long = 0
            Dim targetDirs = {
                Path.Combine(profileDir, "EBWebView\Default\IndexedDB"),
                Path.Combine(profileDir, "EBWebView\Default\Local Storage")
            }
            For Each d In targetDirs
                If Directory.Exists(d) Then
                    For Each f In Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories)
                        Try
                            Dim fi As New FileInfo(f)
                            totalBytes += fi.Length
                        Catch
                        End Try
                    Next
                End If
            Next
            Return totalBytes
        Catch
            Return 0
        End Try
    End Function

    ''' <summary>
    ''' Percorso della cartella snapshot Last-Known-Good per un account (data/webview/WV2Profile_{id}_Snapshot).
    ''' </summary>
    Public Shared Function GetSnapshotProfileDir(accountId As String) As String
        Return Path.Combine(AppAccounts.SharedDataDirectory, $"WV2Profile_{accountId}_Snapshot")
    End Function

    ''' <summary>
    ''' Crea o aggiorna lo snapshot Last-Known-Good solo se il profilo contiene una sessione sana (> 2 MB).
    ''' </summary>
    Public Shared Async Function CreateSnapshotIfHealthyAsync(sourceDir As String, accountId As String) As Task(Of Boolean)
        Try
            Dim size = GetProfileStorageSize(sourceDir)
            If size < 2 * 1024 * 1024 Then
                Return False
            End If

            Dim snapshotDir = GetSnapshotProfileDir(accountId)
            AppLogger.LogSync($"Creazione/aggiornamento Snapshot Last-Known-Good per {accountId} (Dimensione: {size \ 1024} KB)...")
            Dim result = Await SyncDirectoryAsync(sourceDir, snapshotDir, isMirror:=True, onlyIfSourceNewer:=False)
            If result Then
                AppLogger.LogSync($"Snapshot Last-Known-Good completato per {accountId}")
            End If
            Return result
        Catch ex As Exception
            AppLogger.LogSync($"Errore creazione snapshot per {accountId}: {ex.Message}")
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Sincronizza il profilo master dal drive di rete allo staging locale prima dell'avvio di WebView2.
    ''' Protegge la sessione locale se già esistente, evitando sovrascritture da master obsoleto.
    ''' </summary>
    Public Shared Async Function SyncMasterToLocalStagingAsync(accountId As String) As Task(Of Boolean)
        Dim masterDir = GetMasterProfileDir(accountId)
        Dim stagingDir = GetLocalStagingProfileDir(accountId)
        Dim snapshotDir = GetSnapshotProfileDir(accountId)

        Dim masterExists = Directory.Exists(masterDir)
        Dim stagingExists = Directory.Exists(stagingDir)
        Dim snapshotExists = Directory.Exists(snapshotDir)

        Dim masterSize = If(masterExists, GetProfileStorageSize(masterDir), 0L)
        Dim stagingSize = If(stagingExists, GetProfileStorageSize(stagingDir), 0L)
        Dim snapshotSize = If(snapshotExists, GetProfileStorageSize(snapshotDir), 0L)

        AppLogger.LogSync($"SyncMasterToLocalStaging [{accountId}] Inizio verifica: Master={masterSize \ 1024} KB, Staging={stagingSize \ 1024} KB, Snapshot={snapshotSize \ 1024} KB")

        ' Caso 1: Staging locale è vuoto o collassato (< 500 KB) mentre Master ha una sessione (> 2 MB)
        If stagingSize < 500 * 1024 AndAlso masterSize >= 2 * 1024 * 1024 Then
            AppLogger.LogSync($"SyncMasterToLocalStaging [{accountId}] Staging vuoto o non integro ({stagingSize \ 1024} KB). Ripristino completo da Master ({masterSize \ 1024} KB)...")
            Dim sw = Stopwatch.StartNew()
            Dim res = Await SyncDirectoryAsync(masterDir, stagingDir, isMirror:=False, onlyIfSourceNewer:=False)
            sw.Stop()
            AppLogger.LogSync($"SyncMasterToLocalStaging [{accountId}] Ripristino Master -> Staging completato in {sw.ElapsedMilliseconds} ms (Esito: {res})")
            Return res
        End If

        ' Caso 2: Sia Staging che Master sono collassati (< 500 KB) MA Snapshot ha sessione sana (> 2 MB)
        If stagingSize < 500 * 1024 AndAlso masterSize < 500 * 1024 AndAlso snapshotSize >= 2 * 1024 * 1024 Then
            AppLogger.LogSync($"SyncMasterToLocalStaging [{accountId}] ALLARME: Master e Staging entrambi collassati! Ripristino automatico da Snapshot Last-Known-Good ({snapshotSize \ 1024} KB)...")
            Await SyncDirectoryAsync(snapshotDir, stagingDir, isMirror:=False, onlyIfSourceNewer:=False)
            Await SyncDirectoryAsync(snapshotDir, masterDir, isMirror:=False, onlyIfSourceNewer:=False)
            Return True
        End If

        ' Caso 3: Master esiste e Staging esiste
        If masterExists Then
            AppLogger.LogSync($"SyncMasterToLocalStaging [{accountId}] Sincronizzazione incrementale protetta (onlyIfSourceNewer:=True)...")
            Dim sw = Stopwatch.StartNew()
            ' Preserva file locali recenti ma scarica eventuali file mancanti o più recenti dal Master
            Dim res = Await SyncDirectoryAsync(masterDir, stagingDir, isMirror:=False, onlyIfSourceNewer:=True)
            sw.Stop()
            AppLogger.LogSync($"SyncMasterToLocalStaging [{accountId}] Completata in {sw.ElapsedMilliseconds} ms (Esito: {res})")
            Return res
        End If

        Return True
    End Function

    ''' <summary>
    ''' Sincronizza il profilo aggiornato dallo staging locale al master su drive di rete alla chiusura dell'applicazione o periodicamente.
    ''' Se isPeriodic è True, non elimina file dal master (isMirror:=False) a meno che il master non sia oltre il doppio dello staging sano,
    ''' per evitare l'accumulo di vecchi file .ldb orfani durante sessioni attive 24/7.
    ''' </summary>
    Public Shared Async Function SyncLocalStagingToMasterAsync(accountId As String, Optional isPeriodic As Boolean = False) As Task(Of Boolean)
        Dim acquired = Await _syncLock.WaitAsync(If(isPeriodic, 0, 30000))
        If Not acquired Then
            If isPeriodic Then
                AppLogger.LogSync($"SyncLocalStagingToMaster [{accountId}] Sincronizzazione periodica saltata: un'altra operazione di sync è già attiva.")
                Return False
            Else
                AppLogger.LogSync($"SyncLocalStagingToMaster [{accountId}] Attesa lock sincronizzazione scaduta (30s).")
                Return False
            End If
        End If

        Try
            Dim stagingDir = GetLocalStagingProfileDir(accountId)
            Dim masterDir = GetMasterProfileDir(accountId)

            If Not Directory.Exists(stagingDir) Then
                Return False
            End If

            Dim stagingSize = GetProfileStorageSize(stagingDir)
            Dim masterSize = If(Directory.Exists(masterDir), GetProfileStorageSize(masterDir), 0L)

            AppLogger.LogSync($"SyncLocalStagingToMaster [{accountId}] (isPeriodic={isPeriodic}) Staging: {stagingSize \ 1024} KB, Master: {masterSize \ 1024} KB")

            ' Gestione mirror: alla chiusura sempre mirror. Nel periodico, attiva mirror solo se il master si è gonfiato oltre il doppio dello staging sano (> 2MB)
            Dim allowMirror = (Not isPeriodic)
            If isPeriodic AndAlso stagingSize >= 2 * 1024 * 1024 AndAlso masterSize > (stagingSize * 2) Then
                allowMirror = True
                AppLogger.LogSync($"SyncLocalStagingToMaster [{accountId}] Pulizia mirror periodica attivata: Master ({masterSize \ 1024} KB) supera 2x Staging ({stagingSize \ 1024} KB).")
            End If

            ' PROTEZIONE CRITICA COLLASSO:
            ' Se Master conteneva una sessione valida (> 2 MB) e Staging è improvvisamente crollato a < 500 KB (es. logout imprevisto o cancellazione tabelle LevelDB):
            ' NON eseguire MAI il mirror distruttivo che cancellerebbe i file sul Master!
            If allowMirror AndAlso masterSize >= 2 * 1024 * 1024 AndAlso stagingSize < 500 * 1024 Then
                allowMirror = False
                AppLogger.LogSync($"[ALLARME SICUREZZA] Account {accountId}: Staging collassato a {stagingSize \ 1024} KB mentre Master ha {masterSize \ 1024} KB. Mirror disattivato per proteggere il Master!")
                AppLogger.Log("whatsapp_errors.log", "STAGING_COLLAPSE", $"[ACCOUNT: {accountId}] Staging storage collassato a {stagingSize \ 1024} KB (Master: {masterSize \ 1024} KB). Rifiutata eliminazione mirror sul Master.")
            End If

            Dim sw = Stopwatch.StartNew()
            Dim result = Await SyncDirectoryAsync(stagingDir, masterDir, isMirror:=allowMirror, isPeriodic:=isPeriodic)
            sw.Stop()
            AppLogger.LogSync($"SyncLocalStagingToMaster [{accountId}] Completata in {sw.ElapsedMilliseconds} ms (Esito: {result})")

            ' Se la sessione locale è sana (> 2 MB), aggiorna lo snapshot Last-Known-Good SOLO alla chiusura dell'applicazione
            ' MAI durante la sincronizzazione periodica a runtime (evita copia massiva da 33 minuti su SMB che intasa il disco di rete)
            If Not isPeriodic AndAlso stagingSize >= 2 * 1024 * 1024 Then
                Await CreateSnapshotIfHealthyAsync(stagingDir, accountId)
            End If

            Return result
        Finally
            _syncLock.Release()
        End Try
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
