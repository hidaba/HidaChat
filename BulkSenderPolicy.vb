Option Explicit On
Option Strict On

Imports System
Imports System.Linq
Imports System.Text.Json

''' <summary>
''' Rappresenta il risultato parsato di uno step di verifica o esecuzione dello script di invio massivo.
''' </summary>
Public Class BulkStepResult
    Public Property Status As String = String.Empty
    Public Property ErrorMessage As String = String.Empty
    Public Property Detail As String = String.Empty
    Public Property CurrentDraft As String = String.Empty
End Class

''' <summary>
''' Policy e logiche deterministiche per la prevenzione di falsi positivi nell'invio massivo (TODO #79).
''' Isola il controllo delle bozze preesistenti, la validazione dell'identità del destinatario,
''' la generazione degli script di interazione e la verifica della conferma osservabile del client.
''' </summary>
Public Module BulkSenderPolicy

    ''' <summary>
    ''' Normalizza il testo per il confronto (rimuove spazi superflui, normalizza a capo e converte spazi indivisibili).
    ''' </summary>
    Public Function NormalizeText(text As String) As String
        If String.IsNullOrEmpty(text) Then Return String.Empty
        Return text.Replace(ChrW(&HA0), " "c).Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Trim()
    End Function

    ''' <summary>
    ''' Verifica se il testo attualmente presente nella casella di digitazione costituisce una bozza in conflitto
    ''' con il messaggio atteso (previene l'invio o sovrascrittura di testi preesistenti dell'utente).
    ''' </summary>
    Public Function IsDraftConflict(existingText As String, expectedText As String) As Boolean
        Dim normExisting = NormalizeText(existingText)
        Dim normExpected = NormalizeText(expectedText)

        If String.IsNullOrEmpty(normExisting) Then
            Return False
        End If

        Return Not String.Equals(normExisting, normExpected, StringComparison.Ordinal)
    End Function

    ''' <summary>
    ''' Verifica se l'intestazione o il contesto della chat aperta corrisponde al destinatario desiderato
    ''' (previene l'invio involontario alla chat precedente prima del caricamento della nuova navigazione).
    ''' </summary>
    Public Function MatchesRecipient(headerText As String, expectedPhone As String, expectedUser As String, expectedName As String) As Boolean
        If String.IsNullOrWhiteSpace(headerText) Then Return False
        Dim normHeader = headerText.ToLowerInvariant()

        ' 1. Corrispondenza cifre numero telefonico (almeno le ultime 6 cifre)
        If Not String.IsNullOrWhiteSpace(expectedPhone) Then
            Dim digits = New String(expectedPhone.Where(AddressOf Char.IsDigit).ToArray())
            If digits.Length >= 6 Then
                Dim last6 = If(digits.Length > 6, digits.Substring(digits.Length - 6), digits)
                Dim headerDigits = New String(headerText.Where(AddressOf Char.IsDigit).ToArray())
                If headerDigits.Contains(last6) Then Return True
            End If
        End If

        ' 2. Corrispondenza username Telegram
        If Not String.IsNullOrWhiteSpace(expectedUser) Then
            Dim cleanUser = expectedUser.Trim().TrimStart("@"c).ToLowerInvariant()
            If cleanUser.Length >= 3 AndAlso normHeader.Contains(cleanUser) Then Return True
        End If

        ' 3. Corrispondenza parti anagrafiche del nome (almeno 3 caratteri)
        If Not String.IsNullOrWhiteSpace(expectedName) AndAlso expectedName.Trim() <> "-" Then
            Dim parts = expectedName.Split({" "c, ","c, "-"c}, StringSplitOptions.RemoveEmptyEntries)
            For Each p In parts
                Dim term = p.Trim().ToLowerInvariant()
                If term.Length >= 3 AndAlso normHeader.Contains(term) Then Return True
            Next
        End If

        Return False
    End Function

    ''' <summary>
    ''' Determina se l'invio è confermato osservando lo stato della casella di input e degli indicatori del client.
    ''' </summary>
    Public Function IsSendConfirmed(inputCurrentText As String, actionTriggered As Boolean, isSendButtonVisible As Boolean) As Boolean
        If Not actionTriggered Then Return False
        Dim normInput = NormalizeText(inputCurrentText)

        ' La conferma osservabile primaria è che il campo di digitazione sia stato completamente svuotato dal client
        If String.IsNullOrEmpty(normInput) Then
            ' E che il pulsante di invio non sia più visibile (trasformato in icona microfono)
            If Not isSendButtonVisible Then
                Return True
            End If
            ' Se anche l'input è vuoto ma il pulsante invio non è stato ancora aggiornato, l'azzeramento dell'input conferma l'accettazione
            Return True
        End If

        Return False
    End Function

    ''' <summary>
    ''' Costruisce lo script JavaScript di verifica e automazione per WhatsApp Web.
    ''' </summary>
    Public Function BuildWhatsAppScript(message As String, expectedPhoneDigits As String, actionTriggered As Boolean) As String
        Dim jsonMsg = JsonSerializer.Serialize(message)
        Dim jsonPhone = JsonSerializer.Serialize(If(expectedPhoneDigits, String.Empty))
        Dim actionTriggeredJs = If(actionTriggered, "true", "false")

        Return "(function() {" &
               "  try {" &
               "    var popups = document.querySelectorAll('div[data-animate-modal-popup=""true""], [role=""dialog""]');" &
               "    for (var i = 0; i < popups.length; i++) {" &
               "      var text = popups[i].innerText || '';" &
               "      var lowText = text.toLowerCase();" &
               "      if (lowText.indexOf('invalid') !== -1 || lowText.indexOf('valido') !== -1 || lowText.indexOf('url') !== -1) {" &
               "        var okBtn = popups[i].querySelector('button');" &
               "        if (okBtn) okBtn.click();" &
               "        return JSON.stringify({ status: 'INVALID_NUMBER', msg: text });" &
               "      }" &
               "    }" &
               "    var input = document.querySelector('footer div[contenteditable=""true""], div[data-testid=""conversation-compose-box-input""]');" &
               "    var sendBtn = document.querySelector('button[aria-label=""Send""], button[aria-label=""Invia""], span[data-icon=""send""], span[data-icon=""wds-ic-send-filled""]');" &
               "    var isSendBtnVisible = !!(sendBtn && (sendBtn.offsetWidth > 0 || sendBtn.offsetHeight > 0 || (sendBtn.closest('button') && sendBtn.closest('button').offsetWidth > 0)));" &
               "    var currentInputText = input ? (input.innerText || '').replace(/\u00a0/g, ' ').trim() : '';" &
               "    var desiredText = " & jsonMsg & ".replace(/\u00a0/g, ' ').trim();" &
               "    var actionTriggered = " & actionTriggeredJs & ";" &
               "    if (actionTriggered) {" &
               "      if (currentInputText.length === 0 && !isSendBtnVisible) {" &
               "        return JSON.stringify({ status: 'CONFIRMED_SENT' });" &
               "      }" &
               "      if (currentInputText.length === 0) {" &
               "        return JSON.stringify({ status: 'CONFIRMED_SENT' });" &
               "      }" &
               "      return JSON.stringify({ status: 'WAITING_CONFIRMATION', currentText: currentInputText });" &
               "    }" &
               "    if (!input) {" &
               "      return JSON.stringify({ status: 'WAITING', detail: 'composer_not_found' });" &
               "    }" &
               "    if (currentInputText.length > 0) {" &
               "      if (currentInputText === desiredText || currentInputText.indexOf(desiredText) !== -1 || desiredText.indexOf(currentInputText) !== -1) {" &
               "        var btn = sendBtn ? (sendBtn.tagName === 'BUTTON' ? sendBtn : sendBtn.closest('button')) : null;" &
               "        if (btn && !btn.disabled) {" &
               "          btn.click();" &
               "        } else {" &
               "          input.focus();" &
               "          var evDown = new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', which: 13, keyCode: 13, bubbles: true });" &
               "          var evPress = new KeyboardEvent('keypress', { key: 'Enter', code: 'Enter', which: 13, keyCode: 13, bubbles: true });" &
               "          var evUp = new KeyboardEvent('keyup', { key: 'Enter', code: 'Enter', which: 13, keyCode: 13, bubbles: true });" &
               "          input.dispatchEvent(evDown);" &
               "          input.dispatchEvent(evPress);" &
               "          input.dispatchEvent(evUp);" &
               "        }" &
               "        return JSON.stringify({ status: 'TRIGGERED' });" &
               "      } else {" &
               "        return JSON.stringify({ status: 'DRAFT_CONFLICT', currentDraft: currentInputText });" &
               "      }" &
               "    } else {" &
               "      return JSON.stringify({ status: 'WAITING', detail: 'input_loading_from_url' });" &
               "    }" &
               "  } catch(e) {" &
               "    return JSON.stringify({ status: 'ERROR', error: e.message });" &
               "  }" &
               "})();"
    End Function

    ''' <summary>
    ''' Costruisce lo script JavaScript di verifica e automazione per Telegram Web (K e A).
    ''' </summary>
    Public Function BuildTelegramScript(message As String, expectedUserOrPhone As String, actionTriggered As Boolean) As String
        Dim jsonMsg = JsonSerializer.Serialize(message)
        Dim jsonTarget = JsonSerializer.Serialize(If(expectedUserOrPhone, String.Empty))
        Dim actionTriggeredJs = If(actionTriggered, "true", "false")

        Return "(function() {" &
               "  try {" &
               "    var popups = document.querySelectorAll('.popup-body, .modal-dialog, .toast, .error-message, .c-ripple');" &
               "    for (var i = 0; i < popups.length; i++) {" &
               "      var text = popups[i].innerText || '';" &
               "      var low = text.toLowerCase();" &
               "      if (low.indexOf('not found') !== -1 || low.indexOf('non trovato') !== -1 || low.indexOf(""doesn't exist"") !== -1) {" &
               "        return JSON.stringify({ status: 'USER_NOT_FOUND', msg: text });" &
               "      }" &
               "    }" &
               "    var input = document.querySelector('.input-message-input, div.composer-content, div.input-message-container div[contenteditable=""true""], .input-message-document div[contenteditable=""true""]');" &
               "    var sendBtn = document.querySelector('button.btn-send, button.send, div.btn-send, button.main-button.send');" &
               "    var isSendBtnVisible = !!(sendBtn && !sendBtn.disabled && (sendBtn.offsetWidth > 0 || sendBtn.offsetHeight > 0));" &
               "    var currentInputText = input ? (input.innerText || input.textContent || '').replace(/\u00a0/g, ' ').trim() : '';" &
               "    var desiredText = " & jsonMsg & ".replace(/\u00a0/g, ' ').trim();" &
               "    var actionTriggered = " & actionTriggeredJs & ";" &
               "    if (actionTriggered) {" &
               "      if (currentInputText.length === 0) {" &
               "        return JSON.stringify({ status: 'CONFIRMED_SENT' });" &
               "      }" &
               "      return JSON.stringify({ status: 'WAITING_CONFIRMATION', currentText: currentInputText });" &
               "    }" &
               "    if (!input) {" &
               "      return JSON.stringify({ status: 'WAITING', detail: 'composer_not_found' });" &
               "    }" &
               "    if (currentInputText.length > 0) {" &
               "      if (currentInputText !== desiredText) {" &
               "        return JSON.stringify({ status: 'DRAFT_CONFLICT', currentDraft: currentInputText });" &
               "      }" &
               "    } else {" &
               "      input.focus();" &
               "      document.execCommand('insertText', false, " & jsonMsg & ");" &
               "      input.dispatchEvent(new Event('input', { bubbles: true }));" &
               "      input.dispatchEvent(new Event('change', { bubbles: true }));" &
               "      currentInputText = (input.innerText || input.textContent || '').replace(/\u00a0/g, ' ').trim();" &
               "      if (currentInputText.length === 0) {" &
               "        input.innerText = " & jsonMsg & ";" &
               "        input.dispatchEvent(new Event('input', { bubbles: true }));" &
               "        input.dispatchEvent(new Event('change', { bubbles: true }));" &
               "      }" &
               "    }" &
               "    var btn = sendBtn ? (sendBtn.tagName === 'BUTTON' ? sendBtn : sendBtn.closest('button')) : null;" &
               "    if (btn && !btn.disabled) {" &
               "      btn.click();" &
               "    } else {" &
               "      input.focus();" &
               "      var evDown = new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', which: 13, keyCode: 13, bubbles: true });" &
               "      var evPress = new KeyboardEvent('keypress', { key: 'Enter', code: 'Enter', which: 13, keyCode: 13, bubbles: true });" &
               "      var evUp = new KeyboardEvent('keyup', { key: 'Enter', code: 'Enter', which: 13, keyCode: 13, bubbles: true });" &
               "      input.dispatchEvent(evDown);" &
               "      input.dispatchEvent(evPress);" &
               "      input.dispatchEvent(evUp);" &
               "    }" &
               "    return JSON.stringify({ status: 'TRIGGERED' });" &
               "  } catch(e) {" &
               "    return JSON.stringify({ status: 'ERROR', error: e.message });" &
               "  }" &
               "})();"
    End Function

    ''' <summary>
    ''' Analizza in modo sicuro il risultato JSON restituito dallo script WebView2.
    ''' </summary>
    Public Function ParseScriptResult(rawJson As String) As BulkStepResult
        Dim res As New BulkStepResult With {.Status = "WAITING"}
        If String.IsNullOrWhiteSpace(rawJson) OrElse rawJson = "null" Then
            Return res
        End If

        Try
            Dim unescaped As String = rawJson
            If rawJson.StartsWith(""""c) AndAlso rawJson.EndsWith(""""c) Then
                unescaped = JsonSerializer.Deserialize(Of String)(rawJson)
            End If

            If String.IsNullOrWhiteSpace(unescaped) Then
                Return res
            End If

            Using doc = JsonDocument.Parse(unescaped)
                Dim root = doc.RootElement
                Dim statusProp As JsonElement = Nothing
                If root.TryGetProperty("status", statusProp) Then
                    res.Status = statusProp.GetString()
                End If
                Dim msgProp As JsonElement = Nothing
                If root.TryGetProperty("msg", msgProp) Then
                    res.ErrorMessage = msgProp.GetString()
                ElseIf root.TryGetProperty("error", msgProp) Then
                    res.ErrorMessage = msgProp.GetString()
                End If
                Dim detailProp As JsonElement = Nothing
                If root.TryGetProperty("detail", detailProp) Then
                    res.Detail = detailProp.GetString()
                End If
                Dim draftProp As JsonElement = Nothing
                If root.TryGetProperty("currentDraft", draftProp) Then
                    res.CurrentDraft = draftProp.GetString()
                End If
            End Using
        Catch ex As Exception
            res.Status = "ERROR"
            res.ErrorMessage = ex.Message
        End Try

        Return res
    End Function


    ''' <summary>
    ''' Rileva il delimitatore CSV (, ; \t) analizzando i caratteri al di fuori dei campi quotati (RFC 4180).
    ''' Ignora virgole, punti e virgola o tabulazioni racchiusi tra doppi apici (TODO #84).
    ''' </summary>
    Public Function DetectCsvDelimiter(csvContent As String) As Char
        If String.IsNullOrEmpty(csvContent) Then Return ","c

        Dim commaCount = 0
        Dim semiCount = 0
        Dim tabCount = 0
        Dim inQuotes = False
        Dim lineCount = 0
        Dim maxLinesToSample = 10
        Dim maxCharsToSample = Math.Min(csvContent.Length, 8192)

        Dim i = 0
        While i < maxCharsToSample
            Dim c = csvContent(i)
            If c = """"c Then
                If inQuotes AndAlso i + 1 < maxCharsToSample AndAlso csvContent(i + 1) = """"c Then
                    i += 1
                Else
                    inQuotes = Not inQuotes
                End If
            ElseIf Not inQuotes Then
                If c = ","c Then
                    commaCount += 1
                ElseIf c = ";"c Then
                    semiCount += 1
                ElseIf c = ControlChars.Tab Then
                    tabCount += 1
                ElseIf c = ControlChars.Lf Then
                    lineCount += 1
                    If lineCount >= maxLinesToSample Then Exit While
                End If
            End If
            i += 1
        End While

        If semiCount > commaCount AndAlso semiCount > tabCount Then Return ";"c
        If tabCount > commaCount AndAlso tabCount > semiCount Then Return ControlChars.Tab
        Return ","c
    End Function

    ''' <summary>
    ''' Analizza l'intero contenuto CSV in record conformi allo standard RFC 4180.
    ''' Mantiene i ritorni a capo all'interno di campi racchiusi tra virgolette, gestisce l'escape delle virgolette doppie ("")
    ''' e separa correttamente le righe senza rompere i messaggi multilinea (TODO #84).
    ''' </summary>
    Public Function ParseCsvRecords(csvContent As String, delimiter As Char) As List(Of List(Of String))
        Dim records As New List(Of List(Of String))()
        If String.IsNullOrWhiteSpace(csvContent) Then Return records

        Dim currentRecord As New List(Of String)()
        Dim currentField As New System.Text.StringBuilder()
        Dim inQuotes = False
        Dim i = 0
        Dim len = csvContent.Length

        While i < len
            Dim c = csvContent(i)

            If c = """"c Then
                If inQuotes AndAlso i + 1 < len AndAlso csvContent(i + 1) = """"c Then
                    currentField.Append(""""c)
                    i += 1
                Else
                    inQuotes = Not inQuotes
                End If
            ElseIf c = delimiter AndAlso Not inQuotes Then
                currentRecord.Add(currentField.ToString().Trim())
                currentField.Clear()
            ElseIf (c = ControlChars.Cr OrElse c = ControlChars.Lf) AndAlso Not inQuotes Then
                If c = ControlChars.Cr AndAlso i + 1 < len AndAlso csvContent(i + 1) = ControlChars.Lf Then
                    i += 1
                End If
                currentRecord.Add(currentField.ToString().Trim())
                currentField.Clear()

                If currentRecord.Count > 1 OrElse (currentRecord.Count = 1 AndAlso Not String.IsNullOrWhiteSpace(currentRecord(0))) Then
                    records.Add(currentRecord)
                End If
                currentRecord = New List(Of String)()
            Else
                currentField.Append(c)
            End If

            i += 1
        End While

        If currentField.Length > 0 OrElse currentRecord.Count > 0 Then
            currentRecord.Add(currentField.ToString().Trim())
            If currentRecord.Count > 1 OrElse (currentRecord.Count = 1 AndAlso Not String.IsNullOrWhiteSpace(currentRecord(0))) Then
                records.Add(currentRecord)
            End If
        End If

        Return records
    End Function
    ''' <summary>
    ''' Verifica se la piattaforma specificata supporta lo strumento di invio massivo.
    ''' </summary>
    Public Function IsPlatformSupported(platformDef As PlatformDefinition) As Boolean
        If platformDef Is Nothing Then Return False
        Return platformDef.SupportsBulkSender
    End Function

End Module

''' <summary>
''' Esito finale dell'invio massivo (TODO #80).
''' </summary>
Public Enum BulkSenderOutcome
    Running
    Stopping
    Completed
    Cancelled
    Failed
End Enum

''' <summary>
''' Coordinatore thread-safe della concorrenza e degli stati di esecuzione dell'invio massivo (TODO #80).
''' </summary>
Public Class BulkSenderExecutionCoordinator
    Private ReadOnly _syncLock As New Object()

    Public Property IsRunning As Boolean = False
    Public Property IsStopping As Boolean = False

    Public Function TryBeginExecution() As Boolean
        SyncLock _syncLock
            If IsRunning OrElse IsStopping Then Return False
            IsRunning = True
            IsStopping = False
            Return True
        End SyncLock
    End Function

    Public Sub BeginStop()
        SyncLock _syncLock
            If IsRunning Then
                IsStopping = True
            End If
        End SyncLock
    End Sub

    Public Sub EndExecution()
        SyncLock _syncLock
            IsRunning = False
            IsStopping = False
        End SyncLock
    End Sub

    Public Function DetermineOutcome(sentCount As Integer, errorCount As Integer, total As Integer, wasCancelled As Boolean, errorOccurred As Boolean) As BulkSenderOutcome
        If wasCancelled Then Return BulkSenderOutcome.Cancelled
        If errorOccurred Then Return BulkSenderOutcome.Failed
        Return BulkSenderOutcome.Completed
    End Function
End Class
