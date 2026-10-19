Imports System
Imports System.Collections.Generic
Imports System.Collections.Concurrent
Imports System.Linq
#If Not NO_WPF Then
Imports System.Windows.Media
#End If

''' <summary>
''' Enumerazione tipizzata delle piattaforme supportate dall'applicazione (TODO #68).
''' </summary>
Public Enum ChatPlatform
    WhatsApp = 0
    Telegram = 1
    OpenClaw = 2
    Hermes = 3
End Enum

''' <summary>
''' Astrazione tipizzata per una piattaforma di messaggistica / gateway AI.
''' Incapsula metadati (ID, nome visualizzato, porta proxy, URL di default),
''' elementi grafici (colori tema, percorsi vettoriali SVG per icone),
''' script iniettati (temi, script di bootstrap e notifiche)
''' e politiche di sicurezza su origini consentite e navigazione finestre (TODO #68).
''' </summary>
Public MustInherit Class PlatformDefinition

    ''' <summary>Identificativo enum tipizzato della piattaforma.</summary>
    Public MustOverride ReadOnly Property Platform As ChatPlatform

    ''' <summary>Identificativo stringa serializzato nei file di configurazione (es. "WhatsApp", "Telegram").</summary>
    Public MustOverride ReadOnly Property Id As String

    ''' <summary>Nome visualizzato nell'interfaccia utente (es. "WhatsApp", "Telegram", "Hermes Agent").</summary>
    Public MustOverride ReadOnly Property DisplayName As String

    ''' <summary>URL web predefinito per la piattaforma.</summary>
    Public MustOverride ReadOnly Property DefaultUrl As String

    ''' <summary>Porta TCP locale base assegnata per il reverse proxy tsnet (0 se non applicabile).</summary>
    Public MustOverride ReadOnly Property DefaultPort As Integer

    ''' <summary>Codice colore esadecimale caratteristico del brand/piattaforma.</summary>
    Public MustOverride ReadOnly Property ThemeColorHex As String

    ''' <summary>Tracciato SVG Path della rappresentazione iconografica della piattaforma.</summary>
    Public MustOverride ReadOnly Property IconSvgPath As String

    ''' <summary>Indica se la piattaforma rappresenta un gateway AI (OpenClaw / Hermes Agent).</summary>
    Public Overridable ReadOnly Property IsAiGateway As Boolean
        Get
            Return False
        End Get
    End Property

    ''' <summary>Indica se la piattaforma richiede o supporta l'integrazione proxy locale con tsnet.</summary>
    Public Overridable ReadOnly Property RequiresLocalProxy As Boolean
        Get
            Return False
        End Get
    End Property

    ''' <summary>Indica se la piattaforma supporta lo strumento di invio massivo messaggi (Bulk Sender).</summary>
    Public Overridable ReadOnly Property SupportsBulkSender As Boolean
        Get
            Return False
        End Get
    End Property

    ''' <summary>Indica se la piattaforma supporta l'iniezione degli script di sincronizzazione tema scuro/chiaro.</summary>
    Public Overridable ReadOnly Property SupportsThemeScript As Boolean
        Get
            Return False
        End Get
    End Property

    ''' <summary>Indica se la piattaforma supporta il sistema di traduzione messaggi integrato.</summary>
    Public Overridable ReadOnly Property SupportsTranslation As Boolean
        Get
            Return False
        End Get
    End Property

    ''' <summary>Etichetta per il campo token di autenticazione nelle impostazioni.</summary>
    Public Overridable ReadOnly Property AuthTokenLabel As String
        Get
            Return String.Empty
        End Get
    End Property

    ''' <summary>Suggerimento per il campo URL del server nelle impostazioni.</summary>
    Public Overridable ReadOnly Property ServerUrlTooltip As String
        Get
            Return String.Empty
        End Get
    End Property

    ''' <summary>Suggerimento per il campo Token di autenticazione nelle impostazioni.</summary>
    Public Overridable ReadOnly Property AuthTokenTooltip As String
        Get
            Return String.Empty
        End Get
    End Property

#If Not NO_WPF Then
    ''' <summary>Pennello SolidColorBrush congelato (Frozen) corrispondente al colore tema della piattaforma.</summary>
    Public Overridable ReadOnly Property ThemeBrush As Brush
        Get
            Return BrushCache.GetBrush(ThemeColorHex)
        End Get
    End Property
