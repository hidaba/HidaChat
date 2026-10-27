Imports System.Threading
Imports System.Threading.Tasks
Imports System.Linq
Imports Microsoft.Web.WebView2.Wpf
Imports System.Text.Json

''' <summary>
''' Notifica di progresso inviata dal motore di invio all'interfaccia utente.
''' </summary>
Public Class BulkSenderProgress
    Public Property CurrentIndex As Integer
    Public Property TotalCount As Integer
    Public Property SentCount As Integer
    Public Property ErrorCount As Integer
    Public Property CurrentContact As BulkContactItem
    Public Property StatusMessage As String
    Public Property IsCompleted As Boolean
    Public Property CountdownRemainingSeconds As Integer
End Class

''' <summary>
''' Motore di esecuzione asincrono per l'invio sequenziale di messaggi per WhatsApp Web e Telegram Web tramite WebView2.
''' Gestisce controlli di pausa/ripresa, annullamento, delay casuale anti-spam, gestione errori e routing dei protocolli.
''' Integra controlli anti-bozza e verifica della conferma osservabile dal client (TODO #79).
''' </summary>
Public Class BulkSenderEngine
    Private _cts As CancellationTokenSource
    Private ReadOnly _pauseEvent As New ManualResetEventSlim(True)
    Private ReadOnly _random As New Random()

    Public Property IsRunning As Boolean = False
    Public Property IsPaused As Boolean = False

    Public Sub Pause()
        If _isRunning AndAlso Not _isPaused Then
            _isPaused = True
            _pauseEvent.Reset()
        End If
    End Sub

    Public Sub ResumeSending()
        If _isRunning AndAlso _isPaused Then
            _isPaused = False
            _pauseEvent.Set()
        End If
    End Sub

    Public Sub Cancel()
        If _cts IsNot Nothing Then
            Try
                _cts.Cancel()
            Catch
            End Try
        End If
        _pauseEvent.Set()
        _isRunning = False
        _isPaused = False
    End Sub

    ''' <summary>
    ''' Avvia il ciclo di invio sequenziale per tutti i contatti selezionati sulla piattaforma indicata ("WhatsApp" o "Telegram").
    ''' </summary>
    Public Async Function RunAsync(
        contacts As IList(Of BulkContactItem),
        template As String,
        minDelaySec As Integer,
        maxDelaySec As Integer,
        platform As String,
        webView As WebView2,
        progressCallback As Action(Of BulkSenderProgress)
    ) As Task
        If webView Is Nothing OrElse webView.CoreWebView2 Is Nothing Then
            Throw New InvalidOperationException("Il controllo Web (WebView2) non è inizializzato o non è attivo.")
        End If

        Dim selectedContacts = contacts.Where(Function(c) c.Selected).ToList()
        If selectedContacts.Count = 0 Then Return

        Dim isTelegram = (PlatformRegistry.Get(platform).Platform = ChatPlatform.Telegram)

        _isRunning = True
        _isPaused = False
        _pauseEvent.Set()
        _cts = New CancellationTokenSource()
        Dim token = _cts.Token

        Dim total = selectedContacts.Count
        Dim sentCount = 0
        Dim errorCount = 0

        Try
            For i As Integer = 0 To total - 1
                If token.IsCancellationRequested Then Exit For

                While Not _pauseEvent.IsSet
                    Await Task.Delay(200, token)
                    If token.IsCancellationRequested Then Exit For
                End While

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
                    .IsCompleted = False
                })

                Dim success = False
                If isTelegram Then
                    success = Await SendTelegramMessageAsync(contact, message, webView, token)
                Else
                    success = Await SendWhatsAppMessageAsync(contact, message, webView, token)
                End If

                If success Then
                    contact.Status = "Inviato ✔"
                    sentCount += 1
                Else
                    If String.IsNullOrEmpty(contact.Status) OrElse contact.Status = "Inviando..." Then
                        contact.Status = "Errore ✖"
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
                    .IsCompleted = False
                })

                If i < total - 1 AndAlso Not token.IsCancellationRequested Then
                    Dim safeMin = Math.Max(30, Math.Min(minDelaySec, maxDelaySec))
                    Dim safeMax = Math.Max(safeMin, Math.Max(minDelaySec, maxDelaySec))
                    Dim delaySeconds = _random.Next(safeMin, safeMax + 1)
                    For s As Integer = delaySeconds To 1 Step -1
                        If token.IsCancellationRequested Then Exit For

                        While Not _pauseEvent.IsSet
                            Await Task.Delay(200, token)
                            If token.IsCancellationRequested Then Exit For
                        End While

                        progressCallback?.Invoke(New BulkSenderProgress With {
                            .CurrentIndex = i + 1,
                            .TotalCount = total,
                            .SentCount = sentCount,
                            .ErrorCount = errorCount,
                            .CurrentContact = contact,
                            .StatusMessage = $"Attesa anti-spam prima del prossimo contatto...",
                            .CountdownRemainingSeconds = s,
                            .IsCompleted = False
                        })

                        Await Task.Delay(1000, token)
                    Next
                End If
            Next
        Catch ex As OperationCanceledException
        Catch ex As Exception
            Debug.WriteLine($"[BulkSenderEngine] Errore generale: {ex.Message}")
        Finally
            _isRunning = False
            _isPaused = False
            progressCallback?.Invoke(New BulkSenderProgress With {
                .CurrentIndex = total,
                .TotalCount = total,
                .SentCount = sentCount,
                .ErrorCount = errorCount,
                .StatusMessage = "Completato",
                .IsCompleted = True
            })
        End Try
    End Function

    ''' <summary>
    ''' Invia un singolo messaggio su WhatsApp Web navigando a web.whatsapp.com/send e verificando
    ''' la presenza di bozze pregresse e l'effettiva conferma osservabile del client (TODO #79).
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
            Const maxConfirmationCycles = 12 ' fino a 6 secondi di attesa per la conferma osservabile

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
    ''' Invia un singolo messaggio su Telegram Web (K o A) risolvendo l'utente per @username o numero di telefono,
    ''' verificando l'assenza di bozze preesistenti e attendendo la conferma osservabile dell'invio (TODO #79).
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
            Const maxConfirmationCycles = 12 ' fino a 6 secondi di attesa per la conferma osservabile

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
