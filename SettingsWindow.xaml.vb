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

    ' All'apertura della finestra carica lingue, tema, checkbox e account
    Private Sub SettingsWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Try
            ' 1. Set Items for Language Dropdown
            ComboLanguage.ItemsSource = _settingsController.SupportedLanguages
            Dim currentLang = _settingsController.SupportedLanguages.FirstOrDefault(Function(l) l.Code = _settingsController.Language)
            If currentLang IsNot Nothing Then
                ComboLanguage.SelectedItem = currentLang
            End If

        ' 2. Select Theme Selection
        For Each item As ComboBoxItem In ComboTheme.Items
            If item.Content.ToString() = _settingsController.Theme Then
                ComboTheme.SelectedItem = item
                Exit For
            End If
        Next

        ' 3. Set Checkbox checks
        ChkTranslateMessageButton.IsChecked = _settingsController.TranslateMessageButton
        ChkFullPageTranslation.IsChecked = _settingsController.FullPageTranslation
        ChkShowTranslateAllButton.IsChecked = _settingsController.ShowTranslateAllMessagesButton
        ChkTranslateNotifications.IsChecked = _settingsController.TranslateNotifications
        ChkShowTranslateNotificationBtn.IsChecked = _settingsController.ShowTranslateNotificationButton

        ' 4. Bind Accounts List
        AccountsList.ItemsSource = _accountManager.Accounts

        ' 5. Apply Active Theme
        ApplyTheme()

        ' 6. Apply localized UI text
        RefreshLocalization()

        _isInitializing = False

        Catch ex As Exception
            MessageBox.Show(
                "Errore nel caricamento delle impostazioni:" & vbCrLf & vbCrLf &
                ex.ToString(),
                "Errore",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            )
            Me.Close()
        End Try
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

    ' Cambio lingua dal menu a tendina
    Private Async Sub ComboLanguage_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _isInitializing Then Return
        Dim selectedLang = TryCast(ComboLanguage.SelectedItem, LanguageInfo)
        If selectedLang IsNot Nothing Then
            Dim langCode = selectedLang.Code
            Await _settingsController.UpdateLanguageAsync(langCode)

            RefreshLocalization()

            ' Notify active webviews of language update
            Dim langItem = _settingsController.SupportedLanguages.FirstOrDefault(Function(l) l.Code = langCode)
            If langItem IsNot Nothing Then
                Dim langName = langItem.Name
                Dim translatedLangName = If(langCode = "it", "Italiano", langName)
                Dim tooltipLabel = _settingsController.Localizations.Get("translate_to_lang", New Dictionary(Of String, String) From {{"lang", translatedLangName}})

                For Each acc In _accountManager.Accounts
                    Await acc.UpdateWebviewLanguageAsync(langCode, translatedLangName, tooltipLabel, _settingsController.TranslateMessageButton)
                Next
            End If
        End If
    End Sub

    ' Abilita/disabilita opzioni dalle checkbox
    Private Async Sub ChkSetting_Changed(sender As Object, e As RoutedEventArgs)
        If _isInitializing Then Return
        
        Dim chk = CType(sender, CheckBox)
        Select Case chk.Name
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
            Dim langItem = _settingsController.SupportedLanguages.FirstOrDefault(Function(l) l.Code = _settingsController.Language)
            If langItem IsNot Nothing Then
                Dim langName = langItem.Name
                Dim translatedLangName = If(_settingsController.Language = "it", "Italiano", langName)
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

    ' Applica le traduzioni italiane a tutti i label della finestra
    Private Sub RefreshLocalization()
        Dim loc = _settingsController.Localizations
        TitleText.Text = loc.Get("settings")
        SectionTheme.Text = loc.Get("theme")
        LabelAppTheme.Text = loc.Get("match_cohesive")
        SectionLanguage.Text = loc.Get("language")
        LabelSelectLanguage.Text = loc.Get("language")
        ChkTranslateMessageButton.Content = loc.Get("translate_message_button")
        ChkFullPageTranslation.Content = loc.Get("full_page_translation")
        ChkShowTranslateAllButton.Content = loc.Get("show_translate_all_messages_button")
        SectionNotifications.Text = loc.Get("notifications")
        ChkTranslateNotifications.Content = loc.Get("translate_notifications")
        ChkShowTranslateNotificationBtn.Content = loc.Get("show_translate_notification_button")
        SectionAccounts.Text = loc.Get("manage_accounts")
        SectionDevTools.Text = loc.Get("devtools")
        BtnDebugTab.Content = loc.Get("debug_active_tab")
        BtnCheckUpdates.Content = loc.Get("check_now")
    End Sub

    ' Applica tema scuro/chiaro a sfondi, combo e testi
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
            ComboLanguage.Background = New SolidColorBrush(ColorConverter.ConvertFromString("#2a3942"))
            ComboLanguage.Foreground = New SolidColorBrush(ColorConverter.ConvertFromString("#e9edef"))
            ComboTheme.Background = New SolidColorBrush(ColorConverter.ConvertFromString("#2a3942"))
            ComboTheme.Foreground = New SolidColorBrush(ColorConverter.ConvertFromString("#e9edef"))
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
            ComboLanguage.Background = New SolidColorBrush(ColorConverter.ConvertFromString("#ffffff"))
            ComboLanguage.Foreground = New SolidColorBrush(ColorConverter.ConvertFromString("#111b21"))
            ComboTheme.Background = New SolidColorBrush(ColorConverter.ConvertFromString("#ffffff"))
            ComboTheme.Foreground = New SolidColorBrush(ColorConverter.ConvertFromString("#111b21"))
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
