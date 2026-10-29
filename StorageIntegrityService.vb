Imports System.IO
Imports System.Threading.Tasks
Imports System.Windows
Imports Microsoft.Web.WebView2.Core

''' <summary>
''' Risultato dell'analisi strutturale dell'integrità del database IndexedDB di un profilo Chromium.
''' </summary>
Public Class StorageCheckResult
    Public Property IsCorrupted As Boolean = False
    Public Property CorruptedDatabases As New List(Of String)()
    Public Property Reason As String = ""
End Class

''' <summary>
''' Servizio di verifica strutturale preventiva e ripristino per banche dati IndexedDB e Service Worker
''' di WebView2 in caso di crash o mancata scrittura blob (TODO #76).
''' </summary>
Public Class StorageIntegrityService

    ''' <summary>
    ''' Verifica l'integrità strutturale della cartella IndexedDB di un profilo WebView2 a freddo (prima dell'avvio o dopo crash).
    ''' </summary>
    Public Shared Function CheckIndexedDbCorruption(profileDir As String) As StorageCheckResult
        Dim result As New StorageCheckResult()
        If String.IsNullOrWhiteSpace(profileDir) OrElse Not Directory.Exists(profileDir) Then
            Return result
        End If

        Try
            Dim indexedDbRoot = ProfileIsolationPolicy.ResolveIndexedDbDirectory(profileDir)
            If String.IsNullOrWhiteSpace(indexedDbRoot) OrElse Not Directory.Exists(indexedDbRoot) Then
                Return result
            End If

            For Each dbDir In Directory.EnumerateDirectories(indexedDbRoot, "*.indexeddb.leveldb")
                Dim dirInfo As New DirectoryInfo(dbDir)
                Dim files = dirInfo.GetFiles()

                ' 1. Directory vuota o priva di file essenziali
                If files.Length = 0 Then
                    result.IsCorrupted = True
                    result.CorruptedDatabases.Add(dbDir)
                    result.Reason = $"Database directory '{dirInfo.Name}' is completely empty."
                    Continue For
                End If

                ' 2. Verifica del puntatore CURRENT di LevelDB
                Dim currentFile = Path.Combine(dbDir, "CURRENT")
                If Not File.Exists(currentFile) Then
                    ' Se ci sono file di log o ldb ma manca CURRENT, il database è illeggibile
                    Dim hasDataFiles = files.Any(Function(f) f.Extension.Equals(".ldb", StringComparison.OrdinalIgnoreCase) OrElse f.Extension.Equals(".log", StringComparison.OrdinalIgnoreCase))
                    If hasDataFiles Then
                        result.IsCorrupted = True
                        result.CorruptedDatabases.Add(dbDir)
                        result.Reason = $"CURRENT file missing in '{dirInfo.Name}'."
                        Continue For
                    End If
                Else
                    Dim currentInfo As New FileInfo(currentFile)
                    If currentInfo.Length = 0 Then
                        result.IsCorrupted = True
                        result.CorruptedDatabases.Add(dbDir)
                        result.Reason = $"CURRENT file is 0 bytes in '{dirInfo.Name}'."
                        Continue For
                    End If

                    ' Legge il puntatore al MANIFEST
                    Try
                        Dim manifestName = File.ReadAllText(currentFile).Trim()
                        If Not String.IsNullOrWhiteSpace(manifestName) Then
                            Dim manifestPath = Path.Combine(dbDir, manifestName)
                            If Not File.Exists(manifestPath) OrElse New FileInfo(manifestPath).Length = 0 Then
                                result.IsCorrupted = True
                                result.CorruptedDatabases.Add(dbDir)
                                result.Reason = $"Referenced MANIFEST file '{manifestName}' missing or 0 bytes in '{dirInfo.Name}'."
                                Continue For
                            End If
                        End If
                    Catch exRead As Exception
                        result.IsCorrupted = True
                        result.CorruptedDatabases.Add(dbDir)
                        result.Reason = $"Unreadable CURRENT file in '{dirInfo.Name}': {exRead.Message}"
                        Continue For
                    End Try
                End If

                ' 3. Verifica della directory blob associata se presente
                Dim blobDir = dbDir.Substring(0, dbDir.Length - ".leveldb".Length) & ".blob"
                If Directory.Exists(blobDir) Then
                    Try
                        Dim blobFiles = Directory.GetFiles(blobDir, "*.*", SearchOption.AllDirectories)
                        ' Verifica accessibilità dei file blob
                    Catch exBlob As Exception
                        result.IsCorrupted = True
                        result.CorruptedDatabases.Add(blobDir)
                        result.Reason = $"Unreadable blob directory '{Path.GetFileName(blobDir)}': {exBlob.Message}"
                        Continue For
                    End Try
                End If
            Next
        Catch ex As Exception
            AppLogger.LogApp($"[STORAGE_INTEGRITY_CHECK_ERROR] profile={profileDir}: {ex.Message}")
        End Try

        Return result
    End Function

    ''' <summary>
    ''' Ripara le banche dati IndexedDB corrotte e i Service Worker associati a freddo su disco,
    ''' preservando scrupolosamente Local Storage, Session Storage, Cookies e credenziali.
    ''' </summary>
    Public Shared Async Function RepairCorruptedIndexedDbStorageAsync(profileDir As String, Optional specificDbs As List(Of String) = Nothing, Optional onProgress As Action(Of String) = Nothing) As Task(Of Boolean)
        If String.IsNullOrWhiteSpace(profileDir) OrElse Not Directory.Exists(profileDir) Then
            Return False
        End If

        Return Await Task.Run(Function()
            Try
                onProgress?.Invoke("Rimozione database e indici corrotti...")
                Dim indexedDbRoot = ProfileIsolationPolicy.ResolveIndexedDbDirectory(profileDir)

                If specificDbs IsNot Nothing AndAlso specificDbs.Count > 0 Then
                    For Each dbPath In specificDbs
                        Try
                            If Directory.Exists(dbPath) Then
                                Directory.Delete(dbPath, True)
                            End If
                            ' Rimuove anche la cartella blob gemella se applicabile
                            If dbPath.EndsWith(".leveldb", StringComparison.OrdinalIgnoreCase) Then
                                Dim geminiBlob = dbPath.Substring(0, dbPath.Length - ".leveldb".Length) & ".blob"
                                If Directory.Exists(geminiBlob) Then Directory.Delete(geminiBlob, True)
                            End If
                        Catch exDel As Exception
                            AppLogger.LogApp($"[STORAGE_REPAIR_DEL_ERROR] path={dbPath}: {exDel.Message}")
                        End Try
                    Next
                ElseIf Not String.IsNullOrWhiteSpace(indexedDbRoot) AndAlso Directory.Exists(indexedDbRoot) Then
                    Try
                        Directory.Delete(indexedDbRoot, True)
                    Catch exRoot As Exception
                        AppLogger.LogApp($"[STORAGE_REPAIR_ROOT_ERROR]: {exRoot.Message}")
                    End Try
                End If

                ' Pulisce anche la cartella Service Worker per evitare che worker orfani cerchino database cancellati
                Dim swDir = ProfileIsolationPolicy.ResolveServiceWorkerDirectory(profileDir)
                If Not String.IsNullOrWhiteSpace(swDir) AndAlso Directory.Exists(swDir) Then
                    Try
                        Directory.Delete(swDir, True)
                    Catch
                    End Try
                End If

                AppLogger.LogApp($"[STORAGE_REPAIR_SUCCESS] profile={profileDir}")
                Return True
            Catch ex As Exception
                AppLogger.LogApp($"[STORAGE_REPAIR_FATAL] profile={profileDir}: {ex.Message}")
                Return False
            End Try
        End Function)
    End Function

    ''' <summary>
    ''' Esegue la procedura completa di ripristino con visualizzazione della finestra di avanzamento con ProgressBar (TODO #76).
    ''' </summary>
    Public Shared Async Function PerformStorageRecoveryWithDialogAsync(acc As AppAccounts, loc As AppLocalizations, Optional ownerWindow As Window = Nothing) As Task(Of Boolean)
        If acc Is Nothing Then Return False

        Dim dialog As StorageRecoveryProgressDialog = Nothing
        Try
            Await Application.Current.Dispatcher.InvokeAsync(Sub()
                dialog = New StorageRecoveryProgressDialog(loc, acc.Name, acc.Platform)
                If ownerWindow IsNot Nothing AndAlso ownerWindow.IsVisible Then
                    dialog.Owner = ownerWindow
                End If
                dialog.Show()
            End Sub)

            Dim args As New Dictionary(Of String, String) From {
                {"account", acc.Name},
                {"platform", acc.Platform}
            }

            dialog?.UpdateStatus(If(loc IsNot Nothing, loc.Get("storage_recovery_stage_detected", args), $"Rilevata corruzione storage per {acc.Name}. Rigenerazione database..."))
            Await Task.Delay(400)

            dialog?.UpdateStatus(If(loc IsNot Nothing, loc.Get("storage_recovery_stage_repairing", args), "Pulizia cache IndexedDB/ServiceWorker corrotte (sessioni salvaguardate)..."))

            ' Esegue il ripristino su WebView o su disco
            If acc.WebView IsNot Nothing AndAlso acc.WebView.CoreWebView2 IsNot Nothing AndAlso acc.WebView.CoreWebView2.Profile IsNot Nothing Then
                Dim dataKinds = CoreWebView2BrowsingDataKinds.IndexedDb Or
                                CoreWebView2BrowsingDataKinds.ServiceWorkers Or
                                CoreWebView2BrowsingDataKinds.CacheStorage
                Await acc.WebView.CoreWebView2.Profile.ClearBrowsingDataAsync(dataKinds)
            Else
                ' Ripristino a freddo su filesystem
                Dim profPath = acc.ProfilePath
                If String.IsNullOrWhiteSpace(profPath) Then
                    profPath = NetworkProfileSync.GetEffectiveProfileDir(acc.Id, enableStaging:=True)
                End If
                Await RepairCorruptedIndexedDbStorageAsync(profPath)
            End If

            dialog?.UpdateStatus(If(loc IsNot Nothing, loc.Get("storage_recovery_stage_reloading", args), "Ricaricamento vista web e inizializzazione database pulito..."))
            Await Task.Delay(400)

            If acc.WebView IsNot Nothing AndAlso acc.WebView.CoreWebView2 IsNot Nothing Then
                acc.WebView.CoreWebView2.Reload()
            End If

            dialog?.UpdateStatus(If(loc IsNot Nothing, loc.Get("storage_recovery_stage_completed", args), "Ripristino storage completato con successo!"))
            Await Task.Delay(600)

            Return True
        Catch ex As Exception
            AppLogger.LogApp($"PerformStorageRecoveryWithDialogAsync failed: {ex.Message}")
            Return False
        Finally
            If dialog IsNot Nothing Then
                Application.Current?.Dispatcher?.Invoke(Sub()
                    Try
                        dialog.Close()
                    Catch
                    End Try
                End Sub)
            End If
        End Try
    End Function

End Class
