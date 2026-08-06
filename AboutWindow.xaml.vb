Imports System.Diagnostics
Imports System.Windows.Media

''' <summary>
''' Finestra di dialogo WPF che mostra le informazioni sull'applicazione (autore, versione, data, licenza e link utili).
''' </summary>
Public Class AboutWindow
    Private ReadOnly _settingsController As SettingsController

    Public Sub New(settingsController As SettingsController)
        InitializeComponent()
        _settingsController = settingsController
    End Sub

    Private Sub AboutWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Try
            ' Popola le costanti di versione e metadati
            TxtVersionTag.Text = "v" & Constants.AppVersion
            TxtAuthor.Text = Constants.AppAuthor
            TxtReleaseDate.Text = Constants.AppReleaseDate
            TxtLicense.Text = Constants.AppLicense
            
            RefreshLocalization()
            ApplyTheme()
        Catch ex As Exception
            Debug.WriteLine($"AboutWindow_Loaded error: {ex.Message}")
        End Try
    End Sub

    Private Sub TitleBar_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        If e.ChangedButton = MouseButton.Left Then
            Me.DragMove()
        End If
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

        TxtTitle.Text = loc.Get("about_title")
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

    Private Sub ApplyTheme()
        If _settingsController Is Nothing Then Return
        Dim isDark = _settingsController.IsDarkThemeEffective

        If isDark Then
            AboutBorder.Background = BrushCache.GetBrush("#1f2c34")
            AboutBorder.BorderBrush = BrushCache.GetBrush("#2f3e46")
            TitleBar.Background = BrushCache.GetBrush("#202c33")
            TxtTitle.Foreground = BrushCache.GetBrush("#e9edef")
        Else
            AboutBorder.Background = BrushCache.GetBrush("#f0f2f5")
            AboutBorder.BorderBrush = BrushCache.GetBrush("#d1d7db")
            TitleBar.Background = BrushCache.GetBrush("#e9edef")
            TxtTitle.Foreground = BrushCache.GetBrush("#111b21")
            TxtAuthor.Foreground = BrushCache.GetBrush("#111b21")
            TxtReleaseDate.Foreground = BrushCache.GetBrush("#111b21")
            TxtLicense.Foreground = BrushCache.GetBrush("#111b21")
            TxtEnvironment.Foreground = BrushCache.GetBrush("#111b21")
            TxtPortablePath.Foreground = BrushCache.GetBrush("#111b21")
        End If
    End Sub
End Class
