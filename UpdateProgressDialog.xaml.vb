Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Threading
Imports HidaChat.UI.Theme

''' <summary>
''' Fluent Design modal progress dialog showing real-time download, verification,
''' and installation stages during HidaChat OTA updates.
''' </summary>
Public Class UpdateProgressDialog

    Public Sub New(Optional loc As AppLocalizations = Nothing, Optional version As String = "")
        InitializeComponent()
        If Not DesignerProperties.GetIsInDesignMode(Me) Then
            ThemeManager.Instance.RegisterWindow(Me)
        End If
        ApplyLocalization(loc, version)
    End Sub

    Protected Overrides Sub OnClosed(e As EventArgs)
        ThemeManager.Instance.UnregisterWindow(Me)
        MyBase.OnClosed(e)
    End Sub

    ''' <summary>
    ''' Applies localized labels across the 5 supported languages.
    ''' </summary>
    Public Sub ApplyLocalization(loc As AppLocalizations, Optional version As String = "")
        Dim verText = If(Not String.IsNullOrWhiteSpace(version), $" (v{version.TrimStart("v"c)})", "")

        If loc IsNot Nothing Then
            TxtTitle.Text = loc.Get("update_dialog_title")
            Dim args As New Dictionary(Of String, String) From {{"version", version.TrimStart("v"c)}}
            TxtHeader.Text = loc.Get("update_stage_downloading", args)
            TxtDetail.Text = loc.Get("update_stage_downloading", args)
        Else
            TxtTitle.Text = "HidaChat"
            TxtHeader.Text = $"Updating HidaChat{verText}..."
            TxtDetail.Text = "Downloading update package..."
        End If
    End Sub

    ''' <summary>
    ''' Switches the ProgressBar between indeterminate and determinate modes.
    ''' </summary>
    Public Sub SetIndeterminate(isIndeterminate As Boolean)
        If Not Dispatcher.CheckAccess() Then
            Dispatcher.InvokeAsync(Sub() SetIndeterminate(isIndeterminate))
            Return
        End If

        ProgressBarStatus.IsIndeterminate = isIndeterminate
    End Sub

    ''' <summary>
    ''' Updates progress percentage and status labels in a thread-safe manner.
    ''' </summary>
    Public Sub SetProgress(percentage As Double, Optional detailText As String = "", Optional progressText As String = "")
        If Not Dispatcher.CheckAccess() Then
            Dispatcher.InvokeAsync(Sub() SetProgress(percentage, detailText, progressText))
            Return
        End If

        If percentage >= 0 Then
            ProgressBarStatus.IsIndeterminate = False
            ProgressBarStatus.Value = Math.Max(0, Math.Min(100, percentage))
        End If

        If Not String.IsNullOrWhiteSpace(detailText) Then
            TxtDetail.Text = detailText
        End If

        If progressText IsNot Nothing Then
            TxtProgressInfo.Text = progressText
        End If

        Dispatcher.Invoke(DispatcherPriority.Render, Sub()
                                                     End Sub)
    End Sub

    ''' <summary>
    ''' Updates the current stage description in a thread-safe manner.
    ''' </summary>
    Public Sub UpdateStatus(statusText As String, Optional detailText As String = "")
        If Not Dispatcher.CheckAccess() Then
            Dispatcher.InvokeAsync(Sub() UpdateStatus(statusText, detailText))
            Return
        End If

        If Not String.IsNullOrWhiteSpace(statusText) Then
            TxtDetail.Text = statusText
        End If

        If detailText IsNot Nothing Then
            TxtProgressInfo.Text = detailText
        End If

        Dispatcher.Invoke(DispatcherPriority.Render, Sub()
                                                     End Sub)
    End Sub

End Class
