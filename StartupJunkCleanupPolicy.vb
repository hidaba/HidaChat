Option Explicit On
Option Strict On

Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO

''' <summary>
''' Politiche e utilità per la bonifica automatica delle cartelle e dei file spazzatura all'avvio dell'applicazione (TODO #105).
''' Esegue la rimozione sicura di file temporanei (.dmp, .pma, .old) e directory di diagnostica o shader
''' prima che i processi Chromium/WebView2 vengano istanziati e blocchino i file su disco,
''' preservando rigorosamente cookie, LocalStorage, IndexedDB (.leveldb / .blob) e la chiave di crittografia master in Local State.
''' </summary>
Public Module StartupJunkCleanupPolicy

    ''' <summary>
    ''' Insieme di cartelle sicure da eliminare al 100% (non contengono mai credenziali o token di login).
    ''' </summary>
    Public ReadOnly JunkFolderNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "Crashpad",
        "GrShaderCache",
        "ShaderCache",
        "GraphiteDawnCache",
        "DawnCache",
        "DawnGraphiteCache",
        "DawnWebGPUCache",
        "GPUCache",
        "GPUPersistentCache",
        "Code Cache",
        "Cache",
        "BrowserMetrics",
        "BrowserMetrics-spare",
        "component_crx_cache",
        "OptimizationGuidePredictionModels",
        "Safe Browsing",
        "Subresource Filter"
    }

    ''' <summary>
    ''' Estensioni dei file spazzatura generati da crash, metriche o file temporanei orfani.
    ''' </summary>
    Public ReadOnly JunkFileExtensions As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        ".dmp",
        ".pma",
        ".old"
    }

    ''' <summary>
    ''' Cartelle protette contenenti dati essenziali di sessione, credenziali o token Signal che non devono MAI essere toccate.
    ''' </summary>
    Public ReadOnly ProtectedFolderNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "IndexedDB",
        "Local Storage",
        "Session Storage",
        "Sync Data",
        "Network",
        "Default",
        "EBWebView"
    }

    ''' <summary>
    ''' File protetti contenenti credenziali DPAPI, impostazioni o database di sessione.
    ''' </summary>
    Public ReadOnly ProtectedFileNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "Local State",
        "Preferences",
        "Cookies",
        "settings.json",
        ".lock",
        ".app_version",
        "translations_cache.json"
    }

    ''' <summary>
    ''' Determina se il nome di una cartella corrisponde a una directory spazzatura nota.
    ''' </summary>
    Public Function IsJunkFolderName(folderName As String) As Boolean
        If String.IsNullOrWhiteSpace(folderName) Then Return False
        Return JunkFolderNames.Contains(folderName.Trim())
    End Function

    ''' <summary>
    ''' Determina se una cartella è protetta e non deve essere eliminata né attraversata alla ricerca di cartelle spazzatura.
    ''' </summary>
    Public Function IsProtectedDirectory(folderName As String) As Boolean
        If String.IsNullOrWhiteSpace(folderName) Then Return False
        Dim cleanName = folderName.Trim()
        If ProtectedFolderNames.Contains(cleanName) Then Return True
        If cleanName.EndsWith(".leveldb", StringComparison.OrdinalIgnoreCase) Then Return True
        If cleanName.EndsWith(".blob", StringComparison.OrdinalIgnoreCase) Then Return True
        Return False
    End Function

    ''' <summary>
    ''' Determina se un file corrisponde a un residuo temporaneo eliminabile (.dmp, .pma, .old)
    ''' garantendo che non si tratti di un file critico protetto.
    ''' </summary>
    Public Function IsJunkFileName(filePath As String) As Boolean
        If String.IsNullOrWhiteSpace(filePath) Then Return False
        Dim fileName = Path.GetFileName(filePath)
        If ProtectedFileNames.Contains(fileName) Then Return False

        Dim ext = Path.GetExtension(filePath)
        If String.IsNullOrEmpty(ext) Then Return False
        Return JunkFileExtensions.Contains(ext)
    End Function

    ''' <summary>
    ''' Risultato dell'operazione di pulizia automatica spazzatura.
    ''' </summary>
    Public Class CleanupResult
        Public Property DeletedFoldersCount As Integer = 0
        Public Property DeletedFilesCount As Integer = 0
        Public Property FreedBytes As Long = 0
    End Class

    ''' <summary>
    ''' Esegue la scansione e rimozione automatica delle cartelle e dei file spazzatura all'avvio dell'applicazione (TODO #105).
    ''' </summary>
    ''' <param name="baseDataPath">Percorso base della cartella profili (es. data/webview).</param>
    ''' <returns>Dettagli statistici dell'operazione di pulizia.</returns>
    Public Function CleanWebViewJunkFolders(baseDataPath As String) As CleanupResult
        Dim result As New CleanupResult()
        If String.IsNullOrWhiteSpace(baseDataPath) OrElse Not Directory.Exists(baseDataPath) Then
            Return result
        End If

        Try
            ' 1. Scansione ad albero sicuro tramite coda (evita ricorsione cieca e protegge IndexedDB / Local Storage)
            Dim junkDirectories As New List(Of String)()
            Dim queue As New Queue(Of String)()
            queue.Enqueue(baseDataPath)

            While queue.Count > 0
                Dim currentDir = queue.Dequeue()
                Dim subDirs As String() = Nothing
                Try
                    subDirs = Directory.GetDirectories(currentDir)
                Catch
                    Continue While
                End Try

                For Each subDir In subDirs
                    Dim dirName = Path.GetFileName(subDir)
                    If IsJunkFolderName(dirName) Then
                        ' Trovata cartella spazzatura: la aggiungiamo alla lista di rimozione senza esplorarne i figli
                        junkDirectories.Add(subDir)
                    ElseIf Not IsProtectedDirectory(dirName) Then
                        ' Cartella normale (es. profilo account, EBWebView): continuiamo l'esplorazione
                        queue.Enqueue(subDir)
                    End If
                Next
            End While

            ' 2. Eliminazione delle cartelle spazzatura individuate
            For Each dirPath In junkDirectories
                Try
                    If Directory.Exists(dirPath) Then
                        ' Calcola byte prima della rimozione
                        Try
                            Dim di As New DirectoryInfo(dirPath)
                            For Each fi In di.EnumerateFiles("*", SearchOption.AllDirectories)
                                result.FreedBytes += fi.Length
                            Next
                        Catch
                        End Try

                        Directory.Delete(dirPath, recursive:=True)
                        result.DeletedFoldersCount += 1
                    End If
                Catch ex As Exception
                    ' Se una directory è temporaneamente bloccata, prova ad eliminare i singoli file non bloccati
                    Try
                        If Directory.Exists(dirPath) Then
                            For Each f In Directory.EnumerateFiles(dirPath, "*", SearchOption.AllDirectories)
                                Try
                                    Dim fi As New FileInfo(f)
                                    Dim len = fi.Length
                                    File.Delete(f)
                                    result.FreedBytes += len
                                    result.DeletedFilesCount += 1
                                Catch
                                End Try
                            Next
                        End If
                    Catch
                    End Try
                End Try
            Next

            ' 3. Scansione ed eliminazione dei file orfani spazzatura (.dmp, .pma, .old)
            ' Escludiamo le cartelle protette come IndexedDB e Local Storage
            Dim fileScanQueue As New Queue(Of String)()
            fileScanQueue.Enqueue(baseDataPath)

            While fileScanQueue.Count > 0
                Dim currentDir = fileScanQueue.Dequeue()
                Dim files As String() = Nothing
                Dim subDirs As String() = Nothing
                Try
                    files = Directory.GetFiles(currentDir)
                    subDirs = Directory.GetDirectories(currentDir)
                Catch
                    Continue While
                End Try

                If files IsNot Nothing Then
                    For Each filePath In files
                        If IsJunkFileName(filePath) Then
                            Try
                                Dim fi As New FileInfo(filePath)
                                Dim len = fi.Length
                                File.Delete(filePath)
                                result.DeletedFilesCount += 1
                                result.FreedBytes += len
                            Catch
                            End Try
                        End If
                    Next
                End If

                If subDirs IsNot Nothing Then
                    For Each subDir In subDirs
                        Dim dName = Path.GetFileName(subDir)
                        If Not IsProtectedDirectory(dName) AndAlso Not IsJunkFolderName(dName) Then
                            fileScanQueue.Enqueue(subDir)
                        End If
                    Next
                End If
            End While

        Catch ex As Exception
            Debug.WriteLine($"CleanWebViewJunkFolders error: {ex.Message}")
        End Try

        Return result
    End Function

End Module
