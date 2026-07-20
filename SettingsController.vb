Imports System.IO
Imports System.Text.Json
Imports System.ComponentModel
Imports System.Runtime.CompilerServices
Imports System.Threading

Public Class SettingsController
    Implements INotifyPropertyChanged

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private Sub NotifyPropertyChanged(<CallerMemberName> Optional propertyName As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

    Private _cachedSettings As Dictionary(Of String, Object) = Nothing
    Private _dirty As Boolean = False
    Private _lastFlushTask As Task = Task.CompletedTask
    Private _flushCts As CancellationTokenSource = Nothing

    ' --- Impostazioni tema ---
    Private _theme As String = "System"
    Public Property Theme As String
        Get
            Return _theme
        End Get
        Set(value As String)
            If _theme <> value Then
                _theme = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    ' --- Barra schede sempre visibile ---
    Private _alwaysShowTabBar As Boolean = True
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

    Private _language As String = "en"
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
        New LanguageInfo With {.Name = "Italiano", .Code = "it"}
    }
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

    Private _cachedTranslations As New Dictionary(Of String, Dictionary(Of String, String))(StringComparer.OrdinalIgnoreCase)

    ' --- File JSON delle impostazioni ---
    Private ReadOnly Property SettingsFile As String
        Get
            ' Prima cerca settings.json nella cartella base, poi in data\ (fallback per OTA)
            Dim basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json")
            If File.Exists(basePath) Then Return basePath
            Dim dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "settings.json")
            If File.Exists(dataPath) Then Return dataPath
            Return basePath
        End Get
    End Property

    ' --- Cache traduzioni scaricate ---
    Private ReadOnly Property CacheFile As String
        Get
            Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translations_cache.json")
        End Get
    End Property

    ' Legge il file settings.json dal disco (usa cache se già caricato)
    Public Async Function ReadSettingsAsync() As Task(Of Dictionary(Of String, Object))
        If _cachedSettings IsNot Nothing Then Return _cachedSettings
        If Not File.Exists(SettingsFile) Then
            _cachedSettings = New Dictionary(Of String, Object)()
            Return _cachedSettings
        End If
        Try
            Dim contents = Await File.ReadAllTextAsync(SettingsFile)
            If String.IsNullOrEmpty(contents) Then
                _cachedSettings = New Dictionary(Of String, Object)()
            Else
                _cachedSettings = JsonSerializer.Deserialize(Of Dictionary(Of String, Object))(contents)
            End If
        Catch
            _cachedSettings = New Dictionary(Of String, Object)()
        End Try
        If _cachedSettings Is Nothing Then _cachedSettings = New Dictionary(Of String, Object)()
        Return _cachedSettings
    End Function

    ' Salva le impostazioni su disco come JSON (scrive subito, senza debounce)
    Public Async Function WriteSettingsAsync(settings As Dictionary(Of String, Object)) As Task
        _cachedSettings = settings
        _dirty = False
        If _flushCts IsNot Nothing Then _flushCts.Cancel()
        Try
            Dim options As New JsonSerializerOptions With {
                .WriteIndented = True
            }
            Dim contents = JsonSerializer.Serialize(settings, options)
            Await File.WriteAllTextAsync(SettingsFile, contents)
        Catch ex As Exception
            Debug.WriteLine($"Failed to write settings: {ex.Message}")
        End Try
    End Function

    ' Accumula modifiche in memoria e pianifica scrittura differita (debounce 500ms)
    Private Async Function FlushAfterDebounceAsync() As Task
        If _flushCts IsNot Nothing Then _flushCts.Cancel()
        _flushCts = New CancellationTokenSource()
        Dim token = _flushCts.Token
        Try
            Await Task.Delay(500, token)
        Catch ex As OperationCanceledException
            Return
        End Try
        If _dirty AndAlso _cachedSettings IsNot Nothing Then
            _dirty = False
            Await WriteSettingsAsync(_cachedSettings)
        End If
    End Function

    ' Carica tutte le impostazioni dal file JSON all'avvio
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
        ' Lingua salvata nelle impostazioni
        If settings.ContainsKey("language") Then
            _language = settings("language").ToString()
        Else
            _language = "en"
        End If

        ' Carica cache traduzioni da disco
        If File.Exists(CacheFile) Then
            Try
                Dim cacheContent = Await File.ReadAllTextAsync(CacheFile)
                If Not String.IsNullOrEmpty(cacheContent) Then
                    Using doc As JsonDocument = JsonDocument.Parse(cacheContent)
                        Dim root = doc.RootElement

                        ' Load cached translations
                        _cachedTranslations.Clear()
                        Dim cachedTranslationsElement As JsonElement = Nothing
                        If root.TryGetProperty("cached_translations", cachedTranslationsElement) Then
                            For Each langProp In cachedTranslationsElement.EnumerateObject()
                                Dim translationsDict As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                                For Each transProp In langProp.Value.EnumerateObject()
                                    translationsDict(transProp.Name) = transProp.Value.GetString()
                                Next
                                _cachedTranslations(langProp.Name) = translationsDict
                            Next
                        End If
                    End Using
                End If
            Catch ex As Exception
                Debug.WriteLine($"Failed to load translations cache: {ex.Message}")
            End Try
        End If

        FallbackOrLoadTranslations()

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

    Private Async Function SaveCacheFileAsync() As Task
        Try
            Dim cacheData As New Dictionary(Of String, Object)()
            cacheData("cached_translations") = _cachedTranslations
            
            Dim options As New JsonSerializerOptions With {
                .WriteIndented = True
            }
            Dim contents = JsonSerializer.Serialize(cacheData, options)
            Await File.WriteAllTextAsync(CacheFile, contents)
        Catch ex As Exception
            Debug.WriteLine($"Failed to write cache file: {ex.Message}")
        End Try
    End Function

    Private Sub FallbackOrLoadTranslations()
        Select Case _language
            Case "en"
                Localizations = New AppLocalizations(AppLocalizations.EnStrings)
            Case "it"
                Localizations = New AppLocalizations(AppLocalizations.ItStrings)
            Case Else
                ' Lingua non più supportata (es. selezionata prima della 1.1.0) → ricade in inglese
                Localizations = New AppLocalizations(AppLocalizations.EnStrings)
        End Select
    End Sub

    Public Async Function UpdateLanguageAsync(newLanguage As String) As Task
        If _language = newLanguage Then Return
        _language = newLanguage
        NotifyPropertyChanged(NameOf(Language))

        If _cachedSettings Is Nothing Then Await ReadSettingsAsync()
        _cachedSettings("language") = newLanguage
        _dirty = True
        Dim ignore = FlushAfterDebounceAsync()

        If newLanguage = "en" Then
            Localizations = New AppLocalizations(AppLocalizations.EnStrings)
        ElseIf newLanguage = "it" Then
            Localizations = New AppLocalizations(AppLocalizations.ItStrings)
            _cachedTranslations("it") = AppLocalizations.ItStrings
            Await SaveCacheFileAsync()
        End If
    End Function

    Public Async Function SaveThemeAsync(newTheme As String) As Task
        _theme = newTheme
        NotifyPropertyChanged(NameOf(Theme))
        If _cachedSettings Is Nothing Then Await ReadSettingsAsync()
        _cachedSettings("theme") = "ThemeMode." & newTheme.ToLower()
        _dirty = True
        Dim ignore = FlushAfterDebounceAsync()
    End Function

    Public Async Function SaveSettingAsync(key As String, value As Object) As Task
        If _cachedSettings Is Nothing Then Await ReadSettingsAsync()
        _cachedSettings(key) = value
        _dirty = True
        Dim ignore = FlushAfterDebounceAsync()
    End Function

    Private Sub NotifyAllPropertiesChanged()
        NotifyPropertyChanged("")
    End Sub
End Class
