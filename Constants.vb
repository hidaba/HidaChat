Imports System.IO

''' <summary>
''' Contiene le costanti globali dell'applicazione, informazioni sulla versione e parametri di configurazione per gli aggiornamenti.
''' </summary>
Public Module Constants
    ''' <summary>Versione corrente dell'applicazione.</summary>
    Public Const AppVersion As String = "0.1.8"





    ' Configurazione repository GitHub per il sistema di aggiornamento OTA
    Public Const GitHubOwner As String = "hidaba"
    Public Const GitHubRepo As String = "WhatsAppH"
    Public Const GitHubReleasesApiUrl As String = "https://api.github.com/repos/hidaba/WhatsAppH/releases"
    Public Const GitHubLatestReleaseApiUrl As String = "https://api.github.com/repos/hidaba/WhatsAppH/releases/latest"
End Module


