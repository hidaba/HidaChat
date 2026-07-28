Imports System.Threading

''' <summary>
''' Gestisce il ciclo di vita dell'applicazione WPF e garantisce l'esecuzione in istanza singola tramite Mutex.
''' </summary>
Class Application
    Private Shared _mutex As Mutex

    ''' <summary>
    ''' Invocato all'avvio dell'applicazione. Inizializza il Mutex per impedire l'esecuzione di più istanze contemporanee.
    ''' </summary>
    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        Dim createdNew As Boolean
        _mutex = New Mutex(True, "WhatsAppVB_SingleInstance", createdNew)

        If Not createdNew Then
            MessageBox.Show("L'applicazione è già in esecuzione.", "WhatsAppVB", MessageBoxButton.OK, MessageBoxImage.Information)
            _mutex = Nothing
            Environment.Exit(0)
        End If

        MyBase.OnStartup(e)
    End Sub

    ''' <summary>
    ''' Invocato alla chiusura dell'applicazione. Rilascia e rimuove il Mutex dell'istanza singola.
    ''' </summary>
    Protected Overrides Sub OnExit(e As ExitEventArgs)
        If _mutex IsNot Nothing Then
            _mutex.ReleaseMutex()
            _mutex.Dispose()
            _mutex = Nothing
        End If
        MyBase.OnExit(e)
    End Sub
End Class

