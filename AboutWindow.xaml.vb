Imports System.ComponentModel
Imports System.Diagnostics
Imports HidaChat.UI.Theme

''' <summary>
''' Fluent Design WPF dialog displaying application metadata (author, version, release date, license, and links).
''' </summary>
Public Class AboutWindow
    Private ReadOnly _settingsController As SettingsController

    ''' <summary>
    ''' Parameterless constructor preserved for Visual Studio XAML Designer compatibility.
    ''' </summary>
    Public Sub New()
        InitializeComponent()
    End Sub

    Public Sub New(settingsController As SettingsController)
        InitializeComponent()
        _settingsController = settingsController
    End Sub

    Private Sub AboutWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If DesignerProperties.GetIsInDesignMode(Me) Then Return

        Try
            ThemeManager.Instance.RegisterWindow(Me)
            SyncThemeFromSettings()

            BadgeVersion.Text = "v" & Constants.AppVersion
            TxtAuthor.Text = Constants.AppAuthor
            TxtReleaseDate.Text = Constants.AppReleaseDate
            TxtLicense.Text = Constants.AppLicense

            RefreshLocalization()
        Catch ex As Exception
            Debug.WriteLine($"AboutWindow_Loaded error: {ex.Message}")
        End Try
    End Sub

    Protected Overrides Sub OnClosed(e As EventArgs)
        ThemeManager.Instance.UnregisterWindow(Me)
        MyBase.OnClosed(e)
    End Sub

    Private Sub SyncThemeFromSettings()
        If _settingsController Is Nothing Then Return
        Dim mode = AppThemeMode.System
        Select Case _settingsController.Theme
            Case "Dark"
                mode = AppThemeMode.Dark
            Case "Light"
                mode = AppThemeMode.Light
        End Select
        ThemeManager.Instance.SetTheme(mode, persist:=False)
    End Sub

    Private Sub BtnClose_Click(sender As Object, e As RoutedEventArgs)
        Me.Close()
    End Sub

    Private Sub BtnRepo_Click(sender As Object, e As RoutedEventArgs)
        OpenUrl(Constants.AppGitHubUrl)
    End Sub

    Private Sub BtnReleases_Click(sender As Object, e As RoutedEventArgs)
        OpenUrl(Constants.AppGitHubUrl & "/releases")
    End Sub

    Private Sub BtnIssues_Click(sender As Object, e As RoutedEventArgs)
        OpenUrl(Constants.AppGitHubUrl & "/issues")
    End Sub

    Private Shared Sub OpenUrl(url As String)
        Try
            Process.Start(New ProcessStartInfo(url) With {
                .UseShellExecute = True
            })
        Catch ex As Exception
            Debug.WriteLine($"Failed to open URL {url}: {ex.Message}")
        End Try
    End Sub

    Private Sub RefreshLocalization()
        If _settingsController Is Nothing OrElse _settingsController.Localizations Is Nothing Then Return
        Dim loc = _settingsController.Localizations

        Me.Title = loc.Get("about_title")
        TxtDescription.Text = loc.Get("app_description")
        LabelAuthor.Text = loc.Get("author")
        LabelReleaseDate.Text = loc.Get("release_date")
        LabelLicense.Text = loc.Get("license")
        LabelEnvironment.Text = loc.Get("runtime_environment")
        LabelPortablePath.Text = loc.Get("portable_directory")
        BtnRepo.Content = loc.Get("github_repository")
        BtnReleases.Content = loc.Get("view_releases")
        BtnIssues.Content = loc.Get("report_issue")
        BtnCloseBottom.Content = loc.Get("close")
    End Sub
End Class
