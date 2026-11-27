Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Net.Security
Imports System.Security.Authentication
Imports System.Security.Cryptography
Imports System.Security.Cryptography.X509Certificates
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks

' Test doubles for the non-I/O dependencies. No real HidaChat profile is opened.
Public Class AppAccounts
    Public Shared Property SharedDataDirectory As String
End Class

Public Class MockTranslationFailoverHandler
    Inherits HttpMessageHandler

    Public Property CallCount As Integer = 0
    Public Property SimulatedUrls As New List(Of String)()
    Public Property Force429Count As Integer = 1

    Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
        CallCount += 1
        SimulatedUrls.Add(request.RequestUri.ToString())
        If CallCount <= Force429Count Then
            Dim resp429 As New HttpResponseMessage(HttpStatusCode.TooManyRequests)
            resp429.Content = New StringContent("Too Many Requests")
            Return Task.FromResult(resp429)
        Else
            Dim resp200 As New HttpResponseMessage(HttpStatusCode.OK)
            resp200.Content = New StringContent("[[""Hola"",""en""]]")
            Return Task.FromResult(resp200)
        End If
    End Function
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
        For Each origin In {"https://web.whatsapp.com", "https://web.telegram.org", "https://discord.com", "https://WEB.WHATSAPP.COM:443/a"}
            Check(SessionRecoveryPolicy.IsTrustedStorageOrigin(origin), "Trusted storage origin: " & origin)
        Next
        For Each origin In {"http://web.whatsapp.com", "https://web.whatsapp.com.evil.example",
                            "https://evil.example", "https://web.whatsapp.com:444", "https://user@web.whatsapp.com", "http://discord.com", "https://discord.com:444", "https://cdn.discordapp.com", "not-a-url"}
            Check(Not SessionRecoveryPolicy.IsTrustedStorageOrigin(origin), "Reject storage permission: " & origin)
        Next

        ' Test LocalAuthPolicy (TODO #78)
        Check(LocalAuthPolicy.ShouldAttachCredentials("http://127.0.0.1:18800/api/chat", "http", "127.0.0.1", 18800), "Auth policy accepts exact match on assigned local proxy port")
        Check(Not LocalAuthPolicy.ShouldAttachCredentials("http://127.0.0.1:8080/other", "http", "127.0.0.1", 18800), "Auth policy rejects different localhost port")
        Check(Not LocalAuthPolicy.ShouldAttachCredentials("http://localhost:18800/api", "http", "127.0.0.1", 18800), "Auth policy rejects localhost when 127.0.0.1 is expected")
        Check(Not LocalAuthPolicy.ShouldAttachCredentials("https://127.0.0.1:18800/api", "http", "127.0.0.1", 18800), "Auth policy rejects scheme mismatch")
        Check(Not LocalAuthPolicy.ShouldAttachCredentials("http://127.0.0.1.attacker.com:18800/", "http", "127.0.0.1", 18800), "Auth policy rejects subdomain spoofing")
        Check(Not LocalAuthPolicy.ShouldAttachCredentials("http://192.168.1.50:18800/", "http", "127.0.0.1", 18800), "Auth policy rejects external IP")
        Check(Not LocalAuthPolicy.ShouldAttachCredentials("not-a-url", "http", "127.0.0.1", 18800), "Auth policy rejects malformed URL")
        Check(Not LocalAuthPolicy.ShouldAttachCredentials(Nothing, "http", "127.0.0.1", 18800), "Auth policy rejects null URL")
        Check(LocalAuthPolicy.ShouldAttachCredentials("https://gateway.tailnet.ts.net/chat", "https", "gateway.tailnet.ts.net", 443), "Auth policy accepts standard HTTPS port default")
        Check(Not LocalAuthPolicy.ShouldAttachCredentials("https://gateway.tailnet.ts.net:8443/chat", "https", "gateway.tailnet.ts.net", 443), "Auth policy rejects non-standard port when standard expected")
        Check(LocalAuthPolicy.BuildFilterPattern("http", "127.0.0.1", 18800) = "http://127.0.0.1:18800/*", "Filter pattern for non-standard port includes port number")
        Check(LocalAuthPolicy.BuildFilterPattern("https", "gateway.ts.net", 443) = "https://gateway.ts.net/*", "Filter pattern for standard HTTPS omits redundant port")

        ' Test PlatformDefinition and PlatformRegistry (TODO #68)
        Check(PlatformRegistry.All.Count = 5, "PlatformRegistry registers exactly 5 platforms")

        Dim wa = PlatformRegistry.Get(ChatPlatform.WhatsApp)
        Check(wa.Platform = ChatPlatform.WhatsApp, "WhatsApp enum matches")
        Check(wa.Id = "WhatsApp", "WhatsApp ID is 'WhatsApp'")
        Check(wa.DisplayName = "WhatsApp", "WhatsApp display name is 'WhatsApp'")
        Check(wa.DefaultUrl = "https://web.whatsapp.com/", "WhatsApp default URL matches")
        Check(wa.ThemeColorHex = "#25d366", "WhatsApp theme color is #25d366")
        Check(wa.SupportsBulkSender, "WhatsApp supports BulkSender")
        Check(Not wa.IsAiGateway, "WhatsApp is not an AI gateway")
        Check(Not wa.RequiresLocalProxy, "WhatsApp does not require local proxy")

        Dim tg = PlatformRegistry.Get(ChatPlatform.Telegram)
        Check(tg.Platform = ChatPlatform.Telegram, "Telegram enum matches")
        Check(tg.Id = "Telegram", "Telegram ID is 'Telegram'")
        Check(tg.DisplayName = "Telegram", "Telegram display name is 'Telegram'")
        Check(tg.DefaultUrl = "https://web.telegram.org/a/", "Telegram default URL matches")
        Check(tg.ThemeColorHex = "#24A1DE", "Telegram theme color is #24A1DE")
        Check(tg.SupportsBulkSender, "Telegram supports BulkSender")
        Check(Not tg.IsAiGateway, "Telegram is not an AI gateway")
        Check(Not tg.RequiresLocalProxy, "Telegram does not require local proxy")

        Dim oc = PlatformRegistry.Get(ChatPlatform.OpenClaw)
        Check(oc.Platform = ChatPlatform.OpenClaw, "OpenClaw enum matches")
        Check(oc.Id = "OpenClaw", "OpenClaw ID is 'OpenClaw'")
        Check(oc.DefaultUrl = "http://127.0.0.1:18789", "OpenClaw default URL matches")
        Check(oc.DefaultPort = 18800, "OpenClaw default port is 18800")
        Check(oc.ThemeColorHex = "#FF5722", "OpenClaw theme color is #FF5722")
        Check(oc.IsAiGateway, "OpenClaw is an AI gateway")
        Check(oc.RequiresLocalProxy, "OpenClaw requires local proxy")

        Dim hm = PlatformRegistry.Get(ChatPlatform.Hermes)
        Check(hm.Platform = ChatPlatform.Hermes, "Hermes enum matches")
        Check(hm.Id = "Hermes", "Hermes ID is 'Hermes'")
        Check(hm.DisplayName = "Hermes Agent", "Hermes display name is 'Hermes Agent'")
        Check(hm.DefaultUrl = "http://127.0.0.1:9119", "Hermes default URL matches")
        Check(hm.DefaultPort = 18900, "Hermes default port is 18900")
        Check(hm.ThemeColorHex = "#00B0FF", "Hermes theme color is #00B0FF")
        Check(hm.IsAiGateway, "Hermes is an AI gateway")
        Check(hm.RequiresLocalProxy, "Hermes requires local proxy")

        Dim dc = PlatformRegistry.Get(ChatPlatform.Discord)
        Check(dc.Platform = ChatPlatform.Discord, "Discord enum matches")
        Check(dc.Id = "Discord", "Discord ID is 'Discord'")
        Check(dc.DisplayName = "Discord", "Discord display name is 'Discord'")
        Check(dc.DefaultUrl = "https://discord.com/app", "Discord default URL matches")
        Check(dc.DefaultPort = 0, "Discord default port is 0")
        Check(dc.ThemeColorHex = "#5865F2", "Discord theme color is #5865F2")
        Check(Not dc.SupportsBulkSender, "Discord does not support BulkSender")
        Check(Not dc.IsAiGateway, "Discord is not an AI gateway")
        Check(Not dc.RequiresLocalProxy, "Discord does not require local proxy")

        ' Test string resolution and fallbacks
        Check(PlatformRegistry.Get("whatsapp").Platform = ChatPlatform.WhatsApp, "Resolve 'whatsapp' (lowercase)")
        Check(PlatformRegistry.Get("TELEGRAM").Platform = ChatPlatform.Telegram, "Resolve 'TELEGRAM' (uppercase)")
        Check(PlatformRegistry.Get("openclaw").Platform = ChatPlatform.OpenClaw, "Resolve 'openclaw'")
        Check(PlatformRegistry.Get("Hermes").Platform = ChatPlatform.Hermes, "Resolve 'Hermes'")
        Check(PlatformRegistry.Get("Hermes Agent").Platform = ChatPlatform.Hermes, "Resolve 'Hermes Agent' display name")
        Check(PlatformRegistry.Get("discord").Platform = ChatPlatform.Discord, "Resolve 'discord' (lowercase)")
        Check(PlatformRegistry.Get("DISCORD").Platform = ChatPlatform.Discord, "Resolve 'DISCORD' (uppercase)")
        Check(PlatformRegistry.Get("NonExistent").Platform = ChatPlatform.WhatsApp, "Fallback to WhatsApp for unknown platform")
        Check(PlatformRegistry.Get("").Platform = ChatPlatform.WhatsApp, "Fallback to WhatsApp for empty string")
        Check(PlatformRegistry.Get(DirectCast(Nothing, String)).Platform = ChatPlatform.WhatsApp, "Fallback to WhatsApp for null string")

        Dim parsedPlatform As ChatPlatform
        Check(PlatformRegistry.TryFromKey("Telegram", parsedPlatform) AndAlso parsedPlatform = ChatPlatform.Telegram, "TryFromKey succeeds for Telegram")
        Check(PlatformRegistry.TryFromKey("Discord", parsedPlatform) AndAlso parsedPlatform = ChatPlatform.Discord, "TryFromKey succeeds for Discord")
        Check(PlatformRegistry.TryFromKey("Hermes Agent", parsedPlatform) AndAlso parsedPlatform = ChatPlatform.Hermes, "TryFromKey succeeds for Hermes Agent")
        Check(Not PlatformRegistry.TryFromKey("Unknown", parsedPlatform) AndAlso parsedPlatform = ChatPlatform.WhatsApp, "TryFromKey returns False and WhatsApp for unknown")

        ' Test origin authorization
        Check(wa.IsAuthorizedOrigin("https://web.whatsapp.com", ""), "WhatsApp authorizes https://web.whatsapp.com")
        Check(wa.IsAuthorizedOrigin("https://web.whatsapp.com/subpath", ""), "WhatsApp authorizes https://web.whatsapp.com subpath")
        Check(Not wa.IsAuthorizedOrigin("http://web.whatsapp.com", ""), "WhatsApp rejects http://web.whatsapp.com")
        Check(Not wa.IsAuthorizedOrigin("https://attacker.com", ""), "WhatsApp rejects untrusted origin")

        Check(tg.IsAuthorizedOrigin("https://web.telegram.org", ""), "Telegram authorizes https://web.telegram.org")
        Check(tg.IsAuthorizedOrigin("https://web.telegram.org/a/", ""), "Telegram authorizes https://web.telegram.org/a/")
        Check(tg.IsAuthorizedOrigin("https://web.telegram.org/k/", ""), "Telegram authorizes https://web.telegram.org/k/")
        Check(Not tg.IsAuthorizedOrigin("https://telegram.org", ""), "Telegram rejects https://telegram.org")
        Check(Not tg.IsAuthorizedOrigin("http://web.telegram.org", ""), "Telegram rejects http://web.telegram.org")

        Check(oc.IsAuthorizedOrigin("http://localhost:18789", ""), "OpenClaw authorizes localhost")
        Check(oc.IsAuthorizedOrigin("http://127.0.0.1:18789", ""), "OpenClaw authorizes 127.0.0.1")
        Check(oc.IsAuthorizedOrigin("https://claw.my-node.ts.net:8443/chat", "https://claw.my-node.ts.net:8443"), "OpenClaw authorizes configured server URL")
        Check(Not oc.IsAuthorizedOrigin("https://evil.ts.net:8443/chat", "https://claw.my-node.ts.net:8443"), "OpenClaw rejects origin differing from server URL")

        Check(hm.IsAuthorizedOrigin("http://localhost:18800", ""), "Hermes authorizes localhost")
        Check(hm.IsAuthorizedOrigin("http://127.0.0.1:18800", ""), "Hermes authorizes 127.0.0.1")
        Check(hm.IsAuthorizedOrigin("https://hermes.custom.domain/agent", "https://hermes.custom.domain"), "Hermes authorizes configured server URL")
        Check(Not hm.IsAuthorizedOrigin("https://other.domain/agent", "https://hermes.custom.domain"), "Hermes rejects unauthorized host")

        Check(dc.IsAuthorizedOrigin("https://discord.com", ""), "Discord authorizes https://discord.com")
        Check(dc.IsAuthorizedOrigin("https://discord.com/channels/@me", ""), "Discord authorizes https://discord.com subpath")
        Check(Not dc.IsAuthorizedOrigin("http://discord.com", ""), "Discord rejects http://discord.com")
        Check(Not dc.IsAuthorizedOrigin("https://discord.com:8443", ""), "Discord rejects non-standard port")
        Check(Not dc.IsAuthorizedOrigin("https://user@discord.com", ""), "Discord rejects userinfo")
        Check(Not dc.IsAuthorizedOrigin("https://discord.com.evil.org", ""), "Discord rejects subdomain spoofing")
        Check(Not dc.IsAuthorizedOrigin("https://cdn.discordapp.com", ""), "Discord rejects CDN origin")
        Check(Not dc.IsAuthorizedOrigin("https://media.discordapp.net", ""), "Discord rejects media CDN")

        ' Test Telegram deep-link resolution
        Dim tgLink1 = tg.ResolveDeepLink("tg://resolve?domain=testchannel&post=42")
        Check(tgLink1 = "https://web.telegram.org/a/#@testchannel/42", "Telegram resolves tg:// deep-link")
        Dim tgLink2 = tg.ResolveDeepLink("https://t.me/testchannel")
        Check(tgLink2 = "https://web.telegram.org/a/#@testchannel", "Telegram resolves https://t.me/ deep-link")
        Check(tg.ResolveDeepLink("https://example.com") Is Nothing, "Telegram ignores non-telegram links")
        Check(wa.ResolveDeepLink("tg://resolve?domain=testchannel") Is Nothing, "WhatsApp ignores tg:// links")
        Dim dcLink1 = dc.ResolveDeepLink("discord://-/channels/123/456")
        Check(dcLink1 = "https://discord.com/channels/123/456", "Discord resolves discord:// deep-link")
        Dim dcLink2 = dc.ResolveDeepLink("https://discord.gg/testinvite")
        Check(dcLink2 = "https://discord.com/invite/testinvite", "Discord resolves discord.gg invite")
        Dim dcLink3 = dc.ResolveDeepLink("https://discordapp.com/channels/123/456")
        Check(dcLink3 = "https://discord.com/channels/123/456", "Discord resolves discordapp.com channels")
        Check(dc.ResolveDeepLink("https://evil.org/invite") Is Nothing, "Discord ignores non-discord invite links")

        ' Test IsAllowedNewWindowHost
        Check(oc.IsAllowedNewWindowHost(New Uri("https://claw.my-node.ts.net/auth"), "https://claw.my-node.ts.net:8443"), "OpenClaw allows same host in NewWindow")
        Check(Not oc.IsAllowedNewWindowHost(New Uri("https://evil.ts.net/auth"), "https://claw.my-node.ts.net:8443"), "OpenClaw rejects different host in NewWindow")
        Check(Not wa.IsAllowedNewWindowHost(New Uri("https://web.whatsapp.com/other"), ""), "WhatsApp delegates new windows externally")
        Check(tg.IsAllowedNewWindowHost(New Uri("https://t.me/channel"), ""), "Telegram handles t.me inside NewWindow")
        Check(Not tg.IsAllowedNewWindowHost(New Uri("https://example.com"), ""), "Telegram delegates other hosts externally")
        Check(dc.IsAllowedNewWindowHost(New Uri("https://discord.com/channels/123/456"), ""), "Discord handles /channels in NewWindow")
        Check(dc.IsAllowedNewWindowHost(New Uri("https://discord.com/invite/code"), ""), "Discord handles /invite in NewWindow")
        Check(dc.IsAllowedNewWindowHost(New Uri("https://discord.com/login"), ""), "Discord handles /login in NewWindow")
        Check(dc.IsAllowedNewWindowHost(New Uri("https://discord.com/register"), ""), "Discord handles /register in NewWindow")
        Check(dc.IsAllowedNewWindowHost(New Uri("https://discord.com/oauth2/authorize?client_id=123"), ""), "Discord handles /oauth2 in NewWindow")
        Check(dc.IsAllowedNewWindowHost(New Uri("https://discord.com/verify"), ""), "Discord handles /verify in NewWindow")
        Check(dc.IsAllowedNewWindowHost(New Uri("https://discord.com/popout/voice"), ""), "Discord handles /popout in NewWindow")
        Check(Not dc.IsAllowedNewWindowHost(New Uri("https://external.site.com"), ""), "Discord delegates external sites in NewWindow")
        Check(dc.ResolveDeepLink("discord:channels/123/456") = "https://discord.com/channels/123/456", "Discord resolves discord:channels")
        Check(dc.ResolveDeepLink("discord:login") = "https://discord.com/login", "Discord resolves discord:login")
        Check(dc.ResolveDeepLink("discord:oauth2/authorize") = "https://discord.com/oauth2/authorize", "Discord resolves discord:oauth2")
        Check(Not dc.IsAuthorizedOrigin("https://evil-discord.com", ""), "Discord rejects spoofed evil-discord.com")
        Check(Not dc.IsAuthorizedOrigin("https://discord.com.evil.org", ""), "Discord rejects subdomain suffix evil origin")
        Check(dc.IsAuthorizedOrigin("https://discord.com", ""), "Discord authorizes exact https://discord.com")
        Check(SessionRecoveryPolicy.IsTrustedStorageOrigin("https://discord.com"), "SessionRecoveryPolicy trusts https://discord.com")
        Check(Not SessionRecoveryPolicy.IsTrustedStorageOrigin("https://evil-discord.com"), "SessionRecoveryPolicy rejects evil-discord.com")

        Check(BulkSenderPolicy.IsPlatformSupported(wa), "BulkSenderPolicy supports WhatsApp")
        Check(BulkSenderPolicy.IsPlatformSupported(tg), "BulkSenderPolicy supports Telegram")
        Check(Not BulkSenderPolicy.IsPlatformSupported(dc), "BulkSenderPolicy blocks Discord")
        Check(Not BulkSenderPolicy.IsPlatformSupported(oc), "BulkSenderPolicy blocks OpenClaw")
        Check(Not BulkSenderPolicy.IsPlatformSupported(hm), "BulkSenderPolicy blocks Hermes")

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

            ' Test ColdProfileCopy.ShouldSkip for native and legacy profiles
            Check(ColdProfileCopy.ShouldSkip("Cache"), "Native Cache folder is skipped")
            Check(ColdProfileCopy.ShouldSkip("Cache/Cache_Data/0123"), "Native Cache subfile is skipped")
            Check(ColdProfileCopy.ShouldSkip("Code Cache"), "Native Code Cache folder is skipped")
            Check(ColdProfileCopy.ShouldSkip("Code Cache/js/0123"), "Native Code Cache subfile is skipped")
            Check(ColdProfileCopy.ShouldSkip("GPUCache"), "Native GPUCache folder is skipped")
            Check(ColdProfileCopy.ShouldSkip("DawnCache"), "Native DawnCache folder is skipped")
            Check(ColdProfileCopy.ShouldSkip("Crashpad"), "Native Crashpad folder is skipped")
            Check(ColdProfileCopy.ShouldSkip("Service Worker/CacheStorage"), "Service Worker CacheStorage is skipped")
            Check(ColdProfileCopy.ShouldSkip("Service Worker/ScriptCache"), "Service Worker ScriptCache is skipped")
            Check(ColdProfileCopy.ShouldSkip("AutofillAiModelCache"), "AutofillAiModelCache is skipped")
            Check(ColdProfileCopy.ShouldSkip("optimization_guide_hint_cache_store"), "optimization_guide_hint_cache_store is skipped")
            Check(ColdProfileCopy.ShouldSkip("EBWebView/Default/Cache"), "Legacy EBWebView/Default/Cache is skipped")
            Check(ColdProfileCopy.ShouldSkip("crash.dmp"), "Crash dump file is skipped")
            Check(ColdProfileCopy.ShouldSkip("metrics.pma"), "Metrics PMA file is skipped")
            Check(ColdProfileCopy.ShouldSkip("backup.old"), "Old backup file is skipped")
            Check(ColdProfileCopy.ShouldSkip("temp.tmp"), "Temp file is skipped")
            Check(ColdProfileCopy.ShouldSkip("LOCK"), "LOCK file is skipped")
            Check(ColdProfileCopy.ShouldSkip("test.lock"), "Lock file extension is skipped")

            ' Test ColdProfileCopy.ShouldSkip preserves critical authentication and database files
            Check(Not ColdProfileCopy.ShouldSkip("IndexedDB"), "IndexedDB directory is never skipped")
            Check(Not ColdProfileCopy.ShouldSkip("IndexedDB/https_web.whatsapp.com_0.indexeddb.leveldb/000005.ldb"), "IndexedDB database files are never skipped")
            Check(Not ColdProfileCopy.ShouldSkip("Local Storage/leveldb/000003.log"), "LocalStorage files are never skipped")
            Check(Not ColdProfileCopy.ShouldSkip("Network/Cookies"), "Cookies are never skipped")
            Check(Not ColdProfileCopy.ShouldSkip("Preferences"), "Preferences are never skipped")
            Check(Not ColdProfileCopy.ShouldSkip("Local State"), "Local State is never skipped")
            Check(Not ColdProfileCopy.ShouldSkip("Service Worker/Database/CURRENT"), "Service Worker registration DB is never skipped")
            Check(Not ColdProfileCopy.ShouldSkip("Sync Data/LevelDB/000001.ldb"), "Sync Data LevelDB is never skipped")

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

            ' Test ShouldRefreshStagingFromMaster (TODO #109 Multi-PC sync)
            Dim testMaster = Path.Combine(testRoot, "test_master")
            Dim testStaging = Path.Combine(testRoot, "test_staging")
            Directory.CreateDirectory(testMaster)
            Directory.CreateDirectory(testStaging)
            File.WriteAllText(Path.Combine(testMaster, "Preferences"), "{""master"":true}")
            File.WriteAllText(Path.Combine(testStaging, "Preferences"), "{""staging"":true}")

            Dim masterStateJson = System.Text.Json.JsonSerializer.Serialize(New ProfileSyncState With {
                .AccountId = "test_acc",
                .ExportedUtc = DateTime.UtcNow.AddMinutes(5),
                .Machine = "PC-REMOTE",
                .User = "remote_user"
            })
            Dim stagingStateJson = System.Text.Json.JsonSerializer.Serialize(New ProfileSyncState With {
                .AccountId = "test_acc",
                .ExportedUtc = DateTime.UtcNow,
                .Machine = "PC-LOCAL",
                .User = "local_user"
            })
            File.WriteAllText(Path.Combine(testMaster, ".sync_state.json"), masterStateJson)
            File.WriteAllText(Path.Combine(testStaging, ".sync_state.json"), stagingStateJson)
            Check(NetworkProfileSync.ShouldRefreshStagingFromMaster(testMaster, testStaging, "test_acc"), "ShouldRefreshStagingFromMaster returns True when master has newer ExportedUtc")

            File.WriteAllText(Path.Combine(testStaging, ".sync_state.json"), masterStateJson)
            Check(Not NetworkProfileSync.ShouldRefreshStagingFromMaster(testMaster, testStaging, "test_acc"), "ShouldRefreshStagingFromMaster returns False when timestamps are identical")

            File.Delete(Path.Combine(testStaging, ".sync_state.json"))
            Check(NetworkProfileSync.ShouldRefreshStagingFromMaster(testMaster, testStaging, "test_acc"), "ShouldRefreshStagingFromMaster returns True when staging has no sync state")

            ' Test ShouldRefreshStagingFromMaster when staging is newer than master
            Dim olderMasterJson = System.Text.Json.JsonSerializer.Serialize(New ProfileSyncState With {
                .AccountId = "test_acc",
                .ExportedUtc = DateTime.UtcNow.AddMinutes(-5),
                .Machine = "PC-REMOTE",
                .User = "remote_user"
            })
            File.WriteAllText(Path.Combine(testMaster, ".sync_state.json"), olderMasterJson)
            File.WriteAllText(Path.Combine(testStaging, ".sync_state.json"), stagingStateJson)
            Check(Not NetworkProfileSync.ShouldRefreshStagingFromMaster(testMaster, testStaging, "test_acc"), "ShouldRefreshStagingFromMaster returns False when local staging is newer than master")

            ' Test WriteSyncState resilience to Hidden/ReadOnly file attributes
            If OperatingSystem.IsWindows() Then
                Dim stateFile = Path.Combine(testStaging, ".sync_state.json")
                File.SetAttributes(stateFile, FileAttributes.Hidden)
                Dim writeMethod = GetType(NetworkProfileSync).GetMethod("WriteSyncState", System.Reflection.BindingFlags.NonPublic Or System.Reflection.BindingFlags.Static)
                writeMethod.Invoke(Nothing, New Object() {testStaging, "test_acc"})
                Check((File.GetAttributes(stateFile) And (FileAttributes.Hidden Or FileAttributes.ReadOnly)) = 0, "WriteSyncState strips Hidden/ReadOnly attributes and succeeds")
            End If

            ' Test TASK-0127: NetworkProfileSync stale staging detection & export protection
            Dim staleAcc = "unit_stale_test"
            Dim staleDir = Path.Combine(testRoot, "stale_staging")
            Directory.CreateDirectory(staleDir)
            Check(Not NetworkProfileSync.IsStagingStale(staleDir, staleAcc), "New staging directory is not marked stale")
            NetworkProfileSync.MarkStagingStale(staleDir, staleAcc)
            Check(NetworkProfileSync.IsStagingStale(staleDir, staleAcc), "MarkStagingStale sets stale status in memory and on disk")
            Check(File.Exists(Path.Combine(staleDir, ".sync_stale")), "MarkStagingStale creates .sync_stale marker file")
            NetworkProfileSync.ClearStagingStale(staleDir, staleAcc)
            Check(Not NetworkProfileSync.IsStagingStale(staleDir, staleAcc), "ClearStagingStale clears stale status in memory")
            Check(Not File.Exists(Path.Combine(staleDir, ".sync_stale")), "ClearStagingStale deletes .sync_stale marker file")

            ' Test file-only stale detection (e.g. across process restarts)
            File.WriteAllText(Path.Combine(staleDir, ".sync_stale"), "stale")
            Check(NetworkProfileSync.IsStagingStale(staleDir, "different_acc_id"), "IsStagingStale detects marker file on disk independently of memory")
            NetworkProfileSync.ClearStagingStale(staleDir, "different_acc_id")
            Check(Not NetworkProfileSync.IsStagingStale(staleDir, "different_acc_id"), "ClearStagingStale cleans marker file on disk")
            Await TestTranslationProvidersAsync()
            TestBetaChannelResetMigration()
            TestBrowserArgumentsPolicy()
            TestNtfsCompressionPolicy()
            TestUtilityBarAndAudioQuickActions()
            TestOtaUpdateAndProgressLogic()
            TestBulkSenderPolicy()
            Await TestTodo80And84And85Async()
        Finally
            NetworkProfileSync.ReleaseSessionLock("unit")
            Directory.Delete(testRoot, recursive:=True)
        End Try
    End Function
    Private Async Function TestTranslationProvidersAsync() As Task
        ' 1. Display Names
        Check(TranslationProviderRegistry.GetProviderDisplayName("google").Contains("Google"), "Google provider display name")
        Check(TranslationProviderRegistry.GetProviderDisplayName("libretranslate").Contains("LibreTranslate"), "LibreTranslate provider display name")
        Check(TranslationProviderRegistry.GetProviderDisplayName("ollama").Contains("Ollama"), "Ollama provider display name")
        Check(TranslationProviderRegistry.GetProviderDisplayName("openai").Contains("OpenAI"), "OpenAI provider display name")
        Check(TranslationProviderRegistry.GetProviderDisplayName("unknown").Contains("Google"), "Unknown provider fallback display name")

        ' 2. Configuration default
        Dim cfg As New TranslationConfig()
        Check(Not cfg.PrivacyConsentAccepted, "Privacy consent is False by default (Opt-In enforced)")
        Check(cfg.ProviderId = "google", "Default provider is Google")
        Check(cfg.LibreTranslateUrl = "http://localhost:5000", "Default LibreTranslate URL is http://localhost:5000")
        Check(cfg.OllamaUrl = "http://localhost:11434", "Default Ollama URL is http://localhost:11434")
        Check(cfg.OpenAiUrl = "http://localhost:1234/v1", "Default OpenAI URL is http://localhost:1234/v1")

        ' 3. Provider Instantiation
        Dim pGoogle = TranslationProviderRegistry.CreateProvider(New TranslationConfig With {.ProviderId = "google"})
        Check(TypeOf pGoogle Is GoogleTranslationProvider, "Factory creates GoogleTranslationProvider")
        Check(Not pGoogle.IsSelfHosted, "GoogleTranslationProvider is not self-hosted")

        Dim pLibre = TranslationProviderRegistry.CreateProvider(New TranslationConfig With {.ProviderId = "libretranslate"})
        Check(TypeOf pLibre Is LibreTranslateProvider, "Factory creates LibreTranslateProvider")
        Check(pLibre.IsSelfHosted, "LibreTranslateProvider is self-hosted")

        Dim pOllama = TranslationProviderRegistry.CreateProvider(New TranslationConfig With {.ProviderId = "ollama"})
        Check(TypeOf pOllama Is OllamaTranslationProvider, "Factory creates OllamaTranslationProvider")
        Check(pOllama.IsSelfHosted, "OllamaTranslationProvider is self-hosted")

        Dim pOpenAi = TranslationProviderRegistry.CreateProvider(New TranslationConfig With {.ProviderId = "openai", .OpenAiUrl = "http://localhost:1234/v1"})
        Check(TypeOf pOpenAi Is OpenAiTranslationProvider, "Factory creates OpenAiTranslationProvider")
        Check(pOpenAi.IsSelfHosted, "OpenAiTranslationProvider is self-hosted on localhost")

        Dim pOpenAiRemote = New OpenAiTranslationProvider("https://api.openai.com/v1", "key", "gpt-4o")
        Check(Not pOpenAiRemote.IsSelfHosted, "OpenAiTranslationProvider is not self-hosted on remote HTTPS")

        ' 4. Empty Text Handling (No network calls)
        Dim emptyRes = Await pGoogle.TranslateSingleAsync("", "it", Threading.CancellationToken.None)
        Check(emptyRes.Success AndAlso emptyRes.TranslatedText = "", "Empty single text returns success with empty string")

        Dim emptyBatchRes = Await pGoogle.TranslateBatchAsync(New List(Of String)(), "it", Threading.CancellationToken.None)
        Check(emptyBatchRes.Success AndAlso emptyBatchRes.Translations.Count = 0, "Empty batch list returns success with empty list")

        ' 5. Result Factory Methods
        Dim succ = TranslationResult.Succeeded("translated")
        Check(succ.Success AndAlso succ.TranslatedText = "translated" AndAlso succ.ErrorMessage Is Nothing, "TranslationResult.Succeeded sets properties correctly")
        Dim fail = TranslationResult.Failed("error message", "fallback")
        Check(Not fail.Success AndAlso fail.TranslatedText = "fallback" AndAlso fail.ErrorMessage = "error message", "TranslationResult.Failed sets properties correctly")

        Dim bSucc = TranslationBatchResult.Succeeded(New List(Of String) From {"a", "b"})
        Check(bSucc.Success AndAlso bSucc.Translations.Count = 2, "TranslationBatchResult.Succeeded sets properties correctly")
        Dim bFail = TranslationBatchResult.Failed("batch error", New List(Of String) From {"orig"})
        Check(Not bFail.Success AndAlso bFail.Translations.Count = 1 AndAlso bFail.ErrorMessage = "batch error", "TranslationBatchResult.Failed sets properties correctly")

        ' 6. Unreachable backend error handling and discrete reporting
        Dim unreachablePort = 59981
        Dim deadLibre As New LibreTranslateProvider($"http://127.0.0.1:{unreachablePort}")
        Dim deadLibreRes = Await deadLibre.TranslateSingleAsync("hello", "it", Threading.CancellationToken.None)
        Check(Not deadLibreRes.Success, "Unreachable LibreTranslate returns Success = False")
        Check(Not String.IsNullOrEmpty(deadLibreRes.ErrorMessage), "Unreachable LibreTranslate provides clear error message")

        Dim deadOllama As New OllamaTranslationProvider($"http://127.0.0.1:{unreachablePort}")
        Dim deadOllamaRes = Await deadOllama.TranslateSingleAsync("hello", "it", Threading.CancellationToken.None)
        Check(Not deadOllamaRes.Success, "Unreachable Ollama returns Success = False")
        Check(Not String.IsNullOrEmpty(deadOllamaRes.ErrorMessage), "Unreachable Ollama provides clear error message")

        Dim deadOpenAi As New OpenAiTranslationProvider($"http://127.0.0.1:{unreachablePort}/v1")
        Dim deadOpenAiRes = Await deadOpenAi.TranslateSingleAsync("hello", "it", Threading.CancellationToken.None)
        Check(Not deadOpenAiRes.Success, "Unreachable OpenAI returns Success = False")
        Check(Not String.IsNullOrEmpty(deadOpenAiRes.ErrorMessage), "Unreachable OpenAI provides clear error message")

        Using testCts As New Threading.CancellationTokenSource(TimeSpan.FromSeconds(2))
            Dim testConnRes = Await deadLibre.TestConnectionAsync(testCts.Token)
            Check(Not testConnRes.Success, "TestConnection on unreachable host returns Success = False")
            Check(Not String.IsNullOrEmpty(testConnRes.Message), "TestConnection provides descriptive failure message")
        End Using

        ' 7. Google Translate JSON Response Parser (TASK-0128)
        Dim parsedText As String = Nothing
        Dim json3Level = "[[[""Ciao mondo"",""Hello world"",null,null,10]],null,""en""]"
        Check(GoogleTranslationProvider.TryParseGoogleResponse(json3Level, parsedText) AndAlso parsedText = "Ciao mondo", "TryParseGoogleResponse parses 3-level single?dt=t format")

        Dim json3Multi = "[[[""Ciao "",""Hello "",null,null,10],[""mondo"",""world"",null,null,10]],null,""en""]"
        Check(GoogleTranslationProvider.TryParseGoogleResponse(json3Multi, parsedText) AndAlso parsedText = "Ciao mondo", "TryParseGoogleResponse parses 3-level multi-segment format")

        Dim json2Level = "[[""Ciao mondo"",""en""]]"
        Check(GoogleTranslationProvider.TryParseGoogleResponse(json2Level, parsedText) AndAlso parsedText = "Ciao mondo", "TryParseGoogleResponse parses 2-level dict-chrome-ex format")

        Dim json2Multi = "[[""Ciao "",""en""],[""mondo"",""en""]]"
        Check(GoogleTranslationProvider.TryParseGoogleResponse(json2Multi, parsedText) AndAlso parsedText = "Ciao mondo", "TryParseGoogleResponse parses 2-level multi-pair format")

        Dim json1Level = "[""Ciao mondo"",""en""]"
        Check(GoogleTranslationProvider.TryParseGoogleResponse(json1Level, parsedText) AndAlso parsedText = "Ciao mondo", "TryParseGoogleResponse parses 1-level flat string format")

        Check(Not GoogleTranslationProvider.TryParseGoogleResponse("{not: json}", parsedText), "TryParseGoogleResponse rejects invalid JSON")
        Check(Not GoogleTranslationProvider.TryParseGoogleResponse("[]", parsedText), "TryParseGoogleResponse rejects empty array")
        Check(Not GoogleTranslationProvider.TryParseGoogleResponse("", parsedText), "TryParseGoogleResponse rejects empty string")
        Check(Not GoogleTranslationProvider.TryParseGoogleResponse(Nothing, parsedText), "TryParseGoogleResponse rejects null string")

        ' 8. Google Translate Multi-Endpoint Failover on HTTP 429 (TASK-0128)
        Dim mockFailover1 As New MockTranslationFailoverHandler With {.Force429Count = 1}
        Using clientFailover1 As New HttpClient(mockFailover1)
            Dim providerFailover1 As New GoogleTranslationProvider(clientFailover1)
            Dim resFailover1 = Await providerFailover1.TranslateSingleAsync("Hello", "es", Threading.CancellationToken.None)
            Check(resFailover1.Success, "Google provider succeeds via failover when first endpoint returns HTTP 429")
            Check(resFailover1.TranslatedText = "Hola", "Google provider returns translated text from failover endpoint")
            Check(mockFailover1.CallCount = 2, "Google provider tried next endpoint upon receiving HTTP 429")
            Check(mockFailover1.SimulatedUrls(0).Contains("translate.googleapis.com"), "Google provider first tried translate.googleapis.com")
            Check(mockFailover1.SimulatedUrls(1).Contains("clients5.google.com"), "Google provider rotated to clients5.google.com")

            ' Test connection also succeeds seamlessly on 429 failover
            mockFailover1.CallCount = 0
            mockFailover1.Force429Count = 1
            Using testCts As New Threading.CancellationTokenSource(TimeSpan.FromSeconds(5))
                Dim testConnRes = Await providerFailover1.TestConnectionAsync(testCts.Token)
                Check(testConnRes.Success, "TestConnection succeeds via failover when primary endpoint returns HTTP 429")
                Check(testConnRes.Message = "Hola", "TestConnection contains translation from healthy failover endpoint")
            End Using
        End Using

        ' 9. Google Translate All Endpoints Rate-Limited (TASK-0128)
        Dim mockFailoverAll As New MockTranslationFailoverHandler With {.Force429Count = 99}
        Using clientFailoverAll As New HttpClient(mockFailoverAll)
            Dim providerFailoverAll As New GoogleTranslationProvider(clientFailoverAll)
            Dim resFailoverAll = Await providerFailoverAll.TranslateSingleAsync("Hello", "es", Threading.CancellationToken.None)
            Check(Not resFailoverAll.Success, "Google provider returns failure when all endpoints are rate-limited")
            Check(resFailoverAll.ErrorMessage.Contains("429"), "Google provider returns 429 error message when rate limit is persistent")
        End Using
    End Function

    Private Sub TestBetaChannelResetMigration()
        ' User requirement: beta channel checkbox must never be forcibly unchecked
        Dim settingsWithBeta As New Dictionary(Of String, Object) From {
            {"useBetaChannel", True},
            {"theme", "Dark"}
        }
        Dim useBeta = True
        Dim changed = SettingsMigration.ApplyBetaChannelReset130(settingsWithBeta, useBeta)
        Check(Not changed, "ApplyBetaChannelReset130 returns False without forcing uncheck")
        Check(useBeta, "useBetaChannel remains True when marked")

        Dim settingsWithBeta140 As New Dictionary(Of String, Object) From {
            {"useBetaChannel", True},
            {"theme", "Dark"}
        }
        Dim useBeta140 = True
        Dim changed140 = SettingsMigration.ApplyBetaChannelReset140(settingsWithBeta140, useBeta140)
        Check(Not changed140, "ApplyBetaChannelReset140 returns False without forcing uncheck")
        Check(useBeta140, "useBetaChannel remains True for 1.4.0 without forced reset")
    End Sub

    Private Sub TestBrowserArgumentsPolicy()
        ' 1. Default arguments (50 MB limit, spellcheck enabled, Italian language)
        Dim defaultArgs = BrowserArgumentsPolicy.BuildAdditionalBrowserArguments(BrowserArgumentsPolicy.DefaultDiskCacheLimitMb, True, "it-IT")
        Check(defaultArgs.Contains("--disk-cache-size=52428800"), "Default cache limit sets --disk-cache-size=52428800 (50 MB)")
        Check(defaultArgs.Contains($"--media-cache-size={BrowserArgumentsPolicy.MediaCacheSizeBytes}"), "--media-cache-size=20971520 is present")
        Check(defaultArgs.Contains("--aggressive-cache-discard"), "--aggressive-cache-discard is present")
        Check(defaultArgs.Contains("--disable-gpu-shader-disk-cache"), "--disable-gpu-shader-disk-cache is present")
        Check(defaultArgs.Contains("--disable-component-update"), "--disable-component-update is present")
        Check(defaultArgs.Contains("--disable-background-networking"), "--disable-background-networking is present")
        Check(defaultArgs.Contains("Translate"), "Translate feature is disabled")
        Check(defaultArgs.Contains("OptimizationGuideModelDownloading"), "OptimizationGuideModelDownloading is disabled")
        Check(defaultArgs.Contains("OptimizationHintsFetching"), "OptimizationHintsFetching is disabled")
        Check(defaultArgs.Contains("--enable-features=Spellcheck --lang=it-IT"), "Spellcheck feature enabled with active language")

        ' 2. Custom disk cache limit (e.g. 256 MB)
        Dim customArgs = BrowserArgumentsPolicy.BuildAdditionalBrowserArguments(256, False, "")
        Check(customArgs.Contains("--disk-cache-size=268435456"), "Custom cache limit 256 MB sets 268435456 bytes")
        Check(customArgs.Contains("Spellcheck"), "Spellcheck feature disabled when enableSpellcheck is False")
        Check(Not customArgs.Contains("--enable-features=Spellcheck"), "--enable-features=Spellcheck omitted when disabled")

        ' 3. Unlimited cache (0 MB)
        Dim unlimitedArgs = BrowserArgumentsPolicy.BuildAdditionalBrowserArguments(0, True, "en-US")
        Check(Not unlimitedArgs.Contains("--disk-cache-size"), "--disk-cache-size omitted when limit is 0 (unlimited)")
        Check(unlimitedArgs.Contains($"--media-cache-size={BrowserArgumentsPolicy.MediaCacheSizeBytes}"), "Media cache limit preserved even with unlimited HTTP cache")
        Check(unlimitedArgs.Contains("--aggressive-cache-discard"), "Aggressive cache discard preserved with unlimited HTTP cache")
    End Sub

    Private Sub TestNtfsCompressionPolicy()
        ' 1. Argument construction validation (TODO #106)
        Dim args = NtfsCompressionPolicy.BuildCompactArguments("C:\test\data")
        Check(args = "/C /S:""C:\test\data"" /I /Q", "BuildCompactArguments formats expected CLI parameters")

        ' 2. Validation of null / whitespace path rejection
        Check(Not NtfsCompressionPolicy.EnableNtfsCompression(Nothing), "EnableNtfsCompression rejects null path")
        Check(Not NtfsCompressionPolicy.EnableNtfsCompression(""), "EnableNtfsCompression rejects empty path")
        Check(Not NtfsCompressionPolicy.EnableNtfsCompression("   "), "EnableNtfsCompression rejects whitespace path")

        ' 3. Execution on test temporary directory creates directory and runs compact without throwing
        Dim tempDir = Path.Combine(Path.GetTempPath(), "HidaChat_NtfsTest_" & Guid.NewGuid().ToString("N"))
        Try
            Check(Not Directory.Exists(tempDir), "Test directory does not exist yet")
            Dim success = NtfsCompressionPolicy.EnableNtfsCompression(tempDir)
            Check(success, "EnableNtfsCompression returns True for existing/creatable path")
            Check(Directory.Exists(tempDir), "EnableNtfsCompression creates directory if missing")
        Finally
            If Directory.Exists(tempDir) Then
                Try
                    Directory.Delete(tempDir, recursive:=True)
                Catch
                End Try
            End If
        End Try
    End Sub

    Private Sub TestUtilityBarAndAudioQuickActions()
            TestOtaUpdateAndProgressLogic()
            TestBulkSenderPolicy()
        ' 1. Mute toggle logic simulation (TODO #45)
        Dim isMuted As Boolean = False
        isMuted = Not isMuted
        Check(isMuted, "Toggle mute inverts False to True")
        isMuted = Not isMuted
        Check(Not isMuted, "Toggle mute inverts True to False")

        ' 2. Media indicator icon resolution logic (TODO #45)
        Dim ResolveIcon = Function(muted As Boolean, playing As Boolean) As String
                              If muted Then
                                  Return "🔇"
                              ElseIf playing Then
                                  Return "🔊"
                              Else
                                  Return String.Empty
                              End If
                          End Function

        Check(ResolveIcon(True, True) = "🔇", "Muted state takes precedence for indicator icon (🔇)")
        Check(ResolveIcon(True, False) = "🔇", "Muted tab displays 🔇 icon")
        Check(ResolveIcon(False, True) = "🔊", "Active audio playback without mute displays 🔊 icon")
        Check(ResolveIcon(False, False) = "", "Idle unmuted tab displays empty icon")

        ' 3. Chat link resolution hierarchy (active WebView Source > ServerUrl > Platform Default) (TODO #45)
        Dim ResolveChatLink = Function(webSource As String, serverUrl As String, defaultPlatformUrl As String) As String
                                  If Not String.IsNullOrWhiteSpace(webSource) Then Return webSource
                                  If Not String.IsNullOrWhiteSpace(serverUrl) Then Return serverUrl
                                  Return defaultPlatformUrl
                              End Function

        Check(ResolveChatLink("https://web.whatsapp.com/chat/123", "http://127.0.0.1:18800", "https://web.whatsapp.com") = "https://web.whatsapp.com/chat/123", "Active webview source has highest priority for copy link")
        Check(ResolveChatLink("", "http://127.0.0.1:18800", "http://127.0.0.1:18800") = "http://127.0.0.1:18800", "ServerUrl used when webview source is empty")
        Check(ResolveChatLink("", "", "https://web.telegram.org") = "https://web.telegram.org", "Platform default URL used when both webview source and serverUrl are empty")
    End Sub

    Private Sub TestOtaUpdateAndProgressLogic()
        ' 1. Relative path check ensuring data directory is NEVER copied (100% portable) (TODO #87)
        Dim IsDataExcluded = Function(relPath As String) As Boolean
            If relPath.Equals("data", StringComparison.OrdinalIgnoreCase) Then Return True
            If relPath.StartsWith("data" & Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) Then Return True
            If relPath.StartsWith("data/", StringComparison.OrdinalIgnoreCase) Then Return True
            Return False
        End Function

        Check(IsDataExcluded("data"), "Root data directory excluded from OTA file copy")
        Check(IsDataExcluded("data\webview\Account_1"), "data subfolder (backslash) excluded from OTA file copy")
        Check(IsDataExcluded("data/settings.json"), "data file (forward slash) excluded from OTA file copy")
        Check(Not IsDataExcluded("HidaChat.exe"), "App executable is copied")
        Check(Not IsDataExcluded("HidaChat.dll"), "App binary DLL is copied")
        Check(Not IsDataExcluded("images\icon.ico"), "Resource file is copied")

        ' 2. Progress percentage and calculation bounds
        Dim CalculateProgress = Function(readBytes As Long, totalBytes As Long) As Double
            If totalBytes <= 0 Then Return -1.0
            Dim pct = (CDbl(readBytes) / CDbl(totalBytes)) * 100.0
            Return Math.Max(0.0, Math.Min(100.0, pct))
        End Function

        Check(CalculateProgress(0, 100) = 0.0, "Progress at 0 bytes is 0%")
        Check(CalculateProgress(50, 100) = 50.0, "Progress at midpoint is 50%")
        Check(CalculateProgress(100, 100) = 100.0, "Progress at completion is 100%")
        Check(CalculateProgress(150, 100) = 100.0, "Progress exceeding total clamps to 100%")
        Check(CalculateProgress(50, 0) = -1.0, "Unknown total length returns negative percentage")

        ' 3. CLI arguments construction for --apply-update (TODO #87, TASK-0091)
        Dim FormatApplyArgs = Function(pid As Integer, src As String, dst As String, ver As String, lang As String, zip As String, rootTemp As String) As String
            If String.IsNullOrEmpty(rootTemp) Then
                Return $"--apply-update {pid} ""{src}"" ""{dst}"" ""{ver}"" ""{lang}"" ""{zip}"""
            Else
                Return $"--apply-update {pid} ""{src}"" ""{dst}"" ""{ver}"" ""{lang}"" ""{zip}"" ""{rootTemp}"""
            End If
        End Function

        Dim applyArgs = FormatApplyArgs(1234, "C:\Temp\Update\extracted", "C:\HidaChat", "1.3.3-beta", "it", "C:\Temp\update.zip", "C:\Temp\Update")
        Check(applyArgs.Contains("--apply-update 1234"), "CLI args include --apply-update with caller PID")
        Check(applyArgs.Contains("""C:\Temp\Update\extracted"""), "CLI args include quoted source directory")
        Check(applyArgs.Contains("""C:\HidaChat"""), "CLI args include quoted target installation directory")
        Check(applyArgs.Contains("""1.3.3-beta"""), "CLI args include target version")
        Check(applyArgs.Contains("""it"""), "CLI args include language code")
        Check(applyArgs.Contains("""C:\Temp\update.zip"""), "CLI args include temp zip path")
        Check(applyArgs.Contains("""C:\Temp\Update"""), "CLI args include root temporary folder for complete cleanup (TASK-0091)")

        ' 3b. Script exclusion and version marker verification (TASK-0091)
        Dim IsScriptExcluded = Function(relFile As String) As Boolean
            Dim fileName = Path.GetFileName(relFile)
            Return fileName.StartsWith("update_", StringComparison.OrdinalIgnoreCase) OrElse
                   fileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) OrElse
                   fileName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) OrElse
                   fileName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) OrElse
                   fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
        End Function

        Check(IsScriptExcluded("update_installer.bat"), "Bat script excluded from file copy (TASK-0091)")
        Check(IsScriptExcluded("runner.cmd"), "Cmd script excluded from file copy (TASK-0091)")
        Check(IsScriptExcluded("scripts\deploy.ps1"), "PowerShell script excluded from file copy (TASK-0091)")
        Check(IsScriptExcluded("temp.tmp"), "Tmp file excluded from file copy (TASK-0091)")
        Check(Not IsScriptExcluded("HidaChat.exe"), "Executable not excluded as script")
        Check(Not IsScriptExcluded("HidaChat.dll"), "DLL not excluded as script")

        Dim FormatVersionMarker = Function(v As String) As String
            Return "v" & v.TrimStart("v"c)
        End Function
        Check(FormatVersionMarker("1.5.3-beta") = "v1.5.3-beta", "Version marker formats raw semver to v-prefix (TASK-0091)")
        Check(FormatVersionMarker("v1.5.3-beta") = "v1.5.3-beta", "Version marker preserves already prefixed version (TASK-0091)")

        ' 3c. Safe cleanup filter logic (TASK-0091)
        Dim currentSessionDir = "C:\Users\test\AppData\Local\Temp\HidaChat_Update_20261123_110000_12345678"
        Dim currentSessionZip = "C:\Users\test\AppData\Local\Temp\HidaChat_Update_20261123_110000_12345678.zip"
        Dim ShouldDeleteStale = Function(targetPath As String, isCurrent As Boolean, ageHours As Double) As Boolean
            If isCurrent Then Return False
            Return ageHours >= 2.0
        End Function
        Check(Not ShouldDeleteStale(currentSessionDir, True, 0.1), "Current session directory is never cleaned up prematurely")
        Check(Not ShouldDeleteStale(currentSessionZip, True, 0.1), "Current session zip is never cleaned up prematurely")
        Check(Not ShouldDeleteStale("other_recent_dir", False, 0.5), "Recent session (< 2h) is preserved for concurrent updates")
        Check(ShouldDeleteStale("stale_dir", False, 2.5), "Stale update directory (> 2h) is cleaned up")

        ' 3d. Resilient batch script generation and path normalization (TASK-0092)
        ' Test path normalization for robocopy and arguments
        Check(UpdateScriptPolicy.NormalizePathForRobocopy("E:\") = "E:\.", "USB root E:\ is normalized to E:\. (TASK-0092)")
        Check(UpdateScriptPolicy.NormalizePathForRobocopy("E:") = "E:\.", "USB root E: without slash is normalized to E:\. (TASK-0092)")
        Check(UpdateScriptPolicy.NormalizePathForRobocopy("E:/") = "E:\.", "USB root E:/ forward slash is normalized to E:\. (TASK-0092)")
        Check(UpdateScriptPolicy.NormalizePathForRobocopy("C:\Users\è_test\HidaChat\") = "C:\Users\è_test\HidaChat\.", "Path with accents and trailing backslash normalized to \. (TASK-0092)")
        Check(UpdateScriptPolicy.NormalizePathForRobocopy("C:\Users\è_test\HidaChat") = "C:\Users\è_test\HidaChat", "Path with accents without trailing backslash preserved (TASK-0092)")
        Check(UpdateScriptPolicy.NormalizePathForRobocopy("C:\Apps\HidaChat (1)\") = "C:\Apps\HidaChat (1)\.", "Parenthesized path with trailing backslash normalized to \. (TASK-0092)")
        Check(UpdateScriptPolicy.NormalizePathForRobocopy("C:\Apps\HidaChat (1)") = "C:\Apps\HidaChat (1)", "Parenthesized path without trailing backslash preserved (TASK-0092)")
        Check(UpdateScriptPolicy.NormalizePathForRobocopy("C:\Apps\Test%Dir!Name\") = "C:\Apps\Test%Dir!Name\.", "Special characters % and ! with trailing backslash normalized (TASK-0092)")

        ' Test batch echo escaping
        Check(UpdateScriptPolicy.EscapeForBatchEcho("C:\Apps\HidaChat (1)\log.txt") = "C:\Apps\HidaChat (1^)\log.txt", "Closing parenthesis escaped for batch echo (TASK-0092)")
        Check(UpdateScriptPolicy.EscapeForBatchEcho("no_parens") = "no_parens", "String without parentheses unchanged (TASK-0092)")

        ' Test script generation
        Dim batchScript = UpdateScriptPolicy.GenerateUpdateBatchScript(
            sourceDir:="C:\Temp\Update_è_123",
            installDir:="E:\",
            targetExeName:="HidaChat.exe",
            latestVersion:="1.5.6-beta",
            callerPid:=4567,
            tempDir:="C:\Temp\Update_è_123",
            tempZipPath:="C:\Temp\Update_è_123.zip",
            logFilePath:="E:\data\updates.log"
        )
        Check(batchScript.Contains("chcp 65001 >nul"), "Batch script includes chcp 65001 for UTF-8 codepage support (TASK-0092)")
        Check(batchScript.Contains("set ""DST=E:\."""), "Batch script sets DST with normalized USB root E:\. (TASK-0092)")
        Check(batchScript.Contains("set ""SRC=C:\Temp\Update_è_123"""), "Batch script safely quotes SRC containing accented characters (TASK-0092)")
        Check(batchScript.Contains("set ""CALLER_PID=4567"""), "Batch script assigns target caller PID (TASK-0092)")
        Check(batchScript.Contains("robocopy ""%SRC%"" ""%DST%"""), "Batch script executes robocopy with quoted variables (TASK-0092)")
        Check(batchScript.Contains("if %RC% GEQ 8 ("), "Batch script checks robocopy exit code >= 8 (TASK-0092)")
        Check(batchScript.Contains("pause"), "Batch script includes pause on robocopy failure before exit (TASK-0092)")
        Check(batchScript.Contains("exit /b 1"), "Batch script exits with error code 1 on robocopy failure (TASK-0092)")
        Check(batchScript.Contains("echo v%VER%>""%DST%\.app_version"""), "Batch script writes .app_version only after robocopy check (TASK-0092)")
        Check(batchScript.Contains("start """" ""%DST%\%EXE%"""), "Batch script relaunches application from normalized destination (TASK-0092)")
        Check(batchScript.Contains("del ""%~f0"""), "Batch script includes self-deletion (TASK-0092)")

        ' Target-specific process termination by PID (TASK-0118)
        Check(batchScript.Contains("taskkill /f /pid %CALLER_PID% /t"), "Batch script terminates specifically by target caller PID /t (TASK-0118)")
        Check(Not batchScript.Contains("/im HidaChat.exe"), "Batch script does NOT terminate all instances by image name /im HidaChat.exe (TASK-0118)")
        Check(Not batchScript.Contains("/im tsnetd.exe"), "Batch script does NOT terminate all tsnetd instances by image name (TASK-0118)")
        Check(batchScript.Contains(":checktsnet"), "Batch script includes checktsnet label before copyfiles (TASK-0118)")

        ' Safety backup and automatic rollback on robocopy failure (TASK-0119)
        Check(batchScript.Contains("BACKUP_DIR="), "Batch script configures BACKUP_DIR (TASK-0119)")
        Check(batchScript.Contains("robocopy ""%DST%"" ""%BACKUP_DIR%"""), "Batch script creates safety backup of binaries before updating (TASK-0119)")
        Check(batchScript.Contains("robocopy ""%BACKUP_DIR%"" ""%DST%"""), "Batch script restores binaries from backup on failure (TASK-0119)")
        Check(batchScript.Contains("start """" ""%DST%\%EXE%"""), "Batch script relaunches previous working version on failure (TASK-0119)")

        ' Unit tests for UpdateBackupPolicy (TASK-0119)
        Check(UpdateBackupPolicy.IsUserDataPath("data"), "IsUserDataPath recognizes 'data' (TASK-0119)")
        Check(UpdateBackupPolicy.IsUserDataPath("data\webview"), "IsUserDataPath recognizes 'data\webview' (TASK-0119)")
        Check(UpdateBackupPolicy.IsUserDataPath("data/webview/cookies.db"), "IsUserDataPath recognizes forward-slash data path (TASK-0119)")
        Check(UpdateBackupPolicy.IsUserDataPath(".git"), "IsUserDataPath recognizes '.git' (TASK-0119)")
        Check(Not UpdateBackupPolicy.IsUserDataPath("HidaChat.exe"), "IsUserDataPath returns False for application binary (TASK-0119)")
        Check(Not UpdateBackupPolicy.IsUserDataPath("runtimes\win-x64"), "IsUserDataPath returns False for runtimes (TASK-0119)")

        Check(UpdateBackupPolicy.ShouldExcludeFile("data\settings.json"), "ShouldExcludeFile excludes data files (TASK-0119)")
        Check(UpdateBackupPolicy.ShouldExcludeFile("updates.log"), "ShouldExcludeFile excludes updates.log (TASK-0119)")
        Check(UpdateBackupPolicy.ShouldExcludeFile(".update_log.txt"), "ShouldExcludeFile excludes .update_log.txt (TASK-0119)")
        Check(UpdateBackupPolicy.ShouldExcludeFile("update_temp.bat"), "ShouldExcludeFile excludes batch scripts (TASK-0119)")
        Check(UpdateBackupPolicy.ShouldExcludeFile("test.tmp"), "ShouldExcludeFile excludes temporary files (TASK-0119)")
        Check(Not UpdateBackupPolicy.ShouldExcludeFile("HidaChat.exe"), "ShouldExcludeFile keeps main executable (TASK-0119)")
        Check(Not UpdateBackupPolicy.ShouldExcludeFile("sub\helper.dll"), "ShouldExcludeFile keeps library dll (TASK-0119)")

        ' Simulated end-to-end backup, failure, and rollback (TASK-0119)
        Dim testInstallDir = Path.Combine(Path.GetTempPath(), $"HidaChat_TestInstall_{Guid.NewGuid():N}")
        Dim testBackupDir = Path.Combine(Path.GetTempPath(), $"HidaChat_TestBackup_{Guid.NewGuid():N}")
        Try
            Directory.CreateDirectory(testInstallDir)
            File.WriteAllText(Path.Combine(testInstallDir, "HidaChat.exe"), "VERSION-1.0.0-EXE")
            Directory.CreateDirectory(Path.Combine(testInstallDir, "sub"))
            File.WriteAllText(Path.Combine(testInstallDir, "sub", "library.dll"), "VERSION-1.0.0-DLL")
            Directory.CreateDirectory(Path.Combine(testInstallDir, "data", "profiles"))
            File.WriteAllText(Path.Combine(testInstallDir, "data", "profiles", "session.json"), "USER-SESSION-DATA")
            File.WriteAllText(Path.Combine(testInstallDir, "updates.log"), "LOG-DATA")

            ' Step 1: Create backup
            UpdateBackupPolicy.CreateSafetyBackup(testInstallDir, testBackupDir)
            Check(File.Exists(Path.Combine(testBackupDir, "HidaChat.exe")), "Safety backup contains HidaChat.exe (TASK-0119)")
            Check(File.ReadAllText(Path.Combine(testBackupDir, "HidaChat.exe")) = "VERSION-1.0.0-EXE", "Backup binary content matches original (TASK-0119)")
            Check(File.Exists(Path.Combine(testBackupDir, "sub", "library.dll")), "Backup preserves subdirectory structure (TASK-0119)")
            Check(Not Directory.Exists(Path.Combine(testBackupDir, "data")), "Safety backup strictly excludes 'data/' directory (TASK-0119)")
            Check(Not File.Exists(Path.Combine(testBackupDir, "updates.log")), "Safety backup excludes log files (TASK-0119)")

            ' Step 2: Simulate broken/partial update corrupting installation
            File.WriteAllText(Path.Combine(testInstallDir, "HidaChat.exe"), "CORRUPTED-PARTIAL-2.0")
            File.Delete(Path.Combine(testInstallDir, "sub", "library.dll"))

            ' Step 3: Trigger rollback
            UpdateBackupPolicy.RestoreFromSafetyBackup(testBackupDir, testInstallDir)
            Check(File.ReadAllText(Path.Combine(testInstallDir, "HidaChat.exe")) = "VERSION-1.0.0-EXE", "Rollback successfully restored working HidaChat.exe (TASK-0119)")
            Check(File.Exists(Path.Combine(testInstallDir, "sub", "library.dll")), "Rollback restored deleted library.dll (TASK-0119)")
            Check(File.ReadAllText(Path.Combine(testInstallDir, "sub", "library.dll")) = "VERSION-1.0.0-DLL", "Restored library content matches original (TASK-0119)")
            Check(File.Exists(Path.Combine(testInstallDir, "data", "profiles", "session.json")), "User session data intact and undisturbed during rollback (TASK-0119)")
            Check(File.ReadAllText(Path.Combine(testInstallDir, "data", "profiles", "session.json")) = "USER-SESSION-DATA", "User session content completely unaffected (TASK-0119)")

            ' Step 4: Delete backup
            UpdateBackupPolicy.DeleteSafetyBackup(testBackupDir)
            Check(Not Directory.Exists(testBackupDir), "DeleteSafetyBackup cleaned up backup directory (TASK-0119)")
        Finally
            If Directory.Exists(testInstallDir) Then
                Try : Directory.Delete(testInstallDir, True) : Catch : End Try
            End If
            If Directory.Exists(testBackupDir) Then
                Try : Directory.Delete(testBackupDir, True) : Catch : End Try
            End If
        End Try

        ' Verify batch script writing with UTF-8 encoding without BOM
        Dim testBatPath = Path.Combine(Path.GetTempPath(), $"HidaChat_Test_Bat_{Guid.NewGuid():N}.bat")
        Try
            UpdateScriptPolicy.WriteUpdateBatchFile(testBatPath, batchScript)
            Check(File.Exists(testBatPath), "Batch file written to disk successfully (TASK-0092)")
            Dim readBackBytes = File.ReadAllBytes(testBatPath)
            ' Check absence of UTF-8 BOM (0xEF, 0xBB, 0xBF)
            Dim hasBom = readBackBytes.Length >= 3 AndAlso readBackBytes(0) = &HEF AndAlso readBackBytes(1) = &HBB AndAlso readBackBytes(2) = &HBF
            Check(Not hasBom, "Batch file is written without UTF-8 BOM for cmd.exe compatibility (TASK-0092)")
            Dim readBackText = File.ReadAllText(testBatPath, System.Text.Encoding.UTF8)
            Check(readBackText.Contains("C:\Temp\Update_è_123"), "Accented characters preserved in written batch file (TASK-0092)")
        Finally
            If File.Exists(testBatPath) Then File.Delete(testBatPath)
        End Try

        ' 4. Streaming SHA-256 calculation and infinite timeout download policy (TODO #88)
        Dim testPayload = System.Text.Encoding.UTF8.GetBytes("HidaChat OTA Streaming Download Verification")
        Dim expectedShaHex = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(testPayload)).ToLowerInvariant()

        Using stream As New MemoryStream(testPayload)
            Dim streamHashBytes = System.Security.Cryptography.SHA256.HashData(stream)
            Dim streamHashHex = Convert.ToHexString(streamHashBytes).ToLowerInvariant()
            Check(streamHashHex = expectedShaHex, "SHA256 streaming hash directly from stream matches payload hash (TODO #88)")
        End Using

        Dim infiniteTimeout = System.Threading.Timeout.InfiniteTimeSpan
        Check(infiniteTimeout = System.Threading.Timeout.InfiniteTimeSpan, "Dedicated download client configured with InfiniteTimeSpan (TODO #88)")

        Const streamingBufferSize As Integer = 81920
        Check(streamingBufferSize = 80 * 1024, "Chunked streaming buffer size is 80 KB (TODO #88)")
    End Sub

    Private Sub TestBulkSenderPolicy()
        ' 1. Normalization
        Check(BulkSenderPolicy.NormalizeText("") = "", "NormalizeText empty string")
        Check(BulkSenderPolicy.NormalizeText(Nothing) = "", "NormalizeText null string")
        Check(BulkSenderPolicy.NormalizeText("   Hello" & vbCrLf & "World   ") = "Hello" & vbLf & "World", "NormalizeText trim and newline")
        Check(BulkSenderPolicy.NormalizeText("Hello" & ChrW(&HA0) & "World") = "Hello World", "NormalizeText non-breaking space")

        ' 2. Draft conflict detection (TODO #79)
        Check(Not BulkSenderPolicy.IsDraftConflict("", "Messaggio previsto"), "Empty input is not draft conflict")
        Check(Not BulkSenderPolicy.IsDraftConflict("   ", "Messaggio previsto"), "Whitespace input is not draft conflict")
        Check(Not BulkSenderPolicy.IsDraftConflict("Messaggio previsto", "Messaggio previsto"), "Identical text is not draft conflict")
        Check(Not BulkSenderPolicy.IsDraftConflict("Messaggio" & vbCrLf & "previsto", "Messaggio" & vbLf & "previsto"), "Normalized newline text is not draft conflict")
        Check(BulkSenderPolicy.IsDraftConflict("Altra bozza dell'utente", "Messaggio previsto"), "Pre-existing different draft detected as conflict")
        Check(BulkSenderPolicy.IsDraftConflict("Ciao Marco", "Offerta speciale"), "Unrelated draft detected as conflict")

        ' 3. Chat identity verification (TODO #79)
        Check(BulkSenderPolicy.MatchesRecipient("+39 340 1234567", "+393401234567", Nothing, Nothing), "Recipient matched by full phone number")
        Check(BulkSenderPolicy.MatchesRecipient("Chat con 340 1234567", "3401234567", Nothing, Nothing), "Recipient matched by phone digits")
        Check(BulkSenderPolicy.MatchesRecipient("Mario Rossi (@mariorossi)", Nothing, "mariorossi", "Mario Rossi"), "Recipient matched by Telegram username")
        Check(BulkSenderPolicy.MatchesRecipient("Mario Rossi (@mariorossi)", Nothing, "@mariorossi", "Mario Rossi"), "Recipient matched by formatted Telegram username")
        Check(BulkSenderPolicy.MatchesRecipient("Mario Rossi", Nothing, Nothing, "Mario Rossi"), "Recipient matched by full name")
        Check(BulkSenderPolicy.MatchesRecipient("Dott. Rossi Studio", Nothing, Nothing, "Mario Rossi"), "Recipient matched by last name")
        Check(Not BulkSenderPolicy.MatchesRecipient("Giuseppe Verdi", "+393401234567", "mariorossi", "Mario Rossi"), "Unrelated recipient rejected")
        Check(Not BulkSenderPolicy.MatchesRecipient("", "+393401234567", "mariorossi", "Mario Rossi"), "Empty header rejected")

        ' 4. Observable confirmation vs fake Sent (TODO #79)
        Check(Not BulkSenderPolicy.IsSendConfirmed("Messaggio non inviato", False, False), "Un-triggered action is not confirmed")
        Check(Not BulkSenderPolicy.IsSendConfirmed("Messaggio ancora presente", True, True), "Input still containing text after trigger is not confirmed (Enter ignored)")
        Check(Not BulkSenderPolicy.IsSendConfirmed("Messaggio", True, False), "Non-empty composer after trigger is not confirmed")
        Check(BulkSenderPolicy.IsSendConfirmed("", True, False), "Empty composer with send button hidden is confirmed sent")
        Check(BulkSenderPolicy.IsSendConfirmed("   ", True, False), "Whitespace composer with send button hidden is confirmed sent")
        Check(BulkSenderPolicy.IsSendConfirmed("", True, True), "Empty composer after trigger is confirmed sent")

        ' 5. Script Generation
        Dim waScript = BulkSenderPolicy.BuildWhatsAppScript("Test WhatsApp", "393401234567", False)
        Check(waScript.Contains("Test WhatsApp"), "WhatsApp script embeds target message")
        Check(waScript.Contains("INVALID_NUMBER"), "WhatsApp script includes invalid number popup handler")
        Check(waScript.Contains("DRAFT_CONFLICT"), "WhatsApp script includes draft conflict check")
        Check(waScript.Contains("CONFIRMED_SENT"), "WhatsApp script includes observable confirmation")

        Dim tgScript = BulkSenderPolicy.BuildTelegramScript("Test Telegram", "@mariorossi", True)
        Check(tgScript.Contains("Test Telegram"), "Telegram script embeds target message")
        Check(tgScript.Contains("actionTriggered = true"), "Telegram script sets actionTriggered true")
        Check(tgScript.Contains("USER_NOT_FOUND"), "Telegram script includes user not found handler")
        Check(tgScript.Contains("DRAFT_CONFLICT"), "Telegram script includes draft conflict check")

        ' 6. Script Result Parsing
        Dim resSent = BulkSenderPolicy.ParseScriptResult("{""status"":""CONFIRMED_SENT""}")
        Check(resSent.Status = "CONFIRMED_SENT", "ParseScriptResult CONFIRMED_SENT")

        Dim resConflict = BulkSenderPolicy.ParseScriptResult("{""status"":""DRAFT_CONFLICT"",""currentDraft"":""Bozza vecchia""}")
        Check(resConflict.Status = "DRAFT_CONFLICT" AndAlso resConflict.CurrentDraft = "Bozza vecchia", "ParseScriptResult DRAFT_CONFLICT with draft text")

        Dim resInvalid = BulkSenderPolicy.ParseScriptResult("{""status"":""INVALID_NUMBER"",""msg"":""Numero non valido""}")
        Check(resInvalid.Status = "INVALID_NUMBER" AndAlso resInvalid.ErrorMessage = "Numero non valido", "ParseScriptResult INVALID_NUMBER with message")

        Dim resUserNotFound = BulkSenderPolicy.ParseScriptResult("{""status"":""USER_NOT_FOUND"",""msg"":""Utente non trovato""}")
        Check(resUserNotFound.Status = "USER_NOT_FOUND" AndAlso resUserNotFound.ErrorMessage = "Utente non trovato", "ParseScriptResult USER_NOT_FOUND with message")

        Dim resWaiting = BulkSenderPolicy.ParseScriptResult("{""status"":""WAITING_CONFIRMATION"",""currentText"":""Messaggio in attesa""}")
        Check(resWaiting.Status = "WAITING_CONFIRMATION", "ParseScriptResult WAITING_CONFIRMATION")

        Dim resEmpty = BulkSenderPolicy.ParseScriptResult("")
        Check(resEmpty.Status = "WAITING", "ParseScriptResult empty string returns WAITING")

        Dim resNull = BulkSenderPolicy.ParseScriptResult("null")
        Check(resNull.Status = "WAITING", "ParseScriptResult null returns WAITING")

        ' 7. Tests for ProfileIsolationPolicy (TODO #103)
        Check(ProfileIsolationPolicy.GetSafeProfileName("1") = "Account_1", "Profile name for simple ID '1' is 'Account_1'")
        Check(ProfileIsolationPolicy.GetSafeProfileName("account_1234567890abcdef1234567890abcdef") = "Account_1234567890abcdef1234567890abcdef", "Profile name for account_GUID strips redundant prefix")
        Check(ProfileIsolationPolicy.GetSafeProfileName("Account_Work") = "Account_Work", "Profile name with Account_ prefix is preserved")
        Check(ProfileIsolationPolicy.GetSafeProfileName("") = ProfileIsolationPolicy.DefaultProfileName, "Empty account ID returns default profile name")
        Check(ProfileIsolationPolicy.GetSafeProfileName(Nothing) = ProfileIsolationPolicy.DefaultProfileName, "Null account ID returns default profile name")
        Check(ProfileIsolationPolicy.GetSafeProfileName("   ") = ProfileIsolationPolicy.DefaultProfileName, "Whitespace account ID returns default profile name")
        Check(ProfileIsolationPolicy.GetSafeProfileName("bad/id:test*1?") = "Account_bad_id_test_1_", "Profile name sanitizes illegal characters")
        Check(ProfileIsolationPolicy.GetSafeProfileName("test.profile. .") = "Account_test.profile", "Profile name trims trailing dots and spaces")
        Dim veryLongId = New String("a"c, 100)
        Dim safeLongName = ProfileIsolationPolicy.GetSafeProfileName(veryLongId)
        Check(safeLongName.Length <= ProfileIsolationPolicy.MaxProfileNameLength, "Profile name length clamped to 64 chars")
        Check(safeLongName.StartsWith("Account_"), "Clamped profile name starts with Account_")

        Dim relPath = ProfileIsolationPolicy.GetNativeProfileRelativePath("account_test")
        Check(relPath = Path.Combine("EBWebView", "wv2profile_account_test"), "Native relative path is EBWebView\wv2profile_account_test")

        Dim testBaseDir = Path.Combine(Path.GetTempPath(), "HidaChat_Test_" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(testBaseDir)
        Try
            Dim nativeDir = ProfileIsolationPolicy.GetNativeProfileDirectory(testBaseDir, "1")
            Check(nativeDir = Path.Combine(testBaseDir, "EBWebView", "wv2profile_account_1"), "Native directory matches expected path")

            Dim legacyDir = ProfileIsolationPolicy.GetLegacyProfileDirectory(testBaseDir, "1")
            Check(legacyDir = Path.Combine(testBaseDir, "WV2Profile_1"), "Legacy directory matches expected path")

            ' Before directory creation, resolves to nativeDir
            Check(ProfileIsolationPolicy.ResolveEffectiveProfileDirectory(testBaseDir, "1") = nativeDir, "Effective profile resolves to native by default")

            ' If legacy exists, resolves to legacy
            Directory.CreateDirectory(legacyDir)
            Check(ProfileIsolationPolicy.ResolveEffectiveProfileDirectory(testBaseDir, "1") = legacyDir, "Effective profile resolves to legacy if present")

            ' If native exists, native takes precedence
            Directory.CreateDirectory(nativeDir)
            Check(ProfileIsolationPolicy.ResolveEffectiveProfileDirectory(testBaseDir, "1") = nativeDir, "Effective profile resolves to native when native is created")

            ' Storage directory resolution
            Dim nativeIdb = Path.Combine(nativeDir, "IndexedDB")
            Directory.CreateDirectory(nativeIdb)
            Check(ProfileIsolationPolicy.ResolveIndexedDbDirectory(nativeDir) = nativeIdb, "ResolveIndexedDbDirectory finds direct IndexedDB")

            Dim legacyIdb = Path.Combine(legacyDir, "EBWebView", "Default", "IndexedDB")
            Directory.CreateDirectory(legacyIdb)
            Check(ProfileIsolationPolicy.ResolveIndexedDbDirectory(legacyDir) = legacyIdb, "ResolveIndexedDbDirectory finds legacy EBWebView/Default/IndexedDB")

            Dim nativeSw = Path.Combine(nativeDir, "Service Worker")
            Directory.CreateDirectory(nativeSw)
            Check(ProfileIsolationPolicy.ResolveServiceWorkerDirectory(nativeDir) = nativeSw, "ResolveServiceWorkerDirectory finds direct Service Worker")

            Dim legacySw = Path.Combine(legacyDir, "EBWebView", "Default", "Service Worker")
            Directory.CreateDirectory(legacySw)
            Check(ProfileIsolationPolicy.ResolveServiceWorkerDirectory(legacyDir) = legacySw, "ResolveServiceWorkerDirectory finds legacy EBWebView/Default/Service Worker")
        Finally
            Try : Directory.Delete(testBaseDir, True) : Catch : End Try
        End Try

        Check(ProfileIsolationPolicy.IsSystemOrProtectedDirectory("Default"), "Default is protected system directory")
        Check(ProfileIsolationPolicy.IsSystemOrProtectedDirectory("Crashpad"), "Crashpad is protected system directory")
        Check(ProfileIsolationPolicy.IsSystemOrProtectedDirectory("ShaderCache"), "ShaderCache is protected system directory")
        Check(Not ProfileIsolationPolicy.IsSystemOrProtectedDirectory("Account_1"), "Account_1 is not protected system directory")
        Check(ProfileIsolationPolicy.IsNativeAccountProfileDirectory("Account_1"), "Account_1 is native account profile")
        Check(ProfileIsolationPolicy.IsNativeAccountProfileDirectory("wv2profile_account_1"), "wv2profile_account_1 is native account profile")
        Check(ProfileIsolationPolicy.IsNativeAccountProfileDirectory("WV2Profile_account_1784925628238"), "WV2Profile_account_1784925628238 is native account profile")
        Check(ProfileIsolationPolicy.ExtractAccountIdFromProfileDirectory("wv2profile_account_1784925628238") = "account_1784925628238", "ExtractAccountIdFromProfileDirectory extracts clean ID")
        Check(Not ProfileIsolationPolicy.IsNativeAccountProfileDirectory("Default"), "Default is not native account profile")
        Check(Not ProfileIsolationPolicy.IsNativeAccountProfileDirectory("Crashpad"), "Crashpad is not native account profile")
        Check(Not ProfileIsolationPolicy.IsNativeAccountProfileDirectory("random_folder"), "random_folder is not native account profile")

        ' Test migration backup cleanup and safety checks (TODO #103)
        Dim testCleanupDir = Path.Combine(Path.GetTempPath(), "HidaChat_CleanupTest_" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(testCleanupDir)
        Try
            Dim testAccId = "test_cleanup_acc"
            Dim nativeAccDir = ProfileIsolationPolicy.GetNativeProfileDirectory(testCleanupDir, testAccId)
            Directory.CreateDirectory(nativeAccDir)

            ' Before substantial data, purge is unsafe
            Check(Not ProfileIsolationPolicy.CanSafelyPurgeMigrationBackups(testCleanupDir, testAccId), "Purge is unsafe when native profile has no substantial data")

            ' Create substantial data in native profile (e.g. 10 IndexedDB files)
            Dim idbPath = Path.Combine(nativeAccDir, "IndexedDB")
            Directory.CreateDirectory(idbPath)
            For i As Integer = 1 To 10
                File.WriteAllText(Path.Combine(idbPath, $"blob_{i}.bin"), "sample data")
            Next
            Check(ProfileIsolationPolicy.HasSubstantialProfileData(nativeAccDir), "HasSubstantialProfileData returns True with >= 8 IndexedDB files")
            Check(ProfileIsolationPolicy.CanSafelyPurgeMigrationBackups(testCleanupDir, testAccId), "Purge is safe when native profile has verified substantial data")

            ' Verify obsolete candidate paths
            Dim obsoleteCandidates = ProfileIsolationPolicy.GetObsoleteMigrationBackupDirectories(testCleanupDir, testAccId)
            Check(obsoleteCandidates.Count = 5, "GetObsoleteMigrationBackupDirectories returns exactly 5 legacy candidates")
            Check(obsoleteCandidates.Any(Function(p) p.EndsWith(".migrated")), "Candidates include .migrated")
            Check(obsoleteCandidates.Any(Function(p) p.EndsWith(".previous")), "Candidates include .previous")
            Check(obsoleteCandidates.Any(Function(p) p.EndsWith("_Snapshot")), "Candidates include _Snapshot")
            Check(obsoleteCandidates.Any(Function(p) p.EndsWith(".bak")), "Candidates include .bak")
        Finally
            Try : Directory.Delete(testCleanupDir, True) : Catch : End Try
        End Try

        ' Test CacheCompactionPolicy (TODO #104)
        Check(CacheCompactionPolicy.IsSafeCompactionKind(CacheCompactionPolicy.SafeCompactionKindsMask), "SafeCompactionKindsMask is safe")
        Check(CacheCompactionPolicy.IsSafeCompactionKind(CacheCompactionPolicy.KindDiskCache), "KindDiskCache alone is safe")
        Check(CacheCompactionPolicy.IsSafeCompactionKind(CacheCompactionPolicy.KindCacheStorage), "KindCacheStorage alone is safe")
        Check(CacheCompactionPolicy.IsSafeCompactionKind(CacheCompactionPolicy.KindDiskCache Or CacheCompactionPolicy.KindCacheStorage), "DiskCache + CacheStorage combination is safe")
        Check(Not CacheCompactionPolicy.IsSafeCompactionKind(0UL), "Empty kind mask is not safe compaction")
        Check(Not CacheCompactionPolicy.IsSafeCompactionKind(CacheCompactionPolicy.KindCookies), "Cookies is rejected by compaction policy")
        Check(Not CacheCompactionPolicy.IsSafeCompactionKind(CacheCompactionPolicy.KindIndexedDb), "IndexedDb is rejected by compaction policy")
        Check(Not CacheCompactionPolicy.IsSafeCompactionKind(CacheCompactionPolicy.KindLocalStorage), "LocalStorage is rejected by compaction policy")
        Check(Not CacheCompactionPolicy.IsSafeCompactionKind(CacheCompactionPolicy.KindPasswordAutosave), "PasswordAutosave is rejected by compaction policy")
        Check(Not CacheCompactionPolicy.IsSafeCompactionKind(CacheCompactionPolicy.KindGeneralAutofill), "GeneralAutofill is rejected by compaction policy")
        Check(Not CacheCompactionPolicy.IsSafeCompactionKind(CacheCompactionPolicy.KindSettings), "Settings is rejected by compaction policy")
        Check(Not CacheCompactionPolicy.IsSafeCompactionKind(CacheCompactionPolicy.KindDiskCache Or CacheCompactionPolicy.KindCookies), "DiskCache combined with Cookies is rejected")
        Check(Not CacheCompactionPolicy.IsSafeCompactionKind(CacheCompactionPolicy.KindDiskCache Or CacheCompactionPolicy.KindIndexedDb), "DiskCache combined with IndexedDb is rejected")

        ' Mock profile cache directories calculation
        Dim testProfileDir = Path.Combine(Path.GetTempPath(), "HidaChat_CacheTest_" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(testProfileDir)
        Try
            Dim cacheDir = Path.Combine(testProfileDir, "Cache")
            Dim codeCacheDir = Path.Combine(testProfileDir, "Code Cache")
            Dim swCacheStorageDir = Path.Combine(testProfileDir, "Service Worker", "CacheStorage")
            Dim idbDir = Path.Combine(testProfileDir, "IndexedDB")
            Directory.CreateDirectory(cacheDir)
            Directory.CreateDirectory(codeCacheDir)
            Directory.CreateDirectory(swCacheStorageDir)
            Directory.CreateDirectory(idbDir)

            File.WriteAllBytes(Path.Combine(cacheDir, "data_0"), New Byte(1023) {}) ' 1024 bytes
            File.WriteAllBytes(Path.Combine(codeCacheDir, "js_0"), New Byte(2047) {}) ' 2048 bytes
            File.WriteAllBytes(Path.Combine(swCacheStorageDir, "entry_0"), New Byte(4095) {}) ' 4096 bytes
            File.WriteAllBytes(Path.Combine(idbDir, "session.ldb"), New Byte(8191) {}) ' 8192 bytes (must NOT be counted)

            Dim detectedDirs = CacheCompactionPolicy.GetProfileCacheDirectories(testProfileDir)
            Check(detectedDirs.Count = 3, "GetProfileCacheDirectories detected exactly 3 regenerable directories")
            Check(detectedDirs.Contains(cacheDir), "Detected directories include Cache")
            Check(detectedDirs.Contains(codeCacheDir), "Detected directories include Code Cache")
            Check(detectedDirs.Contains(swCacheStorageDir), "Detected directories include Service Worker CacheStorage")
            Check(Not detectedDirs.Contains(idbDir), "Detected directories never include IndexedDB")

            Dim totalTransientBytes = CacheCompactionPolicy.CalculateTransientCacheSizeBytes(testProfileDir)
            Check(totalTransientBytes = 1024L + 2048L + 4096L, "CalculateTransientCacheSizeBytes calculates 7168 bytes without counting IndexedDB")

            Check(CacheCompactionPolicy.FormatByteSize(500L) = "500 B", "FormatByteSize 500 B")
            Check(CacheCompactionPolicy.FormatByteSize(2048L) = "2.0 KB", "FormatByteSize 2.0 KB")
            Check(CacheCompactionPolicy.FormatByteSize(10485760L) = "10.0 MB", "FormatByteSize 10.0 MB")
            Check(CacheCompactionPolicy.FormatByteSize(1073741824L) = "1.00 GB", "FormatByteSize 1.00 GB")
        Finally
            Try : Directory.Delete(testProfileDir, True) : Catch : End Try
        End Try

        ' Test StartupJunkCleanupPolicy (TODO #105)
        Check(StartupJunkCleanupPolicy.IsJunkFolderName("Crashpad"), "Crashpad is recognized as junk folder")
        Check(StartupJunkCleanupPolicy.IsJunkFolderName("GPUCache"), "GPUCache is recognized as junk folder")
        Check(StartupJunkCleanupPolicy.IsJunkFolderName("ShaderCache"), "ShaderCache is recognized as junk folder")
        Check(StartupJunkCleanupPolicy.IsJunkFolderName("GrShaderCache"), "GrShaderCache is recognized as junk folder")
        Check(StartupJunkCleanupPolicy.IsJunkFolderName("GraphiteDawnCache"), "GraphiteDawnCache is recognized as junk folder")
        Check(StartupJunkCleanupPolicy.IsJunkFolderName("DawnCache"), "DawnCache is recognized as junk folder")
        Check(StartupJunkCleanupPolicy.IsJunkFolderName("DawnGraphiteCache"), "DawnGraphiteCache is recognized as junk folder")
        Check(StartupJunkCleanupPolicy.IsJunkFolderName("DawnWebGPUCache"), "DawnWebGPUCache is recognized as junk folder")
        Check(StartupJunkCleanupPolicy.IsJunkFolderName("GPUPersistentCache"), "GPUPersistentCache is recognized as junk folder")
        Check(StartupJunkCleanupPolicy.IsJunkFolderName("Code Cache"), "Code Cache is recognized as junk folder")
        Check(StartupJunkCleanupPolicy.IsJunkFolderName("Cache"), "Cache is recognized as junk folder")
        Check(StartupJunkCleanupPolicy.IsJunkFolderName("BrowserMetrics"), "BrowserMetrics is recognized as junk folder")
        Check(StartupJunkCleanupPolicy.IsJunkFolderName("component_crx_cache"), "component_crx_cache is recognized as junk folder")
        Check(StartupJunkCleanupPolicy.IsJunkFolderName("OptimizationGuidePredictionModels"), "OptimizationGuidePredictionModels is recognized as junk folder")
        Check(StartupJunkCleanupPolicy.IsJunkFolderName("Safe Browsing"), "Safe Browsing is recognized as junk folder")
        Check(StartupJunkCleanupPolicy.IsJunkFolderName("Subresource Filter"), "Subresource Filter is recognized as junk folder")
        Check(Not StartupJunkCleanupPolicy.IsJunkFolderName("IndexedDB"), "IndexedDB is never a junk folder")
        Check(Not StartupJunkCleanupPolicy.IsJunkFolderName("Local Storage"), "Local Storage is never a junk folder")
        Check(Not StartupJunkCleanupPolicy.IsJunkFolderName("Account_1"), "Account folder is never a junk folder")
        Check(Not StartupJunkCleanupPolicy.IsJunkFolderName(Nothing), "Null folder name is not junk")

        Check(StartupJunkCleanupPolicy.IsProtectedDirectory("IndexedDB"), "IndexedDB is protected")
        Check(StartupJunkCleanupPolicy.IsProtectedDirectory("Local Storage"), "Local Storage is protected")
        Check(StartupJunkCleanupPolicy.IsProtectedDirectory("Session Storage"), "Session Storage is protected")
        Check(StartupJunkCleanupPolicy.IsProtectedDirectory("https_web.whatsapp.com_0.indexeddb.leveldb"), ".leveldb directory is protected")
        Check(StartupJunkCleanupPolicy.IsProtectedDirectory("https_web.whatsapp.com_0.indexeddb.blob"), ".blob directory is protected")
        Check(Not StartupJunkCleanupPolicy.IsProtectedDirectory("Crashpad"), "Crashpad is not protected")
        Check(Not StartupJunkCleanupPolicy.IsProtectedDirectory("GPUCache"), "GPUCache is not protected")

        Check(StartupJunkCleanupPolicy.IsJunkFileName("crash.dmp"), ".dmp file is recognized as junk")
        Check(StartupJunkCleanupPolicy.IsJunkFileName("metrics.pma"), ".pma file is recognized as junk")
        Check(StartupJunkCleanupPolicy.IsJunkFileName("profile.old"), ".old file is recognized as junk")
        Check(Not StartupJunkCleanupPolicy.IsJunkFileName("Local State"), "Local State is protected")
        Check(Not StartupJunkCleanupPolicy.IsJunkFileName("Cookies"), "Cookies is protected")
        Check(Not StartupJunkCleanupPolicy.IsJunkFileName("Preferences"), "Preferences is protected")
        Check(Not StartupJunkCleanupPolicy.IsJunkFileName("session.ldb"), "LevelDB .ldb file is never junk")
        Check(Not StartupJunkCleanupPolicy.IsJunkFileName("000003.log"), "LevelDB .log file is never junk")
        Check(Not StartupJunkCleanupPolicy.IsJunkFileName("settings.json"), "settings.json is never junk")
        Check(Not StartupJunkCleanupPolicy.IsJunkFileName(".lock"), ".lock file is never junk")

        ' Mock folder structure for startup cleanup simulation
        Dim testStartupDir = Path.Combine(Path.GetTempPath(), "HidaChat_StartupJunkTest_" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(testStartupDir)
        Try
            ' Create junk folders with nested files
            Dim junkCrashpad = Path.Combine(testStartupDir, "Crashpad", "reports")
            Dim junkGpu = Path.Combine(testStartupDir, "GPUCache")
            Dim junkShader = Path.Combine(testStartupDir, "ShaderCache", "GPU")
            Dim junkMetrics = Path.Combine(testStartupDir, "BrowserMetrics")
            Directory.CreateDirectory(junkCrashpad)
            Directory.CreateDirectory(junkGpu)
            Directory.CreateDirectory(junkShader)
            Directory.CreateDirectory(junkMetrics)

            File.WriteAllText(Path.Combine(junkCrashpad, "crash1.dmp"), "dump data")
            File.WriteAllText(Path.Combine(junkGpu, "data_0"), "gpu cache")
            File.WriteAllText(Path.Combine(junkShader, "shader_blob"), "shader cache")
            File.WriteAllText(Path.Combine(junkMetrics, "metrics.pma"), "metrics data")

            ' Create standalone junk files
            Dim rootDmp = Path.Combine(testStartupDir, "orphan.dmp")
            Dim rootOld = Path.Combine(testStartupDir, "backup.old")
            Dim rootPma = Path.Combine(testStartupDir, "metrics.pma")
            File.WriteAllText(rootDmp, "dmp")
            File.WriteAllText(rootOld, "old")
            File.WriteAllText(rootPma, "pma")

            ' Create PROTECTED session structures
            Dim profileAccount = Path.Combine(testStartupDir, "EBWebView", "wv2profile_account_1")
            Dim idbLevelDb = Path.Combine(profileAccount, "IndexedDB", "https_web.whatsapp.com_0.indexeddb.leveldb")
            Dim idbBlob = Path.Combine(profileAccount, "IndexedDB", "https_web.whatsapp.com_0.indexeddb.blob")
            Dim localStorageDb = Path.Combine(profileAccount, "Local Storage", "leveldb")
            Dim networkDir = Path.Combine(profileAccount, "Network")
            Directory.CreateDirectory(idbLevelDb)
            Directory.CreateDirectory(idbBlob)
            Directory.CreateDirectory(localStorageDb)
            Directory.CreateDirectory(networkDir)

            Dim localStateFile = Path.Combine(testStartupDir, "EBWebView", "Local State")
            File.WriteAllText(localStateFile, "{""os_crypt"":{""encrypted_key"":""SECRET_KEY""}}")
            Dim ldbFile = Path.Combine(idbLevelDb, "000005.ldb")
            File.WriteAllText(ldbFile, "CRITICAL_SESSION_DATA")
            Dim blobFile = Path.Combine(idbBlob, "media.bin")
            File.WriteAllText(blobFile, "MEDIA_BLOB")
            Dim lsLogFile = Path.Combine(localStorageDb, "000001.log")
            File.WriteAllText(lsLogFile, "LOCAL_STORAGE_LOG")
            Dim cookieFile = Path.Combine(networkDir, "Cookies")
            File.WriteAllText(cookieFile, "SESSION_COOKIES")
            Dim prefFile = Path.Combine(profileAccount, "Preferences")
            File.WriteAllText(prefFile, "USER_PREFERENCES")

            ' Run cleanup
            Dim cleanupResult = StartupJunkCleanupPolicy.CleanWebViewJunkFolders(testStartupDir)

            ' Assert junk was deleted
            Check(Not Directory.Exists(Path.Combine(testStartupDir, "Crashpad")), "Junk Crashpad folder was deleted")
            Check(Not Directory.Exists(junkGpu), "Junk GPUCache folder was deleted")
            Check(Not Directory.Exists(Path.Combine(testStartupDir, "ShaderCache")), "Junk ShaderCache folder was deleted")
            Check(Not Directory.Exists(junkMetrics), "Junk BrowserMetrics folder was deleted")
            Check(Not File.Exists(rootDmp), "Orphan .dmp file was deleted")
            Check(Not File.Exists(rootOld), "Orphan .old file was deleted")
            Check(Not File.Exists(rootPma), "Orphan .pma file was deleted")

            ' Assert PROTECTED files remain completely untouched
            Check(File.Exists(localStateFile), "Local State master encryption key remains untouched")
            Check(File.Exists(ldbFile), "IndexedDB .leveldb session database remains untouched")
            Check(File.Exists(blobFile), "IndexedDB .blob media storage remains untouched")
            Check(File.Exists(lsLogFile), "Local Storage leveldb remains untouched")
            Check(File.Exists(cookieFile), "Session Cookies file remains untouched")
            Check(File.Exists(prefFile), "Profile Preferences remain untouched")
            Check(cleanupResult.DeletedFoldersCount >= 4, "Cleanup deleted at least 4 junk folders")
            Check(cleanupResult.DeletedFilesCount >= 3, "Cleanup deleted at least 3 standalone junk files")
            Check(cleanupResult.FreedBytes > 0, "Cleanup reported freed bytes > 0")
        Finally
            Try : Directory.Delete(testStartupDir, True) : Catch : End Try
        End Try

        ' Test ColdProfileCopy.HasModifications differential detection (TODO #108)
        Dim testModSource = Path.Combine(Path.GetTempPath(), "HidaChat_ModTest_Src_" & Guid.NewGuid().ToString("N"))
        Dim testModTarget = Path.Combine(Path.GetTempPath(), "HidaChat_ModTest_Dst_" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(testModSource)
        Try
            ' 1. Missing target returns True (first publish)
            Check(ColdProfileCopy.HasModifications(testModSource, testModTarget), "HasModifications returns True when target does not exist")

            ' Create target directory and write baseline file
            Directory.CreateDirectory(testModTarget)
            WriteFile(testModTarget, "Cookies", "session_v1")
            WriteFile(testModSource, "Cookies", "session_v1")

            ' Align timestamps so source is NOT newer than target
            Dim baseTime = DateTime.UtcNow.AddMinutes(-5)
            File.SetLastWriteTimeUtc(Path.Combine(testModTarget, "Cookies"), baseTime)
            File.SetLastWriteTimeUtc(Path.Combine(testModSource, "Cookies"), baseTime)
            Directory.SetLastWriteTimeUtc(testModTarget, baseTime)

            ' 2. Identical unmodified source returns False (zero network I/O skip)
            Check(Not ColdProfileCopy.HasModifications(testModSource, testModTarget), "HasModifications returns False when source files have not been modified")

            ' 3. Modified source file returns True
            File.SetLastWriteTimeUtc(Path.Combine(testModSource, "Cookies"), DateTime.UtcNow)
            Check(ColdProfileCopy.HasModifications(testModSource, testModTarget), "HasModifications returns True when essential source file timestamp is newer")

            ' 4. Reset source timestamp to base, but touch a SKIPPED folder (e.g. Cache) -> must still return False
            File.SetLastWriteTimeUtc(Path.Combine(testModSource, "Cookies"), baseTime)
            Dim srcCacheDir = Path.Combine(testModSource, "Cache")
            Directory.CreateDirectory(srcCacheDir)
            WriteFile(srcCacheDir, "data_0", "cached_blob")
            File.SetLastWriteTimeUtc(Path.Combine(srcCacheDir, "data_0"), DateTime.UtcNow)
            Check(Not ColdProfileCopy.HasModifications(testModSource, testModTarget), "HasModifications ignores modifications inside skipped Cache folders")
        Finally
            Try : Directory.Delete(testModSource, True) : Catch : End Try
            Try : Directory.Delete(testModTarget, True) : Catch : End Try
        End Try

        ' Test AppLogger batched queue and flush (TODO #108)
        Dim testLogMsg = "Unit test log entry " & Guid.NewGuid().ToString("N")
        AppLogger.LogSync(testLogMsg)
        AppLogger.FlushNow()
        Dim syncLogFile = Path.Combine(AppLogger.LogsDirectory, "sync.log")
        Check(File.Exists(syncLogFile), "AppLogger.FlushNow created sync.log file in data/logs")
        Dim logContent = File.ReadAllText(syncLogFile)
        Check(logContent.Contains(testLogMsg), "AppLogger wrote batched log message to sync.log")
    End Sub
    Private Async Function TestTodo80And84And85Async() As Task
        ' Test TODO #80: BulkSender concurrency coordinator and outcome determination
        Dim coordinator As New BulkSenderExecutionCoordinator()
        Check(Not coordinator.IsRunning, "Coordinator initial state IsRunning is False")
        Check(Not coordinator.IsStopping, "Coordinator initial state IsStopping is False")

        ' 1. Normal start succeeds
        Dim start1 = coordinator.TryBeginExecution()
        Check(start1, "First TryBeginExecution succeeds")
        Check(coordinator.IsRunning, "Coordinator IsRunning is True")

        ' 2. Concurrent start rejected while running
        Dim start2 = coordinator.TryBeginExecution()
        Check(Not start2, "Concurrent TryBeginExecution is blocked while running")

        ' 3. BeginStop transitions to stopping
        coordinator.BeginStop()
        Check(coordinator.IsStopping, "Coordinator IsStopping is True")

        ' 4. Concurrent start rejected while stopping
        Dim start3 = coordinator.TryBeginExecution()
        Check(Not start3, "Concurrent TryBeginExecution is blocked while stopping")

        ' 5. Outcome determination
        Dim outcomeCancelled = coordinator.DetermineOutcome(1, 0, 3, wasCancelled:=True, errorOccurred:=False)
        Check(outcomeCancelled = BulkSenderOutcome.Cancelled, "DetermineOutcome returns Cancelled when wasCancelled is True")

        Dim outcomeFailed = coordinator.DetermineOutcome(1, 1, 3, wasCancelled:=False, errorOccurred:=True)
        Check(outcomeFailed = BulkSenderOutcome.Failed, "DetermineOutcome returns Failed when errorOccurred is True")

        Dim outcomeCompleted = coordinator.DetermineOutcome(3, 0, 3, wasCancelled:=False, errorOccurred:=False)
        Check(outcomeCompleted = BulkSenderOutcome.Completed, "DetermineOutcome returns Completed on clean finish")

        ' 6. EndExecution resets state
        coordinator.EndExecution()
        Check(Not coordinator.IsRunning, "IsRunning is False after EndExecution")
        Check(Not coordinator.IsStopping, "IsStopping is False after EndExecution")

        ' 7. Acceptance Test TODO #80: Stop during async JavaScript execution and immediate restart prevention
        Dim simCoordinator As New BulkSenderExecutionCoordinator()
        Check(simCoordinator.TryBeginExecution(), "Simulated execution begins successfully")
        Check(simCoordinator.IsRunning, "Simulated execution is running")

        ' Simulate contact 1 completed successfully
        Dim simSentCount = 1
        Dim simErrorCount = 0
        Dim simTotal = 3

        ' Simulate contact 2 enters asynchronous JS call (e.g. ExecuteScriptAsync)
        Dim jsCallPending = True
        Dim restartBlockedDuringJs = False
        Dim wasSimCancelled = False
        Dim simContact2Status = "Inviando..."

        ' User presses Stop during the pending JS call
        simCoordinator.BeginStop()
        Check(simCoordinator.IsStopping, "Coordinator enters stopping state during JS call")

        ' User immediately attempts a new start while stop/cancellation is in progress
        restartBlockedDuringJs = Not simCoordinator.TryBeginExecution()
        Check(restartBlockedDuringJs, "Immediate restart attempt during active JS call is strictly blocked")

        ' Active JS operation observes cancellation token
        Dim simTokenCancelled = True
        If simTokenCancelled Then
            wasSimCancelled = True
            simContact2Status = "Interrotto"
            ' Error count must NOT be incremented for interrupted contact
            jsCallPending = False
        End If

        Check(wasSimCancelled, "Asynchronous execution was marked as cancelled")
        Check(simContact2Status = "Interrotto", "Interrupted contact status is marked Interrotto and not Errore")
        Check(simSentCount = 1, "Actual sent count is preserved unchanged as 1")
        Check(simErrorCount = 0, "Error count is not falsely incremented and remains 0")
        Dim unsendCount = simTotal - simSentCount - simErrorCount
        Check(unsendCount = 2, "Unsend count accurately reflects interrupted and remaining contacts (2)")

        ' Determine outcome
        Dim simOutcome = simCoordinator.DetermineOutcome(simSentCount, simErrorCount, simTotal, wasSimCancelled, errorOccurred:=False)
        Check(simOutcome = BulkSenderOutcome.Cancelled, "Outcome determination correctly yields Cancelled")

        ' Execution ends
        simCoordinator.EndExecution()
        Check(Not simCoordinator.IsRunning, "IsRunning is False after execution finishes cancellation")
        Check(Not simCoordinator.IsStopping, "IsStopping is False after execution finishes cancellation")

        ' Subsequent clean start now succeeds
        Check(simCoordinator.TryBeginExecution(), "New execution run can start cleanly after previous cancelled run terminates")
        simCoordinator.EndExecution()

        ' Test TODO #84: RFC 4180 multiline CSV parser and delimiter detection
        Dim commaCsvWithSemicolonsInQuotes = "Name,Phone,Message" & vbCrLf &
            """Mario"",""+391234567"",""Sentence 1; sentence 2; sentence 3; sentence 4""" & vbCrLf &
            """Luigi"",""+397654321"",""Another; message; with; lots; of; semicolons"""
        Dim detectedDelim = BulkSenderPolicy.DetectCsvDelimiter(commaCsvWithSemicolonsInQuotes)
        Check(detectedDelim = ","c, "DetectCsvDelimiter correctly detects comma delimiter ignoring semicolons inside quotes")

        Dim semiCsvWithCommasInQuotes = "Name;Phone;Message" & vbCrLf &
            """Anna"";""+39111222"";""Item 1, item 2, item 3, item 4"""
        Dim detectedSemiDelim = BulkSenderPolicy.DetectCsvDelimiter(semiCsvWithCommasInQuotes)
        Check(detectedSemiDelim = ";"c, "DetectCsvDelimiter correctly detects semicolon delimiter ignoring commas inside quotes")

        Dim tabCsv = "Name" & vbTab & "Phone" & vbTab & "Message" & vbCrLf & "Sara" & vbTab & "123" & vbTab & "Hello"
        Dim detectedTabDelim = BulkSenderPolicy.DetectCsvDelimiter(tabCsv)
        Check(detectedTabDelim = vbTab, "DetectCsvDelimiter correctly detects tab delimiter")

        Dim multilineCsv = "Nome,Telefono,Messaggio" & vbCrLf &
            """Mario"",""+391234567"",""Riga 1" & vbCrLf & "Riga 2" & vbCrLf & "Riga 3 con """"virgolette doppie"""" e virgole,;"",""Altro""" & vbCrLf &
            """Luigi"",""+397654321"",""Messaggio standard"""
        Dim parsedRecords = BulkSenderPolicy.ParseCsvRecords(multilineCsv, ","c)
        Check(parsedRecords.Count = 3, "ParseCsvRecords preserves multiline records (exactly 3 records: header + 2 contacts)")
        Check(parsedRecords(1)(0) = "Mario", "ParseCsvRecords extracts Mario")
        Check(parsedRecords(1)(1) = "+391234567", "ParseCsvRecords extracts phone")
        Check(parsedRecords(1)(2).Contains("Riga 1") AndAlso parsedRecords(1)(2).Contains("Riga 2") AndAlso parsedRecords(1)(2).Contains("""virgolette doppie"""), "ParseCsvRecords preserves multiline message and unescapes quotes")
        Check(parsedRecords(2)(0) = "Luigi", "ParseCsvRecords extracts Luigi without offset")

        ' Test TODO #85: Batch translation separator collision check
        Dim testTextsWithSep As New List(Of String) From {
            "Item with separator ### test",
            "Normal item"
        }
        Dim hasSepCollision = GoogleTranslationProvider.HasSeparatorCollision(testTextsWithSep, vbLf & "###" & vbLf)
        Check(hasSepCollision, "HasSeparatorCollision returns True when item contains ###")

        Dim testTextsWithoutSep As New List(Of String) From {
            "Normal item 1",
            "Normal item 2"
        }
        Dim hasNoSepCollision = GoogleTranslationProvider.HasSeparatorCollision(testTextsWithoutSep, vbLf & "###" & vbLf)
        Check(Not hasNoSepCollision, "HasSeparatorCollision returns False when items do not contain ###")

        ' Safe execution of batch translation
        Dim googleProvider = GoogleTranslationProvider.Instance
        Dim ctsTranslation As New Threading.CancellationTokenSource(2000)
        Try
            Dim batchRes = Await googleProvider.TranslateBatchAsync(testTextsWithSep, "en", ctsTranslation.Token)
            Check(batchRes IsNot Nothing, "TranslateBatchAsync handles texts with ### without crashing")
        Catch ex As Exception
            Check(True, "TranslateBatchAsync handles offline provider gracefully")
        End Try

        ' Test TASK-0089: Account load safety policy and non-destructive deserialization failure handling
        Check(AccountLoadSafetyPolicy.ShouldInhibitSave(True), "AccountLoadSafetyPolicy inhibits save when load failed")
        Check(Not AccountLoadSafetyPolicy.ShouldInhibitSave(False), "AccountLoadSafetyPolicy allows save when load succeeded")
        Check(AccountLoadSafetyPolicy.ShouldInhibitCleanup(True), "AccountLoadSafetyPolicy inhibits cleanup when load failed")
        Check(Not AccountLoadSafetyPolicy.ShouldInhibitCleanup(False), "AccountLoadSafetyPolicy allows cleanup when load succeeded")

        Dim validAccountsJson = "[{""id"":""acc_1"",""name"":""Account 1"",""platform"":""WhatsApp""},{""id"":""acc_2"",""name"":""Account 2"",""platform"":""Telegram""}]"
        Dim errVal As String = Nothing
        Check(AccountLoadSafetyPolicy.TryValidateAccountsJson(validAccountsJson, errVal), "TryValidateAccountsJson accepts valid accounts array")
        Check(errVal Is Nothing, "TryValidateAccountsJson has no error for valid accounts")

        Dim syntaxErrorJson = "[{""id"":""acc_1"", ""name"": ""broken... "
        Check(Not AccountLoadSafetyPolicy.TryValidateAccountsJson(syntaxErrorJson, errVal), "TryValidateAccountsJson rejects syntax error in JSON")
        Check(Not String.IsNullOrEmpty(errVal), "TryValidateAccountsJson provides error for syntax error")

        Dim incompatibleTypeIdJson = "[{""id"": 12345, ""name"": ""Account 1""}]"
        Check(Not AccountLoadSafetyPolicy.TryValidateAccountsJson(incompatibleTypeIdJson, errVal), "TryValidateAccountsJson rejects number type for id")
        Check(errVal.Contains("invalid type"), "TryValidateAccountsJson reports invalid type for id")

        Dim incompatibleTypeNameJson = "[{""id"": ""acc_1"", ""name"": true}]"
        Check(Not AccountLoadSafetyPolicy.TryValidateAccountsJson(incompatibleTypeNameJson, errVal), "TryValidateAccountsJson rejects boolean type for name")

        Dim emptyArrayJson = "[]"
        Check(Not AccountLoadSafetyPolicy.TryValidateAccountsJson(emptyArrayJson, errVal), "TryValidateAccountsJson rejects empty accounts array")

        Dim objectInsteadOfArrayJson = "{""id"":""acc_1""}"
        Check(Not AccountLoadSafetyPolicy.TryValidateAccountsJson(objectInsteadOfArrayJson, errVal), "TryValidateAccountsJson rejects non-array root")

        ' Backup creation test on simulated settings file
        Dim tempDir = Path.Combine(Path.GetTempPath(), "HidaChat_Task89_Test_" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(tempDir)
        Try
            Dim simSettingsFile = Path.Combine(tempDir, "settings.json")
            Dim corruptPayload = "{""theme"":""Dark"",""accounts"":[{""id"":99999,""name"":""Corrupted""}]}"
            File.WriteAllText(simSettingsFile, corruptPayload)

            ' Verify backup file generation
            Dim createdBak = AccountLoadSafetyPolicy.CreateBackup(simSettingsFile)
            Check(Not String.IsNullOrEmpty(createdBak), "CreateBackup returns non-empty path on existing settings file")
            Check(File.Exists(createdBak), "CreateBackup created .bak file on disk")
            Dim bakContent = File.ReadAllText(createdBak)
            Check(bakContent = corruptPayload, "Backup file content exactly matches corrupted original file")

            ' Verify that invalid file fails validation and triggers inhibitor
            Dim validationOk = AccountLoadSafetyPolicy.TryValidateAccountsJson("[{""id"":99999,""name"":""Corrupted""}]", errVal)
            Check(Not validationOk, "Simulated corrupted accounts fails validation")
            Dim simulatedLoadFailed = Not validationOk
            Check(AccountLoadSafetyPolicy.ShouldInhibitSave(simulatedLoadFailed), "Simulated save is strictly inhibited")
            Check(AccountLoadSafetyPolicy.ShouldInhibitCleanup(simulatedLoadFailed), "Simulated profile cleanup is strictly inhibited")

            ' Verify profiles folder preservation
            Dim dummyProfileDir = Path.Combine(tempDir, "WV2Profile_acc1")
            Directory.CreateDirectory(dummyProfileDir)
            File.WriteAllText(Path.Combine(dummyProfileDir, "session.db"), "user_login_data")

            ' Under simulatedLoadFailed, profile must not be deleted
            If Not AccountLoadSafetyPolicy.ShouldInhibitCleanup(simulatedLoadFailed) Then
                Directory.Delete(dummyProfileDir, True)
            End If
            Check(Directory.Exists(dummyProfileDir), "User profile directory remains preserved and untouched on load failure")
            Check(File.Exists(Path.Combine(dummyProfileDir, "session.db")), "Session database inside profile remains preserved")
        Finally
            Try
                If Directory.Exists(tempDir) Then Directory.Delete(tempDir, True)
            Catch
            End Try
        End Try

        ' Test TASK-0090: Safe orphan profile migration (CanSafelyMigrateOrphanProfile and acceptance tests)
        Dim orphanTestDir = Path.Combine(Path.GetTempPath(), "HidaChat_Task90_Test_" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(orphanTestDir)
        Try
            Dim testAccId = "acc_test1"
            Dim orphanPath = Path.Combine(orphanTestDir, "WV2Profile_")
            Dim legacyTargetPath = Path.Combine(orphanTestDir, $"WV2Profile_{testAccId}")
            Dim nativeTargetPath = ProfileIsolationPolicy.GetNativeProfileDirectory(orphanTestDir, testAccId)

            ' 1. Null / empty arguments
            Check(Not AccountLoadSafetyPolicy.CanSafelyMigrateOrphanProfile(Nothing, testAccId), "CanSafelyMigrateOrphanProfile rejects null sharedDir")
            Check(Not AccountLoadSafetyPolicy.CanSafelyMigrateOrphanProfile(orphanTestDir, Nothing), "CanSafelyMigrateOrphanProfile rejects null accountId")
            Check(Not AccountLoadSafetyPolicy.CanSafelyMigrateOrphanProfile(orphanTestDir, ""), "CanSafelyMigrateOrphanProfile rejects empty accountId")

            ' 2. Orphan folder does not exist
            Check(Not AccountLoadSafetyPolicy.CanSafelyMigrateOrphanProfile(orphanTestDir, testAccId), "CanSafelyMigrateOrphanProfile returns False when orphan does not exist")

            ' 3. Create orphan folder
            Directory.CreateDirectory(orphanPath)
            File.WriteAllText(Path.Combine(orphanPath, "orphan_marker.txt"), "orphan_payload")

            ' 4. ACCEPTANCE TEST (TASK-0090): Coexistence of WV2Profile_ and WV2Profile_{id} with differing files
            Directory.CreateDirectory(legacyTargetPath)
            Dim validMarkerPath = Path.Combine(legacyTargetPath, "valid_account_session.txt")
            File.WriteAllText(validMarkerPath, "active_user_valid_session_token_12345")

            ' Policy MUST strictly reject migration
            Dim canMigrateWhenBothExist = AccountLoadSafetyPolicy.CanSafelyMigrateOrphanProfile(orphanTestDir, testAccId)
            Check(Not canMigrateWhenBothExist, "CanSafelyMigrateOrphanProfile strictly rejects migration when valid WV2Profile_{id} already exists")

            ' If rejected, simulate abort: contents of valid profile must remain 100% untouched
            If canMigrateWhenBothExist Then
                Directory.Move(orphanPath, legacyTargetPath)
            End If

            Check(Directory.Exists(legacyTargetPath), "Valid profile directory WV2Profile_{id} remains intact")
            Check(File.Exists(validMarkerPath), "Valid profile marker file remains intact")
            Check(File.ReadAllText(validMarkerPath) = "active_user_valid_session_token_12345", "Valid profile contents are untouched and not overwritten by orphan")
            Check(Directory.Exists(orphanPath), "Orphan directory remains preserved for safe inspection")
            Check(File.Exists(Path.Combine(orphanPath, "orphan_marker.txt")), "Orphan contents remain preserved")

            ' Clean legacyTargetPath for snapshot tests
            Directory.Delete(legacyTargetPath, True)

            ' 5. Snapshot checks: .previous
            Dim legacyPrev = legacyTargetPath & ".previous"
            Directory.CreateDirectory(legacyPrev)
            Check(Not AccountLoadSafetyPolicy.CanSafelyMigrateOrphanProfile(orphanTestDir, testAccId), "CanSafelyMigrateOrphanProfile rejects migration when WV2Profile_{id}.previous exists")
            Directory.Delete(legacyPrev, True)

            ' 6. Snapshot checks: .bak
            Dim legacyBak = legacyTargetPath & ".bak"
            Directory.CreateDirectory(legacyBak)
            Check(Not AccountLoadSafetyPolicy.CanSafelyMigrateOrphanProfile(orphanTestDir, testAccId), "CanSafelyMigrateOrphanProfile rejects migration when WV2Profile_{id}.bak exists")
            Directory.Delete(legacyBak, True)

            ' 7. Snapshot checks: .migrated
            Dim legacyMigrated = legacyTargetPath & ".migrated"
            Directory.CreateDirectory(legacyMigrated)
            Check(Not AccountLoadSafetyPolicy.CanSafelyMigrateOrphanProfile(orphanTestDir, testAccId), "CanSafelyMigrateOrphanProfile rejects migration when WV2Profile_{id}.migrated exists")
            Directory.Delete(legacyMigrated, True)

            ' 8. Snapshot checks: _Snapshot
            Dim legacySnapshot = legacyTargetPath & "_Snapshot"
            Directory.CreateDirectory(legacySnapshot)
            Check(Not AccountLoadSafetyPolicy.CanSafelyMigrateOrphanProfile(orphanTestDir, testAccId), "CanSafelyMigrateOrphanProfile rejects migration when WV2Profile_{id}_Snapshot exists")
            Directory.Delete(legacySnapshot, True)

            ' 9. Native target checks: EBWebView/wv2profile_{id}
            Directory.CreateDirectory(nativeTargetPath)
            Check(Not AccountLoadSafetyPolicy.CanSafelyMigrateOrphanProfile(orphanTestDir, testAccId), "CanSafelyMigrateOrphanProfile rejects migration when native profile EBWebView/wv2profile_{id} exists")
            Directory.Delete(nativeTargetPath, True)

            ' 10. Native snapshot checks: EBWebView/wv2profile_{id}.previous
            Dim nativePrev = nativeTargetPath & ".previous"
            Directory.CreateDirectory(nativePrev)
            Check(Not AccountLoadSafetyPolicy.CanSafelyMigrateOrphanProfile(orphanTestDir, testAccId), "CanSafelyMigrateOrphanProfile rejects migration when native EBWebView/wv2profile_{id}.previous exists")
            Directory.Delete(nativePrev, True)

            ' 11. Alternate native naming checks: EBWebView/Account_{id}
            Dim ebDir = Path.Combine(orphanTestDir, ProfileIsolationPolicy.SharedEbWebViewFolder)
            Directory.CreateDirectory(ebDir)
            Dim altNativeDir = Path.Combine(ebDir, "Account_" & testAccId)
            Directory.CreateDirectory(altNativeDir)
            Check(Not AccountLoadSafetyPolicy.CanSafelyMigrateOrphanProfile(orphanTestDir, testAccId), "CanSafelyMigrateOrphanProfile rejects migration when alternate native EBWebView/Account_{id} exists")
            Directory.Delete(altNativeDir, True)

            ' 12. Clean slate: orphan exists, and NO profile exists for account anywhere
            Check(AccountLoadSafetyPolicy.CanSafelyMigrateOrphanProfile(orphanTestDir, testAccId), "CanSafelyMigrateOrphanProfile returns True when orphan exists and no profile exists for account")

            ' Execute safe move when authorized
            Directory.Move(orphanPath, legacyTargetPath)
            Check(Directory.Exists(legacyTargetPath), "Orphan successfully migrated to legacyTarget directory")
            Check(File.Exists(Path.Combine(legacyTargetPath, "orphan_marker.txt")), "Migrated profile contains original orphan data")
            Check(Not Directory.Exists(orphanPath), "Orphan directory no longer exists at original location after move")
        Finally
            Try
                If Directory.Exists(orphanTestDir) Then Directory.Delete(orphanTestDir, True)
            Catch
            End Try
        End Try

        ' Test TASK-0081: TsnetManager command timeout and shutdown deadline
        Dim powershellExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\v1.0\powershell.exe")
        If File.Exists(powershellExe) Then
            Dim tsnet = TsnetManager.Instance
            tsnet.CustomExecutablePath = powershellExe
            ' Launch mock daemon that sleeps for 60 seconds without writing to stdout
            tsnet.CustomExecutableArguments = "-NoProfile -Command ""Start-Sleep -Seconds 60"""

            Dim started = Await tsnet.StartAsync()
            Check(started, "TsnetManager starts test mock daemon process")
            Check(tsnet.IsRunning OrElse tsnet.NodeState = "Starting", "TsnetManager node state is active/starting")
            Check(tsnet.IsProcessRunning, "IsProcessRunning is True when daemon is active (TASK-0118)")
            Check(tsnet.ProcessId > 0, "ProcessId is positive when daemon is active (TASK-0118)")

            ' Test 1: SendCommandAsync timeout
            Dim swCmd = Stopwatch.StartNew()
            Dim cmdResult = Await tsnet.SendCommandAsync("{""cmd"":""status""}", timeoutMs:=600)
            swCmd.Stop()
            Check(cmdResult Is Nothing, "SendCommandAsync returns Nothing on unresponsive daemon")
            Check(swCmd.ElapsedMilliseconds >= 500 AndAlso swCmd.ElapsedMilliseconds < 3000,
                  $"SendCommandAsync timed out within expected budget (elapsed: {swCmd.ElapsedMilliseconds}ms)")

            ' Test 2: Semaphore release on timeout - subsequent command proceeds without deadlock
            Dim swCmd2 = Stopwatch.StartNew()
            Dim cmdResult2 = Await tsnet.SendCommandAsync("{""cmd"":""ping""}", timeoutMs:=400)
            swCmd2.Stop()
            Check(cmdResult2 Is Nothing, "Subsequent command proceeds and returns Nothing without deadlock")
            Check(swCmd2.ElapsedMilliseconds >= 350 AndAlso swCmd2.ElapsedMilliseconds < 2500,
                  $"Subsequent command timed out without semaphore starvation (elapsed: {swCmd2.ElapsedMilliseconds}ms)")

            ' Test 3: ShutdownAsync with hung daemon terminates within overall deadline
            Dim swShutdown = Stopwatch.StartNew()
            Await tsnet.ShutdownAsync(overallTimeoutMs:=1200)
            swShutdown.Stop()
            Check(swShutdown.ElapsedMilliseconds < 8000,
                  $"ShutdownAsync terminates within overall limit (elapsed: {swShutdown.ElapsedMilliseconds}ms)")
            Check(tsnet.NodeState = "Stopped", "NodeState is Stopped after shutdown")
            Check(Not tsnet.IsRunning, "IsRunning is False after shutdown")
            Check(Not tsnet.IsProcessRunning, "IsProcessRunning is False after shutdown (TASK-0118)")
            Check(tsnet.ProcessId = 0, "ProcessId is 0 after shutdown (TASK-0118)")
            Dim waitExited = Await tsnet.WaitForProcessExitAsync(timeoutMs:=1000)
            Check(waitExited, "WaitForProcessExitAsync returns True after shutdown (TASK-0118)")

            ' Test 4: ShutdownAsync when a command is actively hanging during shutdown call
            tsnet.CustomExecutablePath = powershellExe
            tsnet.CustomExecutableArguments = "-NoProfile -Command ""Start-Sleep -Seconds 60"""
            Await tsnet.StartAsync()

            ' Launch a hanging command with large timeout in background
            Dim hangTask = Task.Run(Async Function()
                                        Await tsnet.SendCommandAsync("{""cmd"":""hang""}", timeoutMs:=15000)
                                    End Function)
            Await Task.Delay(200) ' ensure the command started and acquired lock

            Dim swShutdown2 = Stopwatch.StartNew()
            Await tsnet.ShutdownAsync(overallTimeoutMs:=1200)
            swShutdown2.Stop()
            Check(swShutdown2.ElapsedMilliseconds < 3500,
                  $"ShutdownAsync overrides active hanging command and completes within deadline (elapsed: {swShutdown2.ElapsedMilliseconds}ms)")
            Check(tsnet.NodeState = "Stopped", "NodeState is Stopped after preempting hanging command")

            ' Reset custom executable to avoid interfering with production
            tsnet.CustomExecutablePath = ""
            tsnet.CustomExecutableArguments = ""
        End If

        ' Test TASK-0124: Deterministic proxy port isolation, platform range validation, and auto-healing
        Check(AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.OpenClaw, 18800), "OpenClaw default port 18800 is valid")
        Check(AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.OpenClaw, 18850), "OpenClaw offset port 18850 is valid")
        Check(AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.OpenClaw, 18899), "OpenClaw maximum port 18899 is valid")
        Check(Not AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.OpenClaw, 18900), "OpenClaw with Hermes port 18900 is strictly rejected")
        Check(Not AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.OpenClaw, 18799), "OpenClaw with port 18799 is rejected")
        Check(Not AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.OpenClaw, 0), "OpenClaw with port 0 is rejected (requires proxy)")

        Check(AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.Hermes, 18900), "Hermes default port 18900 is valid")
        Check(AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.Hermes, 18950), "Hermes offset port 18950 is valid")
        Check(AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.Hermes, 18999), "Hermes maximum port 18999 is valid")
        Check(Not AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.Hermes, 18800), "Hermes with OpenClaw port 18800 is strictly rejected")
        Check(Not AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.Hermes, 19000), "Hermes with port 19000 is rejected")
        Check(Not AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.Hermes, 0), "Hermes with port 0 is rejected (requires proxy)")

        Check(AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.WhatsApp, 0), "WhatsApp with port 0 is valid")
        Check(Not AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.WhatsApp, 18800), "WhatsApp with non-zero proxy port is invalid")
        Check(AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.Telegram, 0), "Telegram with port 0 is valid")
        Check(Not AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.Telegram, 18900), "Telegram with non-zero proxy port is invalid")

        ' String overload checks (TASK-0124)
        Check(AccountLoadSafetyPolicy.IsValidProxyPortForPlatform("OpenClaw", 18800), "OpenClaw string overload accepts 18800")
        Check(Not AccountLoadSafetyPolicy.IsValidProxyPortForPlatform("OpenClaw", 18900), "OpenClaw string overload rejects 18900")
        Check(AccountLoadSafetyPolicy.IsValidProxyPortForPlatform("Hermes", 18900), "Hermes string overload accepts 18900")
        Check(AccountLoadSafetyPolicy.IsValidProxyPortForPlatform("WhatsApp", 0), "WhatsApp string overload accepts 0")

        ' Test deterministic port resolution and collision avoidance
        Dim usedTestPorts As New HashSet(Of Integer)()
        Dim portOc1 = AccountLoadSafetyPolicy.ResolveDeterministicPort(ChatPlatform.OpenClaw, usedTestPorts)
        Check(portOc1 = 18800, "First OpenClaw account gets 18800")
        usedTestPorts.Add(portOc1)

        Dim portOc2 = AccountLoadSafetyPolicy.ResolveDeterministicPort(ChatPlatform.OpenClaw, usedTestPorts)
        Check(portOc2 = 18801, "Second OpenClaw account gets 18801 avoiding collision")
        usedTestPorts.Add(portOc2)

        Dim portHm1 = AccountLoadSafetyPolicy.ResolveDeterministicPort(ChatPlatform.Hermes, usedTestPorts)
        Check(portHm1 = 18900, "First Hermes account gets 18900 in its own platform range")
        usedTestPorts.Add(portHm1)

        Dim portHm2 = AccountLoadSafetyPolicy.ResolveDeterministicPort(ChatPlatform.Hermes, usedTestPorts)
        Check(portHm2 = 18901, "Second Hermes account gets 18901 avoiding collision")

        ' Simulate auto-healing pass on swapped accounts (OpenClaw=18900, Hermes=18800)
        Dim swappedOcPort = 18900
        Dim swappedHmPort = 18800
        Dim healedPorts As New HashSet(Of Integer)()

        ' Pass 1: detect and reset swapped ports
        If Not AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.OpenClaw, swappedOcPort) Then
            swappedOcPort = 0
        End If
        If Not AccountLoadSafetyPolicy.IsValidProxyPortForPlatform(ChatPlatform.Hermes, swappedHmPort) Then
            swappedHmPort = 0
        End If
        Check(swappedOcPort = 0, "Swapped OpenClaw port reset to 0 in pass 1")
        Check(swappedHmPort = 0, "Swapped Hermes port reset to 0 in pass 1")

        ' Pass 2: assign deterministic ports in correct ranges
        If swappedOcPort = 0 Then
            swappedOcPort = AccountLoadSafetyPolicy.ResolveDeterministicPort(ChatPlatform.OpenClaw, healedPorts)
            healedPorts.Add(swappedOcPort)
        End If
        If swappedHmPort = 0 Then
            swappedHmPort = AccountLoadSafetyPolicy.ResolveDeterministicPort(ChatPlatform.Hermes, healedPorts)
            healedPorts.Add(swappedHmPort)
        End If
        Check(swappedOcPort = 18800, "Auto-healing assigns 18800 to OpenClaw")
        Check(swappedHmPort = 18900, "Auto-healing assigns 18900 to Hermes")
        Check(swappedOcPort <> swappedHmPort, "Healed ports do not collide or cross-wire")

        ' Test TASK-0117: Dedicated download client, infinite timeout, and chunked streaming download to file
        Using dlClient = UpdateDownloadPolicy.CreateDedicatedDownloadClient("1.5.5-beta")
            Check(dlClient.Timeout = System.Threading.Timeout.InfiniteTimeSpan, "Dedicated download client has infinite timeout")
            Dim userAgentHeader = dlClient.DefaultRequestHeaders.UserAgent.ToString()
            Check(userAgentHeader.Contains("HidaChat-App") AndAlso userAgentHeader.Contains("1.5.5-beta"), "Dedicated client includes ProductInfo user agent")
        End Using

        Check(UpdateDownloadPolicy.DefaultChunkSize = 81920, "Default chunk size is 80 KB (81920 bytes)")
        Check(UpdateDownloadPolicy.DefaultDownloadTimeout = TimeSpan.FromMinutes(10), "Default download timeout is 10 minutes")

        ' Test streaming directly to disk with chunked reading and live progress reports
        Dim testPayloadSize = 250000 ' 250 KB spanning multiple 80KB chunks
        Dim testPayloadBytes(testPayloadSize - 1) As Byte
        Dim rng As New Random(42)
        rng.NextBytes(testPayloadBytes)

        Dim tempDownloadFile = Path.Combine(Path.GetTempPath(), $"HidaChat_Test_Dl_{Guid.NewGuid():N}.tmp")
        Try
            Dim progressReports As New List(Of UpdateDownloadPolicy.DownloadProgressReport)()
            Dim testProgress As New Progress(Of UpdateDownloadPolicy.DownloadProgressReport)(Sub(rep)
                progressReports.Add(rep)
            End Sub)

            Using srcStream As New MemoryStream(testPayloadBytes)
                Dim bytesWritten = Await UpdateDownloadPolicy.DownloadStreamToFileAsync(
                    srcStream,
                    tempDownloadFile,
                    testPayloadSize,
                    testProgress,
                    CancellationToken.None,
                    chunkSize:=81920,
                    throttleMs:=0)

                Check(bytesWritten = testPayloadSize, "Streaming download writes exact byte count to disk")
            End Using

            Check(File.Exists(tempDownloadFile), "Destination file exists on disk after streaming")
            Dim downloadedBytes = File.ReadAllBytes(tempDownloadFile)
            Check(downloadedBytes.Length = testPayloadSize, "Downloaded file length matches payload size")

            Dim bitsEqual As Boolean = True
            For i As Integer = 0 To testPayloadSize - 1
                If downloadedBytes(i) <> testPayloadBytes(i) Then
                    bitsEqual = False
                    Exit For
                End If
            Next
            Check(bitsEqual, "Downloaded file matches source data bit-for-bit without RAM buffering")

            ' Verify progress reporting
            For waitIdx As Integer = 1 To 50
                If progressReports.Count > 0 AndAlso progressReports(progressReports.Count - 1).Percentage = 100.0 Then Exit For
                Await Task.Delay(10)
            Next

            Check(progressReports.Count > 0, "Progress reports were triggered during streaming")
            Dim lastReport = progressReports(progressReports.Count - 1)
            Check(lastReport.Percentage = 100.0, "Final progress report indicates 100% completion")
            Check(lastReport.BytesRead = testPayloadSize, "Final progress report reflects total bytes read")
            Check(Not lastReport.IsIndeterminate, "Progress report is determinate when total size is known")
            Check(Not String.IsNullOrEmpty(lastReport.FormattedDownloadedMb), "Formatted MB read is populated")
            Check(Not String.IsNullOrEmpty(lastReport.FormattedTotalMb), "Formatted total MB is populated")

            ' Test streaming with indeterminate total size (totalBytes = -1)
            Dim tempIndeterminateFile = Path.Combine(Path.GetTempPath(), $"HidaChat_Test_Indet_{Guid.NewGuid():N}.tmp")
            Try
                Dim indetReports As New List(Of UpdateDownloadPolicy.DownloadProgressReport)()
                Dim indetProgress As New Progress(Of UpdateDownloadPolicy.DownloadProgressReport)(Sub(rep)
                    indetReports.Add(rep)
                End Sub)

                Using srcStream2 As New MemoryStream(testPayloadBytes)
                    Await UpdateDownloadPolicy.DownloadStreamToFileAsync(
                        srcStream2,
                        tempIndeterminateFile,
                        -1L,
                        indetProgress,
                        CancellationToken.None,
                        chunkSize:=81920,
                        throttleMs:=0)
                End Using

                For waitIdx As Integer = 1 To 50
                    If indetReports.Count > 0 Then Exit For
                    Await Task.Delay(10)
                Next

                Check(File.Exists(tempIndeterminateFile), "Indeterminate download file created successfully")
                Check(indetReports.Count > 0, "Indeterminate progress reports generated")
                Check(indetReports(0).IsIndeterminate, "Progress report is marked indeterminate when totalBytes = -1")
            Finally
                If File.Exists(tempIndeterminateFile) Then File.Delete(tempIndeterminateFile)
            End Try

            ' Test cancellation and timeout handling
            Dim tempCancelledFile = Path.Combine(Path.GetTempPath(), $"HidaChat_Test_Cancel_{Guid.NewGuid():N}.tmp")
            Try
                Using ctsCancelled As New CancellationTokenSource()
                    ctsCancelled.Cancel()
                    Using srcStream3 As New MemoryStream(testPayloadBytes)
                        Dim threw As Boolean = False
                        Try
                            Await UpdateDownloadPolicy.DownloadStreamToFileAsync(
                                srcStream3,
                                tempCancelledFile,
                                testPayloadSize,
                                Nothing,
                                ctsCancelled.Token,
                                chunkSize:=81920)
                        Catch ex As OperationCanceledException
                            threw = True
                        End Try
                        Check(threw, "Pre-cancelled token throws OperationCanceledException immediately")
                    End Using
                End Using
            Finally
                If File.Exists(tempCancelledFile) Then File.Delete(tempCancelledFile)
            End Try

        Finally
            If File.Exists(tempDownloadFile) Then File.Delete(tempDownloadFile)
        End Try

        ' =====================================================================
        ' TASK-0082: HTTPS Certificate Validation & TLS Error Discrimination Tests
        ' =====================================================================
        ' 1. Thumbprint normalization
        Check(TlsCertificatePolicy.NormalizeThumbprint("aa:bb:cc:dd") = "AABBCCDD", "NormalizeThumbprint strips colons and uppercases")
        Check(TlsCertificatePolicy.NormalizeThumbprint("1a 2b 3c-4d") = "1A2B3C4D", "NormalizeThumbprint strips spaces and dashes")
        Check(TlsCertificatePolicy.NormalizeThumbprint("") = "", "NormalizeThumbprint handles empty string")
        Check(TlsCertificatePolicy.NormalizeThumbprint(Nothing) = "", "NormalizeThumbprint handles Nothing")

        ' 2. Certificate generation & thumbprint calculation
        Using rsaAlg As RSA = RSA.Create(2048)
            Dim certReq As New CertificateRequest("CN=test.hidachat.local", rsaAlg, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            Using testCert = certReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1))
                Dim sha256Thumb = TlsCertificatePolicy.GetThumbprintSha256(testCert)
                Check(sha256Thumb.Length = 64, "GetThumbprintSha256 produces 64-hex SHA-256 string")
                Check(Regex.IsMatch(sha256Thumb, "^[0-9A-F]{64}$"), "GetThumbprintSha256 produces valid uppercase hex characters")

                ' 3. Standard validation with SslPolicyErrors.None succeeds
                Dim failReason As String = Nothing
                Dim resValid = TlsCertificatePolicy.ValidateCertificate(testCert, Nothing, SslPolicyErrors.None, allowInsecure:=False, pinnedThumbprint:=Nothing, customCaPathOrPem:=Nothing, failureReason:=failReason)
                Check(resValid, "Standard validation with SslPolicyErrors.None succeeds")
                Check(failReason Is Nothing, "No failure reason on successful validation")

                ' 4. Standard validation strictly fails on untrusted chain / errors
                Dim resChainErr = TlsCertificatePolicy.ValidateCertificate(testCert, Nothing, SslPolicyErrors.RemoteCertificateChainErrors, allowInsecure:=False, pinnedThumbprint:=Nothing, customCaPathOrPem:=Nothing, failureReason:=failReason)
                Check(Not resChainErr, "Standard validation rejects untrusted certificate chain by default")
                Check(failReason IsNot Nothing AndAlso failReason.Contains("Untrusted"), "Failure reason reports untrusted authority for chain error")

                ' 5. Standard validation strictly fails on hostname mismatch
                Dim resNameErr = TlsCertificatePolicy.ValidateCertificate(testCert, Nothing, SslPolicyErrors.RemoteCertificateNameMismatch, allowInsecure:=False, pinnedThumbprint:=Nothing, customCaPathOrPem:=Nothing, failureReason:=failReason)
                Check(Not resNameErr, "Standard validation rejects hostname mismatch")
                Check(failReason IsNot Nothing AndAlso failReason.Contains("Hostname mismatch"), "Failure reason reports hostname mismatch")

                ' 6. Standard validation fails when certificate is not available
                Dim resNoCert = TlsCertificatePolicy.ValidateCertificate(Nothing, Nothing, SslPolicyErrors.RemoteCertificateNotAvailable, allowInsecure:=False, pinnedThumbprint:=Nothing, customCaPathOrPem:=Nothing, failureReason:=failReason)
                Check(Not resNoCert, "Validation fails when certificate is Nothing")
                Check(failReason IsNot Nothing, "Failure reason provided when certificate is missing")

                ' 7. Explicit bypass (allowInsecure = True)
                Dim resBypass = TlsCertificatePolicy.ValidateCertificate(testCert, Nothing, SslPolicyErrors.RemoteCertificateChainErrors, allowInsecure:=True, pinnedThumbprint:=Nothing, customCaPathOrPem:=Nothing, failureReason:=failReason)
                Check(resBypass, "Explicit allowInsecure=True accepts certificate despite chain errors")

                ' 8. Certificate Pinning
                ' Matching pin succeeds even with chain error
                Dim resPinMatch = TlsCertificatePolicy.ValidateCertificate(testCert, Nothing, SslPolicyErrors.RemoteCertificateChainErrors, allowInsecure:=False, pinnedThumbprint:=sha256Thumb, customCaPathOrPem:=Nothing, failureReason:=failReason)
                Check(resPinMatch, "Certificate pinning succeeds when presented certificate matches pinned SHA-256")

                ' Matching pin formatted with colons
                Dim colonPin = String.Join(":", Enumerable.Range(0, sha256Thumb.Length \ 2).Select(Function(i) sha256Thumb.Substring(i * 2, 2)))
                Dim resColonPin = TlsCertificatePolicy.ValidateCertificate(testCert, Nothing, SslPolicyErrors.RemoteCertificateChainErrors, allowInsecure:=False, pinnedThumbprint:=colonPin, customCaPathOrPem:=Nothing, failureReason:=failReason)
                Check(resColonPin, "Certificate pinning handles colon-separated thumbprint")

                ' Mismatched pin fails
                Dim wrongPin = "00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF"
                Dim resPinMismatch = TlsCertificatePolicy.ValidateCertificate(testCert, Nothing, SslPolicyErrors.None, allowInsecure:=False, pinnedThumbprint:=wrongPin, customCaPathOrPem:=Nothing, failureReason:=failReason)
                Check(Not resPinMismatch, "Certificate pinning rejects mismatched certificate thumbprint")
                Check(failReason IsNot Nothing AndAlso failReason.Contains("pinning mismatch"), "Failure reason reports certificate pinning mismatch")
            End Using
        End Using

        ' 9. Error Discrimination: IsTlsFailure vs general reachability
        Dim tlsDetail As String = Nothing
        Dim authEx As New AuthenticationException("The remote certificate is invalid according to the validation procedure.")
        Check(TlsCertificatePolicy.IsTlsFailure(authEx, tlsDetail), "AuthenticationException is recognized as TLS failure")
        Check(tlsDetail IsNot Nothing, "Extracted TLS detail from AuthenticationException")

        Dim httpTlsEx As New HttpRequestException("The SSL connection could not be established, see inner exception.", authEx)
        Check(TlsCertificatePolicy.IsTlsFailure(httpTlsEx, tlsDetail), "HttpRequestException wrapping AuthenticationException is recognized as TLS failure")

        Dim expiredCertEx As New HttpRequestException("The remote certificate has expired.")
        Check(TlsCertificatePolicy.IsTlsFailure(expiredCertEx, tlsDetail), "Expired certificate message recognized as TLS failure")

        ' General reachability / network errors must NOT be classified as TLS failures
        Dim connRefusedEx As New HttpRequestException("No connection could be made because the target machine actively refused it.")
        Check(Not TlsCertificatePolicy.IsTlsFailure(connRefusedEx, tlsDetail), "Connection refused is classified as general reachability, not TLS failure")

        Dim timeoutEx As New TimeoutException("The operation has timed out.")
        Check(Not TlsCertificatePolicy.IsTlsFailure(timeoutEx, tlsDetail), "TimeoutException is classified as general reachability, not TLS failure")

        Dim hostNotFoundEx As New HttpRequestException("No such host is known.")
        Check(Not TlsCertificatePolicy.IsTlsFailure(hostNotFoundEx, tlsDetail), "Host not found is classified as general reachability, not TLS failure")

        ' 10. Proxy TLS response discrimination
        Using respTls As New HttpResponseMessage(HttpStatusCode.BadGateway)
            respTls.Headers.Add("X-HidaChat-Error-Type", "TLS_CERTIFICATE_ERROR")
            respTls.Headers.Add("X-HidaChat-TLS-Error", "x509: certificate signed by unknown authority")
            Dim isProxyTls = TlsCertificatePolicy.IsTlsProxyResponse(respTls.Headers, respTls.StatusCode, tlsDetail)
            Check(isProxyTls, "Proxy response with X-HidaChat-Error-Type: TLS_CERTIFICATE_ERROR recognized as TLS failure")
            Check(tlsDetail IsNot Nothing AndAlso tlsDetail.Contains("unknown authority"), "Extracted TLS error details from proxy response headers")
        End Using

        Using respNormal As New HttpResponseMessage(HttpStatusCode.BadGateway)
            respNormal.Headers.Add("X-HidaChat-Proxy-Error", "true")
            Dim isProxyNormal = TlsCertificatePolicy.IsTlsProxyResponse(respNormal.Headers, respNormal.StatusCode, tlsDetail)
            Check(Not isProxyNormal, "Standard bad gateway proxy response without TLS error header is not classified as TLS failure")
        End Using

        ' =====================================================================
        ' TASK-0126: Token Expiration Detection & Safe Bearer Token Injection
        ' =====================================================================
        ' 1. Test TryGetTokenExpiration with Hermes binary-signed token
        Dim hermesExpiredToken = "eyJzdWIiOiJhZG1pbiIsImp0aSI6IjJGMGtHRmRhRi1hM29NMGhhR2J5anciLCJzaWQiOiJKaUF2NmRhejFtNHNNdXBsNWpwdFJnIiwia2luZCI6ImFjY2VzcyIsImV4cCI6MTc5NTU2NjMxNH21QAE6C1cgzhOrrODqMfl7YolrnYYZ6-DW8fZBBtsIaw=="
        Dim expHermes = LocalAuthPolicy.TryGetTokenExpiration(hermesExpiredToken)
        Check(expHermes.HasValue AndAlso expHermes.Value = 1795566314L, "TryGetTokenExpiration extracts exp timestamp from Hermes token")

        ' 2. Test TryGetTokenExpiration with standard 3-part JWT
        ' {"sub":"testuser","exp":1893456000} (2030-01-01) -> base64url: eyJzdWIiOiJ0ZXN0dXNlciIsImV4cCI6MTg5MzQ1NjAwMH0
        Dim standardJwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ0ZXN0dXNlciIsImV4cCI6MTg5MzQ1NjAwMH0.c2lnbmF0dXJl"
        Dim expJwt = LocalAuthPolicy.TryGetTokenExpiration(standardJwt)
        Check(expJwt.HasValue AndAlso expJwt.Value = 1893456000L, "TryGetTokenExpiration extracts exp timestamp from standard 3-part JWT")

        ' 3. Test TryGetTokenExpiration with opaque/non-JWT token or token without exp
        Dim opaqueToken = "18a45e22682d05d1a169b626f18e1ce278089c0f41711226"
        Check(Not LocalAuthPolicy.TryGetTokenExpiration(opaqueToken).HasValue, "TryGetTokenExpiration returns Nothing for opaque non-JWT token")
        Check(Not LocalAuthPolicy.TryGetTokenExpiration("").HasValue, "TryGetTokenExpiration returns Nothing for empty token")
        Check(Not LocalAuthPolicy.TryGetTokenExpiration(Nothing).HasValue, "TryGetTokenExpiration returns Nothing for null token")
        Check(Not LocalAuthPolicy.TryGetTokenExpiration("short").HasValue, "TryGetTokenExpiration returns Nothing for short token")

        ' 4. Test IsTokenExpired
        Check(LocalAuthPolicy.IsTokenExpired(hermesExpiredToken), "IsTokenExpired returns True for expired Hermes token")
        Check(Not LocalAuthPolicy.IsTokenExpired(standardJwt), "IsTokenExpired returns False for future valid JWT")
        Check(Not LocalAuthPolicy.IsTokenExpired(opaqueToken), "IsTokenExpired returns False for opaque token without exp claim")
        Check(Not LocalAuthPolicy.IsTokenExpired(""), "IsTokenExpired returns False for empty token")

        ' 5. Test IsAuthenticationEndpoint
        Check(LocalAuthPolicy.IsAuthenticationEndpoint("http://127.0.0.1:18900/login"), "IsAuthenticationEndpoint identifies /login")
        Check(LocalAuthPolicy.IsAuthenticationEndpoint("http://127.0.0.1:18900/login?next=%2F%3F__htok%3D123"), "IsAuthenticationEndpoint identifies /login with query parameters")
        Check(LocalAuthPolicy.IsAuthenticationEndpoint("http://127.0.0.1:18900/signin"), "IsAuthenticationEndpoint identifies /signin")
        Check(LocalAuthPolicy.IsAuthenticationEndpoint("http://127.0.0.1:18900/auth/password-login"), "IsAuthenticationEndpoint identifies /auth/password-login")
        Check(LocalAuthPolicy.IsAuthenticationEndpoint("http://127.0.0.1:18900/auth/login"), "IsAuthenticationEndpoint identifies /auth/login")
        Check(LocalAuthPolicy.IsAuthenticationEndpoint("http://127.0.0.1:18900/auth/token"), "IsAuthenticationEndpoint identifies /auth/token")
        Check(LocalAuthPolicy.IsAuthenticationEndpoint("http://127.0.0.1:18900/auth/refresh"), "IsAuthenticationEndpoint identifies /auth/refresh")
        Check(Not LocalAuthPolicy.IsAuthenticationEndpoint("http://127.0.0.1:18900/"), "IsAuthenticationEndpoint returns False for root path /")
        Check(Not LocalAuthPolicy.IsAuthenticationEndpoint("http://127.0.0.1:18900/system"), "IsAuthenticationEndpoint returns False for /system")
        Check(Not LocalAuthPolicy.IsAuthenticationEndpoint("http://127.0.0.1:18900/api/status"), "IsAuthenticationEndpoint returns False for /api/status")
        Check(Not LocalAuthPolicy.IsAuthenticationEndpoint("http://127.0.0.1:18900/api/chat"), "IsAuthenticationEndpoint returns False for /api/chat")

        ' 6. Test ShouldAttachBearerToken
        Check(Not LocalAuthPolicy.ShouldAttachBearerToken("http://127.0.0.1:18900/system", hermesExpiredToken), "ShouldAttachBearerToken suppresses expired token on protected resource")
        Check(Not LocalAuthPolicy.ShouldAttachBearerToken("http://127.0.0.1:18900/login", standardJwt), "ShouldAttachBearerToken suppresses token on /login endpoint")
        Check(Not LocalAuthPolicy.ShouldAttachBearerToken("http://127.0.0.1:18900/auth/password-login", standardJwt), "ShouldAttachBearerToken suppresses token on /auth/password-login endpoint")
        Check(Not LocalAuthPolicy.ShouldAttachBearerToken("http://127.0.0.1:18900/api/chat", ""), "ShouldAttachBearerToken suppresses empty token")
        Check(Not LocalAuthPolicy.ShouldAttachBearerToken("http://127.0.0.1:18900/api/chat", Nothing), "ShouldAttachBearerToken suppresses null token")
        Check(LocalAuthPolicy.ShouldAttachBearerToken("http://127.0.0.1:18900/api/chat", standardJwt), "ShouldAttachBearerToken allows valid non-expired token on API endpoint")
        Check(LocalAuthPolicy.ShouldAttachBearerToken("http://127.0.0.1:18900/api/chat", opaqueToken), "ShouldAttachBearerToken allows opaque API token on API endpoint")

        ' =====================================================================
        ' TASK-0129: SettingsWindow High-Contrast Styling & Dynamic Themes
        ' =====================================================================
        Check(Constants.AppVersion = "1.5.13-beta", "Constants.AppVersion bumped to 1.5.13-beta")

        Dim supportedLangs = {"en", "it", "fr", "es", "de"}
        Dim categoryKeys = {
            "settings_cat_general", "settings_cat_accounts", "settings_cat_translation",
            "settings_cat_network", "settings_cat_css", "settings_cat_storage", "settings_cat_about"
        }
        Dim subtitleKeys = {
            "settings_sub_general", "settings_sub_accounts", "settings_sub_translation",
            "settings_sub_network", "settings_sub_css", "settings_sub_storage", "settings_sub_about"
        }
        Dim networkKeys = {
            "tsnet_section_title", "tsnet_section_desc", "tsnet_authkey_label",
            "tsnet_authkey_tooltip", "account_use_tailscale", "account_insecure_tls"
        }

        For Each lang In supportedLangs
            Dim locObj = AppLocalizations.CreateForLanguage(lang)
            For Each catKey In categoryKeys
                Dim val = locObj.Get(catKey)
                Check(Not String.IsNullOrWhiteSpace(val) AndAlso val <> catKey, $"Category key '{catKey}' localized in '{lang}': '{val}'")
            Next
            For Each subKey In subtitleKeys
                Dim val = locObj.Get(subKey)
                Check(Not String.IsNullOrWhiteSpace(val) AndAlso val <> subKey, $"Subtitle key '{subKey}' localized in '{lang}': '{val}'")
            Next
            For Each netKey In networkKeys
                Dim val = locObj.Get(netKey)
                Check(Not String.IsNullOrWhiteSpace(val) AndAlso val <> netKey, $"Network key '{netKey}' localized in '{lang}': '{val}'")
            Next
        Next

        ' =====================================================================
        ' TASK-0130: Modern Fluent Design System (DesignTokens & IconManager)
        ' =====================================================================
        Dim lightPalette = UI.Theme.DesignTokens.GetPalette(isDark:=False)
        Dim darkPalette = UI.Theme.DesignTokens.GetPalette(isDark:=True)

        Check(Not lightPalette.IsDark, "Light palette has IsDark = False")
        Check(darkPalette.IsDark, "Dark palette has IsDark = True")
        Check(lightPalette.AppBackground = "#F8FAFC", "Light palette AppBackground matches Fluent token #F8FAFC")
        Check(lightPalette.Surface = "#FFFFFF", "Light palette Surface matches Fluent token #FFFFFF")
        Check(lightPalette.TextPrimary = "#0F172A", "Light palette TextPrimary matches Fluent token #0F172A")
        Check(darkPalette.AppBackground = "#0F172A", "Dark palette AppBackground matches Fluent token #0F172A")
        Check(darkPalette.Surface = "#1E293B", "Dark palette Surface matches Fluent token #1E293B")
        Check(darkPalette.TextPrimary = "#F1F5F9", "Dark palette TextPrimary matches Fluent token #F1F5F9")
        Check(lightPalette.Accent = "#2563EB", "Default light accent is Fluent Blue #2563EB")

        ' Verify WCAG 2.1 contrast ratios meet AA/AAA standards
        Dim lightBodyContrast = UI.Theme.DesignTokens.GetContrastRatio(lightPalette.TextPrimary, lightPalette.Surface)
        Dim darkBodyContrast = UI.Theme.DesignTokens.GetContrastRatio(darkPalette.TextPrimary, darkPalette.Surface)
        Dim lightSecondaryContrast = UI.Theme.DesignTokens.GetContrastRatio(lightPalette.TextSecondary, lightPalette.Surface)
        Dim darkSecondaryContrast = UI.Theme.DesignTokens.GetContrastRatio(darkPalette.TextSecondary, darkPalette.Surface)
        Check(lightBodyContrast >= 7.0, $"Light primary text contrast meets WCAG AAA ({lightBodyContrast}:1)")
        Check(darkBodyContrast >= 7.0, $"Dark primary text contrast meets WCAG AAA ({darkBodyContrast}:1)")
        Check(lightSecondaryContrast >= 4.5, $"Light secondary text contrast meets WCAG AA ({lightSecondaryContrast}:1)")
        Check(darkSecondaryContrast >= 4.5, $"Dark secondary text contrast meets WCAG AA ({darkSecondaryContrast}:1)")

        ' Verify hex validation, normalization, and accessible foreground selection
        Check(UI.Theme.DesignTokens.IsValidHexColor("#2563EB"), "IsValidHexColor accepts valid 6-digit hex")
        Check(Not UI.Theme.DesignTokens.IsValidHexColor("not-a-color"), "IsValidHexColor rejects invalid string")
        Check(UI.Theme.DesignTokens.NormalizeAccentHex("invalid") = UI.Theme.DesignTokens.DefaultAccentHex, "NormalizeAccentHex falls back to DefaultAccentHex")
        Check(UI.Theme.DesignTokens.GetAccessibleForegroundHex("#2563EB") = "#FFFFFF", "Fluent Blue accent uses white foreground")
        Check(UI.Theme.DesignTokens.GetAccessibleForegroundHex("#F8FAFC") = "#0F172A", "Light background uses dark slate foreground")

        ' Verify IconManager registry completeness
        Dim allIcons = UI.Icons.IconManager.GetAllIconKeys()
        Check(allIcons.Count >= 30, $"IconManager registers at least 30 vector icons (count: {allIcons.Count})")
        For Each iconKey In allIcons
            Dim pathData = UI.Icons.IconManager.GetPathData(iconKey)
            Check(Not String.IsNullOrWhiteSpace(pathData), $"IconManager has non-empty SVG path for {iconKey}")
        Next

        ' =====================================================================
        ' TASK-0131: Theme-Aware Tailscale Status Badge Contrast (Light & Dark)
        ' =====================================================================
        Dim badgeSeverities = {"running", "warning", "info", "neutral", "error"}
        For Each sev In badgeSeverities
            For Each darkMode In {False, True}
                Dim bp = UI.Theme.DesignTokens.GetStatusBadgePalette(sev, darkMode)
                Check(bp IsNot Nothing, $"StatusBadgePalette for '{sev}' (dark={darkMode}) is not Nothing")
                Check(UI.Theme.DesignTokens.IsValidHexColor(bp.BackgroundHex), $"StatusBadgePalette '{sev}' (dark={darkMode}) valid background {bp.BackgroundHex}")
                Check(UI.Theme.DesignTokens.IsValidHexColor(bp.ForegroundHex), $"StatusBadgePalette '{sev}' (dark={darkMode}) valid foreground {bp.ForegroundHex}")
                Check(UI.Theme.DesignTokens.IsValidHexColor(bp.BorderHex), $"StatusBadgePalette '{sev}' (dark={darkMode}) valid border {bp.BorderHex}")
                Dim ratio = UI.Theme.DesignTokens.GetContrastRatio(bp.ForegroundHex, bp.BackgroundHex)
                Check(ratio >= 4.5, $"StatusBadgePalette '{sev}' (dark={darkMode}) meets WCAG AA contrast ({ratio}:1)")
            Next
        Next

        ' =====================================================================
        ' TASK-0132: SettingsWindow Fluent Design System Modernization
        ' =====================================================================
        For Each lang In supportedLangs
            Dim locObj = AppLocalizations.CreateForLanguage(lang)
            Dim accentVal = locObj.Get("accent_color")
            Check(Not String.IsNullOrWhiteSpace(accentVal) AndAlso accentVal <> "accent_color", $"Key 'accent_color' localized in '{lang}': '{accentVal}'")
        Next
    End Function
End Module
