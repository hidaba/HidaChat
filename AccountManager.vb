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
    Implements INotifyPropertyChanged

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

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Public Sub NotifyPropertyChanged(<CallerMemberName> Optional propertyName As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

    Private ReadOnly _settingsController As SettingsController
    Private _isDirty As Boolean = False

    ''' <summary>
    ''' Indica se è possibile aggiungere un nuovo account (limite massimo configurato non ancora raggiunto).
    ''' </summary>
    Public ReadOnly Property CanAddAccount As Boolean
        Get
            Return _accounts IsNot Nothing AndAlso _accounts.Count < MaxAccounts
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
    End Sub

    ''' <summary>
    ''' Carica in modo asincrono la lista degli account salvata nelle impostazioni.
    ''' Se nessun account è memorizzato, crea un account predefinito.
    ''' </summary>
    Public Async Function LoadAccountsAsync() As Task
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

                    Dim baseProxyPort = 18800
                    For Each acc In accountsData
                        If acc.IsOpenClaw Then
                            If acc.LocalProxyPort <= 0 Then
                                While usedPorts.Contains(baseProxyPort)
                                    baseProxyPort += 1
                                End While
                                acc.LocalProxyPort = baseProxyPort
                                usedPorts.Add(baseProxyPort)
                                needsSave = True
                            End If
                        ElseIf acc.IsHermes Then
                            If acc.LocalProxyPort <= 0 Then
                                Dim hermesPort = 18900
                                While usedPorts.Contains(hermesPort)
                                    hermesPort += 1
                                End While
                                acc.LocalProxyPort = hermesPort
                                usedPorts.Add(hermesPort)
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

                    If needsSave Then
                        Await SaveAccountsAsync(force:=True)
                    End If

                    ' Pulizia non distruttiva dei profili non referenziati eseguita esclusivamente se la configurazione è valida
                    ' e delegata a un task di background in Task.Run per non bloccare il thread UI all'avvio
                    Dim activeIds = _accounts.Map(Function(a) a.Id).ToList()
                    Dim cleanupTask = CleanupUnusedProfilesAsync(activeIds)

                    Await CleanupTransientCachesAsync()
                    
                    _currentAccount = _accounts.FirstOrDefault(Function(a) a.IsActive)
                    If _currentAccount Is Nothing AndAlso _accounts.Count > 0 Then
                        _currentAccount = _accounts.First()
                        _currentAccount.IsActive = True
                    End If
                    
                    _isDirty = False
                    NotifyPropertyChanged(NameOf(Accounts))
                    NotifyPropertyChanged(NameOf(CurrentAccount))
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
            Dim orphanProfile = Path.Combine(AppAccounts.SharedDataDirectory, "WV2Profile_")
            If Not Directory.Exists(orphanProfile) Then
                Debug.WriteLine("MigrateOrphanProfile: nessun profilo orfano trovato")
                Return
            End If
            Debug.WriteLine($"MigrateOrphanProfile: trovato profilo orfano {orphanProfile}")

            For Each acc In _accounts
                Dim profileDir = Path.Combine(AppAccounts.SharedDataDirectory, $"WV2Profile_{acc.Id}")
                Debug.WriteLine($"MigrateOrphanProfile: check account Id='{acc.Id}', target={profileDir}, exists={Directory.Exists(profileDir)}")
                Dim movedToBak = False
                Dim bakDir = profileDir & ".bak"

                If Directory.Exists(profileDir) Then
                    Try
                        If Directory.Exists(bakDir) Then
                            Dim bakTimestamp = $"{profileDir}.bak_{DateTime.UtcNow:yyyyMMdd_HHmmss}"
                            Try
                                Directory.Move(bakDir, bakTimestamp)
                            Catch
                            End Try
                        End If
                        Directory.Move(profileDir, bakDir)
                        movedToBak = True
                        Debug.WriteLine($"MigrateOrphanProfile: rinominato profilo esistente in backup: {profileDir} -> {bakDir}")
                    Catch exBak As Exception
                        Debug.WriteLine($"MigrateOrphanProfile: errore rinomina in backup: {exBak.Message}")
                        Continue For
                    End Try
                End If

                Try
                    Directory.Move(orphanProfile, profileDir)
                    Debug.WriteLine($"MigrateOrphanProfile: rinominato {orphanProfile} -> {profileDir}")
                Catch ex As Exception
                    Debug.WriteLine($"MigrateOrphanProfile: errore rinomina orfano: {ex.Message}")
                    If movedToBak AndAlso Not Directory.Exists(profileDir) AndAlso Directory.Exists(bakDir) Then
                        Try
                            Directory.Move(bakDir, profileDir)
                        Catch
                        End Try
                    End If
                End Try
                Exit For
            Next
        Catch ex As Exception
            Debug.WriteLine($"MigrateOrphanProfile error: {ex.Message}")
        End Try
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
                       dirName.Contains(".bak_") Then
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
                If Not Directory.Exists(sharedDir) Then Return

                For Each profileDir In Directory.EnumerateDirectories(sharedDir, "WV2Profile_*")
                    Dim dirName = Path.GetFileName(profileDir)
                    If dirName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) OrElse dirName.Contains(".bak_") Then
                        Continue For
                    End If
                    AppAccounts.CleanTransientCacheFolders(profileDir, purgeDiskAndCodeCache:=purgeDiskAndCodeCache)
                Next
            Catch ex As Exception
                Debug.WriteLine($"CleanupTransientCachesAsync error: {ex.Message}")
            End Try
        End Sub)
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
                       dirName.Contains(".bak_") Then
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
        Await CleanupTransientCachesAsync()
        
        NotifyPropertyChanged(NameOf(Accounts))
        NotifyPropertyChanged(NameOf(CurrentAccount))
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
    ''' Aggiunge un nuovo account specificando facoltativamente il nome e la piattaforma (WhatsApp o Telegram).
    ''' </summary>
    Public Async Function AddAccountAsync(Optional name As String = Nothing, Optional platform As String = "WhatsApp") As Task(Of Boolean)
        If _accounts.Count >= MaxAccounts Then
            Debug.WriteLine($"AddAccountAsync: impossibile aggiungere l'account, limite massimo ({MaxAccounts}) raggiunto.")
            Return False
        End If

        Dim accountId = AppAccounts.GenerateId()
        Dim cleanPlatform = If(String.IsNullOrWhiteSpace(platform), "WhatsApp", platform)
        
        Dim accountName = name
        If String.IsNullOrWhiteSpace(accountName) Then
            Dim platformLabel = If(String.Equals(cleanPlatform, "Hermes", StringComparison.OrdinalIgnoreCase), "Hermes", If(String.Equals(cleanPlatform, "OpenClaw", StringComparison.OrdinalIgnoreCase), "OpenClaw", If(String.Equals(cleanPlatform, "Telegram", StringComparison.OrdinalIgnoreCase), "Telegram", "WhatsApp")))
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
        If String.Equals(cleanPlatform, "Hermes", StringComparison.OrdinalIgnoreCase) Then
            newAccount.ServerUrl = "http://127.0.0.1:9119"
            Dim usedPorts = _accounts.Where(Function(a) a.LocalProxyPort > 0).Select(Function(a) a.LocalProxyPort).ToHashSet()
            Dim portCandidate = 18900
            While usedPorts.Contains(portCandidate)
                portCandidate += 1
            End While
            newAccount.LocalProxyPort = portCandidate
        ElseIf String.Equals(cleanPlatform, "OpenClaw", StringComparison.OrdinalIgnoreCase) Then
            Dim usedPorts = _accounts.Where(Function(a) a.LocalProxyPort > 0).Select(Function(a) a.LocalProxyPort).ToHashSet()
            Dim portCandidate = 18800
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
        Return True
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

        Try
            ' Invocazione esplicita IDisposable sul WebView e listener
            accountToRemove.Dispose()
        Catch ex As Exception
            Debug.WriteLine($"Error disposing accountToRemove: {ex.Message}")
        End Try

        Dim profileDir = Path.Combine(AppAccounts.SharedDataDirectory, $"WV2Profile_{accountId}")
        If Directory.Exists(profileDir) Then
            Dim trashDir = Path.Combine(AppAccounts.SharedDataDirectory, "_trash")
            Try
                If Not Directory.Exists(trashDir) Then Directory.CreateDirectory(trashDir)
                Dim trashDest = Path.Combine(trashDir, $"WV2Profile_{accountId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}")
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

