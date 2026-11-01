Imports System
Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.IO
Imports System.Text
Imports System.Threading

''' <summary>
''' Gestore centralizzato, asincrono e thread-safe dei log diagnostici dell'applicazione (TODO #63 / #73 / #108).
''' Scrive esclusivamente all'interno della cartella portabile 'data/logs' rispettando la portabilità al 100%.
''' Utilizza una coda in memoria non bloccante e scritture accorpate (batch flush) per eliminare le latenze
''' di I/O sincrone su dischi o condivisioni di rete Samba/SMB.
''' </summary>
Public Class AppLogger

    Private Structure LogEntry
        Public Property FileName As String
        Public Property Line As String
    End Structure

    Private Shared ReadOnly _logQueue As New ConcurrentQueue(Of LogEntry)()
    Private Shared ReadOnly _writeLock As New Object()
    Private Shared ReadOnly _flushTimer As Timer
    Private Shared _isProcessExiting As Boolean = False

    Shared Sub New()
        ' Timer periodico per il flush automatico della coda ogni 250ms
        _flushTimer = New Timer(AddressOf OnTimerFlush, Nothing, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250))
        Try
            AddHandler AppDomain.CurrentDomain.ProcessExit, AddressOf OnProcessExit
        Catch
        End Try
    End Sub

    Private Shared Sub OnProcessExit(sender As Object, e As EventArgs)
        _isProcessExiting = True
        FlushNow()
    End Sub

    Private Shared Sub OnTimerFlush(state As Object)
        If _logQueue.IsEmpty OrElse _isProcessExiting Then Return
        FlushNow()
    End Sub

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
    ''' Inserisce una voce di log formattata nella coda in memoria in modo non bloccante (0 ms).
    ''' </summary>
    Public Shared Sub Log(fileName As String, category As String, message As String)
        Try
            Dim line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}"
            _logQueue.Enqueue(New LogEntry With {.FileName = fileName, .Line = line})
            System.Diagnostics.Debug.WriteLine(line)

            ' Se si accumulano più di 50 messaggi, esegue il flush immediato in background
            If _logQueue.Count >= 50 Then
                ThreadPool.QueueUserWorkItem(Sub() FlushNow())
            End If
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' Svuota immediatamente la coda dei log raggruppando le scritture per file in batch singoli,
    ''' riducendo drasticamente il numero di accessi e metadati via rete Samba/SMB.
    ''' </summary>
    Public Shared Sub FlushNow()
        If _logQueue.IsEmpty Then Return

        SyncLock _writeLock
            Dim batches As New Dictionary(Of String, StringBuilder)(StringComparer.OrdinalIgnoreCase)
            Dim entry As LogEntry = Nothing

            While _logQueue.TryDequeue(entry)
                If String.IsNullOrWhiteSpace(entry.FileName) Then Continue While
                Dim sb As StringBuilder = Nothing
                If Not batches.TryGetValue(entry.FileName, sb) Then
                    sb = New StringBuilder()
                    batches(entry.FileName) = sb
                End If
                sb.AppendLine(entry.Line)
            End While

            If batches.Count = 0 Then Return

            Dim logsDir = LogsDirectory
            For Each kvp In batches
                Try
                    Dim logPath = Path.Combine(logsDir, kvp.Key)
                    Dim fi As New FileInfo(logPath)
                    If fi.Exists AndAlso fi.Length > 5 * 1024 * 1024 Then
                        Dim oldPath = logPath & ".old"
                        If File.Exists(oldPath) Then File.Delete(oldPath)
                        File.Move(logPath, oldPath)
                    End If
                    File.AppendAllText(logPath, kvp.Value.ToString())
                Catch
                End Try
            Next
        End SyncLock
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

    ''' <summary>Registra un messaggio informativo generale in app.log.</summary>
    Public Shared Sub LogInfo(message As String)
        Log("app.log", "INFO", message)
    End Sub

    ''' <summary>Registra un avviso di sicurezza o anomalia in app.log.</summary>
    Public Shared Sub LogWarn(message As String)
        Log("app.log", "WARN", message)
    End Sub
End Class
