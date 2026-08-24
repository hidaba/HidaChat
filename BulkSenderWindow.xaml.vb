Imports System.Collections.ObjectModel
Imports System.IO
Imports System.Linq
Imports System.Windows.Input
Imports Microsoft.Win32
Imports Microsoft.Web.WebView2.Wpf

''' <summary>
''' Code-behind per la finestra di invio massivo personalizzato da file Excel / CSV.
''' </summary>
Public Class BulkSenderWindow
    Private ReadOnly _webView As WebView2
    Private ReadOnly _settingsController As SettingsController
    Private ReadOnly _engine As New BulkSenderEngine()
    Private ReadOnly _contacts As New ObservableCollection(Of BulkContactItem)()

    Public Sub New(webView As WebView2, settingsController As SettingsController)
        InitializeComponent()
        _webView = webView
        _settingsController = settingsController
        GridContacts.ItemsSource = _contacts

        ApplyTheme()
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
        If e.ButtonState = MouseButtonState.Pressed Then
            Me.DragMove()
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

                _contacts.Clear()
                Dim tmpl = TxtTemplate.Text
                For Each item In loaded
                    item.PreviewMessage = item.GenerateMessage(tmpl)
                    _contacts.Add(item)
                Next

                UpdateCounts()
                TxtStatus.Text = $"Caricati {loaded.Count} contatti con successo. Pronto per l'invio."
                ProgressBarSending.Value = 0
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
