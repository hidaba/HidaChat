Imports System
Imports System.IO
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks

''' <summary>
''' Policy and helper enforcing chunked streaming download of update packages (TASK-0117).
''' Protects against RAM exhaustion by avoiding full-archive buffering, handles slow/throttled connections
''' without premature timeouts, and provides real-time progress reporting.
''' </summary>
Public Class UpdateDownloadPolicy

    Public Const DefaultChunkSize As Integer = 81920 ' 80 KB
    Public Const DefaultProgressThrottleMs As Integer = 80
    Public Shared ReadOnly DefaultDownloadTimeout As TimeSpan = TimeSpan.FromMinutes(10)

    Public Class DownloadProgressReport
        Public Property BytesRead As Long
        Public Property TotalBytes As Long
        Public Property Percentage As Double
        Public Property FormattedDownloadedMb As String
        Public Property FormattedTotalMb As String

        Public ReadOnly Property IsIndeterminate As Boolean
            Get
                Return TotalBytes <= 0
            End Get
        End Property
    End Class

    ''' <summary>
    ''' Creates or configures a dedicated HttpClient for large file downloads with infinite internal timeout (TASK-0117).
    ''' </summary>
    Public Shared Function CreateDedicatedDownloadClient(appVersion As String) As HttpClient
        Dim client As New HttpClient() With {
            .Timeout = Timeout.InfiniteTimeSpan
        }
        client.DefaultRequestHeaders.UserAgent.Add(New Headers.ProductInfoHeaderValue("HidaChat-App", appVersion))
        Return client
    End Function

    ''' <summary>
    ''' Streams an HTTP response directly into a destination file using chunked buffers (TASK-0117).
    ''' </summary>
    Public Shared Async Function DownloadStreamToFileAsync(responseStream As Stream,
                                                          destinationFilePath As String,
                                                          totalBytes As Long,
                                                          progress As IProgress(Of DownloadProgressReport),
                                                          cancellationToken As CancellationToken,
                                                          Optional chunkSize As Integer = DefaultChunkSize,
                                                          Optional throttleMs As Integer = DefaultProgressThrottleMs) As Task(Of Long)
        If responseStream Is Nothing Then Throw New ArgumentNullException(NameOf(responseStream))
        If String.IsNullOrWhiteSpace(destinationFilePath) Then Throw New ArgumentException("Destination file path cannot be empty.", NameOf(destinationFilePath))

        Dim destDir = Path.GetDirectoryName(destinationFilePath)
        If Not String.IsNullOrEmpty(destDir) AndAlso Not Directory.Exists(destDir) Then
            Directory.CreateDirectory(destDir)
        End If

        Dim totalRead As Long = 0
        Dim lastReportTime = DateTime.MinValue
        Dim buffer(chunkSize - 1) As Byte

        Using destStream As New FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, chunkSize, useAsync:=True)
            While True
                cancellationToken.ThrowIfCancellationRequested()
                Dim bytesRead = Await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                If bytesRead = 0 Then Exit While

                Await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                totalRead += bytesRead

                Dim now = DateTime.UtcNow
                Dim shouldReport = (now - lastReportTime).TotalMilliseconds >= throttleMs OrElse (totalBytes > 0 AndAlso totalRead = totalBytes)
                If shouldReport AndAlso progress IsNot Nothing Then
                    lastReportTime = now
                    Dim pct = If(totalBytes > 0, (CDbl(totalRead) / CDbl(totalBytes)) * 100.0, -1.0)
                    Dim mbRead = (totalRead / (1024.0 * 1024.0)).ToString("F1")
                    Dim mbTotal = If(totalBytes > 0, (totalBytes / (1024.0 * 1024.0)).ToString("F1"), "0.0")

                    progress.Report(New DownloadProgressReport With {
                        .BytesRead = totalRead,
                        .TotalBytes = totalBytes,
                        .Percentage = pct,
                        .FormattedDownloadedMb = mbRead,
                        .FormattedTotalMb = mbTotal
                    })
                End If
            End While

            Await destStream.FlushAsync(cancellationToken)
        End Using

        ' Final progress report at 100% completion if not already reported
        If progress IsNot Nothing Then
            Dim finalPct = If(totalBytes > 0, 100.0, -1.0)
            Dim mbRead = (totalRead / (1024.0 * 1024.0)).ToString("F1")
            Dim mbTotal = If(totalBytes > 0, (totalBytes / (1024.0 * 1024.0)).ToString("F1"), mbRead)
            progress.Report(New DownloadProgressReport With {
                .BytesRead = totalRead,
                .TotalBytes = If(totalBytes > 0, totalBytes, totalRead),
                .Percentage = finalPct,
                .FormattedDownloadedMb = mbRead,
                .FormattedTotalMb = mbTotal
            })
        End If

        Return totalRead
    End Function

    ''' <summary>
    ''' Downloads an update package from an HTTP URL directly to disk via streaming, reporting progress (TASK-0117).
    ''' </summary>
    Public Shared Async Function DownloadFileStreamingAsync(downloadUrl As String,
                                                           destinationFilePath As String,
                                                           client As HttpClient,
                                                           progress As IProgress(Of DownloadProgressReport),
                                                           cancellationToken As CancellationToken,
                                                           Optional chunkSize As Integer = DefaultChunkSize) As Task(Of Long)
        If client Is Nothing Then Throw New ArgumentNullException(NameOf(client))
        If String.IsNullOrWhiteSpace(downloadUrl) Then Throw New ArgumentException("Download URL cannot be empty.", NameOf(downloadUrl))

        Using response = Await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            response.EnsureSuccessStatusCode()

            Dim totalBytes = response.Content.Headers.ContentLength.GetValueOrDefault(-1L)
            Using responseStream = Await response.Content.ReadAsStreamAsync(cancellationToken)
                Return Await DownloadStreamToFileAsync(responseStream, destinationFilePath, totalBytes, progress, cancellationToken, chunkSize)
            End Using
        End Using
    End Function

End Class
