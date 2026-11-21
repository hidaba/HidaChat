Imports System.Threading
Imports System.Threading.Tasks
Imports System.Linq
Imports Microsoft.Web.WebView2.Wpf
Imports System.Text.Json

''' <summary>
''' Progress notification sent by the bulk sender engine to the UI.
''' </summary>
Public Class BulkSenderProgress
    Public Property CurrentIndex As Integer
    Public Property TotalCount As Integer
    Public Property SentCount As Integer
    Public Property ErrorCount As Integer
    Public Property CurrentContact As BulkContactItem
    Public Property StatusMessage As String
    Public Property IsCompleted As Boolean
    Public Property IsCancelled As Boolean
    Public Property IsFailed As Boolean
    Public Property CountdownRemainingSeconds As Integer
End Class

''' <summary>
''' Asynchronous execution engine for bulk message sending on WhatsApp Web and Telegram Web via WebView2.
''' Manages pause/resume, asynchronous cancellation, anti-spam randomized delays, error handling, and protocol routing.
''' Integrates pre-existing draft checks, concurrency exclusion, and clean Completed/Cancelled/Failed outcome separation (TASK-0080).
''' </summary>
Public Class BulkSenderEngine
    Private _cts As CancellationTokenSource
    Private ReadOnly _pauseEvent As New ManualResetEventSlim(True)
    Private ReadOnly _random As New Random()
    Private ReadOnly _syncLock As New Object()
    Private ReadOnly _coordinator As New BulkSenderExecutionCoordinator()
    Private _activeRunTcs As TaskCompletionSource(Of Boolean) = Nothing

    Public ReadOnly Property IsRunning As Boolean
        Get
            Return _coordinator.IsRunning
        End Get
    End Property

    Public Property IsPaused As Boolean = False

    Public ReadOnly Property IsStopping As Boolean
        Get
            Return _coordinator.IsStopping
        End Get
    End Property

    Public ReadOnly Property Coordinator As BulkSenderExecutionCoordinator
        Get
            Return _coordinator
        End Get
    End Property

    Public Sub Pause()
        SyncLock _syncLock
            If _coordinator.IsRunning AndAlso Not _isPaused AndAlso Not _coordinator.IsStopping Then
                _isPaused = True
                _pauseEvent.Reset()
            End If
        End SyncLock
    End Sub

    Public Sub ResumeSending()
        SyncLock _syncLock
            If _coordinator.IsRunning AndAlso _isPaused AndAlso Not _coordinator.IsStopping Then
                _isPaused = False
                _pauseEvent.Set()
            End If
        End SyncLock
    End Sub

    Public Sub Cancel()
        SyncLock _syncLock
            If Not _coordinator.IsRunning Then Return
            _coordinator.BeginStop()
        End SyncLock
        If _cts IsNot Nothing Then
            Try
                _cts.Cancel()
            Catch
            End Try
        End If
        _pauseEvent.Set()
    End Sub

    ''' <summary>
    ''' Requests cancellation of bulk sending and awaits the actual completion of the active execution task (TASK-0080).
    ''' </summary>
    Public Async Function CancelAsync() As Task
        Dim waitTask As Task = Nothing
        SyncLock _syncLock
            If Not _coordinator.IsRunning Then Return
            _coordinator.BeginStop()
            If _activeRunTcs IsNot Nothing Then
                waitTask = _activeRunTcs.Task
            End If
        End SyncLock

        If _cts IsNot Nothing Then
            Try
                _cts.Cancel()
            Catch
            End Try
        End If
        _pauseEvent.Set()

        If waitTask IsNot Nothing Then
            Try
                Await Task.WhenAny(waitTask, Task.Delay(10000))
            Catch
            End Try
        End If
    End Function

    ''' <summary>
    ''' Starts the sequential sending cycle for all selected contacts on the indicated platform ("WhatsApp" or "Telegram").
    ''' Prevents concurrent execution independently of UI via BulkSenderExecutionCoordinator (TASK-0080).
    ''' </summary>
    Public Async Function RunAsync(
        contacts As IList(Of BulkContactItem),
        template As String,
        minDelaySec As Integer,
        maxDelaySec As Integer,
        platform As String,
        webView As WebView2,
        progressCallback As Action(Of BulkSenderProgress),
        Optional messageSender As Func(Of BulkContactItem, String, CancellationToken, Task(Of Boolean)) = Nothing
    ) As Task
        If messageSender Is Nothing Then
            If webView Is Nothing OrElse webView.CoreWebView2 Is Nothing Then
                Throw New InvalidOperationException("Il controllo Web (WebView2) non è inizializzato o non è attivo.")
            End If
        End If

        Dim selectedContacts = contacts?.Where(Function(c) c.Selected).ToList()
        If selectedContacts Is Nothing OrElse selectedContacts.Count = 0 Then Return

        Dim tcs As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        SyncLock _syncLock
            If Not _coordinator.TryBeginExecution() Then
                Throw New InvalidOperationException("Un'operazione di invio massivo è già in corso o in fase di arresto.")
            End If
            _isPaused = False
            _activeRunTcs = tcs
        End SyncLock

        Dim def = PlatformRegistry.Get(platform)
        If Not def.SupportsBulkSender Then
            Throw New NotSupportedException($"L'invio massivo non è supportato per la piattaforma {def.DisplayName}.")
        End If
        Dim isTelegram = (def.Platform = ChatPlatform.Telegram)

        _pauseEvent.Set()
        _cts = New CancellationTokenSource()
        Dim token = _cts.Token

        Dim total = selectedContacts.Count
        Dim sentCount = 0
        Dim errorCount = 0
        Dim wasCancelled = False
        Dim errorOccurred = False
        Dim errorDetail = String.Empty

        Try
            For i As Integer = 0 To total - 1
                If token.IsCancellationRequested Then
                    wasCancelled = True
                    Exit For
                End If

                While Not _pauseEvent.IsSet
                    Await Task.Delay(200, token)
                    If token.IsCancellationRequested Then
                        wasCancelled = True
                        Exit For
                    End If
                End While

                If token.IsCancellationRequested Then
                    wasCancelled = True
                    Exit For
                End If

                Dim contact = selectedContacts(i)
                contact.Status = "Inviando..."
                contact.ErrorMessage = String.Empty

                Dim message = contact.GenerateMessage(template)
                contact.PreviewMessage = message

                Dim recipientLabel = If(isTelegram AndAlso Not String.IsNullOrEmpty(contact.FormattedUsername), contact.FormattedUsername, contact.CleanPhone)

                progressCallback?.Invoke(New BulkSenderProgress With {
                    .CurrentIndex = i + 1,
                    .TotalCount = total,
                    .SentCount = sentCount,
                    .ErrorCount = errorCount,
                    .CurrentContact = contact,
                    .StatusMessage = $"Invio a {contact.FullName} ({recipientLabel})...",
                    .IsCompleted = False,
                    .IsCancelled = False,
                    .IsFailed = False
                })

                Dim success = False
                If messageSender IsNot Nothing Then
                    success = Await messageSender(contact, message, token)
                ElseIf isTelegram Then
                    success = Await SendTelegramMessageAsync(contact, message, webView, token)
                Else
                    success = Await SendWhatsAppMessageAsync(contact, message, webView, token)
                End If

                If token.IsCancellationRequested Then
                    wasCancelled = True
                    contact.Status = AppLocalizations.GetText("bulk_status_interrupted", "Interrotto")
                    contact.ErrorMessage = AppLocalizations.GetText("bulk_status_cancelled", "Invio interrotto dall'utente.")
                    Exit For
                End If

                If success Then
                    contact.Status = "Inviato " & ChrW(&H2714)
                    sentCount += 1
                Else
                    If String.IsNullOrEmpty(contact.Status) OrElse contact.Status = "Inviando..." Then
                        contact.Status = "Errore " & ChrW(&H2716)
                    End If
                    errorCount += 1
                End If

                progressCallback?.Invoke(New BulkSenderProgress With {
                    .CurrentIndex = i + 1,
                    .TotalCount = total,
                    .SentCount = sentCount,
                    .ErrorCount = errorCount,
                    .CurrentContact = contact,
                    .StatusMessage = If(success, $"Messaggio inviato a {contact.FullName}", $"Errore per {contact.FullName}: {contact.ErrorMessage}"),
                    .IsCompleted = False,
                    .IsCancelled = False,
                    .IsFailed = False
                })

                If i < total - 1 AndAlso Not token.IsCancellationRequested Then
                    Dim safeMin = Math.Max(30, Math.Min(minDelaySec, maxDelaySec))
                    Dim safeMax = Math.Max(safeMin, Math.Max(minDelaySec, maxDelaySec))
                    Dim delaySeconds = _random.Next(safeMin, safeMax + 1)
                    For s As Integer = delaySeconds To 1 Step -1
                        If token.IsCancellationRequested Then
                            wasCancelled = True
                            Exit For
                        End If

                        While Not _pauseEvent.IsSet
                            Await Task.Delay(200, token)
                            If token.IsCancellationRequested Then
                                wasCancelled = True
                                Exit For
                            End If
                        End While

                        If token.IsCancellationRequested Then
                            wasCancelled = True
                            Exit For
                        End If

                        progressCallback?.Invoke(New BulkSenderProgress With {
                            .CurrentIndex = i + 1,
                            .TotalCount = total,
                            .SentCount = sentCount,
                            .ErrorCount = errorCount,
                            .CurrentContact = contact,
                            .StatusMessage = "Attesa anti-spam prima del prossimo contatto...",
                            .CountdownRemainingSeconds = s,
                            .IsCompleted = False,
                            .IsCancelled = False,
                            .IsFailed = False
                        })

                        Await Task.Delay(1000, token)
                    Next
                End If
            Next
        Catch ex As OperationCanceledException
            wasCancelled = True
        Catch ex As Exception
            errorOccurred = True
            errorDetail = ex.Message
            Debug.WriteLine($"[BulkSenderEngine] Errore generale: {ex.Message}")
        Finally
            Dim finalOutcome As BulkSenderOutcome = BulkSenderOutcome.Completed
            SyncLock _syncLock
                Dim isCancelledFinal = wasCancelled OrElse (_cts IsNot Nothing AndAlso _cts.IsCancellationRequested)
                finalOutcome = _coordinator.DetermineOutcome(sentCount, errorCount, total, isCancelledFinal, errorOccurred)
                _coordinator.EndExecution()
                _isPaused = False
                _activeRunTcs = Nothing
            End SyncLock
            tcs.TrySetResult(True)

            Select Case finalOutcome
                Case BulkSenderOutcome.Cancelled
                    progressCallback?.Invoke(New BulkSenderProgress With {
                        .CurrentIndex = sentCount + errorCount,
                        .TotalCount = total,
                        .SentCount = sentCount,
                        .ErrorCount = errorCount,
                        .StatusMessage = AppLocalizations.GetText("bulk_status_cancelled", "Invio interrotto dall'utente."),
                        .IsCompleted = False,
                        .IsCancelled = True,
                        .IsFailed = False
                    })
                Case BulkSenderOutcome.Failed
                    progressCallback?.Invoke(New BulkSenderProgress With {
                        .CurrentIndex = sentCount + errorCount,
                        .TotalCount = total,
                        .SentCount = sentCount,
                        .ErrorCount = errorCount,
                        .StatusMessage = String.Format(AppLocalizations.GetText("bulk_status_failed", "Errore durante l'invio: {0}"), errorDetail),
                        .IsCompleted = False,
                        .IsCancelled = False,
                        .IsFailed = True
                    })
                Case Else
                    progressCallback?.Invoke(New BulkSenderProgress With {
                        .CurrentIndex = total,
                        .TotalCount = total,
                        .SentCount = sentCount,
                        .ErrorCount = errorCount,
                        .StatusMessage = AppLocalizations.GetText("bulk_status_completed", "Completato"),
                        .IsCompleted = True,
                        .IsCancelled = False,
                        .IsFailed = False
                    })
            End Select
        End Try
    End Function

    ''' <summary>
    ''' Sends a single message on WhatsApp Web by navigating to web.whatsapp.com/send, verifying
    ''' existing drafts, and observing client delivery confirmation (TASK-0079).
    ''' </summary>
    Private Async Function SendWhatsAppMessageAsync(contact As BulkContactItem, message As String, webView As WebView2, token As CancellationToken) As Task(Of Boolean)
        Try
            If String.IsNullOrWhiteSpace(contact.Phone) Then
                contact.ErrorMessage = AppLocalizations.GetText("bulk_err_phone_missing", "Numero di telefono mancante")
                Return False
            End If

            Dim cleanNum = New String(contact.Phone.Where(AddressOf Char.IsDigit).ToArray())
            If String.IsNullOrEmpty(cleanNum) Then
                contact.ErrorMessage = AppLocalizations.GetText("bulk_err_phone_invalid", "Numero di telefono non valido")
                Return False
            End If

            Dim encodedMsg = Uri.EscapeDataString(message)
            Dim targetUrl = $"https://web.whatsapp.com/send?phone={cleanNum}&text={encodedMsg}"

            webView.CoreWebView2.Navigate(targetUrl)

            Dim maxWaitCycles = 60
            Dim cycle = 0
            Dim sent = False
            Dim actionTriggered = False
            Dim confirmationCycles = 0
            Const maxConfirmationCycles = 12 ' up to 6 seconds waiting for client confirmation

            While cycle < maxWaitCycles
                If token.IsCancellationRequested Then Return False

                Await Task.Delay(500, token)
                cycle += 1

                Dim js = BulkSenderPolicy.BuildWhatsAppScript(message, cleanNum, actionTriggered)
                Dim jsonResult = Await webView.CoreWebView2.ExecuteScriptAsync(js)
                Dim stepResult = BulkSenderPolicy.ParseScriptResult(jsonResult)

                Select Case stepResult.Status
                    Case "CONFIRMED_SENT"
                        sent = True
                        Exit While

                    Case "TRIGGERED"
                        actionTriggered = True
                        confirmationCycles = 0

                    Case "WAITING_CONFIRMATION"
                        confirmationCycles += 1
                        If confirmationCycles >= maxConfirmationCycles Then
                            contact.ErrorMessage = AppLocalizations.GetText("bulk_err_send_rejected", "Azione di invio non accettata dal client o pressione di invio ignorata")
                            Return False
                        End If

                    Case "DRAFT_CONFLICT"
                        contact.ErrorMessage = AppLocalizations.GetText("bulk_err_draft_detected", "Bozza preesistente rilevata nel campo di testo: invio interrotto per prevenire invii errati")
                        Return False

                    Case "INVALID_NUMBER"
                        contact.ErrorMessage = If(Not String.IsNullOrEmpty(stepResult.ErrorMessage), stepResult.ErrorMessage, AppLocalizations.GetText("bulk_err_number_not_registered", "Numero non registrato su WhatsApp"))
                        Return False

                    Case "ERROR"
                        Debug.WriteLine($"[BulkSenderEngine] WhatsApp JS error: {stepResult.ErrorMessage}")
                End Select
            End While

            If sent Then
                Await Task.Delay(1500, token)
                Return True
            End If

            contact.ErrorMessage = AppLocalizations.GetText("bulk_err_send_timeout", "Timeout durante l'attesa della conferma di invio del client")
            Return False
        Catch ex As OperationCanceledException
            Return False
        Catch ex As Exception
            contact.ErrorMessage = ex.Message
            Debug.WriteLine($"[BulkSenderEngine] Exception sending WhatsApp message: {ex.Message}")
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Sends a single message on Telegram Web (K or A) by resolving the user by @username or phone number,
    ''' verifying absence of pre-existing drafts, and waiting for observable client delivery confirmation (TASK-0079).
    ''' </summary>
    Private Async Function SendTelegramMessageAsync(contact As BulkContactItem, message As String, webView As WebView2, token As CancellationToken) As Task(Of Boolean)
        Try
            Dim cleanUser = contact.CleanUsername
            Dim cleanPhone = New String(contact.Phone.Where(AddressOf Char.IsDigit).ToArray())

            If String.IsNullOrEmpty(cleanUser) AndAlso String.IsNullOrEmpty(cleanPhone) Then
                contact.ErrorMessage = AppLocalizations.GetText("bulk_err_recipient_missing", "Nessun username o numero di telefono specificato")
                Return False
            End If

            Dim targetUrl As String
            Dim targetDisplay As String
            If Not String.IsNullOrEmpty(cleanUser) Then
                targetUrl = $"https://web.telegram.org/k/#?tgaddr=tg%3A%2F%2Fresolve%3Fdomain%3D{Uri.EscapeDataString(cleanUser)}"
                targetDisplay = "@" & cleanUser
            Else
                targetUrl = $"https://web.telegram.org/k/#?tgaddr=tg%3A%2F%2Fresolve%3Fphone%3D{cleanPhone}"
                targetDisplay = cleanPhone
            End If

            webView.CoreWebView2.Navigate(targetUrl)

            Dim maxWaitCycles = 60
            Dim cycle = 0
            Dim sent = False
            Dim actionTriggered = False
            Dim confirmationCycles = 0
            Const maxConfirmationCycles = 12 ' up to 6 seconds waiting for client confirmation

            While cycle < maxWaitCycles
                If token.IsCancellationRequested Then Return False

                Await Task.Delay(500, token)
                cycle += 1

                Dim js = BulkSenderPolicy.BuildTelegramScript(message, targetDisplay, actionTriggered)
                Dim jsonResult = Await webView.CoreWebView2.ExecuteScriptAsync(js)
                Dim stepResult = BulkSenderPolicy.ParseScriptResult(jsonResult)

                Select Case stepResult.Status
                    Case "CONFIRMED_SENT"
                        sent = True
                        Exit While

                    Case "TRIGGERED"
                        actionTriggered = True
                        confirmationCycles = 0

                    Case "WAITING_CONFIRMATION"
                        confirmationCycles += 1
                        If confirmationCycles >= maxConfirmationCycles Then
                            contact.ErrorMessage = AppLocalizations.GetText("bulk_err_send_rejected", "Azione di invio non accettata dal client o pressione di invio ignorata")
                            Return False
                        End If

                    Case "DRAFT_CONFLICT"
                        contact.ErrorMessage = AppLocalizations.GetText("bulk_err_draft_detected", "Bozza preesistente rilevata nella chat Telegram: invio interrotto per prevenire invii errati")
                        Return False

                    Case "USER_NOT_FOUND"
                        contact.ErrorMessage = If(Not String.IsNullOrEmpty(stepResult.ErrorMessage), stepResult.ErrorMessage, AppLocalizations.GetText("bulk_err_user_not_found", "Utente o numero non trovato su Telegram"))
                        Return False

                    Case "ERROR"
                        Debug.WriteLine($"[BulkSenderEngine] Telegram JS error: {stepResult.ErrorMessage}")
                End Select
            End While

            If sent Then
                Await Task.Delay(1500, token)
                Return True
            End If

            contact.ErrorMessage = AppLocalizations.GetText("bulk_err_send_timeout", "Timeout durante l'attesa della conferma di invio del client")
            Return False
        Catch ex As OperationCanceledException
            Return False
        Catch ex As Exception
            contact.ErrorMessage = ex.Message
            Debug.WriteLine($"[BulkSenderEngine] Exception sending Telegram message: {ex.Message}")
            Return False
        End Try
    End Function
End Class
