Imports System.Windows.Threading

Public Class MessagePopup
    Private ReadOnly _accountId As String
    Private _closeTimer As DispatcherTimer
    Private Shared _activePopups As New List(Of MessagePopup)

    Public Sub New(accountId As String, title As String, body As String)
        InitializeComponent()
        _accountId = accountId
        NameText.Text = title
        MessageText.Text = body
        InitialsText.Text = GetInitials(title)
    End Sub

    Private Shared Function GetInitials(name As String) As String
        If String.IsNullOrWhiteSpace(name) Then Return "?"
        Dim parts = name.Trim().Split(" "c, StringSplitOptions.RemoveEmptyEntries)
        If parts.Length = 0 Then Return "?"
        If parts.Length = 1 Then Return parts(0).Substring(0, 1).ToUpper()
        Return parts(0).Substring(0, 1).ToUpper() & parts(parts.Length - 1).Substring(0, 1).ToUpper()
    End Function

    Private Sub MessagePopup_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        _activePopups.Add(Me)
        RepositionAll()

        _closeTimer = New DispatcherTimer()
        _closeTimer.Interval = TimeSpan.FromSeconds(5)
        AddHandler _closeTimer.Tick, Sub()
            _closeTimer.Stop()
            ClosePopup()
        End Sub
        _closeTimer.Start()
    End Sub

    Private Shared Sub RepositionAll()
        _activePopups.RemoveAll(Function(p) p Is Nothing OrElse Not p.IsLoaded)
        Dim workArea = SystemParameters.WorkArea
        Dim y = workArea.Bottom - 16
        For i As Integer = _activePopups.Count - 1 To 0 Step -1
            Dim popup = _activePopups(i)
            popup.Left = workArea.Right - popup.Width - 16
            popup.Top = y - popup.Height
            y -= popup.Height + 8
        Next
    End Sub

    Private Sub PopupGrid_MouseDown(sender As Object, e As MouseButtonEventArgs)
        _closeTimer?.Stop()

        Dim mainWin = TryCast(Application.Current.MainWindow, MainWindow)
        If mainWin IsNot Nothing Then
            If mainWin.Visibility <> Visibility.Visible Then
                mainWin.Show()
            End If
            mainWin.WindowState = WindowState.Normal
            mainWin.Activate()
            mainWin.Focus()
        End If

        ClosePopup()
    End Sub

    Private Sub CloseBtn_Click(sender As Object, e As RoutedEventArgs)
        _closeTimer?.Stop()
        ClosePopup()
    End Sub

    Private Sub ClosePopup()
        _activePopups.Remove(Me)
        RepositionAll()
        Me.Close()
    End Sub

    Protected Overrides Sub OnClosed(e As EventArgs)
        _activePopups.Remove(Me)
        _closeTimer?.Stop()
        MyBase.OnClosed(e)
    End Sub
End Class