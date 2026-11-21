# TODO

[![English](https://img.shields.io/badge/Language-English-blue.svg)](TODO.md)
[![Italiano](https://img.shields.io/badge/Lingua-Italiano-green.svg)](TODO.it.md)

---

## Security, Sessions & Core Reliability

- [ ] **TASK-0081** [High] A hang in tsnetd can indefinitely block shutdown
  - **Files**: `TsnetManager.vb`, approx lines 247–263 and 386–420
  - **Problem / Need**: `SendCommandAsync()` awaits semaphore and a line from stdout without a timeout. `ShutdownAsync()` calls this method before reaching `WaitForExit(2000)`. Consequently, if the daemon remains alive but unresponsive, the two-second limit is never reached. A previously hanging command can also hold the semaphore and prevent shutdown dispatch.
  - **Fix to Implement**: Apply a cancellable timeout to the entire operation, including semaphore acquisition. Shutdown must have an overall deadline independent of the command channel. Upon expiration, close the channel and handle the process without leaving dangling reads.
  - **Acceptance Test**: Use a test daemon that stops responding on stdout. Shutdown and updates must terminate or fail with a clear error within the expected limit, never remaining hung.
  - **Priority**: P1 | **Impact**: High | **Effort**: Medium

- [ ] **TASK-0082** [Medium] HTTPS certificate verification is disabled
  - **Files**: `tsnetd/main.go`, approx lines 259–269; `SettingsWindow.xaml.vb`, approx lines 320–322
  - **Problem / Need**: The proxy transport sets `InsecureSkipVerify = true`; additionally, the connection test uses a callback that unconditionally accepts any certificate. HTTPS verification of the target service is bypassed in affected paths. In Go, this setting disables standard chain and server name verification. Tailscale tunnel encryption is distinct from validating the HTTPS identity of the service.
  - **Fix to Implement**: Standard validation as default behavior. For servers with private certificates, allow explicit CA configuration or certificate pinning; avoid silent global bypass.
  - **Acceptance Test**: An expired, untrusted, or hostname-mismatched certificate must produce a TLS error distinct from general reachability issues.
  - **Priority**: P2 (subsequent phase) | **Impact**: Medium | **Effort**: Medium

- [ ] **TASK-0083** [Medium] "Test connection" reports Online even when service is down
  - **Files**: `SettingsWindow.xaml.vb`, approx lines 295–348, `BtnTestConnection_Click()` method
  - **Problem / Need**: After `GetAsync()`, code sets `isOnline = True` without checking the HTTP status code. Responses like 401, 403, 500, and 502 can therefore produce a green status indicator. Furthermore, for OpenClaw, this test does not add the Bearer token: the header is only set in the Hermes branch.
  - **Fix to Implement**: Distinguish at least reachability, authentication, and service availability. Use an appropriate platform endpoint and do not consider test successful simply because an HTTP response arrived.
  - **Acceptance Test**: Test responses for 200, 401, 403, 500, 502, and timeout. Bad credentials and unreachable backends must show distinct indicators, both differing from "connection successful".
  - **Priority**: P2 (subsequent phase) | **Impact**: Medium | **Effort**: Low-Medium

- [ ] **TASK-0086** [Medium] Updater cleanup can delete an active update session
  - **Files**: `UpdateChecker.vb`, approx lines 516–545 and subsequent
  - **Problem / Need**: Cleanup is started with fire-and-forget `Task.Run()` and not awaited. It scans for all folders and archives matching update prefixes without distinguishing stale from active sessions. If cleanup runs late, it can interfere with freshly extracted files. Two portable copies in different directories sharing the same `%TEMP%` can also delete each other's temporary files.
  - **Fix to Implement**: Identify genuinely obsolete sessions first, always exclude current session, and protect active sessions with locks. Prevent concurrent updates of the same installation.
  - **Acceptance Test**: Artificially delay cleanup and launch two updates from separate portable copies. Neither session must delete files actively used by the other.
  - **Priority**: P2 (subsequent phase) | **Impact**: Medium | **Effort**: Medium

- [ ] **TASK-0096** [Low] CleanupUnusedProfilesAsync performs synchronous recursive deletion on UI thread with potential startup freeze
  - **File**: `AccountManager.vb` (`CleanupUnusedProfilesAsync` method)
  - **Problem / Need**: Method has async signature, but when called directly or executing synchronous `Directory.Delete(..., True)` on the main thread during startup, it can freeze the UI when dealing with large Chromium profiles (thousands of cache, LevelDB, and blob files) or temporarily locked files.
  - **Fix to Implement**:
    - Run full enumeration and recursive deletion inside `Task.Run` off the UI thread.
    - Use a retry helper with progressive backoff (e.g. `DeleteDirectoryWithRetryAsync`, similar to TODO #37 / #72) to handle temporary locks without blocking main window responsiveness.
  - **Acceptance Test**: Create orphan profiles with thousands of files in profiles folder; on startup, UI must remain immediately responsive and freeze-free during background cleanup.
  - **Priority**: P3 (Low severity) | **Impact**: Low-Medium | **Effort**: Low

- [ ] **TASK-0098** [Low] Missing validity checks and null-safety in UpdateAccountNameAsync and RemoveAccountAsync
  - **File**: `AccountManager.vb` (`UpdateAccountNameAsync`, `RemoveAccountAsync` methods)
  - **Problem / Need**:
    1. `UpdateAccountNameAsync` accepted empty strings, whitespace-only strings, or duplicate names already in use by other accounts, degrading tab readability.
    2. `RemoveAccountAsync` accessed `_currentAccount.Id` without prior `_currentAccount IsNot Nothing` check, risking `NullReferenceException` if current selection was in transition or uninitialized.
  - **Fix to Implement**:
    - In `UpdateAccountNameAsync`, validate `newName`: discard or reject empty/whitespace strings (`String.IsNullOrWhiteSpace`) and names already present in `_accounts` collection.
    - In `RemoveAccountAsync`, add null-safety check on `_currentAccount` before accessing `Id` property.
  - **Acceptance Test**: Verify attempts to rename an account with empty or duplicate name are ignored, and removing an account with null `_currentAccount` does not throw exceptions.
  - **Priority**: P3 (Low severity) | **Impact**: Low | **Effort**: Low



## Bulk Messaging & Translation

- [ ] **TASK-0084** [Medium] CSV parser breaks multiline messages and may pick the wrong separator
  - **Files**: `ExcelContactService.vb`, approx lines 122–131 and 200–238
  - **Problem / Need**: The file is read by physical lines, and each line is parsed independently. A quoted field containing a newline is therefore split. Separator autodetection counts commas and semicolons inside quoted text. In isolated tests, a contact with a 2-line message creates an extra row; a comma-separated CSV is parsed as semicolon-delimited when the message contains enough semicolons.
  - **Fix to Implement**: Use a parser that reads complete records while preserving quote state across lines. Detect separators outside quoted fields and allow explicit user selection when ambiguous (RFC 4180 compliant).
  - **Acceptance Test**: Multiline messages, escaped quotes, and punctuation must preserve contact count and message content unchanged.
  - **Priority**: P2 (subsequent phase) | **Impact**: Medium | **Effort**: Low-Medium

- [ ] **TASK-0085** [Medium] Batch translation can misalign translated text to wrong message
  - **Files**: `Localization.vb`, approx lines 364–430, `TranslateBatchAsync()` method
  - **Problem / Need**: Messages are joined with a textual separator `###` and subsequently split on the same separator. There is no verification that the returned count of parts matches the input message count. Mismatched associations are then stored in cache (e.g., message containing `###` shifts indices).
  - **Fix to Implement**: Prefer structured responses with item identifiers. While using textual separators, check for collisions and part counts; fall back to individual translations upon mismatch without caching ambiguous results.
  - **Acceptance Test**: Messages containing `###`, altered separators, and HTTP errors must not corrupt the cache.
  - **Priority**: P2 (subsequent phase) | **Impact**: Medium | **Effort**: Low-Medium



## OTA Updates & Lifecycle

- [ ] **TASK-0091** [High] Version marker written before robocopy, unique temp directory naming, and temporary leftovers cleanup
  - **File**: `UpdateChecker.vb` (`PerformUpdateFromGitHubAsync`, `WriteLocalVersionMarker` methods), `Application.xaml.vb`
  - **Problem / Need**:
    1. `WriteLocalVersionMarker` was invoked by VB code before the file copy completed: if robocopy failed or was aborted, `.app_version` still claimed the new version even though application binaries were not updated. Additionally, the updater already writes the marker upon successful copy, making the preliminary VB call redundant and harmful, with format discrepancies (e.g. `0.7.1` in VB vs `v0.7.1`).
    2. The update script was generated inside `tempDir`. If the ZIP archive contains `HidaChat.exe` at root, `sourceDir = tempDir`: robocopy then copies the script into the application installation folder.
    3. The extracted temporary folder `tempDir` (potentially hundreds of MBs) and temporary zip files were not reliably cleaned up after update completion, wasting disk space.
    4. Using a fixed temporary folder name (`%TEMP%\HidaChat_Update`) causes collisions and file lock conflicts if multiple users or portable instances update simultaneously.
  - **Fix to Implement**:
    - Remove `WriteLocalVersionMarker` call from VB flow, delegating marker writing solely to the updater only upon successful copy completion (`%RC% < 8`). Unify version format.
    - Generate unique session-specific temporary directories (e.g. `HidaChat_Update_{updateSessionId}`) to prevent collisions between concurrent instances or user accounts.
    - Place temporary runner scripts outside both `tempDir` and `sourceDir` (e.g. directly in `Path.GetTempPath()`), preventing robocopy from copying them into destination.
    - Ensure complete cleanup of extracted temporary folders (`rd /s /q "<tempDir>"`) and temporary zip files upon completion or failure.
  - **Acceptance Test**: Run an update procedure: verify that updater scripts are not present in final installation folder, that `tempDir` is completely deleted upon completion, that concurrent updates in separate folders use isolated temporary directories, and that `.app_version` marker is not written if copy fails.
  - **Priority**: P1 (High severity) | **Impact**: High | **Effort**: Low-Medium

- [ ] **TASK-0092** [High] Fragile update batch script (OEM/UTF-8 codepage, accented characters, special characters, parentheses, and root USB drive)
  - **File**: `UpdateChecker.vb` (batch generation in `PerformUpdateFromGitHubAsync`)
  - **Problem / Need**:
    1. The script is written via `File.WriteAllText` in UTF-8, but `cmd.exe` parses files in OEM codepage (e.g. CP437/CP850). If the path contains accented characters (e.g. Italian vowels `è`, `à`, `ò` in username or folder path), robocopy reads corrupted paths and fails.
    2. Special characters such as `%` or `!` in the path break `cmd.exe` variable parsing and delayed expansion.
    3. Inside `if (...)` blocks, commands like `echo ... {logFile}` with unquoted/unescaped paths containing closing parentheses `)` (e.g. `HidaChat (1)` or `Program Files (x86)`) prematurely terminate the `if` block, causing syntax errors.
    4. If the application is located at the root of a USB drive (e.g. `E:\HidaChat.exe`, a scenario explicitly supported for portable use), `installDir` resolves to `E:\`. In robocopy, `"E:\"` escapes the trailing double quote (`\"`), corrupting the arguments. Normalization using `installDir.TrimEnd("\""c) & "\."` is required.
    5. On robocopy failure, executing `exit /b 1` without `pause` causes the console to vanish instantly without letting the user see the error.
  - **Fix to Implement**:
    - Add `chcp 65001 >nul` at the beginning of the batch script (or write the batch file with OEM encoding `Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage)`) to support accented paths.
    - Properly escape or remove `echo` with `{logFile}` inside `if` blocks, and escape or avoid `%` / `!` expansion issues in path handling.
    - Wrap all paths in double quotes and normalize drive roots for robocopy with `installDir.TrimEnd("\""c) & "\."`.
    - On robocopy error (`%RC% GEQ 8`), insert a `pause` before exit to allow inspection of the log.
  - **Acceptance Test**: Perform update with installation paths containing accented characters (`C:\Users\è_test\HidaChat`), duplicate parentheses (`HidaChat (1)`), `%` symbols, and from USB root (`E:\`). Verify robocopy succeeds without syntax errors.
  - **Priority**: P1 (High severity) | **Impact**: High | **Effort**: Low-Medium

- [ ] **TASK-0093** [Medium] SHA-256 verification: false mismatches with multiple files, filename:hash format, and fail-open if checksum is missing
  - **File**: `UpdateChecker.vb` (`ExtractSha256FromText` method)
  - **Related to**: TASK-0040
  - **Problem / Need**:
    1. If the release lacks both an asset checksum and release notes hash, update currently proceeds ("Verified via HTTPS transport"). This fail-open behavior contradicts the verified integrity requirement of TASK-0040. Missing hash must be treated as an error (or explicit warning) rather than silently proceeding.
    2. In `ExtractSha256FromText`, the fallback searching for "any 64-hex string" picks the wrong hash when encountering multi-asset `SHA256SUMS` files or commit SHAs in release notes, causing false "Integrity check failed" errors.
    3. Checksum formats like `filename: hash` or `hash *filename` with relative `./` prefixes are not reliably parsed.
  - **Fix to Implement**:
    - Remove loose/permissive 64-hex fallback; only extract hash if explicitly paired with the target zip filename or if the text contains a single unambiguous hash.
    - Support standard checksum lines (`hash filename`, `hash *filename`, and `filename: hash`), stripping `./` and `*` prefixes.
    - Enforce strict Fail-Closed policy: if no valid checksum is found matching the downloaded zip, abort update with a clear error dialog instead of allowing unverified installation.
  - **Acceptance Test**: Test with a `SHA256SUMS` file containing multiple hashes, a `filename: hash` formatted file, and a release with no hash. Verify correct hash extraction and that missing checksum halts installation with an integrity alert.
  - **Priority**: P2 (Medium severity) | **Impact**: Medium-High | **Effort**: Low-Medium

- [ ] **TASK-0094** [Medium] Manual update check lacks user feedback on network errors or missing assets and startup latch bug
  - **File**: `UpdateChecker.vb` (`CheckForUpdatesAsync`, `FetchGitHubReleaseInfoAsync` methods)
  - **Problem / Need**:
    1. When user explicitly clicks "Check for Updates" (`force = True`), network connectivity errors, GitHub API 403 rate limits (60 unauthenticated requests/hr), or releases without zip assets only log to `Debug.WriteLine`. The user sees no feedback and the button appears non-functional.
    2. `_hasChecked = True` is set before executing the check: if a transient network failure occurs during application startup, update checking is latched and never retried until application restart.
  - **Fix to Implement**:
    - When `force = True`, display an explicit localized dialog for network exceptions, HTTP errors (403 rate limit, 404), or releases lacking zip assets.
    - Only set `_hasChecked = True` upon successful check completion, or reset `_hasChecked = False` on failure so startup checks can be retried.
  - **Acceptance Test**: Trigger manual check while offline or simulate GitHub 403 response: verify an informative MessageBox appears. Verify startup check retries if initial network connection was down.
  - **Priority**: P2 (Medium severity) | **Impact**: Medium | **Effort**: Low

- [ ] **TASK-0095** [Medium] Flawed pre-release/beta version comparison and remote release selection
  - **File**: `UpdateChecker.vb` (`ComparePrerelease`, `IsNewerVersion`, `FetchGitHubReleaseInfoAsync` methods)
  - **Problem / Need**:
    1. Lexicographical comparison `String.Compare("beta10", "beta9")` produces incorrect result `"beta10" < "beta9"` because '1' precedes '9', preventing users on `beta9` from ever seeing `beta10`.
    2. On beta channel, GitHub API endpoint returns a release list: code selected the first release by date, which may not represent highest semantic version during out-of-order releases or hotfixes.
  - **Fix to Implement**:
    - Implement SemVer 2.0-compliant parsing: extract numeric suffixes from pre-release tags, comparing digit segments numerically (`Integer.TryParse`) and alphanumeric segments lexicographically.
    - On beta channel, parse all API-returned releases containing attached ZIPs and select the one with highest actual semantic version rather than first chronological entry.
  - **Acceptance Test**: Validate that `v1.2.10-beta` is evaluated as newer than `v1.2.9-beta`. With an out-of-order GitHub release list, verify highest semantic version is selected.
  - **Priority**: P2 (Medium severity) | **Impact**: Medium | **Effort**: Medium

- [ ] **TASK-0099** [Low] MessageBox dialogs in UpdateChecker.vb hardcoded in Italian instead of multilingual (Localization.vb)
  - **Files**: `UpdateChecker.vb`, `Localization.vb`
  - **Problem / Need**: Several dialogs and warning/error messages in `UpdateChecker.vb` contained fixed Italian strings, ignoring user's chosen application language (EN, IT, FR, ES, DE).
  - **Fix to Implement**:
    - Review all calls to `ShowMessageBox` and `ShowMessageBoxConfirm` in `UpdateChecker.vb`.
    - Add and synchronize corresponding translation keys across all 5 official dictionaries in `Localization.vb` (`EnStrings`, `ItStrings`, `FrStrings`, `EsStrings`, `DeStrings`).
    - Use `loc.Get(...)` to retrieve localized titles and strings according to active language.
  - **Acceptance Test**: Switch application language to English or French; verify all `UpdateChecker.vb` popups appear in selected language.
  - **Priority**: P3 (Low severity) | **Impact**: Low | **Effort**: Low

- [ ] **TASK-0117** [High] Verification and enforcement of dedicated download client, timeout handling, and file streaming
  - **Files**: `UpdateChecker.vb`, `UpdateProgressDialog.xaml`, `UpdateProgressDialog.xaml.vb`
  - **Related to**: TASK-0088
  - **Problem / Need**:
    - If update ZIP download uses default `_httpClient` with a 15-second timeout, downloading large archives (dozens of MBs) over slow or high-latency connections triggers `TaskCanceledException` with a generic "Installation error" message.
    - Downloading via `GetByteArrayAsync` buffers the entire archive into RAM, increasing memory pressure on portable machines.
    - `UpdateProgressDialog` must be guaranteed to display active real-time download progress (MB read / total MB and percentage) rather than remaining unused or indeterminate.
  - **Fix to Implement**:
    - Verify and ensure dedicated `_downloadClient` with `Timeout = Timeout.InfiniteTimeSpan` guarded by a 10-minute `CancellationTokenSource` is strictly used for all update asset downloads.
    - Stream downloaded bytes directly from response stream to disk file (`File.Create(tempZipPath)`) using chunked buffers (80 KB) without buffering entire archive in RAM.
    - Guarantee `UpdateProgressDialog` is instantiated and updated on the UI dispatcher with real-time transfer progress.
  - **Acceptance Test**: Simulate slow connection (throttled download): verify download continues past 15 seconds without timing out, RAM usage remains constant, and `UpdateProgressDialog` displays live progress bar and downloaded MB.
  - **Priority**: P1 (High severity) | **Impact**: High | **Effort**: Low-Medium

- [ ] **TASK-0118** [High] Target-specific process termination by PID and orderly shutdown of tsnetd.exe child process during update
  - **Files**: `UpdateChecker.vb`, `TsnetManager.vb`, `MainWindow.xaml.vb`
  - **Problem / Need**:
    1. Using image-name process termination (`taskkill /f /im HidaChat.exe /t`) terminates all running instances of HidaChat on the system. In portable usage (e.g. a second instance running from a USB drive or different folder), unrelated instances are terminated after 10 seconds.
    2. The update script does not wait for `tsnetd.exe` (Tailscale daemon child process): if `tsnetd.exe` remains active, robocopy fails when trying to overwrite its executable binary in the installation folder.
  - **Fix to Implement**:
    - Pass the calling process PID (`Environment.ProcessId`) to the updater and wait specifically for that PID and its descendants to exit (`tasklist /fi "PID eq {pid}"` or `Process.GetProcessById`).
    - In `ForceExitForUpdateAsync`, explicitly await clean shutdown and termination of `tsnetd.exe` (`TsnetManager.ShutdownAsync()`), verifying its process has fully exited before handing off to file copy.
  - **Acceptance Test**: Run two independent portable copies of HidaChat simultaneously. Trigger update on one copy: verify only the updating instance exits and the second instance continues running undisturbed. Verify `tsnetd.exe` is shut down before robocopy replaces files.
  - **Priority**: P1 (High severity) | **Impact**: High | **Effort**: Low-Medium

- [ ] **TASK-0119** [High] Installation directory backup, automatic rollback, and application relaunch on update copy failure
  - **Files**: `UpdateChecker.vb`, `Application.xaml.vb`
  - **Problem / Need**:
    1. If file replacement (robocopy exit code >= 8 or IO error) fails halfway through, the script exits (`exit /b 1`), leaving the installation in a corrupted state with mixed old and new binaries, without backup or recovery.
    2. The console window closes immediately, leaving the user with an unbootable app and no visible explanation.
    3. Premature writing of `.app_version` leaves the version marker claiming the new version despite failed binary copy.
  - **Fix to Implement**:
    - Create a temporary safety backup of installation binaries (excluding user data directory `data/`) before starting file copy.
    - If robocopy or file copy fails: automatically restore original binaries from backup, delete backup, prompt a clear error dialog, and relaunch the previous working version of `HidaChat.exe`.
    - Ensure `.app_version` marker is written strictly after file copy succeeds and is verified.
  - **Acceptance Test**: Simulate robocopy failure during update (e.g. lock a target dll): verify updater detects failure, restores old binaries from backup, and relaunches previous working version with an informative error message.
  - **Priority**: P1 (High severity) | **Impact**: High | **Effort**: Medium

- [ ] **TASK-0120** [Low] Residual Directory.GetFiles and unused Directory.GetDirectories enumeration in tempDir
  - **File**: `UpdateChecker.vb`
  - **Related to**: TASK-0021
  - **Problem / Need**:
    - Task #21 was declared completed (`[x] TASK-0021`), but `Directory.GetFiles(tempDir, "HidaChat.exe", SearchOption.AllDirectories)` remains in the codebase, along with `Directory.GetDirectories(tempDir)` assigned to `subDirs` and never used.
    - Redundant directory enumeration remains in code contrary to the completed task description.
  - **Fix to Implement**:
    - Clean up redundant `GetDirectories` assignments and optimize search for `HidaChat.exe` in `tempDir` to direct child checking or single-level resolution.
  - **Acceptance Test**: Verify `UpdateChecker.vb` has no unused directory enumeration variables and compiles cleanly.
  - **Priority**: P3 (Low severity) | **Impact**: Low | **Effort**: Low

- [ ] **TASK-0121** [Low] Inaccurate permission error message suggesting protected system directory C:\Programmi\HidaChat
  - **Files**: `UpdateChecker.vb`, `Localization.vb`
  - **Problem / Need**:
    - When write permission check in installation directory fails, the error dialog advises moving the application to `C:\Programmi\HidaChat`. In standard Windows setups, `C:\Program Files` (`C:\Programmi`) requires administrator privileges and triggers the exact same write permission error.
    - Contradicts the 100% portable philosophy of HidaChat.
  - **Fix to Implement**:
    - Update localized strings across all 5 dictionaries in `Localization.vb` (`update_insufficient_permissions`) and fallback message in `UpdateChecker.vb`.
    - Suggest writable user-owned portable locations (e.g. `Documents`, `Desktop`, or USB drive) and explicitly warn against system protected paths like `C:\Program Files` / `C:\Programmi`.
  - **Acceptance Test**: Trigger permission warning (e.g. read-only folder): verify message suggests user directories and explicitly advises against `C:\Programmi`.
  - **Priority**: P3 (Low severity) | **Impact**: Low | **Effort**: Low

- [ ] **TASK-0122** [Low] Handle Process.Start failure of updater script/process after ForceExitForUpdate to prevent orphan zombie state
  - **File**: `UpdateChecker.vb`
  - **Problem / Need**:
    - If `Process.Start` of the update process or batch script fails (e.g. blocked by antivirus, security policy blocking executable launch from `%TEMP%`, or missing file) after `ForceExitForUpdateAsync` has already closed webviews and application resources, the app remains alive in a non-functional zombie state.
  - **Fix to Implement**:
    - Wrap `Process.Start` in a protected try/catch block before calling `Environment.Exit(0)`.
    - If starting updater fails, display an explanatory error dialog to the user, log the failure to `updates.log`, and cleanly terminate or offer application restart.
  - **Acceptance Test**: Simulate `Process.Start` failure (e.g. invalid executable path): verify a modal error dialog appears informing the user rather than leaving the app hung in a silent zombie state.
  - **Priority**: P3 (Low severity) | **Impact**: Low | **Effort**: Low



## UI, UX & Visual Polish

- [ ] **TASK-0046** [High] Privacy Mode / Anti-Snooping (Blur Mode / Screen Share Shield)
  - **Description**: Add a quick-activation privacy mode (button in title bar or `Alt+P` shortcut) that applies CSS blurring (`filter: blur(5px)`) over messages, recent sidebar previews, images/videos, and contact names.
  - **Interaction**: Blurred content is temporarily and smoothly revealed only when hovering with the mouse cursor (`:hover`).
  - **Utility**: Protects conversation confidentiality in office spaces, public places, or during screen sharing sessions (Teams, Zoom, Google Meet).
  - **Impact**: High | **Effort**: Low

- [ ] **TASK-0048** [Medium] Application Lock with PIN / Master Password (App Lock)
  - **Description**: Ability to secure HidaChat with a PIN or password on startup or after a configurable idle period (e.g., 5, 15, 30 minutes), in addition to the ability to lock the screen instantly with `Ctrl+L`.
  - **Implementation**: WPF modal overlay dimming the window and disabling WebView2 instances until the correct PIN is entered (stored as a secure hash in `settings.json`).
  - **Impact**: Medium-High | **Effort**: Medium

- [ ] **TASK-0049** [Medium] Quick Switcher & Command Palette (`Ctrl+K`)
  - **Description**: Pop-up search and quick navigation modal window (`Ctrl+K` or `Ctrl+P`) in VS Code / Ferdium style to:
    - Switch on the fly between active accounts by typing their name.
    - Open settings or switch language/theme via keyboard.
    - Quickly toggle privacy mode, translations, or reload tabs.
  - **Impact**: Medium | **Effort**: Medium

- [ ] **TASK-0097** [Low] Missing PropertyChanged notification for CanAddAccount in LoadAccountsAsync and CreateDefaultAccountAsync
  - **File**: `AccountManager.vb` (`LoadAccountsAsync`, `CreateDefaultAccountAsync` methods)
  - **Problem / Need**: Upon completion of loading from `settings.json` or default account creation, failure to promptly fire `NotifyPropertyChanged(NameOf(CanAddAccount))` could leave add-account button enabled in UI even when 3 accounts (default maximum) are already configured.
  - **Fix to Implement**: Ensure all completion paths in `LoadAccountsAsync` and `CreateDefaultAccountAsync` fire `NotifyPropertyChanged(NameOf(CanAddAccount))`, `NotifyPropertyChanged(NameOf(HasExcessAccounts))`, and `NotifyPropertyChanged(NameOf(ExcessAccountsCount))`.
  - **Acceptance Test**: Start application with 3 accounts already registered and verify "+" button is immediately disabled on startup.
  - **Priority**: P3 (Low severity) | **Impact**: Low | **Effort**: Low

- [ ] **TASK-0109** [Medium] In-app popup notifications (MessagePopup): chat/platform logo, sender name, and message preview
  - **Files**: `MessagePopup.xaml`, `MessagePopup.xaml.vb`, `AppAccounts.vb`, `Scripts/notification.js`
  - **Problem / Need**: When receiving a new message, in-app graphical popup notifications (`MessagePopup`) must provide comprehensive, instantly recognizable information at a glance:
    1. **Source Chat Logo**: display the logo/icon of the messaging platform to which the chat belongs (WhatsApp, Telegram, Discord, OpenClaw, Hermes) or the conversation avatar/icon.
    2. **Sender Name**: prominently display the name of the contact, user, or group that sent the message.
    3. **Message Body**: clearly show the text body or preview of the received message with proper formatting.
  - **Proposed Solution / Fix to Implement**:
    - Redesign `MessagePopup.xaml` layout to position the source platform/chat logo harmoniously alongside contact avatar.
    - Ensure in `AppAccounts.vb` and `Scripts/notification.js` that the notification event extracts and passes all necessary metadata to `MessagePopup` constructor (sender, actual message text, and origin chat indicator).
    - Verify text readability and proper ellipsis truncation for long messages.
  - **Acceptance Test**: Upon receiving a new message on any configured account (WhatsApp, Telegram, etc.), verify toast popup displays distinct chat/platform logo, sender name, and message text preview.
  - **Priority**: P2 (Medium severity) | **Impact**: Medium-High | **Effort**: Low

- [ ] **TASK-0110** [Medium] Contrast and color fix for "Translation Provider" dropdown in SettingsWindow
  - **Files**: `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`
  - **Problem / Need**: In Settings (`SettingsWindow`), the Translation Provider dropdown (`ComboTranslationProvider`) is unreadable. Unlike other dropdowns (e.g. Theme, Language, DND Mode, Max Accounts, Spellchecker, Hibernation Timeout, Disk Cache Limit), `ComboTranslationProvider` is not handled in `ApplyTheme()`, causing mismatched text and background contrast (e.g. light text on white background in Light Theme).
  - **Proposed Solution / Fix to Implement**:
    - Update `ApplyTheme()` in `SettingsWindow.xaml.vb` applying to `ComboTranslationProvider.Background` and `ComboTranslationProvider.Foreground` the same consistent logic and brushes (`fgBrush`, dark `#2a3942` or light `#ffffff` background) used for other `ComboBox` controls.
    - Verify inner `ComboBoxItem` elements (`CbiProviderGoogle`, `CbiProviderLibreTranslate`, `CbiProviderOllama`, `CbiProviderOpenAi`) inherit styles properly or receive `CustomComboBoxStyle` with text and hover colors matching the rest of settings.
  - **Acceptance Test**: Open Settings in both Dark and Light themes; verify "Translation Provider" dropdown options have consistent text color and full readability matching all other dropdown menus.
  - **Priority**: P2 (Medium severity) | **Impact**: Medium | **Effort**: Low

- [ ] **TASK-0111** [Medium] Color styling and readability of Tailscale screen / section
  - **Files**: `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `TailscaleConnectionErrorDialog.xaml`, `TailscaleConnectionErrorDialog.xaml.vb`
  - **Problem / Need**: The integrated Tailscale (`tsnet`) section in Settings and the connectivity error/status modal dialog (`TailscaleConnectionErrorDialog`) use low-visibility characters and hardcoded dark colors (`#202c33`, `#111b21`, `#8696a0`) that do not adapt to application theme. Consequently, explanatory text, authentication labels, and node status are difficult to read and visually inconsistent with the rest of the interface.
  - **Proposed Solution / Fix to Implement**:
    - In `SettingsWindow.xaml` and `SettingsWindow.xaml.vb`:
      - Dynamically adapt Tailscale section container in `ApplyTheme()` (background, borders, explanatory text, and AuthKey label) using standard settings block colors.
      - Improve contrast of status badge (`BadgeTsnetStatus`, `TxtTsnetStatus`) and `TxtTsnetAuthKey` field so characters remain crisp in both dark and light themes.
    - In `TailscaleConnectionErrorDialog.xaml` and `.vb`:
      - Refine color palette (window background, borders, secondary text `TxtHeader` and `TxtMessage`, action buttons) ensuring high contrast meeting accessibility standards and consistent styling with HidaChat UI.
  - **Acceptance Test**: Check Tailscale section in Settings and open Tailscale status/error dialog in both themes (dark and light): verify all text and labels are clearly visible and legible with harmonious, consistent colors.
  - **Priority**: P2 (Medium severity) | **Impact**: Medium | **Effort**: Low

- [ ] **TASK-0112** [Medium] Contrast and visibility of "HidaChat" title in About window (AboutWindow)
  - **Files**: `AboutWindow.xaml`, `AboutWindow.xaml.vb`
  - **Problem / Need**: In About window (`AboutWindow`), the main "HidaChat" title lacks visibility and sufficient contrast. In XAML layout, `<TextBlock Text="HidaChat" .../>` has a hardcoded light color (`#ffffff` or light gray) and lacks an `x:Name` identifier to be dynamically updated by `ApplyTheme()`, making it poorly readable and inconsistent when application theme changes.
  - **Proposed Solution / Fix to Implement**:
    - Assign explicit identifier (e.g. `x:Name="TxtAppTitle"`) to header `TextBlock` in `AboutWindow.xaml`.
    - In `AboutWindow.xaml.vb`, update `ApplyTheme()` to synchronize title `Foreground` property for both Dark Theme (bright white `#ffffff` or `#e9edef`) and Light Theme (high-contrast dark `#111b21`), ensuring crispness and full legibility.
  - **Acceptance Test**: Open About window in both themes (dark and light): verify "HidaChat" title is bold, crisp, and clearly legible on any background.
  - **Priority**: P2 (Medium severity) | **Impact**: Low-Medium | **Effort**: Low

- [ ] **TASK-0123** [Medium] Modern restyling and structured reorganization of Settings window (SettingsWindow)
  - **Files**: `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`
  - **Problem / Need**:
    - Currently, all application settings (Appearance & Theme, Language & Translation providers, Notifications & DND, Multi-Account management, Tailscale tsnet configuration, Custom CSS editor with code presets, Spellchecker, Network drive local staging, Tab hibernation, Chromium disk cache and storage optimization, Beta update channel, and DevTools) are dumped in a single unorganized, linear vertical scrolling list inside a 520x580 fixed dialog.
    - Lack of categorization makes navigation cumbersome and cluttered as features grow.
    - Misplaced controls: "Check Now" for updates is positioned arbitrarily inside the DevTools section rather than under Updates/About.
    - Incomplete/hardcoded localization: Several labels and buttons (e.g. "Usa nodo Tailscale integrato", "Pulisci Cache", "Compatto", "Svuota", "Applica CSS") are hardcoded in Italian in XAML without leveraging `Localization.vb`.
    - Dynamic translation panels (LibreTranslate, Ollama, OpenAI) cause jumpy scrolling and visual clutter in the main scroll area.
    - Cramped account management view where credentials, server URLs, and connection test buttons are squeezed into narrow inline cards.
  - **Specifications & Solution to Implement**:
    - **Categorized Navigation Architecture (Tabs or Modern Sidebar)**:
      - Replace the single long scroll stack with a clean, intuitive categorized layout (left sidebar or top tab bar):
        - **General**: App Theme (System, Dark, Light), Language selection, Notifications toggle, Do Not Disturb duration, Spellchecker language.
        - **Accounts**: Spacious account management cards with platform selector, name, server URL, auth tokens, Tailscale toggle, and test connection button.
        - **Translation**: Translation switches, provider selection (Google, LibreTranslate, Ollama, OpenAI), structured configuration panels, privacy consent, and connection test.
        - **Network & Tailscale**: Tailscale (tsnet) node status badge, AuthKey, login trigger, and network drive local staging settings.
        - **Custom CSS & Appearance**: Dedicated CSS editor with monospace font, presets (OLED, Compact, Font, Clear), and live apply button.
        - **Storage & Performance**: Inactive tab hibernation timeout, Chromium disk cache limit, live storage compaction, and cache cleaning actions.
        - **About & Updates**: App version information, update channel selection (Beta/Stable), check for updates trigger (relocated from DevTools), release notes link, and developer tools toggle.
    - **Modern Visual Polish & Card Layout**:
      - Encapsulate settings into distinct, cohesive visual cards with subtle borders, rounded corners, and clear section subtitles.
      - Ensure consistent typography, button heights, padding, and alignments.
    - **Full Localization Support**:
      - Extract all hardcoded strings into `Localization.vb` across all 5 supported languages (`EnStrings`, `ItStrings`, `FrStrings`, `EsStrings`, `DeStrings`).
    - **Dynamic Theme Compatibility & Sizing**:
      - Ensure seamless theme switching in `ApplyTheme()` across all navigation elements, cards, and input controls for both Dark and Light modes.
      - Expand window dimensions (e.g. Width ~680–720px, Height ~540–580px) to provide comfortable breathing room without nested scroll conflicts.
  - **Acceptance Test**: Open Settings in both Dark and Light themes; verify all setting categories are easily navigable via tabs/sidebar, all existing controls persist and function correctly (account CRUD, CSS editing, translation testing, cache optimization, updates check), no hardcoded strings remain, and language switching dynamically updates all setting tabs and labels across all 5 languages.
  - **Priority**: P2 (Medium severity) | **Impact**: High | **Effort**: Medium



## Platform Integrations

- [ ] **TASK-0051** [High] Extension to New Web Platforms (Microsoft Teams, Slack)
  - **Description**: Extend `AppAccounts` architecture to allow users to add tabs for **Microsoft Teams** (`https://teams.microsoft.com/v2/`) or **Slack** (`https://app.slack.com/client`) with dedicated vector icons and themes. (Note: Discord integration completed and verified in TASK-0115).
  - **Impact**: High | **Effort**: Medium

- [ ] **TASK-0113** [Low] Integration and native support for Microsoft Teams chat (Teams Web)
  - **Files**: `PlatformDefinition.vb`, `AppAccounts.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Scripts/notification.js`
  - **Problem / Need**: Allow users to add and manage **Microsoft Teams** accounts directly within HidaChat alongside WhatsApp, Telegram, and Discord for unified workplace communication.
  - **Specifications & Solution to Implement**:
    - **Platform Model (`PlatformDefinition.vb`)**:
      - Add `Teams` (or `MicrosoftTeams`) value to `ChatPlatform` enum.
      - Create `TeamsPlatformDefinition` class with default URL (`https://teams.microsoft.com/v2/`), distinctive indigo/purple brand color (`#505ac9` / `#6264a7`), and SVG vector path for Teams icon across tabs, popups, and badges.
    - **Isolation & Authentication (`AppAccounts.vb`)**:
      - Dedicated, isolated profile in `data/webview/` preserving Microsoft 365 / Entra ID login and corporate/personal sessions without cross-account contamination.
      - Handle SSO auth popups (`NewWindowRequested`) for seamless Microsoft OAuth login.
    - **Notifications & Unread Badges (`Scripts/notification.js`)**:
      - Intercept unread message and mention counters (monitoring page title `(N) Teams` or activity badge selectors) propagating to Windows Toast and in-app `MessagePopup`.
    - **User Interface (`SettingsWindow.xaml`)**:
      - Add "Microsoft Teams" option in account platform selection dropdown.
  - **Acceptance Test**: Add a new account selecting "Microsoft Teams" platform, log in to Teams Web, send/receive messages, and verify notification badges and popup with Teams icon appear properly.
  - **Priority**: P3 (Low severity) | **Impact**: Medium-High | **Effort**: Medium

- [ ] **TASK-0114** [Low] Integration and native support for Facebook Messenger chat (Messenger Web)
  - **Files**: `PlatformDefinition.vb`, `AppAccounts.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Scripts/notification.js`
  - **Problem / Need**: Add support for **Facebook Messenger** (`https://www.messenger.com/`) as an integrated chat platform in HidaChat, allowing users to communicate via Messenger within an isolated, portable WebView2 instance.
  - **Specifications & Solution to Implement**:
    - **Platform Model (`PlatformDefinition.vb`)**:
      - Add `Messenger` value to `ChatPlatform` enum.
      - Create `MessengerPlatformDefinition` class with default URL `https://www.messenger.com/`, distinctive blue/gradient color (`#0084ff` / `#00b2ff`), and official SVG vector icon (speech bubble with lightning bolt).
    - **Isolation & Authentication (`AppAccounts.vb`)**:
      - 100% isolated WebView2 profile for sessions independent from other Meta services (e.g. WhatsApp Web).
      - Secure cookie management and persistent storage in `data/webview/`.
    - **Notifications & Unread Badges (`Scripts/notification.js`)**:
      - Continuous tracking of unread messages (mutation observer on title `(N) Messenger` or DOM badges) with bridging to system notifications and custom in-app toasts.
    - **User Interface (`SettingsWindow.xaml`)**:
      - Add "Facebook Messenger" to selectable platforms during account creation/editing.
  - **Acceptance Test**: Create a Messenger account, log in to `messenger.com`, verify message reception, unread badge increments, and proper rendering of notifications and platform avatar/logo.
  - **Priority**: P3 (Low severity) | **Impact**: Medium-High | **Effort**: Medium

- [ ] **TASK-0116** [High] Suppress repetitive Tailscale error dialog on Cancel and display offline agent banner for Hermes/OpenClaw
  - **Files**: `AppAccounts.vb`, `TailscaleConnectionErrorDialog.xaml`, `TailscaleConnectionErrorDialog.xaml.vb`, `MainWindow.xaml.vb`, `Localization.vb`
  - **Problem / Need**: When Tailscale (`tsnet`) is running and connected but the remote assistant endpoint (Hermes Agent or OpenClaw Gateway) is unreachable or offline (e.g. service stopped, closed port, HTTP gateway timeout 502/504), the `TailscaleConnectionErrorDialog` modal dialog is shown. Clicking "Cancel" / "Close" (`BtnClose`) dismisses the dialog, but subsequent navigation events or background health check loops immediately re-trigger error diagnosis, creating an annoying, persistent modal popup loop. Additionally, the account tab is left on a blank page or generic browser error without an in-page indicator informing the user that the agent service is offline.
  - **Specifications & Solution to Implement**:
    - **Suppress Repeated Prompts on Cancel (`AppAccounts.vb`, `TailscaleConnectionErrorDialog.xaml.vb`)**:
      - Introduce a per-account suppression flag (e.g. `_suppressTailscaleErrorPrompt As Boolean`).
      - When the user cancels the error dialog (`dialog.RetryRequested` = False and `dialog.OpenSettingsRequested` = False), set the suppression flag to prevent further automatic popup prompts for that account until an explicit user action occurs.
      - Reset the suppression flag when the user manually requests a reload (Reload button in toolbar or F5/Ctrl+R) or updates the account URL in Settings.
    - **Inject In-Page "Agent Offline" Banner (`AppAccounts.vb`, `Localization.vb`)**:
      - When connection check fails and the user cancels the popup, display a styled in-page banner / overlay inside the Hermes or OpenClaw WebView2 tab:
        - Header: "Agent Offline / Unreachable" (multilingual across EN, IT, FR, ES, DE in `Localization.vb`).
        - Detail: Explains that Tailscale is connected, but the agent service is not responding at the configured server URL.
        - Interactive Actions: "Retry Connection" button (resets suppression and re-triggers connectivity diagnosis) and "Account Settings" button (opens settings to adjust the URL/token).
  - **Acceptance Test**: Set up a Hermes or OpenClaw account pointing to an unreachable endpoint over Tailscale. When `TailscaleConnectionErrorDialog` appears, click "Cancel": verify the dialog closes and does not re-appear on subsequent background checks, and verify that the WebView2 displays the "Agent Offline" banner with operational "Retry" and "Settings" buttons.
  - **Priority**: P1 (High severity) | **Impact**: High | **Effort**: Medium
