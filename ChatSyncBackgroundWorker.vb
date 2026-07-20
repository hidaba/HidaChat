Imports System.Text.Json
Imports System.Threading.Tasks
Imports Microsoft.Web.WebView2.Wpf

Public Class ChatSyncBackgroundWorker
    Private ReadOnly _storageService As ChatJsonStorageService
    Private _isSyncing As Boolean = False
    Public Property IsSyncing As Boolean
        Get
            Return _isSyncing
        End Get
        Private Set(value As Boolean)
            _isSyncing = value
        End Set
    End Property

    Public Event SyncProgressChanged(sender As Object, statusMessage As String)
    Public Event SyncCompleted(sender As Object, success As Boolean)

    Public Sub New(accountId As String, Optional customBaseDir As String = Nothing)
        _storageService = New ChatJsonStorageService(accountId, customBaseDir)
    End Sub

    Public ReadOnly Property StorageService As ChatJsonStorageService
        Get
            Return _storageService
        End Get
    End Property

    ''' <summary>
    ''' Avvia la richiesta di sincronizzazione chiedendo a WebView2 di scansionare le chat.
    ''' </summary>
    Public Async Function RequestSyncAsync(webView As WebView2, bridgeToken As String) As Task
        If _isSyncing OrElse webView Is Nothing OrElse webView.CoreWebView2 Is Nothing Then Return

        _isSyncing = True
        RaiseEvent SyncProgressChanged(Me, "Avvio sincronizzazione chat in background...")

        Try
            ' Carica l'indice per determinare i cutoff per ogni chat
            Dim indexData = Await _storageService.LoadIndexAsync()
            Dim defaultCutoff = DateTimeOffset.UtcNow.AddMonths(-3).ToUnixTimeSeconds()

            ' Prepara mappa [ChatId -> UnixTimestampCutoff]
            Dim cutoffMap As New Dictionary(Of String, Long)()
            For Each kvp In indexData.Chats
                If kvp.Value.LastSyncTimestamp.HasValue Then
                    Dim unixTs = New DateTimeOffset(kvp.Value.LastSyncTimestamp.Value).ToUnixTimeSeconds()
                    cutoffMap(kvp.Key) = unixTs
                End If
            Next

            Dim cutoffMapJson = JsonSerializer.Serialize(cutoffMap)

            ' Script di avvio per JS
            Dim initJs = $"window.startWhatsAppChatSync({defaultCutoff}, {cutoffMapJson}, '{bridgeToken}');"

            Await webView.Dispatcher.InvokeAsync(Async Function()
                                                     Await webView.CoreWebView2.ExecuteScriptAsync(initJs)
                                                 End Function)

        Catch ex As Exception
            _isSyncing = False
            RaiseEvent SyncProgressChanged(Me, "Errore avvio sincronizzazione: " & ex.Message)
            RaiseEvent SyncCompleted(Me, False)
        End Try
    End Function

    ''' <summary>
    ''' Processa in background i lotti di messaggi ricevuti via postMessage da WebView2.
    ''' </summary>
    Public Async Function ProcessIncomingBatchAsync(jsonPayload As JsonElement) As Task
        Await Task.Run(Sub()
                           ProcessIncomingBatchInternal(jsonPayload)
                       End Sub)
    End Function

    Private Sub ProcessIncomingBatchInternal(jsonPayload As JsonElement)
        Try
            Dim chatId = If(jsonPayload.TryGetProperty("chatId", Nothing), jsonPayload.GetProperty("chatId").GetString(), String.Empty)
            Dim chatName = If(jsonPayload.TryGetProperty("chatName", Nothing), jsonPayload.GetProperty("chatName").GetString(), "Chat")
            Dim isGroup = If(jsonPayload.TryGetProperty("isGroup", Nothing), jsonPayload.GetProperty("isGroup").GetBoolean(), False)

            If String.IsNullOrEmpty(chatId) Then Return

            Dim messagesArray As New List(Of JsonElement)()
            If jsonPayload.TryGetProperty("messages", Nothing) Then
                For Each msg In jsonPayload.GetProperty("messages").EnumerateArray()
                    messagesArray.Add(msg)
                Next
            End If

            If messagesArray.Count > 0 Then
                RaiseEvent SyncProgressChanged(Me, $"Salvataggio {messagesArray.Count} messaggi cifrati per: {chatName}")
                _storageService.SaveMessageBatchAsync(chatId, chatName, isGroup, messagesArray).GetAwaiter().GetResult()
            End If

            Dim isFinished = If(jsonPayload.TryGetProperty("isFinished", Nothing), jsonPayload.GetProperty("isFinished").GetBoolean(), False)
            If isFinished Then
                _isSyncing = False
                RaiseEvent SyncProgressChanged(Me, "Sincronizzazione completata con successo.")
                RaiseEvent SyncCompleted(Me, True)
            End If

        Catch ex As Exception
            RaiseEvent SyncProgressChanged(Me, "Errore elaborazione lotto: " & ex.Message)
        End Try
    End Sub
End Class

Public Module TaskExtensions
    <System.Runtime.CompilerServices.Extension>
    Public Sub Forget(task As Task)
        ' Evita avvisi di compilazione per Task non attesi
    End Sub
End Module
