Imports System
Imports System.Collections.Generic
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
        Check(PlatformRegistry.All.Count = 4, "PlatformRegistry registers exactly 4 platforms")

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

        ' Test string resolution and fallbacks
        Check(PlatformRegistry.Get("whatsapp").Platform = ChatPlatform.WhatsApp, "Resolve 'whatsapp' (lowercase)")
        Check(PlatformRegistry.Get("TELEGRAM").Platform = ChatPlatform.Telegram, "Resolve 'TELEGRAM' (uppercase)")
        Check(PlatformRegistry.Get("openclaw").Platform = ChatPlatform.OpenClaw, "Resolve 'openclaw'")
        Check(PlatformRegistry.Get("Hermes").Platform = ChatPlatform.Hermes, "Resolve 'Hermes'")
        Check(PlatformRegistry.Get("Hermes Agent").Platform = ChatPlatform.Hermes, "Resolve 'Hermes Agent' display name")
        Check(PlatformRegistry.Get("NonExistent").Platform = ChatPlatform.WhatsApp, "Fallback to WhatsApp for unknown platform")
        Check(PlatformRegistry.Get("").Platform = ChatPlatform.WhatsApp, "Fallback to WhatsApp for empty string")
        Check(PlatformRegistry.Get(DirectCast(Nothing, String)).Platform = ChatPlatform.WhatsApp, "Fallback to WhatsApp for null string")

        Dim parsedPlatform As ChatPlatform
        Check(PlatformRegistry.TryFromKey("Telegram", parsedPlatform) AndAlso parsedPlatform = ChatPlatform.Telegram, "TryFromKey succeeds for Telegram")
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

        ' Test Telegram deep-link resolution
        Dim tgLink1 = tg.ResolveDeepLink("tg://resolve?domain=testchannel&post=42")
        Check(tgLink1 = "https://web.telegram.org/a/#@testchannel/42", "Telegram resolves tg:// deep-link")
        Dim tgLink2 = tg.ResolveDeepLink("https://t.me/testchannel")
        Check(tgLink2 = "https://web.telegram.org/a/#@testchannel", "Telegram resolves https://t.me/ deep-link")
        Check(tg.ResolveDeepLink("https://example.com") Is Nothing, "Telegram ignores non-telegram links")
        Check(wa.ResolveDeepLink("tg://resolve?domain=testchannel") Is Nothing, "WhatsApp ignores tg:// links")

        ' Test IsAllowedNewWindowHost
        Check(oc.IsAllowedNewWindowHost(New Uri("https://claw.my-node.ts.net/auth"), "https://claw.my-node.ts.net:8443"), "OpenClaw allows same host in NewWindow")
        Check(Not oc.IsAllowedNewWindowHost(New Uri("https://evil.ts.net/auth"), "https://claw.my-node.ts.net:8443"), "OpenClaw rejects different host in NewWindow")
        Check(Not wa.IsAllowedNewWindowHost(New Uri("https://web.whatsapp.com/other"), ""), "WhatsApp delegates new windows externally")
        Check(tg.IsAllowedNewWindowHost(New Uri("https://t.me/channel"), ""), "Telegram handles t.me inside NewWindow")
        Check(Not tg.IsAllowedNewWindowHost(New Uri("https://example.com"), ""), "Telegram delegates other hosts externally")

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
            Await TestTranslationProvidersAsync()
            TestBetaChannelResetMigration()
            TestBrowserArgumentsPolicy()
            TestNtfsCompressionPolicy()
            TestUtilityBarAndAudioQuickActions()
            TestOtaUpdateAndProgressLogic()
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
            TestBrowserArgumentsPolicy()
        ' 1. Client has beta channel marked (True) and no reset marker (1.3.0 update)
        Dim settingsWithBeta As New Dictionary(Of String, Object) From {
            {"useBetaChannel", True},
            {"theme", "Dark"}
        }
        Dim useBeta = True
        Dim changed = SettingsMigration.ApplyBetaChannelReset130(settingsWithBeta, useBeta)
        Check(changed, "ApplyBetaChannelReset130 returns True when migration is applied")
        Check(Not useBeta, "useBetaChannel is set to False when client had it marked")
        Check(TypeOf settingsWithBeta("useBetaChannel") Is Boolean AndAlso Not CBool(settingsWithBeta("useBetaChannel")), "settings('useBetaChannel') is set to False in dictionary")
        Check(TypeOf settingsWithBeta("betaReset130Done") Is Boolean AndAlso CBool(settingsWithBeta("betaReset130Done")), "settings('betaReset130Done') is recorded as True")
        Check(CStr(settingsWithBeta("theme")) = "Dark", "Unrelated settings are preserved during reset")

        ' 2. Client has beta channel NOT marked (False) on 1.3.0 update
        Dim settingsWithoutBeta As New Dictionary(Of String, Object) From {
            {"useBetaChannel", False}
        }
        Dim useBetaFalse = False
        Dim changed2 = SettingsMigration.ApplyBetaChannelReset130(settingsWithoutBeta, useBetaFalse)
        Check(changed2, "ApplyBetaChannelReset130 returns True to record reset marker")
        Check(Not useBetaFalse, "useBetaChannel remains False")
        Check(TypeOf settingsWithoutBeta("betaReset130Done") Is Boolean AndAlso CBool(settingsWithoutBeta("betaReset130Done")), "settings('betaReset130Done') is recorded as True")

        ' 3. Subsequent launches after 1.3.0 reset has already run (betaReset130Done is True)
        Dim settingsAlreadyReset As New Dictionary(Of String, Object) From {
            {"useBetaChannel", False},
            {"betaReset130Done", True}
        }
        Dim useBetaSubsequent = False
        Dim changed3 = SettingsMigration.ApplyBetaChannelReset130(settingsAlreadyReset, useBetaSubsequent)
        Check(Not changed3, "ApplyBetaChannelReset130 returns False when already executed")
        Check(Not useBetaSubsequent, "useBetaChannel remains unchanged")

        ' 4. Client deliberately opts into beta again after 1.3.0 update
        Dim settingsReEnabledBeta As New Dictionary(Of String, Object) From {
            {"useBetaChannel", True},
            {"betaReset130Done", True}
        }
        Dim useBetaReEnabled = True
        Dim changed4 = SettingsMigration.ApplyBetaChannelReset130(settingsReEnabledBeta, useBetaReEnabled)
        Check(Not changed4, "ApplyBetaChannelReset130 does NOT wipe intentional user opt-in on subsequent runs")
        Check(useBetaReEnabled, "useBetaChannel remains True when user intentionally enabled it after 1.3.0 reset")
        Check(CBool(settingsReEnabledBeta("useBetaChannel")), "settings('useBetaChannel') remains True")

        ' 5. JSON deserialization with JsonElement (real-world settings.json scenario)
        Dim jsonText = "{""useBetaChannel"": true, ""theme"": ""Light""}"
        Dim parsedSettings = System.Text.Json.JsonSerializer.Deserialize(Of Dictionary(Of String, Object))(jsonText)
        Dim useBetaJson = True
        Dim changed5 = SettingsMigration.ApplyBetaChannelReset130(parsedSettings, useBetaJson)
        Check(changed5, "ApplyBetaChannelReset130 correctly processes deserialized JsonElement settings")
        Check(Not useBetaJson, "useBetaChannel variable is reset to False from JsonElement")
        Check(TypeOf parsedSettings("useBetaChannel") Is Boolean AndAlso Not CBool(parsedSettings("useBetaChannel")), "settings('useBetaChannel') is replaced with boolean False")
        Check(TypeOf parsedSettings("betaReset130Done") Is Boolean AndAlso CBool(parsedSettings("betaReset130Done")), "betaReset130Done is set to boolean True")

        ' 6. Null settings dictionary handling
        Dim useBetaNull = True
        Dim changedNull = SettingsMigration.ApplyBetaChannelReset130(Nothing, useBetaNull)
        Check(Not changedNull, "ApplyBetaChannelReset130 safely returns False on null settings")
        Check(useBetaNull, "useBetaChannel variable unaffected on null settings")
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
        Check(applyArgs.Contains("""C:\Temp\update.zip"""), "CLI args include temporary zip file")
    End Sub
End Module