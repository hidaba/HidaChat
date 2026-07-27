Imports System.IO

Public Module Constants
    Public Const AppVersion As String = "0.1.0"

    ' Percorsi OTA (modificabili a livello di sorgente)
    Public Const UpdateFilesPath As String = "\\192.168.1.4\massimo\OTARepository\Whatsapp"
    Public Const UpdateFilesPathBeta As String = "\\192.168.1.4\massimo\OTARepository\WhatsappBeta"

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

