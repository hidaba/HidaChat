Imports System.IO
Imports System.Security.Cryptography
Imports System.Text

Public Class CryptoHelper
    Private Shared ReadOnly KeyBytes As Byte() = ConvertHexToBytes(Constants.EncryptionKeyHex)

    Private Shared Function ConvertHexToBytes(hex As String) As Byte()
        Dim numberChars As Integer = hex.Length
        Dim bytes As Byte() = New Byte(numberChars / 2 - 1) {}
        For i As Integer = 0 To numberChars - 1 Step 2
            bytes(i / 2) = Convert.ToByte(hex.Substring(i, 2), 16)
        Next
        Return bytes
    End Function

    Private Shared Function ConvertToHexString(bytes As Byte()) As String
        Dim sb As New StringBuilder(bytes.Length * 2)
        For Each b In bytes
            sb.Append(b.ToString("X2"))
        Next
        Return sb.ToString()
    End Function

    ''' <summary>
    ''' Cifra in modo deterministico un identificatore (es. chatId + periodo) per generare un nome file cifrato e sicuro per il filesystem.
    ''' </summary>
    Public Shared Function EncryptFilename(plainText As String) As String
        If String.IsNullOrEmpty(plainText) Then Return "unknown"
        Dim plainBytes = Encoding.UTF8.GetBytes(plainText)

        ' Genera un Nonce deterministico di 12 byte calcolato via HMAC-SHA256 della stringa con KeyBytes
        Dim nonce(11) As Byte
        Using hmac As New HMACSHA256(KeyBytes)
            Dim hash = hmac.ComputeHash(plainBytes)
            Buffer.BlockCopy(hash, 0, nonce, 0, 12)
        End Using

        Dim tag(15) As Byte
        Dim cipherText(plainBytes.Length - 1) As Byte

        Using aesGcm As New AesGcm(KeyBytes, 16)
            aesGcm.Encrypt(nonce, plainBytes, cipherText, tag)
        End Using

        ' Formato: Nonce (12) + Tag (16) + CipherText (N)
        Dim result(12 + 16 + cipherText.Length - 1) As Byte
        Buffer.BlockCopy(nonce, 0, result, 0, 12)
        Buffer.BlockCopy(tag, 0, result, 12, 16)
        Buffer.BlockCopy(cipherText, 0, result, 28, cipherText.Length)

        Return ConvertToHexString(result)
    End Function

    ''' <summary>
    ''' Decifra un nome file cifrato in formato Hex per ottenere la stringa identificativa originale.
    ''' </summary>
    Public Shared Function DecryptFilename(hexFileName As String) As String
        If String.IsNullOrEmpty(hexFileName) Then Return String.Empty
        Try
            Dim encryptedBytes = ConvertHexToBytes(hexFileName)
            Dim plainBytes = DecryptBytes(encryptedBytes)
            Return Encoding.UTF8.GetString(plainBytes)
        Catch ex As Exception
            Return String.Empty
        End Try
    End Function

    ''' <summary>
    ''' Genera un Hash SHA-256 troncato a 16 caratteri esadecimali per identificare in modo anonimo un file chat.
    ''' </summary>
    Public Shared Function ComputeChatHash(chatId As String) As String
        If String.IsNullOrEmpty(chatId) Then Return "unknown_chat"
        Using sha256 As SHA256 = SHA256.Create()
            Dim bytes = Encoding.UTF8.GetBytes(chatId.Trim().ToLowerInvariant())
            Dim hashBytes = sha256.ComputeHash(bytes)
            Dim sb As New StringBuilder()
            For i As Integer = 0 To Math.Min(7, hashBytes.Length - 1)
                sb.Append(hashBytes(i).ToString("x2"))
            Next
            Return sb.ToString()
        End Using
    End Function

    ''' <summary>
    ''' Cifra una stringa in formato Base64 usando AES-256-GCM (Nonce 12B + Tag 16B + Ciphertext).
    ''' </summary>
    Public Shared Function EncryptString(plainText As String) As String
        If String.IsNullOrEmpty(plainText) Then Return String.Empty
        Dim plainBytes = Encoding.UTF8.GetBytes(plainText)
        Dim cipherBytes = EncryptBytes(plainBytes)
        Return Convert.ToBase64String(cipherBytes)
    End Function

    ''' <summary>
    ''' Decifra una stringa Base64 originariamente cifrata con EncryptString.
    ''' </summary>
    Public Shared Function DecryptString(cipherBase64 As String) As String
        If String.IsNullOrEmpty(cipherBase64) Then Return String.Empty
        Try
            Dim cipherBytes = Convert.FromBase64String(cipherBase64)
            Dim plainBytes = DecryptBytes(cipherBytes)
            Return Encoding.UTF8.GetString(plainBytes)
        Catch ex As Exception
            Return String.Empty
        End Try
    End Function

    ''' <summary>
    ''' Cifra un array di byte utilizzando AES-256-GCM.
    ''' </summary>
    Public Shared Function EncryptBytes(plainBytes As Byte()) As Byte()
        If plainBytes Is Nothing OrElse plainBytes.Length = 0 Then Return Array.Empty(Of Byte)()

        Dim nonce(11) As Byte ' 12 bytes nonce per GCM
        RandomNumberGenerator.Fill(nonce)

        Dim tag(15) As Byte ' 16 bytes auth tag
        Dim cipherText(plainBytes.Length - 1) As Byte

        Using aesGcm As New AesGcm(KeyBytes, 16)
            aesGcm.Encrypt(nonce, plainBytes, cipherText, tag)
        End Using

        ' Formato: Nonce (12) + Tag (16) + CipherText (N)
        Dim result(12 + 16 + cipherText.Length - 1) As Byte
        Buffer.BlockCopy(nonce, 0, result, 0, 12)
        Buffer.BlockCopy(tag, 0, result, 12, 16)
        Buffer.BlockCopy(cipherText, 0, result, 28, cipherText.Length)

        Return result
    End Function

    ''' <summary>
    ''' Decifra un array di byte originariamente cifrato con EncryptBytes.
    ''' </summary>
    Public Shared Function DecryptBytes(encryptedBytes As Byte()) As Byte()
        If encryptedBytes Is Nothing OrElse encryptedBytes.Length < 28 Then Return Array.Empty(Of Byte)()

        Dim nonce(11) As Byte
        Dim tag(15) As Byte
        Dim cipherTextLength As Integer = encryptedBytes.Length - 28
        Dim cipherText(cipherTextLength - 1) As Byte

        Buffer.BlockCopy(encryptedBytes, 0, nonce, 0, 12)
        Buffer.BlockCopy(encryptedBytes, 12, tag, 0, 16)
        Buffer.BlockCopy(encryptedBytes, 28, cipherText, 0, cipherTextLength)

        Dim plainBytes(cipherTextLength - 1) As Byte

        Using aesGcm As New AesGcm(KeyBytes, 16)
            aesGcm.Decrypt(nonce, cipherText, tag, plainBytes)
        End Using

        Return plainBytes
    End Function

    ''' <summary>
    ''' Cifra un file binario su disco salvando l'output cifrato.
    ''' </summary>
    Public Shared Async Function EncryptFileAsync(sourceFilePath As String, destinationFilePath As String) As Task
        Dim plainBytes = Await File.ReadAllBytesAsync(sourceFilePath)
        Dim cipherBytes = EncryptBytes(plainBytes)
        Dim destDir = Path.GetDirectoryName(destinationFilePath)
        If Not String.IsNullOrEmpty(destDir) AndAlso Not Directory.Exists(destDir) Then
            Directory.CreateDirectory(destDir)
        End If
        Await File.WriteAllBytesAsync(destinationFilePath, cipherBytes)
    End Function

    ''' <summary>
    ''' Decifra un file binario da disco.
    ''' </summary>
    Public Shared Async Function DecryptFileAsync(sourceFilePath As String, destinationFilePath As String) As Task
        Dim cipherBytes = Await File.ReadAllBytesAsync(sourceFilePath)
        Dim plainBytes = DecryptBytes(cipherBytes)
        Dim destDir = Path.GetDirectoryName(destinationFilePath)
        If Not String.IsNullOrEmpty(destDir) AndAlso Not Directory.Exists(destDir) Then
            Directory.CreateDirectory(destDir)
        End If
        Await File.WriteAllBytesAsync(destinationFilePath, plainBytes)
    End Function
End Class
