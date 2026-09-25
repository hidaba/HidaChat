Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports System.ComponentModel
Imports System.Runtime.CompilerServices
Imports System.Threading
Imports System.Security.Cryptography
Imports Microsoft.Win32


''' <summary>

''' Controller responsabile della gestione delle impostazioni utente, 
''' persistenza su file JSON con meccanismo di debounce e gestione della localizzazione.
''' </summary>
Public Class SettingsController
    Implements INotifyPropertyChanged

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private Sub NotifyPropertyChanged(<CallerMemberName> Optional propertyName As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

    Private _cachedSettings As Dictionary(Of String, Object) = Nothing
    Private _dirty As Boolean = False
    Private ReadOnly _ioLock As New SemaphoreSlim(1, 1)
    Private _lastFlushTask As Task = Task.CompletedTask
    Private _flushCts As CancellationTokenSource = Nothing
    Private _isLoadedFromValidConfig As Boolean = False

    ''' <summary>
    ''' Indica se le impostazioni correnti sono state caricate con successo da un file di configurazione valido o dal relativo backup (.bak).
    ''' Restituisce False se è stato effettuato un fallback ai valori predefiniti per assenza o corruzione del file.
    ''' </summary>
    Public ReadOnly Property IsLoadedFromValidConfig As Boolean
        Get
            Return _isLoadedFromValidConfig
        End Get
    End Property

    ' --- Impostazioni tema ---
    Private _theme As String = "System"
    ''' <summary>Modalità tema dell'applicazione ("System", "Light", "Dark").</summary>
    Public Property Theme As String
        Get
            Return _theme
        End Get
        Set(value As String)
            If _theme <> value Then
                _theme = value
                NotifyPropertyChanged()
                NotifyPropertyChanged(NameOf(IsDarkThemeEffective))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Indica se il tema scuro è attualmente attivo in base alla configurazione ("Dark", "Light" o "System").
    ''' </summary>
    Public ReadOnly Property IsDarkThemeEffective As Boolean
        Get
            If _theme = "Dark" Then
                Return True
            ElseIf _theme = "System" Then
                Return SystemThemeHelper.IsSystemDarkTheme()
            Else
                Return False
            End If
        End Get
    End Property


    ' --- Barra schede sempre visibile ---
    Private _alwaysShowTabBar As Boolean = True
    ''' <summary>Indica se mantenere sempre visibile la barra delle schede degli account.</summary>
    Public Property AlwaysShowTabBar As Boolean
        Get
            Return _alwaysShowTabBar
        End Get
        Set(value As Boolean)
            If _alwaysShowTabBar <> value Then
                _alwaysShowTabBar = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    ' --- Controllo aggiornamenti all'avvio ---
    Private _checkForUpdates As Boolean = True
    ''' <summary>Indica se verificare automaticamente la presenza di aggiornamenti all'avvio.</summary>
    Public Property CheckForUpdates As Boolean
        Get
            Return _checkForUpdates
        End Get
        Set(value As Boolean)
            If _checkForUpdates <> value Then
                _checkForUpdates = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    ' --- Canale aggiornamenti beta ---
    Private _useBetaChannel As Boolean = False
    ''' <summary>Indica se utilizzare il canale di aggiornamenti Beta.</summary>
    Public Property UseBetaChannel As Boolean
        Get
            Return _useBetaChannel
        End Get
        Set(value As Boolean)
            If _useBetaChannel <> value Then
                _useBetaChannel = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    ' --- Pulsante traduci al passaggio del mouse ---
    Private _translateMessageButton As Boolean = True
    ''' <summary>Indica se mostrare il pulsante di traduzione rapida al passaggio del mouse sui messaggi.</summary>
    Public Property TranslateMessageButton As Boolean
        Get
            Return _translateMessageButton
        End Get
        Set(value As Boolean)
            If _translateMessageButton <> value Then
                _translateMessageButton = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    Private _fullPageTranslation As Boolean = False
    ''' <summary>Indica se attivare la traduzione automatica dell'intera pagina.</summary>
    Public Property FullPageTranslation As Boolean
        Get
            Return _fullPageTranslation
        End Get
        Set(value As Boolean)
            If _fullPageTranslation <> value Then
                _fullPageTranslation = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    Private _showTranslateAllMessagesButton As Boolean = True
    ''' <summary>Indica se mostrare il pulsante "Traduci tutti i messaggi" nella barra del titolo.</summary>
    Public Property ShowTranslateAllMessagesButton As Boolean
        Get
            Return _showTranslateAllMessagesButton
        End Get
        Set(value As Boolean)
            If _showTranslateAllMessagesButton <> value Then
                _showTranslateAllMessagesButton = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    Private _showMessagePopup As Boolean = True
    ''' <summary>Indica se mostrare il popup personalizzato in basso a destra per le nuove notifiche.</summary>
    Public Property ShowMessagePopup As Boolean
        Get
            Return _showMessagePopup
        End Get
        Set(value As Boolean)
            If _showMessagePopup <> value Then
                _showMessagePopup = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    ' --- Temi CSS Personalizzati (TODO #43) ---
    Private _enableCustomCss As Boolean = False
    ''' <summary>Indica se abilitare l'iniezione delle regole CSS personalizzate dell'utente.</summary>
    Public Property EnableCustomCss As Boolean
        Get
            Return _enableCustomCss
        End Get
        Set(value As Boolean)
            If _enableCustomCss <> value Then
                _enableCustomCss = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    Private _customCss As String = ""
    ''' <summary>Regole CSS personalizzate inserite dall'utente.</summary>
    Public Property CustomCss As String
        Get
            Return _customCss
        End Get
        Set(value As String)
            If _customCss <> value Then
                _customCss = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    Private _tsnetAuthKey As String = ""
    ''' <summary>Chiave di autenticazione (AuthKey) opzionale per il nodo Tailscale embedded.</summary>
    Public Property TsnetAuthKey As String
        Get
            Return _tsnetAuthKey
        End Get
        Set(value As String)
            Dim cleanVal = If(value, String.Empty).Trim()
            If _tsnetAuthKey <> cleanVal Then
                _tsnetAuthKey = cleanVal
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    Private _suppressNetworkDriveWarning As Boolean = False
    ''' <summary>Indica se l'utente ha scelto di sopprimere l'avviso relativo all'esecuzione da unità di rete.</summary>
    Public Property SuppressNetworkDriveWarning As Boolean
        Get
            Return _suppressNetworkDriveWarning
        End Get
        Set(value As Boolean)
            If _suppressNetworkDriveWarning <> value Then
                _suppressNetworkDriveWarning = value
                NotifyPropertyChanged()
                If _cachedSettings IsNot Nothing Then
                    _cachedSettings("suppressNetworkDriveWarning") = value
                    _dirty = True
                    Dim ignore = FlushAfterDebounceAsync()
                End If
            End If
        End Set
    End Property

    ' --- Correttore Ortografico (TODO #44) ---
    Private _enableSpellcheck As Boolean = True
    ''' <summary>Indica se abilitare il correttore ortografico nativo WebView2/Chromium.</summary>
    Public Property EnableSpellcheck As Boolean
        Get
            Return _enableSpellcheck
        End Get
        Set(value As Boolean)
            If _enableSpellcheck <> value Then
                _enableSpellcheck = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    Private _spellcheckLanguage As String = "auto"
    ''' <summary>Codice lingua del dizionario del correttore ("auto", "it", "en", "fr", "es", "de").</summary>
    Public Property SpellcheckLanguage As String
        Get
            Return _spellcheckLanguage
        End Get
        Set(value As String)
            If _spellcheckLanguage <> value Then
                _spellcheckLanguage = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    ''' <summary>
    ''' Restituisce il codice lingua Chromium effettivo (es. "it-IT", "en-US", "fr-FR", "es-ES", "de-DE") 
    ''' per il browser e il correttore ortografico.
    ''' </summary>
    Public Function GetEffectiveChromiumLanguage() As String
        Dim targetLang = If(String.IsNullOrWhiteSpace(SpellcheckLanguage) OrElse SpellcheckLanguage = "auto", Language, SpellcheckLanguage)
        If String.IsNullOrWhiteSpace(targetLang) Then targetLang = "en"
        
        Select Case targetLang.ToLowerInvariant()
            Case "it"
                Return "it-IT"
            Case "en"
                Return "en-US"
            Case "fr"
                Return "fr-FR"
            Case "es"
                Return "es-ES"
            Case "de"
                Return "de-DE"
            Case Else
                Return targetLang
        End Select
    End Function

    ' --- Limite Massimo Account ---
    Private _maxAccounts As Integer = 5
    ''' <summary>Numero massimo di account configurabili contemporaneamente (default: 5, min: 2, max: 10).</summary>
    Public Property MaxAccounts As Integer
        Get
            Return _maxAccounts
        End Get
        Set(value As Integer)
            Dim clamped = Math.Clamp(value, 2, 10)
            If _maxAccounts <> clamped Then
                _maxAccounts = clamped
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    ' --- Modalità Non Disturbare / Focus Mode (TODO #47) ---
    Private _isDndEnabled As Boolean = False
    ''' <summary>Indica se la modalità Non Disturbare è stata attivata manualmente o tramite timer.</summary>
    Public Property IsDndEnabled As Boolean
        Get
            Return _isDndEnabled
        End Get
        Set(value As Boolean)
            If _isDndEnabled <> value Then
                _isDndEnabled = value
                NotifyPropertyChanged()
                NotifyPropertyChanged(NameOf(IsDndActive))
            End If
        End Set
    End Property

    Private _dndUntil As Nullable(Of DateTime) = Nothing
    ''' <summary>Timestamp UTC di scadenza della modalità Non Disturbare (Nothing se indefinita o disattivata).</summary>
    Public Property DndUntil As Nullable(Of DateTime)
        Get
            Return _dndUntil
        End Get
        Set(value As Nullable(Of DateTime))
            If _dndUntil <> value Then
                _dndUntil = value
                NotifyPropertyChanged()
                NotifyPropertyChanged(NameOf(IsDndActive))
            End If
        End Set
    End Property

    Private _dndDurationMode As String = "off"
    ''' <summary>Modalità di durata ("off", "30m", "1h", "2h", "8h", "indefinite").</summary>
    Public Property DndDurationMode As String
        Get
            Return _dndDurationMode
        End Get
        Set(value As String)
            If _dndDurationMode <> value Then
                _dndDurationMode = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    ''' <summary>Indica se la modalità Non Disturbare è attualmente attiva ed efficace.</summary>
    Public ReadOnly Property IsDndActive As Boolean
        Get
            If Not _isDndEnabled Then Return False
            If _dndUntil.HasValue Then
                Return DateTime.UtcNow < _dndUntil.Value
            End If
            Return True
        End Get
    End Property

    ''' <summary>Restituisce la descrizione testuale localizzata dello stato Non Disturbare.</summary>
    Public Function GetDndStatusText(loc As AppLocalizations) As String
        If loc Is Nothing Then Return "Do Not Disturb"
        If Not IsDndActive Then
            Return loc.Get("dnd_mode")
        End If
        If _dndUntil.HasValue Then
            Dim localTime = _dndUntil.Value.ToLocalTime()
            Return loc.Get("dnd_active_until", New Dictionary(Of String, String) From {{"time", localTime.ToString("HH:mm")}})
        Else
            Return loc.Get("dnd_active_indefinite")
        End If
    End Function

    Private _language As String = "en"
    ''' <summary>Codice della lingua attualmente selezionata dall'utente (es. "en", "it").</summary>
    Public Property Language As String
        Get
            Return _language
        End Get
        Set(value As String)
            If _language <> value Then
                _language = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    Private _localizations As New AppLocalizations(AppLocalizations.EnStrings)
    ''' <summary>Istanza contenente le stringhe di testo tradotte per l'interfaccia utente.</summary>
    Public Property Localizations As AppLocalizations
        Get
            Return _localizations
        End Get
        Set(value As AppLocalizations)
            _localizations = value
            NotifyPropertyChanged()
        End Set
    End Property

    Private _supportedLanguages As New List(Of LanguageInfo) From {
        New LanguageInfo With {.Name = "English", .Code = "en"},
        New LanguageInfo With {.Name = "Italiano", .Code = "it"},
        New LanguageInfo With {.Name = "Français", .Code = "fr"},
        New LanguageInfo With {.Name = "Español", .Code = "es"},
        New LanguageInfo With {.Name = "Deutsch", .Code = "de"}
    }
    ''' <summary>Lista delle lingue ufficialmente supportate dall'interfaccia.</summary>
    Public Property SupportedLanguages As List(Of LanguageInfo)
        Get
            Return _supportedLanguages
        End Get
        Set(value As List(Of LanguageInfo))
            _supportedLanguages = value
            NotifyPropertyChanged()
        End Set
    End Property

    Private _isTranslating As Boolean = False
    Public Property IsTranslating As Boolean
        Get
            Return _isTranslating
        End Get
        Set(value As Boolean)
            If _isTranslating <> value Then
                _isTranslating = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    ' --- File JSON delle impostazioni ---
    Private ReadOnly Property SettingsFile As String
        Get
            Dim dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data")
            Dim dataPath = Path.Combine(dataDir, "settings.json")
            If File.Exists(dataPath) Then Return dataPath

            Dim rootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json")
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

    ''' <summary>Legge il file settings.json dal disco con protezione atomica, validazione JSON e ripristino da backup (.bak) in caso di corruzione.</summary>
    Public Async Function ReadSettingsAsync() As Task(Of Dictionary(Of String, Object))
        If _cachedSettings IsNot Nothing Then Return _cachedSettings

        Await _ioLock.WaitAsync()
        Try
            If _cachedSettings IsNot Nothing Then Return _cachedSettings

            Dim targetFile = SettingsFile
            Dim bakFile = targetFile & ".bak"

            If Not File.Exists(targetFile) Then
                ' Se il file principale non esiste ma esiste il file di backup, tenta il ripristino
                If File.Exists(bakFile) Then
                    Try
                        Dim bakText = Await File.ReadAllTextAsync(bakFile)
                        If Not String.IsNullOrWhiteSpace(bakText) Then
                            Dim restoredFromBak = JsonSerializer.Deserialize(Of Dictionary(Of String, Object))(bakText)
                            If restoredFromBak IsNot Nothing AndAlso restoredFromBak.Count > 0 Then
                                File.Copy(bakFile, targetFile, overwrite:=True)
                                _cachedSettings = restoredFromBak
                                _isLoadedFromValidConfig = True
                                Debug.WriteLine("ReadSettingsAsync: file principale assente, ripristinato da backup .bak")
                                Return _cachedSettings
                            End If
                        End If
                    Catch exBakInit As Exception
                        Debug.WriteLine($"ReadSettingsAsync: errore ripristino da backup iniziale: {exBakInit.Message}")
                    End Try
                End If

                _isLoadedFromValidConfig = False
                _cachedSettings = New Dictionary(Of String, Object)()
                Return _cachedSettings
            End If

            Dim needsQuarantine = False

            Try
                Dim contents = Await File.ReadAllTextAsync(targetFile)
                If String.IsNullOrWhiteSpace(contents) Then
                    Dim fi As New FileInfo(targetFile)
                    If fi.Length = 0 Then
                        needsQuarantine = True
                    Else
                        _isLoadedFromValidConfig = False
                        _cachedSettings = New Dictionary(Of String, Object)()
                        Return _cachedSettings
                    End If
                Else
                    _cachedSettings = JsonSerializer.Deserialize(Of Dictionary(Of String, Object))(contents)
                    If _cachedSettings Is Nothing Then
                        needsQuarantine = True
                    Else
                        _isLoadedFromValidConfig = True
                        Return _cachedSettings
                    End If
                End If
            Catch ex As Exception
                needsQuarantine = True
                Debug.WriteLine($"ReadSettingsAsync: JSON non valido o errore lettura in '{targetFile}': {ex.Message}")
            End Try

            If needsQuarantine Then
                Dim timeStamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
                Dim targetDir = Path.GetDirectoryName(targetFile)
                Dim corruptPath = Path.Combine(targetDir, $"settings.corrupt-{timeStamp}.json")
                Try
                    If File.Exists(targetFile) Then
                        File.Move(targetFile, corruptPath, overwrite:=True)
                        Debug.WriteLine($"ReadSettingsAsync: file corrotto archiviato in '{corruptPath}'")
                    End If
                Catch exMove As Exception
                    Debug.WriteLine($"ReadSettingsAsync: impossibile archiviare file corrotto: {exMove.Message}")
                End Try

                ' Tentativo di ripristino dal file di backup .bak
                If File.Exists(bakFile) Then
                    Try
                        Dim bakContents = Await File.ReadAllTextAsync(bakFile)
                        If Not String.IsNullOrWhiteSpace(bakContents) Then
                            Dim restored = JsonSerializer.Deserialize(Of Dictionary(Of String, Object))(bakContents)
                            If restored IsNot Nothing AndAlso restored.Count > 0 Then
                                Try
                                    File.Copy(bakFile, targetFile, overwrite:=True)
                                Catch
                                End Try
                                _cachedSettings = restored
                                _isLoadedFromValidConfig = True
                                Debug.WriteLine("ReadSettingsAsync: ripristino completato con successo da .bak")
                                NotifySettingsRecoveredFromBackup(corruptPath)
                                Return _cachedSettings
                            End If
                        End If
                    Catch exBak As Exception
                        Debug.WriteLine($"ReadSettingsAsync: anche il file .bak non è utilizzabile: {exBak.Message}")
                    End Try
                End If

                ' Fallback estremo a dizionario vuoto con avviso esplicito all'utente
                _isLoadedFromValidConfig = False
                _cachedSettings = New Dictionary(Of String, Object)()
                NotifySettingsCorruptedAndReset(corruptPath)
                Return _cachedSettings
            End If

            If _cachedSettings Is Nothing Then
                _isLoadedFromValidConfig = False
                _cachedSettings = New Dictionary(Of String, Object)()
            End If
            Return _cachedSettings
        Finally
            _ioLock.Release()
        End Try
    End Function

    Private Sub NotifySettingsRecoveredFromBackup(corruptPath As String)
        Try
            Dim fileName = Path.GetFileName(corruptPath)
            Application.Current?.Dispatcher?.BeginInvoke(Sub()
                Try
                    Dim loc = Localizations
                    Dim title = If(loc IsNot Nothing, loc.Get("settings_corrupt_recovery_title"), "Ripristino Impostazioni")
                    Dim msg = If(loc IsNot Nothing,
                        loc.Get("settings_corrupt_recovered_msg", New Dictionary(Of String, String) From {{"file", fileName}}),
                        $"Il file di configurazione 'settings.json' risultava danneggiato ed è stato archiviato come '{fileName}'." & vbCrLf & vbCrLf &
                        "Le impostazioni e gli account sono stati ripristinati con successo dall'ultimo backup (.bak) disponibile.")
                    MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Warning)
                Catch
                End Try
            End Sub)
        Catch
        End Try
    End Sub

    Private Sub NotifySettingsCorruptedAndReset(corruptPath As String)
        Try
            Dim fileName = Path.GetFileName(corruptPath)
            Application.Current?.Dispatcher?.BeginInvoke(Sub()
                Try
                    Dim loc = Localizations
                    Dim title = If(loc IsNot Nothing, loc.Get("settings_corrupt_reset_title"), "Errore Configurazione")
                    Dim msg = If(loc IsNot Nothing,
                        loc.Get("settings_corrupt_reset_msg", New Dictionary(Of String, String) From {{"file", fileName}}),
                        $"Il file di configurazione 'settings.json' risultava danneggiato ed è stato archiviato come '{fileName}'." & vbCrLf & vbCrLf &
                        "Non è stato possibile recuperare un backup valido: l'applicazione è stata avviata con le impostazioni predefinite.")
                    MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error)
                Catch
                End Try
            End Sub)
        Catch
        End Try
    End Sub

    ''' <summary>Scrive immediatamente il dizionario delle impostazioni su file JSON in modo atomico e serializzato.</summary>
    Public Async Function WriteSettingsAsync(settings As Dictionary(Of String, Object)) As Task
        _cachedSettings = settings
        _dirty = False
        If _flushCts IsNot Nothing Then
            Try
                _flushCts.Cancel()
                _flushCts.Dispose()
            Catch
            End Try
            _flushCts = Nothing
        End If

        Dim writeTask = WriteSettingsInternalAsync(settings)
        _lastFlushTask = writeTask
        Await writeTask
    End Function

    Private Async Function WriteSettingsInternalAsync(settings As Dictionary(Of String, Object)) As Task
        Await _ioLock.WaitAsync()
        Dim targetFile = SettingsFile
        Dim tmp = targetFile & ".tmp"
        Dim bak = targetFile & ".bak"
        Try
            Dim targetDir = Path.GetDirectoryName(targetFile)
            If Not String.IsNullOrEmpty(targetDir) AndAlso Not Directory.Exists(targetDir) Then
                Directory.CreateDirectory(targetDir)
            End If

            Dim options As New JsonSerializerOptions With {
                .WriteIndented = True
            }
            Dim contents = JsonSerializer.Serialize(settings, options)

            Await File.WriteAllTextAsync(tmp, contents)

            If File.Exists(targetFile) Then
                Try
                    File.Replace(tmp, targetFile, bak)
                Catch exReplace As Exception
                    Debug.WriteLine($"File.Replace non riuscito, fallback con Copy+Move: {exReplace.Message}")
                    Try
                        File.Copy(targetFile, bak, overwrite:=True)
                    Catch
                    End Try
                    File.Move(tmp, targetFile, overwrite:=True)
                End Try
            Else
                File.Move(tmp, targetFile)
            End If
        Catch ex As Exception
            Debug.WriteLine($"Failed to write settings: {ex.Message}")
        Finally
            Try
                If File.Exists(tmp) Then File.Delete(tmp)
            Catch
            End Try
            _ioLock.Release()
        End Try
    End Function

    ''' <summary>Scrittura differita delle impostazioni (debounce di 500ms) per evitare accessi disco troppo frequenti.</summary>
    Private Function FlushAfterDebounceAsync() As Task
        If _flushCts IsNot Nothing Then
            Try
                _flushCts.Cancel()
                _flushCts.Dispose()
            Catch
            End Try
        End If
        _flushCts = New CancellationTokenSource()
        Dim token = _flushCts.Token

        Dim debounceTask As Task = Task.Run(Async Function() As Task
            Try
                Await Task.Delay(500, token)
            Catch ex As OperationCanceledException
                Return
            End Try
            If _dirty AndAlso _cachedSettings IsNot Nothing Then
                _dirty = False
                Await WriteSettingsAsync(_cachedSettings)
            End If
        End Function)

        _lastFlushTask = debounceTask
        Return debounceTask
    End Function

    ''' <summary>
    ''' Forza l'immediata scrittura su disco delle impostazioni se ci sono modifiche in sospeso (_dirty), 
    ''' annullando il debounce ed attendendo il completamento delle operazioni di I/O.
    ''' </summary>
    Public Async Function FlushNowAsync() As Task
        If _flushCts IsNot Nothing Then
            Try
                _flushCts.Cancel()
                _flushCts.Dispose()
            Catch
            End Try
            _flushCts = Nothing
        End If

        Try
            If _lastFlushTask IsNot Nothing Then
                Await _lastFlushTask.ConfigureAwait(False)
            End If
        Catch
        End Try

        If _dirty AndAlso _cachedSettings IsNot Nothing Then
            _dirty = False
            Await WriteSettingsAsync(_cachedSettings).ConfigureAwait(False)
        Else
            Await _ioLock.WaitAsync().ConfigureAwait(False)
            _ioLock.Release()
        End If
    End Function

    ''' <summary>Carica tutte le opzioni dal file JSON e la cache delle traduzioni all'avvio dell'applicazione.</summary>
    Public Async Function LoadSettingsAsync() As Task
        Dim settings = Await ReadSettingsAsync()
        
        If settings.ContainsKey("theme") Then
            Dim tStr = settings("theme").ToString()
            If tStr.Contains("light") Then
                _theme = "Light"
            ElseIf tStr.Contains("dark") Then
                _theme = "Dark"
            Else
                _theme = "System"
            End If
        Else
            _theme = "System"
        End If

        _alwaysShowTabBar = GetBoolSetting(settings, "alwaysShowTabBar", True)
        _checkForUpdates = GetBoolSetting(settings, "checkForUpdates", True)
        _useBetaChannel = GetBoolSetting(settings, "useBetaChannel", False)
        _translateMessageButton = GetBoolSetting(settings, "translateMessageButton", GetBoolSetting(settings, "enableHoverTranslation", True))
        _fullPageTranslation = GetBoolSetting(settings, "fullPageTranslation", GetBoolSetting(settings, "enableFullPageTranslation", False))
        _showTranslateAllMessagesButton = GetBoolSetting(settings, "showTranslateAllMessagesButton", True)
        _showMessagePopup = GetBoolSetting(settings, "showMessagePopup", True)
        _enableCustomCss = GetBoolSetting(settings, "enableCustomCss", False)
        _enableSpellcheck = GetBoolSetting(settings, "enableSpellcheck", True)
        _suppressNetworkDriveWarning = GetBoolSetting(settings, "suppressNetworkDriveWarning", False)

        _isDndEnabled = GetBoolSetting(settings, "isDndEnabled", False)
        If settings.ContainsKey("dndDurationMode") AndAlso settings("dndDurationMode") IsNot Nothing Then
            _dndDurationMode = settings("dndDurationMode").ToString()
        Else
            _dndDurationMode = If(_isDndEnabled, "indefinite", "off")
        End If
        If settings.ContainsKey("dndUntil") AndAlso settings("dndUntil") IsNot Nothing Then
            Dim untilStr = settings("dndUntil").ToString()
            Dim parsedDate As DateTime
            If DateTime.TryParse(untilStr, Nothing, Globalization.DateTimeStyles.RoundtripKind, parsedDate) Then
                _dndUntil = parsedDate.ToUniversalTime()
                If DateTime.UtcNow >= _dndUntil.Value Then
                    _isDndEnabled = False
                    _dndUntil = Nothing
                    _dndDurationMode = "off"
                End If
            Else
                _dndUntil = Nothing
            End If
        Else
            _dndUntil = Nothing
        End If

        If settings.ContainsKey("spellcheckLanguage") Then
            _spellcheckLanguage = settings("spellcheckLanguage").ToString()
        Else
            _spellcheckLanguage = "auto"
        End If

        If settings.ContainsKey("customCss") Then
            _customCss = settings("customCss").ToString()
        Else
            _customCss = ""
        End If

        If settings.ContainsKey("tsnetAuthKey") Then
            Dim enc = settings("tsnetAuthKey")?.ToString()
            If Not String.IsNullOrEmpty(enc) Then
                Try
                    Dim protectedBytes = Convert.FromBase64String(enc)
                    Dim bytes = ProtectedData.Unprotect(protectedBytes, Nothing, DataProtectionScope.CurrentUser)
                    _tsnetAuthKey = Encoding.UTF8.GetString(bytes)
                Catch
                    _tsnetAuthKey = ""
                End Try
            Else
                _tsnetAuthKey = ""
            End If
        Else
            _tsnetAuthKey = ""
        End If

        If settings.ContainsKey("maxAccounts") Then
            Try
                _maxAccounts = Math.Clamp(Convert.ToInt32(settings("maxAccounts").ToString()), 2, 10)
            Catch
                _maxAccounts = 5
            End Try
        Else
            _maxAccounts = 5
        End If
        
        If settings.ContainsKey("language") Then
            _language = settings("language").ToString()
        Else
            _language = "en"
        End If

        ' Inizializza la lingua attiva mantenendo in RAM solo le traduzioni pertinenti
        Await TranslationCacheService.Instance.SetActiveLanguageAsync(_language)
        Localizations = TranslationCacheService.Instance.GetActiveLocalizations()

        NotifyAllPropertiesChanged()
    End Function

    Private Function GetBoolSetting(settings As Dictionary(Of String, Object), key As String, defaultVal As Boolean) As Boolean
        If settings.ContainsKey(key) Then
            Dim obj = settings(key)
            If TypeOf obj Is JsonElement Then
                Dim element = CType(obj, JsonElement)
                If element.ValueKind = JsonValueKind.True Then Return True
                If element.ValueKind = JsonValueKind.False Then Return False
            End If
            Return Convert.ToBoolean(obj.ToString())
        End If
        Return defaultVal
    End Function

    ''' <summary>Aggiorna la lingua selezionata, carica la lingua in memoria e persiste su disco.</summary>
    Public Async Function UpdateLanguageAsync(newLanguage As String) As Task
        If _language = newLanguage Then Return
        _language = newLanguage
        NotifyPropertyChanged(NameOf(Language))

        If _cachedSettings Is Nothing Then Await ReadSettingsAsync()
        _cachedSettings("language") = newLanguage
        _dirty = True
        Dim ignore = FlushAfterDebounceAsync()

        Await TranslationCacheService.Instance.SetActiveLanguageAsync(newLanguage)
        Localizations = TranslationCacheService.Instance.GetActiveLocalizations()
    End Function

    ''' <summary>Aggiorna il tema selezionato ("System", "Light", "Dark") e lo memorizza su disco.</summary>
    Public Async Function SaveThemeAsync(newTheme As String) As Task
        _theme = newTheme
        NotifyPropertyChanged(NameOf(Theme))
        If _cachedSettings Is Nothing Then Await ReadSettingsAsync()
        _cachedSettings("theme") = "ThemeMode." & newTheme.ToLower()
        _dirty = True
        Dim ignore = FlushAfterDebounceAsync()
    End Function

    ''' <summary>Salva una singola chiave/valore arbitrario di configurazione.</summary>
    Public Async Function SaveSettingAsync(key As String, value As Object) As Task
        If _cachedSettings Is Nothing Then Await ReadSettingsAsync()
        _cachedSettings(key) = value
        _dirty = True
        Dim ignore = FlushAfterDebounceAsync()
    End Function

    ''' <summary>Salva e persiste il numero massimo di account consentiti.</summary>
    Public Async Function SaveMaxAccountsAsync(maxAcc As Integer) As Task
        MaxAccounts = maxAcc
        If _cachedSettings Is Nothing Then Await ReadSettingsAsync()
        _cachedSettings("maxAccounts") = _maxAccounts
        _dirty = True
        Dim ignore = FlushAfterDebounceAsync()
    End Function

    ''' <summary>Salva e persiste le impostazioni del CSS personalizzato (TODO #43).</summary>
    Public Async Function SaveCustomCssAsync(enabled As Boolean, css As String) As Task
        _enableCustomCss = enabled
        _customCss = If(css, String.Empty)
        NotifyPropertyChanged(NameOf(EnableCustomCss))
        NotifyPropertyChanged(NameOf(CustomCss))
        If _cachedSettings Is Nothing Then Await ReadSettingsAsync()
        _cachedSettings("enableCustomCss") = enabled
        _cachedSettings("customCss") = _customCss
        _dirty = True
        Dim ignore = FlushAfterDebounceAsync()
    End Function

    ''' <summary>Salva e persiste le impostazioni del correttore ortografico (TODO #44).</summary>
    Public Async Function SaveSpellcheckSettingsAsync(enabled As Boolean, spellLang As String) As Task
        _enableSpellcheck = enabled
        _spellcheckLanguage = If(String.IsNullOrWhiteSpace(spellLang), "auto", spellLang)
        NotifyPropertyChanged(NameOf(EnableSpellcheck))
        NotifyPropertyChanged(NameOf(SpellcheckLanguage))
        If _cachedSettings Is Nothing Then Await ReadSettingsAsync()
        _cachedSettings("enableSpellcheck") = enabled
        _cachedSettings("spellcheckLanguage") = _spellcheckLanguage
        _dirty = True
        Dim ignore = FlushAfterDebounceAsync()
    End Function

    ''' <summary>Salva e cifra via DPAPI la chiave Tailscale AuthKey opzionale per il nodo tsnet.</summary>
    Public Async Function SaveTsnetAuthKeyAsync(authKey As String) As Task
        TsnetAuthKey = If(authKey, String.Empty).Trim()
        Dim encryptedBase64 = ""
        If Not String.IsNullOrEmpty(_tsnetAuthKey) Then
            Try
                Dim bytes = Encoding.UTF8.GetBytes(_tsnetAuthKey)
                Dim protectedBytes = ProtectedData.Protect(bytes, Nothing, DataProtectionScope.CurrentUser)
                encryptedBase64 = Convert.ToBase64String(protectedBytes)
            Catch ex As Exception
                Debug.WriteLine($"Error encrypting tsnetAuthKey: {ex.Message}")
            End Try
        End If

        If _cachedSettings Is Nothing Then Await ReadSettingsAsync()
        _cachedSettings("tsnetAuthKey") = encryptedBase64
        _dirty = True
        Dim ignore = FlushAfterDebounceAsync()
    End Function

    ''' <summary>Imposta e persiste la modalità Non Disturbare con la durata specificata (TODO #47).</summary>
    Public Async Function SetDndModeAsync(mode As String) As Task
        Dim normMode = If(String.IsNullOrWhiteSpace(mode), "off", mode.ToLowerInvariant())
        Select Case normMode
            Case "30m"
                _isDndEnabled = True
                _dndUntil = DateTime.UtcNow.AddMinutes(30)
                _dndDurationMode = "30m"
            Case "1h"
                _isDndEnabled = True
                _dndUntil = DateTime.UtcNow.AddHours(1)
                _dndDurationMode = "1h"
            Case "2h"
                _isDndEnabled = True
                _dndUntil = DateTime.UtcNow.AddHours(2)
                _dndDurationMode = "2h"
            Case "8h"
                _isDndEnabled = True
                _dndUntil = DateTime.UtcNow.AddHours(8)
                _dndDurationMode = "8h"
            Case "indefinite"
                _isDndEnabled = True
                _dndUntil = Nothing
                _dndDurationMode = "indefinite"
            Case Else
                _isDndEnabled = False
                _dndUntil = Nothing
                _dndDurationMode = "off"
        End Select

        NotifyPropertyChanged(NameOf(IsDndEnabled))
        NotifyPropertyChanged(NameOf(DndUntil))
        NotifyPropertyChanged(NameOf(DndDurationMode))
        NotifyPropertyChanged(NameOf(IsDndActive))

        If _cachedSettings Is Nothing Then Await ReadSettingsAsync()
        _cachedSettings("isDndEnabled") = _isDndEnabled
        _cachedSettings("dndUntil") = If(_dndUntil.HasValue, _dndUntil.Value.ToString("o"), Nothing)
        _cachedSettings("dndDurationMode") = _dndDurationMode
        _dirty = True
        Dim ignore = FlushAfterDebounceAsync()
    End Function

    Private Sub NotifyAllPropertiesChanged()
        NotifyPropertyChanged("")
    End Sub
End Class

''' <summary>
''' Modulo helper con caching per la lettura del valore AppsUseLightTheme nel Registro di Windows.
''' </summary>
Public Module SystemThemeHelper
    Private _cachedIsDark As Boolean? = Nothing

    Sub New()
        Try
            AddHandler SystemEvents.UserPreferenceChanged, AddressOf OnUserPreferenceChanged
        Catch ex As Exception
            Debug.WriteLine($"SystemThemeHelper initialization warning: {ex.Message}")
        End Try
    End Sub

    Private Sub OnUserPreferenceChanged(sender As Object, e As UserPreferenceChangedEventArgs)
        If e.Category = UserPreferenceCategory.General OrElse e.Category = UserPreferenceCategory.VisualStyle Then
            _cachedIsDark = Nothing
        End If
    End Sub

    ''' <summary>
    ''' Determina se il tema scuro di sistema è attivo con caching e invalidazione automatica su cambio preferenze utente.
    ''' </summary>
    Public Function IsSystemDarkTheme() As Boolean
        If _cachedIsDark.HasValue Then
            Return _cachedIsDark.Value
        End If

        Dim isDark As Boolean = False
        Try
            Using key = Registry.CurrentUser.OpenSubKey("Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")
                If key IsNot Nothing Then
                    Dim val = key.GetValue("AppsUseLightTheme")
                    If val IsNot Nothing AndAlso Convert.ToInt32(val) = 0 Then
                        isDark = True
                    End If
                End If
            End Using
        Catch ex As Exception
            Debug.WriteLine($"Error reading AppsUseLightTheme registry key: {ex.Message}")
        End Try

        _cachedIsDark = isDark
        Return isDark
    End Function
End Module


