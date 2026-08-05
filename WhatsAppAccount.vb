Imports System.IO
Imports System.ComponentModel
Imports System.Text.Json.Serialization
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.Wpf
Imports Microsoft.Toolkit.Uwp.Notifications
Imports System.Text.Json

''' <summary>

''' Rappresenta un singolo account WhatsApp Web, gestione dell'istanza WebView2 associata, 
''' token di sicurezza per IPC e gestione di notifiche e traduzioni.
''' </summary>
Public Class WhatsAppAccount
    Implements INotifyPropertyChanged
    Implements IDisposable

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private Shared ReadOnly _randLock As New Object()
    Private Shared ReadOnly _rand As New Random()

    ''' <summary>Identificativo univoco dell'account (es. account_1680000000000).</summary>
    <JsonPropertyName("id")>
    Public Property Id As String

    ''' <summary>Nome personalizzato visualizzato nelle schede e impostazioni.</summary>
    <JsonPropertyName("name")>
    Public Property Name As String

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
    
    ''' <summary>Indica se vi sono notifiche pendenti non lette per questo account.</summary>
    <JsonIgnore>
    Public Property HasNotification As Boolean
    
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

    ' Event Handlers fortemente tipizzati per WebView2 (evita memory leak)
    Private _permissionRequestedHandler As EventHandler(Of CoreWebView2PermissionRequestedEventArgs)
    Private _newWindowRequestedHandler As EventHandler(Of CoreWebView2NewWindowRequestedEventArgs)
    Private _webMessageReceivedHandler As EventHandler(Of CoreWebView2WebMessageReceivedEventArgs)
    Private _navigationCompletedHandler As EventHandler(Of CoreWebView2NavigationCompletedEventArgs)

    ''' <summary>Percorso base per il salvataggio dei profili WebView2 isolati degli account.</summary>
    Public Shared ReadOnly Property SharedDataDirectory As String
        Get
            Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "webview")
        End Get
    End Property

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

    Public Sub New(id As String, name As String, Optional isActive As Boolean = False)
        Me.Id = id
        Me.Name = name
        Me.IsActive = isActive
        BridgeToken = GenerateBridgeToken()
    End Sub

    ''' <summary>
    ''' Configura l'ambiente isolato della WebView2, inietta gli script JavaScript per l'intercettazione delle notifiche e traduzioni,
    ''' e naviga verso la pagina di WhatsApp Web.
    ''' </summary>
    Public Async Function SetupWebViewAsync(settings As SettingsController, onNotificationChanged As Action(Of String, Boolean)) As Task
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

        Try
            Dim options As New CoreWebView2EnvironmentOptions()
            options.AdditionalBrowserArguments = "--disk-cache-size=104857600 --media-cache-size=52428800 --disable-background-networking --disable-features=Translate,OptimizationHints,MediaRouter"
            Dim accountEnv = Await CoreWebView2Environment.CreateAsync(Nothing, profileDir, options)
            
            Await WebView.EnsureCoreWebView2Async(accountEnv)
            
            WebView.CoreWebView2.Settings.IsWebMessageEnabled = True
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = True
            
            ' Salvataggio riferimenti handler per poterli rimuovere in Dispose()
            _permissionRequestedHandler = Sub(sender, e)
                If e.PermissionKind = CoreWebView2PermissionKind.Notifications Then
                    e.State = CoreWebView2PermissionState.Allow
                    e.Handled = True
                End If
            End Sub
            AddHandler WebView.CoreWebView2.PermissionRequested, _permissionRequestedHandler

            Dim initScript = $"window.__bridgeToken = '{BridgeToken}';" & vbCrLf &
                NotificationJsScripts.NotificationOverrideJS
            Await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(initScript)

            _newWindowRequestedHandler = Sub(sender, e)
                e.Handled = True
                Try
                    Dim uri = New Uri(e.Uri)
                    Dim host = uri.Host.ToLower()
                    If host = "web.whatsapp.com" OrElse host = "whatsapp.com" OrElse host.EndsWith(".whatsapp.com") Then
                        WebView.CoreWebView2.Navigate(e.Uri)
                    Else
                        System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo(e.Uri) With {
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


                    If brightnessDark Then
                        Await WebView.CoreWebView2.ExecuteScriptAsync(ThemeJsScripts.DarkModeJS)
                    Else
                        Await WebView.CoreWebView2.ExecuteScriptAsync(ThemeJsScripts.LightModeJS)
                    End If

                    Dim langName = "English"
                    Dim langItem = settings.SupportedLanguages.FirstOrDefault(Function(l) l.Code = settings.Language)
                    If langItem IsNot Nothing Then
                        langName = langItem.Name
                    End If

                    Dim translatedLangName = langName
                    If settings.Language <> "en" Then
                        translatedLangName = If(settings.Language = "it", "Italiano", langName)
                    End If

                    Dim tooltipLabel = settings.Localizations.Get("translate_to_lang", New Dictionary(Of String, String) From {{"lang", translatedLangName}})
                    
                    Dim translationScript = TranslationJsScripts.GetTranslationJS(
                        settings.Language,
                        translatedLangName,
                        tooltipLabel,
                        settings.TranslateMessageButton,
                        settings.FullPageTranslation
                    )
                    Await WebView.CoreWebView2.ExecuteScriptAsync(translationScript)
                End If
            End Sub
            AddHandler WebView.CoreWebView2.NavigationCompleted, _navigationCompletedHandler

            WebView.CoreWebView2.Navigate("https://web.whatsapp.com/")

        Catch ex As Exception
            Debug.WriteLine($"Error configuring WebView2 for account {Id}: {ex.Message}")
        End Try
    End Function

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
            ' Limita le dimensioni del set per prevenire memory leak prolungato
            If ActiveNotificationIds.Count >= MaxActiveNotificationIds Then
                ActiveNotificationIds.Clear()
            End If
            ActiveNotificationIds.Add(notificationId)
            HasNotification = True
            onNotificationChanged?.Invoke(Id, True)

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
                        Dim popup As New MessagePopup(Id, title, body)
                        popup.Show()
                    End Sub)
                Catch ex As Exception
                    Debug.WriteLine($"Failed to show popup: {ex.Message}")
                End Try
            End If

        ElseIf type = "NOTIFICATION_CLOSED" Then
            ActiveNotificationIds.Remove(notificationId)
            HasNotification = (ActiveNotificationIds.Count > 0)
            onNotificationChanged?.Invoke(Id, HasNotification)
        End If
        Return Task.CompletedTask
    End Function

    ''' <summary>
    ''' Elabora le richieste di traduzione singole o batch provenienti dal layer JavaScript della WebView.
    ''' </summary>
    Private Async Function HandleTranslationMessageAsync(root As JsonElement) As Task
        Dim id = root.GetProperty("id").GetString()
        Dim targetLang = root.GetProperty("targetLang").GetString()
        
        Dim isBatch = root.TryGetProperty("type", Nothing) AndAlso root.GetProperty("type").GetString() = "BATCH_TRANSLATE"

        If isBatch Then
            Dim success As Boolean = False
            Dim partsJson As String = ""
            Try
                Dim textsElement = root.GetProperty("texts")
                Dim texts As New List(Of String)()
                For Each item In textsElement.EnumerateArray()
                    texts.Add(item.GetString())
                Next
                
                Dim combinedText = String.Join(vbLf & "###" & vbLf, texts)
                Dim result = Await AppLocalizations.TranslateSingle(combinedText, targetLang)
                
                Dim translatedParts = result.Split(New String() {vbLf & "###" & vbLf, vbLf & " ###" & vbLf, vbLf & "### " & vbLf}, StringSplitOptions.None)
                Dim cleanParts As New List(Of String)()
                For i As Integer = 0 To texts.Count - 1
                    Dim part = If(i < translatedParts.Length, translatedParts(i), texts(i))
                    If String.IsNullOrEmpty(part) Then part = texts(i)
                    cleanParts.Add(part)
                Next
                
                partsJson = JsonSerializer.Serialize(cleanParts)
                success = True
            Catch ex As Exception
                Debug.WriteLine($"Error doing batch translation: {ex.Message}")
            End Try

            If success Then
                Await WebView.Dispatcher.InvokeAsync(Async Function()
                    Await WebView.CoreWebView2.ExecuteScriptAsync(
                        $"if (window.onBatchTranslationReceived) {{ window.onBatchTranslationReceived('{id}', {partsJson}, true); }}"
                    )
                End Function)
            Else
                Await WebView.Dispatcher.InvokeAsync(Async Function()
                    Await WebView.CoreWebView2.ExecuteScriptAsync(
                        $"if (window.onBatchTranslationReceived) {{ window.onBatchTranslationReceived('{id}', [], false); }}"
                    )
                End Function)
            End If
        Else
            Dim success As Boolean = False
            Dim jsonResult As String = ""
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
                        $"if (window.onTranslationReceived) {{ window.onTranslationReceived('{id}', {jsonResult}, true); }}"
                    )
                End Function)
            Else
                Await WebView.Dispatcher.InvokeAsync(Async Function()
                    Await WebView.CoreWebView2.ExecuteScriptAsync(
                        $"if (window.onTranslationReceived) {{ window.onTranslationReceived('{id}', '', false); }}"
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
                Await WebView.CoreWebView2.ExecuteScriptAsync(
                    $"if (window.setTargetLanguage) {{ window.setTargetLanguage('{langCode}', '{langName.Replace("'", "\'")}', '{translateTooltipLabel.Replace("'", "\'")}', {enableHover.ToString().ToLower()}); }}"
                )
            Catch
            End Try
        End If
    End Function

    ''' <summary>
    ''' Rimuove tutti gli event handler registrati sulla WebView2 e libera le risorse allocate.
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        Try
            If WebView IsNot Nothing AndAlso WebView.CoreWebView2 IsNot Nothing Then
                If _permissionRequestedHandler IsNot Nothing Then
                    RemoveHandler WebView.CoreWebView2.PermissionRequested, _permissionRequestedHandler
                    _permissionRequestedHandler = Nothing
                End If
                If _newWindowRequestedHandler IsNot Nothing Then
                    RemoveHandler WebView.CoreWebView2.NewWindowRequested, _newWindowRequestedHandler
                    _newWindowRequestedHandler = Nothing
                End If
                If _webMessageReceivedHandler IsNot Nothing Then
                    RemoveHandler WebView.CoreWebView2.WebMessageReceived, _webMessageReceivedHandler
                    _webMessageReceivedHandler = Nothing
                End If
                If _navigationCompletedHandler IsNot Nothing Then
                    RemoveHandler WebView.CoreWebView2.NavigationCompleted, _navigationCompletedHandler
                    _navigationCompletedHandler = Nothing
                End If
            End If

            If WebView IsNot Nothing Then
                WebView.Dispose()
                WebView = Nothing
            End If

            ActiveNotificationIds.Clear()
        Catch ex As Exception
            Debug.WriteLine($"Error disposing WhatsAppAccount: {ex.Message}")
        End Try
    End Sub
End Class

