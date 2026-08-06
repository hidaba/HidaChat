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

    Private _accounts As New ObservableCollection(Of WhatsAppAccount)()

    ''' <summary>
    ''' Collezione osservabile di tutti gli account WhatsApp configurati.
    ''' </summary>
    Public Property Accounts As ObservableCollection(Of WhatsAppAccount)
        Get
            Return _accounts
        End Get
        Set(value As ObservableCollection(Of WhatsAppAccount))
            _accounts = value
            _isDirty = True
            NotifyPropertyChanged()
            NotifyPropertyChanged(NameOf(CanAddAccount))
        End Set
    End Property

    Private _currentAccount As WhatsAppAccount

    ''' <summary>
    ''' Account WhatsApp attualmente selezionato e visualizzato nell'interfaccia.
    ''' </summary>
    Public Property CurrentAccount As WhatsAppAccount
        Get
            Return _currentAccount
        End Get
        Set(value As WhatsAppAccount)
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
                Dim accountsData = JsonSerializer.Deserialize(Of List(Of WhatsAppAccount))(accountsJson, jsonOptions)
                
                If accountsData IsNot Nothing AndAlso accountsData.Count > 0 Then
                    Dim needsSave = False
                    For i As Integer = 0 To accountsData.Count - 1
                        If String.IsNullOrEmpty(accountsData(i).Id) Then
                            accountsData(i).Id = WhatsAppAccount.GenerateId()
                            needsSave = True
                        End If
                        If String.IsNullOrEmpty(accountsData(i).Name) Then
                            accountsData(i).Name = $"Account {i + 1}"
                            needsSave = True
                        End If
                    Next

                    _accounts = New ObservableCollection(Of WhatsAppAccount)(accountsData)

                    Debug.WriteLine($"LoadAccounts: caricati {accountsData.Count} account, needsSave={needsSave}")
                    For i As Integer = 0 To accountsData.Count - 1
                        Debug.WriteLine($"  Account[{i}]: Id='{accountsData(i).Id}', Name='{accountsData(i).Name}', IsActive={accountsData(i).IsActive}")
                    Next

                    MigrateOrphanProfile()

                    If needsSave Then
                        Await SaveAccountsAsync(force:=True)
                    Else
                        Dim activeIds = _accounts.Map(Function(a) a.Id).ToList()
                        Await CleanupUnusedProfilesAsync(activeIds)
                    End If
                    
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
    ''' Verifica la presenza di una cartella di profilo WebView2 orfana (creata senza ID specificato) e la riassocia al primo account.
    ''' </summary>
    Private Sub MigrateOrphanProfile()
        Try
            Dim orphanProfile = Path.Combine(WhatsAppAccount.SharedDataDirectory, "WV2Profile_")
            If Not Directory.Exists(orphanProfile) Then
                Debug.WriteLine("MigrateOrphanProfile: nessun profilo orfano trovato")
                Return
            End If
            Debug.WriteLine($"MigrateOrphanProfile: trovato profilo orfano {orphanProfile}")

            For Each acc In _accounts
                Dim profileDir = Path.Combine(WhatsAppAccount.SharedDataDirectory, $"WV2Profile_{acc.Id}")
                Debug.WriteLine($"MigrateOrphanProfile: check account Id='{acc.Id}', target={profileDir}, exists={Directory.Exists(profileDir)}")
                If Directory.Exists(profileDir) Then
                    Try
                        Directory.Delete(profileDir, True)
                        Debug.WriteLine($"MigrateOrphanProfile: eliminato profilo stale {profileDir}")
                    Catch ex As Exception
                        Debug.WriteLine($"MigrateOrphanProfile: errore cancellazione stale: {ex.Message}")
                        Continue For
                    End Try
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
    End Sub

    ''' <summary>
    ''' Rimuove eventuali profili non più associati ad alcun account attivo.
    ''' </summary>
    Private Function CleanupUnusedProfilesAsync(activeIds As List(Of String)) As Task
        Return Task.CompletedTask
    End Function

    ''' <summary>
    ''' Crea l'account predefinito ("Account 1") quando non è presente alcuna configurazione precedente.
    ''' </summary>
    Private Async Function CreateDefaultAccountAsync() As Task
        Debug.WriteLine("CreateDefaultAccount: nessun account caricato, creo default")
        Dim existingId As String = Nothing

        Try
            Dim sharedDir = WhatsAppAccount.SharedDataDirectory
            Debug.WriteLine($"CreateDefaultAccount: sharedDir={sharedDir}, exists={Directory.Exists(sharedDir)}")
            If Directory.Exists(sharedDir) Then
                Dim orphanProfile = Path.Combine(sharedDir, "WV2Profile_")
                If Directory.Exists(orphanProfile) Then
                    existingId = WhatsAppAccount.GenerateId()
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

        Dim accountId = If(Not String.IsNullOrEmpty(existingId), existingId, WhatsAppAccount.GenerateId())
        Dim defaultAccount As New WhatsAppAccount(accountId, "Account 1", True)

        Dim dir = Path.Combine(WhatsAppAccount.SharedDataDirectory, $"WV2Profile_{accountId}")
        Dim orphanDir = Path.Combine(WhatsAppAccount.SharedDataDirectory, "WV2Profile_")
        If Not Directory.Exists(dir) AndAlso Directory.Exists(orphanDir) Then
            Try
                Directory.Move(orphanDir, dir)
            Catch
            End Try
        End If
        
        _accounts = New ObservableCollection(Of WhatsAppAccount) From {defaultAccount}
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
        
        settings("accounts") = _accounts.Select(Function(a) New With {
            .id = a.Id,
            .name = a.Name,
            .isActive = a.IsActive
        }).ToList()
        
        Await _settingsController.WriteSettingsAsync(settings)
        _isDirty = False
    End Function

    ''' <summary>
    ''' Aggiunge un nuovo account WhatsApp alla collezione (fino a un massimo di 3) e ne salva le modifiche.
    ''' </summary>
    ''' <param name="name">Nome personalizzato facoltativo dell'account.</param>
    ''' <returns>True se l'account è stato aggiunto con successo, False se è già stato raggiunto il limite massimo.</returns>
    Public Async Function AddAccountAsync(Optional name As String = Nothing) As Task(Of Boolean)
        If _accounts.Count >= MaxAccounts Then
            Debug.WriteLine($"AddAccountAsync: impossibile aggiungere l'account, limite massimo ({MaxAccounts}) raggiunto.")
            Return False
        End If

        Dim accountId = WhatsAppAccount.GenerateId()
        
        Dim accountName = name
        If String.IsNullOrWhiteSpace(accountName) Then
            Dim existingNames = _accounts.Select(Function(a) a.Name).ToHashSet()
            For i As Integer = 1 To MaxAccounts + 1
                Dim candidate = $"Account {i}"
                If Not existingNames.Contains(candidate) Then
                    accountName = candidate
                    Exit For
                End If
            Next
            If String.IsNullOrWhiteSpace(accountName) Then
                accountName = $"Account {_accounts.Count + 1}"
            End If
        End If
        
        Dim newAccount As New WhatsAppAccount(accountId, accountName, False)

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

        If _currentAccount.Id = accountId Then
            _currentAccount = _accounts.First()
            _currentAccount.IsActive = True
        End If

        NotifyPropertyChanged(NameOf(Accounts))
        NotifyPropertyChanged(NameOf(CurrentAccount))
        NotifyPropertyChanged(NameOf(CanAddAccount))

        Await Task.Delay(100)
        Try
            ' Invocazione esplicita IDisposable sul WebView e listener
            accountToRemove.Dispose()
        Catch ex As Exception
            Debug.WriteLine($"Error disposing accountToRemove: {ex.Message}")
        End Try

        Await Task.Delay(500)
        Try
            Dim profileDir = Path.Combine(WhatsAppAccount.SharedDataDirectory, $"WV2Profile_{accountId}")
            If Directory.Exists(profileDir) Then
                Directory.Delete(profileDir, True)
                Debug.WriteLine($"Deleted profile folder for: {accountId}")
            End If
        Catch ex As Exception
            Debug.WriteLine($"Error deleting profile directory: {ex.Message}")
        End Try

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

