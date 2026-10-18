Imports System

''' <summary>
''' Politica di sicurezza e validazione per l'autorizzazione delle richieste HTTP/WebSocket verso proxy locali o endpoint gateway (TODO #78).
''' Assicura che token di sicurezza (X-HidaChat-Local-Token) e credenziali account (Bearer token) vengano iniettati
''' esclusivamente nelle richieste indirizzate alla porta e all'host esatti del servizio autorizzato,
''' impedendo l'invio indebito di credenziali a servizi terzi o porte non pertinenti su localhost/127.0.0.1.
''' </summary>
Public Module LocalAuthPolicy

    ''' <summary>
    ''' Valida se l'URI della richiesta corrisponde rigorosamente a schema, host e porta attesi.
    ''' </summary>
    ''' <param name="requestUriStr">URI completo della richiesta intercettata.</param>
    ''' <param name="expectedScheme">Schema atteso (es. "http" o "https").</param>
    ''' <param name="expectedHost">Host atteso (es. "127.0.0.1" o host del gateway).</param>
    ''' <param name="expectedPort">Porta TCP numerica attesa.</param>
    ''' <returns>True se schema, host e porta corrispondono esattamente; False in qualsiasi altro caso.</returns>
    Public Function ShouldAttachCredentials(
        requestUriStr As String,
        expectedScheme As String,
        expectedHost As String,
        expectedPort As Integer
    ) As Boolean
        If String.IsNullOrWhiteSpace(requestUriStr) Then Return False
        If String.IsNullOrWhiteSpace(expectedScheme) OrElse String.IsNullOrWhiteSpace(expectedHost) OrElse expectedPort <= 0 Then Return False

        Dim reqUri As Uri = Nothing
        If Not Uri.TryCreate(requestUriStr, UriKind.Absolute, reqUri) Then Return False

        If Not reqUri.Scheme.Equals(expectedScheme, StringComparison.OrdinalIgnoreCase) Then Return False
        If Not reqUri.Host.Equals(expectedHost, StringComparison.OrdinalIgnoreCase) Then Return False
        If reqUri.Port <> expectedPort Then Return False

        Return True
    End Function

    ''' <summary>
    ''' Costruisce il pattern del filtro WebView2 (AddWebResourceRequestedFilter) limitato
    ''' in modo puntuale a schema, host e porta di destinazione.
    ''' </summary>
    Public Function BuildFilterPattern(scheme As String, host As String, port As Integer) As String
        If String.IsNullOrWhiteSpace(scheme) OrElse String.IsNullOrWhiteSpace(host) OrElse port <= 0 Then Return Nothing
        Dim normScheme = scheme.ToLowerInvariant()
        Dim isStandardPort = (normScheme = "http" AndAlso port = 80) OrElse (normScheme = "https" AndAlso port = 443)
        If isStandardPort Then
            Return $"{normScheme}://{host}/*"
        Else
            Return $"{normScheme}://{host}:{port}/*"
        End If
    End Function

End Module
