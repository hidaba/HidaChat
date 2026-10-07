Imports System
Imports System.Threading.Tasks
Imports Microsoft.Web.WebView2.Core

''' <summary>
''' Tracks the entire browser process group, not just the WPF control.
''' Construct and dispose on the WebView2 UI thread. Never kill the browser to flush it.
''' </summary>
Friend NotInheritable Class WebViewLifetime
    Implements IDisposable

    Private ReadOnly _environment As CoreWebView2Environment
    Private ReadOnly _processId As UInteger
    Private ReadOnly _accountId As String
    Private ReadOnly _exited As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)

    Public Sub New(core As CoreWebView2, accountId As String)
        _environment = core.Environment
        _processId = core.BrowserProcessId
        _accountId = accountId
        AddHandler _environment.BrowserProcessExited, AddressOf OnBrowserProcessExited
    End Sub

    Public ReadOnly Property HasExited As Boolean
        Get
            Return _exited.Task.IsCompletedSuccessfully
        End Get
    End Property

    Private Sub OnBrowserProcessExited(sender As Object, e As CoreWebView2BrowserProcessExitedEventArgs)
        If e.BrowserProcessId <> _processId Then Return
        AppLogger.LogApp($"[BROWSER_EXIT] account={_accountId}, pid={_processId}, kind={e.BrowserProcessExitKind}")
        _exited.TrySetResult(True)
    End Sub

    Public Async Function WaitForExitAsync(Optional timeoutMs As Integer = 10000) As Task(Of Boolean)
        If HasExited Then Return True
        Dim completed = Await Task.WhenAny(_exited.Task, Task.Delay(timeoutMs))
        If completed Is _exited.Task Then Return True
        AppLogger.LogApp($"[BROWSER_EXIT_TIMEOUT] account={_accountId}, pid={_processId}. Profile must not be copied or cleaned.")
        Return False
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        RemoveHandler _environment.BrowserProcessExited, AddressOf OnBrowserProcessExited
    End Sub
End Class
