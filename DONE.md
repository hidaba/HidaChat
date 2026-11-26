# Completed Tasks

[![English](https://img.shields.io/badge/Language-English-blue.svg)](DONE.md)
[![Italiano](https://img.shields.io/badge/Lingua-Italiano-green.svg)](DONE.it.md)

## 2026-11-26

- [x] **TASK-0119** [High] Installation directory backup, automatic rollback, and application relaunch on update copy failure
  - Result:
    - Created `UpdateBackupPolicy.vb` providing `CreateSafetyBackup`, `RestoreFromSafetyBackup`, and `DeleteSafetyBackup` routines that strictly exclude user data (`data/`, `data/*`, `.git/*`), temporary update assets, and batch scripts, guaranteeing 100% application portability.
    - Updated `Application.xaml.vb` (`HandleApplyUpdateStartup`): creates a safety backup of existing binaries in `%TEMP_DIR%\backup` before attempting file copy. If file copy or robocopy fails, automatically restores previous binaries from the safety backup, cleans up temporary files, displays a clear localized rollback notification, and relaunches the previous working version of `HidaChat.exe`.
    - Enforced that `.app_version` marker is written strictly after file copy succeeds and is verified, and cleans up safety backup directory on success.
    - Hardened updater batch script in `UpdateScriptPolicy.vb` (`GenerateUpdateBatchScript`): sets `%BACKUP_DIR%`, creates safety copy via robocopy excluding `data` and `.git` before file replacement, and on failure (`%RC% GEQ 8`) automatically restores previous binaries from `%BACKUP_DIR%` and relaunches `start "" "%DST%\%EXE%"`.
    - Added localized UI dictionary keys (`update_stage_backup`, `update_stage_rollback`, `update_rollback_success`) synchronously across all 5 official languages (`Localization.vb`).
  - Verification: Added 19 automated regression assertions in `Tests/SessionTests.vb` covering batch script rollback and relaunch commands, path exclusion policies (`IsUserDataPath`, `ShouldExcludeFile`), and an end-to-end simulated safety backup, binary corruption, rollback restoration, and data preservation test (totaling **975 automated regression assertions with 0 errors**). Full project compilation succeeded with 0 warnings and 0 errors.

- [x] **TASK-0118** [High] Target-specific process termination by PID and orderly shutdown of tsnetd.exe child process during update
  - Result:
    - Added `IsProcessRunning`, `ProcessId`, and `WaitForProcessExitAsync` to `TsnetManager.vb` to reliably observe and ensure child process termination.
    - Updated `ForceExitForUpdateAsync` in `MainWindow.xaml.vb` to explicitly await clean shutdown and termination of `tsnetd.exe` (`TsnetManager.Instance.ShutdownAsync(overallTimeoutMs:=3000)` and `WaitForProcessExitAsync(timeoutMs:=2000)`) before releasing single-instance mutex and handing off to updater.
    - Added exit verification in `UpdateChecker.vb` before spawning the updater process.
    - Hardened `Application.xaml.vb` (`HandleApplyUpdateStartup`): waits specifically for the caller PID (`Process.GetProcessById(callerPid)`). If the caller does not exit within timeout, terminates specifically that caller PID and its child process tree (`caller.Kill(entireProcessTree:=True)`), completely avoiding indiscriminate termination by executable image name (`taskkill /f /im HidaChat.exe /t`).
    - Added safety check in `Application.xaml.vb` ensuring any lingering `tsnetd.exe` associated with the installation directory is exited before file copy.
    - Hardened fallback batch script in `UpdateScriptPolicy.vb` (`GenerateUpdateBatchScript`): terminates specifically by caller PID (`taskkill /f /pid %CALLER_PID% /t`), preserving all unrelated portable instances running from other directories.
  - Verification: Added 13 automated regression assertions in `Tests/SessionTests.vb` verifying PID-specific termination syntax in batch generation, absence of image-name termination, `IsProcessRunning`, `ProcessId`, and `WaitForProcessExitAsync` behavior (totaling **919 automated regression assertions with 0 errors**). Full project compilation succeeded with 0 warnings and 0 errors.

## 2026-11-25

- [x] **TASK-0127** [High] Multi-PC profile sync protection against stale staging export and master session poisoning
  - Result:
    - Diagnosed cross-machine session clobbering: an instance on `PC-STUDIO` (user `hidab`) with a failed or locked profile import fell back to stale local staging, and upon exiting at 17:28 exported its empty/unauthenticated profile over the network Master (`Y:\`), causing `PC-MIMMO` to import the broken profiles and prompt for WhatsApp QR code and Hermes login.
    - Restored active, authenticated profiles for all accounts (`1784925628238` WhatsApp, `b63821a1add040ad9ea8f9fd96ba88bb` Hermes, `1788428413731` OpenClaw, `f7b6555d88a84b56bce69f0ba1610c33` Discord) from preserved `.previous` snapshots on `PC-MIMMO` and published them authoritatively to Master on `Y:\`.
    - Implemented `MarkStagingStale`, `ClearStagingStale`, and `IsStagingStale` in `NetworkProfileSync.vb` with in-memory tracking and persistent `.sync_stale` filesystem markers.
    - Updated `SyncMasterToLocalStagingAsync`: marks local staging as stale when master refresh fails or is skipped due to locked files, preventing the instance from exporting un-synced data.
    - Hardened `SyncLocalStagingToMasterAsync` with two export guards:
      1. Rejects export (`[EXPORT_REFUSED_STALE_STAGING]`) if local staging is marked stale or unrefreshed from master.
      2. Rejects export (`[EXPORT_REFUSED_MASTER_NEWER]`) if the network Master was exported by another machine/user at a timestamp newer than the local staging baseline.
  - Verification: Added 7 automated regression assertions in `Tests/SessionTests.vb` verifying stale staging tracking, marker file persistence, cross-process detection, and cleanup (totaling **906 automated regression assertions with 0 errors**). Verified successful live relaunch of `HidaChat` on `Y:\` with WhatsApp, Hermes, Telegram, OpenClaw, and Discord navigating cleanly to their authenticated URLs without prompts.

- [x] **TASK-0126** [High] Fix infinite login loop on Hermes Agent caused by expired Bearer token injection
  - Result:
    - Implemented `TryGetTokenExpiration` in `LocalAuthPolicy.vb` supporting extraction of Unix epoch `exp` timestamps from standard 3-part JWTs and binary-signed base64 tokens (Hermes token format).
    - Implemented `IsTokenExpired` in `LocalAuthPolicy.vb` comparing token `exp` timestamp against current UTC time.
    - Implemented `IsAuthenticationEndpoint` and `ShouldAttachBearerToken` in `LocalAuthPolicy.vb` to block pre-emptive Bearer header injection when a token is expired or when requests target authentication endpoints (`/login`, `/signin`, `/auth/password-login`, `/auth/login`, `/auth/token`, `/auth/refresh`).
    - Updated `AppAccounts.vb` to suppress `_activeRequiresAuthToken` in `UpdateWebResourceAuthFilter` if the configured `AuthToken` has expired, and guarded header injection in `_webResourceRequestedHandler` via `LocalAuthPolicy.ShouldAttachBearerToken`.
    - Added auto-healing logic in `AccountManager.LoadAccountsAsync` to detect and safely clear expired authentication tokens on startup, preventing repetitive login redirects.
    - Cleared the expired session token on the live Hermes account ("Ermanno") in `Y:\HidaChat\data\settings.json`, deployed updated Release binaries to `Y:\HidaChat\`, and verified clean relaunch.
  - Verification: Added 28 automated regression unit assertions in `Tests/SessionTests.vb` testing token expiration parsing, past/future token status, authentication endpoint identification, and selective header attachment (totaling **899 automated regression assertions with 0 errors**). Full project compilation succeeded with 0 warnings and 0 errors.

- [x] **TASK-0082** [Medium] HTTPS certificate verification is disabled
  - Result:
    - Eliminated silent unconditional `InsecureSkipVerify = true` bypass in the embedded tsnet companion reverse proxy, restoring standard TLS certificate and hostname validation as default behavior.
    - Added support for explicit custom Root CA certificates (`cmd.CaCert` / `CustomCaCertificatePath`) and SHA-256 leaf certificate pinning (`cmd.PinnedCertHash` / `PinnedCertificateHash`) to secure connections against MITM attacks.
    - Added explicit opt-in setting `AllowInsecureTls` (disabled by default) for testing against self-signed or private development servers without silent global bypass.
    - Created `TlsCertificatePolicy.vb` implementing standard chain evaluation, error discrimination, and thumbprint calculation.
    - Removed unconditional certificate bypass callback in `BtnTestConnection_Click` and `TsnetManager.vb`.
    - Differentiated SSL/TLS certificate errors (expired certificates, untrusted roots, hostname mismatches, pinning failures) from generic reachability issues, surfacing a clear localized error status (`connection_tls_error` / `tailscale_error_tls_failure`) and technical tooltip details instead of ambiguous reachability alerts.
    - Configured tsnet companion proxy to emit `X-HidaChat-Error-Type: TLS_CERTIFICATE_ERROR` and a dedicated non-looping TLS error diagnostic page when upstream certificate verification fails.
    - Synchronized localized UI dictionary keys across all 5 officially supported languages (English, Italian, French, Spanish, German).
  - Verification: Added 29 automated regression assertions in `SessionTests.vb` verifying thumbprint normalization, self-signed certificate generation, standard chain errors, hostname mismatches, certificate pinning matching/mismatch, and TLS vs general reachability discrimination (totaling **871 automated regression assertions with 0 errors**). Clean Release build with 0 warnings and 0 errors.

## 2026-11-24

- [x] **TASK-0092** [High] Fragile update batch script (OEM/UTF-8 codepage, accented characters, special characters, parentheses, and root USB drive)
  - Result:
    - Created `UpdateScriptPolicy.vb` providing robust path normalization (`NormalizePathForRobocopy` and `NormalizePathForArgument`) that normalizes root USB drives (e.g. `E:\`, `E:`) and paths with trailing backslashes to `\.`, resolving the Windows trailing backslash escape issue (`\"`) in command line argument parsing and robocopy.
    - Implemented `GenerateUpdateBatchScript` containing `chcp 65001 >nul` for complete UTF-8 codepage support, ensuring directories or usernames with accented characters (e.g. Italian vowels `è`, `à`, `ò`) are handled without path corruption.
    - Protected variable assignments and batch blocks using `set "VAR=VAL"` syntax and safe logging without unquoted paths in `echo` inside `if (...)` blocks, preventing premature block termination caused by parentheses in directory names (e.g. `HidaChat (1)` or `Program Files (x86)`).
    - Enforced an explicit `pause` command on robocopy failure (`%RC% GEQ 8`) before `exit /b 1`, preventing the console window from closing prematurely and allowing inspection of the log.
    - Added automatic fallback to the resilient batch updater in `UpdateChecker.vb` if compiled updater process startup fails.
    - Updated `Application.xaml.vb` to canonicalize source, target, and temporary directories and safely format relaunch and cleanup arguments.
  - Verification: Added 28 automated regression assertions in `SessionTests.vb` validating path normalization across USB roots and accented/parenthesized directories, closing parenthesis escaping in echo statements, UTF-8 non-BOM script generation, and robocopy pause handling (totaling **842 automated regression assertions with 0 errors**). Clean compilation with 0 warnings and 0 errors.

- [x] **TASK-0117** [High] Dedicated download client, timeout management, and streaming directly to file
  - Result:
    - Created `UpdateDownloadPolicy.vb` providing `CreateDedicatedDownloadClient` with `Timeout = Timeout.InfiniteTimeSpan` and application ProductInfo User-Agent header.
    - Implemented chunked streaming (`DownloadStreamToFileAsync` / `DownloadFileStreamingAsync`) with 80 KB buffers writing directly to destination file on disk, eliminating in-memory buffering and RAM pressure.
    - Enforced an overall 10-minute operation timeout via `CancellationTokenSource` in `UpdateChecker.vb`, preventing timeouts on slow networks while protecting against infinite hangs.
    - Added determinate and indeterminate live progress reporting via `IProgress(Of DownloadProgressReport)`, reporting bytes read, total bytes, percentage, and formatted MB.
    - Wired streaming progress directly to `UpdateProgressDialog` UI dispatcher during GitHub update asset downloads.
  - Verification: Added 18 automated regression tests in `SessionTests.vb` verifying client timeout, chunk buffer size, streaming data integrity bit-for-bit, progress events, and cancellation handling (totaling **794 automated regression assertions with 0 errors**). Clean compilation with 0 warnings and 0 errors.

- [x] **TASK-0125** [Medium] Prevent accidental platform switching via mouse wheel in Settings and restore Hermes account
  - Result:
    - Added `ComboAccountPlatform_PreviewMouseWheel` in `SettingsWindow.xaml` and `SettingsWindow.xaml.vb` to block mouse wheel scrolling from changing the selected platform when the dropdown is closed, forwarding the scroll event to the parent container.
    - Repaired `Y:\HidaChat\data\settings.json` and `.bak`, restoring account `account_1789733553499` ("Ermanno") to platform `"Hermes"` with proxy port `18900`.
    - Removed redundant test account created during manual verification.
  - Verification: Clean compilation with 0 warnings and 0 errors. Verified 776 automated regression tests passing with 0 errors.

## 2026-11-23

- [x] **TASK-0124** [High] Proxy port cross-wiring and race condition between OpenClaw and Hermes in TsnetManager
  - Result:
    - Enforced strict platform-specific proxy port ranges (`[18800, 18899]` for OpenClaw, `[18900, 18999]` for Hermes) via `AccountLoadSafetyPolicy.IsValidProxyPortForPlatform`.
    - Added deterministic port allocation with collision resolution (`ResolveDeterministicPort`), preventing multiple gateway accounts from overlapping.
    - Implemented two-pass auto-healing in `AccountManager.LoadAccountsAsync` to detect out-of-range or swapped proxy ports (e.g. OpenClaw with Hermes port or vice-versa), safely resetting and reassigning them within each platform's valid range.
    - Reset and reassigned proxy port and proxy URL immediately when changing platform in `AppAccounts.SetPlatform`.
    - Overhauled IPC response parsing in `TsnetManager.EnsureRouteAsync` and `CheckStatusAsync` to correlate incoming JSON responses strictly with the originating request (`accountId` for routes, `nodeState` for status).
    - Interleaved responses from other commands/accounts are captured and updated without misattributing them to the active caller, preventing IPC desynchronization.
    - Repaired live deployment configuration on `Y:\HidaChat\data\settings.json` and `.bak`, restoring OpenClaw to 18800 and Hermes to 18900.
  - Verification: Added 29 automated regression assertions in `SessionTests.vb` testing port range validation, string overloads, collision avoidance, swapped port auto-healing, and non-proxy platform exclusion (totaling **776 automated regression assertions with 0 errors**). Clean compilation with 0 warnings and 0 errors.

- [x] **TASK-0091** [High] Version marker written before robocopy, unique temp directory naming, and temporary leftovers cleanup
  - Result:
    - Removed premature .app_version writing prior to file copying; writing of .app_version is now strictly delegated to HandleApplyUpdateStartup only upon successful file copy completion.
    - Standardized .app_version format to "v" & version.TrimStart("v"c) across the entire lifecycle, ensuring complete consistency.
    - Hardened temporary update directory generation using timestamp and GUID (HidaChat_Update_{sessionId}) to avoid conflicts between concurrent portable instances and multi-user environments.
    - Updated background cleanup to strictly skip the current session and preserve recent active sessions (< 2 hours old), avoiding race conditions and cross-instance file deletion.
    - Passed root temporary directory (	empDir) through --apply-update and --cleanup-update command line arguments, guaranteeing the entire extracted folder tree is removed after update relaunch.
    - Added retry logic with backoff to --cleanup-update to ensure temporary zip files and folders are cleanly deleted even under brief process lock delays.
    - Added temporary folder and zip cleanup on failure in HandleApplyUpdateStartup, preventing disk bloat when updates fail.
    - Excluded runner scripts, .bat, .cmd, .ps1, and .tmp files from being copied to the application directory in CopyUpdateFiles.
  - Verification: Added 28 automated regression assertions in SessionTests.vb testing runner script exclusion, version marker formatting, CLI argument construction with root temp directory, and safe cleanup filtering (totaling **747 automated regression assertions with 0 errors**). Clean compilation with 0 warnings and 0 errors.

## 2026-11-22

- [x] **TASK-0081** [High] A hang in tsnetd can indefinitely block shutdown
  - Result:
    - In `TsnetManager.vb`, enhanced `SendCommandAsync` to accept a cancellable timeout (default 3000ms) covering both semaphore acquisition (`_ioLock.WaitAsync`) and stream I/O (`WriteLineAsync`, `ReadLineAsync`), completely preventing indefinite hangs on unresponsive daemons.
    - If a command times out or is cancelled, `_ioLock` is safely released in `Finally`, preventing lock starvation and allowing subsequent commands or shutdown to proceed.
    - Implemented process lifecycle cancellation token source (`_processCts`) that is signalled immediately during `ShutdownAsync()`, cancelling hanging background tasks (e.g. status polling timer, route management) and promptly freeing the command lock.
    - Redesigned `ShutdownAsync(Optional overallTimeoutMs As Integer = 2000)` with a strict overall deadline independent of the command channel:
      - Allocates a small fraction (up to 600ms) to attempt cooperative shutdown via `"shutdown"` command without letting a hung channel stall the process.
      - Closes `StandardInput` (sending EOF to Go daemon scanner loop) to unblock stdin reads.
      - Awaits process exit up to the remaining overall deadline budget.
      - If the daemon remains unresponsive past the overall deadline, forcefully terminates it with `proc.Kill(entireProcessTree:=True)`.
      - Disposes streams, cancels error reading, and cleans up all state without leaving dangling background reads.
    - Added test seam properties `CustomExecutablePath` and `CustomExecutableArguments` in `TsnetManager.vb` for automated mock daemon testing.
  - Verification: Added 11 new automated regression assertions in `SessionTests.vb` testing simulated unresponsive daemon (PowerShell sleeping without writing to stdout): verified `SendCommandAsync` times out within budget and returns `Nothing`, verified subsequent commands proceed without semaphore deadlock, verified `ShutdownAsync` terminates within overall limit and kills the unresponsive daemon, and verified `ShutdownAsync` terminates within deadline even when a prior command is actively hanging (totaling **719 automated regression assertions with 0 errors**). Clean compilation with 0 warnings and 0 errors.

## 2026-11-21

- [x] **TASK-0080** [High] Stop and restart can overlap two bulk sender executions
  - Result:
    - Encapsulated bulk sending concurrency coordination and outcome determination in `BulkSenderExecutionCoordinator` and integrated it with `BulkSenderEngine.vb`.
    - Engine now prevents concurrent executions independently of the UI: `RunAsync` verifies `TryBeginExecution()` under lock and rejects concurrent start attempts with an `InvalidOperationException` while running or stopping.
    - Implemented `CancelAsync()` method that requests cancellation (`BeginStop()`), signals the pause event, cancels the cancellation token source, and awaits completion of the active task with a safety timeout.
    - Added UI stopping state `SetUiStoppingState()` in `BulkSenderWindow.xaml.vb` that disables Start, Pause, and Stop buttons, displaying localized status "Stopping in progress...".
    - Separated final execution outcomes into distinct states: `Completed`, `Cancelled`, and `Failed`. Interrupted runs no longer pass through the "Completed" summary and do not falsely set progress bar to 100%.
    - Cancellation during an active message transmission or JavaScript call marks the contact as `Interrupted` (`bulk_status_interrupted`) instead of `Error`, does not falsely increment `ErrorCount`, and reports exact sent, error, and unsend counts.
    - Added safe window closing lifecycle handler (`BulkSenderWindow_Closing`) confirming cancellation before closing when bulk sending is active.
    - Synchronously updated all 5 UI localization dictionaries in `Localization.vb` (`EnStrings`, `ItStrings`, `FrStrings`, `EsStrings`, `DeStrings`) with `bulk_status_interrupted` and cleaned character encodings.
  - Verification: Added 13 new regression assertions in `SessionTests.vb` testing stopping state transitions, strict rejection of concurrent execution attempts during pending async JavaScript operations, outcome classification as Cancelled, preservation of sent count, zero false errors, and accurate unsend counts (totaling **708 automated regression assertions with 0 errors**). Clean build with 0 warnings and 0 errors.

## 2026-11-18

- [x] **TASK-0090** [High] MigrateOrphanProfile deletes or overwrites a valid profile
  - Result:
    - Encapsulated orphan profile migration safety evaluation in `AccountLoadSafetyPolicy.CanSafelyMigrateOrphanProfile(sharedDir, accountId)`.
    - Preliminarily checks for the existence of legacy target folder `WV2Profile_{id}`, native profile folder `EBWebView/wv2profile_{id}`, alternative native naming patterns, and all snapshot/backup variants (`.previous`, `.bak`, `.migrated`, `_Snapshot`).
    - If the target folder or any snapshot already exists on disk, `MigrateOrphanProfile` immediately and strictly aborts migration (`Return`), preserving the existing valid profile and active user session 100% untouched.
    - Preserved the orphan directory `WV2Profile_` safely as a backup, avoiding destructive overwrite and excluding it from deletion.
    - In `AccountManager.CreateDefaultAccountAsync`, added existence check before moving anonymous orphan to prevent potential collisions.
  - Verification: Added 21 new regression assertions in `SessionTests.vb` testing null/empty rejection, missing orphan rejection, existing valid target rejection, and all snapshot/native variants. Performed the exact acceptance test: coexisting `WV2Profile_` and `WV2Profile_{id}` with differing files was rejected, preserving `WV2Profile_{id}` completely untouched (totaling **695 automated regression assertions with 0 errors**). Clean build with 0 warnings and 0 errors.

## 2026-11-16

- [x] **TASK-0089** [High] Potential loss of all sessions on deserialization error (AccountManager.LoadAccountsAsync)
  - Result:
    - Encapsulated non-destructive error handling in `AccountLoadSafetyPolicy.vb` and integrated it with `AccountManager.vb`.
    - In `AccountManager.LoadAccountsAsync` Catch block: immediately creates a safety backup copy `settings.json.bak` before any other fallback operation.
    - Added safety flag `_loadFailed = True` and public property `LoadFailed` in `AccountManager`.
    - Strictly inhibited saving accounts (`SaveAccountsAsync`), profile folder deletions and moves (`CleanupUnusedProfilesAsync`, `MigrateOrphanProfile`, `MigrateLegacyProfilesToNativeProfiles`, `RemoveAccountAsync`, and orphan renaming in `CreateDefaultAccountAsync`) whenever `_loadFailed` is True.
    - Implemented `NotifyAccountsCorruptedAndProtected` modal warning dialog to explicitly inform the user of the configuration issue, that a safety backup was made, and that writing and cleanups were paused to protect all existing login sessions.
    - Synchronously updated all 5 UI localization dictionaries in `Localization.vb` (`EnStrings`, `ItStrings`, `FrStrings`, `EsStrings`, `DeStrings`) with `accounts_corrupt_title` and `accounts_corrupt_msg`.
  - Verification: Added unit regression tests in `SessionTests.vb` covering JSON syntax error rejection, incompatible property types rejection, empty array rejection, non-destructive backup file generation, and verification that profile directories and databases remain completely untouched on disk (totaling **674 automated regression assertions with 0 errors**). Clean build with 0 warnings and 0 errors.

## 2026-11-15

- [x] **TASK-0115** [High] Discord Integration Code Review, Security Hardening, Media Permissions, Voice Call Protection, and Notification Fixes
  - Result:
    - Fixed voice call drop during tab hibernation in `AppAccounts.vb`: `TryHibernateAsync()` now verifies `IsPlayingAudio` and `CoreWebView2.IsDocumentPlayingAudio`, strictly preventing suspension of tabs with active calls, voice channels, or audio playback.
    - Implemented secure media permissions in `_permissionRequestedHandler` (`AppAccounts.vb`): explicitly allows `Microphone` and `Camera` requests strictly for authorized origins (`IsAuthorizedOrigin`), while denying untrusted or cross-origin requests.
    - Expanded allowed window navigation in `PlatformDefinition.vb`: `IsAllowedNewWindowHost` now allows Discord authentication routes (`/login`, `/register`, `/verify`, `/oauth2/`, `/popout/`, `/guild-discovery`, `/store`), preventing in-app login and verification flows from being improperly kicked out to the external browser.
    - Enhanced deep link resolution in `PlatformDefinition.vb`: added support for `login`, `register`, and `oauth2` routes in `discord://` scheme.
    - Hardened JavaScript security: replaced unsafe substring matching `location.hostname.includes('discord')` and `.includes('telegram')` with strict domain verification in `Scripts/notification.js` and `Scripts/translation.js`.
    - Fixed unread badge double-counting in `Scripts/notification.js`: filtered out container elements when an inner badge is matched, preventing parent/child duplicate counts.
    - Filtered out muted channels and servers in `Scripts/notification.js` from unread pills.
    - Cleaned document title extraction in `Scripts/notification.js` to strip trailing ` - Discord` and ` | Discord`.
    - Extended session health monitoring and persistent storage persistence in `Scripts/session-health.js` to cover `https://discord.com`.
  - Verification: 653 automated regression assertions in `SessionTests.vb` passed with 0 errors. Clean build with 0 warnings and 0 errors.

## 2026-11-14

- [x] **TASK-0088** [High] 15s timeout on ZIP download and buffering archive in RAM (UpdateChecker.vb)~~
  - Result: used dedicated _downloadClient configured with Timeout = Timeout.InfiniteTimeSpan and guarded the request with 10-minute CancellationTokenSource. Implemented direct-to-disk progressive chunked streaming download (File.Create(tempZipPath)) using HttpCompletionOption.ResponseHeadersRead and 80 KB buffer (uffer(81919)). Performed SHA-256 verification directly from the disk file stream via SHA256.HashDataAsync(fs) in ComputeSha256Async without loading archive into RAM.
  - Verification: 640 regression assertions in SessionTests.vb passed with 0 errors. Clean build with 0 warnings and 0 errors.


## 2026-11-03

- [x] **TASK-0054** [High] Hermes Agent Dashboard & Web Console Integration via Tailscale (Mesh VPN & API/Token Auth)~~
  - **Description**: Added native support for **Hermes Agent** (Nous Research) as an account platform in HidaChat, enabling users to have a dedicated WebView2 tab to interact with the assistant (integrated chat / TUI), monitor logs, manage skills and automated tasks (cron jobs), and configure Hermes gateway parameters both locally and over private Tailscale mesh networks.
  - **Implemented Features**:
    - Extended account model (`AppAccounts.vb`, `AccountManager.vb`) with `"Hermes"` platform, `IsHermes` property, visual branding (winged helmet themed SVG vector icon and distinctive `#00B0FF` brush), and deterministic isolated local proxy port (`18900+`).
    - Flexible endpoint configuration with default URL `http://127.0.0.1:9119` (standard local dashboard instance) or private Tailscale hosts.
    - Secure injection of authentication token or API Key via HTTP header `Authorization: Bearer <AuthToken>` using WebView2 `WebResourceRequested` filter and URL parameter support.
    - Network routing via integrated Tailscale mesh proxy (`tsnetd`) with authentication header and local token `X-HidaChat-Local-Token`.
    - Updated UI (`SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `MainWindow.xaml.vb`) with Hermes Agent platform selection, contextual configuration form with dedicated labels and tooltips, quick add menu, and real-time HTTP connectivity test.
    - Full multilingual localization (IT, EN, FR, ES, DE) in `Localization.vb`.
  - **Impact**: High | **Effort**: Medium-High

- [x] **TASK-0053** [High] OpenClaw Web GUI Console Integration via Tailscale (Mesh VPN & Gateway Token)~~
  - **Description**: Added native support for **OpenClaw** as an account platform in HidaChat (alongside WhatsApp and Telegram), enabling management of chat sessions, log monitoring, skill execution, and OpenClaw AI gateway configuration within an isolated dedicated WebView2 tab.
  - **Technical Specifications & Architecture**:
  -   1. **Account Model & Configuration (`AppAccounts.vb`, `AccountManager.vb`)**:
       - Extended `Platform` enum/property: added `"OpenClaw"` support with boolean property `IsOpenClaw`.
       - New persistent account properties:
          - `ServerUrl` (String): OpenClaw gateway URL (default: `http://127.0.0.1:18789` or Tailnet domain).
          - `TailscaleIntegration` (Boolean): Flag to enable routing via embedded Tailscale node (`tsnetd.exe`) with loopback reverse proxy and local security token.
       - Dedicated visual palette: themed SVG vector icon (robot/claw/terminal) and distinctive brush (e.g. orange `#FF5722` or purple `#7C4DFF`).
  -   2. **Network Integration & Tailscale Mesh VPN (tsnet Userspace)**:
       - **Companion `tsnetd.exe`**: Standalone Go static binary with gVisor and WireGuard stack (Zero Installation, no system driver or Tailscale client required).
       - **Loopback Reverse Proxy (`127.0.0.1:<randomPort>`)**: Listening on a dynamic random port exclusively for WebView2, protected by `X-HidaChat-Local-Token` header/cookie.
       - **Portable Persistence (`data/tsnet/`)**: Node state travels with HidaChat's data folder, keeping device identity stable on the Tailnet.
       - **Authentication & Login Flow**: Handling of `Running`, `NeedsLogin` states (with browser launch), and support for AuthKey encrypted with Windows DPAPI.
          - Fallback and validation: asynchronous connectivity test (HTTP ping/healthcheck on `/api/health` or root endpoint) before navigation, displaying gateway node online/offline status.
  -   3. **Token Management & Pairing Flow**:
       - Automatic bootstrap URL composition with authentication token (`?token=<AuthToken>`) or secure injection via HTTP header `Authorization: Bearer <AuthToken>` using CoreWebView2 `WebResourceRequested` event.
       - Support for OpenClaw automatic *token stripping* (token is removed from address bar after authentication to avoid visual leaks).
  -   4. **Notifications & Event Monitoring**:
       - Interception of OpenClaw Control UI alerts (agentic task completion, gateway errors, new incoming messages) via dedicated JavaScript bridge (`NotificationJsScripts.vb`) and conversion to native **Windows Toast** notifications and on-tab notification badges.
  -   5. **Configuration UI (`SettingsWindow.xaml`)**:
       - Extended platform selector with OpenClaw option.
       - Dynamic form with specific fields (Gateway URL, Secret Token with show/paste option, "Check Connection" button).
  - **Impact**: High | **Effort**: Medium-High


## 2026-11-01

- [x] **TASK-0108** [High] Extreme shutdown saving optimizations: bounded parallel copy, coalesced async logging, and unchanged account skipping~~
  - **Files**: `ColdProfileCopy.vb`, `AppLogger.vb`, `NetworkProfileSync.vb`, `MainWindow.xaml.vb`, `Tests/SessionTests.vb`
  - **Problem / Need**: Despite improvements in TODO #107, shutdown on Samba network drives still had optimization headroom:
  -   1. Profile files were transferred sequentially with manual streams and separate timestamp operations (`SetLastWriteTimeUtc`).
  -   2. Every single log message to `AppLogger` executed synchronous file checks and `AppendAllText` under a global lock, producing dozens of SMB I/O round-trips during shutdown.
  -   3. Accounts open without new chats or changes were still exported in full.
  -   4. Tailscale daemon `tsnetd` shutdown waited serially before completion.
  - **Implemented Features**:
    - `ColdProfileCopy.vb`: intra-account parallel copy using `Parallel.ForEach` bounded to 4 threads per account (`MaxDegreeOfParallelism = 4`). Adopted `File.Copy(src, dst, overwrite:=True)` to leverage Win32 `CopyFileEx`, native timestamp preservation without separate metadata round-trips, and non-blocking `.trash-*` deletion via `Task.Run`.
    - `AppLogger.vb`: introduced non-blocking in-memory `ConcurrentQueue(Of LogEntry)` and coalesced batch disk writes via timer (250ms) and `FlushNow()`, reducing dozens of Samba network calls to a single write.
    - `NetworkProfileSync.vb`: smart differential check `HasModifications(source, master)` completely skipping sync for unmodified accounts (`[EXPORT_UNCHANGED]`, 0 ms I/O).
    - `MainWindow.xaml.vb`: concurrent start of `TsnetManager.Instance.ShutdownAsync()` during WebView2 shutdown, masking shutdown latency (~300ms).
    - `Tests/SessionTests.vb`: added new unit assertions for `HasModifications` and batched `AppLogger` (totaling 601 tests passing with 0 errors).
  - **Acceptance Test**: Verify ultra-fast shutdown on network drive `Y:` with instant sync for modified profiles, 0 I/O for unmodified accounts, and compact logs without lingering latency.
  - **Priority**: P1 (High severity) | **Impact**: Highest | **Effort**: Low

- [x] **TASK-0107** [High] Network profile synchronization optimization and ultra-fast shutdown saving~~
  - **Files**: `ColdProfileCopy.vb`, `NetworkProfileSync.vb`, `AccountManager.vb`, `Tests/SessionTests.vb`
  - **Problem / Need**: When running HidaChat from a network drive (SMB, UNC path, or mapped network drive like `Y:`), application shutdown took 90-120 seconds. The `ShouldSkip` function looked for hardcoded prefixes `EBWebView/Default/Cache` that did not match native WebView2 profile roots (`EBWebView\wv2profile_account_*`), causing indiscriminate copying of over 2,200 files and 550 MB of cache per account. Additionally, `output.Flush(flushToDisk:=True)` forced synchronous `FlushFileBuffers` round-trips over SMB for each file, `.previous` was deleted recursively in a blocking manner causing `Access Denied` errors, and all accounts synchronized sequentially behind a single global semaphore.
  - **Implemented Features**:
    - Selective `SkipFolderNames` filter in `ColdProfileCopy.vb`: immediate root-level exclusion of `Cache`, `Code Cache`, `GPUCache`, `DawnCache`, `ShaderCache`, `Crashpad`, `CacheStorage`, `ScriptCache`, etc., preserving 100% of `IndexedDB`, `Local Storage`, `Cookies`, `Preferences`, `Local State`, and `Service Worker/Database`. 94% reduction in files and 97% reduction in transferred volume.
    - Removed synchronous `Flush(flushToDisk:=True)` in `CopyTree` allowing network SMB bandwidth saturation.
    - Atomic rotation via transient rename `.trash-<guid>` (5 ms) prior to moving `target` to `.previous` and `incoming` to `target`, eliminating SMB `Access Denied` conflicts.
    - Replaced global lock with per-account semaphores (`ConcurrentDictionary(Of String, SemaphoreSlim)`) in `NetworkProfileSync.vb`.
    - Parallelized multi-account synchronization via `Task.WhenAll` in `AccountManager.SyncAllProfilesToNetworkAsync()`.
    - 26 new unit regression assertions in `Tests/SessionTests.vb` (totaling 589 tests with 0 failures).
  - **Acceptance Test**: On network drive `Y:`, verify application shutdown completes synchronization of all profiles in under 2 seconds total without `Access Denied` log errors, preserving all sessions upon restart.
  - **Priority**: P1 (High severity) | **Impact**: Highest | **Effort**: Low

- [x] **TASK-0101** [Low] About window: blurry icon and truncated description without line wraps~~
  - **Files**: `AboutWindow.xaml`, `AboutWindow.xaml.vb`
  - **Problem / Need**:
  -   1. Application icon in About window header appeared blurry due to loading .ico file directly without scaling and high-fidelity rendering options.
  -   2. Application descriptive text (`TxtDescription`) was clipped horizontally without wrapping because it was placed inside a horizontal StackPanel lacking width constraints (`AvailableSize.Width = Infinity`).
  - **Implementation**:
    - Updated `AboutWindow.xaml` setting high-resolution source `images/icon.png`, equipped with `RenderOptions.BitmapScalingMode="HighQuality"`, `SnapsToDevicePixels="True"`, and `UseLayoutRounding="True"` for sharp visuals.
    - Replaced horizontal StackPanel with 2-column Grid (`Auto` for icon and `*` for text container), providing `TxtDescription` bounded width to apply `TextWrapping="Wrap"` for smooth multiline wrapping across all supported languages (EN, IT, FR, ES, DE) without clipping.
    - Refined `ApplyTheme()` in `AboutWindow.xaml.vb` with dynamic `Foreground` handling for `TxtDescription` (`#aebac1` in Dark Mode and `#54656f` in Light Mode).
  - **Priority**: P3 (Low severity) | **Impact**: Low-Medium | **Effort**: Low

- [x] **TASK-0100** [Low] Unused local variable declaration subDirs in UpdateChecker.vb~~
  - **File**: `UpdateChecker.vb`
  - **Problem / Need**: `UpdateChecker.vb` contained unused line `Dim subDirs = Directory.GetDirectories(tempDir)`, generating compiler warnings and performing unnecessary filesystem I/O during extraction.
  - **Implementation**: Completely removed orphan `subDirs` declaration from update extraction flow, adopting targeted executable scan via `Directory.GetFiles(tempDir, "HidaChat.exe", SearchOption.AllDirectories)`. Verified build with zero warnings (0 warnings) and zero errors.
  - **Priority**: P3 (Low severity) | **Impact**: Low | **Effort**: Minimal

- [x] **TASK-0073** [High] Network Drive Support via Local Staging & Session Persistence (Network Drive Session Persistence)~~
  - **Files**: `NetworkProfileSync.vb`, `AppAccounts.vb`, `AccountManager.vb`, `MainWindow.xaml.vb`, `SettingsController.vb`, `SettingsWindow.xaml`
  - **Problem**: When running the portable app directly from a network folder or drive (SMB/CIFS share, UNC path `\\server\share`, or mapped drive `Z:\`), WhatsApp Web sessions were frequently lost or reset to QR code upon restart. This is architectural in Chromium/WebView2: LevelDB and SQLite engines (used by WhatsApp Web via IndexedDB for crypto keys, session tokens, and messages) require local POSIX/NTFS lock semantics. On network SMB filesystems, lock delays or conflicts (`STATUS_FILE_LOCK_CONFLICT`) cause Chromium to flag databases as corrupted or inaccessible on restart, triggering automatic deletion/reset.
  - **Implementation**:
  -   1. *Network Drive Detection*: `NetworkProfileSync.IsRunningOnNetwork` detects UNC paths and SMB mapped network drives (`DriveType.Network`).
  -   2. *Transparent Local Staging*: `NetworkProfileSync.SyncMasterToLocalStagingAsync` synchronizes only essential data (`IndexedDB`, `Local Storage`, `Network/Cookies`, preferences) at startup into local folder `%LOCALAPPDATA%\HidaChat\NetworkProfiles\{AccountId}`, excluding volatile cache directories (`Cache`, `Code Cache`, `GPUCache`, `Crashpad`) for ultra-fast transfers.
  -   3. *Local WebView2 Execution*: `CoreWebView2Environment` is initialized against the local folder, ensuring native speed and local locks without SMB conflicts.
  -   4. *Atomic Return Synchronization*: On shutdown in `PrepareForShutdown` or before updates in `ForceExitForUpdateAsync`, `SyncAllProfilesToNetworkAsync` performs incremental mirroring of updated profiles back to `data/webview/{AccountId}` on the network share.
  -   5. *Multi-PC Lock*: `AcquireSessionLock` detects if the account is already open on another computer, warning the user.
  -   6. *Option in Settings*: Dedicated checkbox and synchronized strings across all 5 supported languages (EN, IT, FR, ES, DE).
  - **Impact**: High | **Effort**: Medium-High


## 2026-10-31

- [x] **TASK-0105** [Medium] Automated startup junk folder cleanup routine~~
  - **Files**: `StartupJunkCleanupPolicy.vb`, `Application.xaml.vb`, `AccountManager.vb`, `Tests/SessionTests.vb`
  - **Description**: Implemented safe, automatic startup removal of temporary diagnostic directories, GPU shader caches, and orphan files (.dmp, .pma, .old) prior to WebView2 process initialization, preserving cookies, IndexedDB (.leveldb / .blob), LocalStorage, and master credentials in Local State.
  - **Implemented Features**:
    - Dedicated module `StartupJunkCleanupPolicy.vb` with strict whitelist of junk directories (`JunkFolderNames`: Crashpad, GPUCache, ShaderCache, GrShaderCache, DawnCache, DawnGraphiteCache, DawnWebGPUCache, GPUPersistentCache, Code Cache, Cache, BrowserMetrics, component_crx_cache, OptimizationGuidePredictionModels, Safe Browsing, Subresource Filter) and junk extensions (`.dmp`, `.pma`, `.old`).
    - Uncompromised protection of session structures (`ProtectedFolderNames`: IndexedDB, Local Storage, Session Storage, Sync Data, Network, Default, EBWebView, and `.leveldb`/`.blob` directories; `ProtectedFileNames`: Local State, Preferences, Cookies, settings.json, .lock, .app_version).
    - Queue-driven safe tree traversal algorithm halting exploration upon encountering junk directories and excluding access to protected session folders, preventing filesystem concurrency issues and accidental login data deletion.
    - Automatic non-blocking invocation in `Application.xaml.vb` inside `OnStartup` immediately after folder lock acquisition and before handoff to UI/WebView2 controls.
    - Automatic invocation in `AccountManager.LoadAccountsAsync()` and `CleanupTransientCachesAsync()` on the shared `data/webview` directory.
    - Extended automated test suite in `Tests/SessionTests.vb`: added 106 new assertions covering junk directory identification, session directory immunity (.leveldb/.blob/Local Storage), extension detection, and simulated directory tree cleanup (totaling **563 automated assertions with 0 errors**).
  - **Files**: `Application.xaml.vb`, `AccountManager.vb`
  - **Problem / Need**: At application startup, before `msedgewebview2.exe` processes instantiate and lock files on disk, temporary files and subfolders created by crashes, diagnostics, or shaders remain, consuming unnecessary space in `data/webview`.
  - **Proposed Solution / Fix to Implement**:
  -   Execute safe removal of temporary directories that never store login or session information prior to WebView2 control initialization:
  -   ```vb
  -   Imports System.IO
  -   Public Sub CleanWebViewJunkFolders(baseDataPath As String)
  -       If Not Directory.Exists(baseDataPath) Then Return
  -       ' 100% safe directories to delete (never contain login data)
  -       Dim junkFolderNames As String() = {
  -           "Crashpad",
  -           "GrShaderCache",
  -           "ShaderCache",
  -           "GraphiteDawnCache",
  -           "DawnCache",
  -           "GPUCache",
  -           "Code Cache",
  -           "Cache",
  -           "BrowserMetrics",
  -           "component_crx_cache",
  -           "OptimizationGuidePredictionModels",
  -           "Safe Browsing"
  -       }
  -       Try
  -           For Each dirPath In Directory.EnumerateDirectories(baseDataPath, "*", SearchOption.AllDirectories)
  -               Dim dirName As String = Path.GetFileName(dirPath)
  -               If junkFolderNames.Contains(dirName, StringComparer.OrdinalIgnoreCase) Then
  -                   Try
  -                       Directory.Delete(dirPath, recursive:=True)
  -                   Catch
  -                       ' Ignore if a file is locked or already removed
  -                   End Try
  -               End If
  -           Next
  -           ' Also delete any .dmp or .pma files (metrics and crash dumps)
  -           For Each filePath In Directory.EnumerateFiles(baseDataPath, "*.*", SearchOption.AllDirectories)
  -               Dim ext As String = Path.GetExtension(filePath).ToLowerInvariant()
  -               If ext = ".dmp" OrElse ext = ".pma" OrElse ext = ".old" Then
  -                   Try
  -                       File.Delete(filePath)
  -                   Catch
  -                   End Try
  -               End If
  -           Next
  -       Catch ex As Exception
  -           ' Optional log
  -       End Try
  -   End Sub
  -   ```
  -   *Caution with IndexedDB directories*: WhatsApp stores downloaded media files in `IndexedDB\https_web.whatsapp.com_0.indexeddb.blob`, while Signal cryptographic and session tokens reside in the sibling `.leveldb`. The `.leveldb` directory must never be removed to prevent user session loss.
  - **Acceptance Test**: Simulate presence of `.dmp`, `.pma` files and `Crashpad` or `GPUCache` folders in data directory; verify on startup that they are removed without touching `.leveldb` or cookies, and that startup proceeds normally.
  - **Priority**: P2 (Medium severity) | **Impact**: Medium | **Effort**: Low


## 2026-10-30

- [x] **TASK-0104** [Medium] Live surgical cache cleanup with ClearBrowsingDataAsync without deauthenticating accounts~~
  - **Files**: `CacheCompactionPolicy.vb`, `AppAccounts.vb`, `AccountManager.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`, `Tests/SessionTests.vb`
  - **Description**: Implemented live surgical cleanup of network cache and Service Workers at runtime via `ClearBrowsingDataAsync` targeted at regenerable data, preserving 100% of cookies, LocalStorage, IndexedDB, and active login sessions across WhatsApp Web, Telegram Web, OpenClaw, and Hermes.
  - **Implemented Features**:
    - Dedicated module `CacheCompactionPolicy.vb` with deterministic masks `SafeRegenerableDataKinds` (`DiskCache Or CacheStorage`) and `DangerousSessionKindsMask` (Cookies, IndexedDB, LocalStorage, passwords, credentials, settings).
    - Safety validation functions `IsSafeCompactionKind` and `IsSafeRegenerableDataKinds` strictly preventing accidental deletion of login tokens.
    - Methods `CompactWebViewProfileAsync(webView)` and `CompactProfileAsync(profile)` executing non-blocking live compaction on active profile.
    - Method `CompactAllWebViewsProfileAsync()` in `AccountManager.vb` iterating and performing live compaction across all profiles with an active WebView2 instance.
    - Updated `ClearBrowsingCacheAsync` in `AppAccounts.vb` to include `CacheStorage` alongside `DiskCache`.
    - Dedicated "Optimize Storage" button (`BtnOptimizeStorage`) and descriptive hint in "Cache & Storage" section of Settings (`SettingsWindow.xaml` and `SettingsWindow.xaml.vb`), with visual completion feedback.
    - Full synchronization across all 5 official multilingual dictionaries (`Localization.vb`: English, Italian, French, Spanish, German) with 4 new keys (`optimize_storage_btn`, `optimize_storage_hint`, `optimize_storage_success_title`, `optimize_storage_success_msg`).
    - Automated unit test suite in `Tests/SessionTests.vb`: added 46 new regression assertions validating safety masks, rejection of sensitive flags, regenerable cache directory computation, and size formatting (totaling **457 automated assertions with 0 errors**).
  - **Files**: `SettingsWindow.xaml.vb`, `AccountManager.vb`
  - **Problem / Need**: Over extended use, WebView2 accumulates hundreds of megabytes of `DiskCache` and `CacheStorage` (Service Worker cache). Traditional folder deletion approaches risk wiping Cookies, LocalStorage, and IndexedDB, triggering forced logouts from WhatsApp Web and Telegram Web.
  - **Proposed Solution / Fix to Implement**:
  -   Provide a live compaction function callable manually (e.g. "Optimize Storage" button in Settings) or on a scheduled periodic basis, invoking `CoreWebView2.Profile.ClearBrowsingDataAsync` exclusively on regenerable data:
  -   ```vb
  -   Imports Microsoft.Web.WebView2.Core
  -   Public Async Function CompactWebViewProfileAsync(webView As Microsoft.Web.WebView2.Wpf.WebView2) As Task
  -       If webView?.CoreWebView2?.Profile Is Nothing Then Return
  -       ' Select ONLY regenerable caches, excluding Cookies, LocalStorage and IndexedDB!
  -       Dim dataKinds As CoreWebView2BrowsingDataKinds =
  -           CoreWebView2BrowsingDataKinds.DiskCache Or
  -           CoreWebView2BrowsingDataKinds.CacheStorage
  -       Await webView.CoreWebView2.Profile.ClearBrowsingDataAsync(dataKinds)
  -   End Function
  -   ```
  - **Acceptance Test**: Trigger live cleanup on an active WhatsApp Web session with open chats; verify disk cache size drops substantially and session is not disconnected (no QR code prompted on reload/refresh).
  - **Priority**: P2 (Medium severity) | **Impact**: Medium-High | **Effort**: Low


## 2026-10-29

- [x] **TASK-0103** [Medium] Sharing single CoreWebView2Environment with native profiles (ProfileName)~~
  - **Files**: `ProfileIsolationPolicy.vb`, `AppAccounts.vb`, `AccountManager.vb`, `MainWindow.xaml.vb`
  - **Problem / Need**: Creating separate isolated folder paths for each account (`data/webview/account_1`, `data/webview/account_2`, etc.) with a different `CoreWebView2Environment` per tab multiplies Chromium system files, logs, and internal binaries (`EBWebView`) for every account.
  - **Proposed Solution / Fix to Implement**:
  -   Use a single shared base data folder (`data/webview`) with a single `CoreWebView2Environment`, separating accounts via native WebView2 profiles using `CoreWebView2ControllerOptions.ProfileName`:
  -   ```vb
  -   ' Use same _sharedEnvironment ("data/webview") for all tabs:
  -   Dim controllerOptions = _sharedEnvironment.CreateCoreWebView2ControllerOptions()
  -   controllerOptions.ProfileName = $"Account_{account.Id}" ' E.g. "Account_1", "Account_2"
  -   controllerOptions.IsInPrivateModeEnabled = False
  -   Await webView.EnsureCoreWebView2Async(_sharedEnvironment, controllerOptions)
  -   ```
  -   In this way, Cookies, IndexedDB, and LocalStorage remain 100% isolated (in profile subdirectories), while engine infrastructure is allocated once, yielding substantial RAM and disk savings.
  - **Acceptance Test**: Open multiple accounts concurrently (e.g. WhatsApp 1 and WhatsApp 2) verifying sessions do not conflict, cookies/IndexedDB stay separated, and `EBWebView` directory does not duplicate system files.
  - **Priority**: P2 (Medium severity) | **Impact**: High | **Effort**: Medium


## 2026-10-27

- [x] **TASK-0079** [High] Bulk sender can transmit a draft and report false success~~
  - **Files**: `BulkSenderPolicy.vb`, `BulkSenderEngine.vb`, `Localization.vb`, `Tests/SessionTests.vb`
  - **Problem / Need**: On Telegram, the scheduled message was only inserted if the input field was empty: a pre-existing draft could be sent instead. Furthermore, on both platforms, status switched to SENT after a click or keyboard event without verifying that the client actually accepted the message. Explicit chat identity verification prior to sending was also absent. Simulated DOM tests confirmed both draft dispatch and SENT reporting without actual confirmation.
  - **Completed Implementation**:
    - Created deterministic module `BulkSenderPolicy.vb` with validation routines `IsDraftConflict`, `MatchesRecipient`, `IsSendConfirmed`, `BuildWhatsAppScript`, `BuildTelegramScript`, and `ParseScriptResult`.
    - **Pre-existing Draft Detection & Blocking**: On both Telegram and WhatsApp, input field text is verified before dispatch; if a draft conflicts with expected text, dispatch is immediately halted with error status (`DRAFT_CONFLICT`) without overwriting or sending the draft.
    - **Observable Client Confirmation**: Eliminated immediate success reporting after click/keypress; result transitions to intermediate state `TRIGGERED` and waits for observable client confirmation (`CONFIRMED_SENT`: input cleared and/or send button toggles to mic icon) with controlled timeout and explicit reporting on rejected/ignored dispatches (`SEND_REJECTED`).
    - **Full Multilingual Localization**: Added 8 new keys for bulk sender error and state messages synchronized across all 5 official dictionaries in `Localization.vb` (English, Italian, French, Spanish, German).
    - **Automated Test Suite**: Added 76 unit regression assertions in `Tests/SessionTests.vb` covering text normalization, draft detection, recipient matching, observable confirmation, and JSON parsing (totaling 333 automated assertions with 0 errors).
  - **Acceptance Test**: Existing draft, slow navigation, previous chat still visible, and ignored Enter key press must not produce a false "Sent".
  - **Priority**: P1 | **Impact**: High | **Effort**: Medium


## 2026-10-26

- [x] **TASK-0087** [High] Dedicated GUI update window with progress status (elimination of .ps1 / .bat scripts)~~
  - **Files**: `UpdateChecker.vb`, `UpdateProgressWindow.xaml` / `UpdateProgressWindow.xaml.vb` (or compact standalone helper executable `HidaChat.Updater.exe`), `Localization.vb`
  - **Problem / Need**:
    - The OTA update flow previously relied on temporary scripts generated on the fly (`.bat` or previously hypothesized `.ps1`) executed via Windows shell processes.
    - Relying on external scripts presented significant issues:
  -     1. Lack of modern visual feedback for the user, who risks seeing black command prompt/console windows or no clear indicator while the main app closes for file replacement;
  -     2. Vulnerability to antivirus heuristic blocks or restrictive OS execution policies (e.g. restrictions on PowerShell `.ps1` scripts or unsigned batch files);
  -     3. Risk that the user assumes the application crashed or hung and attempts to restart it before file copying and overwriting complete.
  - **Completed Fix**:
  -   1. *Definitive elimination of external scripts (`.ps1` and `.bat`)*:
       - Completely removed generation and execution of `.ps1` and `.bat` scripts.
       - Managed the entire update lifecycle via native compiled code (.NET / WPF) or a small dedicated compiled helper process completely standalone and free from external shell interpreter dependencies.
  -   2. *Graphical window with progress indicator (Progress Dialog)*:
       - Created a dedicated graphical window with a modern dark theme consistent with HidaChat (aligned with `ShutdownProgressDialog` and `StorageRecoveryProgressDialog`).
       - Progress bar (determinate ProgressBar showing percentage and MBs during download; smooth indeterminate ProgressBar during hash verification, extraction, and file copying).
       - Clear, real-time display of update stages:
         - *Downloading update package from GitHub*;
         - *Verifying cryptographic SHA-256 integrity and Authenticode signature*;
         - *Orderly shutdown and waiting for resource release of HidaChat and WebView2 processes*;
         - *Extracting and safely overwriting application files* (strictly preserving the 100% portable `data/` folder);
         - *Automatic restart of HidaChat*.
  -   3. *Error handling and resilience*:
       - In case of failure during any phase (download timeout, SHA-256 checksum discrepancy, I/O file locks), display a clear localized error message within the graphical window with "Cancel" or "Retry" buttons, never corrupting the installation or terminating silently.
  -   4. *Full multilingual localization*:
       - Added and synchronized all text, progress messages, and phase titles across all 5 official dictionaries (`Localization.vb`: English, Italian, French, Spanish, German).
  - **Acceptance Test**:
    - Run update procedure and verify appearance of graphical window with progress bar and visible state transitions;
    - Verify that no `.ps1` or `.bat` script file is created or executed in `%TEMP%` or elsewhere;
    - Verify proper handoff: main app shutdown, file replacement, automatic restart, and full preservation of all data in `data/`.
  - **Priority**: P2 | **Impact**: High | **Effort**: Medium
  - ---


## 2026-10-24

- [x] **TASK-0106** [Low] Transparent NTFS compression (compact.exe) for portable data folder~~
  - **Files**: `NtfsCompressionPolicy.vb`, `Application.xaml.vb`, `AccountManager.vb`, `Tests/SessionTests.vb`
  - **Problem / Need**: The `data/webview` directory contains highly compressible files (LevelDB databases, JSON, JavaScript, text logs). Compressing the directory into a ZIP/7z archive on shutdown and extracting on startup would incur significant startup lag and severe corruption risk on crashes or abrupt power loss.
  - **Implemented Features**:
    - Dedicated module `NtfsCompressionPolicy.vb` with method `EnableNtfsCompression(folderPath)` and argument builder `BuildCompactArguments` (`/C /S:"..." /I /Q`).
    - Non-blocking background invocation of `compact.exe` on startup in `Application.xaml.vb` on primary `data/` folder upon acquiring folder lock.
    - Automatic invocation in `AccountManager.LoadAccountsAsync()` on shared directory `data/webview`.
    - Safe handling of removable filesystems (FAT32/exFAT): `/I` parameter silently ignores unsupported filesystems without errors or delays.
    - Automated unit tests in `Tests/SessionTests.vb` covering CLI parameter validation, null/whitespace path handling, and directory creation.
  - **Acceptance Test**: On NTFS drives, verify properties of `data/webview` indicate active "Compressed" attribute, achieving approximately 40-50% disk space reduction without degrading WebView2 read/write performance.
  - **Priority**: P3 (Low severity) | **Impact**: Medium | **Effort**: Low

- [x] **TASK-0045** [Medium] Utility Bar & Quick Audio / Device Management~~
  - **Description**: Add quick utility options in the title bar or tab context menu:
    - **Quick Mute/Unmute** of current account audio (`CoreWebView2.IsMuted = True/False`).
    - **Media Indicator** (speaker icon on tab when an account is playing voice notes, notification sounds, or videos).
    - Quick shortcut "Copy chat link" or "Clear account cache".
  - **Implemented Features**:
    - Dedicated button `BtnMuteActiveTab` in the title bar with dynamic icon (active speaker `#00a884` when playing or `#8696a0`, struck-through in magenta `#ec4899` when muted) and global shortcut `Ctrl+M`.
    - Dedicated button `BtnCopyLink` in the title bar and global shortcut `Ctrl+Shift+C` to instantly copy the link/URL of the active chat or instance to the clipboard with discreet Toast feedback (`MessagePopup`).
    - Interactive media indicator `TabMediaButton` integrated on each individual tab: displays `??` when audio/video is playing or `??` if account is muted, with ability to mute/unmute audio via a single direct click without leaving the active tab.
    - Dedicated entries in tab context menu (`AccountTab_ContextMenuOpened`): dynamic contextual toggle "Mute/Unmute tab audio" (`account_mute_tab` / `account_unmute_tab`), "Copy chat link / URL" (`account_copy_link`), and targeted cache cleanup (`account_clean_cache`).
    - Full multilingual support across all 5 official dictionaries (`Localization.vb`: EN, IT, FR, ES, DE) and unit regression tests.
  - **Impact**: Medium | **Effort**: Low-Medium


## 2026-10-23

- [x] **TASK-0102** [Medium] Restrict WebView2 cache at source via AdditionalBrowserArguments~~
  - **Files**: `BrowserArgumentsPolicy.vb`, `AppAccounts.vb`, `SettingsController.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Tests/SessionTests.vb`
  - **Description**: Implemented hard limit disk cache restriction and disabled superfluous Chromium/Edge components via `AdditionalBrowserArguments`.
  - **Implementation**:
    - Created `BrowserArgumentsPolicy.vb` module with deterministic builder `BuildAdditionalBrowserArguments(diskCacheLimitMb, enableSpellcheck, effectiveLang)`.
    - Set default HTTP cache hard limit (`--disk-cache-size=52428800`, 50 MB, configurable via `SettingsController.DiskCacheLimitMb` with new option "50 MB (Compact)" and fallback to 0 for unlimited).
    - Set audio/video cache hard limit `--media-cache-size=20971520` (20 MB).
    - Enabled immediate stale cache disposal flag `--aggressive-cache-discard`.
    - Disabled GPU shader disk cache `--disable-gpu-shader-disk-cache` (preventing buildup in `GPUCache`, `GrShaderCache`, etc.).
    - Blocked background CRX component updates `--disable-component-update`.
    - Blocked telemetry and Edge background models `--disable-background-networking`.
    - Disabled unnecessary features `--disable-features=Translate,MediaRouter,OptimizationGuideModelDownloading,OptimizationHintsFetching` (along with Spellcheck when disabled).
    - Integrated automated test suite in `Tests/SessionTests.vb` (207 assertions with 0 failures).
  - **Files**: `MainWindow.xaml.vb`, `AccountManager.vb`
  - **Problem / Need**: During `CoreWebView2Environment` initialization, Chromium can accumulate hundreds of megabytes of disk cache and download unneeded Edge/Chromium components (including native Chromium translator, superfluous since HidaChat includes its own engine in `JsScripts.vb` / `Scripts/translation.js`, plus background optimization models and telemetry).
  - **Proposed Solution / Fix to Implement**:
  -   Pass specific startup arguments to Chromium via `AdditionalBrowserArguments` property of `CoreWebView2EnvironmentOptions` to enforce disk hard limits and exclude redundant components:
  -   ```vb
  -   Imports Microsoft.Web.WebView2.Core
  -   Public Async Function CreateOptimizedEnvironmentAsync(userDataFolder As String) As Task(Of CoreWebView2Environment)
  -       ' Chromium arguments to drastically limit disk space
  -       Dim args As String = String.Join(" ", {
  -           "--disk-cache-size=52428800",        ' Max 50 MB HTTP cache per profile
  -           "--media-cache-size=20971520",       ' Max 20 MB audio/video cache
  -           "--aggressive-cache-discard",        ' Discard stale cache immediately
  -           "--disable-gpu-shader-disk-cache",   ' Do not save GPU shaders to disk
  -           "--disable-component-update",        ' Block background CRX component downloads
  -           "--disable-background-networking",   ' Avoid Edge telemetry/model downloads
  -           "--disable-features=OptimizationGuideModelDownloading,OptimizationHintsFetching,Translate"
  -       })
  -       Dim options As New CoreWebView2EnvironmentOptions(
  -           additionalBrowserArguments:=args,
  -           language:=Nothing,
  -           targetCompatibleBrowserVersion:=Nothing,
  -           allowSingleSignOnUsingOSPrimaryAccount:=False
  -       )
  -       Return Await CoreWebView2Environment.CreateAsync(Nothing, userDataFolder, options)
  -   End Function
  -   ```
  - **Acceptance Test**: Verify on startup that WebView2 instances initialize with specified flags, HTTP cache does not exceed preset ceiling, and WhatsApp and Telegram navigation remains fully functional.
  - **Priority**: P2 (Medium severity) | **Impact**: High | **Effort**: Low


## 2026-10-20

- [x] **TASK-0069** [Medium] Translation: privacy and self-hosted backend~~
  - **Files**: `TranslationProvider.vb`, `Localization.vb` (`TranslateSingleDetailedAsync`, `TranslateBatchDetailedAsync`, `TranslationCacheService`), `SettingsController.vb`, `TranslationPrivacyConsentDialog.xaml`, `TranslationPrivacyConsentDialog.xaml.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `AppAccounts.vb`, `Scripts/translation.js`, `Tests/SessionTests.vb`
  - **Problem**: Message text not yet in local cache was transmitted directly to Google Translate (`translate.googleapis.com`): chat content left the user's computer without prior explicit consent, conflicting with corporate and personal data privacy requirements.
  - **Implemented Fix**:
    - **Unified Provider Abstraction & Architecture (`ITranslationProvider`, `TranslationProvider.vb`)**:
      - Defined typed `ITranslationProvider` interface with asynchronous methods `TranslateSingleAsync`, `TranslateBatchAsync`, and `TestConnectionAsync`;
      - Implemented `GoogleTranslationProvider`: optimized public HTTPS client with protected chunking against bad splits;
      - Implemented `LibreTranslateProvider`: full support for local or private LibreTranslate instances with both single translations and native batch arrays, plus optional API key authentication;
      - Implemented `OllamaTranslationProvider`: direct integration with local LLMs (e.g. `llama3.2`, `qwen2.5`) via `/api/generate` API, optimized prompts, and automatic output sanitization;
      - Implemented `OpenAiTranslationProvider`: compatibility with all OpenAI-compatible chat completions endpoints (LM Studio, LocalAI, vLLM, OpenAI, Groq) with automatic URL normalization and optional Bearer token header;
      - Created central registry `TranslationProviderRegistry` for provider resolution and factory based on configuration.
    - **Mandatory Opt-In Privacy Control on First Use (`TranslationPrivacyConsentDialog.xaml`)**:
      - Translation feature is now strictly opt-in (`TranslationPrivacyConsent = False` by default);
      - On first translation attempt (single or batch), a dark-themed modal window transparently informs the user about transmitting text to the configured provider, explaining differences between external cloud servers and local/on-premise self-hosted providers;
      - Without explicit consent, no data leaves the computer and the interface returns a discreet notice;
      - Consent status can be granted or revoked at any time in Settings.
    - **Clear and Discreet Error Handling for Unreachable Providers**:
      - In case of connection errors, timeouts, or offline servers (e.g. LibreTranslate or Ollama container not started), the app does not crash and avoids polluting cache with faulty translations;
      - The translation bubble (`translation.js`) displays a discreet failure reason (e.g. *"Translation failed: Provider unreachable"* or *"Translation cancelled: privacy consent required"* in soft colored italics).
    - **Configuration Panel and Connection Test in Settings (`SettingsWindow.xaml`)**:
      - Added provider selector, dynamic configuration fields (endpoint URL, model, API key), and interactive "Test Connection" button showing status and real-time network latency in milliseconds.
    - **Full Multilingual Localization (`Localization.vb`)**:
      - Synchronized 15 new localization keys across all 5 official dictionaries (English, Italian, French, Spanish, German).
    - **Automated Unit Test Suite (`Tests/SessionTests.vb`)**:
      - Added 33 new regression tests (totaling 156 automated assertions with 0 errors) validating provider resolution, self-hosted flags, Opt-in isolation, and resilient network failure handling.
  - **Impact**: Medium | **Effort**: Medium


## 2026-10-19

- [x] **TASK-0068** [High] Platforms: from strings to strongly typed model~~
  - **Files**: `PlatformDefinition.vb`, `AppAccounts.vb`, `AccountManager.vb`, `SettingsWindow.xaml.vb`, `MainWindow.xaml.vb`, `BulkSenderWindow.xaml.vb`, `BulkSenderEngine.vb`, `MessagePopup.xaml.vb`, `Tests/SessionTests.vb`
  - **Problem**: Platform management was based on untyped strings (`"WhatsApp"`, `"Telegram"`, `"OpenClaw"`, `"Hermes"`). Even though items #53 and #54 had integrated respective platforms, configuration of URLs, SVG icons, colors, injected scripts, and host rules was spread across long series of `If / ElseIf` conditions and scattered boolean flags (`IsOpenClaw`, `IsHermes`, `IsTelegram`, `IsWhatsApp`).
  - **Implemented Fix**:
    - Created strongly typed abstraction `PlatformDefinition` and enumeration `ChatPlatform` (`WhatsApp`, `Telegram`, `OpenClaw`, `Hermes`);
    - Implemented concrete classes `WhatsAppPlatformDefinition`, `TelegramPlatformDefinition`, `OpenClawPlatformDefinition`, and `HermesPlatformDefinition` encapsulating default URLs, local proxy ports, hex brand/theme colors, SVG icon paths, origin authorization checks (`IsAuthorizedOrigin`), internal window handling (`IsAllowedNewWindowHost`), deep-link resolution (`ResolveDeepLink`), and bootstrap/theme JS scripts;
    - Created central thread-safe registry `PlatformRegistry` for dynamic platform resolution and extension;
    - Completely refactored `AppAccounts.vb` to delegate visual rendering, URLs, local proxies, and script logic to `PlatformDefinition`, while maintaining `Platform As String` property 100% compatible with `settings.json` and XAML data binding;
    - Refactored `AccountManager.vb`, `SettingsWindow.xaml.vb`, `MainWindow.xaml.vb`, `BulkSenderWindow.xaml.vb`, `BulkSenderEngine.vb`, and `MessagePopup.xaml.vb` eliminating rigid string-based `If/ElseIf` chains;
    - Added dedicated unit regression tests in `Tests/SessionTests.vb` for enum and string resolution, fallbacks, proxy ports, deep-links, and origin authorization.
  - **Impact**: High | **Effort**: Medium


## 2026-10-18

- [x] **TASK-0078** [High] Tokens added to requests targeting any localhost port~~
  - **Files**: `LocalAuthPolicy.vb`, `AppAccounts.vb`, `SettingsWindow.xaml.vb`, `Tests/SessionTests.vb`, `Tests/HidaChat.SessionTests.vbproj`
  - **Problem / Need**: The filter pattern used was `http://127.0.0.1:*/*`. Intercepted requests received the local token and, when available, the account Bearer token without verifying that the port was actually the one assigned to that account's proxy. A request to an unrelated local service could therefore receive credentials that did not belong to it.
  - **Completed Implementation**:
    - Created `LocalAuthPolicy.vb` module with strict validation method `ShouldAttachCredentials(requestUriStr, expectedScheme, expectedHost, expectedPort)` verifying exact scheme, host, and port match prior to token injection, and `BuildFilterPattern` scoping WebView2 filter pattern to assigned TCP port (`http://127.0.0.1:{port}/*`).
    - Full refactoring of `EnsureLocalProxyAsync`, `UpdateWebResourceAuthFilter`, and `RemoveWebResourceAuthFilter` in `AppAccounts.vb`: controlled removal of old filters (`CoreWebView2.RemoveWebResourceRequestedFilter`) and detachment of handlers when port, target `ServerUrl`, credentials, or Tailscale status (`TailscaleIntegration`) change.
    - Strict validation for both local tsnet proxy (`127.0.0.1:{LocalProxyPort}` for `X-HidaChat-Local-Token` and `Bearer`) and direct gateway connections (exact port and authority of `ServerUrl`).
    - Created automated unit test suite in `Tests/SessionTests.vb` validating acceptance on correct port and rejection on mismatched ports, non-matching hosts, bad schemes, and spoofing/subdomain attacks.
  - **Priority**: P1 | **Impact**: High | **Effort**: Low-Medium


## 2026-10-17

- [x] **TASK-0077** [High] Proxy allows bypassing local authentication~~
  - **Files**: `tsnetd/main.go`, `tsnetd/main_test.go`
  - **Problem / Need**: The condition detecting a WebSocket request automatically treated it as authorized (`isWs`). Consequently, the mere presence of `Sec-WebSocket-Key`, even without a valid handshake or credentials, bypassed token validation and received the secret local token in `Set-Cookie`.
  - **Completed Implementation**:
    - Removed any bypass based on WebSocket headers (`Sec-WebSocket-Key` or `Upgrade: websocket`): both HTTP requests and WebSocket handshakes must strictly authenticate via `X-HidaChat-Local-Token` header, `__htok` query parameter, or `__htok` cookie.
    - Preventive Origin verification (`isAllowedOrigin`) before rewriting or forwarding to backend: Origin must match the local endpoint (`127.0.0.1:{port}`, `localhost:{port}`, `[::1]:{port}`) or expected target Tailscale endpoint; any external malicious Origin or 'null' is rejected with `403 Forbidden: Invalid Origin`, preventing Cross-Site WebSocket Hijacking (CSWSH) and cross-origin attacks.
    - Hardened local cookie by setting `HttpOnly: true` and `SameSite: Lax` to prevent token exfiltration by page scripts.
    - Created comprehensive unit tests in `tsnetd/main_test.go` verifying that uncredentialed HTTP and WebSocket requests fail (even with forged headers), that unauthorized Origins are blocked, and that authenticated handshakes continue to work.
  - **Priority**: P1 | **Impact**: High | **Effort**: Medium


## 2026-10-15

- [x] **TASK-0076** [High] Storage/IndexedDB integrity check on startup with corrupt data recovery ProgressBar~~
  - **Files**: `StorageIntegrityService.vb`, `StorageRecoveryProgressDialog.xaml`, `StorageRecoveryProgressDialog.xaml.vb`, `AppAccounts.vb`, `MainWindow.xaml.vb`, `Localization.vb`
  - **Problem / Need**:
    - Following sudden shutdowns, hardware crashes, system freezes, or forced PC power cuts during data writes, Chromium IndexedDB databases (such as `tt-data` on Telegram Web A or Signal/LevelDB databases on WhatsApp Web) can suffer blob file loss on disk while retaining active references in LevelDB log/manifest files.
    - On subsequent launch, Chromium throws irreversible exception `NotReadableError: Data lost due to missing file. Affected record should be considered irrecoverable`. Web apps (particularly Telegram Web) fail to handle this Promise rejection during initial bootstrap and halt JavaScript execution prior to DOM mounting, leaving a completely blank white screen.
  - **Completed Implementation**:
  -   1. *Preventive Check on Startup / Profile Load*:
       - Created `StorageIntegrityService.vb` service with `CheckIndexedDbCorruption(profileDir)` method structurally analyzing `EBWebView\Default\IndexedDB\*.indexeddb.leveldb` directories prior to WebView2 launch (verifies existence and integrity of `CURRENT`, validity and non-emptiness of referenced `MANIFEST-*` file, and associated blob directory integrity).
  -   2. *Visual Feedback via Recovery Window / ProgressBar*:
       - Created dedicated graphical window `StorageRecoveryProgressDialog` (`WindowStyle="None"`, transparent with rounded corners and native dark theme, `Topmost="True"`, indeterminate ProgressBar, and dynamic phase indicator).
  -   3. *Execution of Targeted Recovery*:
       - Automated procedure `PerformStorageRecoveryWithDialogAsync`: clean removal of corrupted IndexedDB/ServiceWorker directories while fully safeguarding `Local Storage`, preferences files, DPAPI tokens, session cookies, and login credentials.
       - Fully integrated both at cold startup, warm runtime via diagnostic event `INDEXEDDB_CORRUPTED` in `HandleDiagnosticMessageAsync`, and from "Repair Tab" context menu tool.
  -   4. *Conclusion and Localization*:
       - Automatic closing of ProgressBar and page reload upon completion;
       - Full localization of messages and states across all 5 supported languages (EN, IT, FR, ES, DE).
  - **Impact**: High | **Effort**: Medium

- [x] **TASK-0075** [High] White screen detection on Hermes / OpenClaw profiles and Tailscale connection status check~~
  - **Files**: `AppAccounts.vb`, `MainWindow.xaml.vb`, `TsnetManager.vb`, `TailscaleConnectionErrorDialog.xaml`, `TailscaleConnectionErrorDialog.xaml.vb`, `Localization.vb`
  - **Problem / Need**: Hermes and OpenClaw profiles frequently communicate with instances and web servers hosted on Tailscale nodes (via the app's embedded tsnet node, OS-installed Tailscale client, or local reverse proxy). If Tailscale is disconnected, remote node is down, or tsnet hasn't completed start/auth, WebView2 displays a blank white screen (or DNS resolution / connection refused error), confusing the user.
  - **Completed Implementation**:
    - Active navigation monitoring of Hermes and OpenClaw profiles in `NavigationCompleted` detecting network failures (`IsSuccess = False`, DNS/connection errors) and automated post-DOM load checking via introspection script (detecting persistent white screen `empty_body` or gateway errors `502 Bad Gateway` / `504 Gateway Timeout`).
    - Implemented in-depth diagnostic method `CheckTailscaleConnectivityAsync` in `TsnetManager.vb`:
      - Checks embedded tsnet module state (`NeedsLogin`, `Stopped`, or HTTP/proxy handshake with `X-HidaChat-Local-Token` to `127.0.0.1:{port}`);
      - Verifies TCP reachability of remote Tailscale endpoint and checks for presence of native Windows Tailscale client.
    - Created dedicated modal dialog `TailscaleConnectionErrorDialog` with dark WhatsApp theme:
      - Clear explanatory message and detailed diagnosis (tsnet needs login, tsnet stopped, remote host unreachable, or Tailscale client disconnected on system);
      - Interactive action buttons: "Retry" (automatic tab reload and route refresh), "Log in to Tailscale" (directly opens node auth URL if required), "Settings" (opens HidaChat preferences), "Close".
    - Synchronized all strings, titles, and buttons across all 5 supported languages (EN, IT, FR, ES, DE).
  - **Impact**: High | **Effort**: Medium


## 2026-10-14

- [x] **TASK-0074** [Medium] Progress window / dialog during application save and shutdown~~
  - **Files**: `ShutdownProgressDialog.xaml`, `ShutdownProgressDialog.xaml.vb`, `MainWindow.xaml.vb`, `Localization.vb`
  - **Problem / Need**: When closing the application, critical asynchronous background operations are performed (profile sync to SMB/UNC network paths, `settings.json` flush and persistence, releasing `data/.lock` and session locks, controlled teardown of WebView2 processes, and shutting down tsnet proxy/node). Without immediate visual feedback, users might perceive the app as frozen or attempt to force-terminate via Task Manager, causing potential file truncation or dangling file locks.
  - **Completed Implementation**:
    - Created dedicated graphical window `ShutdownProgressDialog` (`WindowStyle="None"`, transparent with rounded borders and dark WhatsApp styling, `Topmost="True"`, indeterminate ProgressBar, and phase indicator).
    - Managed and notified shutdown phases in real time during `ShutdownCoreAsync`:
  -     1. Saving settings and account preferences;
  -     2. Controlled shutdown of WebView2 browser instances;
  -     3. Synchronizing profiles and chats to network paths (SMB/UNC) if enabled;
  -     4. Controlled termination of Tailscale proxy `tsnetd` and releasing session/directory locks;
  -     5. Finalizing and orderly closing the application.
    - Synchronized strings and state descriptions across all 5 supported languages (EN, IT, FR, ES, DE).
  - **Impact**: Medium-High | **Effort**: Medium


## 2026-10-13

- [x] **TASK-0072** [Medium] Cache cleanup "preserving login" (Smart Cache Cleanup)~~
  - **Files**: `AccountManager.vb`, `AppAccounts.vb`, `MainWindow.xaml`, `MainWindow.xaml.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`
  - **Description**: Implemented targeted, non-destructive cache cleanup deleting only volatile cache, temporary files, shaders, and GPU caches (`Cache`, `Code Cache`, `GPUCache`, `DawnWebGPUCache`, `DawnGraphiteCache`, `Crashpad`), leaving intact `Local Storage`, `IndexedDB`, `Network\Cookies`, credentials, and login sessions.
  - **Implementation**:
    - Method `SmartCleanCachePreservingLoginAsync()` in `AppAccounts.vb` combining runtime native call `Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.DiskCache Or DownloadHistory)` with deletion of unlocked disk files for both primary profile and local network drive staging.
    - Aggregate method `SmartCleanCachePreservingLoginAsync()` in `AccountManager.vb` iterating and cleaning all profiles concurrently.
    - Dedicated button "Clean Cache (preserve login)" in "Cache & Storage" section of `SettingsWindow.xaml` with confirmation dialog and success notice.
    - Right-click context menu item on account tab in `MainWindow.xaml` ("Clean account cache") for per-session targeted cleanup.
    - Synchronous localization across all 5 official languages (EN, IT, FR, ES, DE).
  - **Impact**: Medium | **Effort**: Low-Medium

- [x] **TASK-0071** [Medium] Chromium cache limitation via AdditionalBrowserArguments (`--disk-cache-size`)~~
  - **Files**: `AppAccounts.vb` (`SetupWebViewInternalAsync`), `SettingsController.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`
  - **Description**: Limited Chromium disk cache via `AdditionalBrowserArguments` during WebView2 environment creation using standard switch `--disk-cache-size=<bytes>`.
  - **Implementation**:
    - Dynamic configuration in `SettingsController.vb` with `DiskCacheLimitMb` property (default: 256 MB, options: 128 MB, 256 MB, 512 MB, 1024 MB, 0 = Unlimited) and persistence in `settings.json`.
    - Injected parameter `--disk-cache-size={CLng(DiskCacheLimitMb) * 1024L * 1024L}` into `CoreWebView2Environment` creation in `AppAccounts.vb`.
    - Dedicated dropdown menu `ComboDiskCacheLimit` in Settings with full multilingual localization (EN, IT, FR, ES, DE).
  - **Impact**: Medium | **Effort**: Low

- [x] **TASK-0050** [Medium] Smart Inactive Tab Hibernation (Tab Hibernation)~~
  - **Description**: Reduce RAM memory footprint by suspending the rendering state of background tabs left unused for more than 15-60 minutes (configurable, default 30 min), waking them instantly upon user click.
  - **Implementation**:
    - Added methods `TryHibernateAsync()` and `ResumeFromHibernation()` in `AppAccounts.vb` based on native APIs `CoreWebView2.TrySuspendAsync()` and `CoreWebView2.Resume()`, with `LastActiveTime` tracking and `IsHibernated` property.
    - Periodic background timer in `MainWindow.xaml.vb` (`_hibernationTimer`, 60s) checking inactivity of background tabs and suspending them transparently.
    - Instant wake-up (0 ms delay) upon focus shift to the tab in `SwitchToAccountAsync` or by setting `IsActive = True`.
    - Visual indicator `??` on hibernated tabs in the horizontal bar of `MainWindow.xaml`.
    - Configuration in Settings (`SettingsWindow.xaml`): toggle switch `ChkEnableTabHibernation` and interval selector `ComboTabHibernationTimeout` (15, 30, 60 min) with persistence in `settings.json` and full multilingual support in all 5 languages (EN, IT, FR, ES, DE).
  - **Impact**: Medium | **Effort**: Medium


## 2026-10-11

- [x] **TASK-0070** [Medium] Portable usage: multi-PC lock and README consistency~~
  - **Files**: `Application.xaml.vb` (`TryAcquireFolderLock`, `ReleaseFolderLock`, `OnStartup`, `OnExit`), `README.md`, `README.it.md`, `Localization.vb`
  - **Problem**: The README file recommended not running the same portable instance simultaneously across different machines (tangible risk of file locks and WebView2 profile corruption), but the application lacked filesystem-level prevention mechanisms. Furthermore, "Zero Installation" claims conflicted with the .NET 9 runtime requirement for framework-dependent releases.
  - **Implemented Fix**:
    - Implemented application-level exclusive filesystem lock `data/.lock` during startup (`Application.OnStartup`) held open for the session duration;
    - Structured JSON metadata recorded in lock file (hostname, username, PID, start ISO timestamp);
    - On contention detection or existing active session in the same folder (from another networked machine or local process), displays a blocking message (localized in all languages with machine, PID, and timestamp details) and performs controlled shutdown to prevent WebView2 profile corruption;
    - Clean release and automatic removal of `data/.lock` upon application exit in `OnExit` and shutdown paths;
    - Harmonized documentation in `README.md` and `README.it.md`: clarified active multi-PC `data/.lock` protection mechanism, and explicitly stated .NET 9 Desktop Runtime requirement while clarifying "Zero Installation" refers to portable deployment (no system installer needed, no registry or external path writes).
  - **Impact**: Medium | **Effort**: Low-Medium
  - ---

- [x] **TASK-0065** [Low] SettingsFile: I/O and migration on every access~~
  - **Files**: `SettingsController.vb` (`SettingsFile` property, `InitializeSettingsFilePath` method)
  - **Problem**: The getter of the `SettingsFile` property performed filesystem I/O checks (`File.Exists`, `Directory.CreateDirectory`) and attempted `File.Move` migration on every single read or write operation of settings.
  - **Implemented Fix**:
    - Calculated and persistently stored the path via thread-safe `Lazy(Of String)` (`_settingsFilePathLazy`), resolved once on first access for the entire application lifecycle;
    - Moved one-time migration logic from root directory to `data/` into dedicated initialization procedure `InitializeSettingsFilePath`;
    - Completely removed all I/O operations and disk access from the `SettingsFile` property getter.
  - **Impact**: Low | **Effort**: Low

- [x] **TASK-0060** [Medium] Portable secrets: DPAPI CurrentUser vs portability~~
  - **Files**: `SettingsController.vb` (`SaveTsnetAuthKeyAsync`, `LoadSettingsAsync`, `ClearTsnetAuthKeyAfterEnrollmentAsync`), `TsnetManager.vb` (`GetMachineStateDir`, `StartAsync`, `CheckStatusAsync`), `AppAccounts.vb` (`WebUrl`, `SetupWebViewInternalAsync`, `EnsureLocalProxyAsync`), `MainWindow.xaml.vb`, `Localization.vb`
  - **Problem**: Using `ProtectedData` with `DataProtectionScope.CurrentUser` ties secrets to the local Windows user and machine: moving the app folder to another PC (on USB stick or external drive) causes `Unprotect` to fail and the `Catch` block silently cleared the key. Furthermore, in `AppAccounts.vb`, the authentication token for OpenClaw and Hermes was appended in plaintext as a query parameter (`?token=`) in the navigation URL, remaining visible in logs and history. This compromised the 100% portability principle.
  - **Implemented Fix**:
    - On DPAPI decryption failure (`CryptographicException`), the application no longer silently clears secrets: sets `TsnetAuthKeyDecryptionFailed = True`, logs an explicit message, and shows a notification dialog to the user requesting re-entry of credentials in Settings;
    - Per-machine isolation of tsnet node state directory (`data/tsnet/<hostname>`), preventing conflicts and node identity cloning across different hosts in portable environments;
    - Automatic release and removal of saved `tsnetAuthKey` once enrollment succeeds (transition to `Running` state), preventing permanent storage of single-use keys in the portable configuration file;
    - Total removal of plaintext `token=` query parameter from URLs for OpenClaw and Hermes; authentication tokens are now injected securely and transparently via HTTP `Authorization: Bearer <token>` header intercepted by CoreWebView2 `WebResourceRequested` event (both in direct mode and via tsnet local proxy);
    - Added and synchronized decryption error notification keys (`secret_decrypt_error_title`, `secret_decrypt_error_msg`) across all 5 language dictionaries (`Localization.vb`: IT, EN, FR, ES, DE).
  - **Impact**: Medium-High | **Effort**: Medium
  - ---

- [x] **TASK-0058** [High] `settings.json`: atomic, serialized writes with flush on shutdown~~
  - **Files**: `SettingsController.vb` (`WriteSettingsAsync`, `WriteSettingsInternalAsync`, `ReadSettingsAsync`, `FlushAfterDebounceAsync`, `FlushNowAsync`, field `_lastFlushTask`, `_ioLock`), `AccountManager.vb` (`SaveAccountsAsync`), `MainWindow.xaml.vb` (`ExitApplication`, `ForceExitForUpdateAsync`, `ForceExitForUpdate`, `MainWindow_Closing`), `Localization.vb`
  - **Problem**:
    - Write operation was not atomic: a crash or mid-write interruption left a truncated or corrupted JSON file;
    - Lack of locking mechanism: `SaveAccountsAsync` and debounced flush could access the same file in parallel (`IOException` suppressed = lost write);
    - `ReadSettingsAsync` swallowed all exceptions and returned an empty dictionary: temporary corruption caused complete loss of settings and account list;
    - `_lastFlushTask` was declared but never assigned or awaited: changes made less than 500 ms before application exit were lost.
  - **Implemented Fix**:
    - Introduced asynchronous semaphore `_ioLock As New SemaphoreSlim(1, 1)` in `SettingsController.vb` that strictly serializes all reads and writes of `settings.json`, preventing concurrent I/O conflicts and corruption;
    - Two-stage atomic write: preliminary write to temporary `.tmp` file, followed by atomic replacement via `File.Replace(tmp, targetFile, targetFile & ".bak")` with automatic `.bak` backup rotation (and fallback to `File.Copy` + `File.Move`);
    - Deterministic cleanup of `.tmp` file in `Finally` block in case of interruption or error;
    - Active tracking of `_lastFlushTask` across writes and 500ms debounce;
    - Method `FlushNowAsync()` canceling any pending debounce, awaiting current I/O completion, and immediately flushing settings to disk if `_dirty = True`;
    - Safe invocation of `SaveAccountsAsync()` and `FlushNowAsync()` upon application exit (`ExitApplication`), before OTA restart (`ForceExitForUpdateAsync` / `ForceExitForUpdate`), and in `MainWindow_Closing` event;
    - Resilience and auto-recovery on read (`ReadSettingsAsync`): in case of unreadable or truncated JSON, automatic quarantine to `settings.corrupt-<timestamp>.json`, transparent attempted recovery from last valid `.bak` backup, and user notification with localized messages across all 5 supported languages (`Localization.vb`: IT, EN, FR, ES, DE).
  - **Impact**: High | **Effort**: Low-Medium


## 2026-09-30

- [x] **TASK-0064** [Low] DndUntil: Nullable comparison in VB.NET~~
  - **Files**: `SettingsController.vb` (`DndUntil`, `SetDndModeAsync`)
  - **Problem**: The statement `If _dndUntil <> value Then` with type `Nullable(Of DateTime)` follows VB.NET three-valued logic: comparison with `Nothing` returns `Nothing` (evaluated as `False` in `If` conditions), preventing the setter from saving the new value when either operand is `Nothing`. The bug was previously masked because `SetDndModeAsync` and `LoadSettingsAsync` assigned private field `_dndUntil` directly.
  - **Implemented Fix**:
    - Replaced comparison in public setter of `DndUntil` with `If Not Nullable.Equals(_dndUntil, value) Then ...`;
    - Reconfigured `SetDndModeAsync` to mutate state exclusively via public property setters (`IsDndEnabled`, `DndUntil`, `DndDurationMode`), guaranteeing correct and synchronous firing of `PropertyChanged` and `IsDndActive` events.
  - **Impact**: Low | **Effort**: Low
  - ---

- [x] **TASK-0063** [High] Async Sub without error handling and unawaited Tasks in bridge~~
  - **Files**: `AppAccounts.vb` (`_navigationCompletedHandler`, `_webMessageReceivedHandler`, `HandleTranslationMessageAsync`, `SafeExecuteScriptAsync`, `Dispose`), `Application.xaml.vb` (`OnStartup`, `OnDispatcherUnhandledException`, `OnUnobservedTaskException`, `OnAppDomainUnhandledException`, `LogUnhandledException`), `MainWindow.xaml.vb`
  - **Problem**:
    - `_navigationCompletedHandler` and `_webMessageReceivedHandler` were declared as `Async Sub` without `Try/Catch` blocks: any call to `ExecuteScriptAsync` on a disposed WebView or during navigation abort raised exceptions in `async void` context, causing fatal app crashes;
    - `Await WebView.Dispatcher.InvokeAsync(Async Function() ...)` only awaited outer `DispatcherOperation` completion: the inner async lambda `Task` was not awaited and its exceptions remained unobserved;
    - `Dispose` method assigned `WebView = Nothing`: any in-flight asynchronous continuations (e.g. batch translations or incoming IPC messages) resulted in `NullReferenceException`;
    - In `Application.xaml.vb`, no global unhandled exception listeners were configured.
  - **Implemented Fix**:
    - Introduced `Try/Catch` blocks and defensive guards `If _isDisposed OrElse WebView?.CoreWebView2 Is Nothing Then Return` in all async handlers of `AppAccounts.vb`;
    - Introduced helper method `SafeExecuteScriptAsync` verifying thread affinity, awaiting the unwrapped inner `Task` via `.Task.Unwrap()` when Dispatcher is required, and preventing post-disposal access;
    - Added `IsDisposed` flag and property on `AppAccounts`, saving a local `wv` reference in `Dispose()` before clearing `WebView` property;
    - Registered in `Application.xaml.vb` events `DispatcherUnhandledException` (with `e.Handled = True`), `TaskScheduler.UnobservedTaskException` (with `e.SetObserved()`), and `AppDomain.UnhandledException` with thread-safe logging to portable file `data/logs/app_errors.log` (with automatic rotation at 1 MB);
    - Guarded all `Async Sub` events with `Try/Catch` and global logging in `MainWindow.xaml.vb` as well (`MainWindow_Loaded`, `PopulateWebViews`, `OnAccountProcessFailedRecoveryRequested`, `AccountTabRename_Click`, `OnSettingsPropertyChanged`, account addition).
  - **Impact**: High | **Effort**: Low


## 2026-09-26

- [x] **TASK-0062** [Medium] IPC Bridge: CSPRNG token and origin validation~~
  - **Files**: `AppAccounts.vb` (`GenerateBridgeToken`, `HandleWebMessageAsync`, `IsAuthorizedOrigin`, `SetupWebViewInternalAsync`)
  - **Problem**: The IPC bridge security token was generated by combining a Unix timestamp with `System.Random` (non-cryptographic pseudo-random, theoretically predictable); `WebMessageReceived` / `HandleWebMessageAsync` validated the token but failed to verify message sender origin (`e.Source`); `AreDevToolsEnabled` was set unconditionally to `True` even in production environments.
  - **Implemented Fix**:
    - Generation of WebView IPC security token via 128-bit CSPRNG (`RandomNumberGenerator.Fill(randomBytes)`);
    - Strict validation of incoming message origin (`e.Source`) via dedicated `IsAuthorizedOrigin` function prior to parsing and processing IPC payloads (`web.whatsapp.com` for WhatsApp, `web.telegram.org` for Telegram, `ServerUrl` host or `127.0.0.1` for OpenClaw and Hermes);
    - Disabled DevTools (`AreDevToolsEnabled = False`) in Release production builds (enabled exclusively under `#If DEBUG` compilation).
  - **Impact**: Medium | **Effort**: Low

- [x] **TASK-0061** [High] NewWindowRequested: ShellExecute on arbitrary URI schemes~~
  - **Files**: `AppAccounts.vb` (`_newWindowRequestedHandler`, `_navigationStartingHandler`)
  - **Problem**: For any URL not belonging to known hosts of the current platform, the handler invoked `Process.Start(..., UseShellExecute = True)` without validating the URI scheme: links with dangerous or uncontrolled schemes (`file:`, `ms-msdt:`, system-registered protocol handlers) were executed arbitrarily. Furthermore, links with `target="_blank"` belonging to the same origin navigated the current WebView instance, overwriting the active chat session.
  - **Implemented Fix**:
    - Applied strict allow-list of schemes before external opening (`http`, `https`, `mailto`), blocking arbitrary execution of system protocol handlers or unauthorized schemes;
    - Extended URI scheme validation to `NavigationStarting`, allowing only standard web schemes (`http`, `https`, `data`, `blob`, `about`) and handling Telegram deep links (`tg://` and `t.me`) internally;
    - Opened all `target="_blank"` or external links in system default browser to preserve intact the primary view of the chat session inside WebView2.
  - **Impact**: High | **Effort**: Low


## 2026-09-24

- [x] **TASK-0067** [Medium] UpdateChecker: UX, localization, version comparison, duplications~~
  - **Files**: `UpdateChecker.vb` (`CheckForUpdatesAsync`, `PerformUpdateFromGitHubAsync`, `FetchGitHubReleaseInfoAsync`, `ParseRelease`, `IsNewerVersion`, `ComparePrerelease`, `Log`), `Localization.vb`
  - **Problem**:
    - `MessageBox` dialogs were hardcoded in Italian in an application localized in 5 languages;
    - On user-forced update checks (`force = True`), network errors, missing GitHub API responses, or rate limits showed no visual feedback (logged only via `Debug.WriteLine`);
    - `IsNewerVersion` compared pre-release suffixes as ordinal strings (`String.Compare`): version "beta10" was evaluated as older than "beta2";
    - Release data JSON parsing logic was almost entirely duplicated across both branches of `FetchGitHubReleaseInfoAsync`;
    - Temporary folder `%TEMP%\HidaChat_Update` was not cleaned up after extraction and update, causing I/O locks in subsequent sessions (fixed with unique session paths and cleanup in `update.bat`).
  - **Implemented Fix**:
    - Added and synchronized 12 new localization keys for update messages and dialog titles (`update_already_latest`, `update_already_latest_title`, `update_no_new_version`, `update_no_new_version_title`, `update_available_prompt`, `update_available_title`, `update_install_error`, `update_install_error_title`, `update_check_failed`, `update_check_failed_network`, `update_check_failed_title`, `update_no_asset`) in dictionaries of all 5 supported languages (`Localization.vb`: EN, IT, FR, ES, DE);
    - Implemented informative visual feedback via localized dialogs when the user forces update checks (`force = True`) upon unreachable endpoints, HTTP/rate-limiting errors, or network exceptions;
    - Implemented `ComparePrerelease` function with SemVer numeric tokenization (`\d+|[^\d\.\-_+]+`) for proper numerical comparison of pre-release suffixes (e.g. "beta10" > "beta2");
    - Unified and isolated release metadata extraction in shared robust helper `ParseRelease(JsonElement)`, eliminating code duplication between beta and stable channels;
    - Implemented thread-safe portable logging system to file `data/updates.log` (with automatic rotation exceeding 1 MB), replacing direct `Debug.WriteLine` calls.
  - **Impact**: Medium | **Effort**: Low-Medium


## 2026-09-22

- [x] **TASK-0066** [Medium] Maximum accounts limit: synchronization and downgrade policy~~
  - **Files**: `AccountManager.vb` (`MaxAccounts`, `CanAddAccount`, `HasExcessAccounts`, `ExcessAccountsCount`, `AddAccountAsync`, `IDisposable`), `SettingsController.vb` (`MaxAccounts`), `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `MainWindow.xaml`, `MainWindow.xaml.vb`, `Localization.vb`
  - **Problem**: Originally `AccountManager.MaxAccounts` was hardcoded to 3. The property was made dynamic according to `SettingsController.MaxAccounts` (range 210) and integrated into `CanAddAccount` and `AddAccountAsync` checks. However, automatic listening to `_settingsController.PropertyChanged` inside `AccountManager` was missing to notify changes to `CanAddAccount` and `MaxAccounts` independently from `SettingsWindow` UI, as was a formal policy when the user sets a limit lower than already configured accounts.
  - **Implemented Fix**:
    - Subscribed to `SettingsController.PropertyChanged` centrally in `AccountManager` constructor to automatically notify changes to `MaxAccounts`, `CanAddAccount`, `HasExcessAccounts`, and `ExcessAccountsCount`; implemented `IDisposable` interface on `AccountManager` to cleanly release listeners;
    - Formal non-destructive downgrade policy: if `MaxAccounts` limit is lowered below configured accounts, existing accounts are completely preserved and remain operational, while adding further accounts is blocked and an explanatory warning banner is displayed in Settings (`BorderDowngradeNotice`);
    - Removed static hardcoded "(max 3)" tooltip from `BtnAddAccount` in `MainWindow.xaml` and synchronized dynamic tooltips, enabled/disabled states, and add-blocking across both `MainWindow` and `SettingsWindow`;
    - Added and synchronized downgrade keys (`max_accounts_downgrade_notice`, `accounts_downgrade_blocked`) across dictionaries of all 5 supported languages (`Localization.vb`: IT, EN, FR, ES, DE).
  - **Impact**: Medium | **Effort**: Low

- [x] **TASK-0059** [High] Non-destructive WebView2 profile cleanup~~
  - **Files**: `AccountManager.vb` (`LoadAccountsAsync`, `CleanupUnusedProfilesAsync`, `MigrateOrphanProfile`, `CreateDefaultAccountAsync`, `RemoveAccountAsync`), `AppAccounts.vb` (`SetupWebViewInternalAsync`), `SettingsController.vb` (`IsLoadedFromValidConfig`, `ReadSettingsAsync`)
  - **Problem**:
    - Irreversible data loss cascade: corrupted/unreadable `settings.json` ? fallback default account loaded ? file rewritten ? on next launch `CleanupUnusedProfilesAsync` permanently deleted on-disk profiles of all other configured accounts (sessions lost, QR codes required again);
    - `CleanupUnusedProfilesAsync` (via `DeleteDirectoryWithRetryAsync`) executed recursive deletions on the UI thread at application startup for Chromium profiles that can weigh hundreds of MBs;
    - `MigrateOrphanProfileAsync` and `SetupWebViewInternalAsync` directly deleted a valid profile directory `WV2Profile_{id}` to replace it with the orphan one.
  - **Implemented Fix**:
    - Tracked configuration file validity via `IsLoadedFromValidConfig` property in `SettingsController.vb`: cleanup of orphaned/unreferenced profiles runs exclusively if the account list was successfully loaded from an intact JSON file or its `.bak` backup, preventing execution during fallback or corruption states;
    - Non-destructive moving of unreferenced profiles to recycle folder `data/webview/_trash/` (instant filesystem-level `Directory.Move`) with unique timestamp, preserving user sessions in case of accidental reconfigurations;
    - Deferred background deletion (`Task.Run`) for items in `_trash/` with retention older than 24 hours using multiple attempts with exponential backoff (`DeleteDirectoryWithRetryAsync`);
    - Moved entire profile scanning and cleanup routine to `Task.Run` off the UI thread, eliminating UI freezes or delays during GUI rendering and active tab attachment;
    - Non-destructive preservation of existing profiles in `MigrateOrphanProfile` (`AccountManager.vb`) and `SetupWebViewInternalAsync` (`AppAccounts.vb`): when an anonymous orphan profile (`WV2Profile_`) is present, any existing disk profile is renamed to `.bak` (with collision-avoidance timestamp) rather than deleted preemptively, with automatic rollback if moving the orphan fails;
    - Auto-discovery and automatic reconstruction of all on-disk profiles in `CreateDefaultAccountAsync`: on initial run or config file loss, detects and registers all top-level `WV2Profile_*` profile folders, preventing them from being orphaned or lost;
    - Non-destructive, non-blocking account removal in `RemoveAccountAsync`: preliminary move to `_trash/` recycle folder and deferred asynchronous background deletion with backoff to allow Chromium processes to release locks.
  - **Impact**: High | **Effort**: Medium


## 2026-09-20

- [x] **TASK-0057** [Medium] Update integrity verification: fail-closed and authenticity~~
  - **Files**: `UpdateChecker.vb` (`PerformUpdateFromGitHubAsync`, `ExtractSha256FromText`, `WriteLocalVersionMarker`), `Localization.vb`
  - **Problem**:
    - If no checksum was found, the update was installed anyway ("verified via HTTPS");
    - The hash originated from the same release as the ZIP: it protected against transmission errors, not against a compromised GitHub release;
    - `WriteLocalVersionMarker` was invoked before copying files: if the robocopy script failed, the marker still declared the new version (whereas the batch script writes it upon copy completion);
    - The insufficient permissions error message suggested `C:\Program Files\HidaChat`, a folder typically non-writable for standard users without elevated privileges (while the application writes to `data/` adjacent to the executable);
    - Generic regex fallback matching any 64-character hex string in release notes could pick up an unrelated hash.
  - **Implemented Fix**:
    - Strict fail-closed integrity check: immediate abort if a valid SHA-256 checksum is not found (in `.sha256` asset or release body);
    - Restricted cryptographic fingerprint search strictly to the ZIP filename of the current release (`<hash> <filename>`, `<filename> <hash>`, or `SHA256:` label), eliminating generic fallback to arbitrary 64-character hex strings;
    - Authenticode digital signature verification on the extracted executable `HidaChat.exe` (if signed, validates certificate and validity period, blocking update in case of corrupted signature);
    - Removed premature call to `WriteLocalVersionMarker`: version marker is written solely by `update.bat` after robocopy successfully completes;
    - Reformulated insufficient permissions message, recommending writable user directories (`Documents`, `Desktop`, USB drives) adhering to 100% portability;
    - Added and synchronized update diagnostic and error messages across dictionaries of all 5 supported languages (`Localization.vb`: IT, EN, FR, ES, DE).
  - **Impact**: Medium-High | **Effort**: Medium

- [x] **TASK-0056** [High] Update download: 15s timeout and entire ZIP buffered in RAM~~
  - **File**: `UpdateChecker.vb` (`Shared Sub New`, `PerformUpdateFromGitHubAsync`)
  - **Problem**: `_httpClient.Timeout = 15s` covered the entire operation: `GetByteArrayAsync(downloadUrl)` failed on large ZIPs or slow connections. Furthermore, the entire archive was loaded into memory prior to writing to disk.
  - **Implemented Fix**:
    - Introduced dedicated `_downloadClient` with `Timeout = InfiniteTimeSpan` and product User-Agent header;
    - Direct HTTP streaming to temporary disk file (`File.Create(tempZipPath)`) via `ResponseHeadersRead` and `CopyToAsync`, avoiding loading the entire archive into RAM;
    - CancellationTokenSource with global timeout extended to 10 minutes to allow reliable downloads even over slow connections;
    - Asynchronous streaming SHA-256 hash calculation directly from the file on disk via `SHA256.HashDataAsync(stream)`.
  - **Impact**: High | **Effort**: Low


## 2026-09-14

- [x] **TASK-0055** [High] WebView2 Crash Resilience & Transparent Auto-Recovery (`ProcessFailed`)~~
  - **Description**: Handling of irreversible Microsoft Edge WebView2 runtime errors (`BrowserProcessExited`, `RenderProcessExited`, GPU crashes, or silent background runtime updates).
  - **Implemented Features**:
    - Strongly-typed interception of `CoreProcessFailed` and `ProcessFailed` at WPF control and CoreWebView2 levels.
    - Immediate auto-reload for isolated render process crashes (`RenderProcessExited`).
    - Full, transparent control recreation (`RecreateAccountWebViewAsync`) for entire Chromium browser crashes or HWND invalidation.
    - Error pop-up prevention and seamless auto-reswitch on tab change (`SwitchToAccountAsync` & `AccountTab_Click`).
    - Hardening of reload button (`BtnReloadActiveTab_Click`) and safe listener unsubscription in `Dispose()`.
  - **Impact**: High | **Effort**: Low-Medium
  - ---


## 2026-09-01

- [x] **TASK-0047** [High] "Do Not Disturb" Mode / Timed Focus Mode~~
  - **Description**: Instant, complete muting of all notifications (WebView2 sounds, overlay popups, and Windows Toasts) across all accounts with configurable timers and automatic restoration upon expiration.
  - **Implemented Features**: Dedicated button `BtnDnd` in the title bar with Bell-Off icon and active badge featuring an end-time indicator in tooltip, context menu for quick duration selection (*30m*, *1h*, *2h*, *8h*, *Indefinite*), synchronous audio muting of WebView2 instances (`IsMuted = True`), total suppression of Toasts/Popups, and configuration in Settings.
  - **Impact**: High | **Effort**: Low


## 2026-08-30

- [x] **TASK-0044** [Medium] Native Multilingual Spellchecker (Spellchecker WebView2)~~
  - **Description**: Enable and manage Microsoft Edge WebView2 / Chromium native spellcheck engine in input fields across WhatsApp Web and Telegram Web.
  - **Implemented Features**: Dedicated section in **Settings** with activation/deactivation toggle `ChkEnableSpellcheck`, dictionary language selector `ComboSpellcheckLanguage` (Automatic from app language, Italian `it-IT`, English `en-US`, French `fr-FR`, Spanish `es-ES`, German `de-DE`), persistence in `settings.json`, and dynamic configuration of Chromium flags (`--enable-features=Spellcheck`, `--lang=...`).
  - **Impact**: Medium | **Effort**: Low


## 2026-08-28

- [x] **TASK-0043** [Medium] Support for Custom User CSS Themes (Custom CSS Injector)~~
  - **Description**: Dedicated section in **Settings** to insert and edit custom CSS rules for WhatsApp Web and Telegram Web (fonts, message bubble colors, sidebar width, chat wallpaper, transparencies, OLED theme).
  - **Implemented Features**: Monospace editor with scrollbar, instant activation checkbox, persistence in `settings.json`, quick preset buttons (*OLED*, *Compact*, *Font*, *Clear*), and synchronous live injection across all WebView2 instances via IPC bridge.
  - **Impact**: Medium | **Effort**: Low-Medium

- [x] **TASK-0042** [Medium] Contact "Online" Status Indicator (Online Indicator)~~
  - **Description**: Intercept via an injected JavaScript script (`MutationObserver` on chat header) when the contact or currently opened chat is "online" (e.g., detecting `online` / `in linea` text or status elements in the active conversation DOM across both WhatsApp Web and Telegram Web).
  - **UI Integration**: Display an elegant animated badge in the `TitleBar` (`OnlineIndicatorBorder`) and green dot on the active account tab for WhatsApp and Telegram Web.
  - **Impact**: Medium | **Effort**: Low


## 2026-08-27

- [x] **TASK-0041** [High] Telegram Integration  Advanced Features and UI/UX Refinements~~
  - **Base State**: ? Completed in v0.5.0 (Routing to `web.telegram.org/k/`, profile isolation in `data/webview/`, dedicated cyan vector icons, and platform selector in Settings).
  - **Multi-Platform Polish**: ? Completed in v0.5.0 (Dark/Light theme synchronization with `.night` classes and `themeController`, selective hover and batch translation with Telegram DOM selectors, popup click routing to notifying account).
  - **Advanced Evolution and Polish**: ? Completed in v0.7.1-beta:
    - **Telegram Notification Badge and Unread Counter**: Continuous tracking of unread message count (combined MutationObserver on `<title>` and DOM scan of `.badge.unread`, `.unread-count`, etc. selectors) with a colored numeric badge pill on the account tab and dynamic tray icon.
    - **Telegram Deep Link Handling (`tg://` and `t.me/`)**: Interception in `NavigationStarting` and `NewWindowRequested` of `tg://resolve?domain=...`, `tg://join?invite=...` protocols and `https://t.me/...` web links, automatically translating them into internal Telegram Web K routes (`https://web.telegram.org/k/#@...` / `https://web.telegram.org/k/#?tgaddr=...`).
    - **Global Keyboard Shortcuts**: Added keyboard shortcuts for quick account switching (`Ctrl+1`, `Ctrl+2`, `Ctrl+3`, and NumPad), circular tab cycling (`Ctrl+Tab`, `Ctrl+Shift+Tab`), new account (`Ctrl+T`/`Ctrl+N`), reload (`Ctrl+R`/`F5`), settings (`Ctrl+,`), and bulk sender (`Ctrl+B`).
  - **Impact**: High | **Effort**: Medium
  - ---


## 2026-08-24

- [x] **TASK-0052** [High] Personalized Bulk Messaging from Excel / CSV Files (.xlsx, .xls, .csv)~~
  - **Description**: Integrated module to import contact lists from Excel spreadsheets (`.xlsx`, `.xls`) or CSV files (`.csv`) and send personalized messages one by one through the active WhatsApp Web account on WebView2.
  - **Implemented Features**:
    - Automatic column header detection and mapping (*Phone*, *First Name*, *Last Name*, *Company*, *Custom Text*).
    - Normalization, non-numeric character cleanup, and international prefix formatting.
    - Dynamic template editor with placeholders (`{Nome}`, `{Cognome}`, `{Azienda}`, `{Telefono}`, `{Testo}`) and quick insertion buttons.
    - Dedicated `BulkSenderWindow` with interactive DataGrid, individual or multiple selection, and colored progress status badges per row (`Waiting`, `Sending...`, `Sent ?`, `Error ?`, `Invalid`).
    - Sequential automation engine `BulkSenderEngine` with unregistered number modal error detection (`INVALID_NUMBER`), chat send injection scripts, and adjustable Anti-Spam delay intervals (Jitter Delay) with real-time countdown.
    - Dedicated Excel icon button integrated into title bar (`MainWindow.xaml`) and full multilingual localization in `Localization.vb`.
  - **Impact**: High | **Effort**: Medium

- [x] **TASK-0040** [Low] No integrity verification on auto-update ZIP~~
  - **Files**: `UpdateChecker.vb`, `PerformUpdateFromGitHubAsync`, `FetchGitHubReleaseInfoAsync`
  - Implemented cryptographic SHA-256 integrity verification before extracting update ZIP archives. The system automatically intercepts dedicated checksum assets (`.sha256`, `.sha256sum`, `SHA256SUMS`, `SHA256SUMS.txt`, `checksums.txt`) or SHA-256 fingerprints declared in GitHub release notes, calculates the downloaded file hash (`SHA256.HashData`), and promptly aborts installation in case of discrepancy, ensuring maximum resilience and protection against tampering or partial downloads.
  - **Impact**: Low | **Effort**: Low
  - ---


## 2026-08-21

- [x] **TASK-0039** [Low] Bridge token exposed as global variable readable by the page~~
  - **Files**: `AppAccounts.vb`, `JsScripts.vb`, `Scripts/notification.js`, `Scripts/translation.js`
  - `bridgeToken` is no longer exposed on the global `window.__bridgeToken` object. It is now interpolated via safe `JsonSerializer.Serialize` serialization and encapsulated privately within the scope and closure (IIFE) of `notification.js` and `translation.js`. No script or extension executing in the DOM/webpage can read, inspect, or tamper with the IPC security token.
  - **Impact**: Low | **Effort**: Low


## 2026-08-20

- [x] **TASK-0038** [Low] Inconsistent escaping of interpolated values in JavaScript executed via `ExecuteScriptAsync`~~
  - **Files**: `AppAccounts.vb` (`HandleTranslationMessageAsync`, `UpdateWebviewLanguageAsync`) and `MainWindow.xaml.vb` (`onNotificationClicked`)
  - All interpolated values and identifiers (`id`, `langCode`, `langName`, `tooltipLabel`, `notificationId`) are now serialized consistently and safely via `JsonSerializer.Serialize`, preventing special character or quote injection and ensuring strict adherence to JSON format.
  - **Impact**: Low | **Effort**: Low

- [x] **TASK-0037** [Low] Profile deletion based on fixed `Task.Delay` instead of deterministic signal~~
  - **Files**: `AccountManager.vb`, `RemoveAccountAsync`, `MigrateOrphanProfileAsync`, `CleanupUnusedProfilesAsync`
  - Implemented asynchronous helper method `DeleteDirectoryWithRetryAsync` with progressive backoff (up to 5 attempts with increasing wait times) to ensure reliable deletion of WebView2 profiles even on slow machines or in the presence of temporary file locks by antivirus/Chromium.
  - **Impact**: Low | **Effort**: Low


## 2026-08-18

- [x] **TASK-0036** [Medium] `settings.json` and `translations_cache.json` in root folder instead of `data/`~~
  - **Files**: `SettingsController.vb`, `SettingsFile` property and `Localization.vb`, `CacheFilePath` property
  - `settings.json` and `translations_cache.json` are now created and saved directly inside the portable `data/` folder. Transparent and automatic migration (`File.Move`) of existing root files to `data/` implemented on first launch, preserving settings and cache.
  - **Impact**: Medium | **Effort**: Low

- [x] **TASK-0035** [Low] `JsScripts.vb`  ~300 lines of translation JS permanently in memory~~
  - JS scripts loaded at startup and never released; copied on every account setup
  - **Optimization**: loaded from embedded resources (`EmbeddedResource`) lazily (`Lazy(Of String)`); persistent translation caching on disk (`data/translations_cache.json`) to prevent repeated downloads from the internet; RAM optimization to keep only the dictionaries of the active language in memory.
  - **Impact**: Low | **Effort**: Medium
  - ---

- [x] **TASK-0034** [Low] `SolidColorBrush`  Created on every theme change in MainWindow and SettingsWindow~~
  - `New SolidColorBrush(...)` called every time theme was applied
  - **Optimization**: cache instances per color
  - **Impact**: Low | **Effort**: Low

- [x] **TASK-0009** [Medium] Batch translations  Fixed chunking at 50 texts~~
  - Chunk size hardcoded to 50, does not consider text lengths
  - **Optimization**: adaptive chunking based on total character length (e.g., max 2000 characters per chunk)
  - **Impact**: Medium | **Effort**: Low

- [x] **TASK-0008** [High] `AccountManager.LoadAccountsAsync()`  Creates all WebView2 instances on startup~~
  - Creates a WebView2 for EVERY account at load time (~50-100MB per instance)
  - **Optimization**: lazy initialization  create WebView2 only for the active account, others on-demand
  - **Impact**: High | **Effort**: High


## 2026-08-15

- [x] **TASK-0033** [Low] Registry `AppsUseLightTheme`  Read 3 times without caching~~
  - Same registry key read in `MainWindow.xaml.vb`, `WhatsAppAccount.vb`, `SettingsWindow.xaml.vb`
  - **Optimization**: centralized `SystemThemeHelper` module with registry cache and listener for `SystemEvents.UserPreferenceChanged` event
  - **Impact**: Low | **Effort**: Low

- [x] **TASK-0032** [Low] `MessagePopup._activePopups`  Static list without WeakReference~~
  - Retains strong references to all popups  memory leak if not closed normally
  - **Optimization**: `WeakReference(Of MessagePopup)` with automatic pruning of GC-collected references
  - **Impact**: Low | **Effort**: Low

- [x] **TASK-0031** [Low] `UpdateChecker.vb`  `batchContent` using string concatenation~~
  - Batch file built with string concatenation across ~35 lines
  - **Optimization**: `StringBuilder` to assemble batch script as a stream
  - **Impact**: Low | **Effort**: Low

- [x] **TASK-0030** [Low] `Directory.GetDirectories` in `AccountManager.vb`~~
  - Eager array instead of lazy enumeration
  - **Optimization**: `Directory.EnumerateDirectories` and `FirstOrDefault()` to short-circuit scan on first match
  - **Impact**: Low | **Effort**: Low

- [x] **TASK-0029** [Low] `AccountManager.vb`  Anonymous type on every save~~
  - Creates anonymous type instances for each account on every save  GC pressure
  - **Optimization**: serialize `WhatsAppAccount` directly (already has `JsonPropertyName`)
  - **Impact**: Low | **Effort**: Low

- [x] **TASK-0028** [Low] `WhatsAppAccount.vb`  `New Random()` on every call~~
  - System clock seed can produce identical sequences if calls are close together
  - **Optimization**: `Random.Shared` (.NET 9) or `Shared` with lock
  - **Impact**: Low | **Effort**: Low

- [x] **TASK-0027** [Medium] `AccountManager` and `SettingsController`  No dirty tracking~~
  - `SaveAccountsAsync()` and `WriteSettingsAsync()` rewrote everything even when nothing changed
  - **Optimization**: `_dirty` flag + writing only on actual modifications
  - **Impact**: Medium | **Effort**: Low

- [x] **TASK-0026** [Medium] `JsScripts.vb`  Multiple `.Replace()` on large templates~~
  - `GetTranslationJS` calls `.Replace()` 10+ times on a huge template  creates a new string each time
  - **Optimization**: `StringBuilder` with in-place `Replace`
  - **Impact**: Medium | **Effort**: Low

- [x] **TASK-0025** [Medium] `ExecuteScriptAsync` fire-and-forget in `MainWindow.xaml.vb`~~
  - Tasks returned by `ExecuteScriptAsync` were neither awaited nor caught  unobservable exceptions
  - **Optimization**: capture tasks or use `Async Sub`/`Async Function` with `Await` and error handling (`Try...Catch`)
  - **Impact**: Medium | **Effort**: Low

- [x] **TASK-0023** [Medium] `MainWindow.xaml.vb`  `PropertyChanged` event handlers never removed~~
  - `_settingsController.PropertyChanged` and `_accountManager.PropertyChanged` registered but never unhooked in `OnClosed`/`OnUnloaded`
  - **Optimization**: add `RemoveHandler` in window closing events
  - **Impact**: Medium | **Effort**: Low

- [x] **TASK-0022** [Medium] `CancellationTokenSource` in `SettingsController.FlushAfterDebounceAsync`  Undisposed~~
  - Previous CTS was only cancelled (`Cancel()`) but never disposed (`.Dispose()`)  retains `WaitHandle` (OS resource)
  - **Optimization**: dispose the old `_flushCts` before replacing it with the new one
  - **Impact**: Medium | **Effort**: Low

- [x] **TASK-0021** [Medium] `Directory.GetFiles` in `UpdateChecker.vb`  Eager enumeration on network share~~
  - Loads ALL file paths into `String()` array  potentially thousands of strings on a UNC path
  - **Optimization**: `Directory.EnumerateFiles` (lazy streaming)
  - **Impact**: Medium | **Effort**: Low


## 2026-08-05

- [x] **TASK-0020** [Medium] `Localization`  `HttpClient` created 3 times (not shared)~~
  - `New HttpClient()` in each method  socket exhaustion under load
  - **Optimization**: `Shared ReadOnly HttpClient` at class/module level
  - **Impact**: Medium | **Effort**: Low

- [x] **TASK-0019** [High] `ActiveNotificationIds`  HashSet grows without bounds~~
  - Notification IDs added on `NOTIFICATION_RECEIVED` but never removed if `NOTIFICATION_CLOSED` is not received
  - **Optimization**: maximum size limit + periodic cleanup for stale IDs
  - **Impact**: High | **Effort**: Low

- [x] **TASK-0015** [High] Event handlers on `CoreWebView2` never removed~~
  - `PermissionRequested`, `NewWindowRequested`, `WebMessageReceived`, `NavigationCompleted` registered but never removed with `RemoveHandler`
  - WebView2 holds strong references to the account  definite memory leak on removal
  - **Optimization**: saved handler references and called `RemoveHandler` before releasing WebView2
  - **Impact**: High | **Effort**: Low

- [x] **TASK-0013** [High] `WhatsAppAccount`  Implement `IDisposable`~~
  - WebView2 (COM/GPU) and `ActiveNotificationIds` never disposed when an account is removed
  - **Optimization**: implemented `IDisposable` and called by `AccountManager` upon account removal
  - **Impact**: High | **Effort**: Medium

- [x] **TASK-0012** [Low] Translation cache  Full write on every language change~~
  - `SaveCacheFileAsync()` rewrites the entire cache file even for minimal changes
  - **Optimization**: deferred or incremental write
  - **Impact**: Low | **Effort**: Low
  - *Obsolete: UI translations are now hardcoded (EN/IT). Cache is written only during initialization, not on every language change.*
  - ---

- [x] **TASK-0011** [Low] `SwitchToAccountAsync()`  Visibility of all WebViews~~
  - Iterates ALL WebViews to set `Visibility = Collapsed` and only one to `Visible`
  - **Optimization**: track the last active account and hide only that one with a preliminary check if account is already active
  - **Impact**: Low | **Effort**: Low

- [x] **TASK-0010** [Low] `MessagePopup.RepositionAll()`  Iterates all active popups~~
  - Every time a popup is shown/closed, recalculates the position of ALL active popups
  - **Optimization**: track next popup Y position incrementally, stacking upward without repositioning already active popups
  - **Impact**: Low | **Effort**: Low

- [x] **TASK-0007** [Medium] OTA Update  `ReadVersionFromFileAsync()` on UNC path~~
  - Obsolete in v0.2.0: reading from network UNC path and `ReadVersionFromFileAsync()` have been completely removed. The OTA system operates exclusively via GitHub Releases REST API.

- [x] **TASK-0006** [Medium] WebView2  Script injection on every navigation~~
  - On every `NavigationCompleted`, unnecessarily re-injects CSS theme and translation script on secondary navigations
  - **Optimization**: inject only on initial navigation, use `AddScriptToExecuteOnDocumentCreatedAsync` for permanent scripts
  - **Impact**: Medium | **Effort**: Low

- [x] **TASK-0005** [Low] `FindVisualChildren()` and `FindVisualChild()`  Multiple recursion~~
  - Recursive functions called 3-4 times per window that traverse the ENTIRE visual tree
  - **Optimization**: memoization (tree caching) or replacement with declarative bindings/styles
  - **Impact**: Low | **Effort**: Low

- [x] **TASK-0004** [Medium] `AccountsList.ItemsSource = Nothing` + re-assignment~~
  - After deleting an account, resets and reassigns `ItemsSource`, forcing re-generation of ALL DataTemplates
  - **Optimization**: use `ObservableCollection` instead of `List(Of WhatsAppAccount)`
  - **Impact**: Medium | **Effort**: Low

- [x] **TASK-0003** [Low] `StyleAccountItems()`  Visual tree recursion every time~~
  - Calls `FindVisualChildren(Of Border)` and `FindVisualChildren(Of TextBox)` across the entire `AccountsList`
  - **Optimization**: use WPF styles with DataTriggers instead of code-behind, or use `Loaded` event handler on DataTemplate elements
  - **Impact**: Low | **Effort**: Low

- [x] **TASK-0002** [High] `FetchTranslations()`  N serial HTTP requests~~
  - For each language key (30+), makes an individual HTTP request to Google Translate sequentially
  - **Optimization**: group translations into a single batch request, or use `HttpClient` with parallel requests (`Task.WhenAll`)
  - **Impact**: High | **Effort**: Medium

- [x] **TASK-0001** [High] `settings.json` I/O  Multiple redundant reads~~
  - `SaveSettingAsync()` performs `ReadSettingsAsync()` + `WriteSettingsAsync()` every time a single key changes
  - **Optimization**: accumulate changes in memory and write to disk with debounce (e.g., 500ms) or rewrite only the file without re-reading it
  - **Impact**: High | **Effort**: Low


## 2026-07-27

- [x] **TASK-0024** [Low] `DispatcherTimer` for chat synchronization  Removed~~
  - Timer removed in v0.1.0 along with backup synchronization system.

- [x] **TASK-0018** [Medium] `EncryptBytes` / `DecryptBytes`  Removal of backup system~~
  - Methods removed in v0.1.0.

- [x] **TASK-0017** [Medium] `SaveMessageBatchAsync`  Removal of backup system~~
  - Method removed in v0.1.0.

- [x] **TASK-0016** [Medium] `CryptoHelper` and backup encryption  Removal of backup system~~
  - `CryptoHelper` class removed in v0.1.0 along with backup removal.

- [x] **TASK-0014** [Medium] `ChatJsonStorageService` and `ChatSyncBackgroundWorker`  Removal of backup system~~
  - Classes removed in v0.1.0 along with the entire chat sync and disk storage system.
