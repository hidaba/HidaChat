Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.IO
Imports System.Linq
Imports System.Windows.Data
Imports Microsoft.Win32
Imports Microsoft.Web.WebView2.Wpf
Imports HidaChat.UI.Controls
Imports HidaChat.UI.Icons
Imports HidaChat.UI.Theme

''' <summary>
''' Fluent Design window for custom bulk messaging from Excel/CSV files.
''' Supports WhatsApp Web and Telegram Web, live search filtering, and detailed preview inspection.
''' </summary>
Public Class BulkSenderWindow
    Private ReadOnly _account As AppAccounts
    Private ReadOnly _webView As WebView2
    Private ReadOnly _settingsController As SettingsController
    Private ReadOnly _engine As New BulkSenderEngine()
    Private ReadOnly _contacts As New ObservableCollection(Of BulkContactItem)()
    Private ReadOnly _platform As String
    Private _contactsView As ICollectionView
    Private _searchFilter As String = String.Empty
    Private _isCancellingAndClosing As Boolean = False

    ''' <summary>
    ''' Parameterless constructor preserved for Visual Studio XAML Designer compatibility.
    ''' </summary>
    Public Sub New()
        InitializeComponent()
        _platform = "WhatsApp"
    End Sub

    Public Sub New(account As AppAccounts, settingsController As SettingsController)
        InitializeComponent()
        _account = account
        _webView = account?.WebView
        _settingsController = settingsController
        _platform = If(account IsNot Nothing, account.Platform, "WhatsApp")

        _contactsView = CollectionViewSource.GetDefaultView(_contacts)
        _contactsView.Filter = AddressOf ContactMatchesSearch
        GridContacts.ItemsSource = _contactsView

        If Not DesignerProperties.GetIsInDesignMode(Me) Then
            ThemeManager.Instance.RegisterWindow(Me)
            SyncThemeFromSettings()
        End If

        ApplyPlatformVisuals()
        UpdateEmptyState()
    End Sub

    Protected Overrides Sub OnClosed(e As EventArgs)
        ThemeManager.Instance.UnregisterWindow(Me)
        MyBase.OnClosed(e)
    End Sub

    Private Sub SyncThemeFromSettings()
        If _settingsController Is Nothing Then Return
        Dim mode = AppThemeMode.System
        Select Case _settingsController.Theme
            Case "Dark"
                mode = AppThemeMode.Dark
            Case "Light"
                mode = AppThemeMode.Light
        End Select
        ThemeManager.Instance.SetTheme(mode, persist:=False)
    End Sub

    Private Sub ApplyPlatformVisuals()
        Dim def = PlatformRegistry.Get(_platform)
        Dim accSuffix = If(_account IsNot Nothing, $" - {_account.Name}", "")
        Dim titleText = $"Invio Massivo Personalizzato ({def.DisplayName}{accSuffix})"
        Me.Title = titleText
        HeaderTitleText.Text = titleText
        HeaderPlatformIcon.Data = Geometry.Parse(def.IconSvgPath)
        HeaderPlatformIcon.Fill = def.ThemeBrush
    End Sub

    Private Function ContactMatchesSearch(obj As Object) As Boolean
        If String.IsNullOrWhiteSpace(_searchFilter) Then Return True
        Dim item = TryCast(obj, BulkContactItem)
        If item Is Nothing Then Return False

        Dim q = _searchFilter
        Return (item.Phone IsNot Nothing AndAlso item.Phone.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) OrElse
               (item.FormattedUsername IsNot Nothing AndAlso item.FormattedUsername.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) OrElse
               (item.FirstName IsNot Nothing AndAlso item.FirstName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) OrElse
               (item.LastName IsNot Nothing AndAlso item.LastName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) OrElse
               (item.Company IsNot Nothing AndAlso item.Company.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
    End Function

    Private Sub TxtContactSearch_SearchChanged(sender As Object, query As String)
        _searchFilter = If(query, String.Empty).Trim()
        _contactsView?.Refresh()
    End Sub

    Private Sub UpdateEmptyState()
        If EmptyContactsState IsNot Nothing Then
            EmptyContactsState.Visibility = If(_contacts.Count = 0, Visibility.Visible, Visibility.Collapsed)
        End If
    End Sub

    ''' <summary>
    ''' Handles window closing: if bulk sending is active, confirms with user and gracefully cancels execution (TASK-0080).
    ''' </summary>
    Private Async Sub BulkSenderWindow_Closing(sender As Object, e As ComponentModel.CancelEventArgs) Handles Me.Closing
        If _isCancellingAndClosing Then Return

        If _engine.IsRunning Then
            e.Cancel = True
            Dim confirmed = DialogManager.ShowConfirm(
                owner:=Me,
                title:="Conferma Chiusura",
                message:="Un invio di messaggi è attualmente in corso. Vuoi interromperlo e chiudere la finestra?",
                confirmText:="Interrompi e Chiudi",
                cancelText:="Annulla",
                isDestructive:=True,
                iconKey:=IconKey.Warning
            )
            If confirmed Then
                _isCancellingAndClosing = True
                SetUiStoppingState()
                Await _engine.CancelAsync()
                Me.Close()
            End If
        End If
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
                    DialogManager.ShowWarning(
                        Me,
                        "File Vuoto o Non Valido",
                        "Nessun contatto valido trovato nel file selezionato." & vbCrLf &
                        "Verifica che il file contenga colonne con numeri di telefono o username."
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
                UpdateEmptyState()
                TxtStatus.Text = $"Caricati {loaded.Count} contatti con successo. Pronto per l'invio."
                ProgressBarSending.Value = 0

                If _contacts.Count > 0 Then
                    GridContacts.SelectedIndex = 0
                End If
            End If
        Catch ex As Exception
            DialogManager.ShowError(
                Me,
                "Errore Lettura File",
                $"Errore durante la lettura del file: {ex.Message}"
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
            DialogManager.ShowInfo(Me, "Nessun Contatto", "Carica prima un file Excel o CSV con i contatti.")
            Return
        End If

        Dim selected = _contacts.Where(Function(c) c.Selected).ToList()
        If selected.Count = 0 Then
            DialogManager.ShowInfo(Me, "Nessun Contatto Selezionato", "Seleziona almeno un contatto dalla lista per procedere con l'invio.")
            Return
        End If

        Dim def = PlatformRegistry.Get(_platform)
        If _webView Is Nothing OrElse _webView.CoreWebView2 Is Nothing Then
            Dim appName = def.DisplayName
            DialogManager.ShowWarning(Me, $"{appName} Non Pronto", $"La sessione di {appName} Web non è attiva o inizializzata. Assicurati che {appName} sia connesso nella finestra principale.")
            Return
        End If

        If Not def.SupportsBulkSender Then
            DialogManager.ShowWarning(Me, "Piattaforma Non Supportata", $"L'invio massivo non è supportato per la piattaforma {def.DisplayName}.")
            Return
        End If

        Dim isTelegram = (def.Platform = ChatPlatform.Telegram)
        If isTelegram Then
            Dim validTg = selected.Where(Function(c) Not String.IsNullOrEmpty(c.CleanUsername) OrElse Not String.IsNullOrEmpty(c.CleanPhone)).ToList()
            If validTg.Count = 0 Then
                DialogManager.ShowWarning(Me, "Destinatari Non Validi", "Nessuno dei contatti selezionati possiede uno Username Telegram (@utente) o un Numero di Telefono valido.")
                Return
            End If
        Else
            Dim validWa = selected.Where(Function(c) Not String.IsNullOrEmpty(c.CleanPhone)).ToList()
            If validWa.Count = 0 Then
                DialogManager.ShowWarning(Me, "Numeri Telefonici Mancanti", "Nessuno dei contatti selezionati possiede un Numero di Telefono valido per WhatsApp.")
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
                            DialogManager.ShowInfo(
                                Me,
                                "Riepilogo Invio Massivo",
                                $"Invio completato su {_platform}!" & vbCrLf & vbCrLf &
                                $"• Inviati con successo: {prog.SentCount}" & vbCrLf &
                                $"• Errori / Non trovati: {prog.ErrorCount}"
                            )
                        ElseIf prog.IsCancelled Then
                            SetUiRunningState(False)
                            TxtStatus.Text = $"{AppLocalizations.GetText("bulk_status_cancelled", "Invio interrotto dall'utente.")} (Inviati: {prog.SentCount}, Errori: {prog.ErrorCount})"
                            If prog.TotalCount > 0 Then
                                ProgressBarSending.Value = (prog.CurrentIndex / CDbl(prog.TotalCount)) * 100
                            End If
                            DialogManager.ShowWarning(
                                Me,
                                AppLocalizations.GetText("bulk_title_cancelled", "Invio Interrotto"),
                                $"Invio interrotto su {_platform}." & vbCrLf & vbCrLf &
                                $"• Inviati con successo: {prog.SentCount}" & vbCrLf &
                                $"• Errori / Non trovati: {prog.ErrorCount}" & vbCrLf &
                                $"• Non inviati: {Math.Max(0, prog.TotalCount - prog.SentCount - prog.ErrorCount)}"
                            )
                        ElseIf prog.IsFailed Then
                            SetUiRunningState(False)
                            TxtStatus.Text = prog.StatusMessage
                            DialogManager.ShowError(Me, "Errore Invio Massivo", prog.StatusMessage)
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
            DialogManager.ShowWarning(Me, "Invio Massivo", ex.Message)
        Catch ex As Exception
            DialogManager.ShowError(Me, "Invio Massivo", $"Errore inatteso durante l'invio: {ex.Message}")
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
                BtnPause.Content = "Pausa"
                TxtStatus.Text = "Invio ripreso..."
            Else
                _engine.Pause()
                BtnPause.Content = "Riprendi"
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
        BtnPause.Content = "Pausa"
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
