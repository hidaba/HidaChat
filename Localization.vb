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
            Dim dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data")
            Dim dataPath = Path.Combine(dataDir, "translations_cache.json")
            If File.Exists(dataPath) Then Return dataPath

            Dim rootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translations_cache.json")
            If File.Exists(rootPath) Then
                Try
                    If Not Directory.Exists(dataDir) Then Directory.CreateDirectory(dataDir)
                    File.Move(rootPath, dataPath)
                    Return dataPath
                Catch
                    Return rootPath
                End Try
            End If

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
            Dim sourceDict As Dictionary(Of String, String) = Nothing
            Select Case _activeLanguage
                Case "it"
                    sourceDict = AppLocalizations.ItStrings
                Case "fr"
                    sourceDict = AppLocalizations.FrStrings
                Case "es"
                    sourceDict = AppLocalizations.EsStrings
                Case "de"
                    sourceDict = AppLocalizations.DeStrings
                Case Else
                    sourceDict = AppLocalizations.EnStrings
            End Select

            If sourceDict IsNot Nothing Then
                For Each kvp In sourceDict
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

            Dim dirName = Path.GetDirectoryName(filePath)
            If Not String.IsNullOrEmpty(dirName) AndAlso Not Directory.Exists(dirName) Then
                Directory.CreateDirectory(dirName)
            End If

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
        {"match_cohesive", "Match this setting in your chat apps for a cohesive look."},
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
        {"accounts_count_info", "Configured accounts: {count} of {max}"},
        {"max_accounts_reached", "Maximum limit of {max} accounts reached."},
        {"max_accounts", "Max accounts:"},
        {"add_whatsapp_account", "Add WhatsApp account"},
        {"add_telegram_account", "Add Telegram account"},
        {"add_openclaw_account", "Add OpenClaw account"},
        {"openclaw_server_url", "Gateway / Tailscale URL:"},
        {"openclaw_auth_token", "Gateway Auth Token:"},
        {"test_connection", "Test Connection"},
        {"connection_testing", "Testing..."},
        {"connection_success", "✓ Online"},
        {"connection_failed", "✗ Unreachable"},
        {"select_platform", "Select platform"},
        {"about", "About"},
        {"about_title", "About HidaChat"},
        {"app_description", "Portable, lightweight, multi-account desktop client for Windows (WhatsApp, Telegram, Teams, etc.)."},
        {"author", "Author"},
        {"release_date", "Release Date"},
        {"license", "License"},
        {"runtime_environment", "Environment & Framework"},
        {"portable_directory", "Portable Data Path"},
        {"github_repository", "GitHub Repository"},
        {"report_issue", "Report Issue"},
        {"view_releases", "View Releases"},
        {"bulk_sender", "Excel / CSV Bulk Sender"},
        {"custom_css", "Custom CSS"},
        {"enable_custom_css", "Enable custom CSS rules"},
        {"custom_css_placeholder", "/* Write or paste your custom CSS for WhatsApp and Telegram Web here */"},
        {"apply_css", "Apply CSS"},
        {"css_applied", "CSS applied successfully!"},
        {"css_preset_oled", "OLED Pure Dark"},
        {"css_preset_compact", "Compact Layout"},
        {"css_preset_font", "Modern Font"},
        {"css_preset_reset", "Clear"},
        {"contact_online", "Online"},
        {"contact_typing", "typing..."},
        {"spellchecker", "Spellchecker"},
        {"enable_spellchecker", "Enable native spellchecker in chat inputs"},
        {"spellchecker_language", "Dictionary language"},
        {"spellchecker_lang_auto", "Automatic (follow app language)"},
        {"spellchecker_restart_hint", "Changes will apply when reloading tabs or restarting HidaChat."},
        {"dnd_mode", "Do Not Disturb (Focus Mode)"},
        {"dnd_enable", "Silence all notifications and sounds"},
        {"dnd_duration", "Duration"},
        {"dnd_30m", "For 30 minutes"},
        {"dnd_1h", "For 1 hour"},
        {"dnd_2h", "For 2 hours"},
        {"dnd_8h", "For 8 hours"},
        {"dnd_indefinite", "Until turned off"},
        {"dnd_active_until", "Do Not Disturb active until {time}"},
        {"dnd_active_indefinite", "Do Not Disturb active (indefinite)"},
        {"dnd_off", "Turn off Do Not Disturb"},
        {"close", "Close"}
    }

    ''' <summary>Dizionario di localizzazione nativa per l'interfaccia in lingua Italiana.</summary>
    Public Shared ReadOnly ItStrings As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
        {"settings", "Impostazioni"},
        {"theme", "Tema"},
        {"system", "Sistema"},
        {"light", "Chiaro"},
        {"dark", "Scuro"},
        {"match_cohesive", "Abbina questa impostazione nelle tue app per un aspetto coerente."},
        {"manage_accounts", "Gestione Account"},
        {"add_account", "Aggiungi account"},
        {"add_whatsapp_account", "Aggiungi account WhatsApp"},
        {"add_telegram_account", "Aggiungi account Telegram"},
        {"add_openclaw_account", "Aggiungi account OpenClaw"},
        {"openclaw_server_url", "URL Gateway / Tailscale:"},
        {"openclaw_auth_token", "Token Autenticazione Gateway:"},
        {"test_connection", "Test Connessione"},
        {"connection_testing", "Verifica in corso..."},
        {"connection_success", "✓ Online"},
        {"connection_failed", "✗ Non raggiungibile"},
        {"select_platform", "Seleziona piattaforma"},
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
        {"accounts_count_info", "Account configurati: {count} su {max}"},
        {"max_accounts_reached", "Raggiunto il limite massimo di {max} account."},
        {"max_accounts", "Account massimi:"},
        {"about", "Informazioni"},
        {"about_title", "Informazioni su HidaChat"},
        {"app_description", "Client desktop multi-account portabile e leggero per Windows (WhatsApp, Telegram, Teams, ecc.)."},
        {"author", "Autore"},
        {"release_date", "Data di rilascio"},
        {"license", "Licenza"},
        {"runtime_environment", "Ambiente e Framework"},
        {"portable_directory", "Percorso Dati Portabile"},
        {"github_repository", "Repository GitHub"},
        {"report_issue", "Segnala un problema"},
        {"view_releases", "Vedi Release"},
        {"bulk_sender", "Invio Massivo da Excel / CSV"},
        {"custom_css", "CSS Personalizzato"},
        {"enable_custom_css", "Abilita regole CSS personalizzate"},
        {"custom_css_placeholder", "/* Scrivi o incolla qui il tuo CSS personalizzato per WhatsApp e Telegram Web */"},
        {"apply_css", "Applica CSS"},
        {"css_applied", "CSS applicato con successo!"},
        {"css_preset_oled", "OLED Nero Puro"},
        {"css_preset_compact", "Layout Compatto"},
        {"css_preset_font", "Font Moderno"},
        {"css_preset_reset", "Svuota"},
        {"contact_online", "In linea"},
        {"contact_typing", "sta scrivendo..."},
        {"spellchecker", "Correttore Ortografico"},
        {"enable_spellchecker", "Abilita correttore ortografico nei campi di testo"},
        {"spellchecker_language", "Lingua del dizionario"},
        {"spellchecker_lang_auto", "Automatica (segui lingua app)"},
        {"spellchecker_restart_hint", "Le modifiche saranno applicate ricaricando le schede o riavviando HidaChat."},
        {"dnd_mode", "Modalità Non Disturbare"},
        {"dnd_enable", "Silenzia tutte le notifiche e i suoni"},
        {"dnd_duration", "Durata"},
        {"dnd_30m", "Per 30 minuti"},
        {"dnd_1h", "Per 1 ora"},
        {"dnd_2h", "Per 2 ore"},
        {"dnd_8h", "Per 8 ore"},
        {"dnd_indefinite", "Fino a disattivazione manuale"},
        {"dnd_active_until", "Non Disturbare attivo fino alle {time}"},
        {"dnd_active_indefinite", "Non Disturbare attivo (indefinito)"},
        {"dnd_off", "Disattiva Non Disturbare"},
        {"close", "Chiudi"}
    }

    ''' <summary>Dizionario di localizzazione nativa per l'interfaccia in lingua Francese.</summary>
    Public Shared ReadOnly FrStrings As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
        {"settings", "Paramètres"},
        {"theme", "Thème"},
        {"system", "Système"},
        {"light", "Clair"},
        {"dark", "Sombre"},
        {"match_cohesive", "Harmonisez ce paramètre dans vos applications pour un aspect cohérent."},
        {"manage_accounts", "Gestion des comptes"},
        {"add_account", "Ajouter un compte"},
        {"add_whatsapp_account", "Ajouter un compte WhatsApp"},
        {"add_telegram_account", "Ajouter un compte Telegram"},
        {"add_openclaw_account", "Ajouter un compte OpenClaw"},
        {"openclaw_server_url", "URL Passerelle / Tailscale :"},
        {"openclaw_auth_token", "Jeton d'authentification Passerelle :"},
        {"test_connection", "Tester la connexion"},
        {"connection_testing", "Vérification..."},
        {"connection_success", "✓ En ligne"},
        {"connection_failed", "✗ Inaccessible"},
        {"select_platform", "Sélectionner la plateforme"},
        {"always_show_tab_bar", "Toujours afficher la barre des onglets"},
        {"updates", "Mises à jour"},
        {"check_updates_launch", "Rechercher les mises à jour au démarrage"},
        {"check_now", "Vérifier maintenant"},
        {"devtools", "Outils de développement"},
        {"debug_active_tab", "Déboguer l'onglet actif"},
        {"delete_account_title", "Supprimer le compte"},
        {"delete_account_confirm", "Supprimer ""{name}"" ? Toutes les données de ce compte seront supprimées."},
        {"delete_account_last", "Vous ne pouvez pas supprimer le seul compte actif."},
        {"cancel", "Annuler"},
        {"delete", "Supprimer"},
        {"rename", "Renommer"},
        {"language", "Langue"},
        {"translate_to_lang", "Traduire en {lang}"},
        {"translate_all_messages", "Traduire tous les messages"},
        {"toggle_window", "Afficher / Masquer la fenêtre"},
        {"exit", "Quitter"},
        {"translate_message_button", "Bouton de traduction du message"},
        {"keep_app_in_english", "Conserver l'interface en anglais"},
        {"full_page_translation", "Traduire toute la page"},
        {"show_translate_all_messages_button", "Bouton de traduction de tous les messages dans la barre"},
        {"reload_active_tab", "Recharger l'onglet actif"},
        {"use_beta_channel", "Utiliser le canal de mise à jour bêta"},
        {"notifications", "Notifications"},
        {"show_message_popup", "Afficher la notification popup"},
        {"accounts_count_info", "Comptes configurés : {count} sur {max}"},
        {"max_accounts_reached", "Limite maximale de {max} comptes atteinte."},
        {"max_accounts", "Comptes maximum :"},
        {"about", "À propos"},
        {"about_title", "À propos de HidaChat"},
        {"app_description", "Client de bureau multi-comptes léger et portable pour Windows (WhatsApp, Telegram, Teams, etc.)."},
        {"author", "Auteur"},
        {"release_date", "Date de version"},
        {"license", "Licence"},
        {"runtime_environment", "Environnement et Framework"},
        {"portable_directory", "Répertoire de données portable"},
        {"github_repository", "Dépôt GitHub"},
        {"report_issue", "Signaler un problème"},
        {"view_releases", "Voir les versions"},
        {"bulk_sender", "Envoi en masse Excel / CSV"},
        {"custom_css", "CSS Personnalisé"},
        {"enable_custom_css", "Activer les règles CSS personnalisées"},
        {"custom_css_placeholder", "/* Écrivez ou collez ici votre CSS personnalisé pour WhatsApp et Telegram Web */"},
        {"apply_css", "Appliquer le CSS"},
        {"css_applied", "CSS appliqué avec succès !"},
        {"css_preset_oled", "OLED Noir Pur"},
        {"css_preset_compact", "Disposition compacte"},
        {"css_preset_font", "Police moderne"},
        {"css_preset_reset", "Effacer"},
        {"contact_online", "En ligne"},
        {"contact_typing", "écrit..."},
        {"spellchecker", "Correcteur orthographique"},
        {"enable_spellchecker", "Activer le correcteur orthographique dans les champs de saisie"},
        {"spellchecker_language", "Langue du dictionnaire"},
        {"spellchecker_lang_auto", "Automatique (suivre la langue de l'app)"},
        {"spellchecker_restart_hint", "Les modifications s'appliqueront après le rechargement des onglets ou le redémarrage de HidaChat."},
        {"dnd_mode", "Mode Ne pas déranger"},
        {"dnd_enable", "Désactiver toutes les notifications et les sons"},
        {"dnd_duration", "Durée"},
        {"dnd_30m", "Pendant 30 minutes"},
        {"dnd_1h", "Pendant 1 heure"},
        {"dnd_2h", "Pendant 2 heures"},
        {"dnd_8h", "Pendant 8 heures"},
        {"dnd_indefinite", "Jusqu'à désactivation"},
        {"dnd_active_until", "Ne pas déranger actif jusqu'à {time}"},
        {"dnd_active_indefinite", "Ne pas déranger actif (indéfini)"},
        {"dnd_off", "Désactiver Ne pas déranger"},
        {"close", "Fermer"}
    }

    ''' <summary>Dizionario di localizzazione nativa per l'interfaccia in lingua Spagnola.</summary>
    Public Shared ReadOnly EsStrings As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
        {"settings", "Ajustes"},
        {"theme", "Tema"},
        {"system", "Sistema"},
        {"light", "Claro"},
        {"dark", "Oscuro"},
        {"match_cohesive", "Haga coincidir este ajuste en sus aplicaciones para una apariencia coherente."},
        {"manage_accounts", "Administrar Cuentas"},
        {"add_account", "Añadir cuenta"},
        {"add_whatsapp_account", "Añadir cuenta de WhatsApp"},
        {"add_telegram_account", "Añadir cuenta de Telegram"},
        {"add_openclaw_account", "Añadir cuenta de OpenClaw"},
        {"openclaw_server_url", "URL Gateway / Tailscale:"},
        {"openclaw_auth_token", "Token de Autenticación Gateway:"},
        {"test_connection", "Probar Conexión"},
        {"connection_testing", "Comprobando..."},
        {"connection_success", "✓ En línea"},
        {"connection_failed", "✗ No accesible"},
        {"select_platform", "Seleccionar plataforma"},
        {"always_show_tab_bar", "Mostrar siempre la barra de pestañas"},
        {"updates", "Actualizaciones"},
        {"check_updates_launch", "Buscar actualizaciones al iniciar"},
        {"check_now", "Comprobar ahora"},
        {"devtools", "Herramientas de desarrollo"},
        {"debug_active_tab", "Depurar pestaña activa"},
        {"delete_account_title", "Eliminar Cuenta"},
        {"delete_account_confirm", "¿Eliminar ""{name}""? Todos los datos de esta cuenta se eliminarán."},
        {"delete_account_last", "No puede eliminar la única cuenta activa."},
        {"cancel", "Cancelar"},
        {"delete", "Eliminar"},
        {"rename", "Renombrar"},
        {"language", "Idioma"},
        {"translate_to_lang", "Traducir a {lang}"},
        {"translate_all_messages", "Traducir todos los mensajes"},
        {"toggle_window", "Mostrar/Ocultar ventana"},
        {"exit", "Salir"},
        {"translate_message_button", "Botón de traducción de mensaje"},
        {"keep_app_in_english", "Mantener la interfaz en inglés"},
        {"full_page_translation", "Traducir toda la página"},
        {"show_translate_all_messages_button", "Botón de traducir todos los mensajes en la barra"},
        {"reload_active_tab", "Recargar pestaña activa"},
        {"use_beta_channel", "Usar canal de actualizaciones beta"},
        {"notifications", "Notificaciones"},
        {"show_message_popup", "Mostrar ventana emergente de mensaje"},
        {"accounts_count_info", "Cuentas configuradas: {count} de {max}"},
        {"max_accounts_reached", "Límite máximo de {max} cuentas alcanzado."},
        {"max_accounts", "Cuentas máximas:"},
        {"about", "Acerca de"},
        {"about_title", "Acerca de HidaChat"},
        {"app_description", "Cliente de escritorio multicuenta ligero y portátil para Windows (WhatsApp, Telegram, Teams, etc.)."},
        {"author", "Autor"},
        {"release_date", "Fecha de lanzamiento"},
        {"license", "Licencia"},
        {"runtime_environment", "Entorno y Framework"},
        {"portable_directory", "Ruta de datos portátil"},
        {"github_repository", "Repositorio de GitHub"},
        {"report_issue", "Informar de un problema"},
        {"view_releases", "Ver versiones"},
        {"bulk_sender", "Envío Masivo desde Excel / CSV"},
        {"custom_css", "CSS Personalizado"},
        {"enable_custom_css", "Habilitar reglas CSS personalizadas"},
        {"custom_css_placeholder", "/* Escriba o pegue aquí su CSS personalizado para WhatsApp y Telegram Web */"},
        {"apply_css", "Aplicar CSS"},
        {"css_applied", "¡CSS aplicado con éxito!"},
        {"css_preset_oled", "OLED Negro Puro"},
        {"css_preset_compact", "Diseño Compacto"},
        {"css_preset_font", "Fuente Moderna"},
        {"css_preset_reset", "Borrar"},
        {"contact_online", "En línea"},
        {"contact_typing", "escribiendo..."},
        {"spellchecker", "Corrector ortográfico"},
        {"enable_spellchecker", "Activar corrector ortográfico en los campos de entrada"},
        {"spellchecker_language", "Idioma del diccionario"},
        {"spellchecker_lang_auto", "Automática (seguir idioma de la app)"},
        {"spellchecker_restart_hint", "Los cambios se aplicarán al recargar las pestañas o reiniciar HidaChat."},
        {"dnd_mode", "Modo No Molestar"},
        {"dnd_enable", "Silenciar todas las notificaciones y sonidos"},
        {"dnd_duration", "Duración"},
        {"dnd_30m", "Durante 30 minutos"},
        {"dnd_1h", "Durante 1 hora"},
        {"dnd_2h", "Durante 2 horas"},
        {"dnd_8h", "Durante 8 horas"},
        {"dnd_indefinite", "Hasta que se desactive"},
        {"dnd_active_until", "No Molestar activo hasta las {time}"},
        {"dnd_active_indefinite", "No Molestar activo (indefinido)"},
        {"dnd_off", "Desactivar No Molestar"},
        {"close", "Cerrar"}
    }

    ''' <summary>Dizionario di localizzazione nativa per l'interfaccia in lingua Tedesca.</summary>
    Public Shared ReadOnly DeStrings As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
        {"settings", "Einstellungen"},
        {"theme", "Design"},
        {"system", "System"},
        {"light", "Hell"},
        {"dark", "Dunkel"},
        {"match_cohesive", "Passen Sie diese Einstellung in Ihren Chat-Apps für ein einheitliches Erscheinungsbild an."},
        {"manage_accounts", "Konten verwalten"},
        {"add_account", "Konto hinzufügen"},
        {"add_whatsapp_account", "WhatsApp-Konto hinzufügen"},
        {"add_telegram_account", "Telegram-Konto hinzufügen"},
        {"add_openclaw_account", "OpenClaw-Konto hinzufügen"},
        {"openclaw_server_url", "Gateway- / Tailscale-URL:"},
        {"openclaw_auth_token", "Gateway-Authentifizierungstoken:"},
        {"test_connection", "Verbindung testen"},
        {"connection_testing", "Prüfung läuft..."},
        {"connection_success", "✓ Online"},
        {"connection_failed", "✗ Nicht erreichbar"},
        {"select_platform", "Plattform auswählen"},
        {"always_show_tab_bar", "Registerkartenleiste immer anzeigen"},
        {"updates", "Aktualisierungen"},
        {"check_updates_launch", "Beim Start nach Aktualisierungen suchen"},
        {"check_now", "Jetzt prüfen"},
        {"devtools", "Entwicklertools"},
        {"debug_active_tab", "Aktiven Tab debuggen"},
        {"delete_account_title", "Konto löschen"},
        {"delete_account_confirm", """{name}"" löschen? Alle Daten dieses Kontos werden entfernt."},
        {"delete_account_last", "Sie können das einzige aktive Konto nicht löschen."},
        {"cancel", "Abbrechen"},
        {"delete", "Löschen"},
        {"rename", "Umbenennen"},
        {"language", "Sprache"},
        {"translate_to_lang", "Auf {lang} übersetzen"},
        {"translate_all_messages", "Alle Nachrichten übersetzen"},
        {"toggle_window", "Fenster ein-/ausblenden"},
        {"exit", "Beenden"},
        {"translate_message_button", "Schaltfläche zur Nachrichtenübersetzung"},
        {"keep_app_in_english", "App-Oberfläche auf Englisch behalten"},
        {"full_page_translation", "Ganze Seite übersetzen"},
        {"show_translate_all_messages_button", "Schaltfläche 'Alle Nachrichten übersetzen' in Titelleiste"},
        {"reload_active_tab", "Aktiven Tab neu laden"},
        {"use_beta_channel", "Beta-Update-Kanal verwenden"},
        {"notifications", "Benachrichtigungen"},
        {"show_message_popup", "Nachrichten-Popup anzeigen"},
        {"accounts_count_info", "Konfigurierte Konten: {count} von {max}"},
        {"max_accounts_reached", "Maximallimit von {max} Konten erreicht."},
        {"max_accounts", "Maximale Konten:"},
        {"about", "Über"},
        {"about_title", "Über HidaChat"},
        {"app_description", "Portabler, schlanker Multi-Account-Desktop-Client für Windows (WhatsApp, Telegram, Teams usw.)."},
        {"author", "Autor"},
        {"release_date", "Veröffentlichungsdatum"},
        {"license", "Lizenz"},
        {"runtime_environment", "Umgebung und Framework"},
        {"portable_directory", "Portabler Datenpfad"},
        {"github_repository", "GitHub-Repository"},
        {"report_issue", "Problem melden"},
        {"view_releases", "Releases anzeigen"},
        {"bulk_sender", "Massenversand via Excel / CSV"},
        {"custom_css", "Benutzerdefiniertes CSS"},
        {"enable_custom_css", "Benutzerdefinierte CSS-Regeln aktivieren"},
        {"custom_css_placeholder", "/* Schreiben oder fügen Sie hier Ihr benutzerdefiniertes CSS für WhatsApp und Telegram Web ein */"},
        {"apply_css", "CSS anwenden"},
        {"css_applied", "CSS erfolgreich angewendet!"},
        {"css_preset_oled", "OLED Tiefschwarz"},
        {"css_preset_compact", "Kompaktes Layout"},
        {"css_preset_font", "Moderne Schriftart"},
        {"css_preset_reset", "Leeren"},
        {"contact_online", "Online"},
        {"contact_typing", "schreibt..."},
        {"spellchecker", "Rechtschreibprüfung"},
        {"enable_spellchecker", "Rechtschreibprüfung in Texteingabefeldern aktivieren"},
        {"spellchecker_language", "Wörterbuchsprache"},
        {"spellchecker_lang_auto", "Automatisch (App-Sprache folgen)"},
        {"spellchecker_restart_hint", "Änderungen werden beim Neuladen der Registerkarten oder beim Neustart von HidaChat wirksam."},
        {"dnd_mode", "Nicht stören (Fokusmodus)"},
        {"dnd_enable", "Alle Benachrichtigungen und Töne stummschalten"},
        {"dnd_duration", "Dauer"},
        {"dnd_30m", "Für 30 Minuten"},
        {"dnd_1h", "Für 1 Stunde"},
        {"dnd_2h", "Für 2 Stunden"},
        {"dnd_8h", "Für 8 Stunden"},
        {"dnd_indefinite", "Bis zur Deaktivierung"},
        {"dnd_active_until", "Nicht stören aktiv bis {time}"},
        {"dnd_active_indefinite", "Nicht stören aktiv (unbegrenzt)"},
        {"dnd_off", "Nicht stören deaktivieren"},
        {"close", "Schließen"}
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
