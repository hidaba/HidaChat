Imports System.Collections.ObjectModel
Imports System.IO
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Windows.Input
Imports System.Windows.Interop
Imports System.Windows.Media.Effects
Imports Microsoft.Win32
Imports Microsoft.Web.WebView2.Wpf

''' <summary>
''' Code-behind for custom bulk sender window from Excel/CSV files.
''' Supports WhatsApp Web and Telegram Web, dynamic resizing, fullscreen, and detailed preview inspection.
''' </summary>
Public Class BulkSenderWindow
    Private ReadOnly _account As AppAccounts
    Private ReadOnly _webView As WebView2
    Private ReadOnly _settingsController As SettingsController
    Private ReadOnly _engine As New BulkSenderEngine()
    Private ReadOnly _contacts As New ObservableCollection(Of BulkContactItem)()
    Private ReadOnly _platform As String
    Private _defaultShadowEffect As Effect
    Private _isCancellingAndClosing As Boolean = False

    Public Sub New(account As AppAccounts, settingsController As SettingsController)
        InitializeComponent()
        _defaultShadowEffect = Me.Effect
        _account = account
        _webView = account?.WebView
        _settingsController = settingsController
        _platform = If(account IsNot Nothing, account.Platform, "WhatsApp")
        GridContacts.ItemsSource = _contacts

        ApplyPlatformVisuals()
        ApplyTheme()
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
    Private Const MONITOR_DEFAULTTONEAREST As Integer = 2

    <DllImport("user32.dll")>
    Private Shared Function MonitorFromWindow(handle As IntPtr, flags As Integer) As IntPtr
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function GetMonitorInfo(hMonitor As IntPtr, ByRef lpmi As MONITORINFO) As Boolean
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
    ''' Restricts fullscreen maximization to the Windows Working Area so it does not overlap the taskbar.
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

    Private Sub ApplyPlatformVisuals()
        Dim def = PlatformRegistry.Get(_platform)
        Dim accSuffix = If(_account IsNot Nothing, $" - {_account.Name}", "")
        HeaderTitleText.Text = $"Invio Massivo Personalizzato ({def.DisplayName}{accSuffix})"
        HeaderPlatformIcon.Data = Geometry.Parse(def.IconSvgPath)
        HeaderPlatformIcon.Fill = def.ThemeBrush
    End Sub

    Private Sub ApplyTheme()
        Dim isDark = If(_settingsController IsNot Nothing, _settingsController.IsDarkThemeEffective, True)
        If isDark Then
            DialogBorder.Background = BrushCache.GetBrush("#111b21")
            DialogBorder.BorderBrush = BrushCache.GetBrush("#2f3e46")
        Else
            DialogBorder.Background = BrushCache.GetBrush("#ffffff")
            DialogBorder.BorderBrush = BrushCache.GetBrush("#dae0e4")
        End If
    End Sub

    Private Sub Header_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        If e.ChangedButton = MouseButton.Left AndAlso e.ClickCount = 2 Then
            ToggleMaximize()
        ElseIf e.ChangedButton = MouseButton.Left Then
            If Me.WindowState = WindowState.Normal Then
                Me.DragMove()
            End If
        End If
    End Sub

    Private Sub Header_MouseMove(sender As Object, e As MouseEventArgs)
        If e.LeftButton = MouseButtonState.Pressed AndAlso Me.WindowState = WindowState.Maximized Then
            Dim mousePos = PointToScreen(e.GetPosition(Me))
            Dim percentX = e.GetPosition(Me).X / Me.ActualWidth

            Me.WindowState = WindowState.Normal

            Me.Left = mousePos.X - (Me.ActualWidth * percentX)
            Me.Top = mousePos.Y - (HeaderBar.ActualHeight / 2)

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

    Private Sub ToggleMaximize()
        If Me.WindowState = WindowState.Maximized Then
            Me.WindowState = WindowState.Normal
        Else
            Me.WindowState = WindowState.Maximized
        End If
    End Sub

    Private Sub BulkSenderWindow_StateChanged(sender As Object, e As EventArgs) Handles Me.StateChanged
        If BtnMaximize Is Nothing Then Return

        If Me.WindowState = WindowState.Maximized Then
            MaximizeIcon.Data = Geometry.Parse("M4,1 L11,1 L11,8 L9,8 L9,2 L4,2 Z M1,4 L8,4 L8,11 L1,11 Z")
            BtnMaximize.ToolTip = "Ripristina"
            DialogBorder.CornerRadius = New CornerRadius(0)
            DialogBorder.BorderThickness = New Thickness(0)
            Me.Effect = Nothing
        Else
            MaximizeIcon.Data = Geometry.Parse("M1,1 L11,1 L11,11 L1,11 Z M2,2 L2,10 L10,10 L10,2 Z")
            BtnMaximize.ToolTip = "Schermo intero"
            DialogBorder.CornerRadius = New CornerRadius(10)
            DialogBorder.BorderThickness = New Thickness(1)
            Me.Effect = _defaultShadowEffect
        End If
    End Sub

    ''' <summary>
    ''' Handles window closing: if bulk sending is active, confirms with user and gracefully cancels execution (TASK-0080).
    ''' </summary>
    Private Async Sub BulkSenderWindow_Closing(sender As Object, e As ComponentModel.CancelEventArgs) Handles Me.Closing
        If _isCancellingAndClosing Then Return

        If _engine.IsRunning Then
            e.Cancel = True
            Dim res = MessageBox.Show(
                "Un invio di messaggi è attualmente in corso. Vuoi interromperlo e chiudere la finestra?",
                "Conferma Chiusura",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            )
            If res = MessageBoxResult.Yes Then
                _isCancellingAndClosing = True
                SetUiStoppingState()
                Await _engine.CancelAsync()
                Me.Close()
            End If
        End If
    End Sub

    Private Sub BtnClose_Click(sender As Object, e As RoutedEventArgs)
        Me.Close()
    End Sub

    ''' <summary>
    ''' Selects and loads an Excel (.xlsx, .xls) or CSV file.
    ''' </summary>
    Private Sub BtnLoadFile_Click(sender As Object, e As RoutedEventArgs)
        Try
            Dim ofd As New OpenFileDialog() With {
                .Title = "Seleziona file Excel o CSV con i contatti",
                .Filter = "File Excel e CSV (*.xlsx;*.xls;*.csv)|*.xlsx;*.xls;*.csv|File Excel (*.xlsx;*.xls)|*.xlsx;*.xls|File CSV (*.csv)|*.csv|Tutti i file (*.*)|*.*",
                .CheckFileExists = True
            }

            If ofd.ShowDialog() = True Then
                Dim loaded = ExcelContactService.LoadContactsFromFile(ofd.FileName)
                If loaded.Count = 0 Then
                    MessageBox.Show(
                        "Nessun contatto valido trovato nel file selezionato." & vbCrLf &
                        "Verifica che il file contenga colonne con numeri di telefono o username.",
                        "File Vuoto o Non Valido",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    )
                    Return
                End If

                TxtFileName.Text = Path.GetFileName(ofd.FileName)
                TxtFileName.ToolTip = ofd.FileName

                Dim hasCustomText = loaded.Any(Function(c) Not String.IsNullOrWhiteSpace(c.CustomText))
                If hasCustomText Then
                    TxtTemplate.Text = "{Testo}"
                ElseIf String.IsNullOrWhiteSpace(TxtTemplate.Text) OrElse TxtTemplate.Text = "{Testo}" Then
                    TxtTemplate.Text = "Gentile {Nome} {Cognome}, le inviamo questa comunicazione da {Azienda}."
                End If

                _contacts.Clear()
                Dim tmpl = TxtTemplate.Text
                For Each item In loaded
                    item.PreviewMessage = item.GenerateMessage(tmpl)
                    _contacts.Add(item)
                Next

                UpdateCounts()
                TxtStatus.Text = $"Caricati {loaded.Count} contatti con successo. Pronto per l'invio."
                ProgressBarSending.Value = 0

                If _contacts.Count > 0 Then
                    GridContacts.SelectedIndex = 0
                End If
            End If
        Catch ex As Exception
            MessageBox.Show(
                $"Errore durante la lettura del file: {ex.Message}",
                "Errore Lettura File",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            )
        End Try
    End Sub

    Private Sub BtnInsertTag_Click(sender As Object, e As RoutedEventArgs)
        Dim btn = TryCast(sender, Button)
        If btn IsNot Nothing AndAlso btn.Tag IsNot Nothing Then
            Dim tag = btn.Tag.ToString()
            Dim selStart = TxtTemplate.SelectionStart
            TxtTemplate.Text = TxtTemplate.Text.Insert(selStart, tag)
            TxtTemplate.SelectionStart = selStart + tag.Length
            TxtTemplate.Focus()
        End If
    End Sub

    Private Sub TxtTemplate_TextChanged(sender As Object, e As TextChangedEventArgs)
        If _contacts Is Nothing OrElse _contacts.Count = 0 Then Return
        Dim tmpl = TxtTemplate.Text
        For Each c In _contacts
            c.PreviewMessage = c.GenerateMessage(tmpl)
        Next

        Dim selected = TryCast(GridContacts.SelectedItem, BulkContactItem)
        If selected IsNot Nothing Then
            TxtSelectedMessagePreview.Text = selected.PreviewMessage
        End If
    End Sub

    Private Sub GridContacts_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Dim selectedItem = TryCast(GridContacts.SelectedItem, BulkContactItem)
        If selectedItem IsNot Nothing Then
            Dim recipientInfo = If(Not String.IsNullOrEmpty(selectedItem.FormattedUsername), $"{selectedItem.FullName} ({selectedItem.FormattedUsername})", $"{selectedItem.FullName} ({selectedItem.Phone})")
            TxtSelectedContactLabel.Text = $"{recipientInfo} - {selectedItem.Company}"
            TxtSelectedMessagePreview.Text = selectedItem.PreviewMessage
        Else
            TxtSelectedContactLabel.Text = "Nessun contatto selezionato nella tabella"
            TxtSelectedMessagePreview.Text = String.Empty
        End If
    End Sub

    Private Sub ChkSelectAllHeader_Click(sender As Object, e As RoutedEventArgs)
        Dim chk = TryCast(sender, CheckBox)
        If chk IsNot Nothing Then
            Dim state = chk.IsChecked.GetValueOrDefault(False)
            For Each c In _contacts
                c.Selected = state
            Next
            UpdateCounts()
        End If
    End Sub

    Private Sub ContactCheckbox_Click(sender As Object, e As RoutedEventArgs)
        UpdateCounts()
    End Sub

    Private Sub BtnSelectAll_Click(sender As Object, e As RoutedEventArgs)
        For Each c In _contacts
            c.Selected = True
        Next
        UpdateCounts()
    End Sub

    Private Sub BtnDeselectAll_Click(sender As Object, e As RoutedEventArgs)
        For Each c In _contacts
            c.Selected = False
        Next
        UpdateCounts()
    End Sub

    Private Sub UpdateCounts()
        TxtTotalCount.Text = _contacts.Count.ToString()
        Dim selCount = _contacts.Where(Function(c) c.Selected).Count()
        TxtSelectedCount.Text = selCount.ToString()
    End Sub

    ''' <summary>
    ''' Starts the bulk sending process for all selected contacts on the active platform.
    ''' </summary>
    Private Async Sub BtnStart_Click(sender As Object, e As RoutedEventArgs)
        If _contacts.Count = 0 Then
            MessageBox.Show("Carica prima un file Excel o CSV con i contatti.", "Nessun Contatto", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim selected = _contacts.Where(Function(c) c.Selected).ToList()
        If selected.Count = 0 Then
            MessageBox.Show("Seleziona almeno un contatto dalla lista per procedere con l'invio.", "Nessun Contatto Selezionato", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim def = PlatformRegistry.Get(_platform)
        If _webView Is Nothing OrElse _webView.CoreWebView2 Is Nothing Then
            Dim appName = def.DisplayName
            MessageBox.Show($"La sessione di {appName} Web non è attiva o inizializzata. Assicurati che {appName} sia connesso nella finestra principale.", $"{appName} Non Pronto", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        If Not def.SupportsBulkSender Then
            MessageBox.Show($"L'invio massivo non è supportato per la piattaforma {def.DisplayName}.", "Piattaforma Non Supportata", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim isTelegram = (def.Platform = ChatPlatform.Telegram)
        If isTelegram Then
            Dim validTg = selected.Where(Function(c) Not String.IsNullOrEmpty(c.CleanUsername) OrElse Not String.IsNullOrEmpty(c.CleanPhone)).ToList()
            If validTg.Count = 0 Then
                MessageBox.Show("Nessuno dei contatti selezionati possiede uno Username Telegram (@utente) o un Numero di Telefono valido.", "Destinatari Non Validi", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return
            End If
        Else
            Dim validWa = selected.Where(Function(c) Not String.IsNullOrEmpty(c.CleanPhone)).ToList()
            If validWa.Count = 0 Then
                MessageBox.Show("Nessuno dei contatti selezionati possiede un Numero di Telefono valido per WhatsApp.", "Numeri Telefonici Mancanti", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return
            End If
        End If

        ValidateDelayInputs()
        Dim minDelay = Integer.Parse(TxtMinDelay.Text)
        Dim maxDelay = Integer.Parse(TxtMaxDelay.Text)

        For Each c In selected
            c.Status = "In attesa"
            c.ErrorMessage = String.Empty
        Next

        SetUiRunningState(True)

        Dim template = TxtTemplate.Text

        Try
            Await _engine.RunAsync(
                _contacts,
                template,
                minDelay,
                maxDelay,
                _platform,
                _webView,
                Sub(prog)
                    Dispatcher.Invoke(Sub()
                        If prog.IsCompleted Then
                            SetUiRunningState(False)
                            TxtStatus.Text = $"{AppLocalizations.GetText("bulk_status_completed", "Completato")}! Inviati: {prog.SentCount}, Errori: {prog.ErrorCount}"
                            ProgressBarSending.Value = 100
                            MessageBox.Show(
                                $"Invio completato su {_platform}!" & vbCrLf & vbCrLf &
                                $"• Inviati con successo: {prog.SentCount}" & vbCrLf &
                                $"• Errori / Non trovati: {prog.ErrorCount}",
                                "Riepilogo Invio Massivo",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information
                            )
                        ElseIf prog.IsCancelled Then
                            SetUiRunningState(False)
                            TxtStatus.Text = $"{AppLocalizations.GetText("bulk_status_cancelled", "Invio interrotto dall'utente.")} (Inviati: {prog.SentCount}, Errori: {prog.ErrorCount})"
                            If prog.TotalCount > 0 Then
                                ProgressBarSending.Value = (prog.CurrentIndex / CDbl(prog.TotalCount)) * 100
                            End If
                            MessageBox.Show(
                                $"Invio interrotto su {_platform}." & vbCrLf & vbCrLf &
                                $"• Inviati con successo: {prog.SentCount}" & vbCrLf &
                                $"• Errori / Non trovati: {prog.ErrorCount}" & vbCrLf &
                                $"• Non inviati: {Math.Max(0, prog.TotalCount - prog.SentCount - prog.ErrorCount)}",
                                AppLocalizations.GetText("bulk_title_cancelled", "Invio Interrotto"),
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning
                            )
                        ElseIf prog.IsFailed Then
                            SetUiRunningState(False)
                            TxtStatus.Text = prog.StatusMessage
                            MessageBox.Show(
                                prog.StatusMessage,
                                "Errore Invio Massivo",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error
                            )
                        Else
                            If prog.TotalCount > 0 Then
                                ProgressBarSending.Value = (prog.CurrentIndex / CDbl(prog.TotalCount)) * 100
                            End If

                            If prog.CountdownRemainingSeconds > 0 Then
                                TxtStatus.Text = $"Inviati {prog.SentCount}/{prog.TotalCount} — Prossimo invio tra {prog.CountdownRemainingSeconds}s..."
                            Else
                                TxtStatus.Text = $"[{prog.CurrentIndex}/{prog.TotalCount}] {prog.StatusMessage}"
                            End If
                        End If
                    End Sub)
                End Sub
            )
        Catch ex As InvalidOperationException
            MessageBox.Show(ex.Message, "Invio Massivo", MessageBoxButton.OK, MessageBoxImage.Warning)
        Catch ex As Exception
            MessageBox.Show($"Errore inatteso durante l'invio: {ex.Message}", "Invio Massivo", MessageBoxButton.OK, MessageBoxImage.Error)
        Finally
            If Not _engine.IsRunning AndAlso Not _engine.IsStopping Then
                SetUiRunningState(False)
            End If
        End Try
    End Sub

    Private Sub BtnPause_Click(sender As Object, e As RoutedEventArgs)
        If _engine.IsRunning AndAlso Not _engine.IsStopping Then
            If _engine.IsPaused Then
                _engine.ResumeSending()
                BtnPause.Content = "⏸ Pausa"
                TxtStatus.Text = "Invio ripreso..."
            Else
                _engine.Pause()
                BtnPause.Content = "▶ Riprendi"
                TxtStatus.Text = "Invio in pausa. Premi 'Riprendi' per continuare."
            End If
        End If
    End Sub

    Private Async Sub BtnStop_Click(sender As Object, e As RoutedEventArgs)
        If _engine.IsRunning AndAlso Not _engine.IsStopping Then
            SetUiStoppingState()
            Await _engine.CancelAsync()
        End If
    End Sub

    Private Sub TxtDelay_LostFocus(sender As Object, e As RoutedEventArgs)
        ValidateDelayInputs()
    End Sub

    ''' <summary>
    ''' Validates delay inputs, enforcing a minimum of 30 seconds for anti-spam safety.
    ''' </summary>
    Private Sub ValidateDelayInputs()
        Dim minVal As Integer
        If Not Integer.TryParse(TxtMinDelay.Text, minVal) OrElse minVal < 30 Then
            minVal = 30
            TxtMinDelay.Text = "30"
        End If

        Dim maxVal As Integer
        If Not Integer.TryParse(TxtMaxDelay.Text, maxVal) OrElse maxVal < 30 Then
            maxVal = Math.Max(minVal, 30)
            TxtMaxDelay.Text = maxVal.ToString()
        End If

        If maxVal < minVal Then
            maxVal = minVal + 10
            TxtMaxDelay.Text = maxVal.ToString()
        End If
    End Sub

    Private Sub SetUiRunningState(isRunning As Boolean)
        BtnStart.IsEnabled = Not isRunning
        BtnLoadFile.IsEnabled = Not isRunning
        TxtTemplate.IsEnabled = Not isRunning
        TxtMinDelay.IsEnabled = Not isRunning
        TxtMaxDelay.IsEnabled = Not isRunning

        BtnPause.IsEnabled = isRunning
        BtnPause.Content = "⏸ Pausa"
        BtnStop.IsEnabled = isRunning
    End Sub

    Private Sub SetUiStoppingState()
        BtnStart.IsEnabled = False
        BtnLoadFile.IsEnabled = False
        TxtTemplate.IsEnabled = False
        TxtMinDelay.IsEnabled = False
        TxtMaxDelay.IsEnabled = False

        BtnPause.IsEnabled = False
        BtnStop.IsEnabled = False
        TxtStatus.Text = AppLocalizations.GetText("bulk_status_stopping", "Arresto in corso...")
    End Sub
End Class
