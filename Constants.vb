Imports System.IO

''' <summary>
''' Contains application global constants, version information, and OTA update configuration.
''' </summary>
Public Module Constants
    ''' <summary>Current application version.</summary>
    Public Const AppVersion As String = "1.5.13-beta"

    ''' <summary>Main application author.</summary>
    Public Const AppAuthor As String = "Massimo Balestrieri (hidaba)"

    ''' <summary>Release date for current version.</summary>
    Public Const AppReleaseDate As String = "2026-11-27"

    ''' <summary>Application software license.</summary>
    Public Const AppLicense As String = "Apache-2.0 License"

    ''' <summary>Official GitHub repository URL.</summary>
    Public Const AppGitHubUrl As String = "https://github.com/hidaba/HidaChat"

    ' GitHub repository configuration for OTA update checker
    Public Const GitHubOwner As String = "hidaba"
    Public Const GitHubRepo As String = "HidaChat"
    Public Const GitHubReleasesApiUrl As String = "https://api.github.com/repos/hidaba/HidaChat/releases?per_page=100"
    Public Const GitHubLatestReleaseApiUrl As String = "https://api.github.com/repos/hidaba/HidaChat/releases/latest"
End Module
