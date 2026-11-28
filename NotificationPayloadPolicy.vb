Option Strict On
Option Explicit On

Imports System.Globalization
Imports System.Text.RegularExpressions

''' <summary>
''' Normalized metadata payload ready for display in desktop toast and in-app MessagePopup notifications.
''' </summary>
Public NotInheritable Class NotificationPayload
    Public ReadOnly Property Title As String
    Public ReadOnly Property Body As String
    Public ReadOnly Property Platform As String
    Public ReadOnly Property Initials As String
    Public ReadOnly Property ThumbnailUrl As String
    Public ReadOnly Property EmojiPreview As String

    Public Sub New(title As String, body As String, platform As String, initials As String)
        Me.New(title, body, platform, initials, String.Empty, String.Empty)
    End Sub

    Public Sub New(title As String, body As String, platform As String, initials As String, thumbnailUrl As String, emojiPreview As String)
        Me.Title = title
        Me.Body = body
        Me.Platform = platform
        Me.Initials = initials
        Me.ThumbnailUrl = If(thumbnailUrl, String.Empty)
        Me.EmojiPreview = If(emojiPreview, String.Empty)
    End Sub
End Class

''' <summary>
''' Pure normalization and fallback policy for incoming chat notifications across all supported platforms.
''' </summary>
Public Module NotificationPayloadPolicy

    Private ReadOnly MultiWhitespaceRegex As New Regex("\s+", RegexOptions.Compiled)
    Private ReadOnly ValidDataImageRegex As New Regex("^data:image/(?:png|jpeg|jpg|webp|gif);base64,[A-Za-z0-9+/=]+$", RegexOptions.Compiled Or RegexOptions.IgnoreCase)

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
    ''' Extracts the first Unicode emoji grapheme cluster from text for miniature display, or String.Empty if none exists.
    ''' </summary>
    Public Function ExtractFirstEmoji(text As String) As String
        If String.IsNullOrEmpty(text) Then Return String.Empty
        Dim enumerator = StringInfo.GetTextElementEnumerator(text)
        While enumerator.MoveNext()
            Dim cluster = enumerator.GetTextElement()
            If IsEmojiCluster(cluster) Then
                Return cluster
            End If
        End While
        Return String.Empty
    End Function

    Private Function IsEmojiCluster(cluster As String) As Boolean
        If String.IsNullOrEmpty(cluster) Then Return False
        ' Variation selector-16 (U+FE0F), ZWJ (U+200D), or keycap combiner (U+20E3) explicitly mark emoji presentation
        If cluster.IndexOf(ChrW(&HFE0F)) >= 0 OrElse cluster.IndexOf(ChrW(&H200D)) >= 0 OrElse cluster.IndexOf(ChrW(&H20E3)) >= 0 Then
            Return True
        End If

        Dim idx As Integer = 0
        While idx < cluster.Length
            Dim cp = Char.ConvertToUtf32(cluster, idx)
            idx += If(Char.IsSurrogatePair(cluster, idx), 2, 1)

            If (cp >= &H1F000 AndAlso cp <= &H1FAFF) OrElse
               (cp >= &H2600 AndAlso cp <= &H27BF) OrElse
               (cp >= &H231A AndAlso cp <= &H23FA) OrElse
               (cp >= &H2B05 AndAlso cp <= &H2B55) OrElse
               cp = &H3030 OrElse cp = &H303D OrElse cp = &H3297 OrElse cp = &H3299 Then
                Return True
            End If
        End While

        Return False
    End Function

    ''' <summary>
    ''' Validates and sanitizes thumbnail URLs from web notification bridges, allowing only safe image data URIs and HTTPS/loopback URLs.
    ''' </summary>
    Public Function SanitizeThumbnailUrl(rawUrl As String) As String
        If String.IsNullOrWhiteSpace(rawUrl) Then Return String.Empty
        Dim trimmed = rawUrl.Trim()

        If trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) Then
            If trimmed.Length > 350000 Then Return String.Empty
            If trimmed.StartsWith("data:image/gif;base64,R0lGODlhAQAB", StringComparison.OrdinalIgnoreCase) Then Return String.Empty
            If ValidDataImageRegex.IsMatch(trimmed) Then Return trimmed
            Return String.Empty
        End If

        Dim parsed As Uri = Nothing
        If Uri.TryCreate(trimmed, UriKind.Absolute, parsed) Then
            If String.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) Then
                Return parsed.AbsoluteUri
            End If
            If String.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) AndAlso parsed.IsLoopback Then
                Return parsed.AbsoluteUri
            End If
        End If

        Return String.Empty
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
    ''' Resolves final title, body, platform, avatar initials, and media/emoji miniature metadata for MessagePopup.
    ''' </summary>
    Public Function ResolvePopupPayload(
        rawTitle As String,
        rawBody As String,
        accountName As String,
        platform As String,
        Optional unreadCount As Integer = 1,
        Optional newMessageFallback As String = "New message received",
        Optional unreadCountFallbackTemplate As String = "{count} unread messages",
        Optional rawThumbnailUrl As String = Nothing,
        Optional imageMessageFallback As String = "Sent an image"
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

        Dim cleanThumb = SanitizeThumbnailUrl(rawThumbnailUrl)

        Dim cleanBody = NormalizePreviewText(rawBody, 240)
        If String.IsNullOrEmpty(cleanBody) Then
            If Not String.IsNullOrEmpty(cleanThumb) Then
                cleanBody = If(String.IsNullOrWhiteSpace(imageMessageFallback), "Sent an image", imageMessageFallback)
            ElseIf unreadCount > 1 AndAlso Not String.IsNullOrWhiteSpace(unreadCountFallbackTemplate) Then
                cleanBody = unreadCountFallbackTemplate.Replace("{count}", unreadCount.ToString())
            Else
                cleanBody = If(String.IsNullOrWhiteSpace(newMessageFallback), "New message received", newMessageFallback)
            End If
        End If

        Dim emojiPreview = ExtractFirstEmoji(cleanBody)

        Return New NotificationPayload(
            title:=cleanTitle,
            body:=cleanBody,
            platform:=resolvedPlatform,
            initials:=ExtractInitials(cleanTitle),
            thumbnailUrl:=cleanThumb,
            emojiPreview:=emojiPreview
        )
    End Function

End Module
