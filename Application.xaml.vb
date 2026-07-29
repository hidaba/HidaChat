Imports System.Diagnostics
Imports System.Threading

''' <summary>
''' Gestisce il ciclo di vita dell'applicazione WPF e garantisce l'esecuzione in istanza singola tramite Mutex e verifica dei processi attivi.
''' </summary>
Class Application
    Private Shared _mutex As Mutex

    ''' <summary>
    ''' Invocato all'avvio dell'applicazione. Inizializza il Mutex per impedire l'esecuzione di più istanze contemporanee.
    ''' </summary>
    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        Dim createdNew As Boolean = False
        _mutex = New Mutex(True, "Local\WhatsAppVB_SingleInstance_Mutex", createdNew)

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
            ' Verifichiamo se esiste effettivamente un altro processo WhatsappH in esecuzione
            Dim currentProc = Process.GetCurrentProcess()
            Dim otherProcesses = Process.GetProcessesByName(currentProc.ProcessName) _
                                        .Where(Function(p) p.Id <> currentProc.Id).ToList()

            If otherProcesses.Count > 0 Then
                MessageBox.Show("L'applicazione è già in esecuzione.", "WhatsAppVB", MessageBoxButton.OK, MessageBoxImage.Information)
                ReleaseSingleInstanceMutex()
                Environment.Exit(0)
                Return
            End If
        End If

        MyBase.OnStartup(e)
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
    ''' Invocato alla chiusura dell'applicazione. Rilascia e rimuove il Mutex dell'istanza singola.
    ''' </summary>
    Protected Overrides Sub OnExit(e As ExitEventArgs)
        ReleaseSingleInstanceMutex()
        MyBase.OnExit(e)
    End Sub
End Class

