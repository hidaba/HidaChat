Imports System.ComponentModel
Imports System.Diagnostics
Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Threading

''' <summary>
''' Gestisce il processo companion tsnetd (nodo Tailscale embedded userspace)
''' ed orchestra il reverse proxy loopback per gli account con TailscaleIntegration = True.
''' </summary>
Public Class TsnetManager
    Implements INotifyPropertyChanged

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private Shared ReadOnly _instance As New Lazy(Of TsnetManager)(Function() New TsnetManager())
    Public Shared ReadOnly Property Instance As TsnetManager
        Get
            Return _instance.Value
        End Get
    End Property

    Public Class RouteInfo
        Public Property TargetUrl As String
        Public Property LocalPort As Integer
    End Class

    Private _process As Process
    Private ReadOnly _ioLock As New SemaphoreSlim(1, 1)
    Private ReadOnly _routes As New Dictionary(Of String, RouteInfo)(StringComparer.OrdinalIgnoreCase)

    Private Shared Function GetOrCreateLocalToken() As String
        Try
            Dim dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "tsnet")
            If Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)
            Dim tokenFilePath = Path.Combine(dir, "local_token.txt")
            If System.IO.File.Exists(tokenFilePath) Then
                Dim tok = System.IO.File.ReadAllText(tokenFilePath).Trim()
                If Not String.IsNullOrWhiteSpace(tok) Then Return tok
            End If
            Dim newTok = Guid.NewGuid().ToString("N")
            System.IO.File.WriteAllText(tokenFilePath, newTok)
            Return newTok
        Catch
            Return Guid.NewGuid().ToString("N")
        End Try
    End Function

    ''' <summary>
    ''' Restituisce la directory di stato isolata per il nodo tsnet associata alla macchina corrente (TODO #60),
    ''' prevenendo la clonazione o sovrascrittura dell'identità del nodo su host differenti in ambiente portabile.
    ''' </summary>
    Public Shared Function GetMachineStateDir() As String
        Dim machineName = Environment.MachineName.Trim()
        Dim safeMachineName = System.Text.RegularExpressions.Regex.Replace(machineName, "[^a-zA-Z0-9_\-]", "_")
        If String.IsNullOrWhiteSpace(safeMachineName) Then safeMachineName = "default"
        Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "tsnet", safeMachineName)
    End Function

    ''' <summary>Fornisce facoltativamente la chiave di autenticazione configurata nelle impostazioni.</summary>
    Public Property AuthKeyProvider As Func(Of String)

    ''' <summary>Azione asincrona invocata una volta completato l'enrollment (passaggio a stato Running).</summary>
    Public Property OnEnrollmentCompletedAsync As Func(Of Task)

    Private ReadOnly _localToken As String = GetOrCreateLocalToken()
    Private _pollTimer As Timer
    Private _isShuttingDown As Boolean = False

    Public ReadOnly Property LocalToken As String
        Get
            Return _localToken
        End Get
    End Property

    Private _nodeState As String = "Stopped"
    Public Property NodeState As String
        Get
            Return _nodeState
        End Get
        Private Set(value As String)
            If _nodeState <> value Then
                _nodeState = value
                NotifyPropertyChanged(NameOf(NodeState))
                NotifyPropertyChanged(NameOf(IsRunning))
                NotifyPropertyChanged(NameOf(NeedsLogin))
                NotifyPropertyChanged(NameOf(StatusDescription))
            End If
        End Set
    End Property

    Private _tailnetIP As String = ""
    Public Property TailnetIP As String
        Get
            Return _tailnetIP
        End Get
        Private Set(value As String)
            If _tailnetIP <> value Then
                _tailnetIP = value
                NotifyPropertyChanged(NameOf(TailnetIP))
                NotifyPropertyChanged(NameOf(StatusDescription))
            End If
        End Set
    End Property

    Private _loginUrl As String = ""
    Public Property LoginUrl As String
        Get
            Return _loginUrl
        End Get
        Private Set(value As String)
            If _loginUrl <> value Then
                _loginUrl = value
                NotifyPropertyChanged(NameOf(LoginUrl))
            End If
        End Set
    End Property

    Public ReadOnly Property IsRunning As Boolean
        Get
            Return String.Equals(NodeState, "Running", StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public ReadOnly Property NeedsLogin As Boolean
        Get
            Return String.Equals(NodeState, "NeedsLogin", StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public ReadOnly Property StatusDescription As String
        Get
            Select Case NodeState
                Case "Running"
                    Return If(Not String.IsNullOrEmpty(TailnetIP), $"Online ({TailnetIP})", "Online")
                Case "NeedsLogin"
                    Return "Richiede autenticazione"
                Case "Starting"
                    Return "Avvio in corso..."
                Case Else
                    Return "Non attivo"
            End Select
        End Get
    End Property

    Private Sub New()
    End Sub

    ''' <summary>
    ''' Trova il percorso del binario tsnetd.exe (nella cartella dell'applicazione o nelle cartelle di sviluppo).
    ''' </summary>
    Private Function FindTsnetdExePath() As String
        Dim baseDir = AppDomain.CurrentDomain.BaseDirectory
        Dim candidate1 = Path.Combine(baseDir, "tsnetd.exe")
        If File.Exists(candidate1) Then Return candidate1

        ' Fallback ambiente di sviluppo
        Dim candidate2 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "tsnetd", "tsnetd.exe"))
        If File.Exists(candidate2) Then Return candidate2

        Dim candidate3 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "tsnetd.exe"))
        If File.Exists(candidate3) Then Return candidate3

        Return candidate1
    End Function

    ''' <summary>
    ''' Avvia tsnetd.exe se non è già in esecuzione.
    ''' </summary>
    Public Async Function StartAsync(Optional authKey As String = "") As Task(Of Boolean)
        Await Task.Yield()
        If _process IsNot Nothing AndAlso Not _process.HasExited Then
            Return True
        End If

        Dim exePath = FindTsnetdExePath()
        If Not File.Exists(exePath) Then
            Debug.WriteLine($"[TsnetManager] tsnetd.exe not found at: {exePath}")
            NodeState = "Stopped"
            Return False
        End If

        Dim stateDir = GetMachineStateDir()
        Directory.CreateDirectory(stateDir)

        Dim effectiveAuthKey = authKey
        If String.IsNullOrWhiteSpace(effectiveAuthKey) AndAlso AuthKeyProvider IsNot Nothing Then
            Try
                effectiveAuthKey = AuthKeyProvider.Invoke()
            Catch
            End Try
        End If

        Dim args = $"-dir ""{stateDir}"" -hostname ""hidachat"""
        If Not String.IsNullOrWhiteSpace(effectiveAuthKey) Then
            args &= $" -authkey ""{effectiveAuthKey.Trim()}"""
        End If

        Dim psi As New ProcessStartInfo With {
            .FileName = exePath,
            .Arguments = args,
            .UseShellExecute = False,
            .RedirectStandardInput = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .CreateNoWindow = True,
            .StandardOutputEncoding = Encoding.UTF8,
            .StandardErrorEncoding = Encoding.UTF8
        }

        Try
            _isShuttingDown = False
            _process = New Process With {.StartInfo = psi, .EnableRaisingEvents = True}
            AddHandler _process.Exited, Sub()
                Debug.WriteLine("[TsnetManager] tsnetd.exe process exited.")
                If Not _isShuttingDown Then
                    NodeState = "Stopped"
                    TailnetIP = ""
                    LoginUrl = ""
                End If
            End Sub

            _process.Start()
            _process.BeginErrorReadLine()
            AddHandler _process.ErrorDataReceived, Sub(s, e)
                If Not String.IsNullOrEmpty(e.Data) Then
                    Debug.WriteLine($"[tsnetd stderr] {e.Data}")
                    AppLogger.LogApp($"[tsnetd] {e.Data}")
                End If
            End Sub

            NodeState = "Starting"
            ' Avvia immediatamente il polling dello stato in background senza bloccare il thread chiamante
            StartStatusPolling()
            Dim ignoreCheck = Task.Run(Async Function()
                Try
                    Await CheckStatusAsync()
                Catch
                End Try
            End Function)
            Return True
        Catch ex As Exception
            Debug.WriteLine($"[TsnetManager] Failed to start tsnetd: {ex.Message}")
            NodeState = "Stopped"
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Avvia o reimposta il timer di polling dello stato.
    ''' </summary>
    Private Sub StartStatusPolling()
        If _pollTimer Is Nothing Then
            _pollTimer = New Timer(Async Sub(state)
                If _isShuttingDown Then Return
                If _process Is Nothing OrElse _process.HasExited Then Return

                Try
                    Await CheckStatusAsync()
                Catch ex As Exception
                    Debug.WriteLine($"[TsnetManager] Polling error: {ex.Message}")
                End Try
            End Sub, Nothing, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3))
        End If
    End Sub

    ''' <summary>
    ''' Invia un comando JSON e legge la riga di risposta.
    ''' </summary>
    Private Async Function SendCommandAsync(cmdJson As String) As Task(Of String)
        If _process Is Nothing OrElse _process.HasExited Then
            Return Nothing
        End If

        Await _ioLock.WaitAsync()
        Try
            Await _process.StandardInput.WriteLineAsync(cmdJson)
            Await _process.StandardInput.FlushAsync()

            Dim responseLine = Await _process.StandardOutput.ReadLineAsync()
            Return responseLine
        Catch ex As Exception
            Debug.WriteLine($"[TsnetManager] SendCommandAsync error: {ex.Message}")
            Return Nothing
        Finally
            _ioLock.Release()
        End Try
    End Function

    ''' <summary>
    ''' Richiede lo stato del nodo tsnet e aggiorna le proprietà.
    ''' </summary>
    Public Async Function CheckStatusAsync() As Task
        Dim cmd = JsonSerializer.Serialize(New With {Key .cmd = "status"})
        Dim resp = Await SendCommandAsync(cmd)
        If String.IsNullOrWhiteSpace(resp) Then Return

        Try
            Using doc As JsonDocument = JsonDocument.Parse(resp)
                Dim root = doc.RootElement
                If root.TryGetProperty("nodeState", Nothing) Then
                    Dim newState = root.GetProperty("nodeState").GetString()
                    Dim wasRunning = String.Equals(NodeState, "Running", StringComparison.OrdinalIgnoreCase)
                    NodeState = newState
                    If Not wasRunning AndAlso String.Equals(newState, "Running", StringComparison.OrdinalIgnoreCase) Then
                        If OnEnrollmentCompletedAsync IsNot Nothing Then
                            Try
                                Await OnEnrollmentCompletedAsync.Invoke()
                            Catch ex As Exception
                                Debug.WriteLine($"[TsnetManager] Error in OnEnrollmentCompletedAsync: {ex.Message}")
                            End Try
                        End If
                    End If
                End If
                If root.TryGetProperty("tailnetIP", Nothing) Then
                    TailnetIP = root.GetProperty("tailnetIP").GetString()
                End If
                If root.TryGetProperty("loginUrl", Nothing) Then
                    LoginUrl = root.GetProperty("loginUrl").GetString()
                End If
            End Using
        Catch ex As Exception
            Debug.WriteLine($"[TsnetManager] Parse status error: {ex.Message}")
        End Try
    End Function

    ''' <summary>
    ''' Registra o aggiorna una rotta di reverse proxy per un account verso un target della tailnet.
    ''' </summary>
    Public Async Function EnsureRouteAsync(accountId As String, targetUrl As String, Optional authKey As String = "", Optional forceUpdate As Boolean = False, Optional preferredPort As Integer = 0) As Task(Of Integer)
        If String.IsNullOrWhiteSpace(accountId) OrElse String.IsNullOrWhiteSpace(targetUrl) Then
            Return 0
        End If

        Dim cleanTarget = targetUrl.Trim()

        SyncLock _routes
            If Not forceUpdate AndAlso _routes.ContainsKey(accountId) Then
                Dim existing = _routes(accountId)
                If String.Equals(existing.TargetUrl, cleanTarget, StringComparison.OrdinalIgnoreCase) AndAlso (preferredPort <= 0 OrElse existing.LocalPort = preferredPort) Then
                    Return existing.LocalPort
                End If
            End If
        End SyncLock

        If Not Await StartAsync(authKey) Then
            Return 0
        End If

        Dim payload As New Dictionary(Of String, Object) From {
            {"cmd", "add_route"},
            {"accountId", accountId},
            {"target", cleanTarget},
            {"localToken", _localToken}
        }
        If preferredPort > 0 Then
            payload("port") = preferredPort
        End If

        Dim cmd = JsonSerializer.Serialize(payload)

        Dim resp = Await SendCommandAsync(cmd)
        If String.IsNullOrWhiteSpace(resp) Then Return 0

        Try
            Using doc As JsonDocument = JsonDocument.Parse(resp)
                Dim root = doc.RootElement
                Dim success = root.GetProperty("success").GetBoolean()
                If success AndAlso root.TryGetProperty("localPort", Nothing) Then
                    Dim port = root.GetProperty("localPort").GetInt32()
                    SyncLock _routes
                        _routes(accountId) = New RouteInfo With {
                            .TargetUrl = cleanTarget,
                            .LocalPort = port
                        }
                    End SyncLock
                    Debug.WriteLine($"[TsnetManager] Registered route for {accountId} -> 127.0.0.1:{port} -> {cleanTarget}")
                    Return port
                End If
            End Using
        Catch ex As Exception
            Debug.WriteLine($"[TsnetManager] AddRoute error: {ex.Message}")
        End Try

        Return 0
    End Function

    ''' <summary>
    ''' Rimuove una rotta registrata per un account.
    ''' </summary>
    Public Async Function RemoveRouteAsync(accountId As String) As Task
        SyncLock _routes
            If Not _routes.ContainsKey(accountId) Then Return
            _routes.Remove(accountId)
        End SyncLock

        If _process Is Nothing OrElse _process.HasExited Then Return

        Dim cmd = JsonSerializer.Serialize(New With {
            Key .cmd = "remove_route",
            Key .accountId = accountId
        })
        Await SendCommandAsync(cmd)

        Dim shouldShutdown As Boolean = False
        SyncLock _routes
            If _routes.Count = 0 Then
                shouldShutdown = True
            End If
        End SyncLock

        If shouldShutdown Then
            Await ShutdownAsync()
        End If
    End Function

    ''' <summary>
    ''' Chiude ordinatamente tsnetd e attende fino a 2 secondi prima di forzare il kill.
    ''' </summary>
    Public Async Function ShutdownAsync() As Task
        If _process Is Nothing OrElse _process.HasExited Then
            NodeState = "Stopped"
            Return
        End If

        _isShuttingDown = True
        If _pollTimer IsNot Nothing Then
            _pollTimer.Dispose()
            _pollTimer = Nothing
        End If

        Try
            Dim cmd = JsonSerializer.Serialize(New With {Key .cmd = "shutdown"})
            Await SendCommandAsync(cmd)

            Dim exited = Await Task.Run(Function() _process.WaitForExit(2000))
            If Not exited AndAlso Not _process.HasExited Then
                Debug.WriteLine("[TsnetManager] tsnetd did not exit in 2s, terminating forcibly...")
                _process.Kill()
            End If
        Catch ex As Exception
            Debug.WriteLine($"[TsnetManager] Shutdown exception: {ex.Message}")
            Try
                If Not _process.HasExited Then _process.Kill()
            Catch
            End Try
        Finally
            _process.Dispose()
            _process = Nothing
            SyncLock _routes
                _routes.Clear()
            End SyncLock
            NodeState = "Stopped"
            TailnetIP = ""
            LoginUrl = ""
        End Try
    End Function

    ''' <summary>
    ''' Verifica la connettività verso la rete Tailscale e l'endpoint remoto (TODO #75).
    ''' Restituisce la diagnosi dettagliata (es. tsnet richiede login, tsnet fermo, nodo non raggiungibile, o client Tailscale non attivo).
    ''' </summary>
    Public Async Function CheckTailscaleConnectivityAsync(targetUrl As String, isUsingTsnet As Boolean, Optional localProxyPort As Integer = 0) As Task(Of TailscaleConnectivityStatus)
        Dim result As New TailscaleConnectivityStatus()
        Dim host = ""
        Dim port = 80

        Try
            Dim uri As Uri = Nothing
            If Uri.TryCreate(targetUrl, UriKind.Absolute, uri) Then
                host = uri.Host
                port = uri.Port
            Else
                Dim raw = targetUrl.Replace("http://", "").Replace("https://", "").Split("/"c)(0)
                If raw.Contains(":"c) Then
                    Dim parts = raw.Split(":"c)
                    host = parts(0)
                    Integer.TryParse(parts(1), port)
                Else
                    host = raw
                End If
            End If
        Catch
            host = targetUrl
        End Try

        result.Details = host

        If isUsingTsnet Then
            Try
                Await CheckStatusAsync()
            Catch
            End Try

            If String.Equals(NodeState, "NeedsLogin", StringComparison.OrdinalIgnoreCase) Then
                result.IsConnected = False
                result.StatusKey = "tailscale_tsnet_needs_login"
                result.LoginUrl = _loginUrl
                Return result
            End If

            If Not IsRunning Then
                result.IsConnected = False
                result.StatusKey = "tailscale_tsnet_stopped"
                Return result
            End If

            ' Il nodo tsnet locale è Running. Verifichiamo la raggiungibilità dell'endpoint tramite proxy o socket
            If localProxyPort > 0 Then
                Try
                    Using handler As New System.Net.Http.SocketsHttpHandler()
                        handler.SslOptions.RemoteCertificateValidationCallback = Function(m, c, ch, p) True
                        Using client As New System.Net.Http.HttpClient(handler)
                            client.Timeout = TimeSpan.FromSeconds(3.5)
                            If Not String.IsNullOrEmpty(_localToken) Then
                                client.DefaultRequestHeaders.Add("X-HidaChat-Local-Token", _localToken)
                            End If
                            Using response = Await client.GetAsync($"http://127.0.0.1:{localProxyPort}/", System.Net.Http.HttpCompletionOption.ResponseHeadersRead)
                                Dim code = CInt(response.StatusCode)
                                If code = 502 OrElse code = 504 Then
                                    result.IsConnected = False
                                    result.StatusKey = "tailscale_node_unreachable"
                                    Return result
                                Else
                                    result.IsConnected = True
                                    result.StatusKey = "tailscale_ok"
                                    Return result
                                End If
                            End Using
                        End Using
                    End Using
                Catch exHttp As Exception
                    result.IsConnected = False
                    result.StatusKey = "tailscale_node_unreachable"
                    Return result
                End Try
            End If

            result.IsConnected = True
            result.StatusKey = "tailscale_ok"
            Return result
        Else
            ' Connessione diretta o client Tailscale nativo di sistema operativo Windows
            Dim canConnect = False
            Try
                Using tcp As New System.Net.Sockets.TcpClient()
                    Dim connectTask = tcp.ConnectAsync(host, port)
                    Dim completed = Await Task.WhenAny(connectTask, Task.Delay(3000))
                    If completed Is connectTask AndAlso tcp.Connected Then
                        canConnect = True
                    End If
                End Using
            Catch
                canConnect = False
            End Try

            If canConnect Then
                result.IsConnected = True
                result.StatusKey = "tailscale_ok"
                Return result
            End If

            ' Connessione fallita: verifica se il client Tailscale Windows è attivo nel sistema
            Dim tailscaleClientFound = False
            Try
                Dim procs = Process.GetProcessesByName("tailscale").Concat(Process.GetProcessesByName("tailscaled")).ToArray()
                If procs.Length > 0 Then
                    tailscaleClientFound = True
                End If
            Catch
            End Try

            If Not tailscaleClientFound Then
                result.IsConnected = False
                result.StatusKey = "tailscale_client_disconnected"
            Else
                result.IsConnected = False
                result.StatusKey = "tailscale_node_unreachable"
            End If

            Return result
        End If
    End Function

    Private Sub NotifyPropertyChanged(propertyName As String)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub
End Class

''' <summary>
''' Stato diagnostico della connettività Tailscale (TODO #75).
''' </summary>
Public Class TailscaleConnectivityStatus
    Public Property IsConnected As Boolean = False
    Public Property StatusKey As String = ""
    Public Property Details As String = ""
    Public Property LoginUrl As String = ""
End Class
