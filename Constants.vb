Imports System.IO

Public Module Constants
    Public Const AppVersion As String = "0.1.1"

    ' Configurazione GitHub Releases OTA
    Public Const GitHubOwner As String = "hidaba"
    Public Const GitHubRepo As String = "WhatsAppH"
    Public Const GitHubReleasesApiUrl As String = "https://api.github.com/repos/hidaba/WhatsAppH/releases"
    Public Const GitHubLatestReleaseApiUrl As String = "https://api.github.com/repos/hidaba/WhatsAppH/releases/latest"

    ' Percorsi di fallback OTA locale (opzionali)
    Public Const UpdateFilesPath As String = "\\192.168.1.4\massimo\OTARepository\WhatsappH"
    Public Const UpdateFilesPathBeta As String = "\\192.168.1.4\massimo\OTARepository\WhatsappHBeta"

    Public ReadOnly Property UpdateVersionFile As String
        Get
            Return Path.Combine(UpdateFilesPath, "version.txt")
        End Get
    End Property

    Public ReadOnly Property UpdateVersionFileBeta As String
        Get
            Return Path.Combine(UpdateFilesPathBeta, "version.txt")
        End Get
    End Property
End Module

