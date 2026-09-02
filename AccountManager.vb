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

    Public Const MaxAccounts As Integer = 3

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private Sub NotifyPropertyChanged(<CallerMemberName> Optional propertyName As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

    Private ReadOnly _settingsController As SettingsController
    Private _isDirty As Boolean = False

    ''' <summary>
    ''' Indica se è possibile aggiungere un nuovo account (limite massimo di 3 account non ancora raggiunto).
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
        
        Dim accountsListObj As Object = Nothing
        If settings.TryGetValue("accounts", accountsListObj) Then
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

                    _accounts = New ObservableCollection(Of AppAccounts)(accountsData)

                    Debug.WriteLine($"LoadAccounts: caricati {accountsData.Count} account, needsSave={needsSave}")
                    For i As Integer = 0 To accountsData.Count - 1
                        Debug.WriteLine($"  Account[{i}]: Id='{accountsData(i).Id}', Name='{accountsData(i).Name}', IsActive={accountsData(i).IsActive}")
                    Next

                    Await MigrateOrphanProfileAsync()

                    If needsSave Then
                        Await SaveAccountsAsync(force:=True)
                    End If

                    Dim activeIds = _accounts.Map(Function(a) a.Id).ToList()
                    Await CleanupUnusedProfilesAsync(activeIds)
                    
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
    ''' Verifica la presenza di una cartella di profilo WebView2 orfana (creata senza ID specificato) e la riassocia al primo account.
    ''' </summary>
    Private Async Function MigrateOrphanProfileAsync() As Task
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
                If Directory.Exists(profileDir) Then
                    Dim deleted = Await DeleteDirectoryWithRetryAsync(profileDir)
                    If Not deleted Then
                        Debug.WriteLine($"MigrateOrphanProfile: errore cancellazione stale: {profileDir}")
                        Continue For
                    End If
                End If

                Try
                    Directory.Move(orphanProfile, profileDir)
                    Debug.WriteLine($"MigrateOrphanProfile: rinominato {orphanProfile} -> {profileDir}")
                Catch ex As Exception
                    Debug.WriteLine($"MigrateOrphanProfile: errore rinomina: {ex.Message}")
                End Try
                Exit For
            Next
        Catch ex As Exception
            Debug.WriteLine($"MigrateOrphanProfile error: {ex.Message}")
        End Try
    End Function

    ''' <summary>
    ''' Rimuove eventuali profili WebView2 non più associati ad alcun account attivo.
    ''' </summary>
    Private Async Function CleanupUnusedProfilesAsync(activeIds As List(Of String)) As Task
        Try
            Dim sharedDir = AppAccounts.SharedDataDirectory
            If Not Directory.Exists(sharedDir) Then
                Return
            End If

            Dim activeSet As New HashSet(Of String)(If(activeIds, New List(Of String)()), StringComparer.OrdinalIgnoreCase)
            Dim profileDirs = Directory.GetDirectories(sharedDir, "WV2Profile_*")

            For Each profileDir In profileDirs
                Dim dirName = Path.GetFileName(profileDir)
                If dirName.StartsWith("WV2Profile_") Then
                    Dim profileId = dirName.Substring("WV2Profile_".Length)
                    If Not activeSet.Contains(profileId) Then
                        Await DeleteDirectoryWithRetryAsync(profileDir)
                    End If
                End If
            Next
        Catch ex As Exception
            Debug.WriteLine($"CleanupUnusedProfilesAsync error: {ex.Message}")
        End Try
    End Function

    ''' <summary>
    ''' Crea l'account predefinito ("Account 1") quando non è presente alcuna configurazione precedente.
    ''' </summary>
    Private Async Function CreateDefaultAccountAsync() As Task
        Debug.WriteLine("CreateDefaultAccount: nessun account caricato, creo default")
        Dim existingId As String = Nothing

        Try
            Dim sharedDir = AppAccounts.SharedDataDirectory
            Debug.WriteLine($"CreateDefaultAccount: sharedDir={sharedDir}, exists={Directory.Exists(sharedDir)}")
            If Directory.Exists(sharedDir) Then
                Dim orphanProfile = Path.Combine(sharedDir, "WV2Profile_")
                If Directory.Exists(orphanProfile) Then
                    existingId = AppAccounts.GenerateId()
                    Dim newDir = Path.Combine(sharedDir, $"WV2Profile_{existingId}")
                    Try
                        Directory.Move(orphanProfile, newDir)
                    Catch
                    End Try
                End If

                If String.IsNullOrEmpty(existingId) Then
                    Dim firstMatchingDir = Directory.EnumerateDirectories(sharedDir, "WV2Profile_account_*", SearchOption.AllDirectories).FirstOrDefault()
                    If Not String.IsNullOrEmpty(firstMatchingDir) Then
                        Dim dirName = Path.GetFileName(firstMatchingDir)
                        existingId = dirName.Substring("WV2Profile_".Length)
                    End If
                End If

            End If
        Catch ex As Exception
            Debug.WriteLine($"Error searching existing profile dirs: {ex.Message}")
        End Try

        Dim accountId = If(Not String.IsNullOrEmpty(existingId), existingId, AppAccounts.GenerateId())
        Dim defaultAccount As New AppAccounts(accountId, "Account 1", True)

        Dim dir = Path.Combine(AppAccounts.SharedDataDirectory, $"WV2Profile_{accountId}")
        Dim orphanDir = Path.Combine(AppAccounts.SharedDataDirectory, "WV2Profile_")
        If Not Directory.Exists(dir) AndAlso Directory.Exists(orphanDir) Then
            Try
                Directory.Move(orphanDir, dir)
            Catch
            End Try
        End If
        
        _accounts = New ObservableCollection(Of AppAccounts) From {defaultAccount}
        _currentAccount = defaultAccount
        _isDirty = True
        
        Await SaveAccountsAsync(force:=True)
        
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
            Dim platformLabel = If(String.Equals(cleanPlatform, "Telegram", StringComparison.OrdinalIgnoreCase), "Telegram", "WhatsApp")
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
        Dim deleted = Await DeleteDirectoryWithRetryAsync(profileDir, maxAttempts:=10)
        If deleted Then
            Debug.WriteLine($"Deleted profile folder for: {accountId}")
        Else
            Debug.WriteLine($"Warning: Failed to delete profile directory '{profileDir}' after retries")
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
    ''' Ricalcola lo stato globale delle notifiche in base allo stato dei singoli account.
    ''' </summary>
    Public Sub HandleNotificationStateChanged(accountId As String, hasNotif As Boolean)
        Dim anyNotif = _accounts.Any(Function(a) a.HasNotification)
        HasAnyNotification = anyNotif
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

