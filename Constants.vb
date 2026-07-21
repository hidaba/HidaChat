Imports System.IO

Public Module Constants
    Public Const AppVersion As String = "1.4.5"

    ' Intervallo di rotazione del file di backup della chat (in giorni) - Costante in fase di compilazione
    Public Const BackupRotationDays As Integer = 7

    ' Chiave di cifratura AES-256 (32 bytes / 64 caratteri Hex)
    Public Const EncryptionKeyHex As String = "4A7F92B3C5E8D1A4F0E2B8C6D4A7F92B3C5E8D1A4F0E2B8C6D4A7F92B3C5E8D1"
    Public Const DefaultBackupFolderName As String = "Backup"
    Public Const ChatsEncryptedFolderName As String = "Chats_Encrypted"

    ' Percorsi OTA (modificabili a livello di sorgente)
    Public Const UpdateFilesPath As String = "\\fs1\annoni-new\IT\OTARepository\Whatsapp"
    Public Const UpdateFilesPathBeta As String = "\\fs1\annoni-new\IT\OTARepository\WhatsappBeta"

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

