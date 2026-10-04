Imports System.IO

''' <summary>
''' Gestore centralizzato e thread-safe dei log diagnostici dell'applicazione (TODO #63 / #73).
''' Scrive esclusivamente all'interno della cartella portabile 'data/logs' rispettando la portabilità al 100%.
''' </summary>
Public Class AppLogger
    Private Shared ReadOnly _logLock As New Object()

    ''' <summary>
    ''' Cartella di destinazione per i file di log portabili (data/logs).
    ''' </summary>
    Public Shared ReadOnly Property LogsDirectory As String
        Get
            Dim dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "logs")
            If Not Directory.Exists(dir) Then
                Try
                    Directory.CreateDirectory(dir)
                Catch
                End Try
            End If
            Return dir
        End Get
    End Property

    ''' <summary>
    ''' Registra una voce di log formattata con timestamp su un file specificato in 'data/logs'.
    ''' Esegue la rotazione automatica del file se supera i 5 MB.
    ''' </summary>
    Public Shared Sub Log(fileName As String, category As String, message As String)
        Try
            Dim logPath = Path.Combine(LogsDirectory, fileName)
            Dim line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}"
            SyncLock _logLock
                Try
                    Dim fi As New FileInfo(logPath)
                    If fi.Exists AndAlso fi.Length > 5 * 1024 * 1024 Then
                        Dim oldPath = logPath & ".old"
                        If File.Exists(oldPath) Then File.Delete(oldPath)
                        File.Move(logPath, oldPath)
                    End If
                    File.AppendAllText(logPath, line & Environment.NewLine)
                Catch
                End Try
            End SyncLock
            System.Diagnostics.Debug.WriteLine(line)
        Catch
        End Try
    End Sub

    ''' <summary>Registra un evento diagnostico specifico per WhatsApp Web.</summary>
    Public Shared Sub LogWhatsApp(message As String)
        Log("whatsapp.log", "WhatsApp", message)
    End Sub

    ''' <summary>Registra un evento relativo alla sincronizzazione profili di rete e staging.</summary>
    Public Shared Sub LogSync(message As String)
        Log("sync.log", "Sync", message)
    End Sub

    ''' <summary>Registra un evento generale dell'applicazione o del ciclo di vita WebView2.</summary>
    Public Shared Sub LogApp(message As String)
        Log("app.log", "App", message)
    End Sub
End Class
