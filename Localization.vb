Imports System.IO
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks

''' <summary>
''' Modulo helper per la gestione del supporto multilingua e l'interrogazione dell'API di traduzione Google Translate.
''' </summary>
Public Module AppLanguages
    Private ReadOnly SharedHttpClient As New HttpClient()

    Private ReadOnly RtlCodes As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "ar", "fa", "he", "iw", "ur", "yi", "ps", "sd", "ug", "syc"
    }

    ''' <summary>
    ''' Restituisce true se il codice lingua specificato utilizza l'orientamento di scrittura da destra a sinistra (RTL).
    ''' </summary>
    Public Function IsRtl(code As String) As Boolean
        If String.IsNullOrEmpty(code) Then Return False
        Dim baseCode = code.Split("-"c)(0)
        Return RtlCodes.Contains(baseCode)
    End Function

    ''' <summary>
    ''' Recupera l'elenco delle lingue supportate interrogando l'API REST di Google Translate.
    ''' </summary>
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

''' <summary>
''' Servizio singleton per la gestione della cache delle traduzioni (UI e messaggi chat) persistente su file disco.
''' Ottimizzato per mantenere in RAM ESCLUSIVAMENTE i dizionari della lingua attualmente attiva/impostata.
''' </summary>
Public Class TranslationCacheService
    Private Shared ReadOnly _instance As New TranslationCacheService()
    Public Shared ReadOnly Property Instance As TranslationCacheService
        Get
            Return _instance
        End Get
    End Property

    Private ReadOnly _httpClient As New HttpClient()
    Private ReadOnly _cacheLock As New Object()
    
    Private _activeLanguage As String = "en"
    Private _activeUiTranslations As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
    Private _activeMessageTranslations As New Dictionary(Of String, String)(StringComparer.Ordinal)
    
    Private _dirty As Boolean = False
    Private _flushCts As CancellationTokenSource = Nothing

    Private Sub New()
    End Sub

    ''' <summary>Percorso del file JSON per la persistenza delle traduzioni su disco.</summary>
    Public Shared ReadOnly Property CacheFilePath As String
        Get
            Dim dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "translations_cache.json")
            If File.Exists(dataPath) Then Return dataPath

            Dim rootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translations_cache.json")
            If File.Exists(rootPath) Then Return rootPath

            Dim dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data")
            If Not Directory.Exists(dataDir) Then
                Try
                    Directory.CreateDirectory(dataDir)
                Catch
                End Try
            End If
            Return dataPath
        End Get
    End Property

    ''' <summary>
    ''' Imposta la lingua attiva, scarica su disco eventuali modifiche della lingua precedente,
    ''' rilascia dalla memoria RAM i dizionari non utilizzati e carica solo quelli della nuova lingua.
    ''' </summary>
    Public Async Function SetActiveLanguageAsync(langCode As String) As Task
        If String.IsNullOrWhiteSpace(langCode) Then langCode = "en"

        ' Flush immediato se c'erano modifiche pendenti sulla vecchia lingua
        If _dirty Then
            Await FlushCacheImmediateAsync()
        End If

        SyncLock _cacheLock
            _activeLanguage = langCode.ToLowerInvariant()
            _activeUiTranslations.Clear()
            _activeMessageTranslations.Clear()
        End SyncLock

        ' Carica i dati per la lingua attiva dal file persistente
        Await LoadActiveLanguageFromDiskAsync(_activeLanguage)

        ' Integra i valori UI predefiniti
        SyncLock _cacheLock
            If _activeLanguage = "it" Then
                For Each kvp In AppLocalizations.ItStrings
                    If Not _activeUiTranslations.ContainsKey(kvp.Key) Then
                        _activeUiTranslations(kvp.Key) = kvp.Value
                    End If
                Next
            ElseIf _activeLanguage = "en" Then
                For Each kvp In AppLocalizations.EnStrings
                    If Not _activeUiTranslations.ContainsKey(kvp.Key) Then
                        _activeUiTranslations(kvp.Key) = kvp.Value
                    End If
                Next
            End If
        End SyncLock
    End Function

    ''' <summary>
    ''' Restituisce un'istanza di AppLocalizations configurata con le traduzioni correnti in RAM.
    ''' </summary>
    Public Function GetActiveLocalizations() As AppLocalizations
        SyncLock _cacheLock
            Return New AppLocalizations(_activeUiTranslations)
        End SyncLock
    End Function

    Private Async Function LoadActiveLanguageFromDiskAsync(langCode As String) As Task
        Dim filePath = CacheFilePath
        If Not File.Exists(filePath) Then Return

        Try
            Dim jsonContent = Await File.ReadAllTextAsync(filePath)
            If String.IsNullOrWhiteSpace(jsonContent) Then Return

            Using doc As JsonDocument = JsonDocument.Parse(jsonContent)
                Dim root = doc.RootElement

                SyncLock _cacheLock
                    ' 1. Lettura traduzioni UI ("ui" -> langCode)
                    Dim uiElement As JsonElement = Nothing
                    If root.TryGetProperty("ui", uiElement) AndAlso uiElement.ValueKind = JsonValueKind.Object Then
                        Dim langUiElement As JsonElement = Nothing
                        If uiElement.TryGetProperty(langCode, langUiElement) AndAlso langUiElement.ValueKind = JsonValueKind.Object Then
                            For Each prop In langUiElement.EnumerateObject()
                                _activeUiTranslations(prop.Name) = prop.Value.GetString()
                            Next
                        End If
                    End If

                    ' 2. Retrocompatibilità: "cached_translations" -> langCode
                    Dim cachedElement As JsonElement = Nothing
                    If root.TryGetProperty("cached_translations", cachedElement) AndAlso cachedElement.ValueKind = JsonValueKind.Object Then
                        Dim langCachedElement As JsonElement = Nothing
                        If cachedElement.TryGetProperty(langCode, langCachedElement) AndAlso langCachedElement.ValueKind = JsonValueKind.Object Then
                            For Each prop In langCachedElement.EnumerateObject()
                                If Not _activeUiTranslations.ContainsKey(prop.Name) Then
                                    _activeUiTranslations(prop.Name) = prop.Value.GetString()
                                End If
                            Next
                        End If
                    End If

                    ' 3. Lettura messaggi chat/DOM tradotti ("messages" -> langCode)
                    Dim msgElement As JsonElement = Nothing
                    If root.TryGetProperty("messages", msgElement) AndAlso msgElement.ValueKind = JsonValueKind.Object Then
                        Dim langMsgElement As JsonElement = Nothing
                        If msgElement.TryGetProperty(langCode, langMsgElement) AndAlso langMsgElement.ValueKind = JsonValueKind.Object Then
                            For Each prop In langMsgElement.EnumerateObject()
                                _activeMessageTranslations(prop.Name) = prop.Value.GetString()
                            Next
                        End If
                    End If
                End SyncLock
            End Using
        Catch ex As Exception
            Debug.WriteLine($"[TranslationCacheService] Error reading cache from disk: {ex.Message}")
        End Try
    End Function

    ''' <summary>
    ''' Recupera una traduzione messaggio memorizzata in cache se presente.
    ''' </summary>
    Public Function TryGetCachedMessage(text As String, targetLang As String, ByRef translatedText As String) As Boolean
        If String.IsNullOrEmpty(text) Then
            translatedText = text
            Return True
        End If

        SyncLock _cacheLock
            If String.Equals(targetLang, _activeLanguage, StringComparison.OrdinalIgnoreCase) Then
                Return _activeMessageTranslations.TryGetValue(text, translatedText)
            End If
        End SyncLock

        translatedText = Nothing
        Return False
    End Function

    ''' <summary>
    ''' Salva una traduzione messaggio nella cache e pianifica la persistenza su disco con debounce.
    ''' </summary>
    Public Sub CacheMessage(text As String, translatedText As String, targetLang As String)
        If String.IsNullOrEmpty(text) OrElse String.IsNullOrEmpty(translatedText) Then Return

        SyncLock _cacheLock
            If String.Equals(targetLang, _activeLanguage, StringComparison.OrdinalIgnoreCase) Then
                _activeMessageTranslations(text) = translatedText
                _dirty = True
            End If
        End SyncLock

        If _dirty Then
            ScheduleFlushDebounced()
        End If
    End Sub

    ''' <summary>
    ''' Salva più traduzioni messaggi in batch nella cache in un'unica operazione.
    ''' </summary>
    Public Sub CacheMessagesBatch(items As IEnumerable(Of KeyValuePair(Of String, String)), targetLang As String)
        SyncLock _cacheLock
            If String.Equals(targetLang, _activeLanguage, StringComparison.OrdinalIgnoreCase) Then
                For Each item In items
                    If Not String.IsNullOrEmpty(item.Key) AndAlso Not String.IsNullOrEmpty(item.Value) Then
                        _activeMessageTranslations(item.Key) = item.Value
                        _dirty = True
                    End If
                Next
            End If
        End SyncLock

        If _dirty Then
            ScheduleFlushDebounced()
        End If
    End Sub

    Private Sub ScheduleFlushDebounced()
        If _flushCts IsNot Nothing Then
            Try
                _flushCts.Cancel()
                _flushCts.Dispose()
            Catch
            End Try
        End If

        _flushCts = New CancellationTokenSource()
        Dim token = _flushCts.Token

        Dim ignore = Task.Run(Async Function()
            Try
                Await Task.Delay(500, token)
                If token.IsCancellationRequested Then Return
                Await FlushCacheImmediateAsync()
            Catch ex As OperationCanceledException
                ' Operazione annullata per nuovo arrivo dati
            Catch ex As Exception
                Debug.WriteLine($"[TranslationCacheService] Debounced flush error: {ex.Message}")
            End Try
        End Function)
    End Sub

    ''' <summary>
    ''' Scrive immediatamente su file disco lo stato della cache unificandolo ai dati già esistenti.
    ''' </summary>
    Public Async Function FlushCacheImmediateAsync() As Task
        Dim currentLang As String
        Dim uiCopy As Dictionary(Of String, String)
        Dim msgCopy As Dictionary(Of String, String)

        SyncLock _cacheLock
            If Not _dirty Then Return
            _dirty = False
            currentLang = _activeLanguage
            uiCopy = New Dictionary(Of String, String)(_activeUiTranslations)
            msgCopy = New Dictionary(Of String, String)(_activeMessageTranslations)
        End SyncLock

        Dim filePath = CacheFilePath
        Try
            ' Carica l'intero file esistente per non sovrascrivere altre lingue memorizzate su disco
            Dim fullData As New Dictionary(Of String, Object)()
            Dim allUi As New Dictionary(Of String, Dictionary(Of String, String))(StringComparer.OrdinalIgnoreCase)
            Dim allMessages As New Dictionary(Of String, Dictionary(Of String, String))(StringComparer.OrdinalIgnoreCase)

            If File.Exists(filePath) Then
                Try
                    Dim existingJson = Await File.ReadAllTextAsync(filePath)
                    If Not String.IsNullOrWhiteSpace(existingJson) Then
                        Using doc As JsonDocument = JsonDocument.Parse(existingJson)
                            Dim root = doc.RootElement

                            Dim uiEl As JsonElement = Nothing
                            If root.TryGetProperty("ui", uiEl) AndAlso uiEl.ValueKind = JsonValueKind.Object Then
                                For Each langProp In uiEl.EnumerateObject()
                                    Dim dict As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                                    For Each item In langProp.Value.EnumerateObject()
                                        dict(item.Name) = item.Value.GetString()
                                    Next
                                    allUi(langProp.Name) = dict
                                Next
                            End If

                            Dim msgEl As JsonElement = Nothing
                            If root.TryGetProperty("messages", msgEl) AndAlso msgEl.ValueKind = JsonValueKind.Object Then
                                For Each langProp In msgEl.EnumerateObject()
                                    Dim dict As New Dictionary(Of String, String)(StringComparer.Ordinal)
                                    For Each item In langProp.Value.EnumerateObject()
                                        dict(item.Name) = item.Value.GetString()
                                    Next
                                    allMessages(langProp.Name) = dict
                                Next
                            End If
                        End Using
                    End If
                Catch ex As Exception
                    Debug.WriteLine($"[TranslationCacheService] Read before write warning: {ex.Message}")
                End Try
            End If

            ' Aggiorna la sezione della lingua attiva
            allUi(currentLang) = uiCopy
            allMessages(currentLang) = msgCopy

            fullData("ui") = allUi
            fullData("messages") = allMessages
            ' Mantiene retrocompatibilità con cached_translations
            fullData("cached_translations") = allUi

            Dim options As New JsonSerializerOptions With {
                .WriteIndented = True
            }
            Dim serialized = JsonSerializer.Serialize(fullData, options)
            Await File.WriteAllTextAsync(filePath, serialized)
        Catch ex As Exception
            Debug.WriteLine($"[TranslationCacheService] Failed to save cache file: {ex.Message}")
        End Try
    End Function

    ''' <summary>
    ''' Traduce un singolo testo interrogando preventivamente la cache su disco/RAM prima di effettuare chiamate HTTP.
    ''' </summary>
    Public Async Function TranslateSingleAsync(text As String, targetLang As String) As Task(Of String)
        If String.IsNullOrEmpty(text) Then Return text

        Dim cached As String = Nothing
        If TryGetCachedMessage(text, targetLang, cached) AndAlso Not String.IsNullOrEmpty(cached) Then
            Return cached
        End If

        Dim translated = Await TranslateTextHttpAsync(text, targetLang)
        If Not String.IsNullOrEmpty(translated) AndAlso translated <> text Then
            CacheMessage(text, translated, targetLang)
        End If
        Return translated
    End Function

    ''' <summary>
    ''' Traduce un elenco di testi estraendo ed inviando a Google Translate solo le frasi non ancora in cache.
    ''' </summary>
    Public Async Function TranslateBatchAsync(texts As List(Of String), targetLang As String) As Task(Of List(Of String))
        If texts Is Nothing OrElse texts.Count = 0 Then Return New List(Of String)()

        Dim results As New List(Of String)(New String(texts.Count - 1) {})
        Dim missingIndices As New List(Of Integer)()
        Dim missingTexts As New List(Of String)()

        For i As Integer = 0 To texts.Count - 1
            Dim t = texts(i)
            Dim cached As String = Nothing
            If TryGetCachedMessage(t, targetLang, cached) AndAlso Not String.IsNullOrEmpty(cached) Then
                results(i) = cached
            Else
                missingIndices.Add(i)
                missingTexts.Add(t)
            End If
        Next

        ' Se tutti i testi erano già memorizzati in cache, ritorna immediatamente senza accessi di rete!
        If missingTexts.Count = 0 Then
            Return results
        End If

        Try
            Dim combinedMissing = String.Join(vbLf & "###" & vbLf, missingTexts)
            Dim httpResult = Await TranslateTextHttpAsync(combinedMissing, targetLang)
            
            Dim parts = httpResult.Split(New String() {vbLf & "###" & vbLf, vbLf & " ###" & vbLf, vbLf & "### " & vbLf}, StringSplitOptions.None)
            Dim newCacheEntries As New List(Of KeyValuePair(Of String, String))()

            For j As Integer = 0 To missingIndices.Count - 1
                Dim origIndex = missingIndices(j)
                Dim translatedPart = If(j < parts.Length AndAlso Not String.IsNullOrEmpty(parts(j)), parts(j), missingTexts(j))
                results(origIndex) = translatedPart
                newCacheEntries.Add(New KeyValuePair(Of String, String)(missingTexts(j), translatedPart))
            Next

            CacheMessagesBatch(newCacheEntries, targetLang)
        Catch ex As Exception
            Debug.WriteLine($"[TranslationCacheService] Batch translation HTTP error: {ex.Message}")
            For j As Integer = 0 To missingIndices.Count - 1
                results(missingIndices(j)) = missingTexts(j)
            Next
        End Try

        Return results
    End Function

    Private Async Function TranslateTextHttpAsync(text As String, targetLang As String) As Task(Of String)
        If String.IsNullOrEmpty(text) Then Return text
        Try
            Dim encodedText = Uri.EscapeDataString(text)
            Dim url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={targetLang}&dt=t&q={encodedText}"
            
            Dim response = Await _httpClient.GetStringAsync(url)
            Using doc As JsonDocument = JsonDocument.Parse(response)
                Dim root = doc.RootElement
                If root.ValueKind = JsonValueKind.Array AndAlso root.GetArrayLength() > 0 Then
                    Dim firstArray = root(0)
                    If firstArray.ValueKind = JsonValueKind.Array Then
                        Dim sb As New StringBuilder()
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
            Debug.WriteLine($"[TranslationCacheService] HTTP Translation error for ""{text}"": {ex.Message}")
        End Try
        Return text
    End Function
End Class

''' <summary>
''' Gestisce i dizionari di localizzazione dell'interfaccia utente (Inglese e Italiano) e il servizio di traduzione automatica.
''' </summary>
Public Class AppLocalizations
    Private ReadOnly _translations As Dictionary(Of String, String)

    Public Sub New(translations As Dictionary(Of String, String))
        _translations = If(translations, New Dictionary(Of String, String)())
    End Sub

    ''' <summary>Dizionario di localizzazione di fallback per l'interfaccia in lingua Inglese.</summary>
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
        {"show_message_popup", "Show message popup"},
        {"accounts_count_info", "Configured accounts: {count} of 3"},
        {"max_accounts_reached", "Maximum limit of 3 accounts reached."},
        {"about", "About"},
        {"about_title", "About WhatsappH"},
        {"app_description", "Portable, lightweight, multi-account WhatsApp Web desktop client for Windows."},
        {"author", "Author"},
        {"release_date", "Release Date"},
        {"license", "License"},
        {"runtime_environment", "Environment & Framework"},
        {"portable_directory", "Portable Data Path"},
        {"github_repository", "GitHub Repository"},
        {"report_issue", "Report Issue"},
        {"view_releases", "View Releases"},
        {"close", "Close"}
    }

    ''' <summary>Dizionario di localizzazione nativa per l'interfaccia in lingua Italiana.</summary>
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
        {"show_message_popup", "Mostra popup messaggio"},
        {"accounts_count_info", "Account configurati: {count} su 3"},
        {"max_accounts_reached", "Raggiunto il limite massimo di 3 account."},
        {"about", "Informazioni"},
        {"about_title", "Informazioni su WhatsappH"},
        {"app_description", "Client desktop WhatsApp Web portabile, leggero e multi-account per Windows."},
        {"author", "Autore"},
        {"release_date", "Data di rilascio"},
        {"license", "Licenza"},
        {"runtime_environment", "Ambiente e Framework"},
        {"portable_directory", "Percorso Dati Portabile"},
        {"github_repository", "Repository GitHub"},
        {"report_issue", "Segnala un problema"},
        {"view_releases", "Vedi Release"},
        {"close", "Chiudi"}
    }

    ''' <summary>
    ''' Restituisce la stringa localizzata per la chiave fornita, applicando eventuali argomenti di formattazione.
    ''' </summary>
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

    ''' <summary>
    ''' Traduce una singola stringa di testo nella lingua di destinazione specificata con cache persistente su file.
    ''' </summary>
    Public Shared Async Function TranslateSingle(text As String, targetLang As String) As Task(Of String)
        Return Await TranslationCacheService.Instance.TranslateSingleAsync(text, targetLang)
    End Function

    ''' <summary>
    ''' Traduce una lista di testi in batch con cache persistente su file (inviando solo le stringhe non ancora in cache).
    ''' </summary>
    Public Shared Async Function TranslateBatch(texts As List(Of String), targetLang As String) As Task(Of List(Of String))
        Return Await TranslationCacheService.Instance.TranslateBatchAsync(texts, targetLang)
    End Function
End Class

''' <summary>
''' Rappresenta le informazioni di una lingua supportata nell'interfaccia (Nome visualizzato e Codice ISO).
''' </summary>
Public Class LanguageInfo
    Public Property Name As String
    Public Property Code As String
End Class
