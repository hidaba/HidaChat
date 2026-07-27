Imports System.IO
Imports System.ComponentModel
Imports System.Windows.Interop
Imports Microsoft.Web.WebView2.Wpf
Imports Microsoft.Toolkit.Uwp.Notifications

Public Class MainWindow
    Private ReadOnly _settingsController As New SettingsController()
    Private _accountManager As AccountManager
    Private _trayIcon As System.Windows.Forms.NotifyIcon
    Private _allowExit As Boolean = False

    Public Sub New()
        InitializeComponent()
        VersionText.Text = "v" & Constants.AppVersion
        _accountManager = New AccountManager(_settingsController)
    End Sub

    ' Caricamento iniziale: impostazioni, account, tema, webview, notifiche
    Private Async Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        ' 1. Load User Settings
        Await _settingsController.LoadSettingsAsync()
        
        ' 2. Verifica che WebView2 runtime sia installato
        If Not CheckWebView2Installed() Then
            MessageBox.Show(
                "Il runtime WebView2 non è installato." & vbCrLf & vbCrLf &
                "Scaricalo da: https://developer.microsoft.com/microsoft-edge/webview2/" & vbCrLf & vbCrLf &
                "Oppure esegui lo script di installazione: .\install_webview2.bat",
                "WebView2 mancante",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            )
            Application.Current.Shutdown()
            Return
        End If

        ' 3. Initialize Account Manager and Accounts
        Await _accountManager.LoadAccountsAsync()
        
        ' 4. Apply WPF Theme Colors (Dark/Light) based on loaded settings
        ApplyWpfTheme()
        
        ' 5. Configure System Tray Icon
        ConfigureSystemTray()
        
        ' 6. Set ItemsSource for Horizontal Tabs
        AccountsList.ItemsSource = _accountManager.Accounts
        
        ' 7. Instanciate all WebView2 controls dynamically
        PopulateWebViews()
        
        ' 8. Listen to changes in settings or accounts
        AddHandler _settingsController.PropertyChanged, AddressOf OnSettingsPropertyChanged
        AddHandler _accountManager.PropertyChanged, AddressOf OnAccountManagerPropertyChanged
        
        ' 9. Configure Toast notifications click routing
        ConfigureToastNotifications()
        
        ' 10. Check updates on launch asynchronously
        Dim ignore = UpdateChecker.CheckForUpdatesAsync(_settingsController, _accountManager)
        
        VersionText.Text = "v" & Constants.AppVersion
    End Sub

    Private Sub ConfigureSystemTray()
        _trayIcon = New System.Windows.Forms.NotifyIcon()
        UpdateTrayIconImage()
        _trayIcon.Text = "WhatsappH Portable"
        _trayIcon.Visible = True
        
        ' Double click restores window
        AddHandler _trayIcon.DoubleClick, Sub()
            ToggleWindow()
        End Sub

        ' Context menu
        Dim contextMenu As New System.Windows.Forms.ContextMenuStrip()
        contextMenu.Items.Add("Toggle Window", Nothing, Sub() ToggleWindow())
        contextMenu.Items.Add("-")
        contextMenu.Items.Add("Exit", Nothing, Sub() ExitApplication())
        
        _trayIcon.ContextMenuStrip = contextMenu
    End Sub

    Private Sub UpdateTrayIconImage()
        If _trayIcon Is Nothing Then Return
        Try
            Dim iconName = If(_accountManager.HasAnyNotification, "icon_notification.ico", "icon.ico")
            Dim iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", iconName)
            If File.Exists(iconPath) Then
                _trayIcon.Icon = New System.Drawing.Icon(iconPath)
            End If
        Catch ex As Exception
            Debug.WriteLine($"Failed to update tray icon image: {ex.Message}")
        End Try
    End Sub

    Private Sub ToggleWindow()
        If Me.Visibility = Visibility.Visible Then
            Me.Hide()
        Else
            Me.Show()
            Me.WindowState = WindowState.Normal
            Me.Activate()
            Me.Focus()
        End If
    End Sub

    Private Sub ExitApplication()
        _allowExit = True

        RemoveHandler _settingsController.PropertyChanged, AddressOf OnSettingsPropertyChanged
        RemoveHandler _accountManager.PropertyChanged, AddressOf OnAccountManagerPropertyChanged

        For Each acc In _accountManager.Accounts
            Try
                acc.Dispose()
            Catch
            End Try
        Next

        If _trayIcon IsNot Nothing Then
            _trayIcon.Visible = False
            _trayIcon.Dispose()
        End If
        ' Uninstall toast listeners
        ToastNotificationManagerCompat.Uninstall()
        Application.Current.Shutdown()
    End Sub

    Public Sub ForceExitForUpdate()
        _allowExit = True
        RemoveHandler _settingsController.PropertyChanged, AddressOf OnSettingsPropertyChanged
        RemoveHandler _accountManager.PropertyChanged, AddressOf OnAccountManagerPropertyChanged
        If _trayIcon IsNot Nothing Then
            _trayIcon.Visible = False
            _trayIcon.Dispose()
        End If
        ToastNotificationManagerCompat.Uninstall()
    End Sub

    Private Sub MainWindow_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If Not _allowExit Then
            e.Cancel = True
            Me.Hide()
        End If
    End Sub

    Private Sub TitleBar_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        If e.ChangedButton = MouseButton.Left AndAlso e.ClickCount = 2 Then
            ToggleMaximize()
        ElseIf e.ChangedButton = MouseButton.Left Then
            If Me.WindowState = WindowState.Maximized Then
                Dim pt = Mouse.GetPosition(Me)
                Me.WindowState = WindowState.Normal
                Me.DragMove()
            Else
                Me.DragMove()
            End If
        End If
    End Sub

    Private Sub BtnMinimize_Click(sender As Object, e As RoutedEventArgs)
        Me.WindowState = WindowState.Minimized
    End Sub

    Private Sub BtnMaximize_Click(sender As Object, e As RoutedEventArgs)
        ToggleMaximize()
    End Sub

    Private Sub ToggleMaximize()
        If Me.WindowState = WindowState.Maximized Then
            Me.WindowState = WindowState.Normal
        Else
            Me.WindowState = WindowState.Maximized
        End If
    End Sub

    Private Sub MainWindow_StateChanged(sender As Object, e As EventArgs) Handles Me.StateChanged
        If Me.WindowState = WindowState.Maximized Then
            MaximizeIcon.Data = Geometry.Parse("M4,1 L11,1 L11,8 L9,8 L9,2 L4,2 Z M1,4 L8,4 L8,11 L1,11 Z")
        Else
            MaximizeIcon.Data = Geometry.Parse("M1,1 L11,1 L11,11 L1,11 Z M2,2 L2,10 L10,10 L10,2 Z")
        End If
    End Sub

    Private Sub BtnClose_Click(sender As Object, e As RoutedEventArgs)
        Me.Hide()
    End Sub

    Private Async Sub PopulateWebViews()
        WebViewsGrid.Children.Clear()

        Dim activeAccount = _accountManager.CurrentAccount
        If activeAccount IsNot Nothing Then
            Await EnsureWebViewAsync(activeAccount)
            activeAccount.WebView.Visibility = Visibility.Visible
        End If
    End Sub

    Private Async Function EnsureWebViewAsync(account As WhatsAppAccount) As Task
        If account.WebView Is Nothing Then
            account.WebView = New WebView2()
            account.WebView.HorizontalAlignment = HorizontalAlignment.Stretch
            account.WebView.VerticalAlignment = VerticalAlignment.Stretch
            account.WebView.Visibility = Visibility.Collapsed
            WebViewsGrid.Children.Add(account.WebView)
        End If

        If account.WebView.CoreWebView2 Is Nothing Then
            Await account.SetupWebViewAsync(_settingsController, AddressOf OnAccountNotificationChanged)
        End If
    End Function

    Private Sub OnAccountNotificationChanged(accountId As String, hasNotification As Boolean)
        _accountManager.HandleNotificationStateChanged(accountId, hasNotification)
    End Sub

    Private Async Sub AccountTab_Click(sender As Object, e As RoutedEventArgs)
        Try
            Dim btn = CType(sender, Button)
            Dim accountId = btn.Tag?.ToString()
            If Not String.IsNullOrEmpty(accountId) Then
                Await SwitchToAccountAsync(accountId)
            End If
        Catch ex As Exception
            Debug.WriteLine($"AccountTab_Click error: {ex.Message}")
            MessageBox.Show("Errore nel cambio account: " & ex.Message, "Errore", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Async Sub AccountTabRename_Click(sender As Object, e As RoutedEventArgs)
        Dim menuItem = CType(sender, MenuItem)
        Dim contextMenu = CType(menuItem.Parent, ContextMenu)
        Dim btn = CType(contextMenu.PlacementTarget, Button)
        Dim acc = CType(btn.DataContext, WhatsAppAccount)

        Dim newName = Microsoft.VisualBasic.Interaction.InputBox("Enter new name:", "Rename Account", acc.Name)
        If Not String.IsNullOrWhiteSpace(newName) Then
            Await _accountManager.UpdateAccountNameAsync(acc.Id, newName.Trim())
        End If
    End Sub

    ' Passa alla scheda account selezionata e nasconde/mostra webview corrispondente
    Private Async Function SwitchToAccountAsync(accountId As String) As Task
        Dim prevAccount = _accountManager.CurrentAccount

        Await _accountManager.SwitchAccountAsync(accountId)

        Dim newAccount = _accountManager.CurrentAccount
        If newAccount Is Nothing Then Return

        ' Nascondi precedente
        If prevAccount IsNot Nothing AndAlso prevAccount.WebView IsNot Nothing Then
            prevAccount.WebView.Visibility = Visibility.Collapsed
        End If

        ' Crea/inizializza WebView per il nuovo account se necessario
        Await EnsureWebViewAsync(newAccount)
        newAccount.WebView.Visibility = Visibility.Visible
    End Function

    ' Apre la finestra Impostazioni
    Private Sub BtnSettings_Click(sender As Object, e As RoutedEventArgs)
        Try
            _accountManager.IsDialogOpen = True
            Dim settingsWin As New SettingsWindow(_settingsController, _accountManager)
            settingsWin.Owner = Me
            settingsWin.ShowDialog()
            _accountManager.IsDialogOpen = False

            ApplyWpfTheme()
        Catch ex As Exception
            _accountManager.IsDialogOpen = False
            MessageBox.Show(
                "Errore aprendo le impostazioni:" & vbCrLf & vbCrLf &
                ex.ToString(),
                "Errore",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            )
        End Try
    End Sub

    Private Sub BtnReloadActiveTab_Click(sender As Object, e As RoutedEventArgs)
        Dim activeAcc = _accountManager.CurrentAccount
        If activeAcc IsNot Nothing AndAlso activeAcc.WebView IsNot Nothing AndAlso activeAcc.WebView.CoreWebView2 IsNot Nothing Then
            activeAcc.WebView.CoreWebView2.Reload()
        End If
    End Sub

    Private Sub OnSettingsPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = NameOf(SettingsController.Theme) Then
            ApplyWpfTheme()
        End If
    End Sub

    Private Sub OnAccountManagerPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = NameOf(AccountManager.HasAnyNotification) Then
            UpdateTrayIconImage()
        End If
    End Sub



    ' Verifica che WebView2 runtime sia installato
    Private Shared Function CheckWebView2Installed() As Boolean
        Try
            Dim ver = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString()
            Return Not String.IsNullOrEmpty(ver)
        Catch
            Return False
        End Try
    End Function

    Private Sub ApplyWpfTheme()
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
            RootBorder.Background = New SolidColorBrush(ColorConverter.ConvertFromString("#111b21"))
            RootBorder.BorderBrush = New SolidColorBrush(ColorConverter.ConvertFromString("#2f3e46"))
            TitleBar.Background = New SolidColorBrush(ColorConverter.ConvertFromString("#202c33"))
            TitleText.Foreground = New SolidColorBrush(ColorConverter.ConvertFromString("#e9edef"))
            ' Refresh active theme inside each WebView
            For Each acc In _accountManager.Accounts
                If acc.WebView IsNot Nothing AndAlso acc.WebView.CoreWebView2 IsNot Nothing Then
                    acc.WebView.CoreWebView2.ExecuteScriptAsync(ThemeJsScripts.DarkModeJS)
                End If
            Next
        Else
            RootBorder.Background = New SolidColorBrush(ColorConverter.ConvertFromString("#f0f2f5"))
            RootBorder.BorderBrush = New SolidColorBrush(ColorConverter.ConvertFromString("#d1d7db"))
            TitleBar.Background = New SolidColorBrush(ColorConverter.ConvertFromString("#e9edef"))
            TitleText.Foreground = New SolidColorBrush(ColorConverter.ConvertFromString("#111b21"))
            ' Refresh active theme inside each WebView
            For Each acc In _accountManager.Accounts
                If acc.WebView IsNot Nothing AndAlso acc.WebView.CoreWebView2 IsNot Nothing Then
                    acc.WebView.CoreWebView2.ExecuteScriptAsync(ThemeJsScripts.LightModeJS)
                End If
            Next
        End If
    End Sub

    Private Sub ConfigureToastNotifications()
        ' Handle Toast clicks when app is running or launched from toast
        AddHandler ToastNotificationManagerCompat.OnActivated, Sub(toastArgs)
            Dim args = toastArgs.Argument
            If Not String.IsNullOrEmpty(args) Then
                ' Parse parameters from toast arguments
                Dim accountId = toastArgs.Argument.Split("&"c).FirstOrDefault(Function(s) s.StartsWith("accountId=")).Split("="c)(1)
                Dim notificationId = toastArgs.Argument.Split("&"c).FirstOrDefault(Function(s) s.StartsWith("notificationId=")).Split("="c)(1)
                
                ' Switch to account and restore window on UI Thread
                Application.Current.Dispatcher.Invoke(Async Function() As Task
                    ToggleWindow()
                    Await SwitchToAccountAsync(accountId)
                    
                    Dim acc = _accountManager.Accounts.FirstOrDefault(Function(a) a.Id = accountId)
                    If acc IsNot Nothing AndAlso acc.WebView IsNot Nothing AndAlso acc.WebView.CoreWebView2 IsNot Nothing Then
                        Await acc.WebView.CoreWebView2.ExecuteScriptAsync($"if (window.onNotificationClicked) {{ window.onNotificationClicked('{notificationId}'); }}")
                    End If
                End Function)
            End If
        End Sub
    End Sub

End Class
