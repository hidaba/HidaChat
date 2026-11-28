Imports System.ComponentModel
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Windows.Interop
Imports System.Windows.Threading
Imports HidaChat.UI.Theme

''' <summary>
''' Fluent Design desktop toast notification popup appearing in the bottom-right corner
''' of the active work area when a new message arrives.
''' </summary>
Public Class MessagePopup
    Private ReadOnly _accountId As String
    Private _closeTimer As DispatcherTimer
    Private Shared ReadOnly _activePopups As New List(Of WeakReference(Of MessagePopup))()

    Private Const HWND_TOPMOST As Integer = -1
    Private Const SWP_NOSIZE As UInteger = &H1
    Private Const SWP_NOMOVE As UInteger = &H2
    Private Const SWP_NOACTIVATE As UInteger = &H10
    Private Const SWP_SHOWWINDOW As UInteger = &H40

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function SetWindowPos(hWnd As IntPtr, hWndInsertAfter As IntPtr, X As Integer, Y As Integer, cx As Integer, cy As Integer, uFlags As UInteger) As Boolean
    End Function

    ''' <summary>
    ''' Parameterless constructor preserved for Visual Studio XAML Designer compatibility.
    ''' </summary>
    Public Sub New()
        InitializeComponent()
    End Sub

    Public Sub New(accountId As String, title As String, body As String, Optional platform As String = "WhatsApp", Optional thumbnailUrl As String = Nothing)
        InitializeComponent()
        _accountId = accountId
        If Not DesignerProperties.GetIsInDesignMode(Me) Then
            ThemeManager.Instance.RegisterWindow(Me)
        End If
        Dim payload = NotificationPayloadPolicy.ResolvePopupPayload(
            title,
            body,
            String.Empty,
            platform,
            1,
            AppLocalizations.GetText("popup_new_message", "New message received"),
            AppLocalizations.GetText("popup_unread_messages", "{count} unread messages"),
            thumbnailUrl,
            AppLocalizations.GetText("popup_image_message", "Sent an image"))
        NameText.Text = payload.Title
        NameText.ToolTip = payload.Title
        MessageText.Text = payload.Body
        MessageText.ToolTip = payload.Body
        SetPlatform(payload.Platform)
        SetMediaPreview(payload.ThumbnailUrl, payload.EmojiPreview)
    End Sub

    Private Sub SetPlatform(platform As String)
        Dim def = PlatformRegistry.Get(platform)
        AccentIndicatorBar.Background = def.ThemeBrush
        PlatformBadgeBorder.Background = def.ThemeBrush
        PlatformBadgeBorder.ToolTip = def.DisplayName
        PlatformBadgeBorder.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, def.DisplayName)
        PlatformBadgeIcon.Data = Geometry.Parse(def.IconSvgPath)
    End Sub

    Private Sub SetMediaPreview(thumbnailUrl As String, emojiPreview As String)
        MediaThumbnailImage.Source = Nothing
        MediaThumbnailImage.Visibility = Visibility.Collapsed
        EmojiThumbnailText.Text = String.Empty
        EmojiThumbnailText.Visibility = Visibility.Collapsed
        MediaPreviewBorder.Visibility = Visibility.Collapsed

        If Not String.IsNullOrEmpty(thumbnailUrl) AndAlso TryShowImageThumbnail(thumbnailUrl, emojiPreview) Then
            Return
        End If

        ShowEmojiThumbnail(emojiPreview)
    End Sub

    Private Function TryShowImageThumbnail(thumbnailUrl As String, fallbackEmoji As String) As Boolean
        Try
            If thumbnailUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) Then
                Dim commaIdx = thumbnailUrl.IndexOf(","c)
                If commaIdx > 0 AndAlso commaIdx + 1 < thumbnailUrl.Length Then
                    Dim bytes = Convert.FromBase64String(thumbnailUrl.Substring(commaIdx + 1))
                    Dim bmp As New BitmapImage()
                    Using ms As New MemoryStream(bytes)
                        bmp.BeginInit()
                        bmp.CacheOption = BitmapCacheOption.OnLoad
                        bmp.DecodePixelWidth = 84
                        bmp.StreamSource = ms
                        bmp.EndInit()
                    End Using
                    If bmp.CanFreeze Then bmp.Freeze()
                    MediaThumbnailImage.Source = bmp
                    MediaThumbnailImage.Visibility = Visibility.Visible
                    MediaPreviewBorder.Visibility = Visibility.Visible
                    Return True
                End If
            Else
                Dim uriObj As New Uri(thumbnailUrl, UriKind.Absolute)
                Dim bmp As New BitmapImage()
                AddHandler bmp.DownloadFailed, Sub()
                    MediaThumbnailImage.Source = Nothing
                    MediaThumbnailImage.Visibility = Visibility.Collapsed
                    ShowEmojiThumbnail(fallbackEmoji)
                End Sub
                bmp.BeginInit()
                bmp.CacheOption = BitmapCacheOption.OnLoad
                bmp.DecodePixelWidth = 84
                bmp.UriSource = uriObj
                bmp.EndInit()
                MediaThumbnailImage.Source = bmp
                MediaThumbnailImage.Visibility = Visibility.Visible
                MediaPreviewBorder.Visibility = Visibility.Visible
                Return True
            End If
        Catch ex As Exception
            Debug.WriteLine($"Failed to load popup miniature image: {ex.Message}")
        End Try
        Return False
    End Function

    Private Sub ShowEmojiThumbnail(emojiPreview As String)
        If String.IsNullOrEmpty(emojiPreview) Then
            MediaPreviewBorder.Visibility = Visibility.Collapsed
            Return
        End If
        EmojiThumbnailText.Text = emojiPreview
        EmojiThumbnailText.Visibility = Visibility.Visible
        MediaPreviewBorder.Visibility = Visibility.Visible
    End Sub

    ''' <summary>
    ''' Returns all currently loaded popups while pruning dead WeakReferences.
    ''' </summary>
    Private Shared Function GetActivePopups() As List(Of MessagePopup)
        Dim active As New List(Of MessagePopup)()
        For i As Integer = _activePopups.Count - 1 To 0 Step -1
            Dim target As MessagePopup = Nothing
            If _activePopups(i).TryGetTarget(target) AndAlso target IsNot Nothing AndAlso target.IsLoaded Then
                active.Add(target)
            Else
                _activePopups.RemoveAt(i)
            End If
        Next
        active.Reverse()
        Return active
    End Function

    Private Sub MessagePopup_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If DesignerProperties.GetIsInDesignMode(Me) Then Return

        _activePopups.Add(New WeakReference(Of MessagePopup)(Me))
        PositionNewPopup()

        Try
            Dim handle = New WindowInteropHelper(Me).Handle
            SetWindowPos(handle, New IntPtr(HWND_TOPMOST), 0, 0, 0, 0, SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOACTIVATE Or SWP_SHOWWINDOW)
        Catch ex As Exception
            Debug.WriteLine($"Failed to set topmost on popup: {ex.Message}")
        End Try

        _closeTimer = New DispatcherTimer()
        _closeTimer.Interval = TimeSpan.FromSeconds(5)
        AddHandler _closeTimer.Tick, Sub()
            _closeTimer.Stop()
            ClosePopup()
        End Sub
        _closeTimer.Start()
    End Sub

    Private Sub MessagePopup_MouseEnter(sender As Object, e As MouseEventArgs) Handles Me.MouseEnter
        _closeTimer?.Stop()
    End Sub

    Private Sub MessagePopup_MouseLeave(sender As Object, e As MouseEventArgs) Handles Me.MouseLeave
        _closeTimer?.Start()
    End Sub

    ''' <summary>
    ''' Stacks the new popup in the bottom-right corner of the primary work area.
    ''' </summary>
    Private Sub PositionNewPopup()
        Dim workArea = SystemParameters.WorkArea
        Dim popupHeight = If(Me.ActualHeight > 0, Me.ActualHeight, If(Not Double.IsNaN(Me.Height) AndAlso Me.Height > 0, Me.Height, 92.0))
        Me.Left = workArea.Right - Me.Width - 16

        Dim active = GetActivePopups()
        Dim others = active.Where(Function(p) p IsNot Me).ToList()
        If others.Count > 0 Then
            Dim highestTop = others.Min(Function(p) p.Top)
            Dim targetTop = highestTop - popupHeight - 8
            If targetTop < workArea.Top Then
                targetTop = workArea.Top + 16
            End If
            Me.Top = targetTop
        Else
            Me.Top = workArea.Bottom - popupHeight - 16
        End If
    End Sub

    ''' <summary>
    ''' Restores MainWindow and activates the account associated with this notification on click.
    ''' </summary>
    Private Async Sub PopupGrid_MouseDown(sender As Object, e As MouseButtonEventArgs)
        _closeTimer?.Stop()

        Dim mainWin = TryCast(Application.Current.MainWindow, MainWindow)
        If mainWin IsNot Nothing Then
            mainWin.ShowWindow()
            If Not String.IsNullOrEmpty(_accountId) Then
                Await mainWin.SwitchToAccountAsync(_accountId)
            End If
        End If

        ClosePopup()
    End Sub

    Private Sub CloseBtn_Click(sender As Object, e As RoutedEventArgs)
        _closeTimer?.Stop()
        ClosePopup()
    End Sub

    Private Sub RemoveFromActive()
        For i As Integer = _activePopups.Count - 1 To 0 Step -1
            Dim target As MessagePopup = Nothing
            If Not _activePopups(i).TryGetTarget(target) OrElse target Is Nothing OrElse target Is Me Then
                _activePopups.RemoveAt(i)
            End If
        Next
    End Sub

    Private Sub ClosePopup()
        RemoveFromActive()
        Me.Close()
    End Sub

    Protected Overrides Sub OnClosed(e As EventArgs)
        ThemeManager.Instance.UnregisterWindow(Me)
        RemoveFromActive()
        _closeTimer?.Stop()
        MyBase.OnClosed(e)
    End Sub
End Class
