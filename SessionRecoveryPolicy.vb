Imports System

''' <summary>Pure decisions shared by the runtime and regression tests.</summary>
Friend NotInheritable Class SessionRecoveryPolicy
    Public Enum RecoveryAction
        Observe
        Reload
        Recreate
    End Enum

    Public Shared Function ForFailure(kind As String) As RecoveryAction
        Select Case kind
            Case "BrowserProcessExited"
                Return RecoveryAction.Recreate
            Case "RenderProcessExited"
                Return RecoveryAction.Reload
            Case Else
                ' GPU/utility failures recover natively. Unresponsive is NOT a crash.
                Return RecoveryAction.Observe
        End Select
    End Function

    Public Shared Function IsTrustedStorageOrigin(value As String) As Boolean
        Dim parsed As Uri = Nothing
        If Not Uri.TryCreate(value, UriKind.Absolute, parsed) Then Return False
        Return parsed.Scheme = Uri.UriSchemeHttps AndAlso parsed.Port = 443 AndAlso
            String.IsNullOrEmpty(parsed.UserInfo) AndAlso
            (parsed.Host.Equals("web.whatsapp.com", StringComparison.OrdinalIgnoreCase) OrElse
             parsed.Host.Equals("web.telegram.org", StringComparison.OrdinalIgnoreCase))
    End Function
End Class
