Imports System.Net.Http
Imports System.Text.Json
Imports System.Threading.Tasks

Public Module AppLanguages
    Private ReadOnly SharedHttpClient As New HttpClient()

    Private ReadOnly RtlCodes As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "ar", "fa", "he", "iw", "ur", "yi", "ps", "sd", "ug", "syc"
    }

    Public Function IsRtl(code As String) As Boolean
        If String.IsNullOrEmpty(code) Then Return False
        Dim baseCode = code.Split("-"c)(0)
        Return RtlCodes.Contains(baseCode)
    End Function

    Public Async Function FetchSupportedLanguages() As Task(Of List(Of LanguageInfo))
        Dim langs As New List(Of LanguageInfo)()
        Try
            Dim url = "https://translate.googleapis.com/translate_a/l?client=gtx&hl=en"
            Dim response = Await SharedHttpClient.GetStringAsync(url)
            Using doc As JsonDocument = JsonDocument.Parse(response)
                Dim tlElement As JsonElement = Nothing
                If doc.RootElement.TryGetProperty("tl", tlElement) Then
                    For Each prop In tlElement.EnumerateObject()
                        langs.Add(New LanguageInfo With {
                            .Code = prop.Name,
                            .Name = prop.Value.ToString()
                        })
                    Next
                    langs.Sort(Function(a, b) a.Name.CompareTo(b.Name))
                End If
            End Using
        Catch ex As Exception
            Debug.WriteLine($"Failed to fetch supported languages: {ex.Message}")
        End Try
        Return langs
    End Function
End Module

