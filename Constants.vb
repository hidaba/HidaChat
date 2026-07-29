Imports System.IO

''' <summary>
''' Contiene le costanti globali dell'applicazione, informazioni sulla versione e parametri di configurazione per gli aggiornamenti.
''' </summary>
Public Module Constants
    ''' <summary>Versione corrente dell'applicazione.</summary>
    Public Const AppVersion As String = "0.1.6"





    ' Configurazione repository GitHub per il sistema di aggiornamento OTA
    Public Const GitHubOwner As String = "hidaba"
    Public Const GitHubRepo As String = "WhatsAppH"
    Public Const GitHubReleasesApiUrl As String = "https://api.github.com/repos/hidaba/WhatsAppH/releases"
    Public Const GitHubLatestReleaseApiUrl As String = "https://api.github.com/repos/hidaba/WhatsAppH/releases/latest"

    ' Percorsi di fallback per gli aggiornamenti OTA da rete locale
    Public Const UpdateFilesPath As String = "\\192.168.1.4\massimo\OTARepository\WhatsappH"
    Public Const UpdateFilesPathBeta As String = "\\192.168.1.4\massimo\OTARepository\WhatsappHBeta"

    ''' <summary>Percorso del file di versione remoto nel repository locale stabile.</summary>
    Public ReadOnly Property UpdateVersionFile As String
        Get
            Return Path.Combine(UpdateFilesPath, "version.txt")
        End Get
    End Property

    ''' <summary>Percorso del file di versione remoto nel repository locale beta.</summary>
    Public ReadOnly Property UpdateVersionFileBeta As String
        Get
            Return Path.Combine(UpdateFilesPathBeta, "version.txt")
        End Get
    End Property
End Module


