Imports System.IO
Imports System.Text.Json.Serialization
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.Wpf
Imports Microsoft.Toolkit.Uwp.Notifications
Imports System.Text.Json

Public Class WhatsAppAccount
    <JsonPropertyName("id")>
    Public Property Id As String

    <JsonPropertyName("name")>
    Public Property Name As String

    <JsonPropertyName("isActive")>
    Public Property IsActive As Boolean
    
    <JsonIgnore>
    Public Property HasNotification As Boolean
    
    <JsonIgnore>
    Public Property BridgeToken As String
    
    <JsonIgnore>
    Public Property WebView As WebView2
    
    Private _syncWorker As ChatSyncBackgroundWorker
    <JsonIgnore>
    Public ReadOnly Property SyncWorker As ChatSyncBackgroundWorker
        Get
            If _syncWorker Is Nothing Then
                _syncWorker = New ChatSyncBackgroundWorker(Id)
            End If
            Return _syncWorker
        End Get
    End Property
    
    <JsonIgnore>
    Public ReadOnly Property ActiveNotificationIds As New HashSet(Of String)()

    Public Shared ReadOnly Property SharedDataDirectory As String
        Get
            Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "webview")
        End Get
    End Property

    Public Shared Function GenerateId() As String
        Return "account_" & DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    End Function

    Private Shared Function GenerateBridgeToken() As String
        Dim rand As New Random()
        Return "bt_" & DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & "_" & rand.Next(100000, 999999)
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

    Public Async Function SetupWebViewAsync(settings As SettingsController, onNotificationChanged As Action(Of String, Boolean)) As Task
        If WebView Is Nothing Then Return

        Dim profileDir = Path.Combine(SharedDataDirectory, $"WV2Profile_{Id}")
        If Not Directory.Exists(profileDir) Then
            ' Ultimo tentativo: ricollega il profilo orfano WV2Profile_ se il target non esiste
            Dim orphanProfile = Path.Combine(SharedDataDirectory, "WV2Profile_")
            If Directory.Exists(orphanProfile) Then
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
        End If

        Try
            ' Initialize WebView2 Environment with isolation
            Dim options As New CoreWebView2EnvironmentOptions()
            Dim env = Await CoreWebView2Environment.CreateAsync(Nothing, SharedDataDirectory, options)
            
            ' Set the userDataFolder inside options or pass environment. We must set profile name.
            ' In WebView2, the profile name can be specified via CoreWebView2ControllerOptions (available in newer SDKs)
            ' or we can just specify the full profile directory as the userDataFolder.
            ' Dart uses: userDataFolder: userDataFolder, profileName: accountId
            ' In Microsoft WebView2, we can create an environment pointing to the profile folder directly as the userDataFolder.
            Dim accountEnv = Await CoreWebView2Environment.CreateAsync(Nothing, profileDir, options)
            
            Await WebView.EnsureCoreWebView2Async(accountEnv)
            
            ' Configure settings
            WebView.CoreWebView2.Settings.IsWebMessageEnabled = True
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = True
            
            ' Grant notifications permission automatically
            AddHandler WebView.CoreWebView2.PermissionRequested, Sub(sender, e)
                If e.PermissionKind = CoreWebView2PermissionKind.Notifications Then
                    e.State = CoreWebView2PermissionState.Allow
                    e.Handled = True
                End If
            End Sub

            ' Inject bridge token and notification override before any script runs (combined to avoid races)
            Dim initScript = $"window.__bridgeToken = '{BridgeToken}';" & vbCrLf & NotificationJsScripts.NotificationOverrideJS
            Await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(initScript)

            ' Handle navigation and links
            AddHandler WebView.CoreWebView2.NewWindowRequested, Sub(sender, e)
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

            ' Register JS Bridge Channel
            AddHandler WebView.CoreWebView2.WebMessageReceived, Async Sub(sender, e)
                Await HandleWebMessageAsync(e.WebMessageAsJson, settings, onNotificationChanged)
            End Sub

            ' Setup script injection and theme injection on page finished load
            AddHandler WebView.CoreWebView2.NavigationCompleted, Async Sub(sender, e)
                If e.IsSuccess Then
                    
                    ' Inject theme CSS
                    Dim brightnessDark = False
                    If settings.Theme = "Dark" Then
                        brightnessDark = True
                    ElseIf settings.Theme = "System" Then
                        ' Check Windows App Theme
                        Try
                            Dim key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")
                            If key IsNot Nothing Then
                                Dim val = key.GetValue("AppsUseLightTheme")
                                If val IsNot Nothing AndAlso Convert.ToInt32(val) = 0 Then
                                    brightnessDark = True
                                End If
                            End If
                        Catch
                        End Try
                    End If

                    If brightnessDark Then
                        Await WebView.CoreWebView2.ExecuteScriptAsync(ThemeJsScripts.DarkModeJS)
                    Else
                        Await WebView.CoreWebView2.ExecuteScriptAsync(ThemeJsScripts.LightModeJS)
                    End If

                    ' Inject translation script
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

                    ' Inietta lo script di sincronizzazione chat e avvia la sincronizzazione in background
                    Await WebView.CoreWebView2.ExecuteScriptAsync(ChatSyncJsScripts.ChatSyncJS)
                    Await SyncWorker.RequestSyncAsync(WebView, BridgeToken)
                End If
            End Sub

            ' Load WhatsApp Web
            WebView.CoreWebView2.Navigate("https://web.whatsapp.com/")

        Catch ex As Exception
            Debug.WriteLine($"Error configuring WebView2 for account {Id}: {ex.Message}")
            ' Se WebView2 non è installato, il controllo viene fatto all'avvio in MainWindow,
            ' quindi qui l'eccezione è probabilmente un errore di rete/navigazione.
        End Try
    End Function

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
                ElseIf channel = "ChatSyncChannel" Then
                    Await SyncWorker.ProcessIncomingBatchAsync(root)
                End If
            End Using
        Catch ex As Exception
            Debug.WriteLine($"Error handling web message: {ex.Message}")
        End Try
    End Function

    Private Function HandleNotificationMessageAsync(root As JsonElement, settings As SettingsController, onNotificationChanged As Action(Of String, Boolean)) As Task
        Dim type = root.GetProperty("type").GetString()
        Dim notificationId = root.GetProperty("id").GetString()
        
        Debug.WriteLine($"[NotificationChannel] accountId={Id}, type={type}, id={notificationId}")

        If type = "NOTIFICATION_RECEIVED" Then
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
End Class
