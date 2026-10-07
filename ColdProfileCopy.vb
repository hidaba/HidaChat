Imports System
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
            If Directory.Exists(previous) Then
                RejectReparsePoint(previous)
                Directory.Delete(previous, recursive:=True)
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
                        output.Flush(flushToDisk:=True)
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

    Private Shared Function ShouldSkip(relative As String) As Boolean
        Dim normalized = relative.Replace("\"c, "/"c)
        Dim name = normalized.Substring(normalized.LastIndexOf("/"c) + 1)
        If name.Equals("LOCK", StringComparison.OrdinalIgnoreCase) OrElse
           name.Equals(".active_session", StringComparison.OrdinalIgnoreCase) OrElse
           name.Equals("DevToolsActivePort", StringComparison.OrdinalIgnoreCase) OrElse
           name.StartsWith("Singleton", StringComparison.OrdinalIgnoreCase) OrElse
           name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) Then Return True
        Dim caches = {"EBWebView/Crashpad", "EBWebView/ShaderCache", "EBWebView/GrShaderCache",
                      "EBWebView/Default/Cache", "EBWebView/Default/Code Cache", "EBWebView/Default/GPUCache"}
        For Each prefix In caches
            If normalized.Equals(prefix, StringComparison.OrdinalIgnoreCase) OrElse
               normalized.StartsWith(prefix & "/", StringComparison.OrdinalIgnoreCase) Then Return True
        Next
        ' Cookies, Local State, SQLite journals/WAL, IndexedDB, Local/Session Storage,
        ' Service Worker data and Preferences are retained together, without size heuristics.
        Return False
    End Function
End Class
