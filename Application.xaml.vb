Imports System.Diagnostics
Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Threading

''' <summary>
''' Gestisce il ciclo di vita dell'applicazione WPF, garantisce il lock di cartella multi-PC (data/.lock)
''' e impedisce l'esecuzione di più istanze contemporanee tramite Mutex e verifica dei processi attivi.
''' </summary>
Class Application
    Private Shared _mutex As Mutex
    Private Shared ReadOnly _logLock As New Object()

    ''' <summary>Metadati serializzati nel file di lock data/.lock per diagnostica multi-PC (TODO #70).</summary>
    Public Class PortableFolderLockInfo
        Public Property Machine As String
        Public Property ProcessId As Integer
        Public Property User As String
        Public Property Timestamp As String
    End Class

    Private Shared _folderLockStream As FileStream = Nothing
    Private Shared ReadOnly _folderLockPath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", ".lock")

    ''' <summary>
    ''' Acquisisce un lease esclusivo a livello di filesystem sulla cartella dell'applicazione (TODO #70),
    ''' impedendo che più postazioni su disco di rete o processi concorrenti corrompano i profili WebView2.
    ''' </summary>
    Private Shared Function TryAcquireFolderLock(ByRef existingLock As PortableFolderLockInfo) As Boolean
        existingLock = Nothing
        Try
            Dim dataDir = Path.GetDirectoryName(_folderLockPath)
            If Not Directory.Exists(dataDir) Then
                Directory.CreateDirectory(dataDir)
            End If

            ' Apertura in scrittura esclusiva ma con FileShare.Read per consentire a un'istanza concorrente
            ' di leggere i metadati dell'istanza bloccante e visualizzare una notifica informativa.
            _folderLockStream = New FileStream(_folderLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read)

            Dim info As New PortableFolderLockInfo With {
                .Machine = Environment.MachineName,
                .ProcessId = Process.GetCurrentProcess().Id,
                .User = Environment.UserName,
                .Timestamp = DateTime.UtcNow.ToString("o")
            }
            Dim json = JsonSerializer.Serialize(info, New JsonSerializerOptions With {.WriteIndented = True})
            Dim bytes = Encoding.UTF8.GetBytes(json)

            _folderLockStream.SetLength(0)
            _folderLockStream.Write(bytes, 0, bytes.Length)
            _folderLockStream.Flush()
            Return True
        Catch ex As IOException
            ' La risorsa è bloccata da un'altra istanza o macchina
            Try
                If File.Exists(_folderLockPath) Then
                    Using fs As New FileStream(_folderLockPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                        Using reader As New StreamReader(fs, Encoding.UTF8)
                            Dim content = reader.ReadToEnd()
                            If Not String.IsNullOrWhiteSpace(content) Then
                                existingLock = JsonSerializer.Deserialize(Of PortableFolderLockInfo)(content)
                            End If
                        End Using
                    End Using
                End If
            Catch
            End Try
            Return False
        Catch ex As Exception
            Debug.WriteLine($"[FolderLock] Warning: verifica lock non riuscita: {ex.Message}")
            Return True
        End Try
    End Function

    ''' <summary>
    ''' Rilascia il lock di cartella e rimuove data/.lock alla chiusura dell'applicazione.
    ''' </summary>
    Public Shared Sub ReleaseFolderLock()
        If _folderLockStream IsNot Nothing Then
            Try
                _folderLockStream.Dispose()
            Catch
            End Try
            _folderLockStream = Nothing
            Try
                If File.Exists(_folderLockPath) Then
                    File.Delete(_folderLockPath)
                End If
            Catch
            End Try
        End If
    End Sub

    ''' <summary>
    ''' Invocato all'avvio dell'applicazione. Inizializza il lock di cartella e il Mutex per impedire l'esecuzione concorrente.
    ''' </summary>
    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        ' Registrazione listener globali per eccezioni non gestite (TODO #63)
        AddHandler Me.DispatcherUnhandledException, AddressOf OnDispatcherUnhandledException
        AddHandler TaskScheduler.UnobservedTaskException, AddressOf OnUnobservedTaskException
        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnAppDomainUnhandledException

        ' 1. Verifica lock multi-PC / cartella condivisa (TODO #70)
        Dim existingLock As PortableFolderLockInfo = Nothing
        If Not TryAcquireFolderLock(existingLock) Then
            Dim machine = If(existingLock?.Machine, "Sconosciuto")
            Dim pid = If(existingLock IsNot Nothing AndAlso existingLock.ProcessId > 0, existingLock.ProcessId.ToString(), "N/D")
            Dim user = If(existingLock?.User, "N/D")
            Dim time = If(existingLock?.Timestamp, "N/D")

            Dim lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant()
            Dim title As String
            Dim msg As String

            Select Case lang
                Case "it"
                    title = "Cartella Applicazione in Uso"
                    msg = $"La cartella di HidaChat è attualmente bloccata ed in uso da un'altra postazione o processo:" & vbCrLf & vbCrLf &
                          $"• Computer: {machine}" & vbCrLf &
                          $"• Utente: {user}" & vbCrLf &
                          $"• PID: {pid}" & vbCrLf &
                          $"• Avviato il: {time}" & vbCrLf & vbCrLf &
                          "Per prevenire la corruzione dei profili WebView2 e delle sessioni, non è consentito avviare contemporaneamente più istanze dalla stessa cartella."
                Case "fr"
                    title = "Dossier d'Application en Cours d'Utilisation"
                    msg = $"Le dossier HidaChat est actuellement verrouillé et utilisé par un autre poste ou processus :" & vbCrLf & vbCrLf &
                          $"• Ordinateur : {machine}" & vbCrLf &
                          $"• Utilisateur : {user}" & vbCrLf &
                          $"• PID : {pid}" & vbCrLf &
                          $"• Démarré le : {time}" & vbCrLf & vbCrLf &
                          "Pour éviter toute corruption des profils WebView2 et des sessions, l'exécution simultanée depuis le même dossier n'est pas autorisée."
                Case "es"
                    title = "Carpeta de la Aplicación en Uso"
                    msg = $"La carpeta de HidaChat está actualmente bloqueada y en uso por otra estación o proceso:" & vbCrLf & vbCrLf &
                          $"• Equipo: {machine}" & vbCrLf &
                          $"• Usuario: {user}" & vbCrLf &
                          $"• PID: {pid}" & vbCrLf &
                          $"• Iniciado: {time}" & vbCrLf & vbCrLf &
                          "Para prevenir la corrupción de los perfiles WebView2 y las sesiones, no se permite ejecutar simultáneamente múltiples instancias desde la misma carpeta."
                Case "de"
                    title = "Anwendungsordner wird Bereits Verwendet"
                    msg = $"Der HidaChat-Ordner ist derzeit gesperrt und wird von einem anderen Computer oder Prozess verwendet:" & vbCrLf & vbCrLf &
                          $"• Computer: {machine}" & vbCrLf &
                          $"• Benutzer: {user}" & vbCrLf &
                          $"• PID: {pid}" & vbCrLf &
                          $"• Gestartet am: {time}" & vbCrLf & vbCrLf &
                          "Um eine Beschädigung der WebView2-Profile und Sitzungen zu verhindern, können nicht mehrere Instanzen gleichzeitig aus demselben Ordner ausgeführt werden."
                Case Else
                    title = "Application Folder in Use"
                    msg = $"The HidaChat folder is currently locked and in use by another instance or computer:" & vbCrLf & vbCrLf &
                          $"• Machine: {machine}" & vbCrLf &
                          $"• User: {user}" & vbCrLf &
                          $"• PID: {pid}" & vbCrLf &
                          $"• Started at: {time}" & vbCrLf & vbCrLf &
                          "To prevent WebView2 profile and session data corruption, running multiple concurrent instances from the same folder is not allowed."
            End Select

            MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Stop)
            ReleaseSingleInstanceMutex()
            Environment.Exit(0)
            Return
        End If

        ' 2. Verifica istanza singola locale tramite Mutex
        Dim createdNew As Boolean = False
        _mutex = New Mutex(True, "Local\HidaChat_SingleInstance_Mutex", createdNew)

        Dim hasHandle As Boolean = False
        Try
            hasHandle = _mutex.WaitOne(TimeSpan.FromMilliseconds(500), False)
        Catch ex As AbandonedMutexException
            ' Il processo precedente è stato terminato anomalamante o via Task Manager senza rilasciare il Mutex.
            ' Acquisiamo la proprietà del Mutex abbandonato.
            hasHandle = True
        Catch ex As Exception
            hasHandle = createdNew
        End Try

        If Not hasHandle Then
            ' Verifichiamo se esiste effettivamente un altro processo HidaChat in esecuzione
            Dim currentProc = Process.GetCurrentProcess()
            Dim otherProcesses = Process.GetProcessesByName(currentProc.ProcessName) _
                                        .Where(Function(p) p.Id <> currentProc.Id).ToList()

            If otherProcesses.Count > 0 Then
                MessageBox.Show("L'applicazione è già in esecuzione.", "HidaChat", MessageBoxButton.OK, MessageBoxImage.Information)
                ReleaseSingleInstanceMutex()
                Environment.Exit(0)
                Return
            End If
        End If

        CleanupLegacyFiles()

        MyBase.OnStartup(e)
    End Sub

    ''' <summary>
    ''' Rimuove eventuali file binari residui della vecchia denominazione WhatsappH.
    ''' </summary>
    Private Shared Sub CleanupLegacyFiles()
        Try
            Dim baseDir = AppDomain.CurrentDomain.BaseDirectory
            Dim legacyFiles = {"WhatsappH.dll", "WhatsappH.pdb", "WhatsappH.deps.json", "WhatsappH.runtimeconfig.json"}
            For Each f In legacyFiles
                Dim fullPath = System.IO.Path.Combine(baseDir, f)
                If System.IO.File.Exists(fullPath) Then
                    Try
                        System.IO.File.Delete(fullPath)
                    Catch
                    End Try
                End If
            Next
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' Rilascia e dispone in modo sicuro il Mutex dell'istanza singola e il lock di cartella.
    ''' </summary>
    Public Shared Sub ReleaseSingleInstanceMutex()
        ReleaseFolderLock()
        If _mutex IsNot Nothing Then
            Try
                _mutex.ReleaseMutex()
            Catch
            End Try
            Try
                _mutex.Dispose()
            Catch
            End Try
            _mutex = Nothing
        End If
    End Sub

    ''' <summary>
    ''' Invocato quando l'utente disconnette la sessione o Windows viene arrestato/riavviato (WM_QUERYENDSESSION / WM_ENDSESSION).
    ''' Consente la persistenza e la chiusura ordinata di WebView2 senza far bloccare lo shutdown dal cancel di MainWindow.
    ''' </summary>
    Protected Overrides Sub OnSessionEnding(e As SessionEndingCancelEventArgs)
        Try
            Dim mainWin = TryCast(MainWindow, MainWindow)
            If mainWin IsNot Nothing Then
                mainWin.PrepareForShutdown()
            End If
        Catch ex As Exception
            Debug.WriteLine($"OnSessionEnding error: {ex.Message}")
        End Try
        MyBase.OnSessionEnding(e)
    End Sub

    Private Sub OnDispatcherUnhandledException(sender As Object, e As DispatcherUnhandledExceptionEventArgs)
        LogUnhandledException("DispatcherUnhandledException", e.Exception)
        ' Evita il crash istantaneo dell'applicazione per eccezioni non critiche a livello di UI
        e.Handled = True
    End Sub

    Private Sub OnUnobservedTaskException(sender As Object, e As UnobservedTaskExceptionEventArgs)
        LogUnhandledException("TaskScheduler.UnobservedTaskException", e.Exception)
        ' Marca l'eccezione come osservata per impedire l'escalation a crash di processo
        e.SetObserved()
    End Sub

    Private Sub OnAppDomainUnhandledException(sender As Object, e As UnhandledExceptionEventArgs)
        Dim ex = TryCast(e.ExceptionObject, Exception)
        LogUnhandledException($"AppDomain.UnhandledException (IsTerminating={e.IsTerminating})", ex)
    End Sub

    ''' <summary>
    ''' Registra un'eccezione non gestita sul file di log portabile 'data/logs/app_errors.log' (TODO #63).
    ''' </summary>
    Public Shared Sub LogUnhandledException(source As String, ex As Exception)
        Try
            Dim baseDir = AppDomain.CurrentDomain.BaseDirectory
            Dim logsDir = Path.Combine(baseDir, "data", "logs")
            If Not Directory.Exists(logsDir) Then
                Directory.CreateDirectory(logsDir)
            End If

            Dim logFile = Path.Combine(logsDir, "app_errors.log")
            Dim sb As New System.Text.StringBuilder()
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}]")
            If ex IsNot Nothing Then
                sb.AppendLine($"Tipo: {ex.GetType().FullName}")
                sb.AppendLine($"Messaggio: {ex.Message}")
                sb.AppendLine($"StackTrace: {ex.StackTrace}")
                If ex.InnerException IsNot Nothing Then
                    sb.AppendLine($"InnerException: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}")
                    sb.AppendLine($"InnerStackTrace: {ex.InnerException.StackTrace}")
                End If
            Else
                sb.AppendLine("Dettagli eccezione non disponibili.")
            End If
            sb.AppendLine(New String("-"c, 60))

            SyncLock _logLock
                Dim fi As New FileInfo(logFile)
                If fi.Exists AndAlso fi.Length > 1024 * 1024 Then
                    Dim oldFile = Path.Combine(logsDir, "app_errors.log.old")
                    If File.Exists(oldFile) Then File.Delete(oldFile)
                    File.Move(logFile, oldFile)
                End If
                File.AppendAllText(logFile, sb.ToString())
            End SyncLock
            Debug.WriteLine($"[CRITICAL] {source}: {ex?.Message}")
        Catch
            ' Fail-safe
        End Try
    End Sub

    ''' <summary>
    ''' Invocato alla chiusura dell'applicazione. Rilascia e rimuove il Mutex dell'istanza singola.
    ''' </summary>
    Protected Overrides Sub OnExit(e As ExitEventArgs)
        ReleaseSingleInstanceMutex()
        MyBase.OnExit(e)
    End Sub
End Class

