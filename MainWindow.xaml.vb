Imports System.IO
Imports System.ComponentModel
Imports System.Runtime.InteropServices
Imports System.Windows.Interop
Imports System.Windows.Media.Effects
Imports Microsoft.Web.WebView2.Wpf
Imports Microsoft.Toolkit.Uwp.Notifications

''' <summary>
''' Finestra principale dell'applicazione WPF. Gestisce l'interfaccia a schede degli account WhatsApp,
''' l'icona nella tray di sistema Windows, l'applicazione dei temi e le notifiche Toast.
''' </summary>
Public Class MainWindow
    Private ReadOnly _settingsController As New SettingsController()
    Private _accountManager As AccountManager
    Private _trayIcon As System.Windows.Forms.NotifyIcon
    Private _allowExit As Boolean = False
    Private _defaultShadowEffect As Effect

    Public Sub New()
        InitializeComponent()
        _defaultShadowEffect = Me.Effect
        VersionText.Text = "v" & Constants.AppVersion
        _accountManager = New AccountManager(_settingsController)
    End Sub

#Region "Win32 Interop - Taskbar & Window Sizing"

    <StructLayout(LayoutKind.Sequential)>
    Private Structure POINT
        Public x As Integer
        Public y As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure MINMAXINFO
        Public ptReserved As POINT
        Public ptMaxSize As POINT
        Public ptMaxPosition As POINT
        Public ptMinTrackSize As POINT
        Public ptMaxTrackSize As POINT
    End Structure

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Auto)>
    Private Structure MONITORINFO
        Public cbSize As Integer
        Public rcMonitor As RECT
        Public rcWork As RECT
        Public dwFlags As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure RECT
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure

    Private Const WM_GETMINMAXINFO As Integer = &H24
    Private Const WM_SYSCOMMAND As Integer = &H112
    Private Const SC_SIZE As Integer = &HF000
    Private Const MONITOR_DEFAULTTONEAREST As Integer = 2

    <DllImport("user32.dll")>
    Private Shared Function MonitorFromWindow(handle As IntPtr, flags As Integer) As IntPtr
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function GetMonitorInfo(hMonitor As IntPtr, ByRef lpmi As MONITORINFO) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    Protected Overrides Sub OnSourceInitialized(e As EventArgs)
        MyBase.OnSourceInitialized(e)
        Dim handle = New WindowInteropHelper(Me).Handle
        Dim source = HwndSource.FromHwnd(handle)
        source?.AddHook(AddressOf WindowProc)
    End Sub

    Private Function WindowProc(hwnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        If msg = WM_GETMINMAXINFO Then
            WmGetMinMaxInfo(hwnd, lParam)
            handled = True
        End If
        Return IntPtr.Zero
    End Function

    ''' <summary>
    ''' Limita l'ingrandimento della finestra alla Working Area dello schermo corrente per evitare di coprire la barra delle applicazioni di Windows.
    ''' </summary>
    Private Shared Sub WmGetMinMaxInfo(hwnd As IntPtr, lParam As IntPtr)
        Dim mmi As MINMAXINFO = Marshal.PtrToStructure(Of MINMAXINFO)(lParam)
        Dim monitor As IntPtr = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST)
        If monitor <> IntPtr.Zero Then
            Dim monitorInfo As New MONITORINFO()
            monitorInfo.cbSize = Marshal.SizeOf(GetType(MONITORINFO))
            GetMonitorInfo(monitor, monitorInfo)

            Dim rcWorkArea As RECT = monitorInfo.rcWork
            Dim rcMonitorArea As RECT = monitorInfo.rcMonitor

            mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left)
            mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top)
            mmi.ptMaxSize.x = Math.Abs(rcWorkArea.Right - rcWorkArea.Left)
            mmi.ptMaxSize.y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top)
            mmi.ptMaxTrackSize.x = mmi.ptMaxSize.x
            mmi.ptMaxTrackSize.y = mmi.ptMaxSize.y
        End If
        Marshal.StructureToPtr(mmi, lParam, False)
    End Sub

#End Region

    ''' <summary>
    ''' Caricamento iniziale dell'applicazione: caricamento impostazioni utente, verifica WebView2,
    ''' inizializzazione degli account, applicazione tema, configurazione tray icon e controllo aggiornamenti.
    ''' </summary>
    Private Async Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        ' 1. Carica le impostazioni utente dal file JSON
        Await _settingsController.LoadSettingsAsync()
        
        ' 2. Verifica che il runtime Microsoft Edge WebView2 sia installato nel sistema
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

        ' 3. Inizializza l'AccountManager ed effettua il caricamento degli account
        Await _accountManager.LoadAccountsAsync()
        
        ' 4. Applica il tema WPF (Scuro/Chiaro) in base alle impostazioni caricate
        Await ApplyWpfThemeAsync()
        
        ' 5. Configura l'icona nell'area di notifica (System Tray)
        ConfigureSystemTray()
        
        ' 6. Collega l'elenco account alla barra delle schede orizzontale
        AccountsList.ItemsSource = _accountManager.Accounts
        UpdateAddAccountButtonState()
        
        ' 7. Istanzia e configura i controlli WebView2 per gli account
        PopulateWebViews()
        
        ' 8. Registra i listener per i cambiamenti di proprietà nelle impostazioni e negli account
        AddHandler _settingsController.PropertyChanged, AddressOf OnSettingsPropertyChanged
        AddHandler _accountManager.PropertyChanged, AddressOf OnAccountManagerPropertyChanged
        
        ' 9. Configura il routing dei click sulle notifiche Toast di Windows
        ConfigureToastNotifications()
        
        ' 10. Verifica in background la disponibilità di aggiornamenti all'avvio
        Dim ignore = UpdateChecker.CheckForUpdatesAsync(_settingsController, _accountManager)
        
        VersionText.Text = "v" & Constants.AppVersion
    End Sub

    ''' <summary>
    ''' Inizializza l'icona nell'area di notifica di Windows (System Tray) con menu contestuale.
    ''' </summary>
    Private Sub ConfigureSystemTray()
        _trayIcon = New System.Windows.Forms.NotifyIcon()
        UpdateTrayIconImage()
        _trayIcon.Text = "HidaChat"
        _trayIcon.Visible = True
        
        ' Il doppio click sulla tray icon alterna la visibilità della finestra
        AddHandler _trayIcon.DoubleClick, Sub()
            ToggleWindow()
        End Sub

        ' Menu contestuale tasto destro
        Dim contextMenu As New System.Windows.Forms.ContextMenuStrip()
        contextMenu.Items.Add("Toggle Window", Nothing, Sub() ToggleWindow())
        contextMenu.Items.Add("About", Nothing, Sub() OpenAboutWindow())
        contextMenu.Items.Add("-")
        contextMenu.Items.Add("Exit", Nothing, Sub() ExitApplication())
        
        _trayIcon.ContextMenuStrip = contextMenu
    End Sub

    ''' <summary>
    ''' Aggiorna l'immagine dell'icona nella tray (mostra l'icona con pallino di notifica se sono presenti notifiche non lette).
    ''' </summary>
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

    ''' <summary>
    ''' Alterna lo stato di visibilità della finestra principale (nascondi/mostra e porta in primo piano).
    ''' </summary>
    Private Sub ToggleWindow()
        If Me.Visibility = Visibility.Visible Then
            Me.Hide()
        Else
            ShowWindow()
        End If
    End Sub

    ''' <summary>
    ''' Mostra e porta in primo piano la finestra principale senza nasconderla se è già visibile.
    ''' </summary>
    Public Sub ShowWindow()
        If Me.Visibility <> Visibility.Visible Then
            Me.Show()
        End If
        If Me.WindowState = WindowState.Minimized Then
            Me.WindowState = WindowState.Normal
        End If
        Me.Activate()
        Me.Focus()
    End Sub

    ''' <summary>
    ''' Esegue la chiusura definitiva dell'applicazione liberando tutte le risorse allocati e rimuovendo i listener.
    ''' </summary>
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
        ' Disinstalla i listener per le notifiche toast
        ToastNotificationManagerCompat.Uninstall()
        Application.Current.Shutdown()
    End Sub

    ''' <summary>
    ''' Forza l'uscita dell'applicazione senza conferma per consentire l'avvio della procedura di aggiornamento automatico.
    ''' </summary>
    Public Sub ForceExitForUpdate()
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
        ToastNotificationManagerCompat.Uninstall()

        ' Rilascia il Mutex dell'istanza singola prima che lo script di aggiornamento avvii il nuovo processo
        Application.ReleaseSingleInstanceMutex()
    End Sub

    ''' <summary>
    ''' Intercetta la chiusura della finestra: invece di chiudere l'applicazione la nasconde nella system tray (riduzione a icona).
    ''' </summary>
    Private Sub MainWindow_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If Not _allowExit Then
            e.Cancel = True
            Me.Hide()
        End If
    End Sub

    ''' <summary>
    ''' Gestisce il trascinamento della finestra ed il doppio click per ingrandire/ripristinare dalla barra del titolo.
    ''' </summary>
    Private Sub TitleBar_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        If e.ChangedButton = MouseButton.Left AndAlso e.ClickCount = 2 Then
            ToggleMaximize()
        ElseIf e.ChangedButton = MouseButton.Left Then
            If Me.WindowState = WindowState.Normal Then
                Me.DragMove()
            End If
        End If
    End Sub

    ''' <summary>
    ''' Consente di ripristinare e trascinare fluidamente la finestra partendo dallo stato ingrandito.
    ''' </summary>
    Private Sub TitleBar_MouseMove(sender As Object, e As MouseEventArgs)
        If e.LeftButton = MouseButtonState.Pressed AndAlso Me.WindowState = WindowState.Maximized Then
            Dim mousePos = PointToScreen(e.GetPosition(Me))
            Dim percentX = e.GetPosition(Me).X / Me.ActualWidth

            Me.WindowState = WindowState.Normal

            Me.Left = mousePos.X - (Me.ActualWidth * percentX)
            Me.Top = mousePos.Y - (TitleBar.ActualHeight / 2)

            Try
                Me.DragMove()
            Catch
            End Try
        End If
    End Sub

    Private Sub BtnMinimize_Click(sender As Object, e As RoutedEventArgs)
        Me.WindowState = WindowState.Minimized
    End Sub

    Private Sub BtnMaximize_Click(sender As Object, e As RoutedEventArgs)
        ToggleMaximize()
    End Sub

    ''' <summary>
    ''' Alterna lo stato ingrandito a finestra/normale.
    ''' </summary>
    Private Sub ToggleMaximize()
        If Me.WindowState = WindowState.Maximized Then
            Me.WindowState = WindowState.Normal
        Else
            Me.WindowState = WindowState.Maximized
        End If
    End Sub

    ''' <summary>
    ''' Aggiorna l'icona del pulsante ingrandisci/ripristina e lo stile dei bordi in base allo stato della finestra.
    ''' </summary>
    Private Sub MainWindow_StateChanged(sender As Object, e As EventArgs) Handles Me.StateChanged
        If Me.WindowState = WindowState.Maximized Then
            MaximizeIcon.Data = Geometry.Parse("M4,1 L11,1 L11,8 L9,8 L9,2 L4,2 Z M1,4 L8,4 L8,11 L1,11 Z")
            BtnMaximize.ToolTip = "Ripristina"
            RootBorder.CornerRadius = New CornerRadius(0)
            RootBorder.BorderThickness = New Thickness(0)
            Me.Effect = Nothing
            If ResizeGrip IsNot Nothing Then
                ResizeGrip.Visibility = Visibility.Collapsed
            End If
        Else
            MaximizeIcon.Data = Geometry.Parse("M1,1 L11,1 L11,11 L1,11 Z M2,2 L2,10 L10,10 L10,2 Z")
            BtnMaximize.ToolTip = "Ingrandisci"
            RootBorder.CornerRadius = New CornerRadius(12)
            RootBorder.BorderThickness = New Thickness(1)
            Me.Effect = _defaultShadowEffect
            If ResizeGrip IsNot Nothing Then
                ResizeGrip.Visibility = Visibility.Visible
            End If
        End If
    End Sub

    ''' <summary>
    ''' Gestisce il ridimensionamento della finestra tramite il grip in basso a destra.
    ''' </summary>
    Private Sub ResizeGrip_PreviewMouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        If e.ChangedButton = MouseButton.Left Then
            Dim handle = New WindowInteropHelper(Me).Handle
            SendMessage(handle, WM_SYSCOMMAND, New IntPtr(SC_SIZE + 8), IntPtr.Zero)
            e.Handled = True
        End If
    End Sub

    Private Sub BtnClose_Click(sender As Object, e As RoutedEventArgs)
        Me.Hide()
    End Sub

    ''' <summary>
    ''' Popola la griglia WPF istanziando e pre-caricando i controlli WebView2 per tutti gli account configurati.
    ''' Inizializza e mostra immediatamente l'account attualmente selezionato all'avvio, 
    ''' e pre-carica in background tutti gli altri account mantenendoli attivi e connessi.
    ''' </summary>
    Private Async Sub PopulateWebViews()
        WebViewsGrid.Children.Clear()

        ' 1. Individua l'account attivo/selezionato all'avvio
        Dim activeAccount = _accountManager.CurrentAccount
        If activeAccount Is Nothing AndAlso _accountManager.Accounts.Count > 0 Then
            activeAccount = _accountManager.Accounts.FirstOrDefault(Function(a) a.IsActive)
            If activeAccount Is Nothing Then
                activeAccount = _accountManager.Accounts.First()
                activeAccount.IsActive = True
            End If
            _accountManager.CurrentAccount = activeAccount
        End If

        ' 2. Carica e mostra con massima priorità l'account selezionato
        If activeAccount IsNot Nothing Then
            Await EnsureWebViewAsync(activeAccount)
            If activeAccount.WebView IsNot Nothing Then
                activeAccount.WebView.Margin = New Thickness(0)
                Panel.SetZIndex(activeAccount.WebView, 10)
                activeAccount.WebView.Focus()
            End If
        End If

        ' 3. Pre-carica in background tutti gli altri account configurati mantenendoli attivi fuori dallo schermo
        Dim otherAccounts = _accountManager.Accounts.Where(Function(a) activeAccount Is Nothing OrElse a.Id <> activeAccount.Id).ToList()
        For Each acc In otherAccounts
            Await EnsureWebViewAsync(acc)
            If acc.WebView IsNot Nothing Then
                acc.WebView.Margin = New Thickness(-20000, 0, 20000, 0)
                Panel.SetZIndex(acc.WebView, 0)
            End If
        Next
    End Sub

    ''' <summary>
    ''' Assicura che l'istanza WebView2 per l'account sia creata, aggiunta alla griglia ed inizializzata.
    ''' </summary>
    Private Async Function EnsureWebViewAsync(account As AppAccounts) As Task
        If account.WebView Is Nothing Then
            account.WebView = New WebView2()
            account.WebView.HorizontalAlignment = HorizontalAlignment.Stretch
            account.WebView.VerticalAlignment = VerticalAlignment.Stretch
            
            Dim isDark = _settingsController.IsDarkThemeEffective
            account.WebView.DefaultBackgroundColor = If(isDark, System.Drawing.Color.FromArgb(17, 27, 33), System.Drawing.Color.FromArgb(240, 242, 245))
            
            account.WebView.Visibility = Visibility.Visible
            If account.IsActive Then
                account.WebView.Margin = New Thickness(0)
                Panel.SetZIndex(account.WebView, 10)
            Else
                account.WebView.Margin = New Thickness(-20000, 0, 20000, 0)
                Panel.SetZIndex(account.WebView, 0)
            End If
            WebViewsGrid.Children.Add(account.WebView)
        End If

        If account.WebView.CoreWebView2 Is Nothing Then
            Await account.SetupWebViewAsync(_settingsController, AddressOf OnAccountNotificationChanged)
        End If
    End Function

    Private Sub OnAccountNotificationChanged(accountId As String, hasNotification As Boolean)
        _accountManager.HandleNotificationStateChanged(accountId, hasNotification)
    End Sub

    ''' <summary>
    ''' Evento generato dal click su una scheda account per passare alla vista dell'account corrispondente.
    ''' </summary>
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

    ''' <summary>
    ''' Consente la rinomina rapida dell'account dal menu contestuale della scheda.
    ''' </summary>
    Private Async Sub AccountTabRename_Click(sender As Object, e As RoutedEventArgs)
        Dim menuItem = CType(sender, MenuItem)
        Dim contextMenu = CType(menuItem.Parent, ContextMenu)
        Dim btn = CType(contextMenu.PlacementTarget, Button)
        Dim acc = CType(btn.DataContext, AppAccounts)

        Dim newName = Microsoft.VisualBasic.Interaction.InputBox("Enter new name:", "Rename Account", acc.Name)
        If Not String.IsNullOrWhiteSpace(newName) Then
            Await _accountManager.UpdateAccountNameAsync(acc.Id, newName.Trim())
        End If
    End Sub

    ''' <summary>
    ''' Passa alla scheda account selezionata portando in primo piano la WebView2 corrispondente e spostando la precedente fuori schermo senza scaricarla.
    ''' </summary>
    Public Async Function SwitchToAccountAsync(accountId As String) As Task
        Dim prevAccount = _accountManager.CurrentAccount
        If prevAccount IsNot Nothing AndAlso prevAccount.Id = accountId Then
            If prevAccount.WebView IsNot Nothing Then
                prevAccount.WebView.Margin = New Thickness(0)
                Panel.SetZIndex(prevAccount.WebView, 10)
                prevAccount.WebView.Focus()
            End If
            Return
        End If

        Await _accountManager.SwitchAccountAsync(accountId)

        Dim newAccount = _accountManager.CurrentAccount
        If newAccount Is Nothing Then Return

        ' Sposta il controllo del precedente account fuori schermo lasciandolo attivo
        If prevAccount IsNot Nothing AndAlso prevAccount.WebView IsNot Nothing Then
            prevAccount.WebView.Margin = New Thickness(-20000, 0, 20000, 0)
            Panel.SetZIndex(prevAccount.WebView, 0)
        End If

        ' Inizializza se necessario e porta in primo piano il controllo WebView2 per il nuovo account
        Await EnsureWebViewAsync(newAccount)
        If newAccount.WebView IsNot Nothing Then
            newAccount.WebView.Margin = New Thickness(0)
            Panel.SetZIndex(newAccount.WebView, 10)
            newAccount.WebView.UpdateLayout()
            newAccount.WebView.Focus()
        End If
    End Function

    ''' <summary>
    ''' Apre la finestra di dialogo modale per le Impostazioni dell'applicazione.
    ''' </summary>
    Private Async Sub BtnSettings_Click(sender As Object, e As RoutedEventArgs)
        Try
            _accountManager.IsDialogOpen = True
            Dim settingsWin As New SettingsWindow(_settingsController, _accountManager)
            settingsWin.Owner = Me
            settingsWin.ShowDialog()
            _accountManager.IsDialogOpen = False

            Await ApplyWpfThemeAsync()
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

    ''' <summary>
    ''' Apre la finestra di dialogo Informazioni sull'applicazione (AboutWindow).
    ''' </summary>
    Private Sub BtnAbout_Click(sender As Object, e As RoutedEventArgs)
        OpenAboutWindow()
    End Sub

    Private Sub OpenAboutWindow()
        Try
            _accountManager.IsDialogOpen = True
            Dim aboutWin As New AboutWindow(_settingsController)
            aboutWin.Owner = Me
            aboutWin.ShowDialog()
            _accountManager.IsDialogOpen = False
        Catch ex As Exception
            _accountManager.IsDialogOpen = False
            Debug.WriteLine($"Error opening About window: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Apre la finestra di invio massivo personalizzato da file Excel/CSV per l'account attivo (WhatsApp o Telegram).
    ''' </summary>
    Private Sub BtnBulkSender_Click(sender As Object, e As RoutedEventArgs)
        Dim activeAcc = _accountManager.CurrentAccount
        If activeAcc Is Nothing OrElse activeAcc.WebView Is Nothing OrElse activeAcc.WebView.CoreWebView2 Is Nothing Then
            Dim platformName = If(activeAcc IsNot Nothing AndAlso activeAcc.IsTelegram, "Telegram", "WhatsApp")
            MessageBox.Show(
                $"Nessuna sessione di {platformName} Web attiva o pronta." & vbCrLf &
                $"Assicurati che la scheda {platformName} sia caricata e connessa.",
                $"{platformName} Non Pronto",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            )
            Return
        End If

        Try
            _accountManager.IsDialogOpen = True
            Dim bulkWin As New BulkSenderWindow(activeAcc, _settingsController)
            bulkWin.Owner = Me
            bulkWin.ShowDialog()
            _accountManager.IsDialogOpen = False
        Catch ex As Exception
            _accountManager.IsDialogOpen = False
            Debug.WriteLine($"Error opening BulkSender window: {ex.Message}")
            MessageBox.Show(
                "Errore nell'apertura della finestra di invio massivo: " & ex.Message,
                "Errore",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            )
        End Try
    End Sub

    ''' <summary>
    ''' Ricarica la pagina corrente all'interno della WebView2 dell'account attivo.
    ''' </summary>
    Private Sub BtnReloadActiveTab_Click(sender As Object, e As RoutedEventArgs)
        Dim activeAcc = _accountManager.CurrentAccount
        If activeAcc IsNot Nothing AndAlso activeAcc.WebView IsNot Nothing AndAlso activeAcc.WebView.CoreWebView2 IsNot Nothing Then
            activeAcc.WebView.CoreWebView2.Reload()
        End If
    End Sub

    ''' <summary>
    ''' Aggiorna lo stato del pulsante di aggiunta account nella barra delle schede.
    ''' </summary>
    Private Sub UpdateAddAccountButtonState()
        If BtnAddAccount IsNot Nothing Then
            Dim canAdd = _accountManager.CanAddAccount
            BtnAddAccount.IsEnabled = canAdd
            BtnAddAccount.Visibility = If(canAdd, Visibility.Visible, Visibility.Collapsed)
        End If
    End Sub

    ''' <summary>
    ''' Gestisce l'evento di click sul pulsante "+" per aggiungere un nuovo account (WhatsApp o Telegram).
    ''' </summary>
    Private Sub BtnAddAccount_Click(sender As Object, e As RoutedEventArgs)
        Try
            If Not _accountManager.CanAddAccount Then
                Dim msg = _settingsController.Localizations.Get("max_accounts_reached")
                MessageBox.Show(msg, "Limiti Account", MessageBoxButton.OK, MessageBoxImage.Information)
                Return
            End If

            Dim loc = _settingsController.Localizations
            Dim menu As New ContextMenu()
            
            Dim itemWhatsApp As New MenuItem With {
                .Header = loc.Get("add_whatsapp_account")
            }
            AddHandler itemWhatsApp.Click, Async Sub()
                Await AddAccountWithPlatformAsync("WhatsApp")
            End Sub

            Dim itemTelegram As New MenuItem With {
                .Header = loc.Get("add_telegram_account")
            }
            AddHandler itemTelegram.Click, Async Sub()
                Await AddAccountWithPlatformAsync("Telegram")
            End Sub

            menu.Items.Add(itemWhatsApp)
            menu.Items.Add(itemTelegram)
            menu.PlacementTarget = BtnAddAccount
            menu.IsOpen = True

        Catch ex As Exception
            Debug.WriteLine($"BtnAddAccount_Click error: {ex.Message}")
            MessageBox.Show("Errore nell'aggiunta dell'account: " & ex.Message, "Errore", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Async Function AddAccountWithPlatformAsync(platform As String) As Task
        Try
            Dim success = Await _accountManager.AddAccountAsync(platform:=platform)
            If success Then
                Dim newAcc = _accountManager.Accounts.LastOrDefault()
                If newAcc IsNot Nothing Then
                    Await SwitchToAccountAsync(newAcc.Id)
                End If
            End If
            UpdateAddAccountButtonState()
        Catch ex As Exception
            Debug.WriteLine($"AddAccountWithPlatformAsync error: {ex.Message}")
            MessageBox.Show("Errore nell'aggiunta dell'account: " & ex.Message, "Errore", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Function

    Private Async Sub OnSettingsPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = NameOf(SettingsController.Theme) Then
            Await ApplyWpfThemeAsync()
        End If
    End Sub

    Private Sub OnAccountManagerPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = NameOf(AccountManager.HasAnyNotification) Then
            UpdateTrayIconImage()
        ElseIf e.PropertyName = NameOf(AccountManager.CanAddAccount) OrElse e.PropertyName = NameOf(AccountManager.Accounts) Then
            UpdateAddAccountButtonState()
        End If
    End Sub

    ''' <summary>
    ''' Verifica la presenza del runtime Microsoft Edge WebView2 nel sistema.
    ''' </summary>
    Private Shared Function CheckWebView2Installed() As Boolean
        Try
            Dim ver = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString()
            Return Not String.IsNullOrEmpty(ver)
        Catch
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Applica lo stile cromatico (Scuro/Chiaro/Sistema) all'interfaccia WPF e sincronizza il tema nelle WebView2.
    ''' </summary>
    Private Async Function ApplyWpfThemeAsync() As Task

        Dim isDark = _settingsController.IsDarkThemeEffective


        If isDark Then
            RootBorder.Background = BrushCache.GetBrush("#111b21")
            RootBorder.BorderBrush = BrushCache.GetBrush("#2f3e46")
            TitleBar.Background = BrushCache.GetBrush("#202c33")
            TitleText.Foreground = BrushCache.GetBrush("#e9edef")
        Else
            RootBorder.Background = BrushCache.GetBrush("#f0f2f5")
            RootBorder.BorderBrush = BrushCache.GetBrush("#d1d7db")
            TitleBar.Background = BrushCache.GetBrush("#e9edef")
            TitleText.Foreground = BrushCache.GetBrush("#111b21")
        End If

        Dim bgColor = If(isDark, System.Drawing.Color.FromArgb(17, 27, 33), System.Drawing.Color.FromArgb(240, 242, 245))

        ' Aggiorna il tema all'interno delle singole WebView2 (WhatsApp / Telegram)
        For Each acc In _accountManager.Accounts
            If acc.WebView IsNot Nothing Then
                acc.WebView.DefaultBackgroundColor = bgColor
            End If
            Await acc.ApplyThemeAsync(isDark)
        Next
    End Function

    ''' <summary>
    ''' Estrae il valore di una chiave specificata dalla stringa degli argomenti (formato "key1=val1&key2=val2").
    ''' Restituisce Nothing se la chiave non è presente o non ha un valore valido.
    ''' </summary>
    Private Shared Function ExtractArg(argument As String, key As String) As String
        If String.IsNullOrEmpty(argument) Then Return Nothing
        Dim part = argument.Split("&"c).FirstOrDefault(Function(s) s.StartsWith(key & "="))
        If part Is Nothing Then Return Nothing
        Dim idx = part.IndexOf("="c)
        Return If(idx >= 0 AndAlso idx < part.Length - 1, part.Substring(idx + 1), Nothing)
    End Function

    ''' <summary>
    ''' Configura la gestione dell'evento di click sulle notifiche Toast di Windows per ripristinare la finestra ed aprire l'account di origine.
    ''' </summary>
    Private Sub ConfigureToastNotifications()
        AddHandler ToastNotificationManagerCompat.OnActivated, Sub(toastArgs)
            Dim args = toastArgs.Argument
            If Not String.IsNullOrEmpty(args) Then
                Dim accountId = ExtractArg(args, "accountId")
                If String.IsNullOrEmpty(accountId) Then Return
                Dim notificationId = ExtractArg(args, "notificationId")
                
                ' Esegue sul thread UI il ripristino della finestra e la selezione dell'account
                Application.Current.Dispatcher.Invoke(Async Function() As Task
                    ShowWindow()
                    Await SwitchToAccountAsync(accountId)
                    
                    If Not String.IsNullOrEmpty(notificationId) Then
                        Dim acc = _accountManager.Accounts.FirstOrDefault(Function(a) a.Id = accountId)
                        If acc IsNot Nothing AndAlso acc.WebView IsNot Nothing AndAlso acc.WebView.CoreWebView2 IsNot Nothing Then
                            Try
                                Dim jsonNotifId = System.Text.Json.JsonSerializer.Serialize(notificationId)
                                Await acc.WebView.CoreWebView2.ExecuteScriptAsync($"if (window.onNotificationClicked) {{ window.onNotificationClicked({jsonNotifId}); }}")
                            Catch ex As Exception
                                Debug.WriteLine($"Failed to execute onNotificationClicked for account {acc.Id}: {ex.Message}")
                            End Try
                        End If
                    End If
                End Function)
            End If
        End Sub
    End Sub

End Class