#End If

    ''' <summary>
    ''' Verifica se l'origine specificata (URI sorgente) è autorizzata per questa piattaforma.
    ''' </summary>
    Public MustOverride Function IsAuthorizedOrigin(sourceUriStr As String, configuredServerUrl As String) As Boolean

    ''' <summary>
    ''' Determina se la richiesta di apertura nuova finestra (NewWindowRequested) deve essere gestita
    ''' internamente nella WebView corrente anziché aperta nel browser esterno di sistema.
    ''' </summary>
    Public Overridable Function IsAllowedNewWindowHost(uri As Uri, configuredServerUrl As String) As Boolean
        Return False
    End Function

    ''' <summary>
    ''' Risolve eventuali deep-link o schemi personalizzati (es. tg:// o link t.me) nell'URL di destinazione effettivo.
    ''' </summary>
    Public Overridable Function ResolveDeepLink(uriStr As String) As String
        Return Nothing
    End Function

    ''' <summary>
    ''' Restituisce lo script JavaScript da iniettare in fase di DocumentCreated specifico per questa piattaforma.
    ''' </summary>
    Public Overridable Function GetInitScript(bridgeToken As String) As String
        Return Nothing
    End Function

    ''' <summary>
    ''' Restituisce lo script di sincronizzazione del tema (scuro/chiaro) per questa piattaforma.
    ''' </summary>
    Public Overridable Function GetThemeScript(isDark As Boolean) As String
        Return Nothing
    End Function

End Class

