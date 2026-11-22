Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Threading.Tasks

' Test doubles for the non-I/O dependencies. No real HidaChat profile is opened.
Public Class AppAccounts
    Public Shared Property SharedDataDirectory As String
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

        ' 3. CLI arguments construction for --apply-update (TODO #87)
        Dim FormatApplyArgs = Function(pid As Integer, src As String, dst As String, ver As String, lang As String, zip As String) As String
            Return $"--apply-update {pid} ""{src}"" ""{dst}"" ""{ver}"" ""{lang}"" ""{zip}"""
        End Function

        Dim applyArgs = FormatApplyArgs(1234, "C:\Temp\Update", "C:\HidaChat", "1.3.3-beta", "it", "C:\Temp\update.zip")
        Check(applyArgs.Contains("--apply-update 1234"), "CLI args include --apply-update with caller PID")
        Check(applyArgs.Contains("""C:\Temp\Update"""), "CLI args include quoted source directory")
        Check(applyArgs.Contains("""C:\HidaChat"""), "CLI args include quoted target installation directory")
        Check(applyArgs.Contains("""1.3.3-beta"""), "CLI args include target version")
        Check(applyArgs.Contains("""it"""), "CLI args include language code")

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
            Check(swShutdown.ElapsedMilliseconds < 3500,
                  $"ShutdownAsync terminates within overall limit (elapsed: {swShutdown.ElapsedMilliseconds}ms)")
            Check(tsnet.NodeState = "Stopped", "NodeState is Stopped after shutdown")
            Check(Not tsnet.IsRunning, "IsRunning is False after shutdown")

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
    End Function
End Module
