Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Windows
Imports HidaChat.UI.Theme

''' <summary>
''' Fluent Design modal diagnostic dialog for reporting Tailscale connectivity issues,
''' offline nodes, or unreachable gateway hosts on Hermes / OpenClaw profiles.
''' </summary>
Public Class TailscaleConnectionErrorDialog

    Public Property RetryRequested As Boolean = False
    Public Property OpenSettingsRequested As Boolean = False
    Public Property LoginUrl As String = ""

    Private ReadOnly _loc As AppLocalizations

    ''' <summary>
    ''' Parameterless constructor preserved for Visual Studio XAML Designer compatibility.
    ''' </summary>
    Public Sub New()
        InitializeComponent()
    End Sub

    Public Sub New(loc As AppLocalizations, accountName As String, platform As String, statusKey As String, Optional details As String = "", Optional loginUrl As String = "")
        InitializeComponent()
        _loc = loc
        Me.LoginUrl = loginUrl
        If Not DesignerProperties.GetIsInDesignMode(Me) Then
            ThemeManager.Instance.RegisterWindow(Me)
        End If
        ApplyDiagnosis(accountName, platform, statusKey, details, loginUrl)
    End Sub

    Protected Overrides Sub OnClosed(e As EventArgs)
        ThemeManager.Instance.UnregisterWindow(Me)
        MyBase.OnClosed(e)
    End Sub

    Private Sub ApplyDiagnosis(accountName As String, platform As String, statusKey As String, details As String, loginUrl As String)
        Dim effName = If(Not String.IsNullOrWhiteSpace(accountName), accountName, "Account")
        Dim effPlatform = If(Not String.IsNullOrWhiteSpace(platform), platform, "Hermes/OpenClaw")

        If _loc IsNot Nothing Then
            Dim titleText = _loc.Get("tailscale_dialog_title")
            Me.Title = titleText
            TxtTitle.Text = titleText

            Dim headerArgs As New Dictionary(Of String, String) From {
                {"name", effName},
                {"platform", effPlatform}
            }
            TxtHeader.Text = _loc.Get("tailscale_dialog_header", headerArgs)

            Dim msgArgs As New Dictionary(Of String, String) From {
                {"name", effName},
                {"platform", effPlatform},
                {"host", If(Not String.IsNullOrWhiteSpace(details), details, "Tailnet")}
            }

            Select Case statusKey
                Case "tailscale_tsnet_needs_login"
                    TxtMessage.Text = _loc.Get("tailscale_error_tsnet_needs_login", msgArgs)
                    If Not String.IsNullOrWhiteSpace(loginUrl) Then
                        BtnLogin.Visibility = Visibility.Visible
                        BtnLogin.Content = _loc.Get("tailscale_btn_login")
                    End If
                Case "tailscale_tsnet_stopped"
                    TxtMessage.Text = _loc.Get("tailscale_error_tsnet_stopped", msgArgs)
                Case "tailscale_node_unreachable"
                    TxtMessage.Text = _loc.Get("tailscale_error_node_unreachable", msgArgs)
                Case "tailscale_client_disconnected"
                    TxtMessage.Text = _loc.Get("tailscale_error_client_disconnected", msgArgs)
                Case Else
                    TxtMessage.Text = _loc.Get("tailscale_error_generic_failure", msgArgs)
            End Select

            BtnRetry.Content = _loc.Get("tailscale_btn_retry")
            BtnSettings.Content = _loc.Get("tailscale_btn_settings")
            BtnClose.Content = _loc.Get("tailscale_btn_close")
        Else
            TxtTitle.Text = "HidaChat — Tailscale Connection Status"
            TxtHeader.Text = $"Unable to load {effPlatform}: {effName}"
            TxtMessage.Text = $"Tailscale connectivity to '{details}' is not active. Check your Tailscale status and retry."
            BtnRetry.Content = "Retry"
            BtnSettings.Content = "Settings"
            BtnClose.Content = "Close"
            If Not String.IsNullOrWhiteSpace(loginUrl) Then
                BtnLogin.Visibility = Visibility.Visible
            End If
        End If
    End Sub

    Private Sub BtnRetry_Click(sender As Object, e As RoutedEventArgs)
        RetryRequested = True
        DialogResult = True
        Close()
    End Sub

    Private Sub BtnSettings_Click(sender As Object, e As RoutedEventArgs)
        OpenSettingsRequested = True
        DialogResult = False
        Close()
    End Sub

    Private Sub BtnLogin_Click(sender As Object, e As RoutedEventArgs)
        If Not String.IsNullOrWhiteSpace(LoginUrl) Then
            Try
                Process.Start(New ProcessStartInfo With {
                    .FileName = LoginUrl,
                    .UseShellExecute = True
                })
            Catch ex As Exception
                Debug.WriteLine($"Failed to open login URL: {ex.Message}")
            End Try
        End If
    End Sub

    Private Sub BtnClose_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
        Close()
    End Sub

End Class
