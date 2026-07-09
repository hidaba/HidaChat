Imports System.IO
Imports System.Text.Json
Imports System.ComponentModel
Imports System.Runtime.CompilerServices

Public Class SettingsController
    Implements INotifyPropertyChanged

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private Sub NotifyPropertyChanged(<CallerMemberName> Optional propertyName As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

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

    Private _keepAppInEnglish As Boolean = False
    Public Property KeepAppInEnglish As Boolean
        Get
            Return _keepAppInEnglish
        End Get
        Set(value As Boolean)
            If _keepAppInEnglish <> value Then
                _keepAppInEnglish = value
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

    Private _translateNotifications As Boolean = True
    Public Property TranslateNotifications As Boolean
        Get
            Return _translateNotifications
        End Get
        Set(value As Boolean)
            If _translateNotifications <> value Then
                _translateNotifications = value
                NotifyPropertyChanged()
            End If
        End Set
    End Property

    Private _showTranslateNotificationButton As Boolean = False
    Public Property ShowTranslateNotificationButton As Boolean
        Get
            Return _showTranslateNotificationButton
        End Get
        Set(value As Boolean)
            If _showTranslateNotificationButton <> value Then
                _showTranslateNotificationButton = value
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

    Private _supportedLanguages As New List(Of Dictionary(Of String, String)) From {
        New Dictionary(Of String, String) From {{"name", "English"}, {"code", "en"}}
    }
    Public Property SupportedLanguages As List(Of Dictionary(Of String, String))
        Get
            Return _supportedLanguages
        End Get
        Set(value As List(Of Dictionary(Of String, String)))
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

    Private ReadOnly Property SettingsFile As String
        Get
            Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json")
        End Get
    End Property

    Private ReadOnly Property CacheFile As String
        Get
            Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translations_cache.json")
        End Get
    End Property

    Public Async Function ReadSettingsAsync() As Task(Of Dictionary(Of String, Object))
        If Not File.Exists(SettingsFile) Then Return New Dictionary(Of String, Object)()
        Try
            Dim contents = Await File.ReadAllTextAsync(SettingsFile)
            If String.IsNullOrEmpty(contents) Then Return New Dictionary(Of String, Object)()
            Return JsonSerializer.Deserialize(Of Dictionary(Of String, Object))(contents)
        Catch
            Return New Dictionary(Of String, Object)()
        End Try
    End Function

    Public Async Function WriteSettingsAsync(settings As Dictionary(Of String, Object)) As Task
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
        _translateMessageButton = GetBoolSetting(settings, "translateMessageButton", GetBoolSetting(settings, "enableHoverTranslation", True))
        _keepAppInEnglish = GetBoolSetting(settings, "keepAppInEnglish", GetBoolSetting(settings, "translateContentOnly", False))
        _fullPageTranslation = GetBoolSetting(settings, "fullPageTranslation", GetBoolSetting(settings, "enableFullPageTranslation", False))
        _showTranslateAllMessagesButton = GetBoolSetting(settings, "showTranslateAllMessagesButton", True)
        _translateNotifications = GetBoolSetting(settings, "translateNotifications", True)
        _showTranslateNotificationButton = GetBoolSetting(settings, "showTranslateNotificationButton", False)
        
        If settings.ContainsKey("language") Then
            _language = settings("language").ToString()
        Else
            _language = "en"
        End If

        ' Load translations cache
        If File.Exists(CacheFile) Then
            Try
                Dim cacheContent = Await File.ReadAllTextAsync(CacheFile)
                If Not String.IsNullOrEmpty(cacheContent) Then
                    Using doc As JsonDocument = JsonDocument.Parse(cacheContent)
                        Dim root = doc.RootElement
                        
                        ' Load cached languages
                        Dim cachedLangsElement As JsonElement = Nothing
                        If root.TryGetProperty("supported_languages", cachedLangsElement) Then
                            Dim newLangs As New List(Of Dictionary(Of String, String))()
                            For Each item In cachedLangsElement.EnumerateArray()
                                Dim dict As New Dictionary(Of String, String)()
                                For Each prop In item.EnumerateObject()
                                    dict(prop.Name) = prop.Value.GetString()
                                Next
                                newLangs.Add(dict)
                            Next
                            _supportedLanguages = newLangs
                        End If

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
        
        ' Run async task to fetch fresh languages list from Google
        Dim ignore = LoadSupportedLanguagesAsync()

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

    Private Async Function LoadSupportedLanguagesAsync() As Task
        Try
            Dim langs = Await AppLanguages.FetchSupportedLanguages()
            If langs.Count > 0 Then
                _supportedLanguages = langs
                NotifyPropertyChanged(NameOf(SupportedLanguages))
                Await SaveCacheFileAsync()
            End If
        Catch ex As Exception
            Debug.WriteLine($"Failed to load supported languages async: {ex.Message}")
        End Try
    End Function

    Private Async Function SaveCacheFileAsync() As Task
        Try
            Dim cacheData As New Dictionary(Of String, Object)()
            cacheData("supported_languages") = _supportedLanguages
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
        If _language = "en" OrElse _keepAppInEnglish Then
            Localizations = New AppLocalizations(AppLocalizations.EnStrings)
        ElseIf _cachedTranslations.ContainsKey(_language) Then
            Dim cached = _cachedTranslations(_language)
            Dim hasAllKeys = AppLocalizations.EnStrings.Keys.All(Function(key) cached.ContainsKey(key))
            If hasAllKeys Then
                Localizations = New AppLocalizations(cached)
            Else
                Dim ignore = LoadTranslationsAsync(_language)
            End If
        Else
            Dim ignore = LoadTranslationsAsync(_language)
        End If
    End Sub

    Private Async Function LoadTranslationsAsync(langCode As String) As Task
        IsTranslating = True
        Try
            Dim fetched = Await AppLocalizations.FetchTranslations(langCode)
            Localizations = New AppLocalizations(fetched)
            _cachedTranslations(langCode) = fetched
            Await SaveCacheFileAsync()
        Catch ex As Exception
            Debug.WriteLine($"Failed to load translations async: {ex.Message}")
        End Try
        IsTranslating = False
    End Function

    Public Async Function UpdateLanguageAsync(newLanguage As String) As Task
        If _language = newLanguage Then Return
        _language = newLanguage
        NotifyPropertyChanged(NameOf(Language))

        Dim settings = Await ReadSettingsAsync()
        settings("language") = newLanguage
        Await WriteSettingsAsync(settings)

        If newLanguage = "en" OrElse _keepAppInEnglish Then
            Localizations = New AppLocalizations(AppLocalizations.EnStrings)
        ElseIf _cachedTranslations.ContainsKey(newLanguage) Then
            Dim cached = _cachedTranslations(newLanguage)
            Dim hasAllKeys = AppLocalizations.EnStrings.Keys.All(Function(key) cached.ContainsKey(key))
            If hasAllKeys Then
                Localizations = New AppLocalizations(cached)
            Else
                Await LoadTranslationsAsync(newLanguage)
            End If
        Else
            Await LoadTranslationsAsync(newLanguage)
        End If
    End Function

    Public Async Function SaveThemeAsync(newTheme As String) As Task
        _theme = newTheme
        NotifyPropertyChanged(NameOf(Theme))
        Dim settings = Await ReadSettingsAsync()
        settings("theme") = "ThemeMode." & newTheme.ToLower()
        Await WriteSettingsAsync(settings)
    End Function

    Public Async Function SaveSettingAsync(key As String, value As Object) As Task
        Dim settings = Await ReadSettingsAsync()
        settings(key) = value
        Await WriteSettingsAsync(settings)
    End Function

    Private Sub NotifyAllPropertiesChanged()
        NotifyPropertyChanged("")
    End Sub
End Class
