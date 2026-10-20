Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks

    ''' <summary>
    ''' Risultato dettagliato di un'operazione di traduzione singola.
    ''' </summary>
    Public Class TranslationResult
        Public Property Success As Boolean
        Public Property TranslatedText As String
        Public Property ErrorMessage As String

        Public Shared Function Succeeded(text As String) As TranslationResult
            Return New TranslationResult With {
                .Success = True,
                .TranslatedText = text,
                .ErrorMessage = Nothing
            }
        End Function

        Public Shared Function Failed(errMsg As String, Optional fallbackText As String = "") As TranslationResult
            Return New TranslationResult With {
                .Success = False,
                .TranslatedText = fallbackText,
                .ErrorMessage = errMsg
            }
        End Function
    End Class

    ''' <summary>
    ''' Risultato dettagliato di un'operazione di traduzione in batch.
    ''' </summary>
    Public Class TranslationBatchResult
        Public Property Success As Boolean
        Public Property Translations As List(Of String)
        Public Property ErrorMessage As String

        Public Shared Function Succeeded(results As List(Of String)) As TranslationBatchResult
            Return New TranslationBatchResult With {
                .Success = True,
                .Translations = If(results, New List(Of String)()),
                .ErrorMessage = Nothing
            }
        End Function

        Public Shared Function Failed(errMsg As String, Optional fallback As List(Of String) = Nothing) As TranslationBatchResult
            Return New TranslationBatchResult With {
                .Success = False,
                .Translations = If(fallback, New List(Of String)()),
                .ErrorMessage = errMsg
            }
        End Function
    End Class

    ''' <summary>
    ''' Risultato del test di connettività verso il fornitore di traduzione.
    ''' </summary>
    Public Class TranslationTestResult
        Public Property Success As Boolean
        Public Property Message As String
        Public Property LatencyMs As Long

        Public Shared Function Succeeded(latencyMs As Long, Optional msg As String = "") As TranslationTestResult
            Return New TranslationTestResult With {
                .Success = True,
                .LatencyMs = latencyMs,
                .Message = msg
            }
        End Function

        Public Shared Function Failed(msg As String) As TranslationTestResult
            Return New TranslationTestResult With {
                .Success = False,
                .Message = msg,
                .LatencyMs = 0
            }
        End Function
    End Class

    ''' <summary>
    ''' Interfaccia unificata per i provider di traduzione (Cloud Google, LibreTranslate, Ollama, OpenAI-compatible).
    ''' </summary>
    Public Interface ITranslationProvider
        ReadOnly Property Id As String
        ReadOnly Property DisplayName As String
        ReadOnly Property IsSelfHosted As Boolean
        Function TranslateSingleAsync(text As String, targetLang As String, cancellationToken As CancellationToken) As Task(Of TranslationResult)
        Function TranslateBatchAsync(texts As List(Of String), targetLang As String, cancellationToken As CancellationToken) As Task(Of TranslationBatchResult)
        Function TestConnectionAsync(cancellationToken As CancellationToken) As Task(Of TranslationTestResult)
    End Interface

    ''' <summary>
    ''' Provider di traduzione predefinito basato su endpoint pubblico Google Translate.
    ''' </summary>
    Public Class GoogleTranslationProvider
        Implements ITranslationProvider

        Private Shared ReadOnly _sharedInstance As New GoogleTranslationProvider()
        Public Shared ReadOnly Property Instance As GoogleTranslationProvider
            Get
                Return _sharedInstance
            End Get
        End Property

        Private ReadOnly _httpClient As HttpClient

        Public Sub New(Optional httpClient As HttpClient = Nothing)
            _httpClient = If(httpClient, New HttpClient())
        End Sub

        Public ReadOnly Property Id As String Implements ITranslationProvider.Id
            Get
                Return "google"
            End Get
        End Property

        Public ReadOnly Property DisplayName As String Implements ITranslationProvider.DisplayName
            Get
                Return "Google Translate (Cloud)"
            End Get
        End Property

        Public ReadOnly Property IsSelfHosted As Boolean Implements ITranslationProvider.IsSelfHosted
            Get
                Return False
            End Get
        End Property

        Public Async Function TranslateSingleAsync(text As String, targetLang As String, cancellationToken As CancellationToken) As Task(Of TranslationResult) Implements ITranslationProvider.TranslateSingleAsync
            If String.IsNullOrEmpty(text) Then Return TranslationResult.Succeeded(text)

            Try
                Dim encodedText = Uri.EscapeDataString(text)
                Dim url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={Uri.EscapeDataString(targetLang)}&dt=t&q={encodedText}"

                Using req As New HttpRequestMessage(HttpMethod.Get, url)
                    Using response = Await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                        If Not response.IsSuccessStatusCode Then
                            Return TranslationResult.Failed($"Google Translate HTTP {(CInt(response.StatusCode))}", text)
                        End If

                        Dim jsonString = Await response.Content.ReadAsStringAsync(cancellationToken)
                        Using doc As JsonDocument = JsonDocument.Parse(jsonString)
                            Dim root = doc.RootElement
                            If root.ValueKind = JsonValueKind.Array AndAlso root.GetArrayLength() > 0 Then
                                Dim firstArray = root(0)
                                If firstArray.ValueKind = JsonValueKind.Array Then
                                    Dim sb As New StringBuilder()
                                    For Each part In firstArray.EnumerateArray()
                                        If part.ValueKind = JsonValueKind.Array AndAlso part.GetArrayLength() > 0 Then
                                            sb.Append(part(0).GetString())
                                        End If
                                    Next
                                    Return TranslationResult.Succeeded(sb.ToString())
                                End If
                            End If
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                Return TranslationResult.Failed($"Google Translate error: {ex.Message}", text)
            End Try

            Return TranslationResult.Failed("Formato risposta Google Translate non riconosciuto", text)
        End Function

        Public Async Function TranslateBatchAsync(texts As List(Of String), targetLang As String, cancellationToken As CancellationToken) As Task(Of TranslationBatchResult) Implements ITranslationProvider.TranslateBatchAsync
            If texts Is Nothing OrElse texts.Count = 0 Then
                Return TranslationBatchResult.Succeeded(New List(Of String)())
            End If

            Const separator As String = vbLf & "###" & vbLf
            Dim results As New List(Of String)(New String(texts.Count - 1) {})

            ' Suddivisione in chunk per evitare URL superiori a ~1800 caratteri (errore HTTP 414)
            Dim currentBatch As New List(Of String)()
            Dim currentBatchIndices As New List(Of Integer)()
            Dim currentLength As Integer = 0

            For i As Integer = 0 To texts.Count - 1
                Dim item = texts(i)
                Dim itemLen = If(item IsNot Nothing, item.Length, 0)
                If currentBatch.Count > 0 AndAlso (currentLength + itemLen > 1500 OrElse currentBatch.Count >= 25) Then
                    Await TranslateBatchChunkAsync(currentBatch, currentBatchIndices, results, targetLang, separator, cancellationToken)
                    currentBatch.Clear()
                    currentBatchIndices.Clear()
                    currentLength = 0
                End If
                currentBatch.Add(item)
                currentBatchIndices.Add(i)
                currentLength += itemLen
            Next

            If currentBatch.Count > 0 Then
                Await TranslateBatchChunkAsync(currentBatch, currentBatchIndices, results, targetLang, separator, cancellationToken)
            End If

            Return TranslationBatchResult.Succeeded(results)
        End Function

        Private Async Function TranslateBatchChunkAsync(batchTexts As List(Of String), batchIndices As List(Of Integer), allResults As List(Of String), targetLang As String, separator As String, cancellationToken As CancellationToken) As Task
            Dim combined = String.Join(separator, batchTexts)
            Dim res = Await TranslateSingleAsync(combined, targetLang, cancellationToken)
            If res.Success AndAlso Not String.IsNullOrEmpty(res.TranslatedText) Then
                Dim parts = res.TranslatedText.Split(New String() {separator, vbLf & " ###" & vbLf, vbLf & "### " & vbLf, "###"}, StringSplitOptions.None)
                If parts.Length = batchTexts.Count Then
                    For j As Integer = 0 To batchIndices.Count - 1
                        allResults(batchIndices(j)) = parts(j).Trim()
                    Next
                    Return
                End If
            End If

            ' Fallback sicuro: se lo split non corrisponde o fallisce, traduce i singoli elementi del chunk
            For j As Integer = 0 To batchIndices.Count - 1
                Dim singleRes = Await TranslateSingleAsync(batchTexts(j), targetLang, cancellationToken)
                allResults(batchIndices(j)) = If(singleRes.Success, singleRes.TranslatedText, batchTexts(j))
            Next
        End Function

        Public Async Function TestConnectionAsync(cancellationToken As CancellationToken) As Task(Of TranslationTestResult) Implements ITranslationProvider.TestConnectionAsync
            Dim sw = Stopwatch.StartNew()
            Try
                Dim res = Await TranslateSingleAsync("Hello", "es", cancellationToken)
                sw.Stop()
                If res.Success AndAlso Not String.IsNullOrWhiteSpace(res.TranslatedText) Then
                    Return TranslationTestResult.Succeeded(sw.ElapsedMilliseconds, res.TranslatedText)
                Else
                    Return TranslationTestResult.Failed(res.ErrorMessage)
                End If
            Catch ex As Exception
                sw.Stop()
                Return TranslationTestResult.Failed($"Google Translate unreachable: {ex.Message}")
            End Try
        End Function
    End Class

    ''' <summary>
    ''' Provider per server LibreTranslate self-hosted o locale (porte tipiche 5000, endpoint /translate).
    ''' </summary>
    Public Class LibreTranslateProvider
        Implements ITranslationProvider

        Public Property ApiUrl As String
        Public Property ApiKey As String
        Private ReadOnly _httpClient As HttpClient

        Public Sub New(Optional apiUrl As String = "http://localhost:5000", Optional apiKey As String = "", Optional httpClient As HttpClient = Nothing)
            Me.ApiUrl = If(Not String.IsNullOrWhiteSpace(apiUrl), apiUrl.Trim(), "http://localhost:5000")
            Me.ApiKey = If(apiKey, "").Trim()
            _httpClient = If(httpClient, New HttpClient())
        End Sub

        Public ReadOnly Property Id As String Implements ITranslationProvider.Id
            Get
                Return "libretranslate"
            End Get
        End Property

        Public ReadOnly Property DisplayName As String Implements ITranslationProvider.DisplayName
            Get
                Return "LibreTranslate (Self-Hosted)"
            End Get
        End Property

        Public ReadOnly Property IsSelfHosted As Boolean Implements ITranslationProvider.IsSelfHosted
            Get
                Return True
            End Get
        End Property

        Private Function NormalizeEndpoint() As String
            Dim base = ApiUrl.TrimEnd("/"c)
            If Not base.EndsWith("/translate", StringComparison.OrdinalIgnoreCase) Then
                base &= "/translate"
            End If
            Return base
        End Function

        Public Async Function TranslateSingleAsync(text As String, targetLang As String, cancellationToken As CancellationToken) As Task(Of TranslationResult) Implements ITranslationProvider.TranslateSingleAsync
            If String.IsNullOrEmpty(text) Then Return TranslationResult.Succeeded(text)

            Dim endpoint = NormalizeEndpoint()
            Try
                Dim payload As New Dictionary(Of String, Object) From {
                    {"q", text},
                    {"source", "auto"},
                    {"target", targetLang.ToLowerInvariant()},
                    {"format", "text"}
                }
                If Not String.IsNullOrEmpty(ApiKey) Then
                    payload("api_key") = ApiKey
                End If

                Dim jsonBody = JsonSerializer.Serialize(payload)
                Using content As New StringContent(jsonBody, Encoding.UTF8, "application/json")
                    Using response = Await _httpClient.PostAsync(endpoint, content, cancellationToken)
                        Dim respText = Await response.Content.ReadAsStringAsync(cancellationToken)
                        If Not response.IsSuccessStatusCode Then
                            Return TranslationResult.Failed($"LibreTranslate HTTP {(CInt(response.StatusCode))}: {respText}", text)
                        End If

                        Using doc = JsonDocument.Parse(respText)
                            Dim root = doc.RootElement
                            If root.TryGetProperty("translatedText", Nothing) Then
                                Dim tt = root.GetProperty("translatedText")
                                If tt.ValueKind = JsonValueKind.String Then
                                    Return TranslationResult.Succeeded(tt.GetString())
                                End If
                            End If
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                Return TranslationResult.Failed($"LibreTranslate unreachable ({endpoint}): {ex.Message}", text)
            End Try

            Return TranslationResult.Failed("Risposta LibreTranslate non valida", text)
        End Function

        Public Async Function TranslateBatchAsync(texts As List(Of String), targetLang As String, cancellationToken As CancellationToken) As Task(Of TranslationBatchResult) Implements ITranslationProvider.TranslateBatchAsync
            If texts Is Nothing OrElse texts.Count = 0 Then
                Return TranslationBatchResult.Succeeded(New List(Of String)())
            End If

            Dim endpoint = NormalizeEndpoint()
            Try
                Dim payload As New Dictionary(Of String, Object) From {
                    {"q", texts},
                    {"source", "auto"},
                    {"target", targetLang.ToLowerInvariant()},
                    {"format", "text"}
                }
                If Not String.IsNullOrEmpty(ApiKey) Then
                    payload("api_key") = ApiKey
                End If

                Dim jsonBody = JsonSerializer.Serialize(payload)
                Using content As New StringContent(jsonBody, Encoding.UTF8, "application/json")
                    Using response = Await _httpClient.PostAsync(endpoint, content, cancellationToken)
                        If response.IsSuccessStatusCode Then
                            Dim respText = Await response.Content.ReadAsStringAsync(cancellationToken)
                            Using doc = JsonDocument.Parse(respText)
                                Dim root = doc.RootElement
                                If root.TryGetProperty("translatedText", Nothing) Then
                                    Dim tt = root.GetProperty("translatedText")
                                    If tt.ValueKind = JsonValueKind.Array Then
                                        Dim list As New List(Of String)()
                                        For Each item In tt.EnumerateArray()
                                            list.Add(item.GetString())
                                        Next
                                        If list.Count = texts.Count Then
                                            Return TranslationBatchResult.Succeeded(list)
                                        End If
                                    End If
                                End If
                            End Using
                        End If
                    End Using
                End Using
            Catch ex As Exception
                ' Se la chiamata batch fallisce, fallback sequenziale
                Debug.WriteLine($"[LibreTranslate] Batch call failed: {ex.Message}, falling back to sequential.")
            End Try

            ' Fallback sequenziale
            Dim results As New List(Of String)()
            For Each t In texts
                Dim singleRes = Await TranslateSingleAsync(t, targetLang, cancellationToken)
                results.Add(If(singleRes.Success, singleRes.TranslatedText, t))
            Next
            Return TranslationBatchResult.Succeeded(results)
        End Function

        Public Async Function TestConnectionAsync(cancellationToken As CancellationToken) As Task(Of TranslationTestResult) Implements ITranslationProvider.TestConnectionAsync
            Dim sw = Stopwatch.StartNew()
            Try
                Dim res = Await TranslateSingleAsync("Hello", "es", cancellationToken)
                sw.Stop()
                If res.Success Then
                    Return TranslationTestResult.Succeeded(sw.ElapsedMilliseconds, res.TranslatedText)
                Else
                    Return TranslationTestResult.Failed(res.ErrorMessage)
                End If
            Catch ex As Exception
                sw.Stop()
                Return TranslationTestResult.Failed($"LibreTranslate unreachable: {ex.Message}")
            End Try
        End Function
    End Class

    ''' <summary>
    ''' Provider per istanza Ollama locale o su rete privata (default port 11434, endpoint /api/generate).
    ''' </summary>
    Public Class OllamaTranslationProvider
        Implements ITranslationProvider

        Public Property ApiUrl As String
        Public Property Model As String
        Private ReadOnly _httpClient As HttpClient

        Public Sub New(Optional apiUrl As String = "http://localhost:11434", Optional model As String = "llama3.2", Optional httpClient As HttpClient = Nothing)
            Me.ApiUrl = If(Not String.IsNullOrWhiteSpace(apiUrl), apiUrl.Trim(), "http://localhost:11434")
            Me.Model = If(Not String.IsNullOrWhiteSpace(model), model.Trim(), "llama3.2")
            _httpClient = If(httpClient, New HttpClient())
        End Sub

        Public ReadOnly Property Id As String Implements ITranslationProvider.Id
            Get
                Return "ollama"
            End Get
        End Property

        Public ReadOnly Property DisplayName As String Implements ITranslationProvider.DisplayName
            Get
                Return "Ollama (Local LLM)"
            End Get
        End Property

        Public ReadOnly Property IsSelfHosted As Boolean Implements ITranslationProvider.IsSelfHosted
            Get
                Return True
            End Get
        End Property

        Private Function NormalizeEndpoint() As String
            Dim base = ApiUrl.TrimEnd("/"c)
            If Not base.EndsWith("/api/generate", StringComparison.OrdinalIgnoreCase) Then
                base &= "/api/generate"
            End If
            Return base
        End Function

        Public Async Function TranslateSingleAsync(text As String, targetLang As String, cancellationToken As CancellationToken) As Task(Of TranslationResult) Implements ITranslationProvider.TranslateSingleAsync
            If String.IsNullOrEmpty(text) Then Return TranslationResult.Succeeded(text)

            Dim endpoint = NormalizeEndpoint()
            Try
                Dim promptText = $"Translate the following text into target language code '{targetLang}'. Output ONLY the raw translated text with NO explanations, NO introductory phrases, and NO surrounding quotes:\n\n{text}"
                Dim payload As New Dictionary(Of String, Object) From {
                    {"model", Model},
                    {"prompt", promptText},
                    {"stream", False},
                    {"options", New Dictionary(Of String, Object) From {
                        {"temperature", 0.1}
                    }}
                }

                Dim jsonBody = JsonSerializer.Serialize(payload)
                Using content As New StringContent(jsonBody, Encoding.UTF8, "application/json")
                    Using response = Await _httpClient.PostAsync(endpoint, content, cancellationToken)
                        Dim respText = Await response.Content.ReadAsStringAsync(cancellationToken)
                        If Not response.IsSuccessStatusCode Then
                            Return TranslationResult.Failed($"Ollama HTTP {(CInt(response.StatusCode))}: {respText}", text)
                        End If

                        Using doc = JsonDocument.Parse(respText)
                            Dim root = doc.RootElement
                            If root.TryGetProperty("response", Nothing) Then
                                Dim rawTrans = root.GetProperty("response").GetString()
                                If rawTrans IsNot Nothing Then
                                    Dim cleanTrans = CleanLlmOutput(rawTrans, text)
                                    Return TranslationResult.Succeeded(cleanTrans)
                                End If
                            End If
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                Return TranslationResult.Failed($"Ollama unreachable ({endpoint}): {ex.Message}", text)
            End Try

            Return TranslationResult.Failed("Risposta Ollama non valida", text)
        End Function

        Private Shared Function CleanLlmOutput(raw As String, original As String) As String
            Dim s = raw.Trim()
            If s.StartsWith("```") Then
                Dim lines = s.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.None)
                If lines.Length >= 3 Then
                    s = String.Join(vbLf, lines, 1, lines.Length - 2).Trim()
                End If
            End If
            If (s.StartsWith(""""c) AndAlso s.EndsWith(""""c)) AndAlso Not (original.StartsWith(""""c) AndAlso original.EndsWith(""""c)) Then
                s = s.Substring(1, s.Length - 2).Trim()
            End If
            Return s
        End Function

        Public Async Function TranslateBatchAsync(texts As List(Of String), targetLang As String, cancellationToken As CancellationToken) As Task(Of TranslationBatchResult) Implements ITranslationProvider.TranslateBatchAsync
            If texts Is Nothing OrElse texts.Count = 0 Then
                Return TranslationBatchResult.Succeeded(New List(Of String)())
            End If

            Dim results As New List(Of String)()
            For Each item In texts
                Dim res = Await TranslateSingleAsync(item, targetLang, cancellationToken)
                results.Add(If(res.Success, res.TranslatedText, item))
            Next
            Return TranslationBatchResult.Succeeded(results)
        End Function

        Public Async Function TestConnectionAsync(cancellationToken As CancellationToken) As Task(Of TranslationTestResult) Implements ITranslationProvider.TestConnectionAsync
            Dim sw = Stopwatch.StartNew()
            Dim baseRoot = ApiUrl.TrimEnd("/"c)
            If baseRoot.EndsWith("/api/generate", StringComparison.OrdinalIgnoreCase) Then
                baseRoot = baseRoot.Substring(0, baseRoot.Length - "/api/generate".Length).TrimEnd("/"c)
            End If
            Dim tagsUrl = $"{baseRoot}/api/tags"

            Try
                Using response = Await _httpClient.GetAsync(tagsUrl, cancellationToken)
                    sw.Stop()
                    If response.IsSuccessStatusCode Then
                        Return TranslationTestResult.Succeeded(sw.ElapsedMilliseconds, $"Ollama online ({Model})")
                    Else
                        Return TranslationTestResult.Failed($"Ollama HTTP {(CInt(response.StatusCode))}")
                    End If
                End Using
            Catch ex As Exception
                sw.Stop()
                Return TranslationTestResult.Failed($"Ollama unreachable ({tagsUrl}): {ex.Message}")
            End Try
        End Function
    End Class

    ''' <summary>
    ''' Provider per endpoint compatibile OpenAI (LM Studio, LocalAI, vLLM, OpenAI API, Groq, ecc.).
    ''' </summary>
    Public Class OpenAiTranslationProvider
        Implements ITranslationProvider

        Public Property ApiUrl As String
        Public Property ApiKey As String
        Public Property Model As String
        Private ReadOnly _httpClient As HttpClient

        Public Sub New(Optional apiUrl As String = "http://localhost:1234/v1", Optional apiKey As String = "", Optional model As String = "gpt-4o-mini", Optional httpClient As HttpClient = Nothing)
            Me.ApiUrl = If(Not String.IsNullOrWhiteSpace(apiUrl), apiUrl.Trim(), "http://localhost:1234/v1")
            Me.ApiKey = If(apiKey, "").Trim()
            Me.Model = If(Not String.IsNullOrWhiteSpace(model), model.Trim(), "gpt-4o-mini")
            _httpClient = If(httpClient, New HttpClient())
        End Sub

        Public ReadOnly Property Id As String Implements ITranslationProvider.Id
            Get
                Return "openai"
            End Get
        End Property

        Public ReadOnly Property DisplayName As String Implements ITranslationProvider.DisplayName
            Get
                Return "OpenAI-Compatible (Custom Endpoint / Local)"
            End Get
        End Property

        Public ReadOnly Property IsSelfHosted As Boolean Implements ITranslationProvider.IsSelfHosted
            Get
                Dim uriObj As Uri = Nothing
                If Uri.TryCreate(ApiUrl, UriKind.Absolute, uriObj) Then
                    Return uriObj.IsLoopback OrElse uriObj.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                End If
                Return False
            End Get
        End Property

        Private Function NormalizeChatEndpoint() As String
            Dim base = ApiUrl.TrimEnd("/"c)
            If base.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) Then
                Return base
            ElseIf base.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) Then
                Return base & "/chat/completions"
            ElseIf Not base.Contains("/v1") Then
                Return base & "/v1/chat/completions"
            Else
                Return base & "/chat/completions"
            End If
        End Function

        Public Async Function TranslateSingleAsync(text As String, targetLang As String, cancellationToken As CancellationToken) As Task(Of TranslationResult) Implements ITranslationProvider.TranslateSingleAsync
            If String.IsNullOrEmpty(text) Then Return TranslationResult.Succeeded(text)

            Dim endpoint = NormalizeChatEndpoint()
            Try
                Dim payload As New Dictionary(Of String, Object) From {
                    {"model", Model},
                    {"messages", New Object() {
                        New Dictionary(Of String, String) From {
                            {"role", "system"},
                            {"content", $"You are a translation engine. Translate the user text into target language code '{targetLang}'. Output ONLY the raw direct translation without greetings, quotes, or markdown explanations."}
                        },
                        New Dictionary(Of String, String) From {
                            {"role", "user"},
                            {"content", text}
                        }
                    }},
                    {"temperature", 0.1}
                }

                Dim jsonBody = JsonSerializer.Serialize(payload)
                Using req As New HttpRequestMessage(HttpMethod.Post, endpoint)
                    req.Content = New StringContent(jsonBody, Encoding.UTF8, "application/json")
                    If Not String.IsNullOrEmpty(ApiKey) Then
                        req.Headers.Authorization = New AuthenticationHeaderValue("Bearer", ApiKey)
                    End If

                    Using response = Await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                        Dim respText = Await response.Content.ReadAsStringAsync(cancellationToken)
                        If Not response.IsSuccessStatusCode Then
                            Return TranslationResult.Failed($"OpenAI API HTTP {(CInt(response.StatusCode))}: {respText}", text)
                        End If

                        Using doc = JsonDocument.Parse(respText)
                            Dim root = doc.RootElement
                            If root.TryGetProperty("choices", Nothing) Then
                                Dim choices = root.GetProperty("choices")
                                If choices.ValueKind = JsonValueKind.Array AndAlso choices.GetArrayLength() > 0 Then
                                    Dim firstChoice = choices(0)
                                    If firstChoice.TryGetProperty("message", Nothing) Then
                                        Dim msg = firstChoice.GetProperty("message")
                                        If msg.TryGetProperty("content", Nothing) Then
                                            Dim rawTrans = msg.GetProperty("content").GetString()
                                            If rawTrans IsNot Nothing Then
                                                Dim clean = rawTrans.Trim()
                                                If (clean.StartsWith(""""c) AndAlso clean.EndsWith(""""c)) AndAlso Not (text.StartsWith(""""c) AndAlso text.EndsWith(""""c)) Then
                                                    clean = clean.Substring(1, clean.Length - 2).Trim()
                                                End If
                                                Return TranslationResult.Succeeded(clean)
                                            End If
                                        End If
                                    End If
                                End If
                            End If
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                Return TranslationResult.Failed($"OpenAI API unreachable ({endpoint}): {ex.Message}", text)
            End Try

            Return TranslationResult.Failed("Risposta OpenAI API non valida", text)
        End Function

        Public Async Function TranslateBatchAsync(texts As List(Of String), targetLang As String, cancellationToken As CancellationToken) As Task(Of TranslationBatchResult) Implements ITranslationProvider.TranslateBatchAsync
            If texts Is Nothing OrElse texts.Count = 0 Then
                Return TranslationBatchResult.Succeeded(New List(Of String)())
            End If

            Dim results As New List(Of String)()
            For Each item In texts
                Dim res = Await TranslateSingleAsync(item, targetLang, cancellationToken)
                results.Add(If(res.Success, res.TranslatedText, item))
            Next
            Return TranslationBatchResult.Succeeded(results)
        End Function

        Public Async Function TestConnectionAsync(cancellationToken As CancellationToken) As Task(Of TranslationTestResult) Implements ITranslationProvider.TestConnectionAsync
            Dim sw = Stopwatch.StartNew()
            Dim baseRoot = ApiUrl.TrimEnd("/"c)
            If baseRoot.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) Then
                baseRoot = baseRoot.Substring(0, baseRoot.Length - "/chat/completions".Length).TrimEnd("/"c)
            End If
            Dim modelsUrl = $"{baseRoot}/models"

            Try
                Using req As New HttpRequestMessage(HttpMethod.Get, modelsUrl)
                    If Not String.IsNullOrEmpty(ApiKey) Then
                        req.Headers.Authorization = New AuthenticationHeaderValue("Bearer", ApiKey)
                    End If
                    Using response = Await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                        sw.Stop()
                        If response.IsSuccessStatusCode Then
                            Return TranslationTestResult.Succeeded(sw.ElapsedMilliseconds, $"OpenAI endpoint online ({Model})")
                        Else
                            ' Fallback: prova traduzione di test se /models non è implementato (es. vLLM)
                            Dim testRes = Await TranslateSingleAsync("Hi", "es", cancellationToken)
                            If testRes.Success Then
                                Return TranslationTestResult.Succeeded(sw.ElapsedMilliseconds, $"OpenAI endpoint online ({Model})")
                            End If
                            Return TranslationTestResult.Failed($"HTTP {(CInt(response.StatusCode))}")
                        End If
                    End Using
                End Using
            Catch ex As Exception
                sw.Stop()
                Return TranslationTestResult.Failed($"OpenAI endpoint unreachable: {ex.Message}")
            End Try
        End Function
    End Class

    ''' <summary>
    ''' Modello dati di configurazione per la traduzione e la privacy (TODO #69).
    ''' </summary>
    Public Class TranslationConfig
        Public Property ProviderId As String = "google"
        Public Property PrivacyConsentAccepted As Boolean = False
        Public Property LibreTranslateUrl As String = "http://localhost:5000"
        Public Property LibreTranslateApiKey As String = ""
        Public Property OllamaUrl As String = "http://localhost:11434"
        Public Property OllamaModel As String = "llama3.2"
        Public Property OpenAiUrl As String = "http://localhost:1234/v1"
        Public Property OpenAiApiKey As String = ""
        Public Property OpenAiModel As String = "gpt-4o-mini"
    End Class

    ''' <summary>
    ''' Factory e registro per la risoluzione dinamica dei provider di traduzione.
    ''' </summary>
    Public Module TranslationProviderRegistry
        Public Function CreateProvider(config As TranslationConfig, Optional httpClient As HttpClient = Nothing) As ITranslationProvider
            If config Is Nothing Then Return GoogleTranslationProvider.Instance

            Select Case (If(config.ProviderId, "google")).ToLowerInvariant()
                Case "libretranslate"
                    Return New LibreTranslateProvider(config.LibreTranslateUrl, config.LibreTranslateApiKey, httpClient)
                Case "ollama"
                    Return New OllamaTranslationProvider(config.OllamaUrl, config.OllamaModel, httpClient)
                Case "openai"
                    Return New OpenAiTranslationProvider(config.OpenAiUrl, config.OpenAiApiKey, config.OpenAiModel, httpClient)
                Case Else
                    Return New GoogleTranslationProvider(httpClient)
            End Select
        End Function

        Public Function GetProviderDisplayName(providerId As String) As String
            Select Case (If(providerId, "google")).ToLowerInvariant()
                Case "libretranslate"
                    Return "LibreTranslate (Self-Hosted)"
                Case "ollama"
                    Return "Ollama (Local LLM)"
                Case "openai"
                    Return "OpenAI-Compatible / Custom Endpoint"
                Case Else
                    Return "Google Translate (Cloud)"
            End Select
        End Function
    End Module
