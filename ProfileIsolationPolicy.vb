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
    Public Const NativeProfileDirPrefix As String = "WV2Profile_"

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
    ''' Restituisce il nome della cartella su disco generata da Edge WebView2 per il profilo nativo sotto EBWebView.
    ''' WebView2 antepone internamente il prefisso "WV2Profile_" al ProfileName specificato.
    ''' </summary>
    Public Function GetNativeProfileFolderName(accountId As String) As String
        Dim safeName = GetSafeProfileName(accountId)
        If safeName.StartsWith(NativeProfileDirPrefix, StringComparison.OrdinalIgnoreCase) Then
            Return safeName.ToLowerInvariant()
        End If
        Return (NativeProfileDirPrefix & safeName).ToLowerInvariant()
    End Function

    ''' <summary>
    ''' Restituisce il percorso relativo della cartella del profilo nativo all'interno della cartella dati condivisa (es. "EBWebView\wv2profile_account_1").
    ''' </summary>
    Public Function GetNativeProfileRelativePath(accountId As String) As String
        Return Path.Combine(SharedEbWebViewFolder, GetNativeProfileFolderName(accountId))
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
    ''' verificando anche eventuali varianti o cartelle legacy preesistenti.
    ''' </summary>
    Public Function ResolveEffectiveProfileDirectory(baseSharedDir As String, accountId As String) As String
        If String.IsNullOrWhiteSpace(baseSharedDir) Then Return ""
        Dim nativeDir = GetNativeProfileDirectory(baseSharedDir, accountId)
        If Directory.Exists(nativeDir) Then Return nativeDir

        ' Controlla se esiste una cartella corrispondente con casing alternativo sotto EBWebView
        Dim ebDir = Path.Combine(baseSharedDir, SharedEbWebViewFolder)
        If Directory.Exists(ebDir) Then
            Dim targetFolder = GetNativeProfileFolderName(accountId)
            For Each subDir In Directory.GetDirectories(ebDir)
                If String.Equals(Path.GetFileName(subDir), targetFolder, StringComparison.OrdinalIgnoreCase) Then
                    Return subDir
                End If
            Next

            ' Compatibilità v1.3.5-beta (se cartella creata senza prefisso WV2Profile_, es. EBWebView\Account_1)
            Dim safeName = GetSafeProfileName(accountId)
            Dim betaDir = Path.Combine(ebDir, safeName)
            If Directory.Exists(betaDir) Then Return betaDir
        End If

        ' Fallback legacy (data/webview/WV2Profile_{id})
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
            "_trash", "_profile_locks", "extensions_crx_cache", "SmartScreen"
        }
        Return protectedNames.Contains(dirName)
    End Function

    ''' <summary>
    ''' Verifica se il nome della directory appartiene a un profilo nativo gestito (inizia con "WV2Profile_" o "Account_").
    ''' </summary>
    Public Function IsNativeAccountProfileDirectory(dirName As String) As Boolean
        If String.IsNullOrWhiteSpace(dirName) Then Return False
        If IsSystemOrProtectedDirectory(dirName) Then Return False
        Return dirName.StartsWith(NativeProfileDirPrefix, StringComparison.OrdinalIgnoreCase) OrElse
               dirName.StartsWith("Account_", StringComparison.OrdinalIgnoreCase) OrElse
               dirName.StartsWith("account_", StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>
    ''' Estrae l'identificativo account originario a partire dal nome della directory profilo (es. "WV2Profile_account_1" -> "account_1").
    ''' </summary>
    Public Function ExtractAccountIdFromProfileDirectory(dirName As String) As String
        If String.IsNullOrWhiteSpace(dirName) Then Return ""
        Dim clean = dirName
        If clean.StartsWith(NativeProfileDirPrefix, StringComparison.OrdinalIgnoreCase) Then
            clean = clean.Substring(NativeProfileDirPrefix.Length)
        End If
        If clean.StartsWith("account_", StringComparison.OrdinalIgnoreCase) Then
            Return "account_" & clean.Substring("account_".Length)
        ElseIf clean.StartsWith("Account_", StringComparison.OrdinalIgnoreCase) Then
            Return "account_" & clean.Substring("Account_".Length)
        End If
        Return clean
    End Function

    ''' <summary>
    ''' Verifica se una cartella di profilo contiene dati di sessione consistenti (IndexedDB consistente o cookie di dimensione significativa).
    ''' </summary>
    Public Function HasSubstantialProfileData(dirPath As String) As Boolean
        Try
            If String.IsNullOrWhiteSpace(dirPath) OrElse Not Directory.Exists(dirPath) Then Return False
            Dim idbDir = ResolveIndexedDbDirectory(dirPath)
            If Directory.Exists(idbDir) Then
                Dim files = Directory.GetFiles(idbDir, "*", SearchOption.AllDirectories)
                If files.Length > 0 Then
                    Dim totalSize As Long = 0
                    For Each f In files
                        totalSize += New FileInfo(f).Length
                    Next
                    ' Se la dimensione di IndexedDB è superiore a 800KB o ci sono almeno 8 file, è una sessione reale
                    If totalSize > 800 * 1024 OrElse files.Length >= 8 Then Return True
                End If
            End If
            Dim cookiesPath = Path.Combine(dirPath, "Network", "Cookies")
            If File.Exists(cookiesPath) AndAlso New FileInfo(cookiesPath).Length > 10240 Then
                Return True
            End If
            Dim defCookiesPath = Path.Combine(dirPath, "Default", "Network", "Cookies")
            If File.Exists(defCookiesPath) AndAlso New FileInfo(defCookiesPath).Length > 10240 Then
                Return True
            End If
        Catch
        End Try
        Return False
    End Function

    ''' <summary>
    ''' Restituisce l'elenco dei percorsi candidati di backup e migrazione legacy (.migrated, .previous, _Snapshot, .bak, e cartella legacy base)
    ''' situati all'esterno di EBWebView per un determinato account.
    ''' </summary>
    Public Function GetObsoleteMigrationBackupDirectories(sharedDir As String, accountId As String) As List(Of String)
        Dim list As New List(Of String)
        If String.IsNullOrWhiteSpace(sharedDir) OrElse String.IsNullOrWhiteSpace(accountId) Then Return list

        Dim legacyBase = Path.Combine(sharedDir, $"{LegacyProfilePrefix}{accountId}")
        list.Add(legacyBase & ".migrated")
        list.Add(legacyBase & ".previous")
        list.Add(legacyBase & "_Snapshot")
        list.Add(legacyBase & ".bak")
        list.Add(legacyBase)
        Return list
    End Function

    ''' <summary>
    ''' Determina se le copie di backup e migrazione legacy possono essere rimosse in sicurezza:
    ''' solo se il profilo nativo in EBWebView/ esiste e contiene dati di sessione reali verificati.
    ''' </summary>
    Public Function CanSafelyPurgeMigrationBackups(sharedDir As String, accountId As String) As Boolean
        If String.IsNullOrWhiteSpace(sharedDir) OrElse String.IsNullOrWhiteSpace(accountId) Then Return False
        Dim nativeDir = ResolveEffectiveProfileDirectory(sharedDir, accountId)
        If String.IsNullOrEmpty(nativeDir) OrElse Not Directory.Exists(nativeDir) Then Return False

        ' Il percorso nativo deve risiedere sotto EBWebView
        Dim ebDir = Path.Combine(sharedDir, SharedEbWebViewFolder)
        If Not nativeDir.StartsWith(ebDir, StringComparison.OrdinalIgnoreCase) Then Return False

        Return HasSubstantialProfileData(nativeDir)
    End Function

End Module
