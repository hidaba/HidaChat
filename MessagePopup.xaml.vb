Imports System.ComponentModel
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

    Public Sub New(accountId As String, title As String, body As String, Optional platform As String = "WhatsApp")
        InitializeComponent()
        _accountId = accountId
        If Not DesignerProperties.GetIsInDesignMode(Me) Then
            ThemeManager.Instance.RegisterWindow(Me)
        End If
        NameText.Text = title
        MessageText.Text = body
        InitialsText.Text = GetInitials(title)
        SetPlatform(platform)
    End Sub

    Private Sub SetPlatform(platform As String)
        Dim def = PlatformRegistry.Get(platform)
        PlatformBadgeBorder.Background = def.ThemeBrush
        PlatformBadgeIcon.Data = Geometry.Parse(def.IconSvgPath)
        AvatarBorder.Background = def.ThemeBrush
        PlatformNameText.Text = $"• {def.DisplayName}"
    End Sub

    ''' <summary>
    ''' Extracts sender initials (e.g. "Mario Rossi" -> "MR") for the circular avatar badge.
    ''' </summary>
    Private Shared Function GetInitials(name As String) As String
        If String.IsNullOrWhiteSpace(name) Then Return "?"
        Dim parts = name.Trim().Split(" "c, StringSplitOptions.RemoveEmptyEntries)
        If parts.Length = 0 Then Return "?"
        If parts.Length = 1 Then Return parts(0).Substring(0, 1).ToUpper()
        Return parts(0).Substring(0, 1).ToUpper() & parts(parts.Length - 1).Substring(0, 1).ToUpper()
    End Function

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
        Me.Left = workArea.Right - Me.Width - 16

        Dim active = GetActivePopups()
        Dim others = active.Where(Function(p) p IsNot Me).ToList()
        If others.Count > 0 Then
            Dim highestTop = others.Min(Function(p) p.Top)
            Dim targetTop = highestTop - Me.Height - 8
            If targetTop < workArea.Top Then
                targetTop = workArea.Top + 16
            End If
            Me.Top = targetTop
        Else
            Me.Top = workArea.Bottom - Me.Height - 16
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