Public Class AppLocalizations
    Private Shared ReadOnly SharedHttpClient As New HttpClient()
    Private ReadOnly _translations As Dictionary(Of String, String)

    Public Sub New(translations As Dictionary(Of String, String))
        _translations = If(translations, New Dictionary(Of String, String)())
    End Sub

    Public Shared ReadOnly EnStrings As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
        {"settings", "Settings"},
        {"theme", "Theme"},
        {"system", "System"},
        {"light", "Light"},
        {"dark", "Dark"},
        {"match_cohesive", "Match this setting in WhatsApp for a cohesive look."},
        {"manage_accounts", "Manage Accounts"},
        {"add_account", "Add account"},
        {"always_show_tab_bar", "Always show tab bar"},
        {"updates", "Updates"},
        {"check_updates_launch", "Check for updates on launch"},
        {"check_now", "Check Now"},
        {"devtools", "DevTools"},
        {"debug_active_tab", "Debug active tab"},
        {"delete_account_title", "Delete Account"},
        {"delete_account_confirm", "Delete ""{name}""? This will remove all data for this account."},
        {"delete_account_last", "You cannot delete the last active account."},
        {"cancel", "Cancel"},
        {"delete", "Delete"},
        {"rename", "Rename"},
        {"language", "Language"},
        {"translate_to_lang", "Translate to {lang}"},
        {"translate_all_messages", "Translate all messages"},
        {"toggle_window", "Toggle Window"},
        {"exit", "Exit"},
        {"translate_message_button", "Translate message button "},
        {"keep_app_in_english", "Keep app UI in English"},
        {"full_page_translation", "Translate entire page"},
        {"show_translate_all_messages_button", "Title bar translate all messages button"},
        {"reload_active_tab", "Reload active tab"},
        {"use_beta_channel", "Use beta update channel"},
        {"notifications", "Notifications"},
        {"show_message_popup", "Show message popup"}
    }

    Public Function [Get](key As String, Optional args As Dictionary(Of String, String) = Nothing) As String
        Dim value As String = Nothing
        If Not _translations.TryGetValue(key, value) Then
            If Not EnStrings.TryGetValue(key, value) Then
                value = key
            End If
        End If

        If args IsNot Nothing Then
            For Each kvp In args
                value = value.Replace("{" & kvp.Key & "}", kvp.Value)
            Next
        End If
        Return value
    End Function

    Public Shared Async Function FetchTranslations(targetLang As String) As Task(Of Dictionary(Of String, String))
        Dim translated As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        If targetLang = "en" Then
            For Each kvp In EnStrings
                translated(kvp.Key) = kvp.Value
            Next
            Return translated
        End If

        Dim tasks = EnStrings.Select(Function(entry) TranslateOneAsync(SharedHttpClient, entry.Key, entry.Value, targetLang)).ToArray()
        Dim results = Await Task.WhenAll(tasks)
        For Each kvp In results
            translated(kvp.Key) = kvp.Value
        Next

        Return translated
    End Function

    Private Shared Async Function TranslateOneAsync(client As HttpClient, key As String, value As String, targetLang As String) As Task(Of KeyValuePair(Of String, String))
        Try
            If key = "delete_account_confirm" Then
                Dim textToTranslate = value.Replace("{name}", "___")
                Dim translatedText = Await TranslateTextInternal(client, textToTranslate, targetLang)
                Return New KeyValuePair(Of String, String)(key, translatedText.Replace("___", "{name}"))
            ElseIf key = "translate_to_lang" Then
                Dim translatedPrefix = Await TranslateTextInternal(client, "Translate to", targetLang)
                Return New KeyValuePair(Of String, String)(key, translatedPrefix & " {lang}")
            Else
                Dim translatedText = Await TranslateTextInternal(client, value, targetLang)
                Return New KeyValuePair(Of String, String)(key, translatedText)
            End If
        Catch ex As Exception
            Debug.WriteLine($"Failed to translate key '{key}': {ex.Message}")
            Return New KeyValuePair(Of String, String)(key, value)
        End Try
    End Function

    Public Shared Async Function TranslateSingle(text As String, targetLang As String) As Task(Of String)
        Return Await TranslateTextInternal(SharedHttpClient, text, targetLang)
    End Function

    Private Shared Async Function TranslateTextInternal(client As HttpClient, text As String, targetLang As String) As Task(Of String)
        If String.IsNullOrEmpty(text) Then Return text
        Try
            Dim encodedText = Uri.EscapeDataString(text)
            Dim url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={targetLang}&dt=t&q={encodedText}"
            
            Dim response = Await client.GetStringAsync(url)
            Using doc As JsonDocument = JsonDocument.Parse(response)
                Dim root = doc.RootElement
                If root.ValueKind = JsonValueKind.Array AndAlso root.GetArrayLength() > 0 Then
                    Dim firstArray = root(0)
                    If firstArray.ValueKind = JsonValueKind.Array Then
                        Dim sb As New Text.StringBuilder()
                        For Each part In firstArray.EnumerateArray()
                            If part.ValueKind = JsonValueKind.Array AndAlso part.GetArrayLength() > 0 Then
                                sb.Append(part(0).GetString())
                            End If
                        Next
                        Return sb.ToString()
                    End If
                End If
            End Using
        Catch ex As Exception
            Debug.WriteLine($"Translation error for ""{text}"": {ex.Message}")
        End Try
        Return text
    End Function
    Public Shared ReadOnly ItStrings As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
        {"settings", "Impostazioni"},
        {"theme", "Tema"},
        {"system", "Sistema"},
        {"light", "Chiaro"},
        {"dark", "Scuro"},
        {"match_cohesive", "Abbina questa impostazione in WhatsApp per un aspetto coerente."},
        {"manage_accounts", "Gestione Account"},
        {"add_account", "Aggiungi account"},
        {"always_show_tab_bar", "Mostra sempre la barra delle schede"},
        {"updates", "Aggiornamenti"},
        {"check_updates_launch", "Controlla aggiornamenti all'avvio"},
        {"check_now", "Verifica ora"},
        {"devtools", "Strumenti sviluppatore"},
        {"debug_active_tab", "Debug scheda attiva"},
        {"delete_account_title", "Elimina Account"},
        {"delete_account_confirm", "Eliminare ""{name}""? Tutti i dati di questo account verranno rimossi."},
        {"delete_account_last", "Non puoi eliminare l'unico account attivo."},
        {"cancel", "Annulla"},
        {"delete", "Elimina"},
        {"rename", "Rinomina"},
        {"language", "Lingua"},
        {"translate_to_lang", "Traduci in {lang}"},
        {"translate_all_messages", "Traduci tutti i messaggi"},
        {"toggle_window", "Mostra/Nascondi finestra"},
        {"exit", "Esci"},
        {"translate_message_button", "Pulsante traduzione messaggio"},
        {"keep_app_in_english", "Mantieni interfaccia in Inglese"},
        {"full_page_translation", "Traduci l'intera pagina"},
        {"show_translate_all_messages_button", "Pulsante traduci tutti i messaggi nella barra"},
        {"reload_active_tab", "Ricarica scheda attiva"},
        {"use_beta_channel", "Usa canale aggiornamenti beta"},
        {"notifications", "Notifiche"},
        {"show_message_popup", "Mostra popup messaggio"}
    }
End Class

Public Class LanguageInfo
    Public Property Name As String
    Public Property Code As String
End Class
