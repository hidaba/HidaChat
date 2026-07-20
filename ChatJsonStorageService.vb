Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports System.Threading
Imports System.Threading.Tasks

Public Class ChatIndexEntry
    Public Property ChatId As String = String.Empty
    Public Property Name As String = String.Empty
    Public Property IsGroup As Boolean = False
    Public Property LastSyncTimestamp As DateTime? = Nothing
    Public Property MessageCount As Integer = 0
End Class

Public Class ChatIndexData
    Public Property AccountId As String = String.Empty
    Public Property LastGlobalSync As DateTime? = Nothing
    Public Property Chats As Dictionary(Of String, ChatIndexEntry) = New Dictionary(Of String, ChatIndexEntry)()
End Class

Public Class ChatJsonStorageService
    Private ReadOnly _accountId As String
    Private ReadOnly _baseAccountDir As String
    Private ReadOnly _chatsDir As String
    Private ReadOnly _fileLock As New SemaphoreSlim(1, 1)

    Public ReadOnly Property AccountId As String
        Get
            Return _accountId
        End Get
    End Property

    Private ReadOnly Property IndexFilePath As String
        Get
            Dim encryptedIndexName = CryptoHelper.EncryptFilename("index") & ".enc"
            Dim newPath = Path.Combine(_chatsDir, encryptedIndexName)
            If File.Exists(newPath) Then Return newPath
            Dim legacyPath = Path.Combine(_chatsDir, "index.enc")
            If File.Exists(legacyPath) Then Return legacyPath
            Return newPath
        End Get
    End Property

    Public Sub New(accountId As String, Optional customBaseDir As String = Nothing)
        Me._accountId = If(String.IsNullOrEmpty(accountId), "default", accountId)
        
        Dim rootDir = If(String.IsNullOrEmpty(customBaseDir), AppDomain.CurrentDomain.BaseDirectory, customBaseDir)
        _baseAccountDir = Path.Combine(rootDir, Constants.DefaultBackupFolderName, "Accounts", _accountId)
        _chatsDir = Path.Combine(_baseAccountDir, Constants.ChatsEncryptedFolderName)

        EnsureDirectoriesCreated()
    End Sub

    Private Sub EnsureDirectoriesCreated()
        If Not Directory.Exists(_chatsDir) Then
            Directory.CreateDirectory(_chatsDir)
        End If
    End Sub

    Public Function GetPeriodIndex(dt As DateTime) As Long
        If Constants.BackupRotationDays <= 0 Then Return 0
        Dim epoch As New DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        Dim totalDays As Long = CLng((dt.ToUniversalTime().Date - epoch).TotalDays)
        Return totalDays \ Constants.BackupRotationDays
    End Function

    Public Function GetChatFilePath(chatId As String, dt As DateTime) As String
        Dim period = GetPeriodIndex(dt)
        Dim rawIdentifier = $"{chatId}_p{period}"
        Dim encryptedFileName = CryptoHelper.EncryptFilename(rawIdentifier) & ".enc"
        Return Path.Combine(_chatsDir, encryptedFileName)
    End Function

    Public Async Function LoadIndexAsync() As Task(Of ChatIndexData)
        Await _fileLock.WaitAsync()
        Try
            Dim indexPath = IndexFilePath
            If Not File.Exists(indexPath) Then
                Return New ChatIndexData With {.AccountId = _accountId}
            End If

            Dim encryptedText = Await File.ReadAllTextAsync(indexPath)
            Dim jsonText = CryptoHelper.DecryptString(encryptedText)
            If String.IsNullOrEmpty(jsonText) Then
                Return New ChatIndexData With {.AccountId = _accountId}
            End If

            Dim data = JsonSerializer.Deserialize(Of ChatIndexData)(jsonText)
            Return If(data, New ChatIndexData With {.AccountId = _accountId})
        Catch ex As Exception
            Return New ChatIndexData With {.AccountId = _accountId}
        Finally
            _fileLock.Release()
        End Try
    End Function

    Public Async Function SaveIndexAsync(indexData As ChatIndexData) As Task
        Await _fileLock.WaitAsync()
        Try
            EnsureDirectoriesCreated()
            indexData.LastGlobalSync = DateTime.UtcNow
            Dim jsonText = JsonSerializer.Serialize(indexData)
            Dim encryptedText = CryptoHelper.EncryptString(jsonText)
            Await File.WriteAllTextAsync(IndexFilePath, encryptedText)
        Catch ex As Exception
            ' Log o gestione errore scrittura indice
        Finally
            _fileLock.Release()
        End Try
    End Function

    Public Async Function GetLastSyncTimestampForChatAsync(chatId As String) As Task(Of DateTime?)
        Dim indexData = Await LoadIndexAsync()
        If indexData.Chats.ContainsKey(chatId) Then
            Return indexData.Chats(chatId).LastSyncTimestamp
        End If
        Return Nothing
    End Function

    Public Async Function SaveMessageBatchAsync(chatId As String, chatName As String, isGroup As Boolean, messagesList As List(Of JsonElement)) As Task
        If messagesList Is Nothing OrElse messagesList.Count = 0 Then Return

        Await _fileLock.WaitAsync()
        Try
            EnsureDirectoriesCreated()
            Dim maxTimestamp As DateTime? = Nothing
            Dim fileBatches As New Dictionary(Of String, List(Of String))()
            Dim totalSavedMessagesCount As Integer = 0

            For Each msgElement In messagesList
                Dim msgNode = JsonNode.Parse(msgElement.GetRawText())
                If msgNode IsNot Nothing Then
                    ' Rimuovi eventuali dati media (i media sono esclusi dal backup)
                    If msgNode("mediaData") IsNot Nothing Then
                        msgNode.AsObject().Remove("mediaData")
                    End If

                    Dim msgDate As DateTime = DateTime.UtcNow

                    ' Controllo timestamp per aggiornare l'indice e determinare la data del file
                    If msgNode("timestamp") IsNot Nothing Then
                        Dim tsLong As Long = 0
                        If Long.TryParse(msgNode("timestamp").ToString(), tsLong) Then
                            Dim dt = DateTimeOffset.FromUnixTimeSeconds(tsLong).UtcDateTime
                            msgDate = dt
                            If maxTimestamp Is Nothing OrElse dt > maxTimestamp.Value Then
                                maxTimestamp = dt
                            End If
                        ElseIf DateTime.TryParse(msgNode("timestamp").ToString(), Nothing) Then
                            Dim dt = DateTime.Parse(msgNode("timestamp").ToString()).ToUniversalTime()
                            msgDate = dt
                            If maxTimestamp Is Nothing OrElse dt > maxTimestamp.Value Then
                                maxTimestamp = dt
                            End If
                        End If
                    End If

                    ' Cifra la singola riga JSON del messaggio
                    Dim msgJsonText = msgNode.ToJsonString()
                    Dim encryptedLine = CryptoHelper.EncryptString(msgJsonText)

                    ' Determina il file di destinazione in base alla data e costante di rotazione
                    Dim targetFilePath = GetChatFilePath(chatId, msgDate)
                    If Not fileBatches.ContainsKey(targetFilePath) Then
                        fileBatches(targetFilePath) = New List(Of String)()
                    End If
                    fileBatches(targetFilePath).Add(encryptedLine)
                    totalSavedMessagesCount += 1
                End If
            Next

            ' Scrittura asincrona in append nei rispettivi file cifrati di chat
            For Each kvp In fileBatches
                If kvp.Value.Count > 0 Then
                    Await File.AppendAllLinesAsync(kvp.Key, kvp.Value)
                End If
            Next

            ' Aggiorna l'indice
            Dim indexData = Await LoadIndexInternalAsync()
            Dim entry As ChatIndexEntry = Nothing
            If Not indexData.Chats.TryGetValue(chatId, entry) Then
                entry = New ChatIndexEntry With {
                    .ChatId = chatId,
                    .Name = chatName,
                    .IsGroup = isGroup
                }
                indexData.Chats(chatId) = entry
            End If

            entry.Name = If(String.IsNullOrEmpty(chatName), entry.Name, chatName)
            entry.IsGroup = isGroup
            entry.MessageCount += totalSavedMessagesCount
            If maxTimestamp.HasValue Then
                If Not entry.LastSyncTimestamp.HasValue OrElse maxTimestamp.Value > entry.LastSyncTimestamp.Value Then
                    entry.LastSyncTimestamp = maxTimestamp.Value
                End If
            End If

            Await SaveIndexInternalAsync(indexData)

        Catch ex As Exception
            ' Gestione eccezioni salvataggio
        Finally
            _fileLock.Release()
        End Try
    End Function

    Private Async Function LoadIndexInternalAsync() As Task(Of ChatIndexData)
        Dim indexPath = IndexFilePath
        If Not File.Exists(indexPath) Then
            Return New ChatIndexData With {.AccountId = _accountId}
        End If

        Dim encryptedText = Await File.ReadAllTextAsync(indexPath)
        Dim jsonText = CryptoHelper.DecryptString(encryptedText)
        If String.IsNullOrEmpty(jsonText) Then
            Return New ChatIndexData With {.AccountId = _accountId}
        End If

        Dim data = JsonSerializer.Deserialize(Of ChatIndexData)(jsonText)
        Return If(data, New ChatIndexData With {.AccountId = _accountId})
    End Function

    Private Async Function SaveIndexInternalAsync(indexData As ChatIndexData) As Task
        EnsureDirectoriesCreated()
        indexData.LastGlobalSync = DateTime.UtcNow
        Dim jsonText = JsonSerializer.Serialize(indexData)
        Dim encryptedText = CryptoHelper.EncryptString(jsonText)
        Await File.WriteAllTextAsync(IndexFilePath, encryptedText)
    End Function
End Class
