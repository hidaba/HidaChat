Imports System.Threading.Tasks
Imports System.IO
Imports System.Text.Json
Imports System.ComponentModel
Imports System.Collections.ObjectModel
Imports System.Runtime.CompilerServices

''' <summary>

''' Gestisce la collezione di account WhatsApp, la selezione dell'account attivo, 
''' il caricamento/salvataggio delle preferenze su file JSON e la pulizia delle cartelle di profilo.
''' </summary>
Public Class AccountManager
    Implements INotifyPropertyChanged, IDisposable

    Public Const DefaultMaxAccounts As Integer = 5
    Public Const AbsoluteMaxAccounts As Integer = 10

    ''' <summary>
    ''' Limite massimo di account configurabili contemporaneamente (configurabile nelle impostazioni, default 5, max 10).
    ''' </summary>
    Public ReadOnly Property MaxAccounts As Integer
        Get
            If _settingsController IsNot Nothing AndAlso _settingsController.MaxAccounts > 0 Then
                Return Math.Clamp(_settingsController.MaxAccounts, 2, AbsoluteMaxAccounts)
            End If
            Return DefaultMaxAccounts
        End Get
    End Property

    ''' <summary>
    ''' Riferimento all'istanza singleton di CoreWebView2Environment condivisa da tutti gli account (TODO #103).
    ''' </summary>
    Public ReadOnly Property SharedEnvironment As Microsoft.Web.WebView2.Core.CoreWebView2Environment
        Get
            Return AppAccounts.SharedEnvironment
        End Get
    End Property

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Public Sub NotifyPropertyChanged(<CallerMemberName> Optional propertyName As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

    Private ReadOnly _settingsController As SettingsController
    Private _isDirty As Boolean = False

    ''' <summary>
    ''' Indica se è possibile aggiungere un nuovo account (limite massimo configurato non ancora raggiunto).
    ''' In caso di downgrade, l'aggiunta di ulteriori account è bloccata finché il totale non scende sotto MaxAccounts.
    ''' </summary>
    Public ReadOnly Property CanAddAccount As Boolean
        Get
            Return _accounts IsNot Nothing AndAlso _accounts.Count < MaxAccounts
        End Get
    End Property

    ''' <summary>
    ''' Indica se il numero di account attualmente configurati supera il limite massimo impostato (stato di downgrade).
    ''' In caso di downgrade, gli account esistenti vengono preservati senza perdite, ma è bloccata l'aggiunta di ulteriori account.
    ''' </summary>
    Public ReadOnly Property HasExcessAccounts As Boolean
        Get
            Return _accounts IsNot Nothing AndAlso _accounts.Count > MaxAccounts
        End Get
    End Property

    ''' <summary>
    ''' Numero di account configurati che eccedono il limite massimo attualmente impostato.
    ''' </summary>
    Public ReadOnly Property ExcessAccountsCount As Integer
        Get
            If _accounts Is Nothing Then Return 0
            Return Math.Max(0, _accounts.Count - MaxAccounts)
        End Get
    End Property

    Private _accounts As New ObservableCollection(Of AppAccounts)()

    ''' <summary>
    ''' Collezione osservabile di tutti gli account WhatsApp configurati.
    ''' </summary>
    Public Property Accounts As ObservableCollection(Of AppAccounts)
        Get
            Return _accounts
        End Get
        Set(value As ObservableCollection(Of AppAccounts))
            _accounts = value
            _isDirty = True
            NotifyPropertyChanged()
            NotifyPropertyChanged(NameOf(CanAddAccount))
            NotifyPropertyChanged(NameOf(HasExcessAccounts))
            NotifyPropertyChanged(NameOf(ExcessAccountsCount))
        End Set
    End Property

    Private _currentAccount As AppAccounts

    ''' <summary>
    ''' Account WhatsApp attualmente selezionato e visualizzato nell'interfaccia.
    ''' </summary>
    Public Property CurrentAccount As AppAccounts
        Get
            Return _currentAccount
        End Get
        Set(value As AppAccounts)
            If _currentAccount IsNot value Then
                _currentAccount = value
                _isDirty = True
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    Private _hasAnyNotification As Boolean = False

    ''' <summary>
    ''' Indica se almeno un account ha notifiche attive non lette.
    ''' </summary>
    Public Property HasAnyNotification As Boolean
        Get
            Return _hasAnyNotification
        End Get
        Set(value As Boolean)
            If _hasAnyNotification <> value Then
                _hasAnyNotification = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    Private _isDialogOpen As Boolean = False

    ''' <summary>
    ''' Indica se una finestra di dialogo modale (es. Impostazioni) è attualmente aperta.
    ''' </summary>
    Public Property IsDialogOpen As Boolean
        Get
            Return _isDialogOpen
        End Get
        Set(value As Boolean)
            If _isDialogOpen <> value Then
                _isDialogOpen = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    Public Sub New(settingsController As SettingsController)
        Me._settingsController = settingsController
        If _settingsController IsNot Nothing Then
            AddHandler _settingsController.PropertyChanged, AddressOf OnSettingsPropertyChanged
        End If
    End Sub

    ''' <summary>
    ''' Sincronizza lo stato di AccountManager quando cambiano le impostazioni pertinenti (es. MaxAccounts).
    ''' </summary>
    Private Sub OnSettingsPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If String.IsNullOrEmpty(e?.PropertyName) OrElse e.PropertyName = NameOf(SettingsController.MaxAccounts) Then
            NotifyPropertyChanged(NameOf(MaxAccounts))
            NotifyPropertyChanged(NameOf(CanAddAccount))
            NotifyPropertyChanged(NameOf(HasExcessAccounts))
            NotifyPropertyChanged(NameOf(ExcessAccountsCount))
        End If
    End Sub

    ''' <summary>
    ''' Carica in modo asincrono la lista degli account salvata nelle impostazioni.
    ''' Se nessun account è memorizzato, crea un account predefinito.
    ''' </summary>
    Public Async Function LoadAccountsAsync() As Task
        ' Abilita preventivamente la compressione trasparente NTFS sulla cartella profili WebView2 (TODO #106)
        EnableNtfsCompression(AppAccounts.SharedDataDirectory)
        Dim settings = Await _settingsController.ReadSettingsAsync()
        
        Dim isConfigValid = _settingsController.IsLoadedFromValidConfig
        Dim accountsListObj As Object = Nothing
        If isConfigValid AndAlso settings.TryGetValue("accounts", accountsListObj) Then
            Try
                Dim accountsJson = accountsListObj.ToString()
                Dim jsonOptions As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}
                Dim accountsData = JsonSerializer.Deserialize(Of List(Of AppAccounts))(accountsJson, jsonOptions)
                
                If accountsData IsNot Nothing AndAlso accountsData.Count > 0 Then
                    Dim needsSave = False
                    For i As Integer = 0 To accountsData.Count - 1
                        If String.IsNullOrEmpty(accountsData(i).Id) Then
                            accountsData(i).Id = AppAccounts.GenerateId()
                            needsSave = True
                        End If
                        If String.IsNullOrEmpty(accountsData(i).Name) Then
                            accountsData(i).Name = $"Account {i + 1}"
                            needsSave = True
                        End If
                    Next

                    Dim usedPorts As New HashSet(Of Integer)()
                    For Each acc In accountsData
                        If acc.LocalProxyPort > 0 Then
                            usedPorts.Add(acc.LocalProxyPort)
                        End If
                    Next

                    For Each acc In accountsData
                        If acc.PlatformDefinition.RequiresLocalProxy Then
                            If acc.LocalProxyPort <= 0 Then
                                Dim targetPort = acc.PlatformDefinition.DefaultPort
                                While usedPorts.Contains(targetPort)
                                    targetPort += 1
                                End While
                                acc.LocalProxyPort = targetPort
                                usedPorts.Add(targetPort)
                                needsSave = True
                            End If
                        End If
                    Next

                    _accounts = New ObservableCollection(Of AppAccounts)(accountsData)

                    Debug.WriteLine($"LoadAccounts: caricati {accountsData.Count} account, needsSave={needsSave}")
                    For i As Integer = 0 To accountsData.Count - 1
                        Debug.WriteLine($"  Account[{i}]: Id='{accountsData(i).Id}', Name='{accountsData(i).Name}', ProxyPort={accountsData(i).LocalProxyPort}, IsActive={accountsData(i).IsActive}")
                    Next

                    MigrateOrphanProfile()
                    MigrateLegacyProfilesToNativeProfiles()

                    If needsSave Then
                        Await SaveAccountsAsync(force:=True)
                    End If

                    ' Pulizia non distruttiva dei profili non referenziati eseguita esclusivamente se la configurazione è valida
                    ' e delegata a un task di background in Task.Run per non bloccare il thread UI all'avvio
                    Dim activeIds = _accounts.Map(Function(a) a.Id).ToList()
                    ' Do not automatically delete profiles based on a possibly incomplete settings list.

                    ' No filesystem cleanup before browser ownership is known.
                    
                    _currentAccount = _accounts.FirstOrDefault(Function(a) a.IsActive)
                    If _currentAccount Is Nothing AndAlso _accounts.Count > 0 Then
                        _currentAccount = _accounts.First()
                        _currentAccount.IsActive = True
                    End If
                    
                    _isDirty = False
                    NotifyPropertyChanged(NameOf(Accounts))
                    NotifyPropertyChanged(NameOf(CurrentAccount))
                    NotifyPropertyChanged(NameOf(CanAddAccount))
                    NotifyPropertyChanged(NameOf(HasExcessAccounts))
                    NotifyPropertyChanged(NameOf(ExcessAccountsCount))
                    Return
                End If
            Catch ex As Exception
                Debug.WriteLine($"Error deserializing accounts: {ex.Message}")
            End Try
        End If

        Await CreateDefaultAccountAsync()
    End Function

    ''' <summary>
    ''' Elimina ricorsivamente una cartella di profilo eseguendo tentativi multipli con backoff progressivo 
    ''' per attendere il rilascio di eventuali lock su file da parte del processo WebView2/Chromium o antivirus.
    ''' </summary>
    Public Shared Async Function DeleteDirectoryWithRetryAsync(dirPath As String, Optional maxAttempts As Integer = 5) As Task(Of Boolean)
        If String.IsNullOrWhiteSpace(dirPath) OrElse Not Directory.Exists(dirPath) Then
            Return True
        End If

        For attempt As Integer = 1 To maxAttempts
            Try
                If attempt > 1 Then
                    Await Task.Delay(attempt * 200)
                End If

                If Directory.Exists(dirPath) Then
                    Directory.Delete(dirPath, True)
                End If
                Debug.WriteLine($"DeleteDirectoryWithRetryAsync: eliminata con successo '{dirPath}' (tentativo {attempt}/{maxAttempts})")
                Return True
            Catch ex As Exception
                Debug.WriteLine($"DeleteDirectoryWithRetryAsync: tentativo {attempt}/{maxAttempts} fallito per '{dirPath}': {ex.Message}")
            End Try
        Next

        Return Not Directory.Exists(dirPath)
    End Function

    ''' <summary>
    ''' Verifica la presenza di una cartella di profilo WebView2 orfana (creata senza ID specificato) e la riassocia al primo account,
    ''' preservando in modo non distruttivo il profilo esistente rinominandolo in .bak anziché cancellarlo preventivamente.
    ''' </summary>
    Private Sub MigrateOrphanProfile()
        Try
            Dim orphan = Path.Combine(AppAccounts.SharedDataDirectory, "WV2Profile_")
            If Not Directory.Exists(orphan) Then Return
            ' An anonymous profile cannot be attributed safely among multiple accounts.
            If _accounts.Count <> 1 Then
                AppLogger.LogApp("[ORPHAN_PRESERVED] Multiple accounts; manual attribution required.")
                Return
            End If
            Dim target = NetworkProfileSync.GetMasterProfileDir(_accounts(0).Id)
            If Directory.Exists(target) OrElse Directory.Exists(target & ".previous") Then
                AppLogger.LogApp("[ORPHAN_PRESERVED] Existing account profile must never be replaced.")
                Return
            End If
            Directory.Move(orphan, target)
        Catch ex As Exception
            AppLogger.LogApp($"[ORPHAN_MIGRATION_FAILED] {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Migra in modo non distruttivo e ripristina i profili (legacy o v1.3.5-beta)
    ''' nella struttura corretta dei profili nativi di WebView2 (data/webview/EBWebView/WV2Profile_{id}) (TODO #103).
    ''' </summary>
    Private Sub MigrateLegacyProfilesToNativeProfiles()
        Try
            Dim sharedDir = AppAccounts.SharedDataDirectory
            If Not Directory.Exists(sharedDir) Then Return

            Dim ebWebViewDir = Path.Combine(sharedDir, ProfileIsolationPolicy.SharedEbWebViewFolder)
            If Not Directory.Exists(ebWebViewDir) Then
                Directory.CreateDirectory(ebWebViewDir)
            End If

            ' 1. Sincronizza o ripristina la chiave master os_crypt.encrypted_key in EBWebView\Local State
            MigrateOrRestoreLocalStateEncryptionKey(sharedDir, ebWebViewDir)

            ' 2. Per ogni account registrato, assicurati che la cartella nativa WV2Profile_ abbia tutti i dati
            For Each acc In _accounts
                Dim targetProfileDir = ProfileIsolationPolicy.GetNativeProfileDirectory(sharedDir, acc.Id)
                If String.IsNullOrEmpty(targetProfileDir) Then Continue For

                ' Cerca tutte le possibili sorgenti contenenti dati di sessione (in ordine di priorità)
                Dim candidateSources As New List(Of String)

                ' A) Sorgente da v1.3.5-beta: EBWebView\Account_{id} o EBWebView\{ProfileName}
                Dim betaDir1 = Path.Combine(ebWebViewDir, acc.ProfileName)
                If Directory.Exists(betaDir1) AndAlso Not String.Equals(betaDir1, targetProfileDir, StringComparison.OrdinalIgnoreCase) Then
                    candidateSources.Add(betaDir1)
                End If
                Dim betaDir2 = Path.Combine(ebWebViewDir, "Account_" & acc.Id)
                If Directory.Exists(betaDir2) AndAlso Not String.Equals(betaDir2, targetProfileDir, StringComparison.OrdinalIgnoreCase) Then
                    candidateSources.Add(betaDir2)
                End If

                ' B) Sorgente legacy pura: data/webview/WV2Profile_{id}
                Dim legacyDir = Path.Combine(sharedDir, $"{ProfileIsolationPolicy.LegacyProfilePrefix}{acc.Id}")
                If Directory.Exists(legacyDir) Then candidateSources.Add(legacyDir)

                ' C) Sorgente legacy rinominata: data/webview/WV2Profile_{id}.migrated
                Dim migratedDir = legacyDir & ".migrated"
                If Directory.Exists(migratedDir) Then candidateSources.Add(migratedDir)

                ' D) Sorgente legacy backup: data/webview/WV2Profile_{id}.previous o _Snapshot
                Dim prevDir = legacyDir & ".previous"
                If Directory.Exists(prevDir) Then candidateSources.Add(prevDir)
                Dim snapDir = legacyDir & "_Snapshot"
                If Directory.Exists(snapDir) Then candidateSources.Add(snapDir)

                ' Se targetProfileDir non esiste, crealo
                If Not Directory.Exists(targetProfileDir) Then
                    Directory.CreateDirectory(targetProfileDir)
                End If

                ' Controlla se targetProfileDir ha già dati reali validi (es. IndexedDB consistente)
                Dim targetHasRealData = HasSubstantialProfileData(targetProfileDir)

                For Each srcCandidate In candidateSources
                    Dim dataDir = ResolveDataDirectoryInsideSource(srcCandidate)
                    If String.IsNullOrEmpty(dataDir) OrElse Not Directory.Exists(dataDir) Then Continue For
                    If Not HasSubstantialProfileData(dataDir) Then Continue For

                    If Not targetHasRealData Then
                        AppLogger.LogApp($"[PROFILE_MIGRATING] Migrazione dati profilo da '{dataDir}' a '{targetProfileDir}' per account {acc.Id} ({acc.Name})")
                        CopyDirectoryRecursively(dataDir, targetProfileDir)
                        targetHasRealData = True
                        AppLogger.LogApp($"[PROFILE_MIGRATED] Dati ripristinati con successo in '{targetProfileDir}' per account {acc.Id}")
                    End If

                    ' Se la sorgente era la cartella errata EBWebView\Account_{id}, possiamo ripulirla
                    If srcCandidate.StartsWith(ebWebViewDir, StringComparison.OrdinalIgnoreCase) AndAlso
                       Not String.Equals(srcCandidate, targetProfileDir, StringComparison.OrdinalIgnoreCase) Then
                        Try
                            Directory.Delete(srcCandidate, True)
                            AppLogger.LogApp($"[CLEANUP_BETA_DIR] Rimossa directory obsoleta: {srcCandidate}")
                        Catch
                        End Try
                    End If
                Next

                ' Se la directory legacy esiste ancora e targetProfileDir ha dati validi, rinominala in .migrated
                If Directory.Exists(legacyDir) AndAlso targetHasRealData Then
                    Dim backupDir = legacyDir & ".migrated"
                    If Not Directory.Exists(backupDir) Then
                        Try
                            Directory.Move(legacyDir, backupDir)
                        Catch
                        End Try
                    End If
                End If
            Next

            ' 3. Pulizia automatica delle copie di migrazione legacy (.migrated, .previous, _Snapshot, .bak)
            ' una volta verificato che i dati di sessione nativi sono presenti e consistenti (TODO #103)
            CleanupObsoleteMigrationBackups(sharedDir, ebWebViewDir)
        Catch ex As Exception
            AppLogger.LogApp($"[PROFILE_MIGRATION_FATAL] Errore generale MigrateLegacyProfilesToNativeProfiles: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Rimuove in sicurezza le copie di backup e migrazione legacy (.migrated, .previous, _Snapshot, .bak)
    ''' dai profili storici in data/webview/ esclusivamente quando il profilo nativo in EBWebView/
    ''' è verificato e contiene dati di sessione validi (TODO #103).
    ''' Consente di recuperare centinaia di megabyte di spazio su disco duplicato.
    ''' </summary>
    Private Sub CleanupObsoleteMigrationBackups(sharedDir As String, ebWebViewDir As String)
        Try
            If Not Directory.Exists(sharedDir) OrElse Not Directory.Exists(ebWebViewDir) Then Return

            For Each acc In _accounts
                If ProfileIsolationPolicy.CanSafelyPurgeMigrationBackups(sharedDir, acc.Id) Then
                    Dim obsoleteCandidates = ProfileIsolationPolicy.GetObsoleteMigrationBackupDirectories(sharedDir, acc.Id)
                    For Each obs In obsoleteCandidates
                        If Directory.Exists(obs) Then
                            Try
                                Directory.Delete(obs, recursive:=True)
                                AppLogger.LogApp($"[CLEANUP_MIGRATION] Rimossa directory di backup/migrazione obsoleta: {obs}")
                            Catch exObs As Exception
                                AppLogger.LogApp($"[CLEANUP_MIGRATION_WARN] Impossibile rimuovere '{obs}': {exObs.Message}")
                            End Try
                        End If
                    Next
                End If
            Next
        Catch ex As Exception
            AppLogger.LogApp($"[CLEANUP_MIGRATION_ERROR] {ex.Message}")
        End Try
    End Sub

    Private Shared Sub MigrateOrRestoreLocalStateEncryptionKey(sharedDir As String, ebWebViewDir As String)
        Try
            Dim targetLocalState = Path.Combine(ebWebViewDir, "Local State")
            Dim legacyKey As String = Nothing

            ' Cerca la chiave DPAPI nei profili legacy
            If Directory.Exists(sharedDir) Then
                For Each dirPath In Directory.GetDirectories(sharedDir)
                    Dim dName = Path.GetFileName(dirPath)
                    If dName.StartsWith(ProfileIsolationPolicy.LegacyProfilePrefix, StringComparison.OrdinalIgnoreCase) Then
                        Dim candPath = Path.Combine(dirPath, ProfileIsolationPolicy.SharedEbWebViewFolder, "Local State")
                        If File.Exists(candPath) Then
                            legacyKey = ExtractOsCryptEncryptedKey(candPath)
                            If Not String.IsNullOrEmpty(legacyKey) Then Exit For
                        End If
                    End If
                Next
            End If

            If String.IsNullOrEmpty(legacyKey) Then Return

            If Not File.Exists(targetLocalState) Then
                ' Copia un file Local State legacy di riferimento come base iniziale
                For Each dirPath In Directory.GetDirectories(sharedDir)
                    Dim candPath = Path.Combine(dirPath, ProfileIsolationPolicy.SharedEbWebViewFolder, "Local State")
                    If File.Exists(candPath) Then
                        File.Copy(candPath, targetLocalState, True)
                        AppLogger.LogApp($"[LOCAL_STATE_SEEDED] Inizializzato Local State condiviso da '{candPath}'")
                        Return
                    End If
                Next
            Else
                ' Se esiste già, verifica se la chiave corrisponde
                Dim currentKey = ExtractOsCryptEncryptedKey(targetLocalState)
                If Not String.IsNullOrEmpty(currentKey) AndAlso Not String.Equals(currentKey, legacyKey, StringComparison.Ordinal) Then
                    Dim content = File.ReadAllText(targetLocalState, System.Text.Encoding.UTF8)
                    Dim updatedContent = content.Replace($"""encrypted_key"":""{currentKey}""", $"""encrypted_key"":""{legacyKey}""")
                    If Not String.Equals(content, updatedContent, StringComparison.Ordinal) Then
                        File.WriteAllText(targetLocalState, updatedContent, System.Text.Encoding.UTF8)
                        AppLogger.LogApp("[LOCAL_STATE_KEY_RESTORED] Chiave master os_crypt.encrypted_key sincronizzata con i profili legacy per preservare i cookie.")
                    End If
                End If
            End If
        Catch ex As Exception
            AppLogger.LogApp($"[LOCAL_STATE_SYNC_ERROR] {ex.Message}")
        End Try
    End Sub

    Private Shared Function ExtractOsCryptEncryptedKey(localStatePath As String) As String
        Try
            If Not File.Exists(localStatePath) Then Return Nothing
            Dim text = File.ReadAllText(localStatePath, System.Text.Encoding.UTF8)
            Dim m = System.Text.RegularExpressions.Regex.Match(text, """encrypted_key""\s*:\s*""([^""]+)""")
            If m.Success Then Return m.Groups(1).Value
        Catch
        End Try
        Return Nothing
    End Function

    Private Shared Function HasSubstantialProfileData(dirPath As String) As Boolean
        Return ProfileIsolationPolicy.HasSubstantialProfileData(dirPath)
    End Function

    Private Shared Function ResolveDataDirectoryInsideSource(sourceDir As String) As String
        If String.IsNullOrWhiteSpace(sourceDir) OrElse Not Directory.Exists(sourceDir) Then Return Nothing
        Dim ebDefault = Path.Combine(sourceDir, ProfileIsolationPolicy.SharedEbWebViewFolder, "Default")
        If Directory.Exists(ebDefault) Then Return ebDefault
        Dim def = Path.Combine(sourceDir, "Default")
        If Directory.Exists(def) Then Return def
        If Directory.Exists(Path.Combine(sourceDir, "IndexedDB")) Then Return sourceDir
        Return sourceDir
    End Function

    Private Shared Sub CopyDirectoryRecursively(sourceDir As String, targetDir As String)
        Directory.CreateDirectory(targetDir)
        For Each file In Directory.EnumerateFiles(sourceDir)
            Dim destFile = Path.Combine(targetDir, Path.GetFileName(file))
            Try
                System.IO.File.Copy(file, destFile, overwrite:=True)
            Catch
            End Try
        Next
        For Each subDir In Directory.EnumerateDirectories(sourceDir)
            Dim destSubDir = Path.Combine(targetDir, Path.GetFileName(subDir))
            CopyDirectoryRecursively(subDir, destSubDir)
        Next
    End Sub

    ''' <summary>
    ''' Rimuove in modo non distruttivo eventuali profili WebView2 non più associati ad alcun account attivo:
    ''' sposta le cartelle non referenziate nel cestino (data/webview/_trash/) e procede all'eliminazione differita
    ''' con tentativi di retry, eseguendo l'intera scansione in Task.Run fuori dal thread UI per non rallentare l'avvio.
    ''' </summary>
    Private Function CleanupUnusedProfilesAsync(activeIds As List(Of String)) As Task
        Return Task.Run(Async Function()
            Try
                Dim sharedDir = AppAccounts.SharedDataDirectory
                If Not Directory.Exists(sharedDir) Then Return

                Dim trashDir = Path.Combine(sharedDir, "_trash")
                Dim activeSet As New HashSet(Of String)(If(activeIds, New List(Of String)()), StringComparer.OrdinalIgnoreCase)

                ' 1. Identifica e sposta i profili non referenziati nel cestino (_trash/)
                For Each profileDir In Directory.EnumerateDirectories(sharedDir, "WV2Profile_*")
                    Dim dirName = Path.GetFileName(profileDir)
                    ' Esclude il profilo orfano generico non tipizzato (gestito da MigrateOrphanProfileAsync) e i backup
                    If dirName.Equals("WV2Profile_", StringComparison.OrdinalIgnoreCase) OrElse
                       dirName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) OrElse
                       dirName.Contains(".bak_") OrElse
                       dirName.EndsWith("_Snapshot", StringComparison.OrdinalIgnoreCase) OrElse
                       dirName.EndsWith(".previous", StringComparison.OrdinalIgnoreCase) OrElse
                       dirName.Contains(".incoming-") Then
                        Continue For
                    End If

                    If dirName.StartsWith("WV2Profile_") Then
                        Dim profileId = dirName.Substring("WV2Profile_".Length)
                        If Not String.IsNullOrEmpty(profileId) AndAlso Not activeSet.Contains(profileId) Then
                            Try
                                If Not Directory.Exists(trashDir) Then
                                    Directory.CreateDirectory(trashDir)
                                End If

                                Dim timeStamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
                                Dim trashDest = Path.Combine(trashDir, $"{dirName}_{timeStamp}")
                                If Directory.Exists(trashDest) Then
                                    trashDest = $"{trashDest}_{Guid.NewGuid().ToString("N").Substring(0, 4)}"
                                End If

                                Directory.Move(profileDir, trashDest)
                                Debug.WriteLine($"CleanupUnusedProfiles: spostato profilo non referenziato in cestino: {profileDir} -> {trashDest}")
                            Catch exMove As Exception
                                Debug.WriteLine($"CleanupUnusedProfiles: errore spostamento in cestino di '{profileDir}': {exMove.Message}")
                            End Try
                        End If
                    End If
                Next

                ' 1b. Identifica e sposta profili nativi non referenziati da EBWebView/ nel cestino (TODO #103)
                Dim ebDir = Path.Combine(sharedDir, ProfileIsolationPolicy.SharedEbWebViewFolder)
                If Directory.Exists(ebDir) Then
                    Dim activeAccountIds As New HashSet(Of String)(_accounts.Select(Function(a) a.Id), StringComparer.OrdinalIgnoreCase)
                    Dim activeProfileNames As New HashSet(Of String)(_accounts.Select(Function(a) a.ProfileName), StringComparer.OrdinalIgnoreCase)
                    For Each pDir In Directory.EnumerateDirectories(ebDir)
                        Dim dName = Path.GetFileName(pDir)
                        If ProfileIsolationPolicy.IsNativeAccountProfileDirectory(dName) Then
                            Dim extractedId = ProfileIsolationPolicy.ExtractAccountIdFromProfileDirectory(dName)
                            Dim safeName = ProfileIsolationPolicy.GetSafeProfileName(extractedId)
                            Dim isReferenced = activeAccountIds.Contains(extractedId) OrElse
                                               activeProfileNames.Contains(safeName) OrElse
                                               activeProfileNames.Contains(dName) OrElse
                                               _accounts.Any(Function(a) String.Equals(ProfileIsolationPolicy.GetNativeProfileFolderName(a.Id), dName, StringComparison.OrdinalIgnoreCase))

                            If Not isReferenced Then
                                Try
                                    If Not Directory.Exists(trashDir) Then Directory.CreateDirectory(trashDir)
                                    Dim timeStamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
                                    Dim trashDest = Path.Combine(trashDir, $"{dName}_{timeStamp}")
                                    If Directory.Exists(trashDest) Then
                                        trashDest = $"{trashDest}_{Guid.NewGuid().ToString("N").Substring(0, 4)}"
                                    End If
                                    Directory.Move(pDir, trashDest)
                                    Debug.WriteLine($"CleanupUnusedProfiles: spostato profilo nativo non referenziato in cestino: {pDir} -> {trashDest}")
                                Catch exMove As Exception
                                    Debug.WriteLine($"CleanupUnusedProfiles: errore spostamento profilo nativo: {exMove.Message}")
                                End Try
                            End If
                        End If
                    Next
                End If

                ' 2. Eliminazione differita degli elementi scaduti nel cestino (_trash/)
                If Directory.Exists(trashDir) Then
                    Dim now = DateTime.UtcNow
                    For Each trashItem In Directory.EnumerateDirectories(trashDir)
                        Try
                            Dim dirInfo As New DirectoryInfo(trashItem)
                            ' Se l'elemento nel cestino ha più di 24 ore, procede all'eliminazione differita con retry
                            If (now - dirInfo.LastWriteTimeUtc) > TimeSpan.FromHours(24) Then
                                Debug.WriteLine($"CleanupUnusedProfiles: eliminazione differita elemento scaduto nel cestino '{trashItem}'")
                                Await DeleteDirectoryWithRetryAsync(trashItem, maxAttempts:=5)
                            End If
                        Catch exTrash As Exception
                            Debug.WriteLine($"CleanupUnusedProfiles: errore eliminazione differita per '{trashItem}': {exTrash.Message}")
                        End Try
                    Next
                End If

                ' 3. Pulizia staging locale non referenziato
                If NetworkProfileSync.IsRunningOnNetwork Then
                    Dim stagingBase = NetworkProfileSync.LocalStagingBaseDirectory
                    If Directory.Exists(stagingBase) Then
                        For Each localProfileDir In Directory.EnumerateDirectories(stagingBase, "WV2Profile_*")
                            Dim dirName = Path.GetFileName(localProfileDir)
                            If dirName.StartsWith("WV2Profile_") Then
                                Dim profileId = dirName.Substring("WV2Profile_".Length)
                                If Not String.IsNullOrEmpty(profileId) AndAlso Not activeSet.Contains(profileId) Then
                                    Try
                                        Directory.Delete(localProfileDir, recursive:=True)
                                        Debug.WriteLine($"CleanupUnusedProfiles: eliminato staging locale orfano {localProfileDir}")
                                    Catch
                                    End Try
                                End If
                            End If
                        Next
                    End If
                End If
            Catch ex As Exception
                Debug.WriteLine($"CleanupUnusedProfilesAsync error: {ex.Message}")
            End Try
        End Function)
    End Function

    ''' <summary>
    ''' Esegue una pulizia preventiva delle cartelle di cache volatile e diagnostica (ShaderCache, GPUCache, Crashpad)
    ''' su tutti i profili presenti su disco prima dell'inizializzazione dei processi WebView2 o alla chiusura.
    ''' </summary>
    Public Async Function CleanupTransientCachesAsync(Optional purgeDiskAndCodeCache As Boolean = False) As Task
        Await Task.Run(Sub()
            Try
                Dim sharedDir = AppAccounts.SharedDataDirectory
                If Directory.Exists(sharedDir) Then
                    ' Pulizia cache nei profili nativi sotto EBWebView (TODO #103)
                    Dim ebWebViewDir = Path.Combine(sharedDir, ProfileIsolationPolicy.SharedEbWebViewFolder)
                    If Directory.Exists(ebWebViewDir) Then
                        AppAccounts.CleanTransientCacheFolders(ebWebViewDir, purgeDiskAndCodeCache:=purgeDiskAndCodeCache)
                    End If

                    For Each profileDir In Directory.EnumerateDirectories(sharedDir, "WV2Profile_*")
                        Dim dirName = Path.GetFileName(profileDir)
                        If dirName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) OrElse dirName.Contains(".bak_") Then
                            Continue For
                        End If
                        AppAccounts.CleanTransientCacheFolders(profileDir, purgeDiskAndCodeCache:=purgeDiskAndCodeCache)
                    Next
                End If

                ' Pulizia cache anche nello staging locale se attivo
                If NetworkProfileSync.IsRunningOnNetwork Then
                    Dim stagingBase = NetworkProfileSync.LocalStagingBaseDirectory
                    If Directory.Exists(stagingBase) Then
                        For Each localProfileDir In Directory.EnumerateDirectories(stagingBase, "WV2Profile_*")
                            AppAccounts.CleanTransientCacheFolders(localProfileDir, purgeDiskAndCodeCache:=purgeDiskAndCodeCache)
                        Next
                    End If
                End If
            Catch ex As Exception
                Debug.WriteLine($"CleanupTransientCachesAsync error: {ex.Message}")
            End Try
        End Sub)
    End Function

    ''' <summary>
    ''' Esegue la compattazione chirurgica a caldo della cache WebView2 per il controllo specificato (TODO #104).
    ''' </summary>
    Public Async Function CompactWebViewProfileAsync(webView As Microsoft.Web.WebView2.Wpf.WebView2) As Task(Of Boolean)
        Return Await CacheCompactionPolicy.CompactWebViewProfileAsync(webView)
    End Function

    ''' <summary>
    ''' Esegue la compattazione a caldo della cache su tutti gli account con WebView2 attiva (TODO #104).
    ''' </summary>
    ''' <returns>Numero di profili attivi compattati con successo.</returns>
    Public Async Function CompactAllWebViewsProfileAsync() As Task(Of Integer)
        Dim count As Integer = 0
        For Each acc In _accounts
            Try
                If acc.WebView?.CoreWebView2?.Profile IsNot Nothing Then
                    Dim ok = Await acc.CompactWebViewProfileAsync()
                    If ok Then count += 1
                End If
            Catch ex As Exception
                Debug.WriteLine($"CompactAllWebViewsProfileAsync account {acc.Id} error: {ex.Message}")
            End Try
        Next
        AppLogger.LogApp($"[HOT_COMPACTION] Compacted {count} active profile(s)")
        Return count
    End Function

    ''' <summary>
    ''' Esegue la pulizia selettiva e profonda della cache su tutti gli account preservando login, sessioni e cookie (TODO #72).
    ''' </summary>
    Public Async Function SmartCleanCachePreservingLoginAsync() As Task
        ' 1. Esegue la pulizia a runtime per tutti gli account con WebView2 aperta
        For Each acc In _accounts
            Try
                Await acc.SmartCleanCachePreservingLoginAsync()
            Catch ex As Exception
                Debug.WriteLine($"SmartCleanCache account {acc.Id} error: {ex.Message}")
            End Try
        Next

        ' 2. Esegue la bonifica completa su disco eliminando le cartelle di cache e shader volatili su tutti i profili
        Await CleanupTransientCachesAsync(purgeDiskAndCodeCache:=True)
    End Function

    ''' <summary>
    ''' Sincronizza tutti i profili locali verso la cartella master su disco di rete e, se non periodico, rilascia i lock (TODO #73).
    ''' </summary>
    Public Async Function CloseAllWebViewsAsync() As Task(Of Boolean)
        Dim activeLifetime = _accounts.Select(Function(a) a.BrowserLifetime).FirstOrDefault(Function(l) l IsNot Nothing)
        Dim results = Await Task.WhenAll(_accounts.Select(Function(acc) acc.CloseWebViewAsync()).ToArray())
        If activeLifetime IsNot Nothing Then
            Await activeLifetime.WaitForExitAsync(10000)
        End If
        Return results.All(Function(closed) closed)
    End Function

    Public Async Function SyncAllProfilesToNetworkAsync(Optional isPeriodic As Boolean = False) As Task
        If isPeriodic Then Return
        For Each acc In _accounts.ToList()
            If Not acc.UsesNetworkStaging Then Continue For
            Try
                If acc.BrowserProfileReleased Then
                    Dim saved = Await NetworkProfileSync.SyncLocalStagingToMasterAsync(acc.Id, browserExited:=True)
                    If Not saved Then AppLogger.LogSync($"[EXPORT_NOT_SAVED] account={acc.Id}; local profile retained.")
                Else
                    AppLogger.LogSync($"[EXPORT_SKIPPED] account={acc.Id}; no confirmed browser exit.")
                End If
            Finally
                ' All views were closed before this method is called. On a timeout no
                ' copy is attempted; the OS releases any remaining lease on process exit.
                If acc.CanReleaseNetworkLease Then NetworkProfileSync.ReleaseSessionLock(acc.Id)
            End Try
        Next
    End Function

    ''' <summary>
    ''' Crea l'account predefinito o ricostruisce gli account da eventuali cartelle di profilo esistenti su disco
    ''' quando non è presente alcuna configurazione precedente, prevenendo la perdita di profili preesistenti.
    ''' </summary>
    Private Async Function CreateDefaultAccountAsync() As Task
        Debug.WriteLine("CreateDefaultAccount: nessun account caricato, inizio fallback non distruttivo")
        Dim restoredAccounts As New List(Of AppAccounts)()

        Try
            Dim sharedDir = AppAccounts.SharedDataDirectory
            Debug.WriteLine($"CreateDefaultAccount: sharedDir={sharedDir}, exists={Directory.Exists(sharedDir)}")
            If Directory.Exists(sharedDir) Then
                ' 1. Se è presente un profilo orfano anonimo (WV2Profile_), lo assegna a un nuovo id
                Dim orphanProfile = Path.Combine(sharedDir, "WV2Profile_")
                If Directory.Exists(orphanProfile) Then
                    Dim newOrphanId = AppAccounts.GenerateId()
                    Dim targetDir = Path.Combine(sharedDir, $"WV2Profile_{newOrphanId}")
                    Try
                        Directory.Move(orphanProfile, targetDir)
                        Debug.WriteLine($"CreateDefaultAccount: spostato orfano anonimo {orphanProfile} -> {targetDir}")
                    Catch ex As Exception
                        Debug.WriteLine($"CreateDefaultAccount: errore spostamento orfano: {ex.Message}")
                    End Try
                End If

                ' 2. Scansione non ricorsiva delle cartelle di profilo valide esistenti a livello principale (escludendo cestino e backup)
                Dim existingDirs = Directory.EnumerateDirectories(sharedDir, "WV2Profile_*", SearchOption.TopDirectoryOnly)
                Dim index = 1
                For Each pDir In existingDirs
                    Dim dirName = Path.GetFileName(pDir)
                    If dirName.Equals("WV2Profile_", StringComparison.OrdinalIgnoreCase) OrElse
                       dirName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) OrElse
                       dirName.Contains(".bak_") OrElse
                       dirName.EndsWith("_Snapshot", StringComparison.OrdinalIgnoreCase) OrElse
                       dirName.EndsWith(".previous", StringComparison.OrdinalIgnoreCase) OrElse
                       dirName.Contains(".incoming-") Then
                        Continue For
                    End If

                    Dim pId = dirName.Substring("WV2Profile_".Length)
                    If Not String.IsNullOrEmpty(pId) Then
                        Dim accName = $"Account {index}"
                        Dim isFirst = (index = 1)
                        restoredAccounts.Add(New AppAccounts(pId, accName, isFirst))
                        index += 1
                        If index > MaxAccounts Then Exit For
                    End If
                Next

                ' 2b. Scansione non ricorsiva dei profili nativi esistenti in EBWebView/WV2Profile_* o EBWebView/Account_*
                Dim ebDir = Path.Combine(sharedDir, ProfileIsolationPolicy.SharedEbWebViewFolder)
                If Directory.Exists(ebDir) Then
                    For Each pDir In Directory.EnumerateDirectories(ebDir)
                        Dim dName = Path.GetFileName(pDir)
                        If ProfileIsolationPolicy.IsNativeAccountProfileDirectory(dName) Then
                            Dim pId = ProfileIsolationPolicy.ExtractAccountIdFromProfileDirectory(dName)
                            If Not String.IsNullOrEmpty(pId) AndAlso Not restoredAccounts.Any(Function(a) a.Id.Equals(pId, StringComparison.OrdinalIgnoreCase)) Then
                                Dim accName = $"Account {index}"
                                Dim isFirst = (index = 1)
                                restoredAccounts.Add(New AppAccounts(pId, accName, isFirst))
                                index += 1
                                If index > MaxAccounts Then Exit For
                            End If
                        End If
                    Next
                End If
            End If
        Catch ex As Exception
            Debug.WriteLine($"Error searching existing profile dirs: {ex.Message}")
        End Try

        ' Se non è stato possibile recuperare alcun profilo esistente su disco, crea un account predefinito ex-novo
        If restoredAccounts.Count = 0 Then
            Dim accountId = AppAccounts.GenerateId()
            Dim defaultAccount As New AppAccounts(accountId, "Account 1", True)
            restoredAccounts.Add(defaultAccount)
        End If

        _accounts = New ObservableCollection(Of AppAccounts)(restoredAccounts)
        _currentAccount = _accounts.FirstOrDefault(Function(a) a.IsActive)
        If _currentAccount Is Nothing AndAlso _accounts.Count > 0 Then
            _currentAccount = _accounts.First()
            _currentAccount.IsActive = True
        End If
        _isDirty = True
        
        Await SaveAccountsAsync(force:=True)
        ' No automatic profile/cache cleanup during account discovery.
        
        NotifyPropertyChanged(NameOf(Accounts))
        NotifyPropertyChanged(NameOf(CurrentAccount))
        NotifyPropertyChanged(NameOf(CanAddAccount))
        NotifyPropertyChanged(NameOf(HasExcessAccounts))
        NotifyPropertyChanged(NameOf(ExcessAccountsCount))
    End Function

    ''' <summary>
    ''' Salva l'elenco corrente degli account nel file di configurazione tramite SettingsController.
    ''' </summary>
    ''' <param name="force">Se true, forzatura del salvataggio anche se non ci sono modifiche rilevate.</param>
    Public Async Function SaveAccountsAsync(Optional force As Boolean = False) As Task
        If Not _isDirty AndAlso Not force Then Return

        Dim settings = Await _settingsController.ReadSettingsAsync()
        
        settings("accounts") = _accounts
        
        Await _settingsController.WriteSettingsAsync(settings)
        _isDirty = False
    End Function

    ''' <summary>
    ''' Aggiunge un nuovo account specificando facoltativamente il nome e la piattaforma (WhatsApp, Telegram, OpenClaw o Hermes).
    ''' </summary>
    Public Async Function AddAccountAsync(Optional name As String = Nothing, Optional platform As String = "WhatsApp") As Task(Of Boolean)
        If Not CanAddAccount Then
            Debug.WriteLine($"AddAccountAsync: impossibile aggiungere l'account, limite massimo ({MaxAccounts}) raggiunto o superato ({_accounts.Count}).")
            Return False
        End If

        Dim accountId = AppAccounts.GenerateId()
        Dim def = PlatformRegistry.Get(platform)
        Dim cleanPlatform = def.Id
        Dim platformLabel = def.DisplayName
        
        Dim accountName = name
        If String.IsNullOrWhiteSpace(accountName) Then
            Dim existingNames = _accounts.Select(Function(a) a.Name).ToHashSet()
            For i As Integer = 1 To MaxAccounts + 1
                Dim candidate = $"{platformLabel} {i}"
                If Not existingNames.Contains(candidate) Then
                    accountName = candidate
                    Exit For
                End If
            Next
            If String.IsNullOrWhiteSpace(accountName) Then
                accountName = $"{platformLabel} {_accounts.Count + 1}"
            End If
        End If
        
        Dim newAccount As New AppAccounts(accountId, accountName, False, cleanPlatform)
        If def.RequiresLocalProxy Then
            newAccount.ServerUrl = def.DefaultUrl
            Dim usedPorts = _accounts.Where(Function(a) a.LocalProxyPort > 0).Select(Function(a) a.LocalProxyPort).ToHashSet()
            Dim portCandidate = def.DefaultPort
            While usedPorts.Contains(portCandidate)
                portCandidate += 1
            End While
            newAccount.LocalProxyPort = portCandidate
        End If

        _accounts.Add(newAccount)
        _isDirty = True
        Await SaveAccountsAsync()
        
        NotifyPropertyChanged(NameOf(Accounts))
        NotifyPropertyChanged(NameOf(CanAddAccount))
        NotifyPropertyChanged(NameOf(HasExcessAccounts))
        NotifyPropertyChanged(NameOf(ExcessAccountsCount))
        Return True
    End Function

    ''' <summary>
    ''' Aggiunge un nuovo account specificando la piattaforma tipizzata tramite enum ChatPlatform (TODO #68).
    ''' </summary>
    Public Async Function AddAccountAsync(platformType As ChatPlatform, Optional name As String = Nothing) As Task(Of Boolean)
        Return Await AddAccountAsync(name:=name, platform:=PlatformRegistry.Get(platformType).Id)
    End Function

    ''' <summary>
    ''' Rimuove un account specificato, rilascio delle relative risorse WebView2 ed eliminazione dei dati di profilo da disco.
    ''' </summary>
    ''' <param name="accountId">Identificativo dell'account da rimuovere.</param>
    Public Async Function RemoveAccountAsync(accountId As String) As Task
        If _accounts.Count <= 1 Then
            Debug.WriteLine("Cannot remove the last account.")
            Return
        End If

        Dim accountToRemove = _accounts.FirstOrDefault(Function(a) a.Id = accountId)
        If accountToRemove Is Nothing Then Return
        If Not Await accountToRemove.CloseWebViewAsync() Then
            Throw New IOException("WebView2 has not released the account; profile removal cancelled.")
        End If

        _accounts.Remove(accountToRemove)
        _isDirty = True

        If _currentAccount IsNot Nothing AndAlso _currentAccount.Id = accountId Then
            _currentAccount = _accounts.FirstOrDefault()
            If _currentAccount IsNot Nothing Then
                _currentAccount.IsActive = True
            End If
        End If

        NotifyPropertyChanged(NameOf(Accounts))
        NotifyPropertyChanged(NameOf(CurrentAccount))
        NotifyPropertyChanged(NameOf(CanAddAccount))
        NotifyPropertyChanged(NameOf(HasExcessAccounts))
        NotifyPropertyChanged(NameOf(ExcessAccountsCount))

        Try
            ' Se il profilo WebView2 è ancora attivo, elimina il profilo nativo Chromium (TODO #103)
            If accountToRemove.WebView?.CoreWebView2?.Profile IsNot Nothing Then
                Try
                    accountToRemove.WebView.CoreWebView2.Profile.Delete()
                Catch exProfDel As Exception
                    Debug.WriteLine($"Profile.Delete warning: {exProfDel.Message}")
                End Try
            End If
            ' Invocazione esplicita IDisposable sul WebView e listener
            accountToRemove.Dispose()
        Catch ex As Exception
            Debug.WriteLine($"Error disposing accountToRemove: {ex.Message}")
        End Try

        Dim dirsToRemove As New List(Of String)()
        Dim nativeProfDir = ProfileIsolationPolicy.GetNativeProfileDirectory(AppAccounts.SharedDataDirectory, accountId)
        If Directory.Exists(nativeProfDir) Then dirsToRemove.Add(nativeProfDir)
        Dim legacyProfDir = ProfileIsolationPolicy.GetLegacyProfileDirectory(AppAccounts.SharedDataDirectory, accountId)
        If Directory.Exists(legacyProfDir) Then dirsToRemove.Add(legacyProfDir)

        For Each profileDir In dirsToRemove
            Dim dirName = Path.GetFileName(profileDir)
            Dim trashDir = Path.Combine(AppAccounts.SharedDataDirectory, "_trash")
            Try
                If Not Directory.Exists(trashDir) Then Directory.CreateDirectory(trashDir)
                Dim trashDest = Path.Combine(trashDir, $"{dirName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}")
                Directory.Move(profileDir, trashDest)
                Debug.WriteLine($"RemoveAccountAsync: profilo spostato in cestino: {profileDir} -> {trashDest}")
                Dim deleteBgTask = Task.Run(Async Function()
                    Await Task.Delay(2000)
                    Await DeleteDirectoryWithRetryAsync(trashDest, maxAttempts:=10)
                End Function)
            Catch ex As Exception
                Debug.WriteLine($"RemoveAccountAsync: fallback a cancellazione differita diretta: {ex.Message}")
                Dim fallbackDeleteTask = Task.Run(Async Function()
                    Await DeleteDirectoryWithRetryAsync(profileDir, maxAttempts:=10)
                End Function)
            End Try
        Next

        If accountToRemove.UsesNetworkStaging Then
            NetworkProfileSync.CleanLocalStaging(accountId)
            NetworkProfileSync.ReleaseSessionLock(accountId)
        End If

        Await SaveAccountsAsync()

    End Function

    ''' <summary>
    ''' Cambia l'account attivo portando in primo piano l'account con l'ID fornito.
    ''' </summary>
    ''' <param name="accountId">Identificativo dell'account da attivare.</param>
    Public Async Function SwitchAccountAsync(accountId As String) As Task
        If _currentAccount IsNot Nothing AndAlso _currentAccount.Id = accountId Then Return

        Dim newAccount = _accounts.FirstOrDefault(Function(a) a.Id = accountId)
        If newAccount Is Nothing Then Return

        If _currentAccount IsNot Nothing Then
            _currentAccount.IsActive = False
        End If

        _currentAccount = newAccount
        _currentAccount.IsActive = True

        _isDirty = True
        Await SaveAccountsAsync()
        
        NotifyPropertyChanged(NameOf(Accounts))
        NotifyPropertyChanged(NameOf(CurrentAccount))
    End Function

    ''' <summary>
    ''' Aggiorna il nome visualizzato di un account specificato.
    ''' </summary>
    Public Async Function UpdateAccountNameAsync(accountId As String, newName As String) As Task
        Dim account = _accounts.FirstOrDefault(Function(a) a.Id = accountId)
        If account IsNot Nothing Then
            account.Name = newName
            _isDirty = True
            Await SaveAccountsAsync()
            NotifyPropertyChanged(NameOf(Accounts))
        End If
    End Function

    ''' <summary>
    ''' Ricalcola lo stato globale delle notifiche quando cambia lo stato di un singolo account.
    ''' </summary>
    Public Sub HandleNotificationStateChanged(accountId As String, hasNotif As Boolean)
        Dim target = _accounts.FirstOrDefault(Function(a) a.Id = accountId)
        If target IsNot Nothing Then
            target.HasNotification = hasNotif
        End If

        If hasNotif Then
            HasAnyNotification = True
        Else
            HasAnyNotification = _accounts.Any(Function(a) a.HasNotification)
        End If
    End Sub

    Private _disposed As Boolean = False

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not _disposed Then
            If disposing Then
                If _settingsController IsNot Nothing Then
                    RemoveHandler _settingsController.PropertyChanged, AddressOf OnSettingsPropertyChanged
                End If
            End If
            _disposed = True
        End If
    End Sub
    ''' <summary>
    ''' Abilita la compressione trasparente a livello di filesystem NTFS sulla cartella specificata (TODO #106).
    ''' </summary>
    Public Shared Sub EnableNtfsCompression(folderPath As String)
        NtfsCompressionPolicy.EnableNtfsCompression(folderPath)
    End Sub
End Class

''' <summary>
''' Modulo di estensione LINQ helper in stile VB.NET per facilitare il mapping delle collezioni.
''' </summary>
Module IEnumerableExtensions
    <Runtime.CompilerServices.Extension>
    Public Function Map(Of TSource, TResult)(source As IEnumerable(Of TSource), selector As Func(Of TSource, TResult)) As IEnumerable(Of TResult)
        Return source.Select(selector)
    End Function
End Module