''' <summary>
''' Definizione specifica per la piattaforma WhatsApp Web.
''' </summary>
Public Class WhatsAppPlatformDefinition
    Inherits PlatformDefinition

    Public Overrides ReadOnly Property Platform As ChatPlatform
        Get
            Return ChatPlatform.WhatsApp
        End Get
    End Property

    Public Overrides ReadOnly Property Id As String
        Get
            Return "WhatsApp"
        End Get
    End Property

    Public Overrides ReadOnly Property DisplayName As String
        Get
            Return "WhatsApp"
        End Get
    End Property

    Public Overrides ReadOnly Property DefaultUrl As String
        Get
            Return "https://web.whatsapp.com/"
        End Get
    End Property

    Public Overrides ReadOnly Property DefaultPort As Integer
        Get
            Return 0
        End Get
    End Property

    Public Overrides ReadOnly Property ThemeColorHex As String
        Get
            Return "#25d366"
        End Get
    End Property

    Public Overrides ReadOnly Property IconSvgPath As String
        Get
            Return "M12.04 2C6.58 2 2.13 6.45 2.13 11.91C2.13 13.66 2.59 15.36 3.45 16.86L2.05 22L7.3 20.62C8.75 21.41 10.38 21.83 12.04 21.83C17.5 21.83 21.95 17.38 21.95 11.92C21.95 9.27 20.92 6.78 19.05 4.91C17.18 3.03 14.69 2 12.04 2M12.05 3.67C14.25 3.67 16.31 4.53 17.87 6.09C19.42 7.65 20.28 9.72 20.28 11.92C20.28 16.46 16.58 20.15 12.04 20.15C10.56 20.15 9.11 19.76 7.85 19L7.55 18.83L4.43 19.65L5.26 16.61L5.06 16.29C4.24 15 3.8 13.47 3.8 11.91C3.81 7.37 7.5 3.67 12.05 3.67M8.53 7.33C8.37 7.33 8.1 7.39 7.87 7.64C7.65 7.89 7.02 8.48 7.02 9.68C7.02 10.88 7.9 12.03 8.02 12.19C8.14 12.35 9.73 14.81 12.18 15.86C12.76 16.11 13.22 16.26 13.57 16.37C14.16 16.56 14.69 16.53 15.11 16.47C15.59 16.4 16.58 15.87 16.78 15.3C16.98 14.73 16.98 14.24 16.92 14.14C16.86 14.04 16.7 13.98 16.45 13.85C16.2 13.73 14.97 13.12 14.74 13.04C14.52 12.96 14.35 12.92 14.19 13.17C14.03 13.41 13.56 13.98 13.42 14.14C13.28 14.31 13.13 14.33 12.89 14.21C12.64 14.08 11.84 13.82 10.89 12.97C10.15 12.31 9.65 11.5 9.51 11.25C9.36 11.01 9.5 10.87 9.62 10.75C9.73 10.64 9.87 10.45 10 10.31C10.13 10.16 10.17 10.06 10.25 9.9C10.33 9.73 10.29 9.59 10.23 9.47C10.17 9.35 9.7 8.19 9.5 7.72C9.31 7.26 9.12 7.32 8.97 7.31C8.84 7.31 8.68 7.33 8.53 7.33Z"
        End Get
    End Property

    Public Overrides ReadOnly Property SupportsBulkSender As Boolean
        Get
            Return True
        End Get
    End Property

    Public Overrides ReadOnly Property SupportsThemeScript As Boolean
        Get
            Return True
        End Get
    End Property

    Public Overrides ReadOnly Property SupportsTranslation As Boolean
        Get
            Return True
        End Get
    End Property

    Public Overrides Function IsAuthorizedOrigin(sourceUriStr As String, configuredServerUrl As String) As Boolean
        If String.IsNullOrWhiteSpace(sourceUriStr) Then Return False
        Dim uri As Uri = Nothing
        If Not Uri.TryCreate(sourceUriStr, UriKind.Absolute, uri) Then Return False
        If uri.Scheme <> Uri.UriSchemeHttps Then Return False
        Dim host = uri.Host.ToLowerInvariant()
        Return host = "web.whatsapp.com" OrElse host.EndsWith(".whatsapp.com")
    End Function

#If Not NO_WPF Then
    Public Overrides Function GetThemeScript(isDark As Boolean) As String
        Return If(isDark, ThemeJsScripts.DarkModeJS, ThemeJsScripts.LightModeJS)
    End Function
#End If

End Class

''' <summary>
''' Definizione specifica per la piattaforma Telegram Web.
''' </summary>
Public Class TelegramPlatformDefinition
    Inherits PlatformDefinition

    Public Overrides ReadOnly Property Platform As ChatPlatform
        Get
            Return ChatPlatform.Telegram
        End Get
    End Property

    Public Overrides ReadOnly Property Id As String
        Get
            Return "Telegram"
        End Get
    End Property

    Public Overrides ReadOnly Property DisplayName As String
        Get
            Return "Telegram"
        End Get
    End Property

    Public Overrides ReadOnly Property DefaultUrl As String
        Get
            Return "https://web.telegram.org/a/"
        End Get
    End Property

    Public Overrides ReadOnly Property DefaultPort As Integer
        Get
            Return 0
        End Get
    End Property

    Public Overrides ReadOnly Property ThemeColorHex As String
        Get
            Return "#24A1DE"
        End Get
    End Property

    Public Overrides ReadOnly Property IconSvgPath As String
        Get
            Return "M9.78 18.65L10.06 14.42L17.74 7.5C18.08 7.19 17.67 7.04 17.22 7.31L7.74 13.3L3.64 12C2.76 11.75 2.75 11.14 3.84 10.7L19.81 4.54C20.54 4.21 21.24 4.72 20.97 5.84L18.25 18.67C18.05 19.6 17.5 19.82 16.73 19.38L12.58 16.32L10.58 18.25C10.36 18.47 10.17 18.65 9.78 18.65Z"
        End Get
    End Property

    Public Overrides ReadOnly Property SupportsBulkSender As Boolean
        Get
            Return True
        End Get
    End Property

    Public Overrides ReadOnly Property SupportsThemeScript As Boolean
        Get
            Return True
        End Get
    End Property

    Public Overrides ReadOnly Property SupportsTranslation As Boolean
        Get
            Return True
        End Get
    End Property

    Public Overrides Function IsAuthorizedOrigin(sourceUriStr As String, configuredServerUrl As String) As Boolean
        If String.IsNullOrWhiteSpace(sourceUriStr) Then Return False
        Dim uri As Uri = Nothing
        If Not Uri.TryCreate(sourceUriStr, UriKind.Absolute, uri) Then Return False
        If uri.Scheme <> Uri.UriSchemeHttps Then Return False
        Dim host = uri.Host.ToLowerInvariant()
        Return host = "web.telegram.org" OrElse host.EndsWith(".telegram.org")
    End Function

    Public Overrides Function IsAllowedNewWindowHost(uri As Uri, configuredServerUrl As String) As Boolean
        If uri Is Nothing Then Return False
        Dim host = uri.Host.ToLowerInvariant()
        Return host = "t.me" OrElse host.EndsWith(".t.me") OrElse host = "telegram.me" OrElse host.EndsWith(".telegram.me")
    End Function

    Public Overrides Function ResolveDeepLink(uriStr As String) As String
        If String.IsNullOrWhiteSpace(uriStr) Then Return Nothing
        Dim trimmed = uriStr.Trim()

        ' 1. Gestione protocollo tg://
        If trimmed.StartsWith("tg:", StringComparison.OrdinalIgnoreCase) Then
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

        Return Nothing
    End Function

