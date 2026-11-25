Imports System
Imports System.IO
Imports System.Net
Imports System.Net.Http.Headers
Imports System.Net.Security
Imports System.Security.Authentication
Imports System.Security.Cryptography
Imports System.Security.Cryptography.X509Certificates
Imports System.Text
Imports System.Text.RegularExpressions

''' <summary>
''' Policy enforcing standard HTTPS certificate validation and secure TLS handling (TASK-0082).
''' Replaces insecure silent bypasses with standard chain validation, certificate pinning,
''' custom CA support, and precise error discrimination for TLS failures vs general reachability.
''' </summary>
Public Class TlsCertificatePolicy

    ''' <summary>
    ''' Computes the uppercase SHA-256 thumbprint (fingerprint) of a certificate without spaces or colons.
    ''' </summary>
    Public Shared Function GetThumbprintSha256(cert As X509Certificate) As String
        If cert Is Nothing Then Return String.Empty
        Dim raw = cert.GetRawCertData()
        If raw Is Nothing OrElse raw.Length = 0 Then Return String.Empty
        Using sha As SHA256 = SHA256.Create()
            Dim hash = sha.ComputeHash(raw)
            Dim sb As New StringBuilder(hash.Length * 2)
            For Each b In hash
                sb.Append(b.ToString("X2"))
            Next
            Return sb.ToString()
        End Using
    End Function

    ''' <summary>
    ''' Normalizes a certificate thumbprint by stripping colons, spaces, dashes, and converting to uppercase.
    ''' </summary>
    Public Shared Function NormalizeThumbprint(thumbprint As String) As String
        If String.IsNullOrWhiteSpace(thumbprint) Then Return String.Empty
        Return thumbprint.Replace(":", "").Replace(" ", "").Replace("-", "").Trim().ToUpperInvariant()
    End Function

    ''' <summary>
    ''' Validates a remote server certificate according to standard chain rules,
    ''' with optional certificate pinning, custom CA trust, or explicit bypass.
    ''' </summary>
    ''' <param name="cert">The remote peer certificate presented by the server.</param>
    ''' <param name="chain">The certificate chain constructed by the TLS stack.</param>
    ''' <param name="sslPolicyErrors">The SSL policy errors identified by the operating system.</param>
    ''' <param name="allowInsecure">If True, explicitly bypasses validation (insecure lab mode).</param>
    ''' <param name="pinnedThumbprint">Optional SHA-256 or SHA-1 fingerprint for certificate pinning.</param>
    ''' <param name="customCaPathOrPem">Optional file path or PEM data of a custom root CA certificate.</param>
    ''' <param name="failureReason">Outputs the human-readable technical cause if validation fails.</param>
    ''' <returns>True if the certificate is accepted; False otherwise.</returns>
    Public Shared Function ValidateCertificate(
        cert As X509Certificate,
        chain As X509Chain,
        sslPolicyErrors As SslPolicyErrors,
        allowInsecure As Boolean,
        pinnedThumbprint As String,
        customCaPathOrPem As String,
        ByRef failureReason As String
    ) As Boolean
        failureReason = Nothing

        ' 1. Explicit bypass requested by user (disabled by default)
        If allowInsecure Then
            Return True
        End If

        If cert Is Nothing Then
            failureReason = "No server certificate presented"
            Return False
        End If

        ' 2. Certificate Pinning check (takes precedence when configured)
        Dim cleanPinned = NormalizeThumbprint(pinnedThumbprint)
        If Not String.IsNullOrEmpty(cleanPinned) Then
            Dim sha256Thumb = GetThumbprintSha256(cert)
            Dim sha1Thumb = NormalizeThumbprint(cert.GetCertHashString())
            If String.Equals(cleanPinned, sha256Thumb, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(cleanPinned, sha1Thumb, StringComparison.OrdinalIgnoreCase) Then
                ' Pinned certificate matches perfectly
                Return True
            Else
                failureReason = $"Certificate pinning mismatch: expected {cleanPinned}, got {sha256Thumb}"
                Return False
            End If
        End If

        ' 3. Custom Root CA evaluation
        If Not String.IsNullOrWhiteSpace(customCaPathOrPem) Then
            Dim caCert = LoadCertificate(customCaPathOrPem)
            If caCert IsNot Nothing Then
                Try
                    Using customChain As New X509Chain()
                        customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck
                        customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust
                        customChain.ChainPolicy.CustomTrustStore.Add(caCert)
                        customChain.ChainPolicy.ExtraStore.Add(caCert)

                        Dim cert2 = If(TypeOf cert Is X509Certificate2, CType(cert, X509Certificate2), New X509Certificate2(cert))
                        Dim isValid = customChain.Build(cert2)
                        If isValid Then
                            ' Check for hostname mismatch even with custom CA
                            If (sslPolicyErrors And SslPolicyErrors.RemoteCertificateNameMismatch) <> 0 Then
                                failureReason = "Hostname mismatch: server certificate name does not match target host"
                                Return False
                            End If
                            Return True
                        End If
                    End Using
                Catch ex As Exception
                    ' Chain build failure with custom CA, fall through to standard error reporting
                End Try
            End If
        End If

        ' 4. Standard TLS validation
        If sslPolicyErrors = SslPolicyErrors.None Then
            Return True
        End If

        ' 5. Discriminate specific TLS error reason
        If (sslPolicyErrors And SslPolicyErrors.RemoteCertificateNameMismatch) <> 0 Then
            failureReason = "Hostname mismatch: certificate name does not match server hostname"
        ElseIf (sslPolicyErrors And SslPolicyErrors.RemoteCertificateNotAvailable) <> 0 Then
            failureReason = "Remote certificate not available"
        ElseIf (sslPolicyErrors And SslPolicyErrors.RemoteCertificateChainErrors) <> 0 Then
            Dim isExpired = False
            If chain IsNot Nothing AndAlso chain.ChainStatus IsNot Nothing Then
                For Each status In chain.ChainStatus
                    If (status.Status And X509ChainStatusFlags.NotTimeValid) <> 0 Then
                        isExpired = True
                        Exit For
                    End If
                Next
            End If

            If isExpired Then
                failureReason = "Certificate has expired or is not yet valid"
            Else
                failureReason = "Untrusted certificate authority: chain does not terminate in a trusted root"
            End If
        Else
            failureReason = $"TLS certificate validation failure: {sslPolicyErrors}"
        End If

        Return False
    End Function

    ''' <summary>
    ''' Attempts to load an X509 certificate from a file path or direct PEM string.
    ''' </summary>
    Public Shared Function LoadCertificate(pathOrPem As String) As X509Certificate2
        If String.IsNullOrWhiteSpace(pathOrPem) Then Return Nothing
        Dim clean = pathOrPem.Trim()
        Try
            If File.Exists(clean) Then
                Return X509CertificateLoader.LoadCertificateFromFile(clean)
            End If
            If clean.Contains("-----BEGIN CERTIFICATE-----") Then
                Dim bytes = Encoding.UTF8.GetBytes(clean)
                Return X509CertificateLoader.LoadCertificate(bytes)
            End If
        Catch ex As Exception
            ' Invalid or unparseable certificate
        End Try
        Return Nothing
    End Function

    ''' <summary>
    ''' Inspects an exception hierarchy to determine whether the failure was caused by
    ''' a TLS / SSL certificate verification error as opposed to general reachability / network failure.
    ''' </summary>
    ''' <param name="ex">The exception thrown during the HTTP request.</param>
    ''' <param name="tlsDetail">Outputs the extracted technical TLS detail if identified.</param>
    ''' <returns>True if the failure is specifically a TLS / SSL error; False otherwise.</returns>
    Public Shared Function IsTlsFailure(ex As Exception, ByRef tlsDetail As String) As Boolean
        tlsDetail = Nothing
        If ex Is Nothing Then Return False

        Dim cur As Exception = ex
        While cur IsNot Nothing
            ' 1. Type-level checks
            If TypeOf cur Is AuthenticationException Then
                tlsDetail = cur.Message
                Return True
            End If

            Dim msg = If(cur.Message, "")

            ' 2. Common .NET TLS / SSL error patterns
            If msg.IndexOf("The SSL connection could not be established", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               msg.IndexOf("The remote certificate is invalid", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               msg.IndexOf("RemoteCertificateValidationCallback", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               msg.IndexOf("SSL/TLS", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               msg.IndexOf("SEC_E_CERT_EXPIRED", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               msg.IndexOf("SEC_E_UNTRUSTED_ROOT", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               msg.IndexOf("certificate has expired", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               msg.IndexOf("untrusted root", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               msg.IndexOf("certificate pinning mismatch", StringComparison.OrdinalIgnoreCase) >= 0 Then
                tlsDetail = msg
                Return True
            End If

            cur = cur.InnerException
        End While

        Return False
    End Function

    ''' <summary>
    ''' Checks if an HTTP response from the local tsnet companion daemon indicates a downstream TLS error.
    ''' </summary>
    Public Shared Function IsTlsProxyResponse(headers As HttpResponseHeaders, statusCode As HttpStatusCode, ByRef tlsErrorDetail As String) As Boolean
        tlsErrorDetail = Nothing
        If headers Is Nothing Then Return False

        If headers.Contains("X-HidaChat-Error-Type") Then
            For Each headerValue In headers.GetValues("X-HidaChat-Error-Type")
                If String.Equals(headerValue, "TLS_CERTIFICATE_ERROR", StringComparison.OrdinalIgnoreCase) Then
                    If headers.Contains("X-HidaChat-TLS-Error") Then
                        tlsErrorDetail = String.Join("; ", headers.GetValues("X-HidaChat-TLS-Error"))
                    Else
                        tlsErrorDetail = "TLS certificate validation failed for destination server"
                    End If
                    Return True
                End If
            Next
        End If

        Return False
    End Function

End Class
