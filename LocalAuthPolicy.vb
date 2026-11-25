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

    ''' <summary>
    ''' Attempts to extract the expiration timestamp ("exp" claim in Unix epoch seconds)
    ''' from a JWT or base64-encoded authentication token (TASK-0126).
    ''' Supports standard three-part JWTs (header.payload.signature) and binary-signed single-payload tokens (e.g. Hermes tokens).
    ''' </summary>
    Public Function TryGetTokenExpiration(token As String) As Long?
        If String.IsNullOrWhiteSpace(token) OrElse token.Length < 10 Then Return Nothing

        Dim candidates As New System.Collections.Generic.List(Of String)()
        Dim parts = token.Split("."c)
        If parts.Length >= 2 Then
            candidates.Add(parts(1))
            candidates.Add(parts(0))
        End If
        candidates.Add(token)

        For Each candidate In candidates
            Dim padded = candidate.Replace("-"c, "+"c).Replace("_"c, "/"c)
            Dim remVal = padded.Length Mod 4
            If remVal = 2 Then
                padded &= "=="
            ElseIf remVal = 3 Then
                padded &= "="
            ElseIf remVal = 1 Then
                Continue For
            End If

            Try
                Dim rawBytes = Convert.FromBase64String(padded)
                Dim text = System.Text.Encoding.UTF8.GetString(rawBytes)
                Dim match = System.Text.RegularExpressions.Regex.Match(text, "\{[^{}]*""exp""\s*:\s*(\d+)[^{}]*\}")
                If match.Success Then
                    Dim expVal As Long
                    If Long.TryParse(match.Groups(1).Value, expVal) Then
                        Return expVal
                    End If
                End If
            Catch
            End Try
        Next

        Return Nothing
    End Function

    ''' <summary>
    ''' Determines whether an authentication token contains an expired "exp" timestamp (TASK-0126).
    ''' Returns True if an "exp" timestamp is present and is less than or equal to current UTC time plus skew.
    ''' Returns False if the token has no "exp" claim (opaque/permanent token) or is still valid.
    ''' </summary>
    Public Function IsTokenExpired(token As String, Optional skewSeconds As Integer = 0) As Boolean
        Dim exp = TryGetTokenExpiration(token)
        If Not exp.HasValue Then Return False
        Dim nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        Return exp.Value <= (nowUnix + skewSeconds)
    End Function

    ''' <summary>
    ''' Determines whether the request URI targets an authentication, sign-in, or session bootstrap endpoint
    ''' where pre-emptive injection of potentially invalid or expired Bearer credentials should be suppressed (TASK-0126).
    ''' </summary>
    Public Function IsAuthenticationEndpoint(requestUriStr As String) As Boolean
        If String.IsNullOrWhiteSpace(requestUriStr) Then Return False
        Dim uri As Uri = Nothing
        If Not Uri.TryCreate(requestUriStr, UriKind.Absolute, uri) Then Return False

        Dim path = uri.AbsolutePath.TrimEnd("/"c).ToLowerInvariant()
        If path = "/login" OrElse path.StartsWith("/login/", StringComparison.Ordinal) Then Return True
        If path = "/signin" OrElse path.StartsWith("/signin/", StringComparison.Ordinal) Then Return True
        If path = "/auth/password-login" OrElse path = "/auth/login" OrElse path = "/auth/token" OrElse path = "/auth/refresh" Then Return True

        Return False
    End Function

    ''' <summary>
    ''' Determines whether an account's Bearer AuthToken should be attached to the intercepted request (TASK-0126).
    ''' Suppresses injection if the token is empty, expired, or if the request targets an authentication/login endpoint.
    ''' </summary>
    Public Function ShouldAttachBearerToken(requestUriStr As String, authToken As String) As Boolean
        If String.IsNullOrWhiteSpace(authToken) Then Return False
        If IsAuthenticationEndpoint(requestUriStr) Then Return False
        If IsTokenExpired(authToken) Then Return False
        Return True
    End Function

End Module
