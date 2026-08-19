Imports System.IO
Imports System.Reflection
Imports System.Text

''' <summary>
''' Helper interno per il caricamento lazy delle risorse incorporate (JavaScript e CSS) da assembly.
''' </summary>
Friend Module EmbeddedScriptLoader
    Private ReadOnly _resourceCache As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _lockObj As New Object()

    Public Function GetEmbeddedString(resourceFileName As String) As String
        SyncLock _lockObj
            Dim cached As String = Nothing
            If _resourceCache.TryGetValue(resourceFileName, cached) Then
                Return cached
            End If

            Dim asm = Assembly.GetExecutingAssembly()
            Dim resourceName = asm.GetManifestResourceNames().FirstOrDefault(Function(n) n.EndsWith(resourceFileName, StringComparison.OrdinalIgnoreCase))
            If resourceName Is Nothing Then
                Debug.WriteLine($"[EmbeddedScriptLoader] Embedded resource '{resourceFileName}' not found.")
                Return String.Empty
            End If

            Using stream = asm.GetManifestResourceStream(resourceName)
                If stream Is Nothing Then Return String.Empty
                Using reader As New StreamReader(stream, Encoding.UTF8)
                    cached = reader.ReadToEnd()
                    _resourceCache(resourceFileName) = cached
                    Return cached
                End Using
            End Using
        End SyncLock
    End Function
End Module

''' <summary>
''' Contiene gli script CSS e JavaScript per la personalizzazione dell'interfaccia Web di WhatsApp e Telegram 
''' (angoli arrotondati, temi scuro/chiaro e sincronizzazione tema) caricati on-demand.
''' </summary>
Public Class ThemeJsScripts
    Private Shared ReadOnly _lightModeJs As New Lazy(Of String)(Function()
        Dim themeCss = EmbeddedScriptLoader.GetEmbeddedString("theme.css")
        Return "var style = document.createElement(""style"");" & vbCrLf &
               "style.innerHTML = `" & themeCss & " body{ background:#fff !important; } ._ap4q::after { background-color: #fff !important; } `;" & vbCrLf &
               "var ref = document.querySelector(""script"");" & vbCrLf &
               "ref.parentNode.insertBefore(style, ref);" & vbCrLf &
               "document.getElementsByTagName(""body"")[0].classList = [""""];"
    End Function)

    Private Shared ReadOnly _darkModeJs As New Lazy(Of String)(Function()
        Dim themeCss = EmbeddedScriptLoader.GetEmbeddedString("theme.css")
        Return "var style = document.createElement(""style"");" & vbCrLf &
               "style.innerHTML = `" & themeCss & " body{ background:#000 !important; } ._ap4q::after { background-color: #000 !important; } `;" & vbCrLf &
               "var ref = document.querySelector(""script"");" & vbCrLf &
               "ref.parentNode.insertBefore(style, ref);" & vbCrLf &
               "document.getElementsByTagName(""body"")[0].classList = [""dark""];"
    End Function)

    Private Shared ReadOnly _telegramLightModeJs As New Lazy(Of String)(Function()
        Return "(function() {" & vbCrLf &
               "  try {" & vbCrLf &
               "    document.documentElement.classList.remove('night', 'theme-dark', 'dark');" & vbCrLf &
               "    if (document.body) { document.body.classList.remove('night', 'theme-dark', 'dark'); }" & vbCrLf &
               "    localStorage.setItem('tt-theme', 'day');" & vbCrLf &
               "    localStorage.setItem('theme', 'day');" & vbCrLf &
               "  } catch(e) {}" & vbCrLf &
               "  if (window.themeController && typeof window.themeController.setTheme === 'function') {" & vbCrLf &
               "    try { window.themeController.setTheme('day'); } catch(e) {}" & vbCrLf &
               "  }" & vbCrLf &
               "  if (window.appThemeController && typeof window.appThemeController.setTheme === 'function') {" & vbCrLf &
               "    try { window.appThemeController.setTheme('day'); } catch(e) {}" & vbCrLf &
               "  }" & vbCrLf &
               "})();"
    End Function)

    Private Shared ReadOnly _telegramDarkModeJs As New Lazy(Of String)(Function()
        Return "(function() {" & vbCrLf &
               "  try {" & vbCrLf &
               "    document.documentElement.classList.add('night');" & vbCrLf &
               "    if (document.body) { document.body.classList.add('night'); }" & vbCrLf &
               "    localStorage.setItem('tt-theme', 'night');" & vbCrLf &
               "    localStorage.setItem('theme', 'night');" & vbCrLf &
               "  } catch(e) {}" & vbCrLf &
               "  if (window.themeController && typeof window.themeController.setTheme === 'function') {" & vbCrLf &
               "    try { window.themeController.setTheme('night'); } catch(e) {}" & vbCrLf &
               "  }" & vbCrLf &
               "  if (window.appThemeController && typeof window.appThemeController.setTheme === 'function') {" & vbCrLf &
               "    try { window.appThemeController.setTheme('night'); } catch(e) {}" & vbCrLf &
               "  }" & vbCrLf &
               "})();"
    End Function)

    ''' <summary>Script per l'applicazione del tema Chiaro in WhatsApp Web.</summary>
    Public Shared ReadOnly Property LightModeJS As String
        Get
            Return _lightModeJs.Value
        End Get
    End Property

    ''' <summary>Script per l'applicazione del tema Scuro in WhatsApp Web.</summary>
    Public Shared ReadOnly Property DarkModeJS As String
        Get
            Return _darkModeJs.Value
        End Get
    End Property

    ''' <summary>Script per l'applicazione del tema Chiaro in Telegram Web.</summary>
    Public Shared ReadOnly Property TelegramLightModeJS As String
        Get
            Return _telegramLightModeJs.Value
        End Get
    End Property

    ''' <summary>Script per l'applicazione del tema Scuro in Telegram Web.</summary>
    Public Shared ReadOnly Property TelegramDarkModeJS As String
        Get
            Return _telegramDarkModeJs.Value
        End Get
    End Property
End Class

''' <summary>
''' Contiene lo script JavaScript per l'override delle notifiche native del browser (ServiceWorkerRegistration e Notification),
''' reindirizzando le notifiche di WhatsApp verso la finestra host WPF via IPC (chrome.webview.postMessage).
''' </summary>
Public Class NotificationJsScripts
    Private Shared ReadOnly _notificationJs As New Lazy(Of String)(Function()
        Return EmbeddedScriptLoader.GetEmbeddedString("notification.js")
    End Function)

    ''' <summary>Script per l'override delle notifiche.</summary>
    Public Shared ReadOnly Property NotificationOverrideJS As String
        Get
            Return _notificationJs.Value
        End Get
    End Property
