Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Threading
Imports HidaChat.UI.Theme

''' <summary>
''' Fluent Design modal progress dialog showing real-time status while verifying
''' and recovering corrupted IndexedDB/ServiceWorker local storage.
''' </summary>
Public Class StorageRecoveryProgressDialog

    Public Sub New(Optional loc As AppLocalizations = Nothing, Optional accountName As String = "", Optional platform As String = "")
        InitializeComponent()
        If Not DesignerProperties.GetIsInDesignMode(Me) Then
            ThemeManager.Instance.RegisterWindow(Me)
        End If
        ApplyLocalization(loc, accountName, platform)
    End Sub

    Protected Overrides Sub OnClosed(e As EventArgs)
        ThemeManager.Instance.UnregisterWindow(Me)
        MyBase.OnClosed(e)
    End Sub

    ''' <summary>
    ''' Applies localized labels across the 5 supported languages.
    ''' </summary>
    Public Sub ApplyLocalization(loc As AppLocalizations, Optional accountName As String = "", Optional platform As String = "")
        Dim effectiveName = If(Not String.IsNullOrWhiteSpace(accountName), accountName, "Account")
        Dim effectivePlatform = If(Not String.IsNullOrWhiteSpace(platform), platform, "Web")

        If loc IsNot Nothing Then
            TxtTitle.Text = loc.Get("storage_recovery_dialog_title")
            TxtHeader.Text = $"{loc.Get("storage_recovery_dialog_header")} ({effectiveName})"
            Dim args As New Dictionary(Of String, String) From {
                {"account", effectiveName},
                {"platform", effectivePlatform}
            }
            TxtDetail.Text = loc.Get("storage_recovery_stage_checking", args)
        Else
            TxtTitle.Text = "HidaChat — Storage Recovery"
            TxtHeader.Text = $"Recovering local storage ({effectiveName})..."
            TxtDetail.Text = $"Checking storage integrity for {effectiveName}..."
        End If
    End Sub

    ''' <summary>
    ''' Updates the current recovery stage text in a thread-safe manner.
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
