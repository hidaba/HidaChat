Imports System.Diagnostics
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Threading

''' <summary>
''' Gestisce il ciclo di vita dell'applicazione WPF e garantisce l'esecuzione in istanza singola tramite Mutex e verifica dei processi attivi.
''' </summary>
Class Application
    Private Shared _mutex As Mutex
    Private Shared ReadOnly _logLock As New Object()

    ''' <summary>
    ''' Invocato all'avvio dell'applicazione. Inizializza il Mutex per impedire l'esecuzione di più istanze contemporanee.
    ''' </summary>
    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        ' Registrazione listener globali per eccezioni non gestite (TODO #63)
        AddHandler Me.DispatcherUnhandledException, AddressOf OnDispatcherUnhandledException
        AddHandler TaskScheduler.UnobservedTaskException, AddressOf OnUnobservedTaskException
        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnAppDomainUnhandledException

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
    ''' Rilascia e dispone in modo sicuro il Mutex dell'istanza singola.
    ''' </summary>
    Public Shared Sub ReleaseSingleInstanceMutex()
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

