Option Strict On
Option Explicit On

Imports System.Text.RegularExpressions

''' <summary>
''' Normalized metadata payload ready for display in desktop toast and in-app MessagePopup notifications.
''' </summary>
Public NotInheritable Class NotificationPayload
    Public ReadOnly Property Title As String
    Public ReadOnly Property Body As String
    Public ReadOnly Property Platform As String
    Public ReadOnly Property Initials As String

    Public Sub New(title As String, body As String, platform As String, initials As String)
        Me.Title = title
        Me.Body = body
        Me.Platform = platform
        Me.Initials = initials
    End Sub
End Class

''' <summary>
''' Pure normalization and fallback policy for incoming chat notifications across all supported platforms.
''' </summary>
Public Module NotificationPayloadPolicy

    Private ReadOnly MultiWhitespaceRegex As New Regex("\s+", RegexOptions.Compiled)

    ''' <summary>
    ''' Collapses newlines, tabs, and repeated whitespace into single spaces and trims the text.
    ''' </summary>
    Public Function NormalizePreviewText(text As String, Optional maxLength As Integer = 240) As String
        If String.IsNullOrWhiteSpace(text) Then Return String.Empty
        Dim cleaned = MultiWhitespaceRegex.Replace(text.Trim(), " ")
        If maxLength > 3 AndAlso cleaned.Length > maxLength Then
            Return cleaned.Substring(0, maxLength - 1).TrimEnd() & "…"
        End If
        Return cleaned
    End Function

    ''' <summary>
    ''' Extracts up to two uppercase alphanumeric initials from a sender or channel display name.
    ''' </summary>
    Public Function ExtractInitials(name As String) As String
        If String.IsNullOrWhiteSpace(name) Then Return "?"
        Dim cleaned = Regex.Replace(name.Trim(), "^[#@•\-\s]+", String.Empty)
        If String.IsNullOrWhiteSpace(cleaned) Then Return "?"

        Dim parts = cleaned.Split(New Char() {" "c, vbTab(0)}, StringSplitOptions.RemoveEmptyEntries)
        If parts.Length = 0 Then Return "?"
        If parts.Length = 1 Then
            Return FirstLetterOrDigit(parts(0))
        End If

        Dim first = FirstLetterOrDigit(parts(0))
        Dim last = FirstLetterOrDigit(parts(parts.Length - 1))
        If first = "?" AndAlso last = "?" Then Return "?"
        If last = "?" Then Return first
        If first = "?" Then Return last
        Return first & last
    End Function

    Private Function FirstLetterOrDigit(token As String) As String
        If String.IsNullOrEmpty(token) Then Return "?"
        For Each ch In token
            If Char.IsLetterOrDigit(ch) Then
                Return Char.ToUpperInvariant(ch).ToString()
            End If
        Next
        Return token.Substring(0, 1).ToUpperInvariant()
    End Function

    ''' <summary>
    ''' Resolves final title, body, platform, and avatar initials for MessagePopup with localized fallbacks.
    ''' </summary>
    Public Function ResolvePopupPayload(
        rawTitle As String,
        rawBody As String,
        accountName As String,
        platform As String,
        Optional unreadCount As Integer = 1,
        Optional newMessageFallback As String = "New message received",
        Optional unreadCountFallbackTemplate As String = "{count} unread messages"
    ) As NotificationPayload
        Dim def = PlatformRegistry.Get(platform)
        Dim resolvedPlatform = def.Id

        Dim cleanTitle = NormalizePreviewText(rawTitle, 120)
        If String.IsNullOrEmpty(cleanTitle) Then
            cleanTitle = NormalizePreviewText(accountName, 120)
        End If
        If String.IsNullOrEmpty(cleanTitle) Then
            cleanTitle = def.DisplayName
        End If

        Dim cleanBody = NormalizePreviewText(rawBody, 240)
        If String.IsNullOrEmpty(cleanBody) Then
            If unreadCount > 1 AndAlso Not String.IsNullOrWhiteSpace(unreadCountFallbackTemplate) Then
                cleanBody = unreadCountFallbackTemplate.Replace("{count}", unreadCount.ToString())
            Else
                cleanBody = If(String.IsNullOrWhiteSpace(newMessageFallback), "New message received", newMessageFallback)
            End If
        End If

        Return New NotificationPayload(
            title:=cleanTitle,
            body:=cleanBody,
            platform:=resolvedPlatform,
            initials:=ExtractInitials(cleanTitle)
        )
    End Function

End Module
