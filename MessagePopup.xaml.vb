Imports System.Windows.Threading

''' <summary>
''' Finestra di popup personalizzata (notifica Toast in stile overlay) che compare in basso a destra 
''' allo schermo al ricevimento di un nuovo messaggio WhatsApp.
''' </summary>
Public Class MessagePopup
    Private ReadOnly _accountId As String
    Private _closeTimer As DispatcherTimer
    Private Shared ReadOnly _activePopups As New List(Of WeakReference(Of MessagePopup))()

    Public Sub New(accountId As String, title As String, body As String, Optional platform As String = "WhatsApp")
        InitializeComponent()
        _accountId = accountId
        NameText.Text = title
        MessageText.Text = body
        InitialsText.Text = GetInitials(title)
        SetPlatform(platform)
    End Sub

    Private Sub SetPlatform(platform As String)
        Dim isTelegram = String.Equals(platform, "Telegram", StringComparison.OrdinalIgnoreCase)
        If isTelegram Then
            PlatformBadgeBorder.Background = BrushCache.GetBrush("#24A1DE")
            PlatformBadgeIcon.Data = Geometry.Parse("M9.78 18.65L10.06 14.42L17.74 7.5C18.08 7.19 17.67 7.04 17.22 7.31L7.74 13.3L3.64 12C2.76 11.75 2.75 11.14 3.84 10.7L19.81 4.54C20.54 4.21 21.24 4.72 20.97 5.84L18.25 18.67C18.05 19.6 17.5 19.82 16.73 19.38L12.58 16.32L10.58 18.25C10.36 18.47 10.17 18.65 9.78 18.65Z")
            AvatarBorder.Background = BrushCache.GetBrush("#24A1DE")
            PlatformNameText.Text = "• Telegram"
        Else
            PlatformBadgeBorder.Background = BrushCache.GetBrush("#25d366")
            PlatformBadgeIcon.Data = Geometry.Parse("M12.04 2C6.58 2 2.13 6.45 2.13 11.91C2.13 13.66 2.59 15.36 3.45 16.86L2.05 22L7.3 20.62C8.75 21.41 10.38 21.83 12.04 21.83C17.5 21.83 21.95 17.38 21.95 11.92C21.95 9.27 20.92 6.78 19.05 4.91C17.18 3.03 14.69 2 12.04 2M12.05 3.67C14.25 3.67 16.31 4.53 17.87 6.09C19.42 7.65 20.28 9.72 20.28 11.92C20.28 16.46 16.58 20.15 12.04 20.15C10.56 20.15 9.11 19.76 7.85 19L7.55 18.83L4.43 19.65L5.26 16.61L5.06 16.29C4.24 15 3.8 13.47 3.8 11.91C3.81 7.37 7.5 3.67 12.05 3.67M8.53 7.33C8.37 7.33 8.1 7.39 7.87 7.64C7.65 7.89 7.02 8.48 7.02 9.68C7.02 10.88 7.9 12.03 8.02 12.19C8.14 12.35 9.73 14.81 12.18 15.86C12.76 16.11 13.22 16.26 13.57 16.37C14.16 16.56 14.69 16.53 15.11 16.47C15.59 16.4 16.58 15.87 16.78 15.3C16.98 14.73 16.98 14.24 16.92 14.14C16.86 14.04 16.7 13.98 16.45 13.85C16.2 13.73 14.97 13.12 14.74 13.04C14.52 12.96 14.35 12.92 14.19 13.17C14.03 13.41 13.56 13.98 13.42 14.14C13.28 14.31 13.13 14.33 12.89 14.21C12.64 14.08 11.84 13.82 10.89 12.97C10.15 12.31 9.65 11.5 9.51 11.25C9.36 11.01 9.5 10.87 9.62 10.75C9.73 10.64 9.87 10.45 10 10.31C10.13 10.16 10.17 10.06 10.25 9.9C10.33 9.73 10.29 9.59 10.23 9.47C10.17 9.35 9.7 8.19 9.5 7.72C9.31 7.26 9.12 7.32 8.97 7.31C8.84 7.31 8.68 7.33 8.53 7.33Z")
            AvatarBorder.Background = BrushCache.GetBrush("#00a884")
            PlatformNameText.Text = "• WhatsApp"
        End If
    End Sub

    ''' <summary>
    ''' Estrae le iniziali del mittente (es. "Mario Rossi" -> "MR") per l'avatar circolare della notifica.
    ''' </summary>
    Private Shared Function GetInitials(name As String) As String
        If String.IsNullOrWhiteSpace(name) Then Return "?"
        Dim parts = name.Trim().Split(" "c, StringSplitOptions.RemoveEmptyEntries)
        If parts.Length = 0 Then Return "?"
        If parts.Length = 1 Then Return parts(0).Substring(0, 1).ToUpper()
        Return parts(0).Substring(0, 1).ToUpper() & parts(parts.Length - 1).Substring(0, 1).ToUpper()
    End Function

    ''' <summary>
    ''' Restituisce tutti i popup attualmente attivi e caricati, ripulendo automaticamente i riferimenti deboli non più validi.
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
        _activePopups.Add(New WeakReference(Of MessagePopup)(Me))
        PositionNewPopup()

        _closeTimer = New DispatcherTimer()
        _closeTimer.Interval = TimeSpan.FromSeconds(5)
        AddHandler _closeTimer.Tick, Sub()
            _closeTimer.Stop()
            ClosePopup()
        End Sub
        _closeTimer.Start()
    End Sub

    ''' <summary>
    ''' Calcola in modo incrementale la posizione dello schermo per il nuovo popup (in basso a destra, impilato verso l'alto).
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
    ''' Ricalcola e riposiziona verticalmente tutti i popup attivi sulla schermata incolonnandoli in basso a destra.
    ''' </summary>
    Private Shared Sub RepositionAll()
        Dim active = GetActivePopups()
        If active.Count = 0 Then Return

        Dim workArea = SystemParameters.WorkArea
        Dim y = workArea.Bottom - 16
        For i As Integer = 0 To active.Count - 1
            Dim popup = active(i)
            popup.Left = workArea.Right - popup.Width - 16
            popup.Top = y - popup.Height
            y -= popup.Height + 8
        Next
    End Sub

    ''' <summary>
    ''' Al click sul popup, ripristina la finestra principale e seleziona l'account associato alla notifica.
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
        RemoveFromActive()
        _closeTimer?.Stop()
        MyBase.OnClosed(e)
    End Sub
End Class