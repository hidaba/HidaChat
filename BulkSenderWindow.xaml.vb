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
''' Code-behind per la finestra di invio massivo personalizzato da file Excel / CSV.
''' Supporta ridimensionamento, schermo intero/massimizzazione e ispezione dettagliata dei messaggi.
''' </summary>
Public Class BulkSenderWindow
    Private ReadOnly _webView As WebView2
    Private ReadOnly _settingsController As SettingsController
    Private ReadOnly _engine As New BulkSenderEngine()
    Private ReadOnly _contacts As New ObservableCollection(Of BulkContactItem)()
    Private _defaultShadowEffect As Effect

    Public Sub New(webView As WebView2, settingsController As SettingsController)
        InitializeComponent()
        _defaultShadowEffect = Me.Effect
        _webView = webView
        _settingsController = settingsController
        GridContacts.ItemsSource = _contacts

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
    ''' Limita l'ingrandimento a schermo intero alla Working Area per evitare di sovrapporsi alla barra delle applicazioni di Windows.
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

    ''' <summary>
    ''' Gestisce il trascinamento e il doppio click per massimizzare/ripristinare la finestra dalla barra del titolo.
    ''' </summary>
    Private Sub Header_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        If e.ChangedButton = MouseButton.Left AndAlso e.ClickCount = 2 Then
            ToggleMaximize()
        ElseIf e.ChangedButton = MouseButton.Left Then
            If Me.WindowState = WindowState.Normal Then
                Me.DragMove()
            End If
        End If
    End Sub

    ''' <summary>
    ''' Ripristina e trascina fluidamente la finestra partendo dallo stato ingrandito.
    ''' </summary>
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

    ''' <summary>
    ''' Alterna lo stato tra finestra normale e schermo intero (ingrandita).
    ''' </summary>
    Private Sub ToggleMaximize()
        If Me.WindowState = WindowState.Maximized Then
            Me.WindowState = WindowState.Normal
        Else
            Me.WindowState = WindowState.Maximized
        End If
    End Sub

    ''' <summary>
    ''' Aggiorna l'icona del pulsante massimizza/ripristina e i bordi della finestra al cambio di stato.
    ''' </summary>
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

    Private Sub BtnClose_Click(sender As Object, e As RoutedEventArgs)
        If _engine.IsRunning Then
            Dim res = MessageBox.Show(
                "Un invio di messaggi è attualmente in corso. Vuoi interromperlo e chiudere la finestra?",
                "Conferma Chiusura",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            )
            If res = MessageBoxResult.Yes Then
                _engine.Cancel()
                Me.Close()
            End If
        Else
            Me.Close()
        End If
    End Sub

    ''' <summary>
    ''' Seleziona e carica un file Excel (.xlsx, .xls) o CSV.
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
                        "Verifica che il file contenga una colonna con i numeri di telefono.",
                        "File Vuoto o Non Valido",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    )
                    Return
                End If

                TxtFileName.Text = Path.GetFileName(ofd.FileName)
                TxtFileName.ToolTip = ofd.FileName

                ' Se il file contiene già testi personalizzati nella colonna Testo, imposta il template a {Testo} per garantire perfetta coerenza 1:1
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

    ''' <summary>
    ''' Inserisce un tag dinamico ({Nome}, {Cognome}, ecc.) all'interno del TextBox del template.
    ''' </summary>
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

    ''' <summary>
    ''' Aggiorna l'anteprima dei messaggi in tabella quando l'utente modifica il template.
    ''' </summary>
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

    ''' <summary>
    ''' Gestisce la selezione di una riga nel DataGrid per mostrare il testo completo nell'ispettore di anteprima.
    ''' </summary>
    Private Sub GridContacts_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Dim selectedItem = TryCast(GridContacts.SelectedItem, BulkContactItem)
        If selectedItem IsNot Nothing Then
            TxtSelectedContactLabel.Text = $"{selectedItem.FullName} ({selectedItem.Phone}) - {selectedItem.Company}"
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
    ''' Avvia il ciclo di invio massivo per tutti i contatti selezionati.
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

        If _webView Is Nothing OrElse _webView.CoreWebView2 Is Nothing Then
            MessageBox.Show("La sessione di WhatsApp Web non è attiva o inizializzata. Assicurati che WhatsApp sia connesso nella finestra principale.", "WhatsApp Non Pronto", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim minDelay As Integer = 10
        Dim maxDelay As Integer = 20
        If Not Integer.TryParse(TxtMinDelay.Text, minDelay) OrElse minDelay < 3 Then minDelay = 10
        If Not Integer.TryParse(TxtMaxDelay.Text, maxDelay) OrElse maxDelay < minDelay Then maxDelay = minDelay + 10

        For Each c In selected
            c.Status = "In attesa"
            c.ErrorMessage = String.Empty
        Next

        SetUiRunningState(True)

        Dim template = TxtTemplate.Text

        Await _engine.RunAsync(
            _contacts,
            template,
            minDelay,
            maxDelay,
            _webView,
            Sub(prog)
                Dispatcher.Invoke(Sub()
                    If prog.IsCompleted Then
                        SetUiRunningState(False)
                        TxtStatus.Text = $"Completato! Inviati: {prog.SentCount}, Errori: {prog.ErrorCount}"
                        ProgressBarSending.Value = 100
                        MessageBox.Show(
                            $"Invio completato!" & vbCrLf & vbCrLf &
                            $"• Inviati con successo: {prog.SentCount}" & vbCrLf &
                            $"• Errori / Non validi: {prog.ErrorCount}",
                            "Riepilogo Invio Massivo",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
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
    End Sub

    Private Sub BtnPause_Click(sender As Object, e As RoutedEventArgs)
        If _engine.IsRunning Then
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

    Private Sub BtnStop_Click(sender As Object, e As RoutedEventArgs)
        If _engine.IsRunning Then
            _engine.Cancel()
            TxtStatus.Text = "Invio interrotto dall'utente."
            SetUiRunningState(False)
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
End Class
