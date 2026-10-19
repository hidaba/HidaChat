Imports System.Threading.Tasks
Imports System.IO
Imports System.ComponentModel
Imports System.Text.Json.Serialization
Imports System.Windows.Media
Imports System.Security.Cryptography
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

    ''' <summary>Identificativo univoco dell'account (es. account_1680000000000).</summary>
    <JsonPropertyName("id")>
    Public Property Id As String

    Private _platformType As ChatPlatform = ChatPlatform.WhatsApp

    ''' <summary>Tipo di piattaforma tipizzato come enum (TODO #68).</summary>
    <JsonIgnore>
    Public Property PlatformType As ChatPlatform
        Get
            Return _platformType
        End Get
        Set(value As ChatPlatform)
            SetPlatform(value)
        End Set
    End Property

    ''' <summary>Tipo di piattaforma ("WhatsApp", "Telegram", "OpenClaw" o "Hermes").</summary>
    <JsonPropertyName("platform")>
    Public Property Platform As String
        Get
            Return PlatformDefinition.Id
        End Get
        Set(value As String)
            SetPlatform(PlatformRegistry.FromKey(value))
        End Set
    End Property

    ''' <summary>Definizione ricca e tipizzata della piattaforma corrente (TODO #68).</summary>
    <JsonIgnore>
    Public ReadOnly Property PlatformDefinition As PlatformDefinition
        Get
            Return PlatformRegistry.Get(_platformType)
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property IsHermes As Boolean
        Get
            Return _platformType = ChatPlatform.Hermes
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property IsOpenClaw As Boolean
        Get
            Return _platformType = ChatPlatform.OpenClaw
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property IsTelegram As Boolean
        Get
            Return _platformType = ChatPlatform.Telegram
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property IsWhatsApp As Boolean
        Get
            Return _platformType = ChatPlatform.WhatsApp
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property OpenClawSettingsVisibility As Visibility
        Get
            Return If(IsOpenClaw, Visibility.Visible, Visibility.Collapsed)
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property HermesSettingsVisibility As Visibility
        Get
            Return If(IsHermes, Visibility.Visible, Visibility.Collapsed)
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property AgentSettingsVisibility As Visibility
        Get
            Return If(PlatformDefinition.IsAiGateway, Visibility.Visible, Visibility.Collapsed)
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property AuthTokenLabel As String
        Get
            Return PlatformDefinition.AuthTokenLabel
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property ServerUrlTooltip As String
        Get
            Return PlatformDefinition.ServerUrlTooltip
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property AuthTokenTooltip As String
        Get
            Return PlatformDefinition.AuthTokenTooltip
        End Get
    End Property

    Private _serverUrl As String = ""
    ''' <summary>URL del gateway o server (OpenClaw, Hermes, locale o remoto via Tailscale).</summary>
    <JsonPropertyName("serverUrl")>
    Public Property ServerUrl As String
        Get
            If String.IsNullOrWhiteSpace(_serverUrl) Then
                Return PlatformDefinition.DefaultUrl
            End If
            Return _serverUrl
        End Get
        Set(value As String)
            Dim defaultUrl = PlatformDefinition.DefaultUrl
            Dim cleanVal = If(String.IsNullOrWhiteSpace(value), defaultUrl, value.Trim())
            If _serverUrl <> cleanVal Then
                _serverUrl = cleanVal
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(ServerUrl)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(WebUrl)))
                If Not TailscaleIntegration Then
                    UpdateWebResourceAuthFilter()
                End If
            End If
        End Set
    End Property

    Private _authToken As String = ""
    ''' <summary>Token di autenticazione o API Key del gateway (OpenClaw / Hermes Agent).</summary>
    <JsonPropertyName("authToken")>
    Public Property AuthToken As String
        Get
            Return If(_authToken, "")
        End Get
        Set(value As String)
            Dim cleanVal = If(value, "").Trim()
            If _authToken <> cleanVal Then
                _authToken = cleanVal
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(AuthToken)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(WebUrl)))
                UpdateWebResourceAuthFilter()
            End If
        End Set
    End Property

    Private _tailscaleIntegration As Boolean = False
    ''' <summary>Se True, instrada il traffico verso la Tailnet tramite il nodo tsnet embedded.</summary>
    <JsonPropertyName("tailscaleIntegration")>
    Public Property TailscaleIntegration As Boolean
        Get
            Return _tailscaleIntegration
        End Get
        Set(value As Boolean)
            If _tailscaleIntegration <> value Then
                _tailscaleIntegration = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(TailscaleIntegration)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(WebUrl)))
                If Not _tailscaleIntegration Then
                    UpdateWebResourceAuthFilter()
                End If
            End If
        End Set
    End Property

    Private _localProxyPort As Integer = 0
    ''' <summary>Porta TCP locale fissa riservata al proxy tsnet per preservare l'Origin e l'approvazione del dispositivo.</summary>
    <JsonPropertyName("localProxyPort")>
    Public Property LocalProxyPort As Integer
        Get
            Return _localProxyPort
        End Get
        Set(value As Integer)
            If _localProxyPort <> value Then
                _localProxyPort = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(LocalProxyPort)))
            End If
        End Set
    End Property

    Private _localProxyUrl As String = ""
    ''' <summary>URL del proxy locale generato a runtime dal companion tsnetd.</summary>
    <JsonIgnore>
    Public Property LocalProxyUrl As String
        Get
            Return _localProxyUrl
        End Get
        Set(value As String)
            Dim cleanVal = If(value, "").Trim()
            If _localProxyUrl <> cleanVal Then
                _localProxyUrl = cleanVal
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(LocalProxyUrl)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(WebUrl)))
            End If
        End Set
    End Property

    Private _connectionStatusText As String = ""
    <JsonIgnore>
    Public Property ConnectionStatusText As String
        Get
            Return _connectionStatusText
        End Get
        Set(value As String)
            If _connectionStatusText <> value Then
                _connectionStatusText = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(ConnectionStatusText)))
            End If
        End Set
    End Property

    Private _connectionStatusBrush As Brush = BrushCache.GetBrush("#8696a0")
    <JsonIgnore>
    Public Property ConnectionStatusBrush As Brush
        Get
            Return _connectionStatusBrush
        End Get
        Set(value As Brush)
            If _connectionStatusBrush IsNot value Then
                _connectionStatusBrush = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(ConnectionStatusBrush)))
            End If
        End Set
    End Property

    <JsonIgnore>
    Public ReadOnly Property WebUrl As String
        Get
            If PlatformDefinition.IsAiGateway Then
                Dim defaultUrl = PlatformDefinition.DefaultUrl
                Dim base = If(TailscaleIntegration AndAlso Not String.IsNullOrEmpty(LocalProxyUrl), LocalProxyUrl, ServerUrl)
                If String.IsNullOrWhiteSpace(base) Then base = defaultUrl
                If Not base.StartsWith("http://", StringComparison.OrdinalIgnoreCase) AndAlso Not base.StartsWith("https://", StringComparison.OrdinalIgnoreCase) Then
                    base = "http://" & base
                End If
                ' NOTA: Il token di autenticazione non viene accodato in chiaro come query parameter (?token=) per preservare
                ' la riservatezza e il principio 100% portabile (TODO #60). Viene iniettato come header 'Authorization: Bearer <token>' via WebResourceRequested.
                If TailscaleIntegration AndAlso Not String.IsNullOrEmpty(LocalProxyUrl) Then
                    Dim sep = If(base.Contains("?"), "&", "?")
                    base = $"{base}{sep}__htok={Uri.EscapeDataString(TsnetManager.Instance.LocalToken)}"
                End If
                Return base
            Else
                Return PlatformDefinition.DefaultUrl
            End If
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property PlatformIconData As String
        Get
            Return PlatformDefinition.IconSvgPath
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property PlatformColorBrush As Brush
        Get
            Return PlatformDefinition.ThemeBrush
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
                _lastActiveTime = DateTime.UtcNow
                If _isActive Then
                    ResumeFromHibernation()
                End If
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(IsActive)))
            End If
        End Set
    End Property

    Private _isHibernated As Boolean = False
    ''' <summary>Indica se la scheda dell'account è attualmente ibernata in memoria (TODO #50).</summary>
    <JsonIgnore>
    Public Property IsHibernated As Boolean
        Get
            Return _isHibernated
        End Get
        Private Set(value As Boolean)
            If _isHibernated <> value Then
                _isHibernated = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(IsHibernated)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(HibernatedIndicatorVisibility)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(HibernatedTooltip)))
            End If
        End Set
    End Property

    Private _lastActiveTime As DateTime = DateTime.UtcNow
    ''' <summary>Data e ora UTC di ultimo utilizzo o selezione della scheda.</summary>
    <JsonIgnore>
    Public Property LastActiveTime As DateTime
        Get
            Return _lastActiveTime
        End Get
        Set(value As DateTime)
            _lastActiveTime = value
        End Set
    End Property

    <JsonIgnore>
    Public ReadOnly Property HibernatedIndicatorVisibility As Visibility
        Get
            Return If(_isHibernated, Visibility.Visible, Visibility.Collapsed)
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property HibernatedTooltip As String
        Get
            Return If(_isHibernated, "Scheda ibernata (risveglio automatico al click)", String.Empty)
        End Get
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
                NotifyUnreadPropertiesChanged()
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
                NotifyUnreadPropertiesChanged()
            End If
        End Set
    End Property

    Private Sub NotifyUnreadPropertiesChanged()
        Dim notifyAction = Sub()
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(UnreadCount)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(HasNotification)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(HasUnreadBadge)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(UnreadBadgeVisibility)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(UnreadBadgeText)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(UnreadBadgeTextVisibility)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(UnreadBadgeCornerRadius)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(UnreadBadgeMinWidth)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(UnreadBadgeWidth)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(UnreadBadgeHeight)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(UnreadBadgePadding)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(UnreadTooltip)))
        End Sub

        Dim app = Application.Current
        If app IsNot Nothing AndAlso app.Dispatcher IsNot Nothing AndAlso Not app.Dispatcher.CheckAccess() Then
            app.Dispatcher.BeginInvoke(notifyAction)
        Else
            notifyAction()
        End If
    End Sub

    ''' <summary>Indica se visualizzare il badge numerico o il bollino di notifica sulla scheda.</summary>
    <JsonIgnore>
    Public ReadOnly Property HasUnreadBadge As Boolean
        Get
            Return _unreadCount > 0 OrElse _hasNotification
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property UnreadBadgeVisibility As Visibility
        Get
            Return If(HasUnreadBadge, Visibility.Visible, Visibility.Collapsed)
        End Get
    End Property

    ''' <summary>Testo formattato del badge (es. "1", "5", "99+").</summary>
    <JsonIgnore>
    Public ReadOnly Property UnreadBadgeText As String
        Get
            If _unreadCount > 99 Then
                Return "99+"
            ElseIf _unreadCount > 0 Then
                Return _unreadCount.ToString()
            Else
                Return String.Empty
            End If
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property UnreadBadgeTextVisibility As Visibility
        Get
            Return If(_unreadCount > 0, Visibility.Visible, Visibility.Collapsed)
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property UnreadBadgeCornerRadius As CornerRadius
        Get
            If _unreadCount > 0 Then
                Return New CornerRadius(8)
            Else
                Return New CornerRadius(4.5)
            End If
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property UnreadBadgeMinWidth As Double
        Get
            If _unreadCount > 0 Then Return 16.0 Else Return 9.0
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property UnreadBadgeWidth As Double
        Get
            If _unreadCount > 0 Then Return Double.NaN Else Return 9.0
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property UnreadBadgeHeight As Double
        Get
            If _unreadCount > 0 Then Return 16.0 Else Return 9.0
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property UnreadBadgePadding As Thickness
        Get
            If _unreadCount > 0 Then Return New Thickness(4, 0, 4, 0) Else Return New Thickness(0)
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property UnreadTooltip As String
        Get
            If _unreadCount > 1 Then
                Return $"{_unreadCount} messaggi non letti"
            ElseIf _unreadCount = 1 Then
                Return "1 messaggio non letto"
            ElseIf _hasNotification Then
                Return "Nuovi messaggi non letti"
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
                Dim notifyAction = Sub()
                    RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(IsContactOnline)))
                    RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(OnlineDotVisibility)))
                    RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(OnlineStatusDisplay)))
                End Sub
                Dim app = Application.Current
                If app IsNot Nothing AndAlso app.Dispatcher IsNot Nothing AndAlso Not app.Dispatcher.CheckAccess() Then
                    app.Dispatcher.BeginInvoke(notifyAction)
                Else
                    notifyAction()
                End If
            End If
        End Set
    End Property

    <JsonIgnore>
    Public ReadOnly Property OnlineDotVisibility As Visibility
        Get
            Return If(_isContactOnline, Visibility.Visible, Visibility.Collapsed)
        End Get
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
                Dim notifyAction = Sub()
                    RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(ContactOnlineStatusText)))
                    RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(OnlineStatusDisplay)))
                End Sub
                Dim app = Application.Current
                If app IsNot Nothing AndAlso app.Dispatcher IsNot Nothing AndAlso Not app.Dispatcher.CheckAccess() Then
                    app.Dispatcher.BeginInvoke(notifyAction)
                Else
                    notifyAction()
                End If
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

    ''' <summary>Tooltip informativo associato al pallino dello stato online.</summary>
    <JsonIgnore>
    Public ReadOnly Property OnlineTooltip As String
        Get
            Return OnlineStatusDisplay
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
    Private _lastNotificationTime As DateTime = DateTime.MinValue

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

    Private _isDisposed As Boolean = False
    ''' <summary>Indica se l'istanza o le sue risorse WebView2 sono state disposte (#63).</summary>
    <JsonIgnore>
    Public ReadOnly Property IsDisposed As Boolean
        Get
            Return _isDisposed
        End Get
    End Property

    ' Event Handlers fortemente tipizzati per WebView2 (evita memory leak)
    Private _permissionRequestedHandler As EventHandler(Of CoreWebView2PermissionRequestedEventArgs)
    Private _notificationReceivedHandler As EventHandler(Of CoreWebView2NotificationReceivedEventArgs)
    Private _newWindowRequestedHandler As EventHandler(Of CoreWebView2NewWindowRequestedEventArgs)
    Private _navigationStartingHandler As EventHandler(Of CoreWebView2NavigationStartingEventArgs)
    Private _webMessageReceivedHandler As EventHandler(Of CoreWebView2WebMessageReceivedEventArgs)
    Private _navigationCompletedHandler As EventHandler(Of CoreWebView2NavigationCompletedEventArgs)
    Private _webResourceRequestedHandler As EventHandler(Of CoreWebView2WebResourceRequestedEventArgs)
    Private _processFailedHandler As EventHandler(Of CoreWebView2ProcessFailedEventArgs)
    Private _settings As SettingsController

    ' Filtro WebResourceRequested per autorizzazione porte e token locali/gateway (TODO #78)
    Private _activeProxyFilterPattern As String = Nothing
    Private _activeProxyExpectedScheme As String = Nothing
    Private _activeProxyExpectedHost As String = Nothing
    Private _activeProxyExpectedPort As Integer = 0
    Private _activeRequiresLocalToken As Boolean = False
    Private _activeRequiresAuthToken As Boolean = False

    ''' <summary>Percorso base per il salvataggio dei profili WebView2 isolati degli account.</summary>
    Public Shared ReadOnly Property SharedDataDirectory As String
        Get
            Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "webview")
        End Get
    End Property

    ''' <summary>
    ''' Rimuove in modo sicuro le cartelle di cache volatile e diagnostica (ShaderCache, GPUCache, Crashpad)
    ''' senza alterare i file essenziali di accelerazione (Code Cache, Disk Cache, Service Worker) o sessioni/cookie/IndexedDB.
    ''' </summary>
    Public Shared Sub CleanTransientCacheFolders(profileDir As String, Optional purgeDiskAndCodeCache As Boolean = False)
        If String.IsNullOrEmpty(profileDir) OrElse Not Directory.Exists(profileDir) Then Return

        Dim relativeDirsToClean As New List(Of String) From {
            "EBWebView\ShaderCache",
            "EBWebView\GrShaderCache",
            "EBWebView\Crashpad\reports",
            "EBWebView\Crashpad",
            "EBWebView\component_crx_cache",
            "EBWebView\Subresource Filter",
            "EBWebView\Default\GPUCache",
            "EBWebView\Default\DawnGraphiteCache",
            "EBWebView\Default\DawnWebGPUCache",
            "EBWebView\Default\GPUPersistentCache"
        }

        If purgeDiskAndCodeCache Then
            relativeDirsToClean.AddRange({
                "EBWebView\Default\Cache",
                "EBWebView\Default\Code Cache"
            })
        End If

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

    ''' <summary>Genera un identificativo alfanumerico basato su GUID; gli ID esistenti restano invariati.</summary>
    Public Shared Function GenerateId() As String
        Return "account_" & Guid.NewGuid().ToString("N")
    End Function

    ''' <summary>Genera un token crittograficamente sicuro (CSPRNG) a 128 bit per la validazione della comunicazione IPC (#62).</summary>
    Private Shared Function GenerateBridgeToken() As String
        Dim randomBytes(15) As Byte
        RandomNumberGenerator.Fill(randomBytes)
        Return "bt_" & Convert.ToHexString(randomBytes).ToLowerInvariant()
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

    Public Sub New(id As String, name As String, isActive As Boolean, platformType As ChatPlatform)
        Me.Id = id
        Me.Name = name
        Me.IsActive = isActive
        Me.PlatformType = platformType
        BridgeToken = GenerateBridgeToken()
    End Sub

    Private _initTask As Task = Nothing
    Private _closeTask As Task(Of Boolean)
    Private _stopRequested As Boolean
    Private _browserLifetime As WebViewLifetime
    Private _environmentCreationStarted As Boolean
    Private _profilePath As String
    Private _usesNetworkStaging As Boolean
    Private _recoveryQueued As Boolean
    Private _lastRendererReloadUtc As DateTime = DateTime.MinValue

    <JsonIgnore>
    Public ReadOnly Property UsesNetworkStaging As Boolean
        Get
            Return _usesNetworkStaging
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property ProfilePath As String
        Get
            If Not String.IsNullOrEmpty(_profilePath) Then Return _profilePath
            Return NetworkProfileSync.GetEffectiveProfileDir(Id, enableStaging:=True)
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property CanReleaseNetworkLease As Boolean
        Get
            Return _isDisposed AndAlso (BrowserProfileReleased OrElse Not _environmentCreationStarted)
        End Get
    End Property

    <JsonIgnore>
    Public ReadOnly Property BrowserProfileReleased As Boolean
        Get
            Return _isDisposed AndAlso _browserLifetime IsNot Nothing AndAlso _browserLifetime.HasExited
        End Get
    End Property

    ''' <summary>
    ''' Configura l'ambiente isolato della WebView2, inietta gli script JavaScript per l'intercettazione delle notifiche e traduzioni,
    ''' e naviga verso la pagina della piattaforma di messaggistica (WhatsApp Web o Telegram Web).
    ''' </summary>
    Public Function SetupWebViewAsync(settings As SettingsController, onNotificationChanged As Action(Of String, Boolean)) As Task
        If _closeTask IsNot Nothing AndAlso Not _closeTask.IsCompleted Then
            Throw New InvalidOperationException("WebView2 is still closing; initialization postponed.")
        End If
        If Not _isDisposed AndAlso _initTask IsNot Nothing AndAlso Not _initTask.IsFaulted AndAlso Not _initTask.IsCanceled AndAlso Not _isCrashed Then
            Return _initTask
        End If
        _stopRequested = False
        _closeTask = Nothing
        _initTask = SetupWebViewInternalAsync(settings, onNotificationChanged)
        Return _initTask
    End Function

    Private Async Function SetupWebViewInternalAsync(settings As SettingsController, onNotificationChanged As Action(Of String, Boolean)) As Task
        If WebView Is Nothing Then Return
        _isDisposed = False
        _settings = settings

        If _browserLifetime IsNot Nothing Then
            If Not Await _browserLifetime.WaitForExitAsync() Then
                Throw New TimeoutException("Previous WebView2 process still owns the profile. No reset was performed.")
            End If
            _browserLifetime.Dispose()
            _browserLifetime = Nothing
        End If
        ThrowIfSetupStopped()
        _environmentCreationStarted = False

        Dim masterProfileDir = NetworkProfileSync.GetMasterProfileDir(Id)
        If String.IsNullOrEmpty(_profilePath) Then
            _usesNetworkStaging = NetworkProfileSync.IsRunningOnNetwork AndAlso (settings Is Nothing OrElse settings.EnableNetworkProfileStaging)
            _profilePath = If(_usesNetworkStaging, NetworkProfileSync.GetLocalStagingProfileDir(Id), masterProfileDir)
        End If
        Dim profileDir = _profilePath
        If _usesNetworkStaging Then
            ' Acquire BEFORE import, and retain the lease until after the final cold export.
            Dim conflict = NetworkProfileSync.AcquireSessionLock(Id)
            If Not String.IsNullOrEmpty(conflict) Then Throw New IOException(conflict)
            profileDir = NetworkProfileSync.GetLocalStagingProfileDir(Id)
            If Not Await NetworkProfileSync.SyncMasterToLocalStagingAsync(Id) Then
                Throw New IOException("Profile import failed. Existing data were not reset; initialization stopped.")
            End If
        End If
        ThrowIfSetupStopped()
        ColdProfileCopy.RecoverInterruptedPublication(profileDir)
        Directory.CreateDirectory(profileDir)
        AppLogger.LogApp($"[PROFILE_OPEN] account={Id}, requested={profileDir}, staging={_usesNetworkStaging}")
        ' No automatic cache deletion or orphan substitution when reopening a profile.

        ' Controllo preventivo dell'integrità strutturale dello storage IndexedDB a freddo (TODO #76)
        Dim corruptionCheck = StorageIntegrityService.CheckIndexedDbCorruption(profileDir)
        If corruptionCheck.IsCorrupted Then
            AppLogger.LogApp($"[PREVENTIVE_CORRUPTION_DETECTED] account={Id}, name={Name}, reason={corruptionCheck.Reason}")
            Dim ownerWin = Application.Current?.Windows.OfType(Of MainWindow)().FirstOrDefault()
            Await StorageIntegrityService.PerformStorageRecoveryWithDialogAsync(Me, settings?.Localizations, ownerWin)
        End If

        Try
            Dim options As New CoreWebView2EnvironmentOptions()
            Dim effectiveLang = settings.GetEffectiveChromiumLanguage()
            If Not String.IsNullOrEmpty(effectiveLang) Then
                options.Language = effectiveLang
            End If

            Dim browserArgsList As New List(Of String)()
            Dim disabledFeatures As New List(Of String) From {"Translate", "MediaRouter"}
            If settings.EnableSpellcheck Then
                browserArgsList.Add($"--enable-features=Spellcheck --lang={effectiveLang}")
            Else
                disabledFeatures.Add("Spellcheck")
            End If
            browserArgsList.Add($"--disable-features={String.Join(",", disabledFeatures)}")

            ' Limitazione cache Chromium via AdditionalBrowserArguments (TODO #71)
            If settings IsNot Nothing AndAlso settings.DiskCacheLimitMb > 0 Then
                Dim diskCacheBytes As Long = CLng(settings.DiskCacheLimitMb) * 1024L * 1024L
                browserArgsList.Add($"--disk-cache-size={diskCacheBytes}")
            End If

            options.AdditionalBrowserArguments = String.Join(" ", browserArgsList).Trim()

            _environmentCreationStarted = True
            Dim accountEnv = Await CoreWebView2Environment.CreateAsync(Nothing, profileDir, options)
            ThrowIfSetupStopped()
            
            Await WebView.EnsureCoreWebView2Async(accountEnv)
            _browserLifetime = New WebViewLifetime(WebView.CoreWebView2, Id)
            ThrowIfSetupStopped()
            _isCrashed = False
            AppLogger.LogApp($"[PROFILE_READY] account={Id}, actual={accountEnv.UserDataFolder}, runtime={accountEnv.BrowserVersionString}, private={WebView.CoreWebView2.Profile.IsInPrivateModeEnabled}")
            
            WebView.CoreWebView2.Settings.IsWebMessageEnabled = True
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = True
            WebView.CoreWebView2.Settings.IsGeneralAutofillEnabled = True
            WebView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = False
            
            ' Registra listener crash a livello di CoreWebView2
            _processFailedHandler = Sub(sender, e)
                HandleProcessFailed(e)
            End Sub
            AddHandler WebView.CoreWebView2.ProcessFailed, _processFailedHandler
            
            ' Monitoraggio diagnostico risposte HTTP per intercettare disconnessioni API o errori 4xx/5xx
            AddHandler WebView.CoreWebView2.WebResourceResponseReceived, Sub(sender, respArgs)
                Try
                    Dim status = respArgs.Response.StatusCode
                    Dim uri = respArgs.Request.Uri
                    If status >= 400 Then
                        AppLogger.LogWhatsApp($"[{Id}] HTTP {status} su {uri}")
                    End If
                Catch
                End Try
            End Sub

            ' Salvataggio riferimenti handler per poterli rimuovere in Dispose()
            _permissionRequestedHandler = Sub(sender, e)
                If e.PermissionKind = CoreWebView2PermissionKind.Notifications Then
                    ' Blocca i permessi notifiche native del browser per evitare la comparsa di popup doppi dell'account
                    e.State = CoreWebView2PermissionState.Deny
                    e.Handled = True
                ElseIf e.PermissionKind = CoreWebView2PermissionKind.PersistentStorage AndAlso
                       SessionRecoveryPolicy.IsTrustedStorageOrigin(e.Uri) Then
                    e.State = CoreWebView2PermissionState.Allow
                    e.Handled = True
                    AppLogger.LogApp($"[PERSISTENT_STORAGE_ALLOWED] account={Id}")
                End If
            End Sub
            AddHandler WebView.CoreWebView2.PermissionRequested, _permissionRequestedHandler

            _notificationReceivedHandler = Sub(sender, e)
                Try
                    ' Intercetta e sopprime il popup/interfaccia predefinita di notifica di WebView2 per l'account
                    e.Handled = True
                    Dim notif = e.Notification
                    If notif IsNot Nothing Then
                        Try
                            notif.ReportShown()
                            notif.ReportClosed()
                        Catch
                        End Try
                    End If
                Catch ex As Exception
                    Debug.WriteLine($"NotificationReceived suppression error: {ex.Message}")
                End Try
            End Sub
            AddHandler WebView.CoreWebView2.NotificationReceived, _notificationReceivedHandler

            Try
                If WebView.CoreWebView2.Profile IsNot Nothing Then
                    Await WebView.CoreWebView2.Profile.SetPermissionStateAsync(CoreWebView2PermissionKind.Notifications, "https://web.whatsapp.com", CoreWebView2PermissionState.Deny)
                    Await WebView.CoreWebView2.Profile.SetPermissionStateAsync(CoreWebView2PermissionKind.Notifications, "https://web.telegram.org", CoreWebView2PermissionState.Deny)
                    ThrowIfSetupStopped()
                    Dim accountUri As Uri = Nothing
                    If Uri.TryCreate(WebUrl, UriKind.Absolute, accountUri) Then
                        Dim origin = accountUri.GetLeftPart(UriPartial.Authority)
                        If Not origin.Equals("https://web.whatsapp.com", StringComparison.OrdinalIgnoreCase) AndAlso
                           Not origin.Equals("https://web.telegram.org", StringComparison.OrdinalIgnoreCase) Then
                            Await WebView.CoreWebView2.Profile.SetPermissionStateAsync(CoreWebView2PermissionKind.Notifications, origin, CoreWebView2PermissionState.Deny)
                            ThrowIfSetupStopped()
                        End If
                    End If
                    For Each origin In {"https://web.whatsapp.com", "https://web.telegram.org"}
                        Await WebView.CoreWebView2.Profile.SetPermissionStateAsync(CoreWebView2PermissionKind.PersistentStorage, origin, CoreWebView2PermissionState.Allow)
                        ThrowIfSetupStopped()
                    Next
                End If
            Catch ex As Exception
                Debug.WriteLine($"Profile.SetPermissionStateAsync warning: {ex.Message}")
            End Try

            ThrowIfSetupStopped()
            Dim healthScript = EmbeddedScriptLoader.GetEmbeddedString("session-health.js").Replace("$$BRIDGE_TOKEN$$", JsonSerializer.Serialize(BridgeToken))
            Dim initScript = healthScript & vbCrLf & NotificationJsScripts.GetNotificationOverrideJS(BridgeToken)
            Dim extraInitScript = PlatformDefinition.GetInitScript(BridgeToken)
            If Not String.IsNullOrEmpty(extraInitScript) Then
                initScript &= vbCrLf & extraInitScript
            End If
            Await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(initScript)
            ThrowIfSetupStopped()

            _navigationStartingHandler = Sub(sender, e)
                If String.IsNullOrEmpty(e.Uri) Then Return
                If e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase) Then Return

                Dim resolvedTarget = PlatformDefinition.ResolveDeepLink(e.Uri)
                If Not String.IsNullOrEmpty(resolvedTarget) Then
                    e.Cancel = True
                    WebView.CoreWebView2.Navigate(resolvedTarget)
                    Return
                End If

                ' Validazione schema URI su navigazione (#61)
                Dim navUri As Uri = Nothing
                If Uri.TryCreate(e.Uri, UriKind.Absolute, navUri) Then
                    Dim allowedNavSchemes As String() = {Uri.UriSchemeHttp, Uri.UriSchemeHttps, "data", "blob", "about"}
                    If Not allowedNavSchemes.Contains(navUri.Scheme.ToLowerInvariant()) Then
                        e.Cancel = True
                        Debug.WriteLine($"[Security] Blocked unsafe navigation scheme: {navUri.Scheme} for URI: {e.Uri}")
                        Return
                    End If
                End If
            End Sub
            AddHandler WebView.CoreWebView2.NavigationStarting, _navigationStartingHandler

            _newWindowRequestedHandler = Sub(sender, e)
                Try
                    Dim uriStr = e.Uri
                    ' Consenti dialoghi web, popup script, OAuth e window.open dinamici
                    If String.IsNullOrWhiteSpace(uriStr) OrElse uriStr.Equals("about:blank", StringComparison.OrdinalIgnoreCase) Then
                        e.Handled = False
                        Return
                    End If

                    Dim resolvedTarget = PlatformDefinition.ResolveDeepLink(uriStr)
                    If Not String.IsNullOrEmpty(resolvedTarget) Then
                        e.Handled = True
                        WebView.CoreWebView2.Navigate(resolvedTarget)
                        Return
                    End If

                    Dim uri As Uri = Nothing
                    If Not Uri.TryCreate(uriStr, UriKind.Absolute, uri) Then
                        e.Handled = True
                        Return
                    End If

                    ' Consenti navigazione interna se l'host è autorizzato per la piattaforma (es. gateway OpenClaw/Hermes)
                    If PlatformDefinition.IsAllowedNewWindowHost(uri, ServerUrl) Then
                        e.Handled = True
                        WebView.CoreWebView2.Navigate(uriStr)
                        Return
                    End If

                    ' Qualsiasi altro link esterno o target="_blank" (inclusi link whatsapp.com/faq o esterni in chat)
                    ' viene aperto nel browser predefinito di sistema previa validazione rigorosa dello schema URI (#61)
                    e.Handled = True
                    Dim allowedSchemes As String() = {Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeMailto}
                    If allowedSchemes.Contains(uri.Scheme.ToLowerInvariant()) Then
                        System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo(uriStr) With {
                            .UseShellExecute = True
                        })
                    Else
                        Debug.WriteLine($"[Security] Blocked unsafe URI scheme in NewWindowRequested: {uri.Scheme} for '{uriStr}'")
                    End If
                Catch ex As Exception
                    Debug.WriteLine($"Error in _newWindowRequestedHandler: {ex.Message}")
                    e.Handled = False
                End Try
            End Sub
            AddHandler WebView.CoreWebView2.NewWindowRequested, _newWindowRequestedHandler

            _webMessageReceivedHandler = Async Sub(sender, e)
                Try
                    If _isDisposed OrElse WebView Is Nothing OrElse WebView.CoreWebView2 Is Nothing Then Return
                    Await HandleWebMessageAsync(e.WebMessageAsJson, e.Source, settings, onNotificationChanged)
                Catch ex As Exception
                    Debug.WriteLine($"Error in _webMessageReceivedHandler for account {Id}: {ex.Message}")
                    Application.LogUnhandledException($"AppAccounts._webMessageReceivedHandler (account={Id})", ex)
                End Try
            End Sub
            AddHandler WebView.CoreWebView2.WebMessageReceived, _webMessageReceivedHandler

            _navigationCompletedHandler = Async Sub(sender, e)
                Try
                    If _isDisposed OrElse WebView Is Nothing OrElse WebView.CoreWebView2 Is Nothing Then Return

                    AppLogger.LogApp($"[NavigationCompleted] Account {Id} ({Name}) - IsSuccess={e.IsSuccess}, WebErrorStatus={e.WebErrorStatus}, Uri={WebView?.Source}")

                    If PlatformDefinition.IsAiGateway Then
                        If Not e.IsSuccess Then
                            Await HandleHermesOpenClawNavigationFailureAsync(e.WebErrorStatus.ToString(), settings)
                            Return
                        Else
                            ' Controllo pagina bianca o errore proxy 502/504 (TODO #75)
                            Try
                                Dim checkJs = "(function() { try { var b = document.body; if (!b) return { isBlank: true, reason: 'no_body' }; var t = (b.innerText || '').trim(); var c = b.childElementCount; var title = (document.title || '').trim(); if (c === 0 && t.length === 0) return { isBlank: true, reason: 'empty_body' }; if (title.indexOf('502 Bad Gateway') >= 0 || title.indexOf('504 Gateway Timeout') >= 0 || t.indexOf('502 Bad Gateway') === 0 || t.indexOf('504 Gateway Timeout') === 0) return { isBlank: true, reason: 'proxy_gateway_error' }; return { isBlank: false }; } catch(e) { return { isBlank: false }; } })()"
                                Dim jsonResult = Await WebView.CoreWebView2.ExecuteScriptAsync(checkJs)
                                If Not String.IsNullOrWhiteSpace(jsonResult) AndAlso jsonResult <> "null" Then
                                    Using doc = JsonDocument.Parse(jsonResult)
                                        Dim isBlankNode As JsonElement = Nothing
                                        If doc.RootElement.TryGetProperty("isBlank", isBlankNode) AndAlso isBlankNode.GetBoolean() Then
                                            Dim reasonNode As JsonElement = Nothing
                                            doc.RootElement.TryGetProperty("reason", reasonNode)
                                            Dim r = If(reasonNode.ValueKind <> JsonValueKind.Undefined, reasonNode.GetString(), "blank_page")
                                            Await HandleHermesOpenClawNavigationFailureAsync(r, settings)
                                            Return
                                        End If
                                    End Using
                                End If
                            Catch exCheck As Exception
                                Debug.WriteLine($"Error checking blank page on Hermes/OpenClaw: {exCheck.Message}")
                            End Try
                        End If
                    End If

                    If e.IsSuccess Then
                        If Not PlatformDefinition.IsAiGateway Then
                            ' Iniezione di sicurezza/fallback di notification.js se non precedentemente caricato
                            Try
                                If WebView?.CoreWebView2 IsNot Nothing Then
                                    Await WebView.CoreWebView2.ExecuteScriptAsync(NotificationJsScripts.GetNotificationOverrideJS(BridgeToken))
                                End If
                            Catch exScript As Exception
                                Debug.WriteLine($"Error injecting notification script for account {Id}: {exScript.Message}")
                            End Try

                            If _isDisposed OrElse WebView?.CoreWebView2 Is Nothing Then Return

                            Dim brightnessDark = settings.IsDarkThemeEffective
                            Dim themeScript = PlatformDefinition.GetThemeScript(brightnessDark)
                            If Not String.IsNullOrEmpty(themeScript) Then
                                Await WebView.CoreWebView2.ExecuteScriptAsync(themeScript)
                            End If

                            If _isDisposed OrElse WebView?.CoreWebView2 Is Nothing Then Return

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
                        End If

                        If _isDisposed OrElse WebView?.CoreWebView2 Is Nothing Then Return

                        ' Iniezione CSS personalizzato utente (TODO #43)
                        Await ApplyCustomCssAsync(settings.CustomCss, settings.EnableCustomCss)
                    End If
                Catch ex As Exception
                    Debug.WriteLine($"Error in _navigationCompletedHandler for account {Id}: {ex.Message}")
                    Application.LogUnhandledException($"AppAccounts._navigationCompletedHandler (account={Id})", ex)
                End Try
            End Sub
            AddHandler WebView.CoreWebView2.NavigationCompleted, _navigationCompletedHandler

            If TailscaleIntegration AndAlso PlatformDefinition.RequiresLocalProxy Then
                Await EnsureLocalProxyAsync(forceUpdate:=True)
            ElseIf PlatformDefinition.RequiresLocalProxy Then
                UpdateWebResourceAuthFilter()
            End If

            ThrowIfSetupStopped()
            WebView.CoreWebView2.Navigate(WebUrl)

        Catch ex As Exception
            Application.LogUnhandledException($"PROFILE_INIT_FAILED account={Id}", ex)
            Throw
        End Try
    End Function

    ''' <summary>
    ''' Intercetta i crash del processo di rendering o del processo browser principale di WebView2 ed avvia il ripristino automatico.
    ''' </summary>
    Private Sub HandleProcessFailed(e As CoreWebView2ProcessFailedEventArgs)
        If _isDisposed OrElse _stopRequested Then Return
        Try
            Dim kind = e.ProcessFailedKind.ToString()
            AppLogger.LogApp($"[PROCESS_FAILED] account={Id}, kind={kind}, reason={e.Reason}, exitCode={e.ExitCode}")
            Dim action = SessionRecoveryPolicy.ForFailure(kind)
            If action = SessionRecoveryPolicy.RecoveryAction.Observe Then
                AppLogger.LogApp($"[RECOVERY_OBSERVE] account={Id}, kind={kind}; browser/profile left intact.")
                Return
            End If
            If _recoveryQueued Then Return
            If action = SessionRecoveryPolicy.RecoveryAction.Reload AndAlso
               DateTime.UtcNow - _lastRendererReloadUtc < TimeSpan.FromSeconds(30) Then
                AppLogger.LogApp($"[RECOVERY_RATE_LIMIT] account={Id}; repeated renderer failure, use manual reload.")
                Return
            End If
            _recoveryQueued = True
            If action = SessionRecoveryPolicy.RecoveryAction.Recreate Then _isCrashed = True
            ' Let the WebView2 callback unwind before reloading or disposing controls.
            Application.Current.Dispatcher.BeginInvoke(Sub()
                Try
                    If _isDisposed OrElse _stopRequested Then Return
                    If action = SessionRecoveryPolicy.RecoveryAction.Reload Then
                        Try
                            _lastRendererReloadUtc = DateTime.UtcNow
                            WebView.CoreWebView2.Reload()
                            Return
                        Catch ex As Exception
                            AppLogger.LogApp($"[RELOAD_FAILED] account={Id}: {ex.Message}")
                        End Try
                    End If
                    _isCrashed = True
                    RaiseEvent ProcessFailedRecoveryRequested(Me, e)
                Finally
                    _recoveryQueued = False
                End Try
            End Sub)
        Catch ex As Exception
            Application.LogUnhandledException($"HandleProcessFailed account={Id}", ex)
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
    ''' Assicura che la rotta tsnet sia registrata ed attiva per questo account, configurando il filtro WebView2 con porta vincolata (TODO #78).
    ''' </summary>
    Public Async Function EnsureLocalProxyAsync(Optional forceUpdate As Boolean = False) As Task(Of Integer)
        If TailscaleIntegration AndAlso PlatformDefinition.RequiresLocalProxy Then
            Try
                Dim defaultPort = PlatformDefinition.DefaultPort
                Dim preferred = If(LocalProxyPort > 0, LocalProxyPort, defaultPort)
                Dim port = Await TsnetManager.Instance.EnsureRouteAsync(Id, ServerUrl, preferredPort:=preferred, forceUpdate:=forceUpdate)
                If port > 0 Then
                    LocalProxyPort = port
                    LocalProxyUrl = $"http://127.0.0.1:{port}"
                    UpdateWebResourceAuthFilter()
                    Return port
                End If
            Catch ex As Exception
                Debug.WriteLine($"Error establishing tsnet route for account {Id}: {ex.Message}")
            End Try
        Else
            UpdateWebResourceAuthFilter()
        End If
        Return 0
    End Function

    ''' <summary>
    ''' Rimuove il filtro WebResourceRequested attivo e il relativo handler dalla WebView2 (TODO #78).
    ''' </summary>
    Public Sub RemoveWebResourceAuthFilter()
        Try
            Dim wv = WebView
            If wv?.CoreWebView2 IsNot Nothing Then
                If Not String.IsNullOrEmpty(_activeProxyFilterPattern) Then
                    Try
                        wv.CoreWebView2.RemoveWebResourceRequestedFilter(_activeProxyFilterPattern, CoreWebView2WebResourceContext.All)
                    Catch ex As Exception
                        Debug.WriteLine($"Failed to remove web resource filter '{_activeProxyFilterPattern}': {ex.Message}")
                    End Try
                End If

                If _webResourceRequestedHandler IsNot Nothing Then
                    Try
                        RemoveHandler wv.CoreWebView2.WebResourceRequested, _webResourceRequestedHandler
                    Catch ex As Exception
                        Debug.WriteLine($"Failed to remove WebResourceRequested handler: {ex.Message}")
                    End Try
                End If
            End If
        Catch ex As Exception
            Debug.WriteLine($"RemoveWebResourceAuthFilter error for account {Id}: {ex.Message}")
        Finally
            _webResourceRequestedHandler = Nothing
            _activeProxyFilterPattern = Nothing
            _activeProxyExpectedScheme = Nothing
            _activeProxyExpectedHost = Nothing
            _activeProxyExpectedPort = 0
            _activeRequiresLocalToken = False
            _activeRequiresAuthToken = False
        End Try
    End Sub

    ''' <summary>
    ''' Configura o aggiorna in modo rigoroso il filtro WebResourceRequested e la verifica puntuale
    ''' di schema, host e porta per l'iniezione dei token (X-HidaChat-Local-Token e Bearer AuthToken),
    ''' rimuovendo i vecchi filtri quando cambia la destinazione ed impedendo la fuga di credenziali
    ''' verso altre porte localhost o host non autorizzati (TODO #78).
    ''' </summary>
    Public Sub UpdateWebResourceAuthFilter()
        If _isDisposed OrElse WebView?.CoreWebView2 Is Nothing Then Return

        RemoveWebResourceAuthFilter()

        If Not PlatformDefinition.RequiresLocalProxy Then Return

        Dim scheme As String = Nothing
        Dim host As String = Nothing
        Dim port As Integer = 0
        Dim reqLocalToken As Boolean = False
        Dim reqAuthToken As Boolean = Not String.IsNullOrWhiteSpace(AuthToken)

        If TailscaleIntegration Then
            If LocalProxyPort <= 0 Then Return
            scheme = "http"
            host = "127.0.0.1"
            port = LocalProxyPort
            reqLocalToken = True
        ElseIf reqAuthToken Then
            Dim targetUri As Uri = Nothing
            If Not Uri.TryCreate(ServerUrl, UriKind.Absolute, targetUri) Then Return
            scheme = targetUri.Scheme
            host = targetUri.Host
            port = targetUri.Port
            reqLocalToken = False
        Else
            Return
        End If

        Dim pattern = LocalAuthPolicy.BuildFilterPattern(scheme, host, port)
        If String.IsNullOrEmpty(pattern) Then Return

        Try
            _activeProxyFilterPattern = pattern
            _activeProxyExpectedScheme = scheme
            _activeProxyExpectedHost = host
            _activeProxyExpectedPort = port
            _activeRequiresLocalToken = reqLocalToken
            _activeRequiresAuthToken = reqAuthToken

            WebView.CoreWebView2.AddWebResourceRequestedFilter(_activeProxyFilterPattern, CoreWebView2WebResourceContext.All)

            _webResourceRequestedHandler = Sub(sender, reqArgs)
                Try
                    If Not LocalAuthPolicy.ShouldAttachCredentials(reqArgs.Request.Uri, _activeProxyExpectedScheme, _activeProxyExpectedHost, _activeProxyExpectedPort) Then
                        Return
                    End If

                    ' Aggiungi il token locale tsnet solo se atteso per la rotta proxy locale (TODO #78)
                    If _activeRequiresLocalToken Then
                        Dim localTok = TsnetManager.Instance.LocalToken
                        If Not String.IsNullOrEmpty(localTok) Then
                            reqArgs.Request.Headers.SetHeader("X-HidaChat-Local-Token", localTok)
                        End If
                    End If

                    ' Aggiungi l'header Bearer token dell'account solo se configurato
                    If _activeRequiresAuthToken AndAlso Not String.IsNullOrWhiteSpace(AuthToken) Then
                        If Not reqArgs.Request.Headers.Contains("Authorization") Then
                            reqArgs.Request.Headers.SetHeader("Authorization", $"Bearer {AuthToken}")
                        End If
                    End If
                Catch exHandler As Exception
                    Debug.WriteLine($"Error in WebResourceRequested handler for account {Id}: {exHandler.Message}")
                End Try
            End Sub

            AddHandler WebView.CoreWebView2.WebResourceRequested, _webResourceRequestedHandler
            AppLogger.LogApp($"[AUTH_FILTER_CONFIGURED] account={Id}, pattern={pattern}, host={host}, port={port}, tsnet={TailscaleIntegration}")
        Catch ex As Exception
            Debug.WriteLine($"UpdateWebResourceAuthFilter error for account {Id}: {ex.Message}")
            AppLogger.LogApp($"[AUTH_FILTER_ERROR] account={Id}: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Ricarica forzatamente l'account garantendo che la rotta locale tsnet (se attiva) sia allineata e naviga a WebUrl.
    ''' </summary>
    Public Async Function ReloadAsync(Optional forceUpdateProxy As Boolean = True) As Task
        If WebView Is Nothing OrElse WebView.CoreWebView2 Is Nothing Then Return

        If TailscaleIntegration AndAlso PlatformDefinition.RequiresLocalProxy Then
            Await EnsureLocalProxyAsync(forceUpdate:=forceUpdateProxy)
        End If

        WebView.CoreWebView2.Navigate(WebUrl)
    End Function

    ''' <summary>
    ''' Imposta una nuova piattaforma per l'account (tipizzata tramite enum ChatPlatform) e ricarica la WebView2 con l'URL corrispondente (TODO #68).
    ''' </summary>
    Public Sub SetPlatform(newPlatform As ChatPlatform)
        If _platformType <> newPlatform Then
            _platformType = newPlatform
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(Platform)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(PlatformType)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(PlatformDefinition)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(IsWhatsApp)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(IsTelegram)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(IsOpenClaw)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(IsHermes)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(OpenClawSettingsVisibility)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(HermesSettingsVisibility)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(AgentSettingsVisibility)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(PlatformIconData)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(PlatformColorBrush)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(AuthTokenLabel)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(ServerUrlTooltip)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(AuthTokenTooltip)))
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(WebUrl)))

            UpdateWebResourceAuthFilter()
            If WebView IsNot Nothing AndAlso WebView.CoreWebView2 IsNot Nothing Then
                WebView.CoreWebView2.Navigate(WebUrl)
            End If
        End If
    End Sub

    ''' <summary>
    ''' Imposta una nuova piattaforma per l'account tramite chiave stringa e ricarica la WebView2 (TODO #68).
    ''' </summary>
    Public Sub SetPlatform(newPlatformStr As String)
        SetPlatform(PlatformRegistry.FromKey(newPlatformStr))
    End Sub

    ''' <summary>
    ''' Valida se l'origine URI specificata (e.Source) corrisponde alla piattaforma o host autorizzato per questo account (#62, #68).
    ''' </summary>
    Public Function IsAuthorizedOrigin(sourceUri As String) As Boolean
        Return PlatformDefinition.IsAuthorizedOrigin(sourceUri, ServerUrl)
    End Function

    ''' <summary>
    ''' Gestisce i messaggi IPC JSON inviati dalla WebView2 tramite `window.chrome.webview.postMessage`.
    ''' Verifica la validità dell'origine del mittente e del token crittografico prima dell'elaborazione (#62).
    ''' </summary>
    Private Async Function HandleWebMessageAsync(messageJson As String, sourceUri As String, settings As SettingsController, onNotificationChanged As Action(Of String, Boolean)) As Task
        Debug.WriteLine($"[WebMessageReceived] accountId={Id}, source={sourceUri}, RAW JSON: {messageJson}")

        ' Validazione rigorosa dell'origine di provenienza del messaggio IPC (#62)
        If Not IsAuthorizedOrigin(sourceUri) Then
            Debug.WriteLine($"[WebMessageReceived] Unauthorized source origin '{sourceUri}' for account {Id}, message dropped.")
            Return
        End If

        Try
            Using doc As JsonDocument = JsonDocument.Parse(messageJson)
                Dim root = doc.RootElement
                Dim channel = ""
                Dim channelNode As JsonElement = Nothing
                If root.TryGetProperty("channel", channelNode) AndAlso channelNode.ValueKind = JsonValueKind.String Then
                    channel = channelNode.GetString()
                End If

                Dim token = ""
                Dim tokenNode As JsonElement = Nothing
                If root.TryGetProperty("bridgeToken", tokenNode) AndAlso tokenNode.ValueKind = JsonValueKind.String Then
                    token = tokenNode.GetString()
                End If
                
                If token <> BridgeToken Then
                    Debug.WriteLine($"[WebMessageReceived] Invalid bridge token for account {Id}, ignoring.")
                    Return
                End If

                If channel = "NotificationChannel" Then
                    Await HandleNotificationMessageAsync(root, settings, onNotificationChanged)
                ElseIf channel = "TranslationChannel" Then
                    Await HandleTranslationMessageAsync(root)
                ElseIf channel = "DiagnosticChannel" Then
                    Await HandleDiagnosticMessageAsync(root)
                End If
            End Using
        Catch ex As Exception
            Debug.WriteLine($"Error handling web message: {ex.Message}")
        End Try
    End Function

    ''' <summary>
    ''' Elabora i messaggi diagnostici provenienti dagli script JavaScript iniettati nella WebView2.
    ''' </summary>
    Private Async Function HandleDiagnosticMessageAsync(root As JsonElement) As Task
        Try
            Dim subType = ""
            Dim typeNode As JsonElement = Nothing
            If root.TryGetProperty("type", typeNode) AndAlso typeNode.ValueKind = JsonValueKind.String Then
                subType = typeNode.GetString()
            End If

            Dim detailsNode As JsonElement = Nothing
            root.TryGetProperty("details", detailsNode)
            Dim detailsText = If(detailsNode.ValueKind <> JsonValueKind.Undefined, detailsNode.ToString(), "")

            AppLogger.LogWhatsApp($"[{Id}] [{subType}] {detailsText}")

            If subType = "INDEXEDDB_CORRUPTED" OrElse
               detailsText.IndexOf("Data lost due to missing file", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               detailsText.IndexOf("irrecoverable", StringComparison.OrdinalIgnoreCase) >= 0 Then
                AppLogger.LogApp($"[INDEXEDDB_CORRUPTION_DETECTED] account={Id}, name={Name}; triggering automatic storage recovery.")
                Await RecoverFromCorruptedStorageAsync(showProgressDialog:=True, loc:=_settings?.Localizations)
            ElseIf subType = "POPUP_ERROR_DETECTED" Then
                AppLogger.Log("whatsapp_errors.log", "POPUP_ERROR", $"[ACCOUNT: {Id} - {Name}] ERRORE DIALOG WHATSAPP: {detailsText}")
            ElseIf subType = "INDEXEDDB_DELETE_ATTEMPT" Then
                AppLogger.Log("whatsapp_errors.log", "INDEXEDDB_DELETE", $"[ACCOUNT: {Id} - {Name}] TENTATIVO CANCELLAZIONE DATABASE: {detailsText}")
            ElseIf subType = "WS_CLOSE" Then
                AppLogger.LogWhatsApp($"[ACCOUNT: {Id} - {Name}] WebSocket Chiuso: {detailsText}")
            End If
        Catch ex As Exception
            Debug.WriteLine($"HandleDiagnosticMessageAsync error: {ex.Message}")
        End Try
    End Function

    ''' <summary>
    ''' Gestisce la ricezione o chiusura delle notifiche dai messaggi IPC e attiva le notifiche Toast o Popup della UI.
    ''' </summary>
    Private Function HandleNotificationMessageAsync(root As JsonElement, settings As SettingsController, onNotificationChanged As Action(Of String, Boolean)) As Task
        Dim type = ""
        Dim typeNode As JsonElement = Nothing
        If root.TryGetProperty("type", typeNode) AndAlso typeNode.ValueKind = JsonValueKind.String Then
            type = typeNode.GetString()
        End If

        Dim notificationId = ""
        Dim idNode As JsonElement = Nothing
        If root.TryGetProperty("id", idNode) AndAlso idNode.ValueKind = JsonValueKind.String Then
            notificationId = idNode.GetString()
        End If
        
        Debug.WriteLine($"[NotificationChannel] accountId={Id}, type={type}, id={notificationId}")

        If type = "NOTIFICATION_RECEIVED" Then
            ' Limita le dimensioni del set per prevenire memory leak prolungato con espulsione FIFO degli ID piu vecchi
            If Not String.IsNullOrEmpty(notificationId) Then
                While ActiveNotificationIds.Count >= MaxActiveNotificationIds
                    Dim oldest = ActiveNotificationIds.FirstOrDefault()
                    If oldest IsNot Nothing Then
                        ActiveNotificationIds.Remove(oldest)
                    Else
                        Exit While
                    End If
                End While
                ActiveNotificationIds.Add(notificationId)
            End If
            HasNotification = True
            onNotificationChanged?.Invoke(Id, True)

            ' Se la modalità Non Disturbare è attiva (TODO #47), silenziamento totale di Toast e Popup
            If settings.IsDndActive Then
                Debug.WriteLine($"[DND Active] Toast & Popup suppressed for account {Id}")
                Return Task.CompletedTask
            End If

            Dim title = ""
            Dim titleNode As JsonElement = Nothing
            If root.TryGetProperty("title", titleNode) AndAlso titleNode.ValueKind = JsonValueKind.String Then
                title = titleNode.GetString()
            End If

            Dim body = ""
            Dim bodyNode As JsonElement = Nothing
            If root.TryGetProperty("body", bodyNode) AndAlso bodyNode.ValueKind = JsonValueKind.String Then
                body = bodyNode.GetString()
            End If

            Try
                Dim builder As New ToastContentBuilder()
                builder.AddText(title)
                builder.AddText(body)
                builder.AddArgument("accountId", Id)
                builder.AddArgument("notificationId", notificationId)
                builder.Show(Sub(toast) toast.SuppressPopup = settings.ShowMessagePopup)
            Catch ex As Exception
                Debug.WriteLine($"Failed to show toast notification: {ex.Message}")
            End Try

            _lastNotificationTime = DateTime.UtcNow
            Dim popupTitle = If(Not String.IsNullOrWhiteSpace(title), title, If(Not String.IsNullOrWhiteSpace(Name), Name, Platform))
            Dim popupBody = If(Not String.IsNullOrWhiteSpace(body), body, "Nuovo messaggio ricevuto")

            If settings.ShowMessagePopup Then
                Try
                    Dim op = Application.Current?.Dispatcher.BeginInvoke(Sub()
                        Dim popup As New MessagePopup(Id, popupTitle, popupBody, Platform)
                        popup.Show()
                    End Sub)
                Catch ex As Exception
                    Debug.WriteLine($"Failed to show popup: {ex.Message}")
                End Try
            End If

        ElseIf type = "NOTIFICATION_CLOSED" Then
            If Not String.IsNullOrEmpty(notificationId) Then
                ActiveNotificationIds.Remove(notificationId)
            End If
            HasNotification = (UnreadCount > 0 OrElse ActiveNotificationIds.Count > 0)
            onNotificationChanged?.Invoke(Id, HasNotification)
        ElseIf type = "UNREAD_COUNT_CHANGED" Then
            Dim count As Integer = 0
            Dim unreadNode As JsonElement = Nothing
            If root.TryGetProperty("unreadCount", unreadNode) Then
                If unreadNode.ValueKind = JsonValueKind.Number Then
                    count = unreadNode.GetInt32()
                ElseIf unreadNode.ValueKind = JsonValueKind.String Then
                    Integer.TryParse(unreadNode.GetString(), count)
                End If
            End If
            System.Diagnostics.Trace.WriteLine($"[UNREAD_COUNT_CHANGED] account={Id} ({Name}), count={count}")
            Dim prevCount = Me.UnreadCount
            Me.UnreadCount = count
            HasNotification = (count > 0 OrElse ActiveNotificationIds.Count > 0)
            onNotificationChanged?.Invoke(Id, HasNotification)

            Dim latestChatTitle As String = ""
            Dim chatTitleNode As JsonElement = Nothing
            If root.TryGetProperty("latestChatTitle", chatTitleNode) AndAlso chatTitleNode.ValueKind = JsonValueKind.String Then
                latestChatTitle = chatTitleNode.GetString().Trim()
            End If

            Dim latestMessageText As String = ""
            Dim msgTextNode As JsonElement = Nothing
            If root.TryGetProperty("latestMessageText", msgTextNode) AndAlso msgTextNode.ValueKind = JsonValueKind.String Then
                latestMessageText = msgTextNode.GetString().Trim()
            End If

            Dim isNewMessage As Boolean = False
            Dim newMsgNode As JsonElement = Nothing
            If root.TryGetProperty("isNewMessage", newMsgNode) AndAlso (newMsgNode.ValueKind = JsonValueKind.True OrElse newMsgNode.ValueKind = JsonValueKind.False) Then
                isNewMessage = newMsgNode.GetBoolean()
            Else
                isNewMessage = (count > prevCount)
            End If

            ' Se è arrivato un nuovo messaggio e non è arrivata una notifica NOTIFICATION_RECEIVED recente
            If isNewMessage AndAlso Not settings.IsDndActive Then
                If (DateTime.UtcNow - _lastNotificationTime).TotalSeconds > 2.0 Then
                    _lastNotificationTime = DateTime.UtcNow
                    
                    Dim notifTitle = If(Not String.IsNullOrWhiteSpace(latestChatTitle), latestChatTitle, If(Not String.IsNullOrWhiteSpace(Name), Name, Platform))
                    Dim notifBody = If(Not String.IsNullOrWhiteSpace(latestMessageText), latestMessageText, If(count = 1, "Nuovo messaggio ricevuto", $"{count} messaggi non letti"))
                    
                    ' Invia notifica Toast Windows se non in DND
                    Try
                        Dim builder As New ToastContentBuilder()
                        builder.AddText(notifTitle)
                        builder.AddText(notifBody)
                        builder.AddArgument("accountId", Id)
                        builder.AddArgument("notificationId", "unread_" + DateTime.UtcNow.Ticks.ToString())
                        builder.Show(Sub(toast) toast.SuppressPopup = settings.ShowMessagePopup)
                    Catch ex As Exception
                        Debug.WriteLine($"Failed to show toast notification for unread change: {ex.Message}")
                    End Try

                    ' Invia popup grafico
                    If settings.ShowMessagePopup Then
                        Try
                            Application.Current?.Dispatcher.BeginInvoke(Sub()
                                Dim popup As New MessagePopup(Id, notifTitle, notifBody, Platform)
                                popup.Show()
                            End Sub)
                        Catch ex As Exception
                            Debug.WriteLine($"Failed to show unread change popup: {ex.Message}")
                        End Try
                    End If
                End If
            End If

            If IsTelegram Then
                Try
                    Dim debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "telegram_debug.txt")
                    Dim sb As New System.Text.StringBuilder()
                    sb.AppendLine($"=== TELEGRAM UNREAD DEBUG REPORT ===")
                    sb.AppendLine($"Data/Ora: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                    sb.AppendLine($"Account: {Id} ({Name})")
                    sb.AppendLine($"UnreadCount Totale: {count}")

                    Dim domCount = 0
                    Dim domNode As JsonElement = Nothing
                    If root.TryGetProperty("domCount", domNode) AndAlso domNode.ValueKind = JsonValueKind.Number Then
                        domCount = domNode.GetInt32()
                    End If
                    sb.AppendLine($"DOM Count: {domCount}")

                    Dim appBadge = 0
                    Dim badgeNode As JsonElement = Nothing
                    If root.TryGetProperty("appBadgeCount", badgeNode) AndAlso badgeNode.ValueKind = JsonValueKind.Number Then
                        appBadge = badgeNode.GetInt32()
                    End If
                    sb.AppendLine($"AppBadge (Badging API): {appBadge}")

                    Dim tCount = 0
                    Dim titleNode As JsonElement = Nothing
                    If root.TryGetProperty("titleCount", titleNode) AndAlso titleNode.ValueKind = JsonValueKind.Number Then
                        tCount = titleNode.GetInt32()
                    End If
                    sb.AppendLine($"Title Count: {tCount}")

                    Dim debugItemsNode As JsonElement = Nothing
                    If root.TryGetProperty("debugItems", debugItemsNode) AndAlso debugItemsNode.ValueKind = JsonValueKind.Array Then
                        sb.AppendLine()
                        sb.AppendLine($"--- Elementi Rilevati nel DOM ({debugItemsNode.GetArrayLength()}) ---")
                        For Each item In debugItemsNode.EnumerateArray()
                            Dim chatTitle = "N/D"
                            Dim chatNode As JsonElement = Nothing
                            If item.TryGetProperty("chatTitle", chatNode) AndAlso chatNode.ValueKind = JsonValueKind.String Then
                                chatTitle = chatNode.GetString()
                            End If
                            Dim c = 0
                            Dim cNode As JsonElement = Nothing
                            If item.TryGetProperty("count", cNode) AndAlso cNode.ValueKind = JsonValueKind.Number Then
                                c = cNode.GetInt32()
                            End If
                            Dim txt = ""
                            Dim txtNode As JsonElement = Nothing
                            If item.TryGetProperty("rawText", txtNode) AndAlso txtNode.ValueKind = JsonValueKind.String Then
                                txt = txtNode.GetString()
                            End If
                            Dim cls = ""
                            Dim clsNode As JsonElement = Nothing
                            If item.TryGetProperty("classes", clsNode) AndAlso clsNode.ValueKind = JsonValueKind.String Then
                                cls = clsNode.GetString()
                            End If
                            Dim html = ""
                            Dim htmlNode As JsonElement = Nothing
                            If item.TryGetProperty("html", htmlNode) AndAlso htmlNode.ValueKind = JsonValueKind.String Then
                                html = htmlNode.GetString()
                            End If
                            sb.AppendLine($"• [{chatTitle}] -> {c} messaggi (testo: '{txt}', classi: '{cls}')")
                            sb.AppendLine($"   HTML: {html}")
                        Next
                    End If
                    File.WriteAllText(debugPath, sb.ToString())
                Catch
                End Try
            End If
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
                Await SafeExecuteScriptAsync($"if (window.onBatchTranslationReceived) {{ window.onBatchTranslationReceived({jsonId}, {partsJson}, true); }}")
            Else
                Await SafeExecuteScriptAsync($"if (window.onBatchTranslationReceived) {{ window.onBatchTranslationReceived({jsonId}, [], false); }}")
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
                Await SafeExecuteScriptAsync($"if (window.onTranslationReceived) {{ window.onTranslationReceived({jsonId}, {jsonResult}, true); }}")
            Else
                Dim emptyJson = JsonSerializer.Serialize(String.Empty)
                Await SafeExecuteScriptAsync($"if (window.onTranslationReceived) {{ window.onTranslationReceived({jsonId}, {emptyJson}, false); }}")
            End If
        End If
    End Function

    ''' <summary>
    ''' Esegue uno script JavaScript all'interno della WebView2 garantendo l'esecuzione sicura sul thread Dispatcher,
    ''' attendendo correttamente i Task asincroni e prevenendo NullReferenceException e crash da oggetti già rilasciati (#63).
    ''' </summary>
    Private Async Function SafeExecuteScriptAsync(script As String) As Task
        If _isDisposed Then Return
        Dim wv = WebView
        If wv Is Nothing Then Return

        Try
            If wv.Dispatcher IsNot Nothing AndAlso Not wv.Dispatcher.CheckAccess() Then
                Await wv.Dispatcher.InvokeAsync(Async Function() As Task
                    Try
                        If Not _isDisposed AndAlso wv.CoreWebView2 IsNot Nothing Then
                            Await wv.CoreWebView2.ExecuteScriptAsync(script)
                        End If
                    Catch ex As Exception
                        Debug.WriteLine($"SafeExecuteScriptAsync dispatch error for account {Id}: {ex.Message}")
                    End Try
                End Function).Task.Unwrap()
            Else
                If Not _isDisposed AndAlso wv.CoreWebView2 IsNot Nothing Then
                    Await wv.CoreWebView2.ExecuteScriptAsync(script)
                End If
            End If
        Catch ex As Exception
            Debug.WriteLine($"SafeExecuteScriptAsync error for account {Id}: {ex.Message}")
        End Try
    End Function

    ''' <summary>
    ''' Notifica alla WebView2 l'aggiornamento della lingua di destinazione per le traduzioni messaggi.
    ''' </summary>
    Public Async Function UpdateWebviewLanguageAsync(langCode As String, langName As String, translateTooltipLabel As String, enableHover As Boolean) As Task
        If Not PlatformDefinition.SupportsTranslation Then Return
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
        If Not PlatformDefinition.SupportsThemeScript Then Return
        If WebView IsNot Nothing AndAlso WebView.CoreWebView2 IsNot Nothing Then
            Try
                Dim script = PlatformDefinition.GetThemeScript(isDark)
                If Not String.IsNullOrEmpty(script) Then
                    Await WebView.CoreWebView2.ExecuteScriptAsync(script)
                End If
            Catch ex As Exception
                Debug.WriteLine($"Failed to apply theme for account {Id}: {ex.Message}")
            End Try
        End If
    End Function

    ''' <summary>
    ''' Notifica al motore WebView2 di svuotare la cronologia temporanea e, se richiesto, la disk cache tramite API nativa.
    ''' </summary>
    Public Async Function ClearBrowsingCacheAsync(Optional clearDiskCache As Boolean = False) As Task
        Try
            If WebView IsNot Nothing AndAlso WebView.CoreWebView2 IsNot Nothing AndAlso WebView.CoreWebView2.Profile IsNot Nothing Then
                Dim dataKinds = CoreWebView2BrowsingDataKinds.DownloadHistory
                If clearDiskCache Then
                    dataKinds = dataKinds Or CoreWebView2BrowsingDataKinds.DiskCache
                End If
                Await WebView.CoreWebView2.Profile.ClearBrowsingDataAsync(dataKinds)
            End If
        Catch ex As Exception
            Debug.WriteLine($"ClearBrowsingCacheAsync error: {ex.Message}")
        End Try
    End Function

    ''' <summary>
    ''' Sospende l'esecuzione di WebView2 in background per liberare risorse di memoria RAM (TODO #50).
    ''' </summary>
    Public Async Function TryHibernateAsync() As Task(Of Boolean)
        If IsActive OrElse _isDisposed Then Return False
        If WebView?.CoreWebView2 Is Nothing Then Return False
        If _isHibernated Then Return True

        Try
            Dim suspended = Await WebView.CoreWebView2.TrySuspendAsync()
            If suspended Then
                IsHibernated = True
                AppLogger.LogApp($"[TAB_HIBERNATED] account={Id}, name={Name}")
                Return True
            End If
        Catch ex As Exception
            Debug.WriteLine($"TryHibernateAsync error for account {Id}: {ex.Message}")
        End Try
        Return False
    End Function

    ''' <summary>
    ''' Risveglia immediatamente l'istanza WebView2 dallo stato di sospensione (TODO #50).
    ''' </summary>
    Public Sub ResumeFromHibernation()
        If WebView?.CoreWebView2 Is Nothing Then
            IsHibernated = False
            Return
        End If
        Try
            If _isHibernated OrElse WebView.CoreWebView2.IsSuspended Then
                WebView.CoreWebView2.Resume()
                IsHibernated = False
                _lastActiveTime = DateTime.UtcNow
                AppLogger.LogApp($"[TAB_RESUMED] account={Id}, name={Name}")
            End If
        Catch ex As Exception
            Debug.WriteLine($"ResumeFromHibernation error for account {Id}: {ex.Message}")
        Finally
            IsHibernated = False
            _lastActiveTime = DateTime.UtcNow
        End Try
    End Sub

    ''' <summary>
    ''' Esegue la pulizia profonda della cache preservando le sessioni utente, i cookie e i dati personali (TODO #72).
    ''' </summary>
    Public Async Function SmartCleanCachePreservingLoginAsync() As Task
        ' 1. Svuota la cache a runtime tramite API WebView2 se il browser è attivo
        If WebView?.CoreWebView2 IsNot Nothing Then
            Await ClearBrowsingCacheAsync(clearDiskCache:=True)
        End If

        ' 2. Elimina le directory temporanee su disco sia per il profilo master sia per l'eventuale staging locale
        Await Task.Run(Sub()
            Try
                Dim masterDir = NetworkProfileSync.GetMasterProfileDir(Id)
                CleanTransientCacheFolders(masterDir, purgeDiskAndCodeCache:=True)

                If _usesNetworkStaging OrElse NetworkProfileSync.IsRunningOnNetwork Then
                    Dim stagingDir = NetworkProfileSync.GetLocalStagingProfileDir(Id)
                    CleanTransientCacheFolders(stagingDir, purgeDiskAndCodeCache:=True)
                End If
            Catch ex As Exception
                Debug.WriteLine($"SmartCleanCachePreservingLoginAsync file cleanup error: {ex.Message}")
            End Try
        End Sub)
    End Function

    Private _lastStorageRecoveryUtc As DateTime = DateTime.MinValue

    ''' <summary>
    ''' Ripristina automaticamente lo storage IndexedDB e i Service Worker in caso di corruzione irreversibile 
    ''' dei file blob su disco (errore Chromium "Data lost due to missing file. Affected record should be considered irrecoverable")
    ''' o schermata bianca provocata da database locale incoerente, mostrando il dialog con ProgressBar (TODO #76).
    ''' </summary>
    Public Async Function RecoverFromCorruptedStorageAsync(Optional showProgressDialog As Boolean = True, Optional loc As AppLocalizations = Nothing) As Task
        If _isDisposed OrElse _stopRequested Then Return
        If DateTime.UtcNow - _lastStorageRecoveryUtc < TimeSpan.FromSeconds(20) Then
            AppLogger.LogApp($"[STORAGE_RECOVERY_RATE_LIMIT] account={Id}; recovery request throttled.")
            Return
        End If
        _lastStorageRecoveryUtc = DateTime.UtcNow

        Try
            AppLogger.LogApp($"[STORAGE_RECOVERY_START] account={Id}, name={Name}, platform={Platform}")
            Dim effectiveLoc = If(loc, _settings?.Localizations)
            If showProgressDialog Then
                Dim ownerWin = Application.Current?.Windows.OfType(Of MainWindow)().FirstOrDefault()
                Await StorageIntegrityService.PerformStorageRecoveryWithDialogAsync(Me, effectiveLoc, ownerWin)
            Else
                If WebView?.CoreWebView2 IsNot Nothing AndAlso WebView.CoreWebView2.Profile IsNot Nothing Then
                    Dim dataKinds = CoreWebView2BrowsingDataKinds.IndexedDb Or
                                    CoreWebView2BrowsingDataKinds.ServiceWorkers Or
                                    CoreWebView2BrowsingDataKinds.CacheStorage
                    Await WebView.CoreWebView2.Profile.ClearBrowsingDataAsync(dataKinds)
                    AppLogger.LogApp($"[STORAGE_RECOVERY_CLEARED] account={Id}")
                    WebView.CoreWebView2.Reload()
                    AppLogger.LogApp($"[STORAGE_RECOVERY_RELOADED] account={Id}")
                End If
            End If
        Catch ex As Exception
            AppLogger.LogApp($"[STORAGE_RECOVERY_FAILED] account={Id}: {ex.Message}")
        End Try
    End Function

    Private _isHandlingTailscaleFailure As Boolean = False
    Private _lastTailscaleErrorPromptUtc As DateTime = DateTime.MinValue

    ''' <summary>
    ''' Rileva fallimenti di rete, pagina bianca o errori gateway per profili Hermes e OpenClaw
    ''' ed interroga lo stato di connettività Tailscale mostrando un dialogo informativo e localizzato (TODO #75).
    ''' </summary>
    Public Async Function HandleHermesOpenClawNavigationFailureAsync(failureReason As String, settings As SettingsController) As Task
        If _isDisposed OrElse _stopRequested OrElse _isHandlingTailscaleFailure Then Return
        If DateTime.UtcNow - _lastTailscaleErrorPromptUtc < TimeSpan.FromSeconds(10) Then Return
        _lastTailscaleErrorPromptUtc = DateTime.UtcNow
        _isHandlingTailscaleFailure = True

        Try
            AppLogger.LogApp($"[HERMES_OPENCLAW_NAV_FAIL] account={Id}, name={Name}, platform={Platform}, reason={failureReason}, url={ServerUrl}, tsnet={TailscaleIntegration}")
            Dim loc = settings?.Localizations

            ' Verifica dettagliata dello stato di Tailscale e della destinazione
            Dim diagStatus = Await TsnetManager.Instance.CheckTailscaleConnectivityAsync(ServerUrl, TailscaleIntegration, LocalProxyPort)

            If Not diagStatus.IsConnected Then
                Await Application.Current.Dispatcher.InvokeAsync(Async Function()
                    Dim ownerWin = Application.Current.Windows.OfType(Of MainWindow)().FirstOrDefault()
                    Dim dialog As New TailscaleConnectionErrorDialog(loc, Name, Platform, diagStatus.StatusKey, diagStatus.Details, diagStatus.LoginUrl)
                    If ownerWin IsNot Nothing AndAlso ownerWin.IsVisible Then
                        dialog.Owner = ownerWin
                    End If

                    dialog.ShowDialog()
                    If dialog.RetryRequested Then
                        AppLogger.LogApp($"[HERMES_OPENCLAW_RETRY] account={Id}; user requested retry.")
                        Await ReloadAsync(forceUpdateProxy:=True)
                    ElseIf dialog.OpenSettingsRequested AndAlso ownerWin IsNot Nothing Then
                        ownerWin.OpenSettingsWindow()
                    End If
                End Function)
            End If
        Catch ex As Exception
            AppLogger.LogApp($"HandleHermesOpenClawNavigationFailureAsync error: {ex.Message}")
        Finally
            _isHandlingTailscaleFailure = False
        End Try
    End Function

    ''' <summary>
    ''' Rimuove tutti gli event handler registrati sulla WebView2 e libera le risorse allocate.
    ''' </summary>
    Public Function CloseWebViewAsync(Optional timeoutMs As Integer = 10000) As Task(Of Boolean)
        If _closeTask IsNot Nothing AndAlso Not _closeTask.IsCompleted Then Return _closeTask
        _stopRequested = True
        _closeTask = CloseWebViewInternalAsync(timeoutMs)
        Return _closeTask
    End Function

    Private Async Function CloseWebViewInternalAsync(timeoutMs As Integer) As Task(Of Boolean)
        Dim pendingInit = _initTask
        If pendingInit IsNot Nothing AndAlso Not pendingInit.IsCompleted Then
            Dim finished = Await Task.WhenAny(pendingInit, Task.Delay(timeoutMs))
            If finished IsNot pendingInit Then
                AppLogger.LogApp($"[INIT_CLOSE_TIMEOUT] account={Id}; profile operations refused.")
                Return False
            End If
        End If
        If pendingInit IsNot Nothing Then
            Try
                Await pendingInit
            Catch
                ' Initialization failures are already logged, but still require disposal.
            End Try
        End If
        Dispose()
        If _browserLifetime Is Nothing Then Return Not _environmentCreationStarted
        Return Await _browserLifetime.WaitForExitAsync(timeoutMs)
    End Function

    Private Sub ThrowIfSetupStopped()
        If _stopRequested OrElse _isDisposed OrElse WebView Is Nothing Then
            Throw New OperationCanceledException("WebView2 initialization stopped during shutdown.")
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _isDisposed Then Return
        _stopRequested = True
        _isDisposed = True

        Try
            Dim wv = WebView
            If wv IsNot Nothing Then
                If wv.CoreWebView2 IsNot Nothing Then
                    Try
                        If _processFailedHandler IsNot Nothing Then
                            RemoveHandler wv.CoreWebView2.ProcessFailed, _processFailedHandler
                            _processFailedHandler = Nothing
                        End If
                        If _permissionRequestedHandler IsNot Nothing Then
                            RemoveHandler wv.CoreWebView2.PermissionRequested, _permissionRequestedHandler
                            _permissionRequestedHandler = Nothing
                        End If
                        If _notificationReceivedHandler IsNot Nothing Then
                            RemoveHandler wv.CoreWebView2.NotificationReceived, _notificationReceivedHandler
                            _notificationReceivedHandler = Nothing
                        End If
                        If _newWindowRequestedHandler IsNot Nothing Then
                            RemoveHandler wv.CoreWebView2.NewWindowRequested, _newWindowRequestedHandler
                            _newWindowRequestedHandler = Nothing
                        End If
                        If _navigationStartingHandler IsNot Nothing Then
                            RemoveHandler wv.CoreWebView2.NavigationStarting, _navigationStartingHandler
                            _navigationStartingHandler = Nothing
                        End If
                        If _webMessageReceivedHandler IsNot Nothing Then
                            RemoveHandler wv.CoreWebView2.WebMessageReceived, _webMessageReceivedHandler
                            _webMessageReceivedHandler = Nothing
                        End If
                        If _navigationCompletedHandler IsNot Nothing Then
                            RemoveHandler wv.CoreWebView2.NavigationCompleted, _navigationCompletedHandler
                            _navigationCompletedHandler = Nothing
                        End If
                        RemoveWebResourceAuthFilter()
                    Catch
                        ' Se il CoreWebView2 è in stato invalidato a causa di un crash, la rimozione degli handler potrebbe sollevare eccezioni
                    End Try
                End If
            End If

            If TailscaleIntegration Then
                Dim accId = Id
                Task.Run(Function() TsnetManager.Instance.RemoveRouteAsync(accId))
            End If

            If wv IsNot Nothing Then
                Dim parentGrid = TryCast(wv.Parent, System.Windows.Controls.Grid)
                parentGrid?.Children.Remove(wv)
                wv.Dispose()
                WebView = Nothing
            End If

            ' The network lease is released only by the owner after confirmed browser exit/export.
            _isCrashed = False
            ActiveNotificationIds.Clear()
        Catch ex As Exception
            Debug.WriteLine($"Error disposing AppAccounts: {ex.Message}")
        End Try
    End Sub
End Class

