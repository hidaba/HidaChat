Imports System.IO
Imports System.ComponentModel
Imports System.Text.Json.Serialization
Imports System.Windows.Media
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.Wpf
Imports Microsoft.Toolkit.Uwp.Notifications
Imports System.Text.Json

''' <summary>
''' Rappresenta un singolo account di chat (WhatsApp Web o Telegram Web), gestione dell'istanza WebView2 associata, 
''' token di sicurezza per IPC e gestione di notifiche e traduzioni.
''' </summary>
Public Class AppAccounts
    Implements INotifyPropertyChanged
    Implements IDisposable

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private Shared ReadOnly _randLock As New Object()
    Private Shared ReadOnly _rand As New Random()

    ''' <summary>Identificativo univoco dell'account (es. account_1680000000000).</summary>
    <JsonPropertyName("id")>
    Public Property Id As String

    Private _platform As String = "WhatsApp"
    ''' <summary>Tipo di piattaforma di messaggistica ("WhatsApp" o "Telegram").</summary>
    <JsonPropertyName("platform")>
    Public Property Platform As String
        Get
            If String.IsNullOrWhiteSpace(_platform) Then Return "WhatsApp"
            Return _platform
        End Get
        Set(value As String)
            Dim cleanVal = If(String.IsNullOrWhiteSpace(value), "WhatsApp", value)
            If _platform <> cleanVal Then
                _platform = cleanVal
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(Platform)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(IsWhatsApp)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(IsTelegram)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(PlatformIconData)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(PlatformColorBrush)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(WebUrl)))
            End If
        End Set
    End Property

    <JsonIgnore>
    Public ReadOnly Property IsTelegram As Boolean
        Get
            Return Platform.Equals("Telegram", StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property IsWhatsApp As Boolean
        Get
            Return Not IsTelegram
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property WebUrl As String
        Get
            If IsTelegram Then
                Return "https://web.telegram.org/a/"
            Else
                Return "https://web.whatsapp.com/"
            End If
        End Get
    End Property

    Private Shared ReadOnly WhatsAppBrush As Brush = BrushCache.GetBrush("#25d366")
    Private Shared ReadOnly TelegramBrush As Brush = BrushCache.GetBrush("#24A1DE")

    <JsonIgnore>
    Public ReadOnly Property PlatformIconData As String
        Get
            If IsTelegram Then
                Return "M9.78 18.65L10.06 14.42L17.74 7.5C18.08 7.19 17.67 7.04 17.22 7.31L7.74 13.3L3.64 12C2.76 11.75 2.75 11.14 3.84 10.7L19.81 4.54C20.54 4.21 21.24 4.72 20.97 5.84L18.25 18.67C18.05 19.6 17.5 19.82 16.73 19.38L12.58 16.32L10.58 18.25C10.36 18.47 10.17 18.65 9.78 18.65Z"
            Else
                Return "M12.04 2C6.58 2 2.13 6.45 2.13 11.91C2.13 13.66 2.59 15.36 3.45 16.86L2.05 22L7.3 20.62C8.75 21.41 10.38 21.83 12.04 21.83C17.5 21.83 21.95 17.38 21.95 11.92C21.95 9.27 20.92 6.78 19.05 4.91C17.18 3.03 14.69 2 12.04 2M12.05 3.67C14.25 3.67 16.31 4.53 17.87 6.09C19.42 7.65 20.28 9.72 20.28 11.92C20.28 16.46 16.58 20.15 12.04 20.15C10.56 20.15 9.11 19.76 7.85 19L7.55 18.83L4.43 19.65L5.26 16.61L5.06 16.29C4.24 15 3.8 13.47 3.8 11.91C3.81 7.37 7.5 3.67 12.05 3.67M8.53 7.33C8.37 7.33 8.1 7.39 7.87 7.64C7.65 7.89 7.02 8.48 7.02 9.68C7.02 10.88 7.9 12.03 8.02 12.19C8.14 12.35 9.73 14.81 12.18 15.86C12.76 16.11 13.22 16.26 13.57 16.37C14.16 16.56 14.69 16.53 15.11 16.47C15.59 16.4 16.58 15.87 16.78 15.3C16.98 14.73 16.98 14.24 16.92 14.14C16.86 14.04 16.7 13.98 16.45 13.85C16.2 13.73 14.97 13.12 14.74 13.04C14.52 12.96 14.35 12.92 14.19 13.17C14.03 13.41 13.56 13.98 13.42 14.14C13.28 14.31 13.13 14.33 12.89 14.21C12.64 14.08 11.84 13.82 10.89 12.97C10.15 12.31 9.65 11.5 9.51 11.25C9.36 11.01 9.5 10.87 9.62 10.75C9.73 10.64 9.87 10.45 10 10.31C10.13 10.16 10.17 10.06 10.25 9.9C10.33 9.73 10.29 9.59 10.23 9.47C10.17 9.35 9.7 8.19 9.5 7.72C9.31 7.26 9.12 7.32 8.97 7.31C8.84 7.31 8.68 7.33 8.53 7.33Z"
            End If
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property PlatformColorBrush As Brush
        Get
            If IsTelegram Then
                Return TelegramBrush
            Else
                Return WhatsAppBrush
            End If
        End Get
    End Property

    Private _name As String
    ''' <summary>Nome personalizzato visualizzato nelle schede e impostazioni.</summary>
    <JsonPropertyName("name")>
    Public Property Name As String
        Get
            Return _name
        End Get
        Set(value As String)
            If _name <> value Then
                _name = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(Name)))
            End If
        End Set
    End Property

    Private _isActive As Boolean
    ''' <summary>Indica se l'account è attualmente attivo e visibile nella finestra principale.</summary>
    <JsonPropertyName("isActive")>
    Public Property IsActive As Boolean
        Get
            Return _isActive
        End Get
        Set(value As Boolean)
            If _isActive <> value Then
                _isActive = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(IsActive)))
            End If
        End Set
    End Property
    
    Private _hasNotification As Boolean
    ''' <summary>Indica se vi sono notifiche pendenti non lette per questo account.</summary>
    <JsonIgnore>
    Public Property HasNotification As Boolean
        Get
            Return _hasNotification
        End Get
        Set(value As Boolean)
            If _hasNotification <> value Then
                _hasNotification = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(HasNotification)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(HasUnreadBadge)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(UnreadBadgeText)))
            End If
        End Set
    End Property

    Private _unreadCount As Integer = 0
    ''' <summary>Numero di messaggi o chat non lette rilevate nella piattaforma.</summary>
    <JsonIgnore>
    Public Property UnreadCount As Integer
        Get
            Return _unreadCount
        End Get
        Set(value As Integer)
            Dim cleanVal = Math.Max(0, value)
            If _unreadCount <> cleanVal Then
                _unreadCount = cleanVal
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(UnreadCount)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(HasUnreadBadge)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(UnreadBadgeText)))
            End If
        End Set
    End Property

    ''' <summary>Indica se visualizzare il badge numerico o il pallino di notifica sulla scheda.</summary>
    <JsonIgnore>
    Public ReadOnly Property HasUnreadBadge As Boolean
        Get
            Return _unreadCount > 0 OrElse _hasNotification
        End Get
    End Property

    ''' <summary>Testo formattato del badge (es. "1", "5", "99+" o "•").</summary>
    <JsonIgnore>
    Public ReadOnly Property UnreadBadgeText As String
        Get
            If _unreadCount > 99 Then
                Return "99+"
            ElseIf _unreadCount > 0 Then
                Return _unreadCount.ToString()
            ElseIf _hasNotification Then
                Return "•"
            Else
                Return String.Empty
            End If
        End Get
    End Property

    Private _isContactOnline As Boolean = False
    ''' <summary>Indica se il contatto della chat attualmente aperta nella sessione è in linea o sta scrivendo (TODO #42).</summary>
    <JsonIgnore>
    Public Property IsContactOnline As Boolean
        Get
            Return _isContactOnline
        End Get
        Set(value As Boolean)
            If _isContactOnline <> value Then
                _isContactOnline = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(IsContactOnline)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(OnlineStatusDisplay)))
            End If
        End Set
    End Property

    Private _contactOnlineStatusText As String = ""
    ''' <summary>Testo descrittivo dello stato del contatto (es. "in linea", "online", "sta scrivendo...").</summary>
    <JsonIgnore>
    Public Property ContactOnlineStatusText As String
        Get
            Return _contactOnlineStatusText
        End Get
        Set(value As String)
            Dim cleanVal = If(value, String.Empty).Trim()
            If _contactOnlineStatusText <> cleanVal Then
                _contactOnlineStatusText = cleanVal
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(ContactOnlineStatusText)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(OnlineStatusDisplay)))
            End If
        End Set
    End Property

    ''' <summary>Etichetta formattata per la visualizzazione dello stato online nella UI.</summary>
    <JsonIgnore>
    Public ReadOnly Property OnlineStatusDisplay As String
        Get
            If Not _isContactOnline Then Return String.Empty
            If Not String.IsNullOrWhiteSpace(_contactOnlineStatusText) Then
                Return _contactOnlineStatusText
            End If
            Return "in linea"
        End Get
    End Property
    
    ''' <summary>Token di sicurezza generato ad ogni sessione per validare i messaggi IPC provenienti dal JavaScript della WebView.</summary>
    <JsonIgnore>
    Public Property BridgeToken As String
    
    ''' <summary>Controllo WPF WebView2 dinamico associato all'account.</summary>
    <JsonIgnore>
    Public Property WebView As WebView2
    
    ''' <summary>Insieme degli identificativi delle notifiche attualmente attive per questo account.</summary>
    <JsonIgnore>
    Public ReadOnly Property ActiveNotificationIds As New HashSet(Of String)()

    Private Shared ReadOnly MaxActiveNotificationIds As Integer = 500

    ''' <summary>Evento sollevato quando il processo WebView2 crasha e richiede una rigenerazione completa del controllo.</summary>
    Public Event ProcessFailedRecoveryRequested As EventHandler(Of CoreWebView2ProcessFailedEventArgs)

    Private _isCrashed As Boolean = False
    ''' <summary>Indica se il processo browser associato alla WebView2 è crashato o non valido.</summary>
    <JsonIgnore>
    Public Property IsCrashed As Boolean
        Get
            Return _isCrashed
        End Get
        Set(value As Boolean)
            _isCrashed = value
        End Set
    End Property

    ' Event Handlers fortemente tipizzati per WebView2 (evita memory leak)
    Private _permissionRequestedHandler As EventHandler(Of CoreWebView2PermissionRequestedEventArgs)
    Private _newWindowRequestedHandler As EventHandler(Of CoreWebView2NewWindowRequestedEventArgs)
    Private _navigationStartingHandler As EventHandler(Of CoreWebView2NavigationStartingEventArgs)
    Private _webMessageReceivedHandler As EventHandler(Of CoreWebView2WebMessageReceivedEventArgs)
    Private _navigationCompletedHandler As EventHandler(Of CoreWebView2NavigationCompletedEventArgs)
    Private _processFailedHandler As EventHandler(Of CoreWebView2ProcessFailedEventArgs)

    ''' <summary>Percorso base per il salvataggio dei profili WebView2 isolati degli account.</summary>
    Public Shared ReadOnly Property SharedDataDirectory As String
        Get
            Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "webview")
        End Get
    End Property

    ''' <summary>
    ''' Rimuove in modo sicuro le cartelle di cache volatile (Code Cache, Disk Cache, Service Worker CacheStorage, ShaderCache, Crashpad)
    ''' senza alterare cookie, sessioni attive o il database messaggi IndexedDB.
    ''' </summary>
    Public Shared Sub CleanTransientCacheFolders(profileDir As String)
        If String.IsNullOrEmpty(profileDir) OrElse Not Directory.Exists(profileDir) Then Return

        Dim relativeDirsToClean As String() = {
            "EBWebView\ShaderCache",
            "EBWebView\GrShaderCache",
            "EBWebView\Crashpad\reports",
            "EBWebView\Crashpad",
            "EBWebView\component_crx_cache",
            "EBWebView\Subresource Filter",
            "EBWebView\Default\Cache",
            "EBWebView\Default\Code Cache",
            "EBWebView\Default\GPUCache",
            "EBWebView\Default\DawnGraphiteCache",
            "EBWebView\Default\DawnWebGPUCache",
            "EBWebView\Default\GPUPersistentCache",
            "EBWebView\Default\Service Worker\CacheStorage",
            "EBWebView\Default\Service Worker\ScriptCache"
        }

        For Each relDir In relativeDirsToClean
            Try
                Dim targetDir = Path.Combine(profileDir, relDir)
                If Directory.Exists(targetDir) Then
                    Directory.Delete(targetDir, recursive:=True)
                    Debug.WriteLine($"CleanTransientCacheFolders: rimossa {targetDir}")
                End If
            Catch ex As Exception
                Try
                    Dim targetDir = Path.Combine(profileDir, relDir)
                    If Directory.Exists(targetDir) Then
                        For Each f In Directory.EnumerateFiles(targetDir, "*", SearchOption.AllDirectories)
                            Try
                                File.Delete(f)
                            Catch
                            End Try
                        Next
                    End If
                Catch
                End Try
                Debug.WriteLine($"CleanTransientCacheFolders warning for {relDir}: {ex.Message}")
            End Try
        Next
    End Sub

    ''' <summary>Genera un identificativo alfanumerico univoco basato sul timestamp corrente.</summary>
    Public Shared Function GenerateId() As String
        Return "account_" & DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    End Function

    ''' <summary>Genera un token casuale di sicurezza per la validazione della comunicazione IPC.</summary>
    Private Shared Function GenerateBridgeToken() As String
        Dim val As Integer
        SyncLock _randLock
            val = _rand.Next(100000, 999999)
        End SyncLock
        Return "bt_" & DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & "_" & val
    End Function

    Public Sub New()
        BridgeToken = GenerateBridgeToken()
    End Sub

    Public Sub New(id As String, name As String, Optional isActive As Boolean = False, Optional platform As String = "WhatsApp")
        Me.Id = id
        Me.Name = name
        Me.IsActive = isActive
        Me.Platform = platform
        BridgeToken = GenerateBridgeToken()
    End Sub

    Private _initTask As Task = Nothing

    ''' <summary>
    ''' Configura l'ambiente isolato della WebView2, inietta gli script JavaScript per l'intercettazione delle notifiche e traduzioni,
    ''' e naviga verso la pagina della piattaforma di messaggistica (WhatsApp Web o Telegram Web).
    ''' </summary>
    Public Function SetupWebViewAsync(settings As SettingsController, onNotificationChanged As Action(Of String, Boolean)) As Task
        If _initTask IsNot Nothing AndAlso Not _initTask.IsFaulted AndAlso Not _isCrashed Then
            Return _initTask
        End If
        _initTask = SetupWebViewInternalAsync(settings, onNotificationChanged)
        Return _initTask
    End Function

    Private Async Function SetupWebViewInternalAsync(settings As SettingsController, onNotificationChanged As Action(Of String, Boolean)) As Task
        If WebView Is Nothing Then Return

        Dim profileDir = Path.Combine(SharedDataDirectory, $"WV2Profile_{Id}")
        Dim orphanProfile = Path.Combine(SharedDataDirectory, "WV2Profile_")
        If Directory.Exists(orphanProfile) Then
            If Directory.Exists(profileDir) Then
                Try
                    Directory.Delete(profileDir, True)
                    Debug.WriteLine($"SetupWebView: eliminato profilo stale {profileDir}")
                Catch ex As Exception
                    Debug.WriteLine($"SetupWebView: errore cancellazione stale: {ex.Message}")
                End Try
            End If
            Try
                Directory.Move(orphanProfile, profileDir)
                Debug.WriteLine($"SetupWebView: recuperato profilo orfano {orphanProfile} -> {profileDir}")
            Catch ex As Exception
                Debug.WriteLine($"SetupWebView: fallito recupero orfano: {ex.Message}")
            End Try
        End If

        If Not Directory.Exists(profileDir) Then
            Directory.CreateDirectory(profileDir)
            Debug.WriteLine($"SetupWebView: creato nuovo profilo {profileDir}")
        End If

        ' Pulizia preventiva delle cartelle di cache volatile prima di agganciare il processo WebView2
        CleanTransientCacheFolders(profileDir)

        Try
            Dim options As New CoreWebView2EnvironmentOptions()
            Dim effectiveLang = settings.GetEffectiveChromiumLanguage()
            If Not String.IsNullOrEmpty(effectiveLang) Then
                options.Language = effectiveLang
            End If

            Dim browserArgs = "--disk-cache-size=104857600 --media-cache-size=52428800 --disable-gpu-shader-disk-cache --disable-component-update --disable-domain-reliability --no-crash-upload --disable-background-timer-throttling --disable-backgrounding-occluded-windows --disable-renderer-backgrounding"
            Dim disabledFeatures As New List(Of String) From {"Translate", "MediaRouter"}
            If settings.EnableSpellcheck Then
                browserArgs &= $" --enable-features=Spellcheck --lang={effectiveLang}"
            Else
                disabledFeatures.Add("Spellcheck")
            End If
            browserArgs &= $" --disable-features={String.Join(",", disabledFeatures)}"
            options.AdditionalBrowserArguments = browserArgs

            Dim accountEnv = Await CoreWebView2Environment.CreateAsync(Nothing, profileDir, options)
            
            Await WebView.EnsureCoreWebView2Async(accountEnv)
            _isCrashed = False
            
            WebView.CoreWebView2.Settings.IsWebMessageEnabled = True
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = True
            WebView.CoreWebView2.Settings.IsGeneralAutofillEnabled = True
            WebView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = False
            
            ' Registra listener crash a livello di CoreWebView2
            _processFailedHandler = Sub(sender, e)
                HandleProcessFailed(e)
            End Sub
            AddHandler WebView.CoreWebView2.ProcessFailed, _processFailedHandler
            
            ' Salvataggio riferimenti handler per poterli rimuovere in Dispose()
            _permissionRequestedHandler = Sub(sender, e)
                If e.PermissionKind = CoreWebView2PermissionKind.Notifications Then
                    e.State = CoreWebView2PermissionState.Allow
                    e.Handled = True
                End If
            End Sub
            AddHandler WebView.CoreWebView2.PermissionRequested, _permissionRequestedHandler

            Dim initScript = NotificationJsScripts.GetNotificationOverrideJS(BridgeToken)
            If IsTelegram Then
                initScript &= vbCrLf & ThemeJsScripts.TelegramInitJS
            End If
            Await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(initScript)

            _navigationStartingHandler = Sub(sender, e)
                If String.IsNullOrEmpty(e.Uri) Then Return
                If e.Uri.StartsWith("tg:", StringComparison.OrdinalIgnoreCase) Then
                    e.Cancel = True
                    Dim target = ResolveTelegramUrl(e.Uri)
                    WebView.CoreWebView2.Navigate(target)
                ElseIf IsTelegram AndAlso (e.Uri.StartsWith("https://t.me/", StringComparison.OrdinalIgnoreCase) OrElse e.Uri.StartsWith("http://t.me/", StringComparison.OrdinalIgnoreCase) OrElse e.Uri.StartsWith("https://telegram.me/", StringComparison.OrdinalIgnoreCase) OrElse e.Uri.StartsWith("http://telegram.me/", StringComparison.OrdinalIgnoreCase)) Then
                    e.Cancel = True
                    Dim target = ResolveTelegramUrl(e.Uri)
                    WebView.CoreWebView2.Navigate(target)
                End If
            End Sub
            AddHandler WebView.CoreWebView2.NavigationStarting, _navigationStartingHandler

            _newWindowRequestedHandler = Sub(sender, e)
                e.Handled = True
                Try
                    Dim uriStr = e.Uri
                    If uriStr.StartsWith("tg:", StringComparison.OrdinalIgnoreCase) Then
                        If IsTelegram Then
                            Dim target = ResolveTelegramUrl(uriStr)
                            WebView.CoreWebView2.Navigate(target)
                            Return
                        End If
                    End If

                    Dim uri = New Uri(uriStr)
                    Dim host = uri.Host.ToLower()
                    If IsWhatsApp AndAlso (host = "web.whatsapp.com" OrElse host = "whatsapp.com" OrElse host.EndsWith(".whatsapp.com")) Then
                        WebView.CoreWebView2.Navigate(uriStr)
                    ElseIf IsTelegram AndAlso (host = "web.telegram.org" OrElse host = "telegram.org" OrElse host.EndsWith(".telegram.org")) Then
                        WebView.CoreWebView2.Navigate(uriStr)
                    ElseIf IsTelegram AndAlso (host = "t.me" OrElse host.EndsWith(".t.me") OrElse host = "telegram.me" OrElse host.EndsWith(".telegram.me")) Then
                        Dim target = ResolveTelegramUrl(uriStr)
                        WebView.CoreWebView2.Navigate(target)
                    Else
                        System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo(uriStr) With {
                            .UseShellExecute = True
                        })
                    End If
                Catch
                End Try
            End Sub
            AddHandler WebView.CoreWebView2.NewWindowRequested, _newWindowRequestedHandler

            _webMessageReceivedHandler = Async Sub(sender, e)
                Await HandleWebMessageAsync(e.WebMessageAsJson, settings, onNotificationChanged)
            End Sub
            AddHandler WebView.CoreWebView2.WebMessageReceived, _webMessageReceivedHandler

            _navigationCompletedHandler = Async Sub(sender, e)
                If e.IsSuccess Then
                    Dim brightnessDark = settings.IsDarkThemeEffective

                    If IsTelegram Then
                        If brightnessDark Then
                            Await WebView.CoreWebView2.ExecuteScriptAsync(ThemeJsScripts.TelegramDarkModeJS)
                        Else
                            Await WebView.CoreWebView2.ExecuteScriptAsync(ThemeJsScripts.TelegramLightModeJS)
                        End If
                    Else
                        If brightnessDark Then
                            Await WebView.CoreWebView2.ExecuteScriptAsync(ThemeJsScripts.DarkModeJS)
                        Else
                            Await WebView.CoreWebView2.ExecuteScriptAsync(ThemeJsScripts.LightModeJS)
                        End If
                    End If

                    Dim translatedLangName = "English"
                    Dim langItem = settings.SupportedLanguages.FirstOrDefault(Function(l) l.Code = settings.Language)
                    If langItem IsNot Nothing Then
                        translatedLangName = langItem.Name
                    End If

                    Dim tooltipLabel = settings.Localizations.Get("translate_to_lang", New Dictionary(Of String, String) From {{"lang", translatedLangName}})
                    
                    Dim translationScript = TranslationJsScripts.GetTranslationJS(
                        BridgeToken,
                        settings.Language,
                        translatedLangName,
                        tooltipLabel,
                        settings.TranslateMessageButton,
                        settings.FullPageTranslation
                    )
                    Await WebView.CoreWebView2.ExecuteScriptAsync(translationScript)

                    ' Iniezione CSS personalizzato utente (TODO #43)
                    Await ApplyCustomCssAsync(settings.CustomCss, settings.EnableCustomCss)
                End If
            End Sub
            AddHandler WebView.CoreWebView2.NavigationCompleted, _navigationCompletedHandler

            WebView.CoreWebView2.Navigate(WebUrl)

        Catch ex As Exception
            Debug.WriteLine($"Error configuring WebView2 for account {Id}: {ex.Message}")
        End Try
    End Function

    ''' <summary>
    ''' Intercetta i crash del processo di rendering o del processo browser principale di WebView2 ed avvia il ripristino automatico.
    ''' </summary>
    Private Sub HandleProcessFailed(e As CoreWebView2ProcessFailedEventArgs)
        Try
            Debug.WriteLine($"[ProcessFailed] Account {Id} ({Name}) - Kind: {e.ProcessFailedKind}, Reason: {e.Reason}, ExitCode: {e.ExitCode}, Description: {e.ProcessDescription}")
            
            ' Se crasha unicamente il processo di rendering (tab), un rapido reload è sufficiente per ripristinarlo
            If e.ProcessFailedKind = CoreWebView2ProcessFailedKind.RenderProcessExited Then
                Debug.WriteLine($"[ProcessFailed] Render process exited for account {Id}, attempting fast reload...")
                Try
                    If WebView IsNot Nothing AndAlso WebView.CoreWebView2 IsNot Nothing Then
                        WebView.CoreWebView2.Reload()
                        Return
                    End If
                Catch ex As Exception
                    Debug.WriteLine($"[ProcessFailed] Fast reload failed: {ex.Message}")
                End Try
            End If

            ' Per BrowserProcessExited o fallimento del reload, segna lo stato di crash e richiede l'Auto-Recovery completa
            _isCrashed = True
            _initTask = Nothing
            
            Application.Current?.Dispatcher.BeginInvoke(Sub()
                RaiseEvent ProcessFailedRecoveryRequested(Me, e)
            End Sub)
        Catch ex As Exception
            Debug.WriteLine($"[ProcessFailed] Error in HandleProcessFailed: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Inietta o aggiorna le regole CSS personalizzate dell'utente all'interno della WebView2 (TODO #43).
    ''' </summary>
    Public Async Function ApplyCustomCssAsync(cssText As String, enabled As Boolean) As Task
        If WebView IsNot Nothing AndAlso WebView.CoreWebView2 IsNot Nothing Then
            Try
                Dim script = ThemeJsScripts.GetCustomCssJS(cssText, enabled)
                Await WebView.CoreWebView2.ExecuteScriptAsync(script)
            Catch ex As Exception
                Debug.WriteLine($"Error applying custom CSS to account {Id}: {ex.Message}")
            End Try
        End If
    End Function

    ''' <summary>
    ''' Imposta una nuova piattaforma per l'account e ricarica la WebView2 con l'URL corrispondente.
    ''' </summary>
    Public Sub SetPlatform(newPlatform As String)
        If Not String.Equals(_platform, newPlatform, StringComparison.OrdinalIgnoreCase) Then
            Platform = newPlatform
            If WebView IsNot Nothing AndAlso WebView.CoreWebView2 IsNot Nothing Then
                WebView.CoreWebView2.Navigate(WebUrl)
            End If
        End If
    End Sub

    ''' <summary>
    ''' Gestisce i messaggi IPC JSON inviati dalla WebView2 tramite `window.chrome.webview.postMessage`.
    ''' Verifica la validità del token prima dell'elaborazione.
    ''' </summary>
    Private Async Function HandleWebMessageAsync(messageJson As String, settings As SettingsController, onNotificationChanged As Action(Of String, Boolean)) As Task
        Debug.WriteLine($"[WebMessageReceived] accountId={Id}, RAW JSON: {messageJson}")
        Try
            Using doc As JsonDocument = JsonDocument.Parse(messageJson)
                Dim root = doc.RootElement
                Dim channel = root.GetProperty("channel").GetString()
                Dim token = root.GetProperty("bridgeToken").GetString()
                
                If token <> BridgeToken Then
                    Debug.WriteLine("Invalid bridge token, ignoring message.")
                    Return
                End If

                If channel = "NotificationChannel" Then
                    Await HandleNotificationMessageAsync(root, settings, onNotificationChanged)
                ElseIf channel = "TranslationChannel" Then
                    Await HandleTranslationMessageAsync(root)
                End If
            End Using
        Catch ex As Exception
            Debug.WriteLine($"Error handling web message: {ex.Message}")
        End Try
    End Function

    ''' <summary>
    ''' Gestisce la ricezione o chiusura delle notifiche dai messaggi IPC e attiva le notifiche Toast o Popup della UI.
    ''' </summary>
    Private Function HandleNotificationMessageAsync(root As JsonElement, settings As SettingsController, onNotificationChanged As Action(Of String, Boolean)) As Task
        Dim type = root.GetProperty("type").GetString()
        Dim notificationId = root.GetProperty("id").GetString()
        
        Debug.WriteLine($"[NotificationChannel] accountId={Id}, type={type}, id={notificationId}")

        If type = "NOTIFICATION_RECEIVED" Then
            ' Limita le dimensioni del set per prevenire memory leak prolungato con espulsione FIFO degli ID piu vecchi
            While ActiveNotificationIds.Count >= MaxActiveNotificationIds
                Dim oldest = ActiveNotificationIds.FirstOrDefault()
                If oldest IsNot Nothing Then
                    ActiveNotificationIds.Remove(oldest)
                Else
                    Exit While
                End If
            End While
            ActiveNotificationIds.Add(notificationId)
            HasNotification = True
            onNotificationChanged?.Invoke(Id, True)

            ' Se la modalità Non Disturbare è attiva (TODO #47), silenziamento totale di Toast e Popup
            If settings.IsDndActive Then
                Debug.WriteLine($"[DND Active] Toast & Popup suppressed for account {Id}")
                Return Task.CompletedTask
            End If

            Dim title = root.GetProperty("title").GetString()
            Dim body = root.GetProperty("body").GetString()

            Try
                Dim builder As New ToastContentBuilder()
                builder.AddText(title)
                builder.AddText(body)
                builder.AddArgument("accountId", Id)
                builder.AddArgument("notificationId", notificationId)
                builder.Show()
            Catch ex As Exception
                Debug.WriteLine($"Failed to show toast notification: {ex.Message}")
            End Try

            If settings.ShowMessagePopup Then
                Try
                    Dim op = Application.Current?.Dispatcher.BeginInvoke(Sub()
                        Dim popup As New MessagePopup(Id, title, body, Platform)
                        popup.Show()
                    End Sub)
                Catch ex As Exception
                    Debug.WriteLine($"Failed to show popup: {ex.Message}")
                End Try
            End If

        ElseIf type = "NOTIFICATION_CLOSED" Then
            ActiveNotificationIds.Remove(notificationId)
            HasNotification = (UnreadCount > 0 OrElse ActiveNotificationIds.Count > 0)
            onNotificationChanged?.Invoke(Id, HasNotification)
        ElseIf type = "UNREAD_COUNT_CHANGED" Then
            Dim count As Integer = 0
            Dim unreadNode As JsonElement = Nothing
            If root.TryGetProperty("unreadCount", unreadNode) AndAlso unreadNode.ValueKind = JsonValueKind.Number Then
                count = unreadNode.GetInt32()
            End If
            Me.UnreadCount = count
            HasNotification = (count > 0 OrElse ActiveNotificationIds.Count > 0)
            onNotificationChanged?.Invoke(Id, HasNotification)
        ElseIf type = "ONLINE_STATUS_CHANGED" Then
            Dim online As Boolean = False
            Dim onlineNode As JsonElement = Nothing
            If root.TryGetProperty("isOnline", onlineNode) AndAlso (onlineNode.ValueKind = JsonValueKind.True OrElse onlineNode.ValueKind = JsonValueKind.False) Then
                online = onlineNode.GetBoolean()
            End If
            Dim statusText As String = ""
            Dim statusNode As JsonElement = Nothing
            If root.TryGetProperty("statusText", statusNode) AndAlso statusNode.ValueKind = JsonValueKind.String Then
                statusText = statusNode.GetString()
            End If
            Me.IsContactOnline = online
            Me.ContactOnlineStatusText = statusText
        End If
        Return Task.CompletedTask
    End Function

    ''' <summary>
    ''' Risolve un deep link Telegram (tg:// o https://t.me/) trasformandolo nel percorso appropriato per Telegram Web K.
    ''' </summary>
    Public Shared Function ResolveTelegramUrl(rawUri As String) As String
        If String.IsNullOrWhiteSpace(rawUri) Then Return "https://web.telegram.org/k/"
        Dim trimmed = rawUri.Trim()

        ' 1. Gestione protocollo tg://
        If trimmed.StartsWith("tg://", StringComparison.OrdinalIgnoreCase) Then
            Dim lower = trimmed.ToLowerInvariant()
            If lower.StartsWith("tg://resolve?") Then
                Dim query = trimmed.Substring("tg://resolve?".Length)
                Dim domainMatch = System.Text.RegularExpressions.Regex.Match(query, "(?:^|&)domain=([^&]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                Dim postMatch = System.Text.RegularExpressions.Regex.Match(query, "(?:^|&)post=([^&]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                If domainMatch.Success Then
                    Dim domain = domainMatch.Groups(1).Value
                    If postMatch.Success Then
                        Return $"https://web.telegram.org/a/#@{domain}/{postMatch.Groups(1).Value}"
                    Else
                        Return $"https://web.telegram.org/a/#@{domain}"
                    End If
                End If
                Return "https://web.telegram.org/a/#?tgaddr=" & Uri.EscapeDataString(trimmed)
            ElseIf lower.StartsWith("tg://join?") OrElse lower.StartsWith("tg://msg_url?") Then
                Return "https://web.telegram.org/a/#?tgaddr=" & Uri.EscapeDataString(trimmed)
            Else
                Return "https://web.telegram.org/a/#?tgaddr=" & Uri.EscapeDataString(trimmed)
            End If
        End If

        ' 2. Gestione link web https://t.me/ o https://telegram.me/
        Try
            Dim uriObj As New Uri(trimmed)
            Dim host = uriObj.Host.ToLowerInvariant()
            If host = "t.me" OrElse host.EndsWith(".t.me") OrElse host = "telegram.me" OrElse host.EndsWith(".telegram.me") Then
                Dim path = uriObj.AbsolutePath.TrimStart("/"c)
                If String.IsNullOrEmpty(path) Then Return "https://web.telegram.org/a/"
                
                If path.StartsWith("+") OrElse path.StartsWith("joinchat/", StringComparison.OrdinalIgnoreCase) Then
                    Return "https://web.telegram.org/a/#?tgaddr=" & Uri.EscapeDataString($"tg://join?invite={path.Replace("joinchat/", "").TrimStart("+"c)}")
                ElseIf path.StartsWith("c/", StringComparison.OrdinalIgnoreCase) Then
                    Return $"https://web.telegram.org/a/#{path}"
                ElseIf path.StartsWith("s/", StringComparison.OrdinalIgnoreCase) Then
                    Return $"https://web.telegram.org/a/#@{path.Substring(2)}"
                Else
                    Return $"https://web.telegram.org/a/#@{path}"
                End If
            End If
        Catch
        End Try

        Return trimmed
    End Function

    ''' <summary>
    ''' Elabora le richieste di traduzione singole o batch provenienti dal layer JavaScript della WebView.
    ''' </summary>
    Private Async Function HandleTranslationMessageAsync(root As JsonElement) As Task
        Dim id = root.GetProperty("id").GetString()
        Dim jsonId = JsonSerializer.Serialize(id)
        Dim targetLang = root.GetProperty("targetLang").GetString()
        
        Dim isBatch = root.TryGetProperty("type", Nothing) AndAlso root.GetProperty("type").GetString() = "BATCH_TRANSLATE"

        If isBatch Then
            Dim success As Boolean = False
            Dim partsJson As String = "[]"
            Try
                Dim textsElement = root.GetProperty("texts")
                Dim texts As New List(Of String)()
                For Each item In textsElement.EnumerateArray()
                    texts.Add(item.GetString())
                Next
                
                Dim cleanParts = Await AppLocalizations.TranslateBatch(texts, targetLang)
                partsJson = JsonSerializer.Serialize(cleanParts)
                success = True
            Catch ex As Exception
                Debug.WriteLine($"Error doing batch translation: {ex.Message}")
            End Try

            If success Then
                Await WebView.Dispatcher.InvokeAsync(Async Function()
                    Await WebView.CoreWebView2.ExecuteScriptAsync(
                        $"if (window.onBatchTranslationReceived) {{ window.onBatchTranslationReceived({jsonId}, {partsJson}, true); }}"
                    )
                End Function)
            Else
                Await WebView.Dispatcher.InvokeAsync(Async Function()
                    Await WebView.CoreWebView2.ExecuteScriptAsync(
                        $"if (window.onBatchTranslationReceived) {{ window.onBatchTranslationReceived({jsonId}, [], false); }}"
                    )
                End Function)
            End If
        Else
            Dim success As Boolean = False
            Dim jsonResult As String = "null"
            Try
                Dim text = root.GetProperty("text").GetString()
                Dim quotedText As String = Nothing
                Dim quotedNode As JsonElement = Nothing
                If root.TryGetProperty("quotedText", quotedNode) AndAlso quotedNode.ValueKind <> JsonValueKind.Null Then
                    quotedText = quotedNode.GetString()
                End If

                Dim resultStr As String
                If quotedText IsNot Nothing Then
                    Dim transQuoted = Await AppLocalizations.TranslateSingle(quotedText, targetLang)
                    Dim transResponse = Await AppLocalizations.TranslateSingle(text, targetLang)
                    
                    Dim dict As New Dictionary(Of String, String) From {
                        {"quoted", transQuoted},
                        {"response", transResponse}
                    }
                    resultStr = JsonSerializer.Serialize(dict)
                Else
                    resultStr = Await AppLocalizations.TranslateSingle(text, targetLang)
                End If
                
                jsonResult = JsonSerializer.Serialize(resultStr)
                success = True
            Catch ex As Exception
                Debug.WriteLine($"Error doing single translation: {ex.Message}")
            End Try

            If success Then
                Await WebView.Dispatcher.InvokeAsync(Async Function()
                    Await WebView.CoreWebView2.ExecuteScriptAsync(
                        $"if (window.onTranslationReceived) {{ window.onTranslationReceived({jsonId}, {jsonResult}, true); }}"
                    )
                End Function)
            Else
                Dim emptyJson = JsonSerializer.Serialize(String.Empty)
                Await WebView.Dispatcher.InvokeAsync(Async Function()
                    Await WebView.CoreWebView2.ExecuteScriptAsync(
                        $"if (window.onTranslationReceived) {{ window.onTranslationReceived({jsonId}, {emptyJson}, false); }}"
                    )
                End Function)
            End If
        End If
    End Function

    ''' <summary>
    ''' Notifica alla WebView2 l'aggiornamento della lingua di destinazione per le traduzioni messaggi.
    ''' </summary>
    Public Async Function UpdateWebviewLanguageAsync(langCode As String, langName As String, translateTooltipLabel As String, enableHover As Boolean) As Task
        If WebView IsNot Nothing AndAlso WebView.CoreWebView2 IsNot Nothing Then
            Try
                Dim jsonLangCode = JsonSerializer.Serialize(If(langCode, "en"))
                Dim jsonLangName = JsonSerializer.Serialize(If(langName, "English"))
                Dim jsonTooltip = JsonSerializer.Serialize(If(translateTooltipLabel, "Translate"))
                Dim hoverBool = If(enableHover, "true", "false")

                Await WebView.CoreWebView2.ExecuteScriptAsync(
                    $"if (window.setTargetLanguage) {{ window.setTargetLanguage({jsonLangCode}, {jsonLangName}, {jsonTooltip}, {hoverBool}); }}"
                )
            Catch
            End Try
        End If
    End Function

    ''' <summary>
    ''' Applica lo script per la sincronizzazione del tema (Scuro o Chiaro) all'interno della WebView2.
    ''' </summary>
    Public Async Function ApplyThemeAsync(isDark As Boolean) As Task
        If WebView IsNot Nothing AndAlso WebView.CoreWebView2 IsNot Nothing Then
            Try
                If IsTelegram Then
                    Await WebView.CoreWebView2.ExecuteScriptAsync(If(isDark, ThemeJsScripts.TelegramDarkModeJS, ThemeJsScripts.TelegramLightModeJS))
                Else
                    Await WebView.CoreWebView2.ExecuteScriptAsync(If(isDark, ThemeJsScripts.DarkModeJS, ThemeJsScripts.LightModeJS))
                End If
            Catch ex As Exception
                Debug.WriteLine($"Failed to apply theme for account {Id}: {ex.Message}")
            End Try
        End If
    End Function

    ''' <summary>
    ''' Notifica al motore WebView2 di svuotare la disk cache e cronologia temporanea tramite API nativa.
    ''' </summary>
    Public Async Function ClearBrowsingCacheAsync() As Task
        Try
            If WebView IsNot Nothing AndAlso WebView.CoreWebView2 IsNot Nothing AndAlso WebView.CoreWebView2.Profile IsNot Nothing Then
                Await WebView.CoreWebView2.Profile.ClearBrowsingDataAsync(
                    CoreWebView2BrowsingDataKinds.DiskCache Or 
                    CoreWebView2BrowsingDataKinds.DownloadHistory
                )
            End If
        Catch ex As Exception
            Debug.WriteLine($"ClearBrowsingCacheAsync error: {ex.Message}")
        End Try
    End Function

    ''' <summary>
    ''' Rimuove tutti gli event handler registrati sulla WebView2 e libera le risorse allocate.
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        Try
            If WebView IsNot Nothing Then
                If WebView.CoreWebView2 IsNot Nothing Then
                    Try
                        If _processFailedHandler IsNot Nothing Then
                            RemoveHandler WebView.CoreWebView2.ProcessFailed, _processFailedHandler
                            _processFailedHandler = Nothing
                        End If
                        If _permissionRequestedHandler IsNot Nothing Then
                            RemoveHandler WebView.CoreWebView2.PermissionRequested, _permissionRequestedHandler
                            _permissionRequestedHandler = Nothing
                        End If
                        If _newWindowRequestedHandler IsNot Nothing Then
                            RemoveHandler WebView.CoreWebView2.NewWindowRequested, _newWindowRequestedHandler
                            _newWindowRequestedHandler = Nothing
                        End If
                        If _navigationStartingHandler IsNot Nothing Then
                            RemoveHandler WebView.CoreWebView2.NavigationStarting, _navigationStartingHandler
                            _navigationStartingHandler = Nothing
                        End If
                        If _webMessageReceivedHandler IsNot Nothing Then
                            RemoveHandler WebView.CoreWebView2.WebMessageReceived, _webMessageReceivedHandler
                            _webMessageReceivedHandler = Nothing
                        End If
                        If _navigationCompletedHandler IsNot Nothing Then
                            RemoveHandler WebView.CoreWebView2.NavigationCompleted, _navigationCompletedHandler
                            _navigationCompletedHandler = Nothing
                        End If
                    Catch
                        ' Se il CoreWebView2 è in stato invalidato a causa di un crash, la rimozione degli handler potrebbe sollevare eccezioni
                    End Try
                End If
            End If

            If WebView IsNot Nothing Then
                Dim parentGrid = TryCast(WebView.Parent, System.Windows.Controls.Grid)
                parentGrid?.Children.Remove(WebView)
                WebView.Dispose()
                WebView = Nothing
            End If

            _initTask = Nothing
            _isCrashed = False
            ActiveNotificationIds.Clear()
        Catch ex As Exception
            Debug.WriteLine($"Error disposing AppAccounts: {ex.Message}")
        End Try
    End Sub
End Class

