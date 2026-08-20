Imports System.IO

''' <summary>
''' Contiene le costanti globali dell'applicazione, informazioni sulla versione e parametri di configurazione per gli aggiornamenti.
''' </summary>
Public Module Constants
    ''' <summary>Versione corrente dell'applicazione.</summary>
    Public Const AppVersion As String = "0.6.2-beta"

    ''' <summary>Autore principale dell'applicazione.</summary>
    Public Const AppAuthor As String = "Massimo Balestrieri (hidaba)"

    ''' <summary>Data di rilascio della versione corrente.</summary>
    Public Const AppReleaseDate As String = "2026-08-20"

    ''' <summary>Licenza software dell'applicazione.</summary>
    Public Const AppLicense As String = "Apache-2.0 License"

    ''' <summary>URL del repository GitHub ufficiale.</summary>
    Public Const AppGitHubUrl As String = "https://github.com/hidaba/HidaChat"

    ' Configurazione repository GitHub per il sistema di aggiornamento OTA
    Public Const GitHubOwner As String = "hidaba"
    Public Const GitHubRepo As String = "HidaChat"
    Public Const GitHubReleasesApiUrl As String = "https://api.github.com/repos/hidaba/HidaChat/releases"
    Public Const GitHubLatestReleaseApiUrl As String = "https://api.github.com/repos/hidaba/HidaChat/releases/latest"
End Module


