Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Threading.Tasks

''' <summary>
''' Whole-directory copy for a CLOSED browser profile. Never merges database generations.
''' The caller must hold the account lease and have observed BrowserProcessExited.
''' A failed copy leaves the destination unchanged. Publication uses two renames with rollback;
''' this is not a filesystem transaction (a power loss can leave .previous beside the target).
''' </summary>
Friend NotInheritable Class ColdProfileCopy

    Public Shared ReadOnly SkipFolderNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "Cache",
        "Code Cache",
        "GPUCache",
        "GPUPersistentCache",
        "ShaderCache",
        "GrShaderCache",
        "DawnCache",
        "DawnGraphiteCache",
        "DawnWebGPUCache",
        "GraphiteDawnCache",
        "Crashpad",
        "BrowserMetrics",
        "BrowserMetrics-spare",
        "component_crx_cache",
        "extensions_crx_cache",
        "OptimizationGuidePredictionModels",
        "optimization_guide_hint_cache_store",
        "AutofillAiModelCache",
        "Safe Browsing",
        "Subresource Filter",
        "CacheStorage",
        "ScriptCache",
        "blob_storage"
    }

    Private Shared ReadOnly SkipExactFileNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "LOCK",
        ".active_session",
        "DevToolsActivePort"
    }

    Public Shared Function HasData(directoryPath As String) As Boolean
        If Not Directory.Exists(directoryPath) Then Return False
        ' Conservatively preserve ANY existing state, even a small/empty-looking database.
        Return Directory.EnumerateFileSystemEntries(directoryPath).Any()
    End Function

    Public Shared Sub RecoverInterruptedPublication(target As String)
        Dim previous = target & ".previous"
        If Not Directory.Exists(target) AndAlso Directory.Exists(previous) Then
            Directory.Move(previous, target)
        End If
        ' Bonifica eventuali directory temporanee orfane (.incoming-* o .trash-*)
        Try
            Dim parentDir = Path.GetDirectoryName(target)
            If Directory.Exists(parentDir) Then
                Dim targetName = Path.GetFileName(target)
                For Each orphan In Directory.EnumerateDirectories(parentDir, targetName & ".*")
                    If orphan.Contains(".incoming-") OrElse orphan.Contains(".trash-") Then
                        Try
                            Directory.Delete(orphan, recursive:=True)
                        Catch
                        End Try
                    End If
                Next
            End If
        Catch
        End Try
    End Sub

    Public Shared Async Function SeedIfEmptyAsync(source As String, target As String) As Task(Of Boolean)
        RecoverInterruptedPublication(target)
        If HasData(target) Then Return True
        RecoverInterruptedPublication(source)
        If Not HasData(source) Then
            Directory.CreateDirectory(target)
            Return True
        End If
        Return Await PublishAsync(source, target)
    End Function

    Public Shared Function PublishAsync(source As String, target As String) As Task(Of Boolean)
        Return Task.Run(Function() Publish(source, target))
    End Function

    Private Shared Function Publish(source As String, target As String) As Boolean
        Dim incoming As String = Nothing
        Dim previous As String = Nothing
        Dim trash As String = Nothing
        Try
            source = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            target = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            If source.Equals(target, StringComparison.OrdinalIgnoreCase) OrElse
               source.StartsWith(target & Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) OrElse
               target.StartsWith(source & Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) Then
                Throw New IOException("Source and destination profiles overlap.")
            End If
            If Not Directory.Exists(source) Then Return False
            RejectReparsePoint(source)
            Directory.CreateDirectory(Path.GetDirectoryName(target))
            RecoverInterruptedPublication(target)
            If Directory.Exists(target) Then RejectReparsePoint(target)
            previous = target & ".previous"
            incoming = target & ".incoming-" & Guid.NewGuid().ToString("N")
            CopyTree(source, incoming, "")

            ' Do not touch the old destination until EVERY required file has been copied.
            ' On network/SMB shares, avoid synchronous in-place recursive deletion of .previous
            ' before moving, as it can fail with ERROR_ACCESS_DENIED or stall.
            ' Rename .previous to .trash-<guid> instantly, rotate target -> previous and incoming -> target,
            ' then delete the trash directory.
            If Directory.Exists(previous) Then
                RejectReparsePoint(previous)
                trash = target & ".trash-" & Guid.NewGuid().ToString("N")
                Directory.Move(previous, trash)
            End If
            If Directory.Exists(target) Then Directory.Move(target, previous)
            Try
                Directory.Move(incoming, target)
            Catch
                If Not Directory.Exists(target) AndAlso Directory.Exists(previous) Then
                    Directory.Move(previous, target)
                End If
                Throw
            End Try

            If trash IsNot Nothing AndAlso Directory.Exists(trash) Then
                Try
                    Directory.Delete(trash, recursive:=True)
                Catch
                    ' Leftover .trash-* directory is never selected as a profile and can be purged asynchronously.
                End Try
            End If

            ' Retain .previous for manual recovery; it is not proof of a valid login.
            Return True
        Catch ex As Exception
            AppLogger.LogSync($"Cold profile copy failed; no partial database is published: {ex.Message}")
            Return False
        Finally
            If incoming IsNot Nothing AndAlso Directory.Exists(incoming) Then
                Try
                    Directory.Delete(incoming, recursive:=True)
                Catch
                    ' A leftover .incoming-* directory is NEVER selected as a profile.
                End Try
            End If
        End Try
    End Function

    Private Shared Sub CopyTree(source As String, target As String, relative As String)
        RejectReparsePoint(source)
        Directory.CreateDirectory(target)
        For Each entry In Directory.EnumerateFileSystemEntries(source)
            Dim name = Path.GetFileName(entry)
            Dim childRelative = If(relative.Length = 0, name, relative & "/" & name)
            If ShouldSkip(childRelative) Then Continue For
            RejectReparsePoint(entry)
            Dim destination = Path.Combine(target, name)
            If Directory.Exists(entry) Then
                CopyTree(entry, destination, childRelative)
            Else
                ' Do not silently read a database that another process is writing.
                Using input As New FileStream(entry, FileMode.Open, FileAccess.Read, FileShare.Read)
                    Using output As New FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None)
                        input.CopyTo(output)
                    End Using
                End Using
                File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(entry))
            End If
        Next
    End Sub

    Private Shared Sub RejectReparsePoint(value As String)
        If (File.GetAttributes(value) And FileAttributes.ReparsePoint) <> 0 Then
            Throw New IOException("Profile copy refuses symbolic links/reparse points: " & value)
        End If
    End Sub

    Friend Shared Function ShouldSkip(relative As String) As Boolean
        If String.IsNullOrWhiteSpace(relative) Then Return False
        Dim normalized = relative.Replace("\"c, "/"c).Trim("/"c)
        Dim parts = normalized.Split("/"c)
        Dim name = parts(parts.Length - 1)

        ' Check lock, session or temporary singleton files
        If SkipExactFileNames.Contains(name) OrElse
           name.StartsWith("Singleton", StringComparison.OrdinalIgnoreCase) OrElse
           name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) OrElse
           name.EndsWith(".dmp", StringComparison.OrdinalIgnoreCase) OrElse
           name.EndsWith(".pma", StringComparison.OrdinalIgnoreCase) OrElse
           name.EndsWith(".old", StringComparison.OrdinalIgnoreCase) OrElse
           name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) Then
            Return True
        End If

        ' If ANY folder in the relative path is a cache/junk folder, skip it
        For Each part In parts
            If SkipFolderNames.Contains(part) Then Return True
        Next

        ' Compatibility for legacy relative paths (e.g., EBWebView/Default/Cache)
        Dim legacyPrefixes = {"EBWebView/Crashpad", "EBWebView/ShaderCache", "EBWebView/GrShaderCache",
                              "EBWebView/Default/Cache", "EBWebView/Default/Code Cache", "EBWebView/Default/GPUCache"}
        For Each prefix In legacyPrefixes
            If normalized.Equals(prefix, StringComparison.OrdinalIgnoreCase) OrElse
               normalized.StartsWith(prefix & "/", StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
        Next

        ' Cookies, Local State, SQLite journals/WAL, IndexedDB, Local/Session Storage,
        ' Service Worker Database (worker registrations) and Preferences are retained together, without size heuristics.
        Return False
    End Function
End Class
