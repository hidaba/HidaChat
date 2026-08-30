Imports System.ComponentModel
Imports System.Windows.Media
Imports System.Windows.Threading

''' <summary>

''' Finestra di dialogo WPF per la gestione delle impostazioni utente (tema, lingua, notifiche, opzioni di traduzione e lista account).
''' </summary>
Public Class SettingsWindow
    Private ReadOnly _settingsController As SettingsController
    Private ReadOnly _accountManager As AccountManager
    Private _isInitializing As Boolean = True

    Private _cachedCheckBoxes As List(Of CheckBox) = Nothing
    Private _cachedTextBlocks As List(Of TextBlock) = Nothing
    Private _cachedAccountBorders As List(Of Border) = Nothing
    Private _cachedAccountTextBoxes As List(Of TextBox) = Nothing

    Public Sub New(settingsController As SettingsController, accountManager As AccountManager)
        InitializeComponent()
        _settingsController = settingsController
        _accountManager = accountManager
    End Sub

    ''' <summary>
    ''' Inizializzazione della finestra all'apertura: imposta i valori dei menu a tendina, 
    ''' stato delle checkbox, elenco degli account e localizzazione della UI.
    ''' </summary>
    Private Sub SettingsWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Try
            ' 1. Imposta gli elementi del dropdown Lingua
            ComboLanguage.ItemsSource = _settingsController.SupportedLanguages
            Dim currentLang = _settingsController.SupportedLanguages.FirstOrDefault(Function(l) l.Code = _settingsController.Language)
            If currentLang IsNot Nothing Then
                ComboLanguage.SelectedItem = currentLang
            End If

            ' 2. Seleziona la modalità del Tema
            For Each item As ComboBoxItem In ComboTheme.Items
                If item.Content.ToString() = _settingsController.Theme Then
                    ComboTheme.SelectedItem = item
                    Exit For
                End If
            Next

            ' 3. Imposta lo stato delle Checkbox
            ChkTranslateMessageButton.IsChecked = _settingsController.TranslateMessageButton
            ChkFullPageTranslation.IsChecked = _settingsController.FullPageTranslation
            ChkShowTranslateAllButton.IsChecked = _settingsController.ShowTranslateAllMessagesButton
            ChkShowMessagePopup.IsChecked = _settingsController.ShowMessagePopup
            
            ' 4. Imposta lo stato della Checkbox Canale Beta
            ChkUseBetaChannel.IsChecked = _settingsController.UseBetaChannel

            ' 4b. Imposta lo stato e il testo del CSS personalizzato (TODO #43)
            ChkEnableCustomCss.IsChecked = _settingsController.EnableCustomCss
            TxtCustomCss.Text = If(_settingsController.CustomCss, String.Empty)

            ' 4c. Imposta lo stato del Correttore Ortografico (TODO #44)
            ChkEnableSpellcheck.IsChecked = _settingsController.EnableSpellcheck
            Dim targetSpellLang = If(String.IsNullOrWhiteSpace(_settingsController.SpellcheckLanguage), "auto", _settingsController.SpellcheckLanguage.ToLowerInvariant())
            For Each item As ComboBoxItem In ComboSpellcheckLanguage.Items
                If item.Tag IsNot Nothing AndAlso item.Tag.ToString().ToLowerInvariant() = targetSpellLang Then
                    ComboSpellcheckLanguage.SelectedItem = item
                    Exit For
                End If
            Next
            If ComboSpellcheckLanguage.SelectedItem Is Nothing AndAlso ComboSpellcheckLanguage.Items.Count > 0 Then
                ComboSpellcheckLanguage.SelectedIndex = 0
            End If

            ' 5. Collega l'elenco degli account
            AccountsList.ItemsSource = _accountManager.Accounts

            ' 6. Applica il tema cromatico corrente
            ApplyTheme()

            ' 7. Applica le stringhe tradotte all'interfaccia utente
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

    Private Sub BtnAbout_Click(sender As Object, e As RoutedEventArgs)
        Dim aboutWin As New AboutWindow(_settingsController)
        aboutWin.Owner = Me
        aboutWin.ShowDialog()
    End Sub

    ''' <summary>
    ''' Gestisce il cambio di selezione del tema dal menu a tendina.
    ''' </summary>
    Private Async Sub ComboTheme_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _isInitializing Then Return
        Dim selectedItem = CType(ComboTheme.SelectedItem, ComboBoxItem)
        If selectedItem IsNot Nothing Then
            Dim themeStr = selectedItem.Content.ToString()
            Await _settingsController.SaveThemeAsync(themeStr)
            ApplyTheme()
        End If
    End Sub

    ''' <summary>
    ''' Gestisce il cambio di lingua selezionata dal menu a tendina e notifica i controlli WebView.
    ''' </summary>
    Private Async Sub ComboLanguage_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _isInitializing Then Return
        Dim selectedLang = TryCast(ComboLanguage.SelectedItem, LanguageInfo)
        If selectedLang IsNot Nothing Then
            Dim langCode = selectedLang.Code
            Await _settingsController.UpdateLanguageAsync(langCode)

            RefreshLocalization()

            ' Notifica alle WebView2 attive l'aggiornamento della lingua
            Dim langItem = _settingsController.SupportedLanguages.FirstOrDefault(Function(l) l.Code = langCode)
            If langItem IsNot Nothing Then
                Dim translatedLangName = langItem.Name
                Dim tooltipLabel = _settingsController.Localizations.Get("translate_to_lang", New Dictionary(Of String, String) From {{"lang", translatedLangName}})

                For Each acc In _accountManager.Accounts
                    Await acc.UpdateWebviewLanguageAsync(langCode, translatedLangName, tooltipLabel, _settingsController.TranslateMessageButton)
                Next
            End If
        End If
    End Sub

    ''' <summary>
    ''' Gestisce l'abilitazione o disabilitazione delle opzioni dalle varie CheckBox della finestra.
    ''' </summary>
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
            Case "ChkShowMessagePopup"
                _settingsController.ShowMessagePopup = chk.IsChecked.Value
                Await _settingsController.SaveSettingAsync("showMessagePopup", chk.IsChecked.Value)
        End Select

        ' Notifica le WebView2 sui cambiamenti del pulsante hover
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
        Dim acc = CType(txt.DataContext, AppAccounts)
        If acc IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(txt.Text) Then
            Await _accountManager.UpdateAccountNameAsync(acc.Id, txt.Text.Trim())
        End If
    End Sub

    ''' <summary>
    ''' Gestisce l'evento di eliminazione di un account previa conferma dell'utente.
    ''' </summary>
    Private Async Sub BtnDeleteAccount_Click(sender As Object, e As RoutedEventArgs)
        Dim btn = CType(sender, Button)
        Dim accountId = TryCast(btn.Tag, String)
        If String.IsNullOrEmpty(accountId) Then Return

        Dim acc = _accountManager.Accounts.FirstOrDefault(Function(a) a.Id = accountId)
        If acc Is Nothing Then Return

        Dim loc = _settingsController.Localizations

        If _accountManager.Accounts.Count <= 1 Then
            MessageBox.Show(
                loc.Get("delete_account_last"),
                loc.Get("delete_account_title"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            )
            Return
        End If

        Dim confirmMsg = loc.Get("delete_account_confirm", New Dictionary(Of String, String) From {{"name", acc.Name}})
        Dim result = MessageBox.Show(
            confirmMsg,
            loc.Get("delete_account_title"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        )
        
        If result = MessageBoxResult.Yes Then
            Await _accountManager.RemoveAccountAsync(accountId)
            _cachedAccountBorders = Nothing
            _cachedAccountTextBoxes = Nothing
            ' Aggiorna la lista nella UI
            AccountsList.ItemsSource = Nothing
            AccountsList.ItemsSource = _accountManager.Accounts
            UpdateAccountsUIState()
        End If
    End Sub

    ''' <summary>
    ''' Gestisce il cambio di piattaforma (WhatsApp / Telegram) di un account dall'elenco nelle impostazioni.
    ''' </summary>
    Private Async Sub ComboAccountPlatform_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _isInitializing Then Return
        Dim cmb = TryCast(sender, ComboBox)
        If cmb Is Nothing Then Return

        Dim accountId = TryCast(cmb.Tag, String)
        If String.IsNullOrEmpty(accountId) Then Return

        Dim selectedItem = TryCast(cmb.SelectedItem, ComboBoxItem)
        If selectedItem Is Nothing Then Return

        Dim newPlatform = TryCast(selectedItem.Tag, String)
        If String.IsNullOrWhiteSpace(newPlatform) Then Return

        Dim targetAcc = _accountManager.Accounts.FirstOrDefault(Function(a) a.Id = accountId)
        If targetAcc IsNot Nothing AndAlso Not String.Equals(targetAcc.Platform, newPlatform, StringComparison.OrdinalIgnoreCase) Then
            targetAcc.SetPlatform(newPlatform)
            Await _accountManager.SaveAccountsAsync()
        End If
    End Sub

    ''' <summary>
    ''' Gestisce l'aggiunta di un nuovo account dalla finestra delle impostazioni con scelta della piattaforma.
    ''' </summary>
    Private Sub BtnAddAccountSettings_Click(sender As Object, e As RoutedEventArgs)
        If Not _accountManager.CanAddAccount Then
            Dim loc = _settingsController.Localizations
            MessageBox.Show(loc.Get("max_accounts_reached"), loc.Get("manage_accounts"), MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim locStrings = _settingsController.Localizations
        Dim menu As New ContextMenu()

        Dim itemWhatsApp As New MenuItem With {
            .Header = locStrings.Get("add_whatsapp_account")
        }
        AddHandler itemWhatsApp.Click, Async Sub()
            Await AddAccountSettingsWithPlatformAsync("WhatsApp")
        End Sub

        Dim itemTelegram As New MenuItem With {
            .Header = locStrings.Get("add_telegram_account")
        }
        AddHandler itemTelegram.Click, Async Sub()
            Await AddAccountSettingsWithPlatformAsync("Telegram")
        End Sub

        menu.Items.Add(itemWhatsApp)
        menu.Items.Add(itemTelegram)
        menu.PlacementTarget = BtnAddAccountSettings
        menu.IsOpen = True
    End Sub

    Private Async Function AddAccountSettingsWithPlatformAsync(platform As String) As Task
        Dim success = Await _accountManager.AddAccountAsync(platform:=platform)
        If success Then
            _cachedAccountBorders = Nothing
            _cachedAccountTextBoxes = Nothing
            AccountsList.ItemsSource = Nothing
            AccountsList.ItemsSource = _accountManager.Accounts
        End If
        UpdateAccountsUIState()
    End Function

    ''' <summary>
    ''' Aggiorna lo stato del conteggio account e del pulsante aggiungi nelle impostazioni.
    ''' </summary>
    Private Sub UpdateAccountsUIState()
        Dim loc = _settingsController.Localizations
        If TxtAccountsCount IsNot Nothing Then
            TxtAccountsCount.Text = loc.Get("accounts_count_info", New Dictionary(Of String, String) From {{"count", _accountManager.Accounts.Count.ToString()}})
        End If
        If BtnAddAccountSettings IsNot Nothing Then
            BtnAddAccountSettings.IsEnabled = _accountManager.CanAddAccount
            BtnAddAccountSettings.Content = loc.Get("add_account")
        End If
    End Sub

    ''' <summary>
    ''' Apre la finestra degli Strumenti di Sviluppo (DevTools) di Edge per la WebView2 dell'account attivo.
    ''' </summary>
    Private Sub BtnDevTools_Click(sender As Object, e As RoutedEventArgs)
        Dim activeAcc = _accountManager.CurrentAccount
        If activeAcc IsNot Nothing AndAlso activeAcc.WebView IsNot Nothing AndAlso activeAcc.WebView.CoreWebView2 IsNot Nothing Then
            activeAcc.WebView.CoreWebView2.OpenDevToolsWindow()
        End If
    End Sub

    ''' <summary>
    ''' Avvia la verifica manuale della presenza di aggiornamenti.
    ''' </summary>
    Private Async Sub BtnCheckUpdates_Click(sender As Object, e As RoutedEventArgs)
        Await UpdateChecker.CheckForUpdatesAsync(_settingsController, _accountManager, force:=True)
    End Sub

    Private Async Sub ChkUseBetaChannel_Changed(sender As Object, e As RoutedEventArgs)
        If _isInitializing Then Return
        Dim chk = CType(sender, CheckBox)
        _settingsController.UseBetaChannel = chk.IsChecked.Value
        Await _settingsController.SaveSettingAsync("useBetaChannel", chk.IsChecked.Value)
    End Sub

    ' --- Gestori eventi Custom CSS (TODO #43) ---
    Private Async Sub ChkEnableCustomCss_Changed(sender As Object, e As RoutedEventArgs)
        If _isInitializing Then Return
        Dim enabled = ChkEnableCustomCss.IsChecked.GetValueOrDefault(False)
        Await _settingsController.SaveCustomCssAsync(enabled, TxtCustomCss.Text)
        For Each acc In _accountManager.Accounts
            Await acc.ApplyCustomCssAsync(_settingsController.CustomCss, enabled)
        Next
    End Sub

    Private Async Sub BtnApplyCustomCss_Click(sender As Object, e As RoutedEventArgs)
        Dim enabled = ChkEnableCustomCss.IsChecked.GetValueOrDefault(False)
        Dim css = TxtCustomCss.Text
        Await _settingsController.SaveCustomCssAsync(enabled, css)
        For Each acc In _accountManager.Accounts
            Await acc.ApplyCustomCssAsync(css, enabled)
        Next
        Dim loc = _settingsController.Localizations
        BtnApplyCustomCss.Content = "✔ " & loc.Get("css_applied")
        Await Task.Delay(1500)
        BtnApplyCustomCss.Content = loc.Get("apply_css")
    End Sub

    Private Sub BtnPresetOled_Click(sender As Object, e As RoutedEventArgs)
        TxtCustomCss.Text = "/* OLED Pure Dark Theme */" & vbCrLf &
                            "body, #app, ._aigv, .two, #main, #side {" & vbCrLf &
                            "  background-color: #000000 !important;" & vbCrLf &
                            "}" & vbCrLf &
                            "._ajyl, ._amj9, .message-in, .message-out {" & vbCrLf &
                            "  background-color: #0d0d0d !important;" & vbCrLf &
                            "}"
        ChkEnableCustomCss.IsChecked = True
    End Sub

    Private Sub BtnPresetCompact_Click(sender As Object, e As RoutedEventArgs)
        TxtCustomCss.Text = "/* Compact UI Layout */" & vbCrLf &
                            "#side, .chatlist-chat, .sidebar-header {" & vbCrLf &
                            "  max-width: 320px !important;" & vbCrLf &
                            "}" & vbCrLf &
                            "._amjv, .chat-item {" & vbCrLf &
                            "  padding: 4px !important;" & vbCrLf &
                            "}"
        ChkEnableCustomCss.IsChecked = True
    End Sub

    Private Sub BtnPresetFont_Click(sender As Object, e As RoutedEventArgs)
        TxtCustomCss.Text = "/* Modern Clean Typography */" & vbCrLf &
                            "* {" & vbCrLf &
                            "  font-family: 'Segoe UI Variable Display', 'Segoe UI', system-ui, sans-serif !important;" & vbCrLf &
                            "}"
        ChkEnableCustomCss.IsChecked = True
    End Sub

    Private Sub BtnPresetReset_Click(sender As Object, e As RoutedEventArgs)
        TxtCustomCss.Text = ""
    End Sub

    ' --- Gestori eventi Correttore Ortografico (TODO #44) ---
    Private Async Sub ChkEnableSpellcheck_Changed(sender As Object, e As RoutedEventArgs)
        If _isInitializing Then Return
        Dim enabled = ChkEnableSpellcheck.IsChecked.GetValueOrDefault(True)
        Dim selectedItem = TryCast(ComboSpellcheckLanguage.SelectedItem, ComboBoxItem)
        Dim spellLang = If(selectedItem?.Tag IsNot Nothing, selectedItem.Tag.ToString(), "auto")
        Await _settingsController.SaveSpellcheckSettingsAsync(enabled, spellLang)
    End Sub

    Private Async Sub ComboSpellcheckLanguage_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _isInitializing Then Return
        Dim selectedItem = TryCast(ComboSpellcheckLanguage.SelectedItem, ComboBoxItem)
        Dim spellLang = If(selectedItem?.Tag IsNot Nothing, selectedItem.Tag.ToString(), "auto")
        Dim enabled = ChkEnableSpellcheck.IsChecked.GetValueOrDefault(True)
        Await _settingsController.SaveSpellcheckSettingsAsync(enabled, spellLang)
    End Sub

    ''' <summary>
    ''' Aggiorna tutti i testi delle etichette della finestra in base alle traduzioni correnti.
    ''' </summary>
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
        ChkShowMessagePopup.Content = loc.Get("show_message_popup")
        SectionAccounts.Text = loc.Get("manage_accounts")
        SectionCustomCss.Text = loc.Get("custom_css")
        ChkEnableCustomCss.Content = loc.Get("enable_custom_css")
        BtnApplyCustomCss.Content = loc.Get("apply_css")
        BtnPresetOled.Content = loc.Get("css_preset_oled")
        BtnPresetCompact.Content = loc.Get("css_preset_compact")
        BtnPresetFont.Content = loc.Get("css_preset_font")
        BtnPresetReset.Content = loc.Get("css_preset_reset")
        SectionSpellcheck.Text = loc.Get("spellchecker")
        ChkEnableSpellcheck.Content = loc.Get("enable_spellchecker")
        LabelSpellcheckLanguage.Text = loc.Get("spellchecker_language")
        CbiSpellAuto.Content = loc.Get("spellchecker_lang_auto")
        TxtSpellcheckHint.Text = loc.Get("spellchecker_restart_hint")
        SectionUpdates.Text = loc.Get("updates")
        ChkUseBetaChannel.Content = loc.Get("use_beta_channel")
        SectionDevTools.Text = loc.Get("devtools")
        BtnDebugTab.Content = loc.Get("debug_active_tab")
        BtnCheckUpdates.Content = loc.Get("check_now")
        UpdateAccountsUIState()
    End Sub

    Private Function GetCachedLogicalChildren(Of T As DependencyObject)(ByRef cache As List(Of T)) As List(Of T)
        If cache Is Nothing Then
            cache = FindLogicalChildren(Of T)(Me)
        End If
        Return cache
    End Function

    ''' <summary>
    ''' Applica i colori del tema chiaro o scuro ai controlli e contenitori della finestra.
    ''' </summary>
    Private Sub ApplyTheme()
        Dim isDark = _settingsController.IsDarkThemeEffective


        Dim fgColor = If(isDark, "#aebac1", "#111b21")
        Dim fgBrush = BrushCache.GetBrush(fgColor)

        SettingsBorder.Background = BrushCache.GetBrush(If(isDark, "#1f2c34", "#ffffff"))
        TitleBar.Background = BrushCache.GetBrush(If(isDark, "#202c33", "#e9edef"))
        ComboLanguage.Background = BrushCache.GetBrush(If(isDark, "#2a3942", "#ffffff"))
        ComboLanguage.Foreground = fgBrush
        ComboTheme.Background = BrushCache.GetBrush(If(isDark, "#2a3942", "#ffffff"))
        ComboTheme.Foreground = fgBrush
        If ComboSpellcheckLanguage IsNot Nothing Then
            ComboSpellcheckLanguage.Background = BrushCache.GetBrush(If(isDark, "#2a3942", "#ffffff"))
            ComboSpellcheckLanguage.Foreground = fgBrush
        End If

        For Each chk In GetCachedLogicalChildren(_cachedCheckBoxes)
            chk.Foreground = fgBrush
        Next
        For Each txt In GetCachedLogicalChildren(_cachedTextBlocks)
            If txt.Style Is Nothing Then
                txt.Foreground = fgBrush
            End If
        Next

        If CustomCssBorder IsNot Nothing Then
            CustomCssBorder.Background = BrushCache.GetBrush(If(isDark, "#111b21", "#f0f2f5"))
            CustomCssBorder.BorderBrush = BrushCache.GetBrush(If(isDark, "#00a884", "#00a884"))
        End If
        If TxtCustomCss IsNot Nothing Then
            TxtCustomCss.Foreground = BrushCache.GetBrush(If(isDark, "#25d366", "#008069"))
        End If

        StyleAccountItems(isDark)
    End Sub

    ''' <summary>
    ''' Applica lo stile cromatico agli elementi della lista degli account.
    ''' </summary>
    Private Sub StyleAccountItems(isDark As Boolean)
        Dispatcher.BeginInvoke(New Action(Sub()
            If _cachedAccountBorders Is Nothing Then
                _cachedAccountBorders = FindVisualChildren(Of Border)(AccountsList)
                _cachedAccountTextBoxes = FindVisualChildren(Of TextBox)(AccountsList)
            End If

            Dim bgColor = If(isDark, "#1f2c34", "#f0f2f5")
            Dim borderColor = If(isDark, "#222e35", "#d1d7db")
            Dim tbBg = If(isDark, "#2a3942", "#ffffff")
            Dim tbFg = If(isDark, "#ffffff", "#111b21")

            For Each border In _cachedAccountBorders
                border.Background = BrushCache.GetBrush(bgColor)
                border.BorderBrush = BrushCache.GetBrush(borderColor)
            Next
            For Each txt As TextBox In _cachedAccountTextBoxes
                txt.Background = BrushCache.GetBrush(tbBg)
                txt.Foreground = BrushCache.GetBrush(tbFg)
                txt.BorderBrush = BrushCache.GetBrush("#00a884")
            Next
        End Sub), DispatcherPriority.Background)
    End Sub

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

    Private Shared Function FindVisualChildren(Of T As DependencyObject)(depObj As DependencyObject) As List(Of T)
        Dim list As New List(Of T)()
        If depObj IsNot Nothing Then
            For i As Integer = 0 To VisualTreeHelper.GetChildrenCount(depObj) - 1
                Dim child = VisualTreeHelper.GetChild(depObj, i)
                If child IsNot Nothing AndAlso TypeOf child Is T Then
                    list.Add(CType(child, T))
                End If
                list.AddRange(FindVisualChildren(Of T)(child))
            Next
        End If
        Return list
    End Function
End Class

