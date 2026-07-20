Imports System.IO
Imports System.Text.Json
Imports System.ComponentModel
Imports System.Runtime.CompilerServices
Imports Microsoft.Web.WebView2.Wpf

Public Class AccountManager
    Implements INotifyPropertyChanged

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private Sub NotifyPropertyChanged(<CallerMemberName> Optional propertyName As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

    Private ReadOnly _settingsController As SettingsController

    Private _accounts As New List(Of WhatsAppAccount)()
    Public Property Accounts As List(Of WhatsAppAccount)
        Get
            Return _accounts
        End Get
        Set(value As List(Of WhatsAppAccount))
            _accounts = value
            NotifyPropertyChanged()
        End Set
    End Property

    Private _currentAccount As WhatsAppAccount
    Public Property CurrentAccount As WhatsAppAccount
        Get
            Return _currentAccount
        End Get
        Set(value As WhatsAppAccount)
            If _currentAccount IsNot value Then
                _currentAccount = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    Private _hasAnyNotification As Boolean = False
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

    Public Async Function LoadAccountsAsync() As Task
        Dim settings = Await _settingsController.ReadSettingsAsync()
        
        Dim accountsListObj As Object = Nothing
        If settings.TryGetValue("accounts", accountsListObj) Then
            Try
                Dim accountsJson = accountsListObj.ToString()
                Dim jsonOptions As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}
                Dim accountsData = JsonSerializer.Deserialize(Of List(Of WhatsAppAccount))(accountsJson, jsonOptions)
                
                If accountsData IsNot Nothing AndAlso accountsData.Count > 0 Then
                    ' Rigenera ID/name per account corrotti dal vecchio formato case-sensitive
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

                    _accounts = accountsData

                    Debug.WriteLine($"LoadAccounts: caricati {accountsData.Count} account, needsSave={needsSave}")
                    For i As Integer = 0 To accountsData.Count - 1
                        Debug.WriteLine($"  Account[{i}]: Id='{accountsData(i).Id}', Name='{accountsData(i).Name}', IsActive={accountsData(i).IsActive}")
                    Next

                    MigrateOrphanProfile()

                    If needsSave Then
                        For Each acc In _accounts
                            acc.WebView = New WebView2()
                        Next
                        Await SaveAccountsAsync()
                    Else
                        Dim activeIds = _accounts.Map(Function(a) a.Id).ToList()
                        Await CleanupUnusedProfilesAsync(activeIds)
                        For Each acc In _accounts
                            acc.WebView = New WebView2()
                        Next
                    End If
                    
                    _currentAccount = _accounts.FirstOrDefault(Function(a) a.IsActive)
                    If _currentAccount Is Nothing AndAlso _accounts.Count > 0 Then
                        _currentAccount = _accounts.First()
                        _currentAccount.IsActive = True
                    End If
                    
                    NotifyPropertyChanged(NameOf(Accounts))
                    NotifyPropertyChanged(NameOf(CurrentAccount))
                    Return
                End If
            Catch ex As Exception
                Debug.WriteLine($"Error deserializing accounts: {ex.Message}")
            End Try
        End If

        ' Default account if none loaded
        Await CreateDefaultAccountAsync()
    End Function

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
                If Not Directory.Exists(profileDir) Then
                    Try
                        Directory.Move(orphanProfile, profileDir)
                        Debug.WriteLine($"MigrateOrphanProfile: rinominato {orphanProfile} -> {profileDir}")
                    Catch ex As Exception
                        Debug.WriteLine($"MigrateOrphanProfile: errore rinomina: {ex.Message}")
                    End Try
                    Exit For
                End If
            Next
        Catch ex As Exception
            Debug.WriteLine($"MigrateOrphanProfile error: {ex.Message}")
        End Try
    End Sub

    Private Function CleanupUnusedProfilesAsync(activeIds As List(Of String)) As Task
        ' Disabilitata la cancellazione automatica sul disco per evitare perdite accidentali di sessione se settings.json è assente o in aggiornamento
        Return Task.CompletedTask
    End Function

    Private Async Function CreateDefaultAccountAsync() As Task
        Debug.WriteLine("CreateDefaultAccount: nessun account caricato, creo default")
        Dim existingId As String = Nothing

        Try
            ' Cerca se esiste già una cartella di profilo salvata precedentemente sul disco
            Dim sharedDir = WhatsAppAccount.SharedDataDirectory
            Debug.WriteLine($"CreateDefaultAccount: sharedDir={sharedDir}, exists={Directory.Exists(sharedDir)}")
            If Directory.Exists(sharedDir) Then
                ' Cerca profili orfani con chiave vuota (WV2Profile_)
                Dim orphanProfile = Path.Combine(sharedDir, "WV2Profile_")
                If Directory.Exists(orphanProfile) Then
                    ' Genera nuovo ID e rinomina il profilo orfano
                    existingId = WhatsAppAccount.GenerateId()
                    Dim newDir = Path.Combine(sharedDir, $"WV2Profile_{existingId}")
                    Try
                        Directory.Move(orphanProfile, newDir)
                    Catch
                    End Try
                End If

                ' Cerca profili esistenti normali
                If String.IsNullOrEmpty(existingId) Then
                    Dim matchingDirs = Directory.GetDirectories(sharedDir, "WV2Profile_account_*", SearchOption.AllDirectories)
                    If matchingDirs.Length > 0 Then
                        Dim dirName = Path.GetFileName(matchingDirs(0))
                        existingId = dirName.Substring("WV2Profile_".Length)
                    End If
                End If
            End If
        Catch ex As Exception
            Debug.WriteLine($"Error searching existing profile dirs: {ex.Message}")
        End Try

        Dim accountId = If(Not String.IsNullOrEmpty(existingId), existingId, WhatsAppAccount.GenerateId())
        Dim defaultAccount As New WhatsAppAccount(accountId, "Account 1", True)
        defaultAccount.WebView = New WebView2()

        ' Ricollega profilo orfano se esiste ancora (primo avvio dopo fix)
        Dim dir = Path.Combine(WhatsAppAccount.SharedDataDirectory, $"WV2Profile_{accountId}")
        Dim orphanDir = Path.Combine(WhatsAppAccount.SharedDataDirectory, "WV2Profile_")
        If Not Directory.Exists(dir) AndAlso Directory.Exists(orphanDir) Then
            Try
                Directory.Move(orphanDir, dir)
            Catch
            End Try
        End If
        
        _accounts = New List(Of WhatsAppAccount) From {defaultAccount}
        _currentAccount = defaultAccount
        
        Await SaveAccountsAsync()
        
        NotifyPropertyChanged(NameOf(Accounts))
        NotifyPropertyChanged(NameOf(CurrentAccount))
    End Function

    Public Async Function SaveAccountsAsync() As Task
        Dim settings = Await _settingsController.ReadSettingsAsync()
        
        ' Convert accounts to JSON structure
        settings("accounts") = _accounts.Select(Function(a) New With {
            .id = a.Id,
            .name = a.Name,
            .isActive = a.IsActive
        }).ToList()
        
        Await _settingsController.WriteSettingsAsync(settings)
    End Function

    Public Async Function AddAccountAsync(Optional name As String = Nothing) As Task
        Dim accountId = WhatsAppAccount.GenerateId()
        Dim accountName = If(name, $"Account {_accounts.Count + 1}")
        
        Dim newAccount As New WhatsAppAccount(accountId, accountName, False)
        newAccount.WebView = New WebView2()
        
        _accounts.Add(newAccount)
        Await SaveAccountsAsync()
        
        NotifyPropertyChanged(NameOf(Accounts))
    End Function

    Public Async Function RemoveAccountAsync(accountId As String) As Task
        If _accounts.Count <= 1 Then
            Debug.WriteLine("Cannot remove the last account.")
            Return
        End If

        Dim accountToRemove = _accounts.FirstOrDefault(Function(a) a.Id = accountId)
        If accountToRemove Is Nothing Then Return

        _accounts.Remove(accountToRemove)

        If _currentAccount.Id = accountId Then
            _currentAccount = _accounts.First()
            _currentAccount.IsActive = True
        End If

        NotifyPropertyChanged(NameOf(Accounts))
        NotifyPropertyChanged(NameOf(CurrentAccount))

        ' Dispose WebView control asynchronously after a brief delay
        Await Task.Delay(100)
        Try
            accountToRemove.WebView.Dispose()
        Catch ex As Exception
            Debug.WriteLine($"Error disposing webview: {ex.Message}")
        End Try

        ' Delete local profiles folder on disk after a brief delay
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

    Public Async Function SwitchAccountAsync(accountId As String) As Task
        If _currentAccount IsNot Nothing AndAlso _currentAccount.Id = accountId Then Return

        Dim newAccount = _accounts.FirstOrDefault(Function(a) a.Id = accountId)
        If newAccount Is Nothing Then Return

        If _currentAccount IsNot Nothing Then
            _currentAccount.IsActive = False
        End If

        _currentAccount = newAccount
        _currentAccount.IsActive = True

        Await SaveAccountsAsync()
        
        NotifyPropertyChanged(NameOf(Accounts))
        NotifyPropertyChanged(NameOf(CurrentAccount))
    End Function

    Public Async Function UpdateAccountNameAsync(accountId As String, newName As String) As Task
        Dim account = _accounts.FirstOrDefault(Function(a) a.Id = accountId)
        If account IsNot Nothing Then
            account.Name = newName
            Await SaveAccountsAsync()
            NotifyPropertyChanged(NameOf(Accounts))
        End If
    End Function

    Public Sub HandleNotificationStateChanged(accountId As String, hasNotif As Boolean)
        Dim anyNotif = _accounts.Any(Function(a) a.HasNotification)
        HasAnyNotification = anyNotif
    End Sub
End Class

' Extension class to map elements in Linq-like style in VB
Module IEnumerableExtensions
    <Runtime.CompilerServices.Extension>
    Public Function Map(Of TSource, TResult)(source As IEnumerable(Of TSource), selector As Func(Of TSource, TResult)) As IEnumerable(Of TResult)
        Return source.Select(selector)
    End Function
End Module