#If Not NO_WPF Then
    Public Overrides Function GetInitScript(bridgeToken As String) As String
        Return ThemeJsScripts.TelegramInitJS
    End Function

    Public Overrides Function GetThemeScript(isDark As Boolean) As String
        Return If(isDark, ThemeJsScripts.TelegramDarkModeJS, ThemeJsScripts.TelegramLightModeJS)
    End Function
#End If

End Class

''' <summary>
''' Definizione specifica per la piattaforma OpenClaw AI Gateway.
''' </summary>
Public Class OpenClawPlatformDefinition
    Inherits PlatformDefinition

    Public Overrides ReadOnly Property Platform As ChatPlatform
        Get
            Return ChatPlatform.OpenClaw
        End Get
    End Property

    Public Overrides ReadOnly Property Id As String
        Get
            Return "OpenClaw"
        End Get
    End Property

    Public Overrides ReadOnly Property DisplayName As String
        Get
            Return "OpenClaw"
        End Get
    End Property

    Public Overrides ReadOnly Property DefaultUrl As String
        Get
            Return "http://127.0.0.1:18789"
        End Get
    End Property

    Public Overrides ReadOnly Property DefaultPort As Integer
        Get
            Return 18800
        End Get
    End Property

    Public Overrides ReadOnly Property ThemeColorHex As String
        Get
            Return "#FF5722"
        End Get
    End Property

    Public Overrides ReadOnly Property IconSvgPath As String
        Get
            Return "M12,2A2,2 0 0,1 14,4C14,4.74 13.6,5.39 13,5.73V7H14A7,7 0 0,1 21,14H22A1,1 0 0,1 23,15V18A1,1 0 0,1 22,19H21V20A2,2 0 0,1 19,22H5A2,2 0 0,1 3,20V19H2A1,1 0 0,1 1,18V15A1,1 0 0,1 2,14H3A7,7 0 0,1 10,7H11V5.73C10.4,5.39 10,4.74 10,4A2,2 0 0,1 12,2M7.5,13A2.5,2.5 0 0,0 5,15.5A2.5,2.5 0 0,0 7.5,18A2.5,2.5 0 0,0 10,15.5A2.5,2.5 0 0,0 7.5,13M16.5,13A2.5,2.5 0 0,0 14,15.5A2.5,2.5 0 0,0 16.5,18A2.5,2.5 0 0,0 19,15.5A2.5,2.5 0 0,0 16.5,13Z"
        End Get
    End Property

    Public Overrides ReadOnly Property IsAiGateway As Boolean
        Get
            Return True
        End Get
    End Property

    Public Overrides ReadOnly Property RequiresLocalProxy As Boolean
        Get
            Return True
        End Get
    End Property

    Public Overrides ReadOnly Property SupportsBulkSender As Boolean
        Get
            Return False
        End Get
    End Property

    Public Overrides ReadOnly Property SupportsThemeScript As Boolean
        Get
            Return False
        End Get
    End Property

    Public Overrides ReadOnly Property SupportsTranslation As Boolean
        Get
            Return False
        End Get
    End Property

    Public Overrides ReadOnly Property AuthTokenLabel As String
        Get
            Return "Gateway Auth Token:"
        End Get
    End Property

    Public Overrides ReadOnly Property ServerUrlTooltip As String
        Get
            Return "Es: http://127.0.0.1:18789 o https://node.tailnet.ts.net"
        End Get
    End Property

    Public Overrides ReadOnly Property AuthTokenTooltip As String
        Get
            Return "Token segreto OpenClaw gateway"
        End Get
    End Property

    Public Overrides Function IsAuthorizedOrigin(sourceUriStr As String, configuredServerUrl As String) As Boolean
        If String.IsNullOrWhiteSpace(sourceUriStr) Then Return False
        Dim uri As Uri = Nothing
        If Not Uri.TryCreate(sourceUriStr, UriKind.Absolute, uri) Then Return False
        Dim host = uri.Host.ToLowerInvariant()
        If host = "127.0.0.1" OrElse host = "localhost" Then Return True
        If Not String.IsNullOrWhiteSpace(configuredServerUrl) Then
            Dim baseUri As Uri = Nothing
            If Uri.TryCreate(configuredServerUrl, UriKind.Absolute, baseUri) Then
                Return String.Equals(host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
            End If
        End If
        Return False
    End Function

    Public Overrides Function IsAllowedNewWindowHost(uri As Uri, configuredServerUrl As String) As Boolean
        If uri Is Nothing OrElse String.IsNullOrWhiteSpace(configuredServerUrl) Then Return False
        Dim baseUri As Uri = Nothing
        If Uri.TryCreate(configuredServerUrl, UriKind.Absolute, baseUri) Then
            Return String.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
        End If
        Return False
    End Function

End Class

''' <summary>
''' Definizione specifica per la piattaforma Hermes Agent.
''' </summary>
Public Class HermesPlatformDefinition
    Inherits PlatformDefinition

    Public Overrides ReadOnly Property Platform As ChatPlatform
        Get
            Return ChatPlatform.Hermes
        End Get
    End Property

    Public Overrides ReadOnly Property Id As String
        Get
            Return "Hermes"
        End Get
    End Property

    Public Overrides ReadOnly Property DisplayName As String
        Get
            Return "Hermes Agent"
        End Get
    End Property

    Public Overrides ReadOnly Property DefaultUrl As String
        Get
            Return "http://127.0.0.1:9119"
        End Get
    End Property

    Public Overrides ReadOnly Property DefaultPort As Integer
        Get
            Return 18900
        End Get
    End Property

    Public Overrides ReadOnly Property ThemeColorHex As String
        Get
            Return "#00B0FF"
        End Get
    End Property

    Public Overrides ReadOnly Property IconSvgPath As String
        Get
            Return "M12,2A3,3 0 0,0 9,5C9,5.27 9.04,5.53 9.1,5.78C6.67,6.34 5.09,8.5 5,11C3.12,11.36 1.74,12.82 1.3,14.7C0.85,16.63 1.45,18.66 2.87,20.08L3.58,19.37C2.4,18.19 1.9,16.5 2.27,14.92C2.64,13.35 3.82,12.13 5.38,11.83L6,11.71V11C6,8.68 7.5,6.67 9.71,6.13L10.5,5.94L10.15,5.2C10.05,5 10,4.76 10,4.5A2,2 0 0,1 12,2.5A2,2 0 0,1 14,4.5C14,4.76 13.95,5 13.85,5.2L13.5,5.94L14.29,6.13C16.5,6.67 18,8.68 18,11V11.71L18.62,11.83C20.18,12.13 21.36,13.35 21.73,14.92C22.1,16.5 21.6,18.19 20.42,19.37L21.13,20.08C22.55,18.66 23.15,16.63 22.7,14.7C22.26,12.82 20.88,11.36 19,11C18.91,8.5 17.33,6.34 14.9,5.78C14.96,5.53 15,5.27 15,5A3,3 0 0,0 12,2M12,7A5,5 0 0,1 17,12V14A5,5 0 0,1 12,19A5,5 0 0,1 7,14V12A5,5 0 0,1 12,7M10,13A1,1 0 0,0 9,14A1,1 0 0,0 10,15A1,1 0 0,0 11,14A1,1 0 0,0 10,13M14,13A1,1 0 0,0 13,14A1,1 0 0,0 14,15A1,1 0 0,0 15,14A1,1 0 0,0 14,13Z"
        End Get
    End Property

    Public Overrides ReadOnly Property IsAiGateway As Boolean
        Get
            Return True
        End Get
    End Property

    Public Overrides ReadOnly Property RequiresLocalProxy As Boolean
        Get
            Return True
        End Get
    End Property

    Public Overrides ReadOnly Property SupportsBulkSender As Boolean
        Get
            Return False
        End Get
    End Property

    Public Overrides ReadOnly Property SupportsThemeScript As Boolean
        Get
            Return False
        End Get
    End Property

    Public Overrides ReadOnly Property SupportsTranslation As Boolean
        Get
            Return False
        End Get
    End Property

    Public Overrides ReadOnly Property AuthTokenLabel As String
        Get
            Return "API Key / Auth Token:"
        End Get
    End Property

    Public Overrides ReadOnly Property ServerUrlTooltip As String
        Get
            Return "Es: http://127.0.0.1:9119 o https://node.tailnet.ts.net"
        End Get
    End Property

    Public Overrides ReadOnly Property AuthTokenTooltip As String
        Get
            Return "Token o API Key per Hermes Agent / Dashboard"
        End Get
    End Property

    Public Overrides Function IsAuthorizedOrigin(sourceUriStr As String, configuredServerUrl As String) As Boolean
        If String.IsNullOrWhiteSpace(sourceUriStr) Then Return False
        Dim uri As Uri = Nothing
        If Not Uri.TryCreate(sourceUriStr, UriKind.Absolute, uri) Then Return False
        Dim host = uri.Host.ToLowerInvariant()
        If host = "127.0.0.1" OrElse host = "localhost" Then Return True
        If Not String.IsNullOrWhiteSpace(configuredServerUrl) Then
            Dim baseUri As Uri = Nothing
            If Uri.TryCreate(configuredServerUrl, UriKind.Absolute, baseUri) Then
                Return String.Equals(host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
            End If
        End If
        Return False
    End Function

    Public Overrides Function IsAllowedNewWindowHost(uri As Uri, configuredServerUrl As String) As Boolean
        If uri Is Nothing OrElse String.IsNullOrWhiteSpace(configuredServerUrl) Then Return False
        Dim baseUri As Uri = Nothing
        If Uri.TryCreate(configuredServerUrl, UriKind.Absolute, baseUri) Then
            Return String.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
        End If
        Return False
    End Function

End Class

''' <summary>
''' Registro centrale thread-safe per la risoluzione e l'estensione delle piattaforme supportate (TODO #68).
''' </summary>
Public Module PlatformRegistry

    Private ReadOnly _definitionsByEnum As New ConcurrentDictionary(Of ChatPlatform, PlatformDefinition)()
    Private ReadOnly _definitionsByKey As New ConcurrentDictionary(Of String, PlatformDefinition)(StringComparer.OrdinalIgnoreCase)

    Sub New()
        Register(New WhatsAppPlatformDefinition())
        Register(New TelegramPlatformDefinition())
        Register(New OpenClawPlatformDefinition())
        Register(New HermesPlatformDefinition())
    End Sub

    ''' <summary>
    ''' Registra una nuova o aggiornata definizione di piattaforma.
    ''' </summary>
    Public Sub Register(definition As PlatformDefinition)
        If definition Is Nothing Then Throw New ArgumentNullException(NameOf(definition))
        _definitionsByEnum(definition.Platform) = definition
        _definitionsByKey(definition.Id) = definition
        _definitionsByKey(definition.DisplayName) = definition
    End Sub

    ''' <summary>
    ''' Recupera la definizione tipizzata a partire dall'enum ChatPlatform.
    ''' </summary>
    Public Function [Get](platform As ChatPlatform) As PlatformDefinition
        Dim def As PlatformDefinition = Nothing
        If _definitionsByEnum.TryGetValue(platform, def) Then
            Return def
        End If
        Return _definitionsByEnum(ChatPlatform.WhatsApp)
    End Function

    ''' <summary>
    ''' Recupera la definizione tipizzata a partire da una stringa identificativa (es. "WhatsApp", "Telegram", "OpenClaw", "Hermes").
    ''' In caso di valore vuoto o non riconosciuto, restituisce la definizione di WhatsApp (default).
    ''' </summary>
    Public Function [Get](key As String) As PlatformDefinition
        If String.IsNullOrWhiteSpace(key) Then
            Return [Get](ChatPlatform.WhatsApp)
        End If
        Dim def As PlatformDefinition = Nothing
        If _definitionsByKey.TryGetValue(key.Trim(), def) Then
            Return def
        End If
        Return [Get](ChatPlatform.WhatsApp)
    End Function

    ''' <summary>
    ''' Risolve l'enum ChatPlatform corrispondente alla stringa specificata.
    ''' </summary>
    Public Function FromKey(key As String) As ChatPlatform
        Return [Get](key).Platform
    End Function

    ''' <summary>
    ''' Tenta di risolvere l'enum ChatPlatform corrispondente alla stringa specificata.
    ''' </summary>
    Public Function TryFromKey(key As String, ByRef platform As ChatPlatform) As Boolean
        If String.IsNullOrWhiteSpace(key) Then
            platform = ChatPlatform.WhatsApp
            Return False
        End If
        Dim def As PlatformDefinition = Nothing
        If _definitionsByKey.TryGetValue(key.Trim(), def) Then
            platform = def.Platform
            Return True
        End If
        platform = ChatPlatform.WhatsApp
        Return False
    End Function

    ''' <summary>
    ''' Elenco completo di tutte le definizioni di piattaforma registrate.
    ''' </summary>
    Public ReadOnly Property All As IReadOnlyList(Of PlatformDefinition)
        Get
            Return _definitionsByEnum.Values.ToList().AsReadOnly()
        End Get
    End Property

End Module
