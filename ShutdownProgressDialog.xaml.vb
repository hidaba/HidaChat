Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Threading
Imports HidaChat.UI.Theme

''' <summary>
''' Compact, localized Fluent Design progress dialog shown during orderly
''' HidaChat data saving and application shutdown.
''' </summary>
Public Class ShutdownProgressDialog

    Public Sub New(Optional loc As AppLocalizations = Nothing)
        InitializeComponent()
        If Not DesignerProperties.GetIsInDesignMode(Me) Then
            ThemeManager.Instance.RegisterWindow(Me)
        End If
        ApplyLocalization(loc)
    End Sub

    Protected Overrides Sub OnClosed(e As EventArgs)
        ThemeManager.Instance.UnregisterWindow(Me)
        MyBase.OnClosed(e)
    End Sub

    ''' <summary>
    ''' Applies localized labels across the 5 supported languages.
    ''' </summary>
    Public Sub ApplyLocalization(loc As AppLocalizations)
        If loc IsNot Nothing Then
            TxtTitle.Text = loc.Get("shutdown_dialog_title")
            TxtHeader.Text = loc.Get("shutdown_dialog_header")
            TxtDetail.Text = loc.Get("shutdown_stage_settings")
        Else
            TxtTitle.Text = "HidaChat"
            TxtHeader.Text = "Saving data and closing..."
            TxtDetail.Text = "Saving settings and account preferences..."
        End If
    End Sub

    ''' <summary>
    ''' Updates the current shutdown stage text in a thread-safe manner.
    ''' </summary>
    Public Sub UpdateStatus(statusText As String)
        If String.IsNullOrWhiteSpace(statusText) Then Return

        If Not Dispatcher.CheckAccess() Then
            Dispatcher.InvokeAsync(Sub() UpdateStatus(statusText))
            Return
        End If

        TxtDetail.Text = statusText
        Dispatcher.Invoke(DispatcherPriority.Render, Sub()
                                                     End Sub)
    End Sub

End Class
