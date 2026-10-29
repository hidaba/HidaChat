Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.RegularExpressions

''' <summary>
''' Politiche e utilità per la gestione isolata dei profili nativi di CoreWebView2 sotto un unico Environment condiviso (TODO #103).
''' Consente la condivisione di una sola cartella dati base (data/webview) e di un singolo processo browser Chromium,
''' separando cookie, IndexedDB e LocalStorage tramite CoreWebView2ControllerOptions.ProfileName.
''' </summary>
Public Module ProfileIsolationPolicy

    Public Const MaxProfileNameLength As Integer = 64
    Public Const DefaultProfileName As String = "Account_Default"
    Public Const SharedEbWebViewFolder As String = "EBWebView"
    Public Const LegacyProfilePrefix As String = "WV2Profile_"

    ''' <summary>
    ''' Costruisce e sanitizza un nome profilo univoco, valido e conforme alle restrizioni di CoreWebView2
    ''' (lunghezza massima 64 caratteri, caratteri consentiti [a-zA-Z0-9_\-\.]).
    ''' </summary>
    Public Function GetSafeProfileName(accountId As String) As String
        If String.IsNullOrWhiteSpace(accountId) Then Return DefaultProfileName

        Dim cleanId = accountId.Trim(" "c, "."c)
        If String.IsNullOrEmpty(cleanId) Then Return DefaultProfileName

        Dim rawName As String
        If cleanId.StartsWith("account_", StringComparison.OrdinalIgnoreCase) Then
            rawName = "Account_" & cleanId.Substring("account_".Length)
        ElseIf cleanId.StartsWith("Account_", StringComparison.OrdinalIgnoreCase) Then
            rawName = cleanId
        Else
            rawName = "Account_" & cleanId
        End If

        ' Caratteri ammessi da WebView2: a-z, A-Z, 0-9, #, @, $, (, ), +, -, _, ~, . e spazio
        ' Per massima sicurezza sui percorsi filesystem Windows usiamo [a-zA-Z0-9_\-\.]
        Dim safeName = Regex.Replace(rawName, "[^a-zA-Z0-9_\-\.]", "_")
        If safeName.Length > MaxProfileNameLength Then
            safeName = safeName.Substring(0, MaxProfileNameLength)
        End If
        safeName = safeName.TrimEnd("."c, " "c)
        If String.IsNullOrEmpty(safeName) Then
            safeName = DefaultProfileName
        End If
        Return safeName
    End Function

    ''' <summary>
    ''' Restituisce il percorso relativo della cartella del profilo nativo all'interno della cartella dati condivisa (es. "EBWebView\Account_1").
    ''' </summary>
    Public Function GetNativeProfileRelativePath(accountId As String) As String
        Dim safeName = GetSafeProfileName(accountId)
        Return Path.Combine(SharedEbWebViewFolder, safeName)
    End Function

    ''' <summary>
    ''' Restituisce il percorso assoluto della cartella del profilo nativo su disco.
    ''' </summary>
    Public Function GetNativeProfileDirectory(baseSharedDir As String, accountId As String) As String
        If String.IsNullOrWhiteSpace(baseSharedDir) Then Return ""
        Return Path.Combine(baseSharedDir, GetNativeProfileRelativePath(accountId))
    End Function

    ''' <summary>
    ''' Restituisce il percorso legacy della cartella del profilo (es. "data/webview/WV2Profile_{id}").
    ''' </summary>
    Public Function GetLegacyProfileDirectory(baseSharedDir As String, accountId As String) As String
        If String.IsNullOrWhiteSpace(baseSharedDir) OrElse String.IsNullOrWhiteSpace(accountId) Then Return ""
        Return Path.Combine(baseSharedDir, $"{LegacyProfilePrefix}{accountId}")
    End Function

    ''' <summary>
    ''' Determina il percorso effettivo del profilo: restituisce il percorso nativo (se presente o se non vi sono dati legacy),
    ''' oppure il percorso legacy preesistente se non ancora migrato.
    ''' </summary>
    Public Function ResolveEffectiveProfileDirectory(baseSharedDir As String, accountId As String) As String
        If String.IsNullOrWhiteSpace(baseSharedDir) Then Return ""
        Dim nativeDir = GetNativeProfileDirectory(baseSharedDir, accountId)
        If Directory.Exists(nativeDir) Then Return nativeDir
        Dim legacyDir = GetLegacyProfileDirectory(baseSharedDir, accountId)
        If Directory.Exists(legacyDir) Then Return legacyDir
        Return nativeDir
    End Function

    ''' <summary>
    ''' Individua la cartella IndexedDB sia per profili nativi che legacy.
    ''' </summary>
    Public Function ResolveIndexedDbDirectory(profileDir As String) As String
        If String.IsNullOrWhiteSpace(profileDir) Then Return ""
        Dim direct = Path.Combine(profileDir, "IndexedDB")
        If Directory.Exists(direct) Then Return direct
        Dim ebDefault = Path.Combine(profileDir, SharedEbWebViewFolder, "Default", "IndexedDB")
        If Directory.Exists(ebDefault) Then Return ebDefault
        Dim def = Path.Combine(profileDir, "Default", "IndexedDB")
        If Directory.Exists(def) Then Return def
        Return direct
    End Function

    ''' <summary>
    ''' Individua la cartella Service Worker sia per profili nativi che legacy.
    ''' </summary>
    Public Function ResolveServiceWorkerDirectory(profileDir As String) As String
        If String.IsNullOrWhiteSpace(profileDir) Then Return ""
        Dim direct = Path.Combine(profileDir, "Service Worker")
        If Directory.Exists(direct) Then Return direct
        Dim ebDefault = Path.Combine(profileDir, SharedEbWebViewFolder, "Default", "Service Worker")
        If Directory.Exists(ebDefault) Then Return ebDefault
        Dim def = Path.Combine(profileDir, "Default", "Service Worker")
        If Directory.Exists(def) Then Return def
        Return direct
    End Function

    ''' <summary>
    ''' Verifica se il nome di una cartella corrisponde a una directory di sistema o cache protetta di Chromium.
    ''' </summary>
    Public Function IsSystemOrProtectedDirectory(dirName As String) As Boolean
        If String.IsNullOrWhiteSpace(dirName) Then Return True
        Dim protectedNames = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            "Default", "Crashpad", "ShaderCache", "GrShaderCache", "component_crx_cache",
            "Subresource Filter", "GraphiteDawnCache", "DawnCache", "DawnGraphiteCache",
            "DawnWebGPUCache", "GPUCache", "GPUPersistentCache", "CertificateRevocation",
            "WidevineCdm", "Recovery", "BrowserMetrics", "Crowd Deny", "Origin Trials",
            "_trash", "_profile_locks"
        }
        Return protectedNames.Contains(dirName)
    End Function

    ''' <summary>
    ''' Verifica se il nome della directory appartiene a un profilo nativo gestito (inizia con "Account_").
    ''' </summary>
    Public Function IsNativeAccountProfileDirectory(dirName As String) As Boolean
        If String.IsNullOrWhiteSpace(dirName) Then Return False
        Return dirName.StartsWith("Account_", StringComparison.OrdinalIgnoreCase) AndAlso Not IsSystemOrProtectedDirectory(dirName)
    End Function

End Module
