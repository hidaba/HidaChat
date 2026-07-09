Imports System.Threading

Class Application
    Private Shared _mutex As Mutex

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

    Protected Overrides Sub OnExit(e As ExitEventArgs)
        If _mutex IsNot Nothing Then
            _mutex.ReleaseMutex()
            _mutex.Dispose()
            _mutex = Nothing
        End If
        MyBase.OnExit(e)
    End Sub
End Class
