Imports System
Imports System.IO
Imports System.Threading

''' <summary>
''' Safety backup and automatic rollback policy for OTA updates (TASK-0119).
''' Creates a temporary safety copy of existing installation binaries before file replacement,
''' restores previous binaries upon copy failure, and relaunches the working version.
''' Strictly excludes user data directories ('data/') to preserve 100% portability.
''' </summary>
Public Class UpdateBackupPolicy

    ''' <summary>
    ''' Checks whether a relative path belongs to the user data directory.
    ''' </summary>
    Public Shared Function IsUserDataPath(relativePath As String) As Boolean
        If String.IsNullOrWhiteSpace(relativePath) Then Return False
        Dim norm = relativePath.Trim().Replace("/"c, Path.DirectorySeparatorChar)
        Return norm.Equals("data", StringComparison.OrdinalIgnoreCase) OrElse
               norm.StartsWith("data" & Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) OrElse
               norm.Equals(".git", StringComparison.OrdinalIgnoreCase) OrElse
               norm.StartsWith(".git" & Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>
    ''' Checks whether a file should be excluded from backup and rollback (logs, temporary files, updater scripts).
    ''' </summary>
    Public Shared Function ShouldExcludeFile(relativePath As String) As Boolean
        If IsUserDataPath(relativePath) Then Return True
        Dim fileName = Path.GetFileName(relativePath)
        If fileName.StartsWith("update_", StringComparison.OrdinalIgnoreCase) OrElse
           fileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) OrElse
           fileName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) OrElse
           fileName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) OrElse
           fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) OrElse
           fileName.Equals(".update_log.txt", StringComparison.OrdinalIgnoreCase) OrElse
           fileName.Equals("updates.log", StringComparison.OrdinalIgnoreCase) Then
            Return True
        End If
        Return False
    End Function

    ''' <summary>
    ''' Creates a safety backup of all installation binaries from installDir to backupDir.
    ''' Strictly excludes user data ('data/') and temporary update scripts.
    ''' </summary>
    Public Shared Sub CreateSafetyBackup(installDir As String, backupDir As String)
        If Not Directory.Exists(installDir) Then Return
        If Directory.Exists(backupDir) Then
            DeleteSafetyBackup(backupDir)
        End If
        Directory.CreateDirectory(backupDir)

        Dim allDirs = Directory.GetDirectories(installDir, "*", SearchOption.AllDirectories)
        For Each dirPath In allDirs
            Dim relDir = Path.GetRelativePath(installDir, dirPath)
            If IsUserDataPath(relDir) Then Continue For

            Dim targetDir = Path.Combine(backupDir, relDir)
            If Not Directory.Exists(targetDir) Then Directory.CreateDirectory(targetDir)
        Next

        Dim allFiles = Directory.GetFiles(installDir, "*", SearchOption.AllDirectories)
        For Each filePath In allFiles
            Dim relFile = Path.GetRelativePath(installDir, filePath)
            If ShouldExcludeFile(relFile) Then Continue For

            Dim targetFilePath = Path.Combine(backupDir, relFile)
            Dim targetFileDir = Path.GetDirectoryName(targetFilePath)
            If Not Directory.Exists(targetFileDir) Then Directory.CreateDirectory(targetFileDir)

            For attempt As Integer = 1 To 5
                Try
                    File.Copy(filePath, targetFilePath, overwrite:=True)
                    Exit For
                Catch ex As Exception
                    If attempt = 5 Then Throw
                    Thread.Sleep(100)
                End Try
            Next
        Next
    End Sub

    ''' <summary>
    ''' Restores original binaries from backupDir to installDir, strictly preserving user data ('data/').
    ''' </summary>
    Public Shared Sub RestoreFromSafetyBackup(backupDir As String, installDir As String)
        If Not Directory.Exists(backupDir) Then
            Throw New DirectoryNotFoundException($"Backup directory not found: '{backupDir}'")
        End If
        If Not Directory.Exists(installDir) Then Directory.CreateDirectory(installDir)

        Dim allDirs = Directory.GetDirectories(backupDir, "*", SearchOption.AllDirectories)
        For Each dirPath In allDirs
            Dim relDir = Path.GetRelativePath(backupDir, dirPath)
            If IsUserDataPath(relDir) Then Continue For

            Dim targetDir = Path.Combine(installDir, relDir)
            If Not Directory.Exists(targetDir) Then Directory.CreateDirectory(targetDir)
        Next

        Dim allFiles = Directory.GetFiles(backupDir, "*", SearchOption.AllDirectories)
        For Each filePath In allFiles
            Dim relFile = Path.GetRelativePath(backupDir, filePath)
            If ShouldExcludeFile(relFile) Then Continue For

            Dim targetFilePath = Path.Combine(installDir, relFile)
            Dim targetFileDir = Path.GetDirectoryName(targetFilePath)
            If Not Directory.Exists(targetFileDir) Then Directory.CreateDirectory(targetFileDir)

            For attempt As Integer = 1 To 10
                Try
                    File.Copy(filePath, targetFilePath, overwrite:=True)
                    Exit For
                Catch ex As Exception
                    If attempt = 10 Then Throw
                    Thread.Sleep(150)
                End Try
            Next
        Next
    End Sub

    ''' <summary>
    ''' Safely deletes the temporary backup directory.
    ''' </summary>
    Public Shared Sub DeleteSafetyBackup(backupDir As String)
        If String.IsNullOrWhiteSpace(backupDir) OrElse Not Directory.Exists(backupDir) Then Return
        Try
            Directory.Delete(backupDir, recursive:=True)
        Catch
        End Try
    End Sub

End Class
