Imports System.ComponentModel

Public Class SettingsWindow
    Private ReadOnly _settingsController As SettingsController
    Private ReadOnly _accountManager As AccountManager
    Private _isInitializing As Boolean = True

    Public Sub New(settingsController As SettingsController, accountManager As AccountManager)
        InitializeComponent()
        _settingsController = settingsController
        _accountManager = accountManager
    End Sub

    Private Sub SettingsWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        ' 1. Set Items for Language Dropdown
        ComboLanguage.ItemsSource = _settingsController.SupportedLanguages
        ComboLanguage.SelectedValue = _settingsController.Language

        ' 2. Select Theme Selection
        For Each item As ComboBoxItem In ComboTheme.Items
            If item.Content.ToString() = _settingsController.Theme Then
                ComboTheme.SelectedItem = item
                Exit For
            End If
        Next

        ' 3. Set Checkbox checks
        ChkKeepAppInEnglish.IsChecked = _settingsController.KeepAppInEnglish
        ChkTranslateMessageButton.IsChecked = _settingsController.TranslateMessageButton
        ChkFullPageTranslation.IsChecked = _settingsController.FullPageTranslation
        ChkShowTranslateAllButton.IsChecked = _settingsController.ShowTranslateAllMessagesButton
        ChkTranslateNotifications.IsChecked = _settingsController.TranslateNotifications
        ChkShowTranslateNotificationBtn.IsChecked = _settingsController.ShowTranslateNotificationButton

        ' 4. Bind Accounts List
        AccountsList.ItemsSource = _accountManager.Accounts

        ' 5. Apply Active Theme
        ApplyTheme()

        _isInitializing = False
    End Sub

    Private Sub TitleBar_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        If e.ChangedButton = MouseButton.Left Then
            Me.DragMove()
        End If
    End Sub

    Private Sub BtnClose_Click(sender As Object, e As RoutedEventArgs)
        Me.Close()
    End Sub

    Private Async Sub ComboTheme_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _isInitializing Then Return
        Dim selectedItem = CType(ComboTheme.SelectedItem, ComboBoxItem)
        If selectedItem IsNot Nothing Then
            Dim themeStr = selectedItem.Content.ToString()
            Await _settingsController.SaveThemeAsync(themeStr)
            ApplyTheme()
        End If
    End Sub

    Private Async Sub ComboLanguage_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _isInitializing Then Return
        If ComboLanguage.SelectedValue IsNot Nothing Then
            Dim langCode = ComboLanguage.SelectedValue.ToString()
            Await _settingsController.UpdateLanguageAsync(langCode)
            
            ' Notify active webviews of language update
            Dim langItem = _settingsController.SupportedLanguages.FirstOrDefault(Function(l) l("code") = langCode)
            If langItem IsNot Nothing Then
                Dim langName = langItem("name")
                Dim translatedLangName = langName
                If langCode <> "en" Then
                    Try
                        translatedLangName = Await AppLocalizations.TranslateSingle(langName, langCode)
                    Catch
                    End Try
                End If
                Dim tooltipLabel = _settingsController.Localizations.Get("translate_to_lang", New Dictionary(Of String, String) From {{"lang", translatedLangName}})
                
                For Each acc In _accountManager.Accounts
                    Await acc.UpdateWebviewLanguageAsync(langCode, translatedLangName, tooltipLabel, _settingsController.TranslateMessageButton)
                Next
            End If
        End If
    End Sub

    Private Async Sub ChkSetting_Changed(sender As Object, e As RoutedEventArgs)
        If _isInitializing Then Return
        
        Dim chk = CType(sender, CheckBox)
        Select Case chk.Name
            Case "ChkKeepAppInEnglish"
                _settingsController.KeepAppInEnglish = chk.IsChecked.Value
                Await _settingsController.SaveSettingAsync("keepAppInEnglish", chk.IsChecked.Value)
                ' Force reload translation system
                Await _settingsController.UpdateLanguageAsync(_settingsController.Language)
            Case "ChkTranslateMessageButton"
                _settingsController.TranslateMessageButton = chk.IsChecked.Value
                Await _settingsController.SaveSettingAsync("translateMessageButton", chk.IsChecked.Value)
            Case "ChkFullPageTranslation"
                _settingsController.FullPageTranslation = chk.IsChecked.Value
                Await _settingsController.SaveSettingAsync("fullPageTranslation", chk.IsChecked.Value)
            Case "ChkShowTranslateAllButton"
                _settingsController.ShowTranslateAllMessagesButton = chk.IsChecked.Value
                Await _settingsController.SaveSettingAsync("showTranslateAllMessagesButton", chk.IsChecked.Value)
            Case "ChkTranslateNotifications"
                _settingsController.TranslateNotifications = chk.IsChecked.Value
                Await _settingsController.SaveSettingAsync("translateNotifications", chk.IsChecked.Value)
            Case "ChkShowTranslateNotificationBtn"
                _settingsController.ShowTranslateNotificationButton = chk.IsChecked.Value
                Await _settingsController.SaveSettingAsync("showTranslateNotificationButton", chk.IsChecked.Value)
        End Select

        ' Notify webviews of hover state changes
        If chk.Name = "ChkTranslateMessageButton" Then
            Dim langItem = _settingsController.SupportedLanguages.FirstOrDefault(Function(l) l("code") = _settingsController.Language)
            If langItem IsNot Nothing Then
                Dim langName = langItem("name")
                Dim translatedLangName = langName
                If _settingsController.Language <> "en" Then
                    Try
                        translatedLangName = Await AppLocalizations.TranslateSingle(langName, _settingsController.Language)
                    Catch
                    End Try
                End If
                Dim tooltipLabel = _settingsController.Localizations.Get("translate_to_lang", New Dictionary(Of String, String) From {{"lang", translatedLangName}})
                
                For Each acc In _accountManager.Accounts
                    Await acc.UpdateWebviewLanguageAsync(_settingsController.Language, translatedLangName, tooltipLabel, chk.IsChecked.Value)
                Next
            End If
        End If
    End Sub

    Private Async Sub TxtAccountName_LostFocus(sender As Object, e As RoutedEventArgs)
        Dim txt = CType(sender, TextBox)
        Dim acc = CType(txt.DataContext, WhatsAppAccount)
        If acc IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(txt.Text) Then
            Await _accountManager.UpdateAccountNameAsync(acc.Id, txt.Text.Trim())
        End If
    End Sub

    Private Async Sub BtnDeleteAccount_Click(sender As Object, e As RoutedEventArgs)
        Dim btn = CType(sender, Button)
        Dim accountId = btn.Tag.ToString()
        
        Dim acc = _accountManager.Accounts.FirstOrDefault(Function(a) a.Id = accountId)
        If acc Is Nothing Then Return

        If _accountManager.Accounts.Count <= 1 Then
            MessageBox.Show("You cannot delete the last active account.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim confirmMsg = $"Delete ""{acc.Name}""? This will remove all data for this account."
        Dim result = MessageBox.Show(confirmMsg, "Delete Account", MessageBoxButton.YesNo, MessageBoxImage.Question)
        
        If result = MessageBoxResult.Yes Then
            Await _accountManager.RemoveAccountAsync(accountId)
            ' Refresh List UI
            AccountsList.ItemsSource = Nothing
            AccountsList.ItemsSource = _accountManager.Accounts
        End If
    End Sub

    Private Sub BtnDevTools_Click(sender As Object, e As RoutedEventArgs)
        Dim activeAcc = _accountManager.CurrentAccount
        If activeAcc IsNot Nothing AndAlso activeAcc.WebView.CoreWebView2 IsNot Nothing Then
            activeAcc.WebView.CoreWebView2.OpenDevToolsWindow()
        End If
    End Sub

    Private Async Sub BtnCheckUpdates_Click(sender As Object, e As RoutedEventArgs)
        Await UpdateChecker.CheckForUpdatesAsync(_settingsController, _accountManager, force:=True)
    End Sub

    Private Sub ApplyTheme()
        Dim isDark = False
        If _settingsController.Theme = "Dark" Then
            isDark = True
        ElseIf _settingsController.Theme = "System" Then
            Try
                Dim key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")
                If key IsNot Nothing Then
                    Dim val = key.GetValue("AppsUseLightTheme")
                    If val IsNot Nothing AndAlso Convert.ToInt32(val) = 0 Then
                        isDark = True
                    End If
                End If
            Catch
            End Try
        End If

        If isDark Then
            SettingsBorder.Background = New SolidColorBrush(ColorConverter.ConvertFromString("#1f2c34"))
            TitleBar.Background = New SolidColorBrush(ColorConverter.ConvertFromString("#202c33"))
            ' Refresh styling for all checkboxes and labels in scrollview
            For Each chk In FindLogicalChildren(Of CheckBox)(Me)
                chk.Foreground = New SolidColorBrush(ColorConverter.ConvertFromString("#aebac1"))
            Next
            For Each txt In FindLogicalChildren(Of TextBlock)(Me)
                If txt.Style Is Nothing Then
                    txt.Foreground = New SolidColorBrush(ColorConverter.ConvertFromString("#aebac1"))
                End If
            Next
        Else
            SettingsBorder.Background = New SolidColorBrush(ColorConverter.ConvertFromString("#ffffff"))
            TitleBar.Background = New SolidColorBrush(ColorConverter.ConvertFromString("#e9edef"))
            ' Refresh styling for all checkboxes and labels in scrollview
            For Each chk In FindLogicalChildren(Of CheckBox)(Me)
                chk.Foreground = New SolidColorBrush(ColorConverter.ConvertFromString("#111b21"))
            Next
            For Each txt In FindLogicalChildren(Of TextBlock)(Me)
                If txt.Style Is Nothing Then
                    txt.Foreground = New SolidColorBrush(ColorConverter.ConvertFromString("#111b21"))
                End If
            Next
        End If
    End Sub

    ' Helpers for finding logical children
    Private Shared Function FindLogicalChildren(Of T As DependencyObject)(depObj As DependencyObject) As List(Of T)
        Dim list As New List(Of T)()
        If depObj IsNot Nothing Then
            For Each child In LogicalTreeHelper.GetChildren(depObj)
                If TypeOf child Is DependencyObject Then
                    Dim depChild = CType(child, DependencyObject)
                    If TypeOf depChild Is T Then
                        list.Add(CType(depChild, T))
                    End If
                    list.AddRange(FindLogicalChildren(Of T)(depChild))
                End If
            Next
        End If
        Return list
    End Function
End Class
