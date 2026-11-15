# TODO

[![English](https://img.shields.io/badge/Language-English-blue.svg)](TODO.md)
[![Italiano](https://img.shields.io/badge/Lingua-Italiano-green.svg)](TODO.it.md)

---

## Security, Sessions & Core Reliability

- [ ] **TASK-0080** [High] Stop and restart can overlap two bulk sender executions
  - **Files**: `BulkSenderWindow.xaml.vb`, approx lines 343–438; `BulkSenderEngine.vb`, methods `Cancel()` and `RunAsync()`
  - **Problem / Need**: Stop button requests cancellation and immediately re-enables Start without waiting for the previous execution to complete. The latter may still be inside an asynchronous call. Furthermore, an interrupted execution passes through the "Completed" summary, advancing the progress bar to 100%.
  - **Fix to Implement**: Introduce a "Stopping..." state and a `CancelAsync()` method that awaits the active task. The engine must prevent concurrent executions independently of the UI. Separate Completed, Cancelled, and Failed outcomes.
  - **Acceptance Test**: Press Stop during a JavaScript call and immediately attempt a new start. Two execution runs must not start on the same WebView; the summary must retain actual counts and indicate interruption.
  - **Priority**: P1 (to be fixed before next release) | **Impact**: High | **Effort**: Low-Medium

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

- [ ] **TASK-0089** [High] Potential loss of all sessions on deserialization error (AccountManager.LoadAccountsAsync)
  - **File**: `AccountManager.vb` (`LoadAccountsAsync` method)
  - **Problem / Need**: If deserialization of the `accounts` list fails (e.g. due to mismatched field types, corrupted JSON, manual user edits, or schema changes between versions), the `Catch` block falls back to calling `CreateDefaultAccountAsync()`. The latter invokes `SaveAccountsAsync(force:=True)`, overwriting the entire `settings.json` file with a single default account. On next restart or maintenance cycle, `CleanupUnusedProfilesAsync` deletes all `WV2Profile_*` directories no longer tracked in configuration, causing irreversible destruction of all login sessions for WhatsApp, Telegram, OpenClaw, and Hermes.
  - **Fix to Implement**:
    - In `LoadAccountsAsync` deserialization `Catch` block, immediately create a backup copy `settings.json.bak` before any other operation.
    - Set a safety flag `_loadFailed = True` at manager level.
    - Inhibit both forced saving (`SaveAccountsAsync`) and profile cleanup (`CleanupUnusedProfilesAsync`) when `_loadFailed` is active.
    - Display a warning/error dialog to the user reporting configuration file corruption, prompting manual recovery or correction rather than silently wiping accounts.
  - **Acceptance Test**: Introduce a syntax error or incompatible type in the `accounts` block of `settings.json`. On startup, original file must be saved to `settings.json.bak`, no disk profile must be deleted or overwritten, and an explicit warning dialog must appear.
  - **Priority**: P1 (High severity) | **Impact**: Critical | **Effort**: Medium

- [ ] **TASK-0090** [High] MigrateOrphanProfile deletes or overwrites a valid profile
  - **File**: `AccountManager.vb` (`MigrateOrphanProfile` method)
  - **Problem / Need**: If both `WV2Profile_` (orphan or legacy profile without specified ID) and `WV2Profile_{id}` associated with the first account coexist on the filesystem, a flawed or destructive migration logic risks deleting the valid first account profile to move the orphan on top of it, causing the loss of the user's active session.
  - **Fix to Implement**:
    - Inside `MigrateOrphanProfile`, preliminarily check for the existence of the target folder `WV2Profile_{id}` (and any `.previous` snapshots).
    - If the target folder already exists, abort migration immediately (`Continue For` / `Return`), preserving the existing valid profile intact.
    - Allow the orphan folder to be handled safely by non-destructive cleanup mechanisms or retained as a backup.
  - **Acceptance Test**: Manually create both `data/webview/WV2Profile_` and `data/webview/WV2Profile_{id}` with differing files. On app launch, contents of `WV2Profile_{id}` must remain untouched and not be replaced or deleted by the orphan.
  - **Priority**: P1 (High severity) | **Impact**: High | **Effort**: Low

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

- [ ] **TASK-0091** [High] Version marker written before robocopy and temporary batch script leftovers
  - **File**: `UpdateChecker.vb` (`PerformUpdateFromGitHubAsync`, `WriteLocalVersionMarker` methods)
  - **Problem / Need**:
    1. `WriteLocalVersionMarker` was invoked by VB code before the batch script completed file replacement: if the robocopy command failed or was aborted, `.app_version` still claimed the new version even though application binaries were not updated. Additionally, the batch script already rewrites the marker upon successful copy, making the preliminary VB call redundant and harmful, with format discrepancies (e.g. `0.7.1` in VB vs `v0.7.1` in batch).
    2. The `update.bat` script is created inside `tempDir`. If the ZIP archive contains `HidaChat.exe` at root, `sourceDir = tempDir`: robocopy then copies `update.bat` into the application installation folder.
    3. The extracted temporary folder `tempDir` (potentially hundreds of MBs) was not deleted upon update completion, leaving wasted disk space.
  - **Fix to Implement**:
    - Remove `WriteLocalVersionMarker` call from VB flow, delegating marker writing solely to the batch script (or updater process) only upon successful robocopy completion (`%RC% < 8`). Unify version format.
    - Place batch script in `Path.GetTempPath()` outside both `tempDir` and `sourceDir`, preventing robocopy from including it in target destination.
    - Add forced removal of extracted temporary directory (`rd /s /q "<tempDir>"`) and temporary zip file at the end of the script.
  - **Acceptance Test**: Run an update procedure: verify that `update.bat` is not present in final installation folder, that `tempDir` is completely deleted upon completion, and that `.app_version` marker is not written if robocopy fails.
  - **Priority**: P1 (High severity) | **Impact**: High | **Effort**: Low-Medium

- [ ] **TASK-0092** [Medium] Fragile update batch script (parentheses in paths, root USB drive, and error handling)
  - **File**: `UpdateChecker.vb` (batch generation in `PerformUpdateFromGitHubAsync`)
  - **Problem / Need**:
    1. Inside the `if %RC% GEQ 8 ( ... )` block, the command `echo ... {logFile}` did not wrap the path in double quotes. In a path containing parentheses such as `C:\Program Files (x86)\...`, the closing parenthesis `)` prematurely closed the `if` block, causing a cmd syntax error and script failure.
    2. If the application is located at the root of a USB drive (e.g. `E:\HidaChat.exe`, a scenario explicitly supported by README for portable use), `installDir` resolves to `E:\`. In robocopy, `"E:\"` is interpreted as an escaped quote (`\"`), breaking the command. Normalization using `installDir.TrimEnd("\""c) & "\."` is required.
    3. On robocopy failure, the script executed `exit /b 1` without a `pause` command: the console closed immediately without allowing the user to read the error, and the application was not restarted, leaving the user with no UI.
    4. Using `taskkill /f /im HidaChat.exe` forcefully killed all running HidaChat instances rather than verifying the PID of the calling process.
  - **Fix to Implement**:
    - Always wrap expanded paths in double quotes in batch echo commands (`echo ... "%logFile%"` or `"%LOG%"`).
    - Handle drive roots by normalizing the destination path for robocopy with `installDir.TrimEnd("\""c) & "\."`.
    - On batch error (`%RC% GEQ 8`), insert a `pause` before exit or re-launch the original application in emergency fallback mode to avoid leaving the user without an app.
    - Wait for orderly shutdown by checking the specific PID (`tasklist /fi "PID eq {Environment.ProcessId}"`) rather than using blanket forceful process termination.
  - **Acceptance Test**: Perform update with path containing parentheses (e.g. `C:\Program Files (x86)\HidaChat`) and from root drive (`E:\`). Verify robocopy succeeds and, on simulated error, that a readable message appears before exit.
  - **Priority**: P2 (Medium severity) | **Impact**: Medium-High | **Effort**: Low-Medium

- [ ] **TASK-0093** [Medium] SHA-256 verification: false mismatches with multiple files and fail-open if checksum is missing
  - **File**: `UpdateChecker.vb` (`ExtractSha256FromText` method)
  - **Problem / Need**: If the ZIP filename is not matched exactly in checksum text (e.g. listed as `./HidaChat.zip` or with path prefixes), a fallback grabbing the first 64-character hex string risks picking an irrelevant hash (e.g. hash of another archive in a multi-asset `SHA256SUMS` file or a commit SHA in release notes), blocking updates due to false "mismatches". Conversely, if no hash is found and strict fail-closed policy is not enforced, update risks proceeding without integrity verification.
  - **Fix to Implement**:
    - Normalize filename matching by stripping relative prefixes such as `./` or escape characters `*`.
    - If a `filename` is specified, ignore generic arbitrary fallback or accept it exclusively if the text contains a single unambiguous 64-character hash.
    - Strictly enforce Fail-Closed policy: if no valid and unique checksum is resolved for the downloaded file, abort update and notify user with a clear error message.
  - **Acceptance Test**: Provide a `SHA256SUMS` file containing hashes of multiple files and verify only the hash matching HidaChat's zip is extracted. Verify that missing checksum blocks update with alert without attempting unverified installation.
  - **Priority**: P2 (Medium severity) | **Impact**: Medium-High | **Effort**: Low-Medium

- [ ] **TASK-0094** [Medium] Manual update check lacks user feedback on network errors or missing assets
  - **File**: `UpdateChecker.vb` (`CheckForUpdatesAsync`, `FetchGitHubReleaseInfoAsync` methods)
  - **Problem / Need**: When user explicitly clicks "Check for Updates" (`force = True`), if GitHub API responds with an error (403 rate limit, offline, unreachable endpoint) or if the remote release lacks an attached ZIP archive, execution terminated with a simple `Debug.WriteLine`. User received zero visual feedback and the button appeared broken.
  - **Fix to Implement**:
    - When `force = True`, always display an explicit dialog across three critical scenarios:
      1. Network connectivity failure or exception;
      2. Non-success HTTP response from GitHub API (including 403 rate limiting and 404);
      3. New version found but lacks a valid ZIP update asset.
  - **Acceptance Test**: Trigger manual check while disconnected or simulate 403 GitHub response: an informative MessageBox with error explanation must appear instead of silent termination.
  - **Priority**: P2 (Medium severity) | **Impact**: Medium | **Effort**: Low

- [ ] **TASK-0095** [Medium] Flawed pre-release/beta version comparison and remote release selection
  - **File**: `UpdateChecker.vb` (`ComparePrerelease`, `IsNewerVersion`, `FetchGitHubReleaseInfoAsync` methods)
  - **Problem / Need**:
    1. Lexicographical comparison `String.Compare("beta10", "beta2")` produces incorrect result `beta10 < beta2` because '1' precedes '2', preventing proper detection of multi-digit beta releases.
    2. On beta channel, GitHub API endpoint returns a release list: code selected the first release by date, which may not represent highest semantic version during out-of-order releases or hotfixes.
  - **Fix to Implement**:
    - Implement SemVer 2.0-compliant parsing: split alphanumeric and numeric tokens in pre-release tags, comparing digit-only segments numerically (`Integer.TryParse`) and alphanumeric segments lexicographically.
    - On beta channel, parse all API-returned releases containing attached ZIPs and select the one with highest actual semantic version rather than first chronological entry.
  - **Acceptance Test**: Validate that `v1.2.10-beta` is evaluated as newer than `v1.2.2-beta`. With an out-of-order GitHub release list, verify highest semantic version is selected.
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
