Imports System.ComponentModel
Imports System.Threading
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Threading
Imports HidaChat.UI.Controls
Imports HidaChat.UI.Icons
Imports HidaChat.UI.Theme

''' <summary>
''' Modern Fluent Design WPF dialog window for managing HidaChat settings
''' (theme, accent, language, notifications, accounts, translation, network, CSS, storage, and updates).
''' </summary>
Public Class SettingsWindow
    Private ReadOnly _settingsController As SettingsController
    Private ReadOnly _accountManager As AccountManager
    Private _isInitializing As Boolean = True
    Private _isUpdatingSidebar As Boolean = False
    Private _activeNavTab As String = "General"

    ''' <summary>
    ''' Parameterless constructor preserved for Visual Studio XAML Designer compatibility.
    ''' </summary>
    Public Sub New()
        InitializeComponent()
    End Sub

    Public Sub New(settingsController As SettingsController, accountManager As AccountManager)
        InitializeComponent()
        _settingsController = settingsController
        _accountManager = accountManager
    End Sub

    ''' <summary>
    ''' Initializes controls, registers with ThemeManager, binds accounts, and applies localization on load.
    ''' </summary>
    Private Sub SettingsWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If DesignerProperties.GetIsInDesignMode(Me) Then Return

        Try
            ' Register with centralized Fluent ThemeManager and synchronize with SettingsController
            ThemeManager.Instance.RegisterWindow(Me)
            SyncThemeManagerFromSettings(persist:=False)
            InitializeAccentSwatches()

            If _settingsController Is Nothing Then
                _isInitializing = False
                Return
            End If

            ' 1. Populate Language dropdown items
            ComboLanguage.ItemsSource = _settingsController.SupportedLanguages
            Dim currentLang = _settingsController.SupportedLanguages.FirstOrDefault(Function(l) l.Code = _settingsController.Language)
            If currentLang IsNot Nothing Then
                ComboLanguage.SelectedItem = currentLang
            End If

            ' 2. Select Theme mode
            For Each item As ComboBoxItem In ComboTheme.Items
                If item.Content.ToString() = _settingsController.Theme Then
                    ComboTheme.SelectedItem = item
                    Exit For
                End If
            Next

            ' 3. Set CheckBox states
            ChkTranslateMessageButton.IsChecked = _settingsController.TranslateMessageButton
            ChkFullPageTranslation.IsChecked = _settingsController.FullPageTranslation
            ChkShowTranslateAllButton.IsChecked = _settingsController.ShowTranslateAllMessagesButton
            ChkShowMessagePopup.IsChecked = _settingsController.ShowMessagePopup
            ChkTranslationPrivacyConsent.IsChecked = _settingsController.TranslationPrivacyConsent

            ' 3b. Set translation provider and configuration fields
            Dim targetProvider = If(String.IsNullOrWhiteSpace(_settingsController.TranslationProvider), "google", _settingsController.TranslationProvider.ToLowerInvariant())
            For Each item As ComboBoxItem In ComboTranslationProvider.Items
                If item.Tag IsNot Nothing AndAlso item.Tag.ToString().ToLowerInvariant() = targetProvider Then
                    ComboTranslationProvider.SelectedItem = item
                    Exit For
                End If
            Next
            If ComboTranslationProvider.SelectedItem Is Nothing AndAlso ComboTranslationProvider.Items.Count > 0 Then
                ComboTranslationProvider.SelectedIndex = 0
            End If

            TxtLibreTranslateUrl.Text = _settingsController.TranslationLibreTranslateUrl
            TxtLibreTranslateApiKey.Text = _settingsController.TranslationLibreTranslateApiKey
            TxtOllamaUrl.Text = _settingsController.TranslationOllamaUrl
            TxtOllamaModel.Text = _settingsController.TranslationOllamaModel
            TxtOpenAiUrl.Text = _settingsController.TranslationOpenAiUrl
            TxtOpenAiModel.Text = _settingsController.TranslationOpenAiModel
            TxtOpenAiApiKey.Text = _settingsController.TranslationOpenAiApiKey
            UpdateTranslationPanelsVisibility()

            ' 4. Set Beta Channel CheckBox state
            ChkUseBetaChannel.IsChecked = _settingsController.UseBetaChannel

            ' 4b. Set Custom CSS state and text
            ChkEnableCustomCss.IsChecked = _settingsController.EnableCustomCss
            TxtCustomCss.Text = If(_settingsController.CustomCss, String.Empty)

            ' 4c. Set Spellchecker state and language
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

            ' 4d. Set Do Not Disturb mode state
            Dim targetDndMode = If(_settingsController.IsDndActive, _settingsController.DndDurationMode.ToLowerInvariant(), "off")
            For Each item As ComboBoxItem In ComboDndMode.Items
                If item.Tag IsNot Nothing AndAlso item.Tag.ToString().ToLowerInvariant() = targetDndMode Then
                    ComboDndMode.SelectedItem = item
                    Exit For
                End If
            Next
            If ComboDndMode.SelectedItem Is Nothing AndAlso ComboDndMode.Items.Count > 0 Then
                ComboDndMode.SelectedIndex = 0
            End If

            ' 4e. Set MaxAccounts selected value
            Dim currentMax = _settingsController.MaxAccounts.ToString()
            For Each item As ComboBoxItem In ComboMaxAccounts.Items
                If item.Tag IsNot Nothing AndAlso item.Tag.ToString() = currentMax Then
                    ComboMaxAccounts.SelectedItem = item
                    Exit For
                End If
            Next
            If ComboMaxAccounts.SelectedItem Is Nothing AndAlso ComboMaxAccounts.Items.Count > 0 Then
                ComboMaxAccounts.SelectedIndex = 1
            End If

            ' 4f. Set Network Drive Staging CheckBox state
            ChkEnableNetworkStaging.IsChecked = _settingsController.EnableNetworkProfileStaging

            ' 4g. Set Tab Hibernation state and timeout
            ChkEnableTabHibernation.IsChecked = _settingsController.EnableTabHibernation
            Dim currentHibTimeout = _settingsController.TabHibernationMinutes.ToString()
            For Each item As ComboBoxItem In ComboTabHibernationTimeout.Items
                If item.Tag IsNot Nothing AndAlso item.Tag.ToString() = currentHibTimeout Then
                    ComboTabHibernationTimeout.SelectedItem = item
                    Exit For
                End If
            Next
            If ComboTabHibernationTimeout.SelectedItem Is Nothing AndAlso ComboTabHibernationTimeout.Items.Count > 0 Then
                ComboTabHibernationTimeout.SelectedIndex = 1
            End If

            ' 4h. Set Chromium disk cache limit
            Dim currentCacheLimit = _settingsController.DiskCacheLimitMb.ToString()
            For Each item As ComboBoxItem In ComboDiskCacheLimit.Items
                If item.Tag IsNot Nothing AndAlso item.Tag.ToString() = currentCacheLimit Then
                    ComboDiskCacheLimit.SelectedItem = item
                    Exit For
                End If
            Next
            If ComboDiskCacheLimit.SelectedItem Is Nothing AndAlso ComboDiskCacheLimit.Items.Count > 0 Then
                ComboDiskCacheLimit.SelectedIndex = 0
            End If

            ' 5. Bind accounts list when AccountManager is available
            If _accountManager IsNot Nothing Then
                AccountsList.ItemsSource = _accountManager.Accounts
                AddHandler _accountManager.PropertyChanged, AddressOf OnAccountManagerPropertyChanged
            End If

            ' 6. Apply localized strings and build navigation sidebar
            RefreshLocalization()

            If TxtTsnetAuthKey IsNot Nothing Then
                TxtTsnetAuthKey.Text = _settingsController.TsnetAuthKey
            End If
            UpdateTsnetUI()
            AddHandler TsnetManager.Instance.PropertyChanged, AddressOf OnTsnetPropertyChanged

            _isInitializing = False
            SetActiveNavTab("General")

        Catch ex As Exception
            DialogManager.ShowError(
                Me,
                "Settings Error",
                "Failed to load settings:" & vbCrLf & vbCrLf & ex.Message
            )
            Me.Close()
        End Try
    End Sub

    Private Sub SettingsWindow_Closed(sender As Object, e As EventArgs) Handles Me.Closed
        ThemeManager.Instance.UnregisterWindow(Me)
        RemoveHandler TsnetManager.Instance.PropertyChanged, AddressOf OnTsnetPropertyChanged
        If _accountManager IsNot Nothing Then
            RemoveHandler _accountManager.PropertyChanged, AddressOf OnAccountManagerPropertyChanged
        End If
    End Sub

    Private Sub SettingsWindow_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Escape Then
            ' Let open ComboBox dropdowns consume Escape before closing the settings window
            Dim focusedCombo = TryCast(Keyboard.FocusedElement, ComboBox)
            If focusedCombo IsNot Nothing AndAlso focusedCombo.IsDropDownOpen Then Return
            If TypeOf Keyboard.FocusedElement Is ComboBoxItem Then Return

            e.Handled = True
            Me.Close()
        End If
    End Sub

    Private Sub InitializeAccentSwatches()
        ConfigureAccentSwatch(BtnAccentEmerald, DesignTokens.BrandEmeraldAccentHex)
        ConfigureAccentSwatch(BtnAccentBlue, DesignTokens.DefaultAccentHex)
        ConfigureAccentSwatch(BtnAccentViolet, DesignTokens.AccentVioletHex)
        ConfigureAccentSwatch(BtnAccentTeal, DesignTokens.AccentTealHex)
        ConfigureAccentSwatch(BtnAccentRose, DesignTokens.AccentRoseHex)
    End Sub

    Private Shared Sub ConfigureAccentSwatch(btn As Button, hexColor As String)
        If btn Is Nothing Then Return
        btn.Tag = hexColor
        btn.Background = BrushCache.GetBrush(hexColor)
    End Sub

    Private Sub BtnAccentSwatch_Click(sender As Object, e As RoutedEventArgs)
        Dim btn = TryCast(sender, Button)
        Dim hex = TryCast(btn?.Tag, String)
        If String.IsNullOrWhiteSpace(hex) Then Return

        ThemeManager.Instance.SetAccentColor(hex, persist:=True)
        If SettingsToast IsNot Nothing AndAlso btn?.ToolTip IsNot Nothing Then
            SettingsToast.Show(btn.ToolTip.ToString(), SemanticSeverity.Success, durationMs:=2200)
        End If
    End Sub

    ''' <summary>
    ''' Synchronizes the centralized ThemeManager mode with SettingsController.Theme.
    ''' </summary>
    Private Sub SyncThemeManagerFromSettings(persist As Boolean)
        Dim mode = AppThemeMode.System
        If _settingsController IsNot Nothing Then
            Select Case _settingsController.Theme
                Case "Dark"
                    mode = AppThemeMode.Dark
                Case "Light"
                    mode = AppThemeMode.Light
                Case Else
                    mode = AppThemeMode.System
            End Select
        End If
        ThemeManager.Instance.SetTheme(mode, persist:=persist)
    End Sub

    Private Sub OnAccountManagerPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If String.IsNullOrEmpty(e?.PropertyName) OrElse
           e.PropertyName = NameOf(AccountManager.CanAddAccount) OrElse
           e.PropertyName = NameOf(AccountManager.MaxAccounts) OrElse
           e.PropertyName = NameOf(AccountManager.HasExcessAccounts) OrElse
           e.PropertyName = NameOf(AccountManager.ExcessAccountsCount) OrElse
           e.PropertyName = NameOf(AccountManager.Accounts) Then
            UpdateAccountsUIState()
            RebuildSidebarItems()
        End If
    End Sub

    Private Sub BtnAbout_Click(sender As Object, e As RoutedEventArgs)
        Dim aboutWin As New AboutWindow(_settingsController)
        aboutWin.Owner = Me
        aboutWin.ShowDialog()
    End Sub

    Private Sub Sidebar_NavigationSelected(sender As Object, e As NavigationSelectionChangedEventArgs)
        If _isUpdatingSidebar OrElse e?.SelectedItem Is Nothing Then Return
        SetActiveNavTab(e.SelectedItem.Key)
    End Sub

    ''' <summary>
    ''' Activates the specified category panel, collapses all others, and updates header context and sidebar selection.
    ''' </summary>
    Public Sub SetActiveNavTab(tabName As String)
        If String.IsNullOrWhiteSpace(tabName) Then tabName = "General"
        _activeNavTab = tabName

        If PanelGeneral IsNot Nothing Then PanelGeneral.Visibility = If(String.Equals(tabName, "General", StringComparison.OrdinalIgnoreCase), Visibility.Visible, Visibility.Collapsed)
        If PanelAccounts IsNot Nothing Then PanelAccounts.Visibility = If(String.Equals(tabName, "Accounts", StringComparison.OrdinalIgnoreCase), Visibility.Visible, Visibility.Collapsed)
        If PanelTranslation IsNot Nothing Then PanelTranslation.Visibility = If(String.Equals(tabName, "Translation", StringComparison.OrdinalIgnoreCase), Visibility.Visible, Visibility.Collapsed)
        If PanelNetwork IsNot Nothing Then PanelNetwork.Visibility = If(String.Equals(tabName, "Network", StringComparison.OrdinalIgnoreCase), Visibility.Visible, Visibility.Collapsed)
        If PanelCss IsNot Nothing Then PanelCss.Visibility = If(String.Equals(tabName, "Css", StringComparison.OrdinalIgnoreCase), Visibility.Visible, Visibility.Collapsed)
        If PanelStorage IsNot Nothing Then PanelStorage.Visibility = If(String.Equals(tabName, "Storage", StringComparison.OrdinalIgnoreCase), Visibility.Visible, Visibility.Collapsed)
        If PanelAbout IsNot Nothing Then PanelAbout.Visibility = If(String.Equals(tabName, "About", StringComparison.OrdinalIgnoreCase), Visibility.Visible, Visibility.Collapsed)

        If Sidebar IsNot Nothing Then
            _isUpdatingSidebar = True
            Sidebar.SelectByKey(tabName)
            _isUpdatingSidebar = False
        End If

        UpdatePageHeaderForActiveTab()
        If String.Equals(tabName, "Accounts", StringComparison.OrdinalIgnoreCase) Then
            LocalizeAccountItemControls()
        End If
    End Sub

    Private Sub UpdatePageHeaderForActiveTab()
        Dim loc = _settingsController?.Localizations
        If loc Is Nothing OrElse TxtPageTitle Is Nothing OrElse TxtPageSubtitle Is Nothing Then Return

        Select Case _activeNavTab.ToLowerInvariant()
            Case "accounts"
                TxtPageTitle.Text = loc.Get("settings_cat_accounts")
                TxtPageSubtitle.Text = loc.Get("settings_sub_accounts")
            Case "translation"
                TxtPageTitle.Text = loc.Get("settings_cat_translation")
                TxtPageSubtitle.Text = loc.Get("settings_sub_translation")
            Case "network"
                TxtPageTitle.Text = loc.Get("settings_cat_network")
                TxtPageSubtitle.Text = loc.Get("settings_sub_network")
            Case "css"
                TxtPageTitle.Text = loc.Get("settings_cat_css")
                TxtPageSubtitle.Text = loc.Get("settings_sub_css")
            Case "storage"
                TxtPageTitle.Text = loc.Get("settings_cat_storage")
                TxtPageSubtitle.Text = loc.Get("settings_sub_storage")
            Case "about"
                TxtPageTitle.Text = loc.Get("settings_cat_about")
                TxtPageSubtitle.Text = loc.Get("settings_sub_about")
            Case Else
                TxtPageTitle.Text = loc.Get("settings_cat_general")
                TxtPageSubtitle.Text = loc.Get("settings_sub_general")
        End Select
    End Sub

    Private Sub RebuildSidebarItems()
        If Sidebar Is Nothing OrElse _settingsController?.Localizations Is Nothing Then Return
        Dim loc = _settingsController.Localizations
        Dim accountBadge = If(_accountManager IsNot Nothing,
                              $"{_accountManager.Accounts.Count}/{_accountManager.MaxAccounts}",
                              String.Empty)

        Dim items As New List(Of NavigationItem) From {
            New NavigationItem With {.Key = "General", .Title = loc.Get("settings_cat_general"), .IconKey = IconKey.Settings},
            New NavigationItem With {.Key = "Accounts", .Title = loc.Get("settings_cat_accounts"), .IconKey = IconKey.Users, .BadgeText = accountBadge},
            New NavigationItem With {.Key = "Translation", .Title = loc.Get("settings_cat_translation"), .IconKey = IconKey.Sparkles},
            New NavigationItem With {.Key = "Network", .Title = loc.Get("settings_cat_network"), .IconKey = IconKey.Shield},
            New NavigationItem With {.Key = "Css", .Title = loc.Get("settings_cat_css"), .IconKey = IconKey.Palette},
            New NavigationItem With {.Key = "Storage", .Title = loc.Get("settings_cat_storage"), .IconKey = IconKey.Dashboard},
            New NavigationItem With {.Key = "About", .Title = loc.Get("settings_cat_about"), .IconKey = IconKey.Info}
        }

        _isUpdatingSidebar = True
        Sidebar.HeaderTitle = "HidaChat"
        Sidebar.HeaderSubtitle = loc.Get("settings")
        Sidebar.ItemsSource = items
        Sidebar.SelectByKey(_activeNavTab)
        _isUpdatingSidebar = False
    End Sub

    ''' <summary>
    ''' Handles theme dropdown selection changes and updates both SettingsController and ThemeManager live.
    ''' </summary>
    Private Async Sub ComboTheme_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _isInitializing Then Return
        Dim selectedItem = TryCast(ComboTheme.SelectedItem, ComboBoxItem)
        If selectedItem IsNot Nothing Then
            Dim themeStr = selectedItem.Content.ToString()
            Await _settingsController.SaveThemeAsync(themeStr)
            SyncThemeManagerFromSettings(persist:=True)
            UpdateTsnetUI()
        End If
    End Sub

    ''' <summary>
    ''' Handles interface language changes and notifies active WebView2 instances.
    ''' </summary>
    Private Async Sub ComboLanguage_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _isInitializing Then Return
        Dim selectedLang = TryCast(ComboLanguage.SelectedItem, LanguageInfo)
        If selectedLang IsNot Nothing Then
            Dim langCode = selectedLang.Code
            Await _settingsController.UpdateLanguageAsync(langCode)

            RefreshLocalization()

            ' Notify active WebView2 instances of the language update
            If _accountManager IsNot Nothing Then
                Dim langItem = _settingsController.SupportedLanguages.FirstOrDefault(Function(l) l.Code = langCode)
                If langItem IsNot Nothing Then
                    Dim translatedLangName = langItem.Name
                    Dim tooltipLabel = _settingsController.Localizations.Get("translate_to_lang", New Dictionary(Of String, String) From {{"lang", translatedLangName}})

                    For Each acc In _accountManager.Accounts
                        Await acc.UpdateWebviewLanguageAsync(langCode, translatedLangName, tooltipLabel, _settingsController.TranslateMessageButton)
                    Next
                End If
            End If
        End If
    End Sub

    ''' <summary>
    ''' Handles CheckBox option changes across translation and notification settings.
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
            Case "ChkTranslationPrivacyConsent"
                _settingsController.TranslationPrivacyConsent = chk.IsChecked.Value
                Await _settingsController.SaveSettingAsync("translationPrivacyConsent", chk.IsChecked.Value)
                TranslationCacheService.Instance.PrivacyConsentAccepted = chk.IsChecked.Value
        End Select

        ' Notify WebView2 instances when hover translate button visibility changes
        If chk.Name = "ChkTranslateMessageButton" AndAlso _accountManager IsNot Nothing Then
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

    ''' <summary>
    ''' Handles translation provider dropdown selection changes.
    ''' </summary>
    Private Async Sub ComboTranslationProvider_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _isInitializing Then Return
        Dim item = TryCast(ComboTranslationProvider.SelectedItem, ComboBoxItem)
        If item IsNot Nothing AndAlso item.Tag IsNot Nothing Then
            Dim providerId = item.Tag.ToString().ToLowerInvariant()
            _settingsController.TranslationProvider = providerId
            Await _settingsController.SaveSettingAsync("translationProvider", providerId)
            UpdateTranslationPanelsVisibility()
            If TxtTranslationTestStatus IsNot Nothing Then
                TxtTranslationTestStatus.Text = ""
            End If
        End If
    End Sub

    Private Sub UpdateTranslationPanelsVisibility()
        Dim providerId = If(_settingsController?.TranslationProvider, "google").ToLowerInvariant()
        If PanelLibreTranslate IsNot Nothing Then
            PanelLibreTranslate.Visibility = If(providerId = "libretranslate", Visibility.Visible, Visibility.Collapsed)
        End If
        If PanelOllama IsNot Nothing Then
            PanelOllama.Visibility = If(providerId = "ollama", Visibility.Visible, Visibility.Collapsed)
        End If
        If PanelOpenAi IsNot Nothing Then
            PanelOpenAi.Visibility = If(providerId = "openai", Visibility.Visible, Visibility.Collapsed)
        End If
    End Sub

    ''' <summary>
    ''' Persists configuration fields for self-hosted or custom translation providers.
    ''' </summary>
    Private Async Sub TranslationConfig_Changed(sender As Object, e As RoutedEventArgs)
        If _isInitializing Then Return
        If TxtLibreTranslateUrl IsNot Nothing Then
            _settingsController.TranslationLibreTranslateUrl = TxtLibreTranslateUrl.Text.Trim()
            Await _settingsController.SaveSettingAsync("translationLibreTranslateUrl", _settingsController.TranslationLibreTranslateUrl)
        End If
        If TxtLibreTranslateApiKey IsNot Nothing Then
            _settingsController.TranslationLibreTranslateApiKey = TxtLibreTranslateApiKey.Text.Trim()
            Await _settingsController.SaveSettingAsync("translationLibreTranslateApiKey", _settingsController.TranslationLibreTranslateApiKey)
        End If
        If TxtOllamaUrl IsNot Nothing Then
            _settingsController.TranslationOllamaUrl = TxtOllamaUrl.Text.Trim()
            Await _settingsController.SaveSettingAsync("translationOllamaUrl", _settingsController.TranslationOllamaUrl)
        End If
        If TxtOllamaModel IsNot Nothing Then
            _settingsController.TranslationOllamaModel = TxtOllamaModel.Text.Trim()
            Await _settingsController.SaveSettingAsync("translationOllamaModel", _settingsController.TranslationOllamaModel)
        End If
        If TxtOpenAiUrl IsNot Nothing Then
            _settingsController.TranslationOpenAiUrl = TxtOpenAiUrl.Text.Trim()
            Await _settingsController.SaveSettingAsync("translationOpenAiUrl", _settingsController.TranslationOpenAiUrl)
        End If
        If TxtOpenAiModel IsNot Nothing Then
            _settingsController.TranslationOpenAiModel = TxtOpenAiModel.Text.Trim()
            Await _settingsController.SaveSettingAsync("translationOpenAiModel", _settingsController.TranslationOpenAiModel)
        End If
        If TxtOpenAiApiKey IsNot Nothing Then
            _settingsController.TranslationOpenAiApiKey = TxtOpenAiApiKey.Text.Trim()
            Await _settingsController.SaveSettingAsync("translationOpenAiApiKey", _settingsController.TranslationOpenAiApiKey)
        End If
        _settingsController.ApplyTranslationConfig()
    End Sub

    ''' <summary>
    ''' Runs a connectivity test against the currently configured translation provider.
    ''' </summary>
    Private Async Sub BtnTestTranslationProvider_Click(sender As Object, e As RoutedEventArgs)
        If BtnTestTranslationProvider Is Nothing OrElse TxtTranslationTestStatus Is Nothing Then Return

        Dim loc = _settingsController.Localizations
        Dim palette = ThemeManager.Instance.CurrentPalette
        BtnTestTranslationProvider.IsEnabled = False
        TxtTranslationTestStatus.Foreground = BrushCache.GetBrush(palette.TextSecondary)
        TxtTranslationTestStatus.Text = loc.Get("connection_testing")

        Try
            ' Ensure current input fields are persisted before running the test
            TranslationConfig_Changed(Nothing, Nothing)
            Dim provider = TranslationCacheService.Instance.ActiveProvider
            If provider Is Nothing Then
                provider = GoogleTranslationProvider.Instance
            End If

            Using cts As New CancellationTokenSource(TimeSpan.FromSeconds(8))
                Dim testRes = Await provider.TestConnectionAsync(cts.Token)
                If testRes.Success Then
                    TxtTranslationTestStatus.Foreground = BrushCache.GetBrush(palette.Success)
                    Dim args As New Dictionary(Of String, String) From {
                        {"latency", testRes.LatencyMs.ToString()}
                    }
                    TxtTranslationTestStatus.Text = loc.Get("translation_test_success", args)
                Else
                    TxtTranslationTestStatus.Foreground = BrushCache.GetBrush(palette.ErrorColor)
                    Dim errMsg = If(Not String.IsNullOrEmpty(testRes.Message), testRes.Message, "Unreachable")
                    Dim args As New Dictionary(Of String, String) From {
                        {"error", errMsg}
                    }
                    Dim baseFailure = loc.Get("translation_test_failure", args)
                    If errMsg.Contains("429") Then
                        Dim hint = loc.Get("translation_rate_limit_hint")
                        TxtTranslationTestStatus.Text = $"{baseFailure} - {hint}"
                    Else
                        TxtTranslationTestStatus.Text = baseFailure
                    End If
                End If
            End Using
        Catch ex As Exception
            TxtTranslationTestStatus.Foreground = BrushCache.GetBrush(palette.ErrorColor)
            Dim args As New Dictionary(Of String, String) From {
                {"error", ex.Message}
            }
            TxtTranslationTestStatus.Text = loc.Get("translation_test_failure", args)
        Finally
            BtnTestTranslationProvider.IsEnabled = True
        End Try
    End Sub

    Private Async Sub TxtAccountName_LostFocus(sender As Object, e As RoutedEventArgs)
        If _accountManager Is Nothing Then Return
        Dim txt = CType(sender, TextBox)
        Dim acc = TryCast(txt.DataContext, AppAccounts)
        If acc IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(txt.Text) Then
            Await _accountManager.UpdateAccountNameAsync(acc.Id, txt.Text.Trim())
        End If
    End Sub

    Private Async Sub TxtServerUrl_LostFocus(sender As Object, e As RoutedEventArgs)
        If _accountManager Is Nothing Then Return
        Dim txt = CType(sender, TextBox)
        Dim acc = TryCast(txt.DataContext, AppAccounts)
        If acc IsNot Nothing Then
            Dim defaultUrl = acc.PlatformDefinition.DefaultUrl
            Dim newUrl = If(String.IsNullOrWhiteSpace(txt.Text), defaultUrl, txt.Text.Trim())
            If acc.ServerUrl <> newUrl Then
                acc.ServerUrl = newUrl
                If acc.TailscaleIntegration AndAlso acc.PlatformDefinition.RequiresLocalProxy Then
                    Await acc.EnsureLocalProxyAsync(forceUpdate:=True)
                End If
                Await _accountManager.SaveAccountsAsync()
                If acc.WebView?.CoreWebView2 IsNot Nothing Then
                    acc.WebView.CoreWebView2.Navigate(acc.WebUrl)
                End If
            End If
        End If
    End Sub

    Private Async Sub TxtAuthToken_LostFocus(sender As Object, e As RoutedEventArgs)
        If _accountManager Is Nothing Then Return
        Dim txt = CType(sender, TextBox)
        Dim acc = TryCast(txt.DataContext, AppAccounts)
        If acc IsNot Nothing Then
            Dim newToken = If(txt.Text, "").Trim()
            If acc.AuthToken <> newToken Then
                acc.AuthToken = newToken
                Await _accountManager.SaveAccountsAsync()
            End If
        End If
    End Sub

    Private Async Sub ChkAccountTailscale_Click(sender As Object, e As RoutedEventArgs)
        If _accountManager Is Nothing Then Return
        Dim chk = TryCast(sender, CheckBox)
        If chk Is Nothing Then Return
        Dim accountId = TryCast(chk.Tag, String)
        Dim acc = _accountManager.Accounts.FirstOrDefault(Function(a) a.Id = accountId)
        If acc IsNot Nothing Then
            acc.TailscaleIntegration = chk.IsChecked.GetValueOrDefault()
            Await _accountManager.SaveAccountsAsync()
            If acc.TailscaleIntegration AndAlso acc.PlatformDefinition.RequiresLocalProxy Then
                Await acc.EnsureLocalProxyAsync(forceUpdate:=True)
                acc.WebView?.CoreWebView2?.Navigate(acc.WebUrl)
            ElseIf Not acc.TailscaleIntegration Then
                acc.LocalProxyUrl = ""
                Await TsnetManager.Instance.RemoveRouteAsync(acc.Id)
                acc.UpdateWebResourceAuthFilter()
                acc.WebView?.CoreWebView2?.Navigate(acc.WebUrl)
            End If
            UpdateTsnetUI()
        End If
    End Sub

    Private Async Sub ChkAccountInsecureTls_Click(sender As Object, e As RoutedEventArgs)
        If _accountManager Is Nothing Then Return
        Dim chk = TryCast(sender, CheckBox)
        If chk Is Nothing Then Return
        Dim accountId = TryCast(chk.Tag, String)
        Dim acc = _accountManager.Accounts.FirstOrDefault(Function(a) a.Id = accountId)
        If acc IsNot Nothing Then
            acc.AllowInsecureTls = chk.IsChecked.GetValueOrDefault()
            Await _accountManager.SaveAccountsAsync()
            If acc.TailscaleIntegration AndAlso acc.PlatformDefinition.RequiresLocalProxy Then
                Await acc.EnsureLocalProxyAsync(forceUpdate:=True)
            End If
        End If
    End Sub

    Private Async Sub BtnTestConnection_Click(sender As Object, e As RoutedEventArgs)
        If _accountManager Is Nothing Then Return
        Dim btn = TryCast(sender, Button)
        If btn Is Nothing Then Return
        Dim accountId = TryCast(btn.Tag, String)
        Dim acc = _accountManager.Accounts.FirstOrDefault(Function(a) a.Id = accountId)
        If acc Is Nothing Then Return

        Dim loc = _settingsController.Localizations
        Dim palette = ThemeManager.Instance.CurrentPalette
        acc.ConnectionStatusText = If(loc IsNot Nothing, loc.Get("connection_testing"), "Testing...")
        acc.ConnectionStatusTooltip = ""
        acc.ConnectionStatusBrush = BrushCache.GetBrush(palette.Warning)
        btn.IsEnabled = False

        Dim isOnline = False
        Dim isTlsError = False
        Dim tlsErrorDetail As String = ""

        Try
            Dim targetUrl = acc.ServerUrl
            Dim localToken = ""

            If acc.TailscaleIntegration Then
                Dim port = Await acc.EnsureLocalProxyAsync(forceUpdate:=True)
                If port > 0 Then
                    targetUrl = acc.LocalProxyUrl
                    localToken = TsnetManager.Instance.LocalToken
                End If
            End If

            If Not targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) AndAlso Not targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) Then
                targetUrl = "http://" & targetUrl
            End If

            Using handler As New System.Net.Http.SocketsHttpHandler()
                ' Standard TLS certificate validation with optional certificate pinning, custom CA, or explicit bypass (TASK-0082)
                Dim callbackFailureReason As String = Nothing
                handler.SslOptions.RemoteCertificateValidationCallback = Function(message, cert, chain, sslPolicyErrors)
                    Return TlsCertificatePolicy.ValidateCertificate(
                        cert, chain, sslPolicyErrors,
                        acc.AllowInsecureTls,
                        acc.PinnedCertificateHash,
                        acc.CustomCaCertificatePath,
                        callbackFailureReason
                    )
                End Function

                Using client As New System.Net.Http.HttpClient(handler)
                    client.Timeout = TimeSpan.FromSeconds(4.0)
                    If Not String.IsNullOrEmpty(localToken) Then
                        client.DefaultRequestHeaders.Add("X-HidaChat-Local-Token", localToken)
                    End If
                    If acc.PlatformDefinition.RequiresLocalProxy AndAlso Not String.IsNullOrWhiteSpace(acc.AuthToken) Then
                        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {acc.AuthToken}")
                    End If
                    Using response = Await client.GetAsync(targetUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead)
                        If TlsCertificatePolicy.IsTlsProxyResponse(response.Headers, response.StatusCode, tlsErrorDetail) Then
                            isTlsError = True
                            isOnline = False
                        Else
                            isOnline = True
                        End If
                    End Using
                End Using
            End Using
        Catch ex As Exception
            isOnline = False
            If TlsCertificatePolicy.IsTlsFailure(ex, tlsErrorDetail) Then
                isTlsError = True
            End If
        Finally
            btn.IsEnabled = True
        End Try

        If isOnline Then
            acc.ConnectionStatusText = If(loc IsNot Nothing, loc.Get("connection_success"), "Online")
            acc.ConnectionStatusTooltip = ""
            acc.ConnectionStatusBrush = BrushCache.GetBrush(palette.Success)
        ElseIf isTlsError Then
            acc.ConnectionStatusText = If(loc IsNot Nothing, loc.Get("connection_tls_error"), "SSL/TLS certificate error")
            acc.ConnectionStatusTooltip = If(Not String.IsNullOrWhiteSpace(tlsErrorDetail), tlsErrorDetail, "SSL/TLS certificate verification failed.")
            acc.ConnectionStatusBrush = BrushCache.GetBrush(palette.ErrorColor)
        Else
            acc.ConnectionStatusText = If(loc IsNot Nothing, loc.Get("connection_failed"), "Unreachable")
            acc.ConnectionStatusTooltip = ""
            acc.ConnectionStatusBrush = BrushCache.GetBrush(palette.ErrorColor)
        End If
        UpdateTsnetUI()
    End Sub

    Private Sub OnTsnetPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        Dispatcher.Invoke(Sub() UpdateTsnetUI())
    End Sub

    ''' <summary>
    ''' Updates the embedded Tailscale (tsnet) StatusBadge text, semantic severity, and login button visibility.
    ''' </summary>
    Private Sub UpdateTsnetUI()
        If BadgeTsnetStatus Is Nothing Then Return
        Dim ts = TsnetManager.Instance

        Select Case ts.NodeState
            Case "Running"
                BadgeTsnetStatus.Text = If(Not String.IsNullOrEmpty(ts.TailnetIP), $"Online ({ts.TailnetIP})", "Online")
                BadgeTsnetStatus.Severity = SemanticSeverity.Success
                If BtnTsnetLogin IsNot Nothing Then BtnTsnetLogin.Visibility = Visibility.Collapsed
            Case "NeedsLogin"
                BadgeTsnetStatus.Text = "Richiede Login"
                BadgeTsnetStatus.Severity = SemanticSeverity.Warning
                If BtnTsnetLogin IsNot Nothing Then BtnTsnetLogin.Visibility = Visibility.Visible
            Case "Starting"
                BadgeTsnetStatus.Text = "Avvio..."
                BadgeTsnetStatus.Severity = SemanticSeverity.Info
                If BtnTsnetLogin IsNot Nothing Then BtnTsnetLogin.Visibility = Visibility.Collapsed
            Case Else
                BadgeTsnetStatus.Text = "Non attivo"
                BadgeTsnetStatus.Severity = SemanticSeverity.Neutral
                If BtnTsnetLogin IsNot Nothing Then BtnTsnetLogin.Visibility = Visibility.Collapsed
        End Select
    End Sub

    Private Sub BtnTsnetLogin_Click(sender As Object, e As RoutedEventArgs)
        Dim loginUrl = TsnetManager.Instance.LoginUrl
        If Not String.IsNullOrEmpty(loginUrl) Then
            Try
                Process.Start(New ProcessStartInfo(loginUrl) With {.UseShellExecute = True})
            Catch ex As Exception
                Debug.WriteLine($"Error opening login URL: {ex.Message}")
            End Try
        End If
    End Sub

    Private Async Sub TxtTsnetAuthKey_LostFocus(sender As Object, e As RoutedEventArgs)
        Await _settingsController.SaveTsnetAuthKeyAsync(TxtTsnetAuthKey.Text)
    End Sub

    ''' <summary>
    ''' Deletes an account after an explicit destructive confirmation dialog.
    ''' </summary>
    Private Async Sub BtnDeleteAccount_Click(sender As Object, e As RoutedEventArgs)
        If _accountManager Is Nothing Then Return
        Dim btn = CType(sender, Button)
        Dim accountId = TryCast(btn.Tag, String)
        If String.IsNullOrEmpty(accountId) Then Return

        Dim acc = _accountManager.Accounts.FirstOrDefault(Function(a) a.Id = accountId)
        If acc Is Nothing Then Return

        Dim loc = _settingsController.Localizations

        If _accountManager.Accounts.Count <= 1 Then
            DialogManager.ShowWarning(
                Me,
                loc.Get("delete_account_title"),
                loc.Get("delete_account_last")
            )
            Return
        End If

        Dim confirmMsg = loc.Get("delete_account_confirm", New Dictionary(Of String, String) From {{"name", acc.Name}})
        Dim confirmed = DialogManager.ShowConfirm(
            owner:=Me,
            title:=loc.Get("delete_account_title"),
            message:=confirmMsg,
            confirmText:=loc.Get("delete"),
            cancelText:=loc.Get("cancel"),
            isDestructive:=True,
            iconKey:=IconKey.Trash
        )

        If confirmed Then
            Await _accountManager.RemoveAccountAsync(accountId)
            AccountsList.ItemsSource = Nothing
            AccountsList.ItemsSource = _accountManager.Accounts
            UpdateAccountsUIState()
            RebuildSidebarItems()
            LocalizeAccountItemControls()
        End If
    End Sub

    ''' <summary>
    ''' Handles platform switching (WhatsApp / Telegram / Discord / OpenClaw / Hermes) for an account.
    ''' </summary>
    Private Async Sub ComboAccountPlatform_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _isInitializing OrElse _accountManager Is Nothing Then Return
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
            targetAcc.SetPlatform(PlatformRegistry.FromKey(newPlatform))
            Await _accountManager.SaveAccountsAsync()
        End If
    End Sub

    ''' <summary>
    ''' Prevents accidental platform switching when scrolling with mouse wheel over closed ComboBox (TASK-0125).
    ''' Forwards the scroll event to the parent container so page scrolling remains smooth.
    ''' </summary>
    Private Sub ComboAccountPlatform_PreviewMouseWheel(sender As Object, e As MouseWheelEventArgs)
        Dim cmb = TryCast(sender, ComboBox)
        If cmb IsNot Nothing AndAlso Not cmb.IsDropDownOpen Then
            e.Handled = True
            Dim parent = TryCast(cmb.Parent, UIElement)
            If parent IsNot Nothing Then
                Dim newEvent = New MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) With {
                    .RoutedEvent = UIElement.MouseWheelEvent,
                    .Source = sender
                }
                parent.RaiseEvent(newEvent)
            End If
        End If
    End Sub

    ''' <summary>
    ''' Opens the platform selection menu to add a new account from Settings.
    ''' </summary>
    Private Sub BtnAddAccountSettings_Click(sender As Object, e As RoutedEventArgs)
        If _accountManager Is Nothing Then Return
        If Not _accountManager.CanAddAccount Then
            Dim loc = _settingsController.Localizations
            If _accountManager.HasExcessAccounts Then
                DialogManager.ShowWarning(
                    Me,
                    loc.Get("manage_accounts"),
                    loc.Get("accounts_downgrade_blocked", New Dictionary(Of String, String) From {
                        {"count", _accountManager.Accounts.Count.ToString()},
                        {"max", _accountManager.MaxAccounts.ToString()}
                    })
                )
            Else
                DialogManager.ShowInfo(
                    Me,
                    loc.Get("manage_accounts"),
                    loc.Get("max_accounts_reached", New Dictionary(Of String, String) From {{"max", _accountManager.MaxAccounts.ToString()}})
                )
            End If
            Return
        End If

        Dim locStrings = _settingsController.Localizations
        Dim menu As New ContextMenu()

        Dim itemWhatsApp As New MenuItem With {.Header = locStrings.Get("add_whatsapp_account")}
        AddHandler itemWhatsApp.Click, Async Sub()
            Await AddAccountSettingsWithPlatformAsync(ChatPlatform.WhatsApp)
        End Sub

        Dim itemTelegram As New MenuItem With {.Header = locStrings.Get("add_telegram_account")}
        AddHandler itemTelegram.Click, Async Sub()
            Await AddAccountSettingsWithPlatformAsync(ChatPlatform.Telegram)
        End Sub

        Dim itemOpenClaw As New MenuItem With {.Header = locStrings.Get("add_openclaw_account")}
        AddHandler itemOpenClaw.Click, Async Sub()
            Await AddAccountSettingsWithPlatformAsync(ChatPlatform.OpenClaw)
        End Sub

        Dim itemDiscord As New MenuItem With {.Header = locStrings.Get("add_discord_account")}
        AddHandler itemDiscord.Click, Async Sub()
            Await AddAccountSettingsWithPlatformAsync(ChatPlatform.Discord)
        End Sub

        Dim itemHermes As New MenuItem With {.Header = locStrings.Get("add_hermes_account")}
        AddHandler itemHermes.Click, Async Sub()
            Await AddAccountSettingsWithPlatformAsync(ChatPlatform.Hermes)
        End Sub

        menu.Items.Add(itemWhatsApp)
        menu.Items.Add(itemTelegram)
        menu.Items.Add(itemOpenClaw)
        menu.Items.Add(itemDiscord)
        menu.Items.Add(itemHermes)
        menu.PlacementTarget = BtnAddAccountSettings
        menu.IsOpen = True
    End Sub

    Private Async Sub ComboMaxAccounts_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _isInitializing Then Return
        Dim item = TryCast(ComboMaxAccounts.SelectedItem, ComboBoxItem)
        If item IsNot Nothing AndAlso item.Tag IsNot Nothing Then
            Dim maxVal As Integer
            If Integer.TryParse(item.Tag.ToString(), maxVal) Then
                Await _settingsController.SaveMaxAccountsAsync(maxVal)
                UpdateAccountsUIState()
                RebuildSidebarItems()
            End If
        End If
    End Sub

    Private Async Function AddAccountSettingsWithPlatformAsync(platform As ChatPlatform) As Task
        If _accountManager Is Nothing Then Return
        Dim success = Await _accountManager.AddAccountAsync(platformType:=platform)
        If success Then
            AccountsList.ItemsSource = Nothing
            AccountsList.ItemsSource = _accountManager.Accounts
            LocalizeAccountItemControls()
        End If
        UpdateAccountsUIState()
        RebuildSidebarItems()
    End Function

    Private Async Function AddAccountSettingsWithPlatformAsync(platformStr As String) As Task
        Await AddAccountSettingsWithPlatformAsync(PlatformRegistry.FromKey(platformStr))
    End Function

    ''' <summary>
    ''' Updates account count badge, downgrade notice banner, and Add Account button state.
    ''' </summary>
    Private Sub UpdateAccountsUIState()
        If _settingsController Is Nothing Then Return
        Dim loc = _settingsController.Localizations

        If _accountManager Is Nothing Then
            If BtnAddAccountSettings IsNot Nothing Then BtnAddAccountSettings.IsEnabled = False
            Return
        End If

        If BadgeAccountsCount IsNot Nothing Then
            BadgeAccountsCount.Text = loc.Get("accounts_count_info", New Dictionary(Of String, String) From {
                {"count", _accountManager.Accounts.Count.ToString()},
                {"max", _accountManager.MaxAccounts.ToString()}
            })
            BadgeAccountsCount.Severity = If(_accountManager.HasExcessAccounts, SemanticSeverity.Warning, SemanticSeverity.Info)
        End If
        If BtnAddAccountSettings IsNot Nothing Then
            BtnAddAccountSettings.IsEnabled = _accountManager.CanAddAccount
            BtnAddAccountSettings.Content = loc.Get("add_account")
        End If
        If BorderDowngradeNotice IsNot Nothing AndAlso TxtDowngradeNotice IsNot Nothing Then
            If _accountManager.HasExcessAccounts Then
                BorderDowngradeNotice.Visibility = Visibility.Visible
                TxtDowngradeNotice.Text = loc.Get("max_accounts_downgrade_notice", New Dictionary(Of String, String) From {
                    {"count", _accountManager.Accounts.Count.ToString()},
                    {"max", _accountManager.MaxAccounts.ToString()}
                })
            Else
                BorderDowngradeNotice.Visibility = Visibility.Collapsed
            End If
        End If
    End Sub

    ''' <summary>
    ''' Opens Edge DevTools for the currently active account's WebView2 instance.
    ''' </summary>
    Private Sub BtnDevTools_Click(sender As Object, e As RoutedEventArgs)
        Dim activeAcc = _accountManager?.CurrentAccount
        If activeAcc IsNot Nothing AndAlso activeAcc.WebView IsNot Nothing AndAlso activeAcc.WebView.CoreWebView2 IsNot Nothing Then
            activeAcc.WebView.CoreWebView2.OpenDevToolsWindow()
        End If
    End Sub

    ''' <summary>
    ''' Triggers a manual check for updates on GitHub Releases.
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

    Private Async Sub ChkEnableNetworkStaging_Changed(sender As Object, e As RoutedEventArgs)
        If _isInitializing Then Return
        Dim chk = CType(sender, CheckBox)
        Dim enabled = chk.IsChecked.GetValueOrDefault(True)
        _settingsController.EnableNetworkProfileStaging = enabled
        Await _settingsController.SaveSettingAsync("enableNetworkProfileStaging", enabled)
    End Sub

    ' --- Tab Hibernation handlers ---
    Private Async Sub ChkEnableTabHibernation_Changed(sender As Object, e As RoutedEventArgs)
        If _isInitializing Then Return
        Dim enabled = ChkEnableTabHibernation.IsChecked.GetValueOrDefault(True)
        Dim selectedItem = TryCast(ComboTabHibernationTimeout.SelectedItem, ComboBoxItem)
        Dim timeoutMin = 30
        If selectedItem?.Tag IsNot Nothing Then
            Integer.TryParse(selectedItem.Tag.ToString(), timeoutMin)
        End If
        Await _settingsController.SaveTabHibernationSettingsAsync(enabled, timeoutMin)
    End Sub

    Private Async Sub ComboTabHibernationTimeout_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _isInitializing Then Return
        Dim selectedItem = TryCast(ComboTabHibernationTimeout.SelectedItem, ComboBoxItem)
        Dim timeoutMin = 30
        If selectedItem?.Tag IsNot Nothing Then
            Integer.TryParse(selectedItem.Tag.ToString(), timeoutMin)
        End If
        Dim enabled = ChkEnableTabHibernation.IsChecked.GetValueOrDefault(True)
        Await _settingsController.SaveTabHibernationSettingsAsync(enabled, timeoutMin)
    End Sub

    ' --- Disk Cache & Smart Cleanup handlers ---
    Private Async Sub ComboDiskCacheLimit_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _isInitializing Then Return
        Dim selectedItem = TryCast(ComboDiskCacheLimit.SelectedItem, ComboBoxItem)
        Dim limitMb = 256
        If selectedItem?.Tag IsNot Nothing Then
            Integer.TryParse(selectedItem.Tag.ToString(), limitMb)
        End If
        Await _settingsController.SaveDiskCacheLimitAsync(limitMb)
    End Sub

    Private Async Sub BtnOptimizeStorage_Click(sender As Object, e As RoutedEventArgs)
        If _accountManager Is Nothing Then Return
        Try
            Dim loc = _settingsController.Localizations
            BtnOptimizeStorage.IsEnabled = False
            If SettingsLoading IsNot Nothing Then
                SettingsLoading.Message = If(loc IsNot Nothing, loc.Get("optimize_storage_btn"), "Optimizing storage...")
                SettingsLoading.IsLoading = True
            End If

            Dim compactedCount = Await _accountManager.CompactAllWebViewsProfileAsync()

            Dim okTitle = If(loc IsNot Nothing, loc.Get("optimize_storage_success_title"), "Storage Optimized")
            Dim okMsg = If(loc IsNot Nothing, loc.Get("optimize_storage_success_msg"), "Live cache compaction completed while preserving account sessions.")
            If SettingsToast IsNot Nothing Then
                SettingsToast.Show(okMsg, SemanticSeverity.Success, title:=okTitle, durationMs:=4500)
            End If
        Catch ex As Exception
            Debug.WriteLine($"BtnOptimizeStorage_Click error: {ex.Message}")
            If SettingsToast IsNot Nothing Then
                SettingsToast.Show(ex.Message, SemanticSeverity.Error)
            End If
        Finally
            If SettingsLoading IsNot Nothing Then SettingsLoading.IsLoading = False
            BtnOptimizeStorage.IsEnabled = True
        End Try
    End Sub

    Private Async Sub BtnSmartCleanCache_Click(sender As Object, e As RoutedEventArgs)
        If _accountManager Is Nothing Then Return
        Try
            Dim loc = _settingsController.Localizations
            Dim confirmTitle = If(loc IsNot Nothing, loc.Get("smart_clean_cache_confirm_title"), "Clean Cache")
            Dim confirmMsg = If(loc IsNot Nothing, loc.Get("smart_clean_cache_confirm_msg"), "Clean temporary files and browser cache for all accounts? Active login sessions will be preserved.")

            Dim confirmed = DialogManager.ShowConfirm(
                owner:=Me,
                title:=confirmTitle,
                message:=confirmMsg,
                confirmText:=If(loc IsNot Nothing, loc.Get("smart_clean_cache_btn"), "Clean Cache"),
                cancelText:=If(loc IsNot Nothing, loc.Get("cancel"), "Cancel"),
                isDestructive:=False,
                iconKey:=IconKey.Trash
            )
            If Not confirmed Then Return

            BtnSmartCleanCache.IsEnabled = False
            If SettingsLoading IsNot Nothing Then
                SettingsLoading.Message = If(loc IsNot Nothing, loc.Get("smart_clean_cache_btn"), "Cleaning cache...")
                SettingsLoading.IsLoading = True
            End If

            Await _accountManager.SmartCleanCachePreservingLoginAsync()

            Dim okTitle = If(loc IsNot Nothing, loc.Get("smart_clean_cache_success_title"), "Cleanup Complete")
            Dim okMsg = If(loc IsNot Nothing, loc.Get("smart_clean_cache_success_msg"), "Cache cleanup completed while preserving account sessions.")
            If SettingsToast IsNot Nothing Then
                SettingsToast.Show(okMsg, SemanticSeverity.Success, title:=okTitle, durationMs:=4500)
            End If
        Catch ex As Exception
            Debug.WriteLine($"BtnSmartCleanCache_Click error: {ex.Message}")
            If SettingsToast IsNot Nothing Then
                SettingsToast.Show(ex.Message, SemanticSeverity.Error)
            End If
        Finally
            If SettingsLoading IsNot Nothing Then SettingsLoading.IsLoading = False
            BtnSmartCleanCache.IsEnabled = True
        End Try
    End Sub

    ' --- Custom CSS handlers ---
    Private Async Sub ChkEnableCustomCss_Changed(sender As Object, e As RoutedEventArgs)
        If _isInitializing Then Return
        Dim enabled = ChkEnableCustomCss.IsChecked.GetValueOrDefault(False)
        Await _settingsController.SaveCustomCssAsync(enabled, TxtCustomCss.Text)
        If _accountManager IsNot Nothing Then
            For Each acc In _accountManager.Accounts
                Await acc.ApplyCustomCssAsync(_settingsController.CustomCss, enabled)
            Next
        End If
    End Sub

    Private Async Sub BtnApplyCustomCss_Click(sender As Object, e As RoutedEventArgs)
        Dim enabled = ChkEnableCustomCss.IsChecked.GetValueOrDefault(False)
        Dim css = TxtCustomCss.Text
        Await _settingsController.SaveCustomCssAsync(enabled, css)
        If _accountManager IsNot Nothing Then
            For Each acc In _accountManager.Accounts
                Await acc.ApplyCustomCssAsync(css, enabled)
            Next
        End If
        Dim loc = _settingsController.Localizations
        If SettingsToast IsNot Nothing Then
            SettingsToast.Show(loc.Get("css_applied"), SemanticSeverity.Success, durationMs:=2500)
        End If
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

    ' --- Spellchecker handlers ---
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

    ' --- Do Not Disturb handlers ---
    Private Async Sub ComboDndMode_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _isInitializing Then Return
        Dim selectedItem = TryCast(ComboDndMode.SelectedItem, ComboBoxItem)
        Dim mode = If(selectedItem?.Tag IsNot Nothing, selectedItem.Tag.ToString(), "off")
        Await _settingsController.SetDndModeAsync(mode)
        UpdateDndSettingsText()
    End Sub

    Private Sub UpdateDndSettingsText()
        Dim loc = _settingsController.Localizations
        If loc Is Nothing Then Return
        If _settingsController.IsDndActive Then
            TxtDndStatus.Text = _settingsController.GetDndStatusText(loc)
        Else
            TxtDndStatus.Text = loc.Get("dnd_enable")
        End If
    End Sub

    ''' <summary>
    ''' Applies all localized strings across cards, labels, sidebar navigation, and buttons.
    ''' </summary>
    Private Sub RefreshLocalization()
        Dim loc = _settingsController.Localizations
        Me.Title = loc.Get("settings")

        If BtnAbout IsNot Nothing Then BtnAbout.Content = loc.Get("about")
        If BtnOpenAboutDialog IsNot Nothing Then BtnOpenAboutDialog.Content = loc.Get("about")

        RebuildSidebarItems()
        UpdatePageHeaderForActiveTab()

        If TxtSidebarVersion IsNot Nothing Then
            TxtSidebarVersion.Text = $"HidaChat v{Constants.AppVersion}"
        End If
        If BadgeAboutVersion IsNot Nothing Then
            BadgeAboutVersion.Text = $"v{Constants.AppVersion}"
        End If

        CardTheme.Header = loc.Get("theme")
        LabelAppTheme.Text = loc.Get("match_cohesive")
        If LabelAccentColor IsNot Nothing Then LabelAccentColor.Text = loc.Get("accent_color")
        CardLanguage.Header = loc.Get("language")
        LabelSelectLanguage.Text = loc.Get("language")

        ChkTranslateMessageButton.Content = loc.Get("translate_message_button")
        ChkFullPageTranslation.Content = loc.Get("full_page_translation")
        ChkShowTranslateAllButton.Content = loc.Get("show_translate_all_messages_button")
        If ChkTranslationPrivacyConsent IsNot Nothing Then
            ChkTranslationPrivacyConsent.Content = loc.Get("translation_privacy_consent_checkbox")
        End If
        If CardTranslationProvider IsNot Nothing Then
            CardTranslationProvider.Header = loc.Get("translation_provider")
        End If
        If LabelTranslationProvider IsNot Nothing Then
            LabelTranslationProvider.Text = loc.Get("translation_provider")
        End If
        If TxtLibreTranslateUrl IsNot Nothing Then TxtLibreTranslateUrl.Label = loc.Get("translation_url")
        If TxtLibreTranslateApiKey IsNot Nothing Then TxtLibreTranslateApiKey.Label = loc.Get("translation_api_key")
        If TxtOllamaUrl IsNot Nothing Then TxtOllamaUrl.Label = loc.Get("translation_url")
        If TxtOllamaModel IsNot Nothing Then TxtOllamaModel.Label = loc.Get("translation_model")
        If TxtOpenAiUrl IsNot Nothing Then TxtOpenAiUrl.Label = loc.Get("translation_url")
        If TxtOpenAiModel IsNot Nothing Then TxtOpenAiModel.Label = loc.Get("translation_model")
        If TxtOpenAiApiKey IsNot Nothing Then TxtOpenAiApiKey.Label = loc.Get("translation_api_key")
        If BtnTestTranslationProvider IsNot Nothing Then
            BtnTestTranslationProvider.Content = loc.Get("translation_test_btn")
        End If

        CardNotifications.Header = loc.Get("notifications")
        ChkShowMessagePopup.Content = loc.Get("show_message_popup")
        LabelDndMode.Text = loc.Get("dnd_mode")
        CbiDndOff.Content = loc.Get("dnd_off")
        CbiDnd30m.Content = loc.Get("dnd_30m")
        CbiDnd1h.Content = loc.Get("dnd_1h")
        CbiDnd2h.Content = loc.Get("dnd_2h")
        CbiDnd8h.Content = loc.Get("dnd_8h")
        CbiDndIndefinite.Content = loc.Get("dnd_indefinite")
        UpdateDndSettingsText()

        SectionAccounts.Text = loc.Get("manage_accounts")
        If LabelMaxAccounts IsNot Nothing Then
            LabelMaxAccounts.Text = loc.Get("max_accounts")
        End If

        CardCustomCss.Header = loc.Get("custom_css")
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

        If CardTsnet IsNot Nothing Then CardTsnet.Header = loc.Get("tsnet_section_title")
        If TxtTsnetDescription IsNot Nothing Then TxtTsnetDescription.Text = loc.Get("tsnet_section_desc")
        If TxtTsnetAuthKey IsNot Nothing Then
            TxtTsnetAuthKey.Label = loc.Get("tsnet_authkey_label")
            TxtTsnetAuthKey.ToolTip = loc.Get("tsnet_authkey_tooltip")
        End If

        If CardNetworkStaging IsNot Nothing Then CardNetworkStaging.Header = loc.Get("network_drive_section")
        If ChkEnableNetworkStaging IsNot Nothing Then ChkEnableNetworkStaging.Content = loc.Get("enable_network_profile_staging")
        If TxtNetworkStagingHint IsNot Nothing Then TxtNetworkStagingHint.Text = loc.Get("enable_network_profile_staging_hint")

        If CardHibernation IsNot Nothing Then CardHibernation.Header = loc.Get("tab_hibernation_section")
        If ChkEnableTabHibernation IsNot Nothing Then ChkEnableTabHibernation.Content = loc.Get("enable_tab_hibernation")
        If LabelTabHibernationTimeout IsNot Nothing Then LabelTabHibernationTimeout.Text = loc.Get("tab_hibernation_timeout")
        If TxtTabHibernationHint IsNot Nothing Then TxtTabHibernationHint.Text = loc.Get("enable_tab_hibernation_hint")

        If CardCache IsNot Nothing Then CardCache.Header = loc.Get("cache_section")
        If LabelDiskCacheLimit IsNot Nothing Then LabelDiskCacheLimit.Text = loc.Get("disk_cache_limit")
        If TxtDiskCacheLimitHint IsNot Nothing Then TxtDiskCacheLimitHint.Text = loc.Get("disk_cache_limit_hint")
        If BtnOptimizeStorage IsNot Nothing Then BtnOptimizeStorage.Content = loc.Get("optimize_storage_btn")
        If TxtOptimizeStorageHint IsNot Nothing Then TxtOptimizeStorageHint.Text = loc.Get("optimize_storage_hint")
        If BtnSmartCleanCache IsNot Nothing Then BtnSmartCleanCache.Content = loc.Get("smart_clean_cache_btn")
        If TxtSmartCleanCacheHint IsNot Nothing Then TxtSmartCleanCacheHint.Text = loc.Get("smart_clean_cache_hint")

        CardUpdates.Header = loc.Get("updates")
        ChkUseBetaChannel.Content = loc.Get("use_beta_channel")
        CardDevTools.Header = loc.Get("devtools")
        BtnDebugTab.Content = loc.Get("debug_active_tab")
        BtnCheckUpdates.Content = loc.Get("check_now")

        UpdateAccountsUIState()
        LocalizeAccountItemControls()
    End Sub

    ''' <summary>
    ''' Localizes template-generated buttons and checkboxes inside AccountsList without altering DynamicResource theme brushes.
    ''' </summary>
    Private Sub LocalizeAccountItemControls()
        Dim loc = _settingsController?.Localizations
        If loc Is Nothing OrElse AccountsList Is Nothing Then Return

        Dispatcher.BeginInvoke(New Action(Sub()
            For Each btn In FindVisualChildren(Of Button)(AccountsList)
                If btn.Name = "BtnDeleteAccount" Then
                    btn.Content = loc.Get("delete")
                ElseIf btn.Name = "BtnTestAccountConnection" Then
                    btn.Content = loc.Get("test_connection")
                End If
            Next

            For Each chk In FindVisualChildren(Of CheckBox)(AccountsList)
                If chk.Name = "ChkAccountTailscale" Then
                    chk.Content = loc.Get("account_use_tailscale")
                ElseIf chk.Name = "ChkAccountInsecureTls" Then
                    chk.Content = loc.Get("account_insecure_tls")
                End If
            Next
        End Sub), DispatcherPriority.Background)
    End Sub

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
