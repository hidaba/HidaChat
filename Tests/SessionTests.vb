Imports System
Imports System.IO
Imports System.Threading.Tasks

' Test doubles for the two non-I/O dependencies. No real HidaChat profile is opened.
Public Class AppAccounts
    Public Shared Property SharedDataDirectory As String
End Class
Public Class AppLogger
    Public Shared Sub LogSync(message As String)
        Console.WriteLine(message)
    End Sub
End Class

Module SessionTests
    Private _checks As Integer

    Sub Main()
        RunAsync().GetAwaiter().GetResult()
        Console.WriteLine($"PASS: {_checks} regression assertions")
    End Sub

    Private Sub Check(condition As Boolean, message As String)
        If Not condition Then Throw New Exception("FAIL: " & message)
        _checks += 1
        Console.WriteLine("PASS: " & message)
    End Sub

    Private Function WriteFile(folder As String, name As String, content As String) As String
        Directory.CreateDirectory(folder)
        Dim result = Path.Combine(folder, name)
        File.WriteAllText(result, content)
        Return result
    End Function

    Private Async Function RunAsync() As Task
        For Each kind In {"GpuProcessExited", "UtilityProcessExited", "RenderProcessUnresponsive",
                          "FrameRenderProcessExited", "UnknownProcessExited", "FutureFailureKind"}
            Check(SessionRecoveryPolicy.ForFailure(kind) = SessionRecoveryPolicy.RecoveryAction.Observe,
                  kind & " must not destroy/recreate the whole browser")
        Next
        Check(SessionRecoveryPolicy.ForFailure("BrowserProcessExited") = SessionRecoveryPolicy.RecoveryAction.Recreate,
              "A dead browser requires recreation")
        Check(SessionRecoveryPolicy.ForFailure("RenderProcessExited") = SessionRecoveryPolicy.RecoveryAction.Reload,
              "A dead renderer requires only a page reload first")
        For Each origin In {"https://web.whatsapp.com", "https://web.telegram.org", "https://WEB.WHATSAPP.COM:443/a"}
            Check(SessionRecoveryPolicy.IsTrustedStorageOrigin(origin), "Trusted storage origin: " & origin)
        Next
        For Each origin In {"http://web.whatsapp.com", "https://web.whatsapp.com.evil.example",
                            "https://evil.example", "https://web.whatsapp.com:444", "https://user@web.whatsapp.com", "not-a-url"}
            Check(Not SessionRecoveryPolicy.IsTrustedStorageOrigin(origin), "Reject storage permission: " & origin)
        Next

        Dim testRoot = Path.Combine(Path.GetTempPath(), "hidachat-session-tests-" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(testRoot)
        AppAccounts.SharedDataDirectory = Path.Combine(testRoot, "shared")
        Try
            Dim source = Path.Combine(testRoot, "source")
            Dim target = Path.Combine(testRoot, "target")
            WriteFile(source, "Local State", "new-state")
            WriteFile(source, "Cookies", "new-cookies")
            WriteFile(Path.Combine(source, "EBWebView", "Default", "IndexedDB", "db"), "CURRENT", "MANIFEST-2")
            WriteFile(Path.Combine(source, "EBWebView", "Default", "IndexedDB", "db"), "MANIFEST-2", "new-manifest")
            WriteFile(Path.Combine(source, "EBWebView", "Default", "IndexedDB", "db"), "LOCK", "do-not-copy")
            WriteFile(Path.Combine(source, "EBWebView", "Default", "Service Worker"), "registration", "keep-service-worker")
            WriteFile(target, "Local State", "old-state")
            WriteFile(target, "obsolete.ldb", "obsolete-generation")
            Check(Await ColdProfileCopy.PublishAsync(source, target), "Complete cold export succeeds")
            Check(File.ReadAllText(Path.Combine(target, "Cookies")) = "new-cookies", "Cookies copied with their generation")
            Check(Not File.Exists(Path.Combine(target, "obsolete.ldb")), "Old database generations are not merged")
            Check(File.ReadAllText(Path.Combine(target & ".previous", "Local State")) = "old-state", "Previous destination retained")
            Check(Not File.Exists(Path.Combine(target, "EBWebView", "Default", "IndexedDB", "db", "LOCK")), "Runtime lock file omitted")
            Check(File.Exists(Path.Combine(target, "EBWebView", "Default", "Service Worker", "registration")), "Service Worker state retained")

            Dim smallLocal = Path.Combine(testRoot, "small-local")
            WriteFile(smallLocal, "Cookies", "local-newer-session")
            Check(Await ColdProfileCopy.SeedIfEmptyAsync(source, smallLocal), "Existing small local profile accepted")
            Check(File.ReadAllText(Path.Combine(smallLocal, "Cookies")) = "local-newer-session", "Small profile never overwritten by size heuristic")
            Check(Not File.Exists(Path.Combine(smallLocal, "Local State")), "Missing files are not imported into an existing profile")
            Dim seeded = Path.Combine(testRoot, "seeded")
            Check(Await ColdProfileCopy.SeedIfEmptyAsync(source, seeded), "First-time empty staging is seeded")
            Check(File.ReadAllText(Path.Combine(seeded, "Cookies")) = "new-cookies", "First import contains the complete generation")
            Check(Not Await ColdProfileCopy.PublishAsync(source, source), "Self-copy refused")
            Check(Not Await ColdProfileCopy.PublishAsync(source, Path.Combine(source, "nested")), "Overlapping directories refused")
            Check(Not Await ColdProfileCopy.PublishAsync(Path.Combine(testRoot, "absent"), target), "Missing source refused")

            If OperatingSystem.IsWindows() Then
                Using locked As New FileStream(Path.Combine(source, "Cookies"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                    Check(Not Await ColdProfileCopy.PublishAsync(source, target), "Locked source aborts the copy")
                    Check(File.ReadAllText(Path.Combine(target, "Cookies")) = "new-cookies", "Failed copy leaves destination intact")
                End Using
            End If

            Dim interrupted = Path.Combine(testRoot, "interrupted")
            WriteFile(interrupted & ".previous", "Cookies", "previous-complete-generation")
            ColdProfileCopy.RecoverInterruptedPublication(interrupted)
            Check(File.Exists(Path.Combine(interrupted, "Cookies")), "Interrupted rename can recover the previous complete directory")
            Check(Not Await NetworkProfileSync.SyncLocalStagingToMasterAsync("unit", isPeriodic:=True, browserExited:=True), "Periodic export is always refused")
            Check(Not Await NetworkProfileSync.SyncLocalStagingToMasterAsync("unit"), "Export without browser exit is refused")
            Check(Not Await NetworkProfileSync.SyncMasterToLocalStagingAsync("unit"), "Import without a session lease is refused")
            Check(NetworkProfileSync.AcquireSessionLock("unit") Is Nothing, "Account lease acquired")
            Check(NetworkProfileSync.AcquireSessionLock("unit") Is Nothing, "Same owner can retain its lease across recovery")
            If OperatingSystem.IsWindows() Then
                Dim denied = False
                Try
                    Using contender As New FileStream(Path.Combine(AppAccounts.SharedDataDirectory, "_profile_locks", "unit.lock"),
                                                       FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                    End Using
                Catch ex As IOException
                    denied = True
                End Try
                Check(denied, "A second OS handle cannot steal the lease")
            End If
            NetworkProfileSync.ReleaseSessionLock("unit")
            Check(File.Exists(Path.Combine(AppAccounts.SharedDataDirectory, "_profile_locks", "unit.lock")), "Lease release does not delete a potentially reused lock file")
            Dim invalidIdRejected = False
            Try
                NetworkProfileSync.GetMasterProfileDir("../escape")
            Catch ex As ArgumentException
                invalidIdRejected = True
            End Try
            Check(invalidIdRejected, "Profile ID path traversal rejected")
        Finally
            NetworkProfileSync.ReleaseSessionLock("unit")
            Directory.Delete(testRoot, recursive:=True)
        End Try
    End Function
End Module