End Class

''' <summary>
''' Contiene gli script JavaScript necessari alla funzione di traduzione messaggi (pulsante hover, bolla di traduzione, scansione DOM).
''' </summary>
Public Class TranslationJsScripts
    Private Shared ReadOnly _translationTemplate As New Lazy(Of String)(Function()
        Return EmbeddedScriptLoader.GetEmbeddedString("translation.js")
    End Function)

    ''' <summary>
    ''' Assembla ed inietta lo script JavaScript completo di traduzione configurato con la lingua e le opzioni attuali.
    ''' </summary>
    Public Shared Function GetTranslationJS(
        targetLangCode As String,
        targetLangName As String,
        tooltipLabel As String,
        enableHover As Boolean,
        enableFullPage As Boolean
    ) As String
        Dim escapedTooltip = tooltipLabel.Replace("'", "\'")
        Dim escapedName = targetLangName.Replace("'", "\'")

        Dim hoverClassListCmd = If(enableHover,
            "document.body.classList.remove('disable-hover-translation');",
            "document.body.classList.add('disable-hover-translation');")

        Dim fullPageInitCmd = If(enableFullPage,
            "if (document.readyState === 'complete') { window.translatePage(); } else { window.addEventListener('load', () => window.translatePage()); }",
            "")

        Dim sb As New StringBuilder(_translationTemplate.Value)
        sb.Replace("$$LANG_CODE$$", targetLangCode)
        sb.Replace("$$LANG_NAME$$", escapedName)
        sb.Replace("$$TOOLTIP$$", escapedTooltip)
        sb.Replace("$$ENABLE_HOVER$$", enableHover.ToString().ToLowerInvariant())
        sb.Replace("$$HOVER_CLASS_CMD$$", hoverClassListCmd)
        sb.Replace("$$FULL_PAGE_INIT$$", fullPageInitCmd)

        Return sb.ToString()
    End Function
End Class
