Imports System.Windows.Threading

''' <summary>
''' Finestra di popup personalizzata (notifica Toast in stile overlay) che compare in basso a destra 
''' allo schermo al ricevimento di un nuovo messaggio WhatsApp.
''' </summary>
Public Class MessagePopup
    Private ReadOnly _accountId As String
    Private _closeTimer As DispatcherTimer
    Private Shared ReadOnly _activePopups As New List(Of WeakReference(Of MessagePopup))()

    Public Sub New(accountId As String, title As String, body As String)
        InitializeComponent()
        _accountId = accountId
        NameText.Text = title
        MessageText.Text = body
        InitialsText.Text = GetInitials(title)
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