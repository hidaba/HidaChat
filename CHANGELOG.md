# Changelog

[![English](https://img.shields.io/badge/Language-English-blue.svg)](CHANGELOG.md)
[![Italiano](https://img.shields.io/badge/Lingua-Italiano-green.svg)](CHANGELOG.it.md)

---

## [1.5.0] - 2026-11-19

### Stable Release — Discord Integration, Session Hardening, and Resilient Storage Architecture
- **Discord Integration (Multi-Platform Messaging Expansion — TASK-0115)**:
  - Added official Discord Web platform support (`https://discord.com/app`) alongside WhatsApp, Telegram, OpenClaw, and Hermes Agent, expanding HidaChat to 5 core messaging ecosystems.
  - Multi-account isolation: dedicated WebView2 tabs with sandboxed storage, persistent cookies, and independent logins under `data/webview/`.
  - Comprehensive voice call and media protection in tab hibernation (`AppAccounts.vb`): active audio detection via `IsPlayingAudio` and `CoreWebView2.IsDocumentPlayingAudio` ensures background Discord tabs during voice calls, meetings, or streams are never suspended.
  - Granular hardware media permissions (`AppAccounts.vb`): secure handling for `CoreWebView2PermissionKind.Microphone` and `Camera` strictly restricted to authorized Discord domains.
  - In-app login and verification navigation (`PlatformDefinition.vb`): enabled Discord authentication paths (`/login`, `/register`, `/verify`, `/oauth2/`, `/popout/`, `/guild-discovery`, `/store`), preventing external browser dropouts.
  - Deep link resolution (`PlatformDefinition.vb`): native handler for `discord://` protocol scheme and invite links (`discord.gg`).
  - Unread badge tracking and toast alerts (`Scripts/notification.js`): accurate mention detection, parent/child badge deduplication, muted channel filtering, and native Windows Toast notifications.
  - Persistent storage API protection (`Scripts/session-health.js`): automated `navigator.storage.persist()` registration protecting Discord IndexedDB data from quota evictions.
- **Fail-Safe Settings Protection and Non-Destructive Deserialization (`AccountManager.vb`, `AccountLoadSafetyPolicy.vb` — TASK-0089)**:
  - Automatic emergency backup copy (`settings.json.bak`) generated immediately upon JSON deserialization failure before any fallback action.
  - Fail-safe state lock (`_loadFailed = True`) inhibiting both auto-save and manual saving to strictly prevent corrupted configurations from overwriting valid user data.
  - Protected profile persistence: completely inhibited profile folder cleanup and deletions during failure states, guaranteeing that existing sessions (WhatsApp, Telegram, Discord, OpenClaw, Hermes) are never lost.
  - User-facing modal notification dialog informing of configuration issues with instructions to check the backup file.
- **Safe Orphan Profile Reassociation and Collision Prevention (`AccountManager.vb`, `AccountLoadSafetyPolicy.vb` — TASK-0090)**:
  - Safety policy `CanSafelyMigrateOrphanProfile` verifies whether orphan directories (`WV2Profile_`) can be reassociated without destroying existing profiles or snapshots (`.previous`, `.bak`, `.migrated`, `_Snapshot`).
  - Strictly preserves existing valid accounts and aborts migration safely if collision risks exist, safeguarding user sessions.
- **Resilient Direct-to-Disk Streaming Updates (`UpdateChecker.vb` — TASK-0088)**:
  - Replaced memory-buffered downloads with direct-to-disk chunked streaming (80 KB buffer) with dedicated long-timeout client (10-minute timeout).
  - Direct file-stream cryptographic SHA-256 calculation (`SHA256.HashDataAsync`), minimizing RAM consumption during updates.
- **Platform Architecture & Test Suite Expansion (`Tests/SessionTests.vb`)**:
  - Full suite of 695 automated unit and regression assertions passing with 0 errors across platform routing, origin authorization, deep links, storage isolation, and fail-safe policies.

## [1.4.7-beta] - 2026-11-18

### Pre-release / Beta — Non-Destructive Orphan Profile Migration and Session Safeguarding
- **Safe Orphan Profile Reassociation & Overwrite Prevention (`AccountManager.vb`, `AccountLoadSafetyPolicy.vb` — TASK-0090)**:
  - In `AccountLoadSafetyPolicy`, implemented `CanSafelyMigrateOrphanProfile(sharedDir, accountId)` to verify whether an anonymous orphan profile folder (`WV2Profile_`) can safely be migrated without colliding with or destroying existing profiles.
  - Preliminarily checks for the existence of target directory `WV2Profile_{id}`, native profile directory `EBWebView/wv2profile_{id}`, alternative native naming formats, and all snapshot/backup variants (`.previous`, `.bak`, `.migrated`, `_Snapshot`).
  - In `AccountManager.MigrateOrphanProfile`, if a valid profile or snapshot already exists for the account, migration is immediately and strictly aborted (`Return`), logging `[ORPHAN_PRESERVED]` and keeping the user's active session completely intact and untouched.
  - The orphan directory is safely preserved as a backup and excluded from destructive cleanup.
  - In `AccountManager.CreateDefaultAccountAsync`, added existence check before moving anonymous orphan to prevent potential collisions.
- **Automated Regression Suite Expansion (`Tests/SessionTests.vb`)**:
  - Added comprehensive automated regression tests verifying that `CanSafelyMigrateOrphanProfile` rejects null/empty arguments, missing orphan folder, existing valid legacy directories, all snapshot variants (`.previous`, `.bak`, `.migrated`, `_Snapshot`), native directories, and native snapshots.
  - Verified full end-to-end acceptance test: when both `WV2Profile_` and `WV2Profile_{id}` coexist with differing files, migration is rejected, `WV2Profile_{id}` and its contents remain 100% untouched and preserved, and `WV2Profile_` remains preserved for safe inspection (totaling **695 automated regression assertions with 0 errors**).

## [1.4.6-beta] - 2026-11-16

### Pre-release / Beta — Non-Destructive Deserialization Failure Protection and Session Preservation
- **Immediate Settings Backup on Account Deserialization Failure (`AccountManager.vb`, `AccountLoadSafetyPolicy.vb` — TASK-0089)**:
  - In `AccountManager.LoadAccountsAsync`, protected the JSON deserialization `Catch` block by immediately creating a safety backup copy `settings.json.bak` before any fallback or account creation can occur.
- **Fail-Safe Mode & Save Inhibition (`AccountManager.vb`, `AccountLoadSafetyPolicy.vb` — TASK-0089)**:
  - Introduced safety flag `_loadFailed = True` and public property `LoadFailed` in `AccountManager`.
  - When `_loadFailed` is active, strictly inhibited both automatic and forced saving in `SaveAccountsAsync`, preventing fallback default accounts from overwriting `settings.json`.
- **Complete Profile Directory Preservation (`AccountManager.vb`, `AccountLoadSafetyPolicy.vb` — TASK-0089)**:
  - Inhibited all profile deletion and renaming routines (`CleanupUnusedProfilesAsync`, `MigrateOrphanProfile`, `MigrateLegacyProfilesToNativeProfiles`, `RemoveAccountAsync`, and orphan renaming in `CreateDefaultAccountAsync`) when `_loadFailed` is True, guaranteeing that existing WebView2 login sessions (WhatsApp, Telegram, Discord, OpenClaw, Hermes) are never wiped or destroyed.
- **Explicit Modal Warning Dialog (`AccountManager.vb`, `Localization.vb` — TASK-0089)**:
  - Implemented `NotifyAccountsCorruptedAndProtected`, prompting a localized modal warning dialog that reports the configuration issue, points to the safety backup file, and informs the user that saving and cleanup have been disabled to protect their data.
  - Added localized strings `accounts_corrupt_title` and `accounts_corrupt_msg` across all 5 supported languages (EN, IT, FR, ES, DE) in `Localization.vb`.
- **Automated Regression Suite Expansion (`Tests/SessionTests.vb`, `AccountLoadSafetyPolicy.vb`)**:
  - Added regression test suite verifying JSON syntax error rejection, incompatible property types rejection, empty array rejection, non-destructive backup file creation, and profile directory preservation under failure states (totaling **674 automated regression assertions with 0 errors**).

## [1.4.5-beta] - 2026-11-15

### Pre-release / Beta — Discord Hardening, Voice Call Hibernation Protection, and Notification Fixes
- **Active Voice Call & Media Protection in Tab Hibernation (`AppAccounts.vb` — TASK-0115)**:
  - Guarded `TryHibernateAsync()` with audio activity detection: checks both `IsPlayingAudio` and `CoreWebView2.IsDocumentPlayingAudio` to ensure background tabs in active voice channels, voice calls, or media playback are never suspended.
- **Secure Media Permission Handling (`AppAccounts.vb` — TASK-0115)**:
  - Extended `_permissionRequestedHandler` to explicitly handle `CoreWebView2PermissionKind.Microphone` and `CoreWebView2PermissionKind.Camera`. Grants media access strictly to authorized origins (`IsAuthorizedOrigin`) while denying untrusted or cross-origin requests.
- **Discord Authentication & Navigation In-App Support (`PlatformDefinition.vb`, `Tests/SessionTests.vb` — TASK-0115)**:
  - Expanded `IsAllowedNewWindowHost` for Discord to allow authentication and popout paths (`/login`, `/register`, `/verify`, `/oauth2/`, `/popout/`, `/guild-discovery`, `/store`), preventing in-app login and verification flows from being improperly kicked out to the external browser.
  - Enhanced `ResolveDeepLink` for `discord://` protocol scheme to support `login`, `register`, and `oauth2` routes alongside channels and invites.
- **JavaScript Security Hardening (`Scripts/notification.js`, `Scripts/translation.js` — TASK-0115)**:
  - Replaced loose `.includes('discord')` and `.includes('telegram')` substring hostname matching with strict exact and subdomain-suffix checks (`host === 'discord.com' || host.endsWith('.discord.com')`), eliminating spoofing risks from malicious domains containing platform keywords.
- **Discord Notification & Unread Badge Accuracy (`Scripts/notification.js` — TASK-0115)**:
  - Resolved badge double-counting in `scanDiscordUnread()` by filtering out container elements containing matched child badges.
  - Filtered out muted channels and servers from unread pill detection.
  - Improved chat title extraction from window titles by stripping trailing ` - Discord` or ` | Discord` branding.
- **Persistent Storage & Diagnostic Session Health for Discord (`Scripts/session-health.js` — TASK-0115)**:
  - Added `https://discord.com` to authorized origins in `session-health.js` to ensure `navigator.storage.persist()` is called on Discord sessions, protecting IndexedDB from quota eviction.
- **Automated Regression Suite Expansion (`Tests/SessionTests.vb`)**:
  - Added new regression unit tests covering Discord NewWindow authentication paths, deep-link resolution, origin authorization security, and storage trust policies (totaling **653 automated regression assertions with 0 errors**).

## [1.4.4-beta] - 2026-11-14

### Pre-release / Beta — Resilient OTA Streaming Download and RAM Footprint Optimization
- **Dedicated Infinite Timeout Download Client (`UpdateChecker.vb`, `Tests/SessionTests.vb` — TODO #88)**:
  - Eliminated the 15-second HTTP timeout on update archive downloads by introducing dedicated `_downloadClient` with `Timeout = Timeout.InfiniteTimeSpan`.
  - Guarded GitHub asset downloads with a 10-minute `CancellationTokenSource`, preventing premature `"A task was canceled"` aborts on slow, throttled, or high-latency network connections.
- **Direct-to-Disk Chunked Streaming Download (`UpdateChecker.vb` — TODO #88)**:
  - Replaced full-archive in-memory buffering (`GetByteArrayAsync`) with progressive streaming directly to a temporary disk file (`File.Create(tempZipPath)`) via `HttpCompletionOption.ResponseHeadersRead`.
  - Configured 80 KB chunked transfer buffer (`buffer(81919)`), ensuring constant, minimal RAM utilization throughout the download process regardless of archive size.
  - Provided real-time progress calculations, streaming downloaded megabytes and percentage completion directly to `UpdateProgressDialog`.
- **Stream-Based SHA-256 Cryptographic Verification (`UpdateChecker.vb`, `Tests/SessionTests.vb` — TODO #88)**:
  - Refactored `ComputeSha256Async` to read directly from the disk file stream using `SHA256.HashDataAsync(stream)` without loading archive bytes into RAM.
- **Automated Regression Test Suite Expansion (`Tests/SessionTests.vb`)**:
  - Added new regression assertions for streaming SHA-256 verification directly from stream, dedicated client infinite timeout configuration, and 80 KB buffer sizing (total of **640 automated assertions with 0 errors**).

## [1.4.3-beta] - 2026-11-12

### Pre-release / Beta — Network Profile Sync Resilience and Seamless Account Switching
- **Resilience and Local Staging Fallback in Account Switching (`AppAccounts.vb`, `NetworkProfileSync.vb`, `ColdProfileCopy.vb`)**:
  - Resolved the critical freeze and the error dialog popup (`Profile import failed. Existing data were not reset; initialization stopped.`) when switching tabs between Telegram and WhatsApp (or other accounts) while running from network paths or mapped drives.
  - Implemented seamless automatic fallback to local staging: if refreshing from Master on the network share fails (e.g. due to open files or temporary locks held by the Chromium runtime or the active browser process), HidaChat checks for valid local session data (`ColdProfileCopy.HasData`) and continues initialization without throwing exceptions or interrupting the user experience.
  - In `ColdProfileCopy.Publish`, introduced the `SafeMoveDirectory` mechanism with multiple retries and progressive backoff to overcome temporary file and folder locks during atomic profile rotations.
- **Fix for `.sync_state.json` Metadata Overwriting on SMB / Linux Shares (`NetworkProfileSync.vb`, `Tests/SessionTests.vb`)**:
  - Fixed sync state update failure: on Samba- or Linux-managed network shares, dotfiles automatically inherit the `Hidden` attribute, preventing `File.WriteAllText` from updating the file due to an `UnauthorizedAccessException`. `WriteSyncState` now proactively strips `Hidden` and `ReadOnly` attributes before rewriting.
  - Refined `ShouldRefreshStagingFromMaster` logic: if metadata indicates that local staging holds a session newer than or identical to the network session (`ExportedUtc`), the comparison immediately returns `False` without falling back to file-by-file differential checks that could trigger false positives on SMB timestamps.
- **Multilingual Error Notification Localization (`MainWindow.xaml.vb`, `Localization.vb`)**:
  - Replaced hardcoded error message strings during account switching with localized keys `account_switch_error` and `dialog_title_error`, synchronized across all 5 officially supported languages (English, Italian, French, Spanish, German).
- **Extended Automated Test Suite (`Tests/SessionTests.vb`)**:
  - Added new unit regression assertions to verify `ShouldRefreshStagingFromMaster` comparison with newer staging and resilient rewriting of `.sync_state.json` with hidden attributes (total of **634 assertions with 0 errors**).


## [1.4.2-beta] - 2026-11-06

### Pre-release / Beta — Official Discord Web Integration with Profile Isolation and Native Notifications
- **Discord Web Platform Support (`PlatformDefinition.vb`, `AppAccounts.vb`)**:
  - Added the `Discord` platform to the `ChatPlatform` enum and implemented the `DiscordPlatformDefinition` class registered in `PlatformRegistry`.
  - Configured official initial URL `https://discord.com/app`, signature theme color `#5865F2` (Discord Blurple), and standard Clyde SVG vector icon.
  - Strict IPC bridge origin authorization policy (`IsAuthorizedOrigin`): exclusively authorizes the HTTPS origin `https://discord.com` on standard port 443, explicitly rejecting insecure HTTP schemes, non-standard ports, userinfo credentials, CDN subdomains (`cdn.discordapp.com`), and domain spoofing attempts.
  - Intelligent deep-link and invite resolution (`ResolveDeepLink`): seamlessly resolves official `discord.gg/<code>` and `discordapp.com` invites to `https://discord.com/invite/<code>`, normalizing the `discord://` scheme to internal application routes.
  - Window navigation policy (`IsAllowedNewWindowHost`): Discord internal channel and invite links stay within the WebView2 tab, while external links clicked in chats safely open in the default system browser.
- **Profile Isolation, Persistent Sessions, and Storage (`SessionRecoveryPolicy.vb`, `AppAccounts.vb`)**:
  - Added `https://discord.com` to trusted persistent storage origins (`PersistentStorage`) in `SessionRecoveryPolicy.IsTrustedStorageOrigin`, protecting IndexedDB databases and Chromium sessions from disk quota eviction.
  - Separate, isolated WebView2 profiles in unique folders based on the account's internal ID, enabling concurrent independent sessions for multiple Discord accounts without conflicts or overwrites.
  - Guaranteed persistence during renaming, restarts, and tab switching.
- **Windows Desktop Notification Integration and Unread Detection (`Scripts/notification.js`, `AppAccounts.vb`, `MainWindow.xaml.vb`)**:
  - In `notification.js`, introduced dedicated `scanDiscordUnread` DOM scanner for instant detection of unread messages and mentions (numeric mention badges and unread pill indicators).
  - Intercepted `window.Notification` and `ServiceWorkerRegistration.showNotification` with proactive suppression of duplicate Chromium popups, routing via native Windows Toasts with secure account selection arguments (`accountId`).
  - Clicking the toast notification restores the window, immediately selects the target Discord account, and invokes the `window.onNotificationClicked(id)` callback to navigate directly to the conversation.
- **User Interface and Multilingual Localization (`MainWindow.xaml.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`)**:
  - Added "Add Discord Account" entry to the `+` button context menu on the main tab bar and in the Settings window.
  - Added `Discord` option in the platform dropdown selector in Account Settings.
  - Synchronously updated all 5 supported language dictionaries (`EnStrings`, `ItStrings`, `FrStrings`, `EsStrings`, `DeStrings`) in `Localization.vb`.
- **Bulk Sender Protection**:
  - Set `SupportsBulkSender` capability strictly to `False` for Discord at both the platform metadata level and in the engine (`BulkSenderEngine` and `BulkSenderWindow`), preventing automated or bulk sending attempts.
- **Extended Automated Test Suite (`Tests/SessionTests.vb`)**:
  - Added new unit regression assertions covering: Discord enum and registry compliance, authorized origin validation and rejection, Discord deep-link/invite resolution, NewWindow handling, persistent storage permissions, and Bulk Sender blocking.
- **Intelligent Multi-PC Network Share Profile Sync (`NetworkProfileSync.vb`, `Tests/SessionTests.vb` — TODO #109)**:
  - Resolved critical issue where secondary machines failed to update local staging: previously, if a host machine already had a staging folder `%LOCALAPPDATA%\HidaChat\NetworkProfiles` from earlier sessions, `SyncMasterToLocalStagingAsync` flatly rejected refreshing (`[LOCAL_PROFILE_PRESERVED]`), ignoring newer sessions saved on Master from another PC and causing login/disconnection screens or QR codes.
  - Introduced sync metadata tracking (`.sync_state.json`) with UTC export timestamps, machine name, and username.
  - Implemented `ShouldRefreshStagingFromMaster(master, staging)`: compares sync metadata and differential file timestamps between Master and local staging. When Master on the network share contains a session exported more recently than local staging, HidaChat performs an atomic cold refresh (`PublishAsync`), ensuring switching PCs on portable drives/network preserves active logins and sessions across all configured accounts.
  - Test suite expanded to **632 automated assertions with 0 errors**.

## [1.4.1-beta] - 2026-11-04

### Pre-release / Beta — Robust Bulk Sender with Concurrent Stopping, RFC 4180 CSV Parser, and Safe Batch Translation
- **Removed Forced Beta Channel Unchecking (`SettingsController.vb`, `SettingsMigration.vb`, `Tests/SessionTests.vb`)**:
  - Permanently removed the automatic startup override that set `useBetaChannel = False`. The user's beta channel selection is now respected and preserved without resets or overrides in `settings.json`.
- **Concurrency Control and Async Cancellation for Bulk Sender (`BulkSenderEngine.vb`, `BulkSenderWindow.xaml.vb`, `BulkSenderPolicy.vb` — TODO #80)**:
  - Introduced the "Stopping..." state (`IsStopping`) and asynchronous `CancelAsync()` method, waiting for running asynchronous operations to truly complete before re-enabling UI controls.
  - Implemented `BulkSenderExecutionCoordinator` and engine-level concurrency lock strictly preventing overlapping concurrent executions on the same WebView (`InvalidOperationException`).
  - Separated sending outcomes into explicit states (`IsCompleted`, `IsCancelled`, `IsFailed`) in the progress report `BulkSenderProgress`: cancellation no longer forces the progress bar to 100% or displays "Completed", preserving true counts of sent, failed, and remaining messages.
- **RFC 4180 Multiline CSV Parser and Intelligent Delimiter Detection (`BulkSenderPolicy.vb`, `ExcelContactService.vb` — TODO #84)**:
  - Implemented RFC 4180-compliant `ParseCsvRecords` parser that preserves multiline fields enclosed in double quotes and correctly handles escaped double quotes (`""`), avoiding fragmentation of messages with line breaks into phantom contacts.
  - Automatic delimiter detection (`DetectCsvDelimiter`) analyzes separators (`,`, `;`, `\t`) strictly outside quoted fields, preventing commas or semicolons within message bodies from skewing delimiter detection.
- **Collision Prevention and Misalignment Safeguard in Batch Translations (`TranslationProvider.vb`, `Localization.vb` — TODO #85)**:
  - Introduced `HasSeparatorCollision` check in `GoogleTranslationProvider`: if a phrase contains the `###` delimiter, text is translated atomically 1-to-1 rather than batch-merged to avoid index desynchronization.
  - In `Localization.TranslateBatchDetailedAsync`, verified exact match between returned translation count and requested text count: on discrepancy or failure, falls back to individual translation without poisoning the cache with erroneous mappings.
  - Localized across all 5 supported languages (EN, IT, FR, ES, DE) with new UI keys for stopping and cancellation states (`bulk_status_stopping`, `bulk_status_cancelled`, `bulk_title_cancelled`, `bulk_status_completed`, `bulk_status_failed`).
- **Extended Automated Test Suite (`Tests/SessionTests.vb`)**:
  - Added 21 new unit regression assertions covering concurrency coordinator, async stop, RFC 4180 multiline CSV parser, and translation collision checking (total of **593 automated assertions with 0 errors**).

## [1.4.0] - 2026-11-03

### Stable Release — Radical Disk Space Optimization and Network Sync Acceleration
- **Resource-Saving Architecture and Disk Space Optimization (`ProfileIsolationPolicy.vb`, `CacheCompactionPolicy.vb`, `StartupJunkCleanupPolicy.vb`, `NtfsCompressionPolicy.vb`, `AccountManager.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`)**:
  - **Shared Singleton `CoreWebView2Environment` and Native Profiles (`wv2profile_account_*`)**: Unified the Edge WebView2 runtime across all active tabs and accounts (WhatsApp, Telegram, OpenClaw, Hermes). Cookies, IndexedDB, and sessions remain 100% isolated, achieving 60% to 70% RAM savings and eliminating redundant Chromium processes.
  - **Automatic Cleanup of Obsolete Migration Backups**: Automatically removed redundant historical copies (`.migrated`, `.previous`, `_Snapshot`, `.bak`) once native profile integrity is verified, reclaiming up to 1.4 GB of disk space.
  - **Surgical Live Cache Compaction ("Optimize Storage" Button)**: Introduced live cleanup of `DiskCache` and `CacheStorage` without de-authentication or session loss. Credentials and IndexedDB databases remain intact and operational.
  - **Startup Junk Cleanup Routine (`StartupJunkCleanupPolicy`)**: Safe, automatic startup purging of Chromium crash dumps (`.dmp`), orphaned metrics (`.pma`), shader caches, and diagnostic logs, with an inviolable protective whitelist for WhatsApp and Signal LevelDB databases, cookies, and the DPAPI `Local State` encryption key file.
  - **NTFS Transparent Compression**: Enabled NTFS filesystem-level compression for the portable data directory (`data/webview`), cutting disk usage by 40-50% with zero read/write performance degradation.
- **Extreme SMB/Samba Network Disk Sync Acceleration (`ColdProfileCopy.vb`, `NetworkProfileSync.vb`, `AppLogger.vb`, `MainWindow.xaml.vb`)**:
  - **Radical Exclusion of Volatile Caches on Network Shares**: Rigorously filtered temporary cache folders during network path exports (SMB/UNC/Samba): transferred data drops by 97% (from ~550 MB to ~15 MB per account) and file count drops by 94% (from 2,200+ to ~120-160 essential files).
  - **Bounded Win32 `CopyFileEx` Parallel Copying**: Controlled parallel transfer (`MaxDegreeOfParallelism = 4`) leveraging the native Win32 API to preserve timestamps without separate round-trips for metadata.
  - **Elimination of Synchronous SMB Flushes and Atomic Rotation**: Removed synchronous `Flush(flushToDisk:=True)` to saturate network bandwidth, introducing atomic master profile rotation with an asynchronous transient trash folder (`.trash-*`), eliminating "Access is denied" errors and lock conflicts on SMB servers.
  - **Parallel Multi-Account Sync**: Concurrent synchronization using `Task.WhenAll` with per-account semaphores: shutdown save duration drops from over 120 seconds to under 2 seconds total.
  - **Smart Differential Detection and Inactive Account Skip**: Instantaneous timestamp check on essential files (`HasModifications`): accounts unmodified during the session are skipped immediately (`[EXPORT_UNCHANGED]`), reducing save overhead to 0 ms and 0 bytes for idle tabs.
  - **Asynchronous Queue and Batched Writes for Logging (`AppLogger.vb`)**: Diagnostic messages are queued in a non-blocking thread-safe buffer and flushed in batched atomic writes, eliminating dozens of synchronous network calls to Samba shares.
  - **Parallel Tsnet Shutdown**: Asynchronous shutdown of the Tailscale daemon concurrent with closing WebView2 instances, eliminating lingering socket latencies.
- **Enhanced Bulk Sender and Responsive UI (`BulkSenderPolicy.vb`, `BulkSenderEngine.vb`, `AboutWindow.xaml`)**:
  - **Draft Conflict Detection and Real Send Confirmation**: Protection against accidental overwriting of pre-existing drafts in WhatsApp Web and Telegram Web, coupled with observable input clearance verification (`CONFIRMED_SENT`) to eliminate false positives.
  - **Crisp Icon Rendering and About Window Layout**: High-resolution icon in AboutWindow with text wrapping across all 5 official languages (EN, IT, FR, ES, DE).
- **Automatic Beta Channel Reset for Stable Release (`SettingsMigration.vb`, `SettingsController.vb`, `Tests/SessionTests.vb`)**:
  - Automatically reset `useBetaChannel = False` to align existing clients with the official stable channel for the 1.4.0 release.
  - Extended automated regression test suite (606 automated assertions with 0 errors).

## [1.3.12-beta] - 2026-11-01

### Pre-release / Beta — Bounded Parallel Copy, Batched Async Logging, and Inactive Account Skip on Shutdown
- **Advanced Network Save and Shutdown Optimizations for Samba/SMB (`ColdProfileCopy.vb`, `AppLogger.vb`, `NetworkProfileSync.vb`, `MainWindow.xaml.vb`, `Tests/SessionTests.vb` — TODO #108)**:
  - **Bounded Parallel Copy with Win32 `CopyFileEx` (`ColdProfileCopy.vb`)**: Files within each account sync are no longer copied purely sequentially, but with controlled concurrency (`Parallel.ForEach` with `MaxDegreeOfParallelism = 4`). Replacing managed streams (`FileStream` + buffer) with `File.Copy(source, dest, overwrite:=True)` delegates directly to the Win32 `CopyFileEx` API, natively preserving timestamps (`LastWriteTimeUtc`) and eliminating redundant metadata I/O round-trips. Over SMB connections to Samba, per-file overhead is effectively eliminated.
  - **Asynchronous Queue and Batched Writes for Logging (`AppLogger.vb`)**: Completely redesigned the logging engine to eliminate Samba/SMB bottlenecks. Diagnostic messages (`LogSync`, `Log`, `LogAccount`, etc.) are queued non-blocking in memory via `ConcurrentQueue(Of LogEntry)`. Disk flushing (`FlushNow` or 250ms timer) batches all pending entries into a single atomic write via `File.AppendAllText`, reducing 50+ synchronous network round-trips to a single cumulative I/O call.
  - **Smart Differential Detection and Inactive Account Skip (`ColdProfileCopy.vb`, `NetworkProfileSync.vb`)**: Introduced `ColdProfileCopy.HasModifications(source, target)`. Prior to exporting to a network share, HidaChat checks locally on `C:` whether essential files have changed relative to the master baseline (`target.previous` / `target`). If an account recorded no new activity or messages, sync is skipped immediately (`[EXPORT_UNCHANGED]`), reducing transfer to 0 ms and 0 bytes for idle tabs.
  - **Non-Blocking Trash Cleanup Directory (`ColdProfileCopy.vb`)**: Deletion of `.trash-*` directories resulting from atomic master profile rotation is delegated to a background task (`Task.Run`), decoupling UI and process shutdown from recursive SMB deletion latencies.
  - **Overlapped Tsnet Shutdown (`MainWindow.xaml.vb`)**: During shutdown (`ShutdownCoreAsync`), controlled termination of the Tailscale network daemon (`TsnetManager.Instance.ShutdownAsync()`) runs concurrently with closing WebView2 instances rather than in serial queue, completely masking the ~300ms socket release timeout.
  - **Extended Automated Test Suite (`Tests/SessionTests.vb`)**: Added 12 new unit regression assertions validating `HasModifications` differential detection (essential file inclusion, automatic exclusion of regenerable caches) and `AppLogger` async write queue (**601 automated assertions with 0 errors**).

## [1.3.11-beta] - 2026-11-01

### Pre-release / Beta — Network Profile Sync Optimization and Ultra-Fast Shutdown Saving
- **Network Profile Sync Acceleration (`ColdProfileCopy.vb`, `NetworkProfileSync.vb`, `AccountManager.vb`, `Tests/SessionTests.vb` — TODO #107)**:
  - **Advanced Filtering for Regenerable Cache Folders (`ColdProfileCopy.vb`)**: Fixed cache directory recognition in WebView2 native profiles (`EBWebView\wv2profile_account_*`). Expanded `ShouldSkip` with `SkipFolderNames` to exclude all temporary cache or shader folders upfront (`Cache`, `Code Cache`, `GPUCache`, `DawnCache`, `ShaderCache`, `Crashpad`, `CacheStorage`, `ScriptCache`, `AutofillAiModelCache`, `optimization_guide_hint_cache_store`, `Safe Browsing`, `Subresource Filter`). Transferred data per account dropped from over 2,200 files (~550 MB) to ~120-160 essential files (~15-17 MB), achieving a 94% file count reduction and 97% data volume reduction.
  - **Strict Session and Database Protection**: Verified and guaranteed integrity of `IndexedDB` (LevelDB databases for WhatsApp and Signal), `Local Storage`, `Session Storage`, `Sync Data`, `Network` (persistent cookies), `Preferences`, `Local State`, and worker registrations in `Service Worker/Database`. No cookies or logins are compromised.
  - **Removal of Synchronous SMB Flush (`output.Flush(flushToDisk:=True)`)**: Removed blocking synchronous flushes per file in `CopyTree`. On network shares and SMB mounts (e.g. `Y:\HidaChat`), continuous `FlushFileBuffers` calls caused network latencies of over 35 seconds per profile; operations now leverage the full system transmission buffer, cutting copy times from 36 seconds to under 1 second (872 ms on real-world network).
  - **Atomic Folder Rotation and "Access Denied" Resolution**: Reconfigured master profile rotation on network drives: `.previous` is moved instantaneously to a temporary `.trash-<guid>` folder via `Directory.Move` (5 ms), eliminating blocking recursive deletions before renaming that caused `Access is denied` errors on in-use network folders; the trash directory is cleaned up in the background.
  - **Parallel Multi-Account Synchronization (`NetworkProfileSync.vb`, `AccountManager.vb`)**: Replaced global lock `_syncLock` with per-account atomic semaphores (`ConcurrentDictionary(Of String, SemaphoreSlim)`). In `AccountManager.SyncAllProfilesToNetworkAsync()`, all configured accounts (WhatsApp, Telegram, OpenClaw, Hermes) sync in parallel using `Task.WhenAll`, cutting overall shutdown time from ~120 seconds to under 2 seconds.
  - **Extended Automated Test Suite (`Tests/SessionTests.vb`)**: Added 26 new unit regression assertions covering selective cache folder detection in native/legacy profiles, strict session file preservation, and atomic folder rotation (**589 automated assertions with 0 errors**).

## [1.3.10-beta] - 2026-11-01

### Pre-release / Beta — Crisp Icon Rendering and About Window Description Text-Wrapping Fix
- **About Window Layout and Rendering Improvements (`AboutWindow.xaml`, `AboutWindow.xaml.vb` — TODO #101)**:
  - **Crisp High-Resolution Icon**: Replaced header icon source with `images/icon.png`, integrating `RenderOptions.BitmapScalingMode="HighQuality"`, `SnapsToDevicePixels="True"`, and `UseLayoutRounding="True"` to eliminate blurriness and scaling artifacts.
  - **Responsive Constrained Layout**: Replaced the unconstrained horizontal `StackPanel` with a two-column `Grid` (`Auto` for icon, `*` for text), allowing application description (`TxtDescription`) to wrap properly (`TextWrapping="Wrap"`) without overflowing or clipping across all 5 supported languages (EN, IT, FR, ES, DE).
  - **UI Theme Polish**: Added dynamic Foreground color updates for `TxtDescription` in `AboutWindow.xaml.vb` (`#aebac1` in Dark Mode, `#54656f` in Light Mode).
- **Unused Variables Cleanup and Update Extraction (`UpdateChecker.vb` — TODO #100)**:
  - Confirmed complete removal of orphaned `subDirs` variable in update extraction flow in favor of recursive `Directory.GetFiles(...)`, ensuring a clean build with zero warnings and zero errors.

## [1.3.9-beta] - 2026-10-31

### Pre-release / Beta — Automatic Startup Cleanup Routine for Junk Files and Folders
- **Automatic Startup Junk Files and Folders Cleanup (`StartupJunkCleanupPolicy.vb`, `Application.xaml.vb`, `AccountManager.vb`, `Tests/SessionTests.vb` — TODO #105)**:
  - **Centralized `StartupJunkCleanupPolicy` Module**: Introduced dedicated `StartupJunkCleanupPolicy.vb` module to safely purge Chromium diagnostics directories, crash dumps, and orphaned shader caches before browser processes instantiate and lock files on disk.
  - **Strict Junk Folder Whitelist**: Defined `JunkFolderNames` constant with 100% regenerable and safe-to-delete directories (`Crashpad`, `GPUCache`, `GPUPersistentCache`, `ShaderCache`, `GrShaderCache`, `DawnCache`, `DawnGraphiteCache`, `DawnWebGPUCache`, `GraphiteDawnCache`, `Code Cache`, `Cache`, `BrowserMetrics`, `BrowserMetrics-spare`, `component_crx_cache`, `OptimizationGuidePredictionModels`, `Safe Browsing`, `Subresource Filter`).
  - **Orphaned File and Dump Removal**: Targeted detection and deletion of leftover files with extensions `.dmp` (crash dumps), `.pma` (Chromium metrics), and `.old` (pre-existing migration leftovers).
  - **Inviolable Protection for Session Data and Credentials**: Implemented `ProtectedFolderNames`, `ProtectedFileNames`, and the `IsProtectedDirectory` predicate: `IndexedDB` directories (including Signal/WhatsApp `.leveldb` databases and `.blob` media), `Local Storage`, `Session Storage`, `Sync Data`, `Network` (`Cookies` file), `Preferences`, and master DPAPI encryption file `Local State` are strictly protected and never touched or traversed, guaranteeing immunity from de-authentication or data loss.
  - **Non-Blocking Startup Execution**: Integrated `StartupJunkCleanupPolicy.CleanWebViewJunkFolders()` in `Application.xaml.vb` inside `OnStartup` (after folder lock acquisition and before UI initialization) and in `AccountManager.LoadAccountsAsync()` / `CleanupTransientCachesAsync()` on the shared `data/webview` folder.
  - **Extended Automated Test Suite (`Tests/SessionTests.vb`)**: Added 106 new unit regression assertions covering junk whitelist validation, session directory immunity, temporary extension detection, and file/folder tree simulations (**563 automated assertions with 0 errors**).

## [1.3.8-beta] - 2026-10-30

### Pre-release / Beta — Surgical Live WebView2 Cache Compaction (ClearBrowsingDataAsync) Without Account De-Authentication
- **Surgical Live WebView2 Cache Compaction (`CacheCompactionPolicy.vb`, `AppAccounts.vb`, `AccountManager.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`, `Tests/SessionTests.vb` — TODO #104)**:
  - **Centralized `CacheCompactionPolicy` Module**: Introduced dedicated `CacheCompactionPolicy.vb` module to define and validate safe, regenerable data bitmasks (`SafeRegenerableDataKinds = CoreWebView2BrowsingDataKinds.DiskCache Or CoreWebView2BrowsingDataKinds.CacheStorage`).
  - **Strict Isolation and Protection of User Credentials**: Implemented validation functions `IsSafeCompactionKind` and `IsSafeRegenerableDataKinds` with mandatory blocking (`DangerousSessionKindsMask`) of `Cookies`, `IndexedDb`, `LocalStorage`, `AllDomStorage`, `PasswordAutosave`, `GeneralAutofill`, and `Settings`. During live compaction, active sessions and credentials for WhatsApp Web, Telegram Web, OpenClaw, and Hermes remain intact and functional without requiring restarts or re-authentication (no new QR code scans).
  - **Asynchronous Compaction on Active Native Profiles**: Added `CompactWebViewProfileAsync(webView)` and `CompactProfileAsync(profile)` in `CacheCompactionPolicy.vb`, `CompactWebViewProfileAsync()` in `AppAccounts.vb`, and `CompactAllWebViewsProfileAsync()` in `AccountManager.vb`, enabling runtime purging of HTTP cache and Service Worker storage (`CacheStorage`) across all open accounts.
  - **Complete Service Worker Purge in `ClearBrowsingCacheAsync`**: Updated `ClearBrowsingCacheAsync` in `AppAccounts.vb` to include `CacheStorage` alongside `DiskCache`, ensuring that smart session-preserving cleanup (`SmartCleanCachePreservingLoginAsync`) also removes assets accumulated by Service Workers.
  - **UI and "Optimize Storage" Button**: Added dedicated button `BtnOptimizeStorage` under "Cache & Storage" in Settings (`SettingsWindow.xaml` and `SettingsWindow.xaml.vb`) with explanatory hint and immediate visual feedback via informative MessageBox.
  - **Full Multilingual Localization (5 Official Languages)**: Added 4 new translation keys (`optimize_storage_btn`, `optimize_storage_hint`, `optimize_storage_success_title`, `optimize_storage_success_msg`) synchronized across all 5 official dictionaries in `Localization.vb` (English, Italian, French, Spanish, German).
  - **Extended Automated Test Suite (`Tests/SessionTests.vb`)**: Added 46 new unit regression assertions covering safe bitmask validation, sensitive data rejection, regenerable cache folder scanning (Cache, Code Cache, Service Worker/CacheStorage), byte calculations, and `InvariantCulture`-compliant size formatting (**457 automated assertions with 0 errors**).

## [1.3.7-beta] - 2026-10-29

### Pre-release / Beta — Automatic Migration Backup Purge for Disk Space Recovery and OpenClaw Proxy Stability
- **Automatic Migration Backup Purge and Disk Space Recovery (`AccountManager.vb`, `ProfileIsolationPolicy.vb`, `Tests/SessionTests.vb` — TODO #103)**:
  - **Identification and Removal of Duplicate Historical Copies**: Resolved disk space inflation observed after migration to unified native profiles in `EBWebView/`. Introduced `CanSafelyPurgeMigrationBackups()` and `GetObsoleteMigrationBackupDirectories()` in `ProfileIsolationPolicy.vb` and the `CleanupObsoleteMigrationBackups()` method in `AccountManager.vb`: once a native profile in `EBWebView\wv2profile_account_*` completes migration and is verified with consistent session data (IndexedDB / cookies), all external duplicate backups (`.migrated`, `.previous`, `_Snapshot`, `.bak`) are automatically removed, freeing up to ~1.4 GB of disk space (from 1.83 GB down to ~414 MB).
  - **Complete Native Transient Cache Cleanup**: In `AccountManager.CleanupTransientCachesAsync()`, extended periodic purging of Chromium diagnostic cache folders (Crashpad, GPUCache, ShaderCache, GPUPersistentCache) to the shared `EBWebView/` folder and individual native profile subfolders.
  - **Extended Automated Test Suite (`Tests/SessionTests.vb`)**: Added 16 new regression assertions validating safe identification of session data and safety conditions for migration backup purging (**411 automated assertions with 0 errors**).
- **OpenClaw Reverse Proxy Stability and Gateway Timeout Resolution (`tsnetd/main.go`, `TsnetManager.vb`)**:
  - **Deterministic Synchronization Channel Close (`tsReady`)**: Invoked `tsReadyOnce.Do(func() { close(tsReady) })` on both login completion and `tsServer.Up()` error, eliminating risks of indefinite proxy request hangs and white screens on localhost/tailnet.
  - **Instantaneous Proxy Startup**: Reduced initial preventive timeout to 2s and integrated `tsnetd` daemon standard error logging directly into application logs (`AppLogger.LogApp`) in `TsnetManager.vb`.

## [1.3.6-beta] - 2026-10-29

### Pre-release / Beta — WebView2 Native Profile Folder Alignment (WV2Profile_) and Automatic Session/Credential Restoration
- **Native Profile Folder Path Alignment (`ProfileIsolationPolicy.vb`, `AppAccounts.vb`, `AccountManager.vb`, `Tests/SessionTests.vb` — TODO #103)**:
  - **Edge WebView2 Native Folder Mapping (`WV2Profile_`)**: Fixed path misalignment where Chromium/Edge WebView2 internally generates and searches profile subdirectories with prefix `WV2Profile_` under `data/webview/EBWebView` rather than `Account_{id}`. Updated `ProfileIsolationPolicy.vb` (`GetNativeProfileFolderName`, `GetNativeProfileRelativePath`, `GetNativeProfileDirectory`) to accurately reflect the `EBWebView\wv2profile_account_{id}` path expected by the browser runtime.
  - **Non-Destructive Session Migration and Restoration**: In `AccountManager.MigrateLegacyProfilesToNativeProfiles()`, implemented prioritized auto-detection of consistent session data (IndexedDB, LocalStorage, and cookies) from legacy folders (`data/webview/WV2Profile_{id}` and `.migrated` / `.previous` backups) as well as un-prefixed folders created by earlier betas (`EBWebView\Account_{id}`). Real data is copied and atomically aligned to active `EBWebView\WV2Profile_account_{id}` directory, and obsolete directories are cleanly removed.
  - **DPAPI Master Key Synchronization (`os_crypt.encrypted_key`)**: Added `MigrateOrRestoreLocalStateEncryptionKey()` to synchronize and restore the master encryption key in `data/webview/EBWebView/Local State` from pre-existing profiles, ensuring transparent cookie decryption without logging out WhatsApp Web, Telegram Web, and active sessions.
  - **Trash Protection and Active Profile Recognition**: Updated `IsNativeAccountProfileDirectory()` and `ExtractAccountIdFromProfileDirectory()` to recognize `WV2Profile_` prefixes, preventing `CleanupUnusedProfilesAsync` from erroneously moving active profiles to trash.
  - **Automated Test Suite (`Tests/SessionTests.vb`)**: Updated and expanded regression assertions to validate `EBWebView\wv2profile_account_*` mapping, safe ID parsing, and account extraction (**395 automated assertions with 0 errors**).

## [1.3.5-beta] - 2026-10-29

### Pre-release / Beta — Shared CoreWebView2Environment with Native Profiles and Radical RAM Consumption Reduction
- **Shared WebView2 Architecture and Native Profiles (`ProfileIsolationPolicy.vb`, `AppAccounts.vb`, `AccountManager.vb`, `NetworkProfileSync.vb`, `StorageIntegrityService.vb`, `Tests/SessionTests.vb` — TODO #103)**:
  - **Centralized `ProfileIsolationPolicy` Module**: Introduced dedicated `ProfileIsolationPolicy.vb` module for compliant `ProfileName` construction and sanitization (max 64 chars, safe character whitelist `[a-zA-Z0-9_\-\.]`, anonymous ID and GUID handling), mapping relative and absolute profile paths on disk, dynamic resolution of `IndexedDB` and `Service Worker` folders, and Chromium system directory filters.
  - **Shared Singleton `CoreWebView2Environment`**: Replaced per-tab `CoreWebView2Environment` allocations (which multiplied dozens of system files, logs, and `msedgewebview2.exe` processes per account) with a single shared singleton instance bound to `data/webview`. All tabs share the same runtime infrastructure, achieving 60-70% RAM savings and drastically reducing loading times between tabs.
  - **Total Isolation via Native Profiles (`ProfileName`)**: Each tab is instantiated using `CoreWebView2ControllerOptions.ProfileName` (`Account_{id}`). Cookies, sessions, IndexedDB, LocalStorage, and permissions remain 100% strictly isolated inside dedicated subdirectories (`data/webview/EBWebView/Account_{id}`), preventing cross-account credential leakage between multiple WhatsApp, Telegram, OpenClaw, or Hermes accounts.
  - **Non-Destructive Live Auto-Migration of Pre-Existing Profiles**: On startup, `AccountManager.MigrateLegacyProfilesToNativeProfiles()` automatically detects profiles stored in legacy format (`data/webview/WV2Profile_{id}`) and atomically transfers the `Default` folder (IndexedDB, LocalStorage, cookies, and sessions) into the new native profile structure (`data/webview/EBWebView/Account_{id}`), preserving active sessions and renaming the source folder to `.migrated` to prevent session loss.
  - **Improved Structural Integrity and Deferred Cleanup**: Updated `StorageIntegrityService` and `NetworkProfileSync` to detect and cold-repair corrupted IndexedDB databases across both native and legacy paths. Account removal in `AccountManager.RemoveAccountAsync` invokes the native Chromium `Profile.Delete()` API and defers profile directory cleanup via trash (`_trash/`).
  - **Extended Automated Test Suite (`Tests/SessionTests.vb`)**: Added 58 new unit regression checks covering profile name sanitization, length boundaries, effective path resolution, IndexedDB/Service Worker hierarchy, and system directory whitelisting (**391 automated assertions with 0 errors**).

## [1.3.4-beta] - 2026-10-27

### Pre-release / Beta — Draft Handling and Observable Send Confirmation in Bulk Sender Engine
- **Bulk Sender Reliability and Integrity (`BulkSenderPolicy.vb`, `BulkSenderEngine.vb`, `Localization.vb`, `Tests/SessionTests.vb` — TODO #79)**:
  - **Centralized `BulkSenderPolicy` Module**: Introduced dedicated `BulkSenderPolicy.vb` module for deterministic send cycle validation, text normalization (`NormalizeText`), chat recipient verification (`MatchesRecipient`), draft conflict detection (`IsDraftConflict`), automation script generation for WhatsApp Web and Telegram Web, and secure JSON response parsing (`ParseScriptResult`).
  - **Draft Overwriting and Accidental Sending Prevention**: Fixed issue where pre-existing drafts in Telegram Web blocked bulk text insertion and caused accidental sending of user drafts. Both WhatsApp Web and Telegram Web input boxes are now inspected prior to action: if a conflicting pre-existing draft is detected, sending stops immediately without overwriting or dispatching the text (`DRAFT_CONFLICT`), warning the user.
  - **Observable Client Confirmation Against False Positives**: Removed premature marking as `SENT` immediately after simulated clicks or `Enter` keyboard events (which triggered false positives on ignored events, blocking popovers, or DOM delays). Transmission moves to `TRIGGERED` and waits for observable client confirmation (`CONFIRMED_SENT`), validating actual clearing of the input field and send button toggling into a microphone icon; if not accepted within the timeout, sending is marked as failed without declaring false success (`SEND_REJECTED`).
  - **Full Multilingual Localization**: Added 8 new localization keys synchronized across all 5 official dictionaries in `Localization.vb` (English, Italian, French, Spanish, German) for accurate reporting of unregistered phone numbers, users not found, detected drafts, and unaccepted sends.
  - **Automated Test Suite (`Tests/SessionTests.vb`)**: Added 76 unit regression assertions covering text normalization, conflict detection, recipient matching, observable confirmation, and response parsing (**333 automated assertions with 0 errors**).

## [1.3.3-beta] - 2026-10-26

### Pre-release / Beta — Graphical Progress Dialog for OTA Updates and Elimination of Batch Scripts
- **Graphical OTA Update Progress Dialog (`UpdateProgressDialog.xaml`, `UpdateProgressDialog.xaml.vb`, `UpdateChecker.vb`, `Application.xaml.vb`, `Localization.vb`, `Tests/SessionTests.vb` — TODO #87)**:
  - **Dedicated Modal Graphical Dialog (`UpdateProgressDialog`)**: Created a modern graphical window in dark WhatsApp theme (border `#00a884`, background `#111b21`, smooth drop shadow, and vector download icon) aligned with the app design system, eliminating black console windows and disconnected text popups.
  - **Reactive Dual-State ProgressBar with Real-Time Download Details**: Deterministic progress during GitHub download showing downloaded MB, total MB, and percentage (`x.x MB of y.y MB (z%)`); smooth indeterminate marquee during calculation and file copy phases.
  - **Complete Elimination of External Scripts (`.bat` / `.ps1`)**: Fully removed batch file generation and shell invocations (`cmd.exe`, `update.bat`, `robocopy`), eliminating antivirus/EDR false positives and path syntax vulnerabilities with parentheses or removable root drives.
  - **Compiled Native CLI Handoff (`--apply-update`)**: Update lifecycle is handled directly by the compiled .NET/WPF runtime (`HidaChat.exe --apply-update <pid> <sourceDir> <installDir> <version> <lang> <tempZip>`), featuring non-blocking wait for graceful main process termination, secure recursive file copy with progressive retries (10 attempts for I/O locks), and automatic application restart.
  - **Absolute Portability Protection (`data/`)**: Recursive copy strictly isolates and excludes the `data/` folder and all subfolders, ensuring WebView2 profiles, caches, settings, and active sessions for WhatsApp, Telegram, OpenClaw, and Hermes are never touched or overwritten.
  - **Asynchronous Temporary Artifact Cleanup (`--cleanup-update`)**: Upon launching the updated application, a deferred background task cleanly and silently removes ZIP archives and session directories in `%TEMP%`.
  - **Full Multilingual Localization**: Added 8 new localization keys synchronized across all 5 official dictionaries in `Localization.vb` (English, Italian, French, Spanish, German) with real-time descriptions for every phase: downloading, SHA-256 and Authenticode signature verification, extraction, graceful shutdown, application, and restart.
  - **Automated Test Suite (`Tests/SessionTests.vb`)**: Added 34 new assertions covering data folder exclusion in OTA copy, progress percentage calculation, boundary clamping, and `--apply-update` CLI argument formatting (total of 257 automated assertions with 0 errors).

## [1.3.2-beta] - 2026-10-24

### Pre-release / Beta — Utility Bar & Quick Audio / Device Management and NTFS Transparent Compression
- **Utility Bar & Quick Audio / Device Management (`AppAccounts.vb`, `MainWindow.xaml`, `MainWindow.xaml.vb`, `Localization.vb`, `Tests/SessionTests.vb` — TODO #45)**:
  - **Quick Mute/Unmute for Active Tab**: Added dedicated `BtnMuteActiveTab` button in title bar with instant bidirectional synchronization with `CoreWebView2.IsMuted`, dynamic icon (idle `#8696a0`, multimedia playback WhatsApp green `#00a884`, muted slashed magenta `#ec4899`), and reactive tooltip.
  - **Dedicated Keyboard Shortcuts**: Introduced `Ctrl+M` shortcut to instantly mute or unmute audio on the current tab, and `Ctrl+Shift+C` to copy the active tab's URL/chat link to clipboard.
  - **Interactive Tab Media Indicator (`TabMediaButton`)**: Introduced audio indicator on each account tab displaying `🔊` when the account plays voice notes, audio notifications, or video (`IsDocumentPlayingAudio`), and `🔇` when muted (`IsMuted`), with direct click action to toggle mute on the fly without switching tabs.
  - **Extended Tab Context Menu**: Added dynamic context action "Mute/Unmute tab audio" (`account_mute_tab` / `account_unmute_tab`) and "Copy chat link / URL" (`account_copy_link`) with subtle toast overlay feedback (`MessagePopup`).
  - **Full Multilingual Localization**: Added 8 new localized keys synchronized across all 5 official dictionaries in `Localization.vb` (English, Italian, French, Spanish, German).
- **NTFS Filesystem Transparent Compression (`NtfsCompressionPolicy.vb`, `Application.xaml.vb`, `AccountManager.vb`, `Tests/SessionTests.vb` — TODO #106)**:
  - **Centralized `NtfsCompressionPolicy` Module**: Introduced dedicated `NtfsCompressionPolicy.vb` module for non-blocking native NTFS compression via `compact.exe` (`/C /S /I /Q`).
  - **Data and WebView2 Profiles Compression**: Automatic background invocation on `data/` at application startup (`Application.xaml.vb`) and on `data/webview` in `AccountManager.LoadAccountsAsync()`, providing 40-50% disk space savings for LevelDB databases and caches without impacting startup time or data integrity.
  - **USB Removable Drives Protection**: The `/I` flag silently and safely handles FAT32/exFAT drives without crashes or errors.
  - **Automated Test Suite (`Tests/SessionTests.vb`)**: Added 16 new unit regression checks covering compact CLI validation, null/whitespace paths, muting, multimedia indicators, and URL resolution hierarchy (total of 223 automated assertions with 0 errors).

## [1.3.1-beta] - 2026-10-23

### Pre-release / Beta — Hard Limit Cache Restriction and Chromium Resource Optimization via AdditionalBrowserArguments
- **Disk Cache Limitation and WebView2 Resource Optimization (`BrowserArgumentsPolicy.vb`, `AppAccounts.vb`, `SettingsController.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Tests/SessionTests.vb` — TODO #102)**:
  - **Centralized `BrowserArgumentsPolicy` Module**: Introduced dedicated `BrowserArgumentsPolicy.vb` module for deterministic construction of Chromium command-line flags (`BuildAdditionalBrowserArguments`), centralizing policy application across all tabs on startup.
  - **HTTP Disk Cache Hard Limit**: Set default maximum cap to 50 MB (`--disk-cache-size=52428800`), configurable in Settings via `DiskCacheLimitMb` with a new "50 MB (Compact)" quick option and dynamic fallback to unlimited cache on user selection (0 MB).
  - **Strict Audio/Video Media Cache Cap**: Introduced fixed hard limit of 20 MB (`--media-cache-size=20971520`) for audio and video playback, preventing uncontrolled growth of temporary data in WhatsApp and Telegram profiles.
  - **Aggressive Cache Discard**: Enabled `--aggressive-cache-discard` flag to promptly release obsolete cache chunks and files.
  - **Inhibit GPU Shader Cache on Disk**: Integrated `--disable-gpu-shader-disk-cache` flag to prevent disk writing and cumulative accumulation of `GPUCache`, `GrShaderCache`, `ShaderCache`, `GraphiteDawnCache`, and `DawnCache` folders.
  - **Block Background Component Downloads and Telemetry**: Added `--disable-component-update` (prevents silent downloads of extensions and CRX modules) and `--disable-background-networking` (blocks unsolicited telemetry requests and Edge models).
  - **Disable Redundant Features**: Removed background downloads of remote models and optimization hints via `--disable-features=Translate,MediaRouter,OptimizationGuideModelDownloading,OptimizationHintsFetching` (with coordinated disabling of `Spellcheck` when inactive).
  - **Automated Test Suite (`Tests/SessionTests.vb`)**: Added 32 new unit regression checks covering startup switch validation, cache byte calculations, unlimited mode, and enabled/disabled features (total of 207 automated assertions with 0 errors).

## [1.3.0] - 2026-10-21

### Stable Release — Restored Stable Update Channel and Automatic Beta Channel Reset
- **Automatic Beta Channel Reset for Stable Release (`SettingsMigration.vb`, `SettingsController.vb`, `Constants.vb`, `Tests/SessionTests.vb`)**:
  - **Revert Existing Clients to Stable Channel**: For the 1.3.0 stable release, any client that had previously checked "Use beta update channel" (`useBetaChannel = True`) has the option automatically unchecked and reset to `False`.
  - **Atomic Configuration Migration (`SettingsMigration.vb`)**: Introduced dedicated `SettingsMigration.vb` module with `ApplyBetaChannelReset130()` method that:
    - Detects and converts `useBetaChannel` to `False` both in the in-memory controller and in `data/settings.json`;
    - Persists the permanent flag `betaReset130Done = True` so the reset executes only once for the 1.3.0 transition;
    - Preserves future intentional user choices, allowing manual re-enabling of the beta channel in Settings for subsequent development cycles without unwanted resets on restart.
  - **Immediate Alignment of OTA Update Checker (`UpdateChecker.vb`)**:
    - With the flag disabled, the update subsystem exclusively queries official stable releases on GitHub (`/releases/latest`) on startup, preventing accidental downloads of future pre-release builds.
  - **Automated Regression Test Suite (`Tests/SessionTests.vb`)**:
    - Added test procedure `TestBetaChannelResetMigration` with 19 new assertions covering `True` to `False` conversion, idempotence, `betaReset130Done` marker persistence, deserialized `JsonElement` handling, and preservation of intentional manual re-enabling (175 total assertions with 0 errors).

## [1.2.9-beta] - 2026-10-20

### Pre-release / Beta — Multi-Provider Translation Support (LibreTranslate, Ollama, OpenAI) and Mandatory Privacy Opt-In
- **Privacy Opt-In and Multi-Provider Translation Architecture (`TranslationProvider.vb`, `Localization.vb`, `SettingsController.vb`, `TranslationPrivacyConsentDialog.xaml`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `AppAccounts.vb`, `Scripts/translation.js`, `Tests/SessionTests.vb` — TODO #69)**:
  - **Typed Architecture and `ITranslationProvider` Interface**: Created `ITranslationProvider` interface with unified support for single translations (`TranslateSingleAsync`), batch translations (`TranslateBatchAsync`), and connectivity testing (`TestConnectionAsync`), integrated via the dynamic registry `TranslationProviderRegistry`:
    - `GoogleTranslationProvider`: Default cloud provider with robust chunking and automatic fallback to single elements upon split collisions or misalignments;
    - `LibreTranslateProvider`: Support for self-hosted / on-premise LibreTranslate backends (default `http://localhost:5000`) with single translation, native batch arrays, and optional API key authentication;
    - `OllamaTranslationProvider`: Support for local LLMs running on Ollama (default `http://localhost:11434`, configurable model such as `llama3.2`), optimized prompts, and automatic response sanitization (stripping quotes and markdown blocks);
    - `OpenAiTranslationProvider`: Seamless integration with LM Studio, LocalAI, vLLM, OpenAI API, and other OpenAI-compatible endpoints (default `http://localhost:1234/v1`, optional model and Bearer token).
  - **Mandatory Privacy Opt-In and Informative Modal Dialog (`TranslationPrivacyConsentDialog`)**:
    - Translation feature is strictly opt-in (`TranslationPrivacyConsent = False` by default); no text leaves the computer without prior authorization;
    - On first use of in-chat translation, a dark-themed modal dialog explains the difference between external cloud servers (Google) and local self-hosted providers (LibreTranslate, Ollama, LM Studio), offering options to authorize, cancel, or open Settings immediately;
    - If consent is declined, translation stops discreetly without transmitting any network packets.
  - **Resilient Error Handling and In-Chat Feedback**:
    - In case of unreachable or offline backends (e.g. stopped local container or service), the application does not crash or pollute the cache with empty translations or error messages;
    - The translation bubble displays a subtle italic error message in red tint (`#e57373`), informing the user of the provider status;
    - Settings includes an interactive "Test Connection" button with real-time diagnostics and response latency.
  - **Full Multilingual Localization (`Localization.vb`)**:
    - Synchronized 15 new keys for privacy notice, providers, network settings, and connection status across all 5 official dictionaries (English, Italian, French, Spanish, German).
  - **Automated Regression Tests (`Tests/SessionTests.vb`)**:
    - Added 33 new unit validation tests for provider resolution, default settings, Opt-In logic, and network exception handling (156 automated assertions, 0 failures).

## [1.2.8-beta] - 2026-10-19

### Pre-release / Beta — Strongly Typed ChatPlatform Abstraction and Extensible PlatformDefinition Model
- **Strongly Typed Platform Abstraction and Extensible Model (`PlatformDefinition.vb`, `AppAccounts.vb`, `AccountManager.vb`, `SettingsWindow.xaml.vb`, `MainWindow.xaml.vb`, `BulkSenderWindow.xaml.vb`, `BulkSenderEngine.vb`, `MessagePopup.xaml.vb`, `Tests/SessionTests.vb` — TODO #68)**:
  - **Typed `ChatPlatform` Enum**: Created formal `ChatPlatform` enum (`WhatsApp`, `Telegram`, `OpenClaw`, `Hermes`), superseding previous arbitrary string-based handling (`"WhatsApp"`, `"Telegram"`, `"OpenClaw"`, `"Hermes"`).
  - **`PlatformDefinition` Class Hierarchy**: Introduced abstract base class `PlatformDefinition` and concrete implementations (`WhatsAppPlatformDefinition`, `TelegramPlatformDefinition`, `OpenClawPlatformDefinition`, `HermesPlatformDefinition`) encapsulating and centralizing:
    - Metadata and network ports: serialized ID, DisplayName, default URLs (`DefaultUrl`), and TCP ports for local tsnet proxy (`DefaultPort`);
    - Visual elements and styling: hex theme colors (`ThemeColorHex`), frozen WPF brushes (`ThemeBrush`), and SVG vector icon paths (`IconSvgPath`);
    - Capabilities and flags: `IsAiGateway`, `RequiresLocalProxy`, `SupportsBulkSender`, `SupportsThemeScript`, `SupportsTranslation`;
    - Security and navigation policies: HTTPS authorized origin validation (`IsAuthorizedOrigin`), internal window allowlists (`IsAllowedNewWindowHost`), and seamless Telegram deep-link resolution (`ResolveDeepLink`);
    - Injected scripts: DocumentCreated bootstrap scripts (`GetInitScript`) and dark/light theme synchronization (`GetThemeScript`).
  - **Thread-Safe Central Registry `PlatformRegistry`**: Implemented `PlatformRegistry` for deterministic bidirectional resolution from enum and string (with support for case-insensitive keys, DisplayName, and safe fallback to WhatsApp), preparing architecture for effortless addition of future platforms.
  - **Architectural Refactoring and 100% Backward Compatibility**:
    - `AppAccounts.vb`: Removed flags and cascaded `If/ElseIf` blocks, delegating directly to `PlatformDefinition`. Preserved `Platform As String` property backed by `PlatformDefinition.Id` to maintain full backward compatibility with existing `settings.json` files and XAML bindings;
    - `AccountManager.vb`, `SettingsWindow.xaml.vb`, `MainWindow.xaml.vb`, `BulkSenderWindow.xaml.vb`, `BulkSenderEngine.vb`, and `MessagePopup.xaml.vb`: Removed hardcoded string checks in favor of typed registry methods and definition properties;
    - `Tests/SessionTests.vb`: Integrated dedicated unit regression tests (total of 123 automated assertions with 0 errors) covering enum, registry, string resolution and fallbacks, proxy ports, deep-links, and origin authorization.

## [1.2.7-beta] - 2026-10-18

### Pre-release / Beta — Port Filter Restriction and Strict Host/Scheme Validation for Localhost Credentials
- **WebView2 Filter Security and Localhost Credential Protection (`LocalAuthPolicy.vb`, `AppAccounts.vb`, `SettingsWindow.xaml.vb`, `Tests/SessionTests.vb` — TODO #78)**:
  - **Strict Validation Module `LocalAuthPolicy`**: Introduced `LocalAuthPolicy.vb` validation module for rigorous scheme, host, and port checks before injecting security tokens (`X-HidaChat-Local-Token` and Bearer `Authorization`).
  - **Exact TCP Port Filter Restriction (`BuildFilterPattern`)**: Eliminated generic wildcard filter `http://127.0.0.1:*/*` in favor of patterns strictly bound to the assigned port (`http://127.0.0.1:{port}/*`), preventing accidental interception of requests destined for other local services or open ports on `127.0.0.1`.
  - **Exact Scheme, Host, and Port Verification in Handler**: Added strict application checks in `WebResourceRequested` validating that requested URIs match scheme (HTTP/HTTPS), host (`127.0.0.1` or gateway hostname), and port designated for that specific account, rejecting mismatches, spoofing attempts, or subdomain attacks.
  - **Dynamic Filter Updates and Removal (`UpdateWebResourceAuthFilter`, `RemoveWebResourceAuthFilter`)**: Implemented clean and timely unregistration (`CoreWebView2.RemoveWebResourceRequestedFilter`) of active filters upon configuration changes (port, server URL, token, Tailscale toggle, or account closure/disposal), avoiding lingering handlers or orphaned memory filters.
  - **Automated Regression Tests (`SessionTests.vb`)**: Added 12 new unit validation tests verifying acceptance on authorized ports and immediate rejection on mismatched ports, unauthorized hosts, invalid schemes, and malformed URIs.

## [1.2.6-beta] - 2026-10-17

### Pre-release / Beta — Mandatory WebSocket Handshake Authentication and Upfront Origin Verification in tsnetd Proxy
- **tsnetd Local Proxy Security: Elimination of WebSocket Auth Bypass and Origin Protection (`tsnetd/main.go`, `tsnetd/main_test.go` — TODO #77)**:
  - **Eliminated WebSocket Bypass for Local Tokens (`checkLocalAuth`)**: Removed the authentication bypass that permitted any request carrying `Sec-WebSocket-Key` or `Upgrade: websocket` headers to bypass token verification and acquire the local secret via the `__htok` response cookie. All requests, both HTTP and WebSocket, must now present valid credentials (`X-HidaChat-Local-Token` header, `__htok` query parameter, or `__htok` cookie).
  - **Upfront Origin Verification and CSWSH Protection (`isAllowedOrigin`)**: Introduced strict `Origin` header validation prior to any forwarding or rewriting. Only authorized local hosts (`127.0.0.1:{port}`, `localhost:{port}`, `[::1]:{port}`) or the target Tailscale endpoint host are permitted. Requests with unauthorized Origins (including `null` and external websites) are rejected immediately with `403 Forbidden: Invalid Origin`, eliminating risks of Cross-Site WebSocket Hijacking (CSWSH) and cross-origin attacks from external browsers.
  - **Hardened Session Cookie Flags**: The `__htok` authentication cookie is now issued with `HttpOnly: true` and `SameSite: Lax` security flags, preventing access and token exfiltration from malicious scripts in web page contexts.
  - **Dedicated Unit Test Suite (`tsnetd/main_test.go`)**: Implemented automated tests validating failure on uncredentialed requests, blocking forged WebSocket handshakes, rejecting malicious Origins, and confirming end-to-end operation of authorized sessions.

## [1.2.5-beta] - 2026-10-15

### Pre-release / Beta — Hermes/OpenClaw White Screen Detection with Tailscale Connection Checks and IndexedDB Storage Recovery with ProgressBar
- **Hermes / OpenClaw Profile White Screen Detection and Tailscale Connection State Checks (`AppAccounts.vb`, `MainWindow.xaml.vb`, `TsnetManager.vb`, `TailscaleConnectionErrorDialog.xaml`, `TailscaleConnectionErrorDialog.xaml.vb` — TODO #75)**:
  - **Active Navigation Monitoring and DOM Diagnostics**: Monitored navigation for Hermes and OpenClaw profiles in `NavigationCompleted`, intercepting network errors (`IsSuccess = False`, DNS and connection failures) and executing post-load DOM introspection scripts to detect persistent blank pages (`empty_body`) or reverse proxy error responses (`502 Bad Gateway`, `504 Gateway Timeout`).
  - **Integrated Tailscale Connectivity Diagnostics (`CheckTailscaleConnectivityAsync`)**: Implemented multi-tier Tailscale connectivity checks:
    - Embedded tsnet node state checks (`NeedsLogin`, `Stopped`, or reverse proxy handshake with local token `X-HidaChat-Local-Token` to dedicated loopback port);
    - Detected availability of the Tailscale Windows client instance and TCP reachability tests to target endpoint;
    - Precise differentiation of failure causes (authentication required, local proxy inactive, remote node offline/powered down, or Tailscale client not running).
  - **Dedicated Informative Graphical Dialog (`TailscaleConnectionErrorDialog`)**: Created native dark-themed modal dialog with explicit failure explanations and interactive buttons for:
    - "Retry": reload tab with local proxy route refresh;
    - "Sign in to Tailscale": immediately open tsnet node login page;
    - "Settings": open application preferences;
    - "Close".
- **Startup Storage/IndexedDB Integrity Pre-Check and Recovery Dialog with ProgressBar (`StorageIntegrityService.vb`, `StorageRecoveryProgressDialog.xaml`, `StorageRecoveryProgressDialog.xaml.vb`, `AppAccounts.vb`, `MainWindow.xaml.vb` — TODO #76)**:
  - **Preventive Cold Check (`CheckIndexedDbCorruption`)**: Before WebView2 runtime initialization, checks `CURRENT` pointer integrity, existence and non-emptiness of `MANIFEST-*`, and `.indexeddb.blob` folder consistency, preventing the fatal `NotReadableError: Data lost due to missing file` exception before it causes a blank screen.
  - **Recovery ProgressBar Dialog (`StorageRecoveryProgressDialog`)**: Created modal graphical dialog with WhatsApp-styled indeterminate ProgressBar that informs users while corrupt databases are repaired and rebuilt.
  - **Selective and Non-Destructive Recovery (`PerformStorageRecoveryWithDialogAsync`)**: Cleanly purges corrupt IndexedDB databases and Service Worker cache while fully preserving `Local Storage`, session cookies, DPAPI tokens, and credentials. Integrated across cold startup, runtime `INDEXEDDB_CORRUPTED` events, and the "Repair Tab" tool.
- **Full Multilingual Localization (`Localization.vb`)**:
  - Synchronized 18 new localization keys for Tailscale dialogs (`tailscale_dialog_*`, `tailscale_error_*`, `tailscale_btn_*`) and storage recovery phases (`storage_recovery_*`) across all 5 official dictionaries (English, Italian, French, Spanish, German).

## [1.2.4-beta] - 2026-10-14

### Pre-release / Beta — Shutdown and Save Progress Dialog, Telegram/WhatsApp White Screen Resolution, and Tab Repair
- **Application Save and Shutdown Progress Dialog (`ShutdownProgressDialog.xaml`, `ShutdownProgressDialog.xaml.vb`, `MainWindow.xaml.vb` — TODO #74)**:
  - **Dedicated Shutdown Graphical Dialog**: Created non-intrusive `ShutdownProgressDialog` in native dark HidaChat/WhatsApp styling, staying topmost with animated indeterminate ProgressBar and dynamic messaging to inform users of ongoing I/O and persistence operations.
  - **Shutdown Stage Tracking**: On exit (`ExitApplication`), the main window hides immediately while showing detailed progress for each sub-stage:
    1. Saving account settings and preferences;
    2. Graceful shutdown of WebView2 processes;
    3. Atomic profile and session synchronization to SMB/UNC paths (if enabled);
    4. Shutting down `tsnetd` network proxy and releasing session/filesystem locks;
    5. Finalizing cleanup and releasing the application Mutex.
- **Telegram Web White Screen Resolution and IndexedDB Blob Corruption (`notification.js`, `AppAccounts.vb`)**:
  - **Advanced Diagnostics and Detection (`INDEXEDDB_CORRUPTED`)**: Intercepted in bootstrap script (`notification.js`) the critical Chromium exception `NotReadableError: Data lost due to missing file. Affected record should be considered irrecoverable`, triggered when `tt-data` blobs fail to persist to disk following crashes.
  - **Automatic Live Auto-Recovery (`RecoverFromCorruptedStorageAsync`)**: Implemented automated repair routine that, upon discovering unrecoverable database errors, invokes `ClearBrowsingDataAsync` to selectively and cleanly purge `IndexedDb`, `ServiceWorkers`, and `CacheStorage`, re-initializing a healthy database and instantly reloading the page while preserving `localStorage`, session cookies, and login credentials.
  - **CacheStorage Protection in Cache Cleanup (`CleanTransientCacheFolders`)**: Excluded `Service Worker\CacheStorage` and `ScriptCache` folders from cold cleanup to prevent desynchronization between PWA files and Telegram/WhatsApp IndexedDB tables.
- **Manual "Repair Tab" Context Menu Tool (`MainWindow.xaml`, `MainWindow.xaml.vb`)**:
  - Added "Repair tab (reset corrupted database)" option to the account tab right-click context menu, allowing users to manually force local database and web cache resets in case of freezes or persistent blank pages.
- **Full Multilingual Localization (`Localization.vb`)**:
  - Synchronized new keys for tab repair (`account_repair_*`) and shutdown dialog stages (`shutdown_dialog_*`, `shutdown_stage_*`) across all 5 official dictionaries (English, Italian, French, Spanish, German).

## [1.2.3-beta] - 2026-10-13

### Pre-release / Beta — Smart Hibernation of Inactive Tabs, Chromium Cache Limiting, and Login-Preserving Selective Cleanup
- **Smart Hibernation of Inactive Tabs (`AppAccounts.vb`, `MainWindow.xaml`, `MainWindow.xaml.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb` — TODO #50)**:
  - **Automatic RAM Suspension (`TrySuspendAsync`)**: Implemented `TryHibernateAsync()` and `ResumeFromHibernation()` methods that suspend JavaScript execution and rendering in background WebView2 instances without closing the process, drastically shrinking RAM working set.
  - **Inactivity Timer and Instant Wakeup**: Configured 60-second periodic timer detecting tab inactivity (configurable threshold: 15, 30, or 60 minutes, default 30 min) and suspending tabs transparently. Zero-latency instant wake-up upon clicking a tab or switching accounts.
  - **Visual Indicator and UI Options**: Displays `💤` icon on hibernated tabs in the main bar, along with a dedicated Settings section featuring inactivity timeout dropdown and toggle.
- **Chromium Disk Cache Limiting (`AppAccounts.vb`, `SettingsController.vb`, `SettingsWindow.xaml` — TODO #71)**:
  - **`--disk-cache-size` Configuration**: Added option to cap Chromium HTTP disk cache via `CoreWebView2EnvironmentOptions` (128 MB, 256 MB recommended, 512 MB, 1024 MB, or unlimited), preventing uncontrolled growth of browser profile storage.
- **Smart Login-Preserving Cache Cleanup (`AccountManager.vb`, `AppAccounts.vb`, `MainWindow.xaml.vb`, `SettingsWindow.xaml` — TODO #72)**:
  - **Non-Destructive Selective Cleanup**: Implemented `SmartCleanCachePreservingLoginAsync()` method to clear bulky temporary files, disk cache, GPU shader cache, and crash reports (`Cache`, `Code Cache`, `GPUCache`, `DawnWebGPUCache`, `Crashpad`), while preserving cookies, crypto keys, `IndexedDB`, `Local Storage`, and active login sessions.
  - **Centralized and Per-Tab Triggering**: Added "Clear Cache (preserve login)" button in Settings and a dedicated entry in the account tab context menu.
- **Full Multilingual Localization (`Localization.vb`)**:
  - Synchronized 15 new keys across all 5 supported language dictionaries (EN, IT, FR, ES, DE).

## [1.2.2-beta] - 2026-10-12

### Pre-release / Beta — WebView2 Account Native Web Notification Suppression and Duplicate Popup Resolution
- **Block Native Notifications and Popups for WebView2 Accounts (`AppAccounts.vb`)**:
  - **Deny Notification Permissions at Profile and Runtime Levels (`CoreWebView2PermissionState.Deny`)**: Permanently denied browser-native notification permissions both in the `PermissionRequested` handler and in persistent WebView2 profile configuration (`Profile.SetPermissionStateAsync`) for WhatsApp Web, Telegram Web, and all configured account origins (OpenClaw, Hermes, etc.). Web apps running in tabs will no longer display native browser popups, banners, or system notifications.
  - **Intercept and Suppress `CoreWebView2.NotificationReceived`**: Registered `NotificationReceived` on all WebView2 instances with `e.Handled = True`, preventing the default WebView2 notification UI from displaying and signaling orderly completion to the Chromium engine (`ReportShown()` / `ReportClosed()`). Safe listener deregistration guaranteed in `Dispose()`.
- **Elimination of Screen Overlaps and Duplicate Popups (`AppAccounts.vb`)**:
  - **Suppress Windows Toast Banners When In-App Popup Active (`SuppressPopup`)**: When "Show message popup" (`ShowMessagePopup`) is enabled, native Windows Toast notifications are routed silently to Action Center with `toast.SuppressPopup = True`, ensuring that only HidaChat's custom overlay popup (`MessagePopup`) appears on screen and completely eliminating duplicate visual alerts.

## [1.2.1-beta] - 2026-10-11

### Pre-release / Beta — Configuration I/O Optimization, Portable Secrets & Bearer Token Security, Multi-PC File Lock, and tsnet State Directory Isolation
- **I/O Optimization and `SettingsFile` Resolution (`SettingsController.vb` — TODO #65)**:
  - **Thread-Safe Lazy Resolution**: Replaced per-access I/O disk checks with thread-safe cached storage via `Lazy(Of String)` (`_settingsFilePathLazy`). The `data/settings.json` configuration path is calculated and cached exactly once across the application lifecycle.
  - **Dedicated Initialization and Migration (`InitializeSettingsFilePath`)**: Directory verification for `data/` and one-time migration of historical `settings.json` from the root are isolated in the initialization routine, eliminating redundant `File.Exists`, `Directory.CreateDirectory`, and `File.Move` calls during standard read/write cycles.
- **Portable Secrets Security, tsnet Host Isolation, and Bearer Token Injection (`SettingsController.vb`, `TsnetManager.vb`, `AppAccounts.vb`, `MainWindow.xaml.vb` — TODO #60)**:
  - **Explicit DPAPI Error Handling**: On DPAPI decryption failures (`CryptographicException`, e.g. moving the portable app folder to another Windows PC or user account), the application no longer silently wipes the key; instead, it sets `TsnetAuthKeyDecryptionFailed = True`, logs diagnostics, and alerts the user with an explicit dialog requesting credential re-entry.
  - **tsnet Host State Directory Isolation (`data/tsnet/<hostname>`)**: Implemented `GetMachineStateDir()` mapping each computer to an isolated state directory for the embedded tsnet node, preventing node identity cloning or enrollment conflicts across machines sharing the portable folder.
  - **Automatic `tsnetAuthKey` Cleanup Post-Enrollment**: Once the node reaches `Running` state, `OnEnrollmentCompletedAsync` automatically purges the stored single-use key (`ClearTsnetAuthKeyAfterEnrollmentAsync`), preventing lingering secrets in JSON files.
  - **Clear-Text URL Token Removal & Bearer Token Injection**: Completely eliminated `token=` query parameters from navigation URLs for OpenClaw and Hermes platforms. Access tokens are now securely injected via the `Authorization: Bearer <token>` HTTP header intercepted by CoreWebView2's `WebResourceRequested` event (both for direct connections and through the tsnet reverse proxy).
- **Active Multi-PC Protection via Folder Lock (`Application.xaml.vb`, `README.md`, `README.it.md` — TODO #70)**:
  - **OS Exclusive File Lock (`data/.lock`)**: On startup (`Application.OnStartup`), an exclusive filesystem lock (`data/.lock`) is acquired with `FileShare.Read` and held throughout execution.
  - **Profile Corruption Prevention and Diagnostic Dialog**: When started from a second computer or concurrent process pointing to the same folder, the app reads serialized metadata (hostname, user, PID, startup timestamp of the locking instance) and displays a blocking informational warning, shutting down cleanly to protect WebView2 LevelDB profiles and databases.
  - **Controlled Release on Exit**: Clean handle release and deletion of `data/.lock` in `Application.OnExit` and all shutdown/exit code paths.
  - **Harmonized Portability Documentation and Requirements**: Updated `README.md` and `README.it.md` detailing the `data/.lock` mechanism and clarifying "Zero Installation" (standalone portable distribution) requirements alongside Microsoft .NET 9 Desktop Runtime.
- **Synchronous Multilingual Localization (`Localization.vb`)**:
  - Added and synchronized new keys for credential decryption alerts and folder lock warnings (`secret_decrypt_error_title`, `secret_decrypt_error_msg`, `folder_locked_title`, `folder_locked_msg`) across all 5 supported language dictionaries (English, Italian, French, Spanish, German).

## [1.2.0] - 2026-10-09

### Stable Release — Resilient WebView2 Profile Architecture, Atomic Cold Profile Copies, Native Web API Restoration, and Multi-PC Synchronization
- **Full Restoration of Native Web APIs and Browser Normalization (`Scripts/notification.js`, `AppAccounts.vb`)**:
  - **Native WebSocket and IndexedDB Integrity**: Completely removed interception and monkey-patching of `window.WebSocket` constructors and `indexedDB.deleteDatabase`. Socket connections and cryptographic storage (Signal and LevelDB) operate 100% per native W3C Chromium standards with no prototype alterations or timing interference.
  - **Chromium Browser Flags Normalization**: Removed background timer throttling overrides (`--disable-background-timer-throttling` and `--disable-backgrounding-occluded-windows`), restoring standard browser suspension behavior and preventing disk I/O contention and transaction aborts (`QuotaExceededError`) during virtual or hardware resets.
  - **Sanitized Permission Delegation**: `navigator.permissions.query` overrides are strictly limited to notifications, delegating all other permissions to native WebView2 handling.
- **Resilient WebView2 Profile Lifecycle Architecture (`AppAccounts.vb`, `WebViewLifetime.vb`)**:
  - **Differentiated Process Failures**: Non-fatal GPU or utility subsystem crashes no longer trigger forced browser instance recreation; recreation is only invoked if the main browser process has terminated.
  - **Atomic `BrowserProcessExited` Synchronization**: Asynchronously awaits true termination of the `msedgewebview2.exe` process before releasing resources or reusing directories, eliminating lock contention and LevelDB corruptions.
  - **Thread-Safe Serialization**: Synchronized recreations via semaphore to eliminate race conditions.
- **Atomic Cold Profile Copies and Staging Safety (`ColdProfileCopy.vb`, `NetworkProfileSync.vb`)**:
  - **Strict Ban on Live Copies (`[LIVE_COPY_REFUSED]`)**: Blocked all profile copying or exporting while the browser is running.
  - **Atomic Staging and Rollback**: Copy operations target temporary `.incoming-*` folders with final atomic renames and retention of intact `.previous` backups.
  - **Eliminated Destructive Heuristics**: Removed size-based checks that risked discarding valid profiles.
- **OS Exclusive Lease for Multi-PC Shares (`NetworkProfileSync.vb`)**:
  - Filesystem-level file lock (`FileShare.None`) active throughout the session to prevent concurrent launches of the same account from different workstations.
- **Clean and Non-Intrusive Updates (`UpdateChecker.vb`)**:
  - Eliminated forced termination via `taskkill /f`: updater waits for graceful host process termination before replacing files.
- **Complete Test Suite**:
  - Successfully verified all 42 .NET regression tests and 9 JavaScript tests in joint execution.

## [1.1.8-beta] - 2026-10-08

### Pre-release / Beta — Full Restoration of Native Web APIs (WebSocket and IndexedDB), Chromium Browser Flags Normalization, and I/O Resilience Isolation
- **Full Restoration of Native Web APIs (`Scripts/notification.js`)**:
  - **Removal of `window.WebSocket` Override**: Eliminated replacement of global WebSocket constructor and live diagnostic listener injection. Native constructor, prototypes, and socket negotiation remain 100% unaltered, avoiding timing anomalies and concurrent duplicate connections during reconnects.
  - **Removal of `window.indexedDB.deleteDatabase` Override**: Removed wrapper on database deletion; IndexedDB and LevelDB databases operate exclusively through native W3C browser engines without JavaScript interception or stack trace allocations.
  - **Sanitized Navigator Permissions Handling**: The fallback in `navigator.permissions.query` is strictly confined to `notifications`, avoiding unexpected results for storage, clipboard, or device queries.
- **Chromium Browser Flags Normalization (`AppAccounts.vb`)**:
  - **Removal of `--disable-background-timer-throttling` and `--disable-backgrounding-occluded-windows`**: Forcing throttling off prevented Chromium from gracefully handling sleep, disk controller resets (VirtIO SCSI `vioscsi 129`), and sleep/wake events, causing LevelDB transaction aborts (`AbortError: QuotaExceededError`). Restored native Microsoft Edge WebView2 behavior that safeguards background disk I/O.
  - **Browser Arguments Cleanup**: Removed non-recommended flags for Microsoft distribution, keeping only standard `Spellcheck` support and disabling non-essential features (`Translate`, `MediaRouter`).
- **Joint In-Page Script Verification (`Tests/session-health.test.cjs`)**:
  - Expanded JavaScript test suite to validate that simultaneous execution of all injected scripts (`session-health.js` and `notification.js`) preserves native transport (`window.WebSocket`) and storage (`indexedDB.deleteDatabase`) prototypes and constructors.

## [1.1.7-beta] - 2026-10-07

### Pre-release / Beta — Defensive WebView2 Profile Lifecycle Architecture, Atomic Cold Profile Copies, Multi-PC Exclusive Lease, and Session Health Diagnostics
- **Resilient WebView2 Failure Handling (`AppAccounts.vb`, `WebViewLifetime.vb`)**:
  - **Process Failure Differentiation (`ProcessFailed`)**: Non-fatal GPU crashes, utility subsystem crashes, or temporary renderer unresponsiveness (`RenderProcessUnresponsive`) no longer tear down the browser instance. Full recreation occurs solely if the main browser process has exited.
  - **Explicit `BrowserProcessExited` Synchronization**: Introduced asynchronous waiting (`WaitForBrowserProcessExitAsync`) for actual termination of the OS process `msedgewebview2.exe` before freeing resources, reusing profile directories, or initiating exports, eliminating lock contention and LevelDB corruption from premature teardowns.
  - **Recreation Serialization**: Thread-safe synchronization via semaphore to prevent race conditions or concurrent recreations during rapid crash bursts.
- **Atomic Cold Profile Copies and Staging Safety (`ColdProfileCopy.vb`, `NetworkProfileSync.vb`)**:
  - **Strict Rejection of Live Copies (`[LIVE_COPY_REFUSED]`)**: Banned any profile directory exports while the associated browser is running, preventing capture of partially written LevelDB/IndexedDB files.
  - **Atomic Staging and Rollback (`.incoming-*` and `.previous`)**: Disk and network copies use temporary staging directories with final atomic renaming. If interrupted, the intact previous version is preserved and remains recoverable.
  - **Removal of Destructive Size Heuristics (`SessionRecoveryPolicy.vb`)**: Removed heuristic checks based on arbitrary size thresholds that discarded valid local profiles or overwrote active profiles with orphaned folders.
- **OS-Level Exclusive Lease for Multi-PC Shares (`NetworkProfileSync.vb`)**:
  - **Blocking OS File Handle**: Replaced timestamp-based checks with an exclusive filesystem lock (`FileShare.None`) maintained for the full duration of the session. If another machine is already running the account, acquisition fails cleanly, preventing cross-overwriting.
- **Clean Lifecycle in Updates (`UpdateChecker.vb`)**:
  - **Removal of Destructive `taskkill /f`**: Eliminated forced browser killing during updates; the script waits for graceful termination of the host process before replacing binaries.
- **Passive In-Page Session Health Diagnostics (`Scripts/session-health.js`)**:
  - **Periodic Storage & UI Heartbeat**: Passive 60-second monitoring of storage persistence (`navigator.storage.persist()`), IndexedDB quota estimation, and visual WhatsApp UI state (QR transitions, loading, active chat) without altering the DOM or intercepting native database calls.
  - **WebView2 Permission Handling**: Explicitly handled `PermissionRequested` to automatically grant storage persistence permissions for trusted hosts (`web.whatsapp.com`, `web.telegram.org`).
- **Regression Test Suite (.NET and JavaScript)**:
  - Integrated 42 .NET unit regression tests (`Tests/HidaChat.SessionTests.vbproj`) and 9 JavaScript tests (`Tests/session-health.test.cjs`) in both local builds and GitHub Actions CI pipelines.

## [1.1.5-beta] - 2026-10-05

### Pre-release / Beta — Root Cause Resolution for Overnight Disconnections: Live LevelDB Exclusion from Periodic Sync, IndexedDB Database Integrity Restoration, and W3C Storage Persistence
- **LevelDB Lock Contention and QuotaExceededError Elimination (`NetworkProfileSync.vb`)**:
  - **Exclusion of Live LevelDB Folders During Periodic Sync**: During live periodic sync (`isPeriodic:=True`), active `IndexedDB`, `Local Storage`, and `Session Storage` folders are categorically excluded from copying to SMB shares. Sequential reading of `.ldb` and `.log` files while Chromium held active locks over high-latency network shares induced prolonged I/O contention, triggering `QuotaExceededError (AbortError) - dropping db read operation due to logout`. The local SSD staging folder remains the single fast and reliable source of truth 24/7; full mirroring to master occurs atomically upon standard application exit.
  - **Removal of Massive Snapshot Updates in Periodic Timer**: The Last-Known-Good snapshot is now updated exclusively on application shutdown (`Not isPeriodic`) instead of every 10-minute cycle, eliminating continuous 30+ minute copies that saturated network bandwidth and disk subsystems.
- **Browser Database Error Resolution and QR Pairing Restoration (`Scripts/notification.js`)**:
  - **Removal of Dummy `indexedDB.deleteDatabase` Interception**: Removed redirection of deletions to dummy databases. The dummy emulation prevented WhatsApp from recreating corrupt tables during initial pairing or schema migrations, producing the error "A database error occurred in your browser". Deletion attempts are now logged in diagnostics and passed directly to the native IndexedDB engine.
  - **Corrupt WhatsApp Tables Cleanup**: Purged leftover LevelDB files damaged by previous lockups, allowing WhatsApp to initialize a clean, conflict-free database.
  - **W3C Storage Persistence Request (`navigator.storage.persist()`)**: Automatically requests storage persistence on startup to prevent Chromium from applying restrictive background storage quotas or automatic data eviction.

## [1.1.4-beta] - 2026-10-04

### Pre-release / Beta — Root Cause Resolution for WhatsApp Session Loss: IndexedDB Auto-Destruction Block, LevelDB ERROR_SHARING_VIOLATION Fix, and SMB Sync Optimization
- **Absolute Protection Against IndexedDB Auto-Destruction (`Scripts/notification.js`)**:
  - **Blocked Session Database Deletions**: Intercepted W3C `window.indexedDB.deleteDatabase` to prevent WhatsApp Web's internal error handlers (`t.reset()` triggered on temporary I/O glitches or WebSocket drops) from wiping cryptographic databases (`signal-storage`, `wawc_db_enc`, `wawc`, `model-storage`, `worker-storage`, etc.).
  - **Compliant IDBOpenDBRequest Emulation**: Deletion requests for protected databases are transparently redirected to a dummy database, returning a valid `IDBOpenDBRequest` with `onsuccess`. This prevents client crashes while cryptographic keys and device registration remain intact on disk, allowing immediate session recovery without QR code rescans.
- **Resolution of `ERROR_SHARING_VIOLATION` (0x20) and LevelDB Storage-Error (`NetworkProfileSync.vb`)**:
  - **Enabled `FileShare.Delete`**: Added `FileShare.Delete` (`FileShare.ReadWrite Or FileShare.Delete`) when opening files in `CopyFileWithRetry`. On Windows, omitting `FileShare.Delete` prevented Chromium LevelDB from deleting or renaming SST files (`.ldb`) and logs (`.log`) during background compactions while copying was reading, causing `ERROR_SHARING_VIOLATION (32)` and subsequent `AbortError (storage-error) / QuotaExceededError` in WhatsApp Web.
- **SMB Network Sync Performance and Concurrency Optimization (`NetworkProfileSync.vb`, `MainWindow.xaml.vb`)**:
  - **Bulk Pre-Indexing of Remote Files**: Resolved the bottleneck that caused 20+ minute sync times on SMB network shares. `SyncDirectoryAsync` now indexes destination folder files upfront into an in-memory dictionary, eliminating thousands of individual network RPC calls (`File.Exists` and `FileInfo` metadata queries).
  - **Atomic Concurrency Control (`SemaphoreSlim`)**: Integrated a class-level thread-safe semaphore `_syncLock` in `NetworkProfileSync.vb` ensuring periodic and shutdown syncs never overlap or collide on the same files.
  - **Reentrancy Guard in Periodic Timer (`MainWindow.xaml.vb`)**: Added `_isPeriodicNetworkSyncRunning` on `_periodicNetworkSyncTimer` tick to ignore new ticks if a previous sync is still running.
  - **Controlled Mirror Cleanup of Orphaned Files**: During live periodic sync, if master size exceeds twice that of healthy local staging (> 2 MB), safe mirror removal activates to purge hundreds of orphaned SST files discarded by Chromium, avoiding profile bloating on network drives.

## [1.1.3-beta] - 2026-10-03

### Pre-release / Beta — In-Depth WhatsApp Web Diagnostics, Last-Known-Good Snapshot, and Storage Collapse Protection
- **Centralized Portable Logging System (`AppLogger.vb`)**:
  - **Portable Log Folder (`data/logs/`)**: Introduced thread-safe logging architecture inside the application portable folder, with automatic file rotation at 5 MB.
  - **WhatsApp Web Tracking (`whatsapp.log` and `whatsapp_errors.log`)**: Continuous recording of WhatsApp page lifecycle events, including HTTP 4xx/5xx errors, WebSocket closures and status codes, database deletion attempts, and modal popups.
  - **Network Synchronization Tracking (`sync.log`)**: Detailed recording of transfers between local staging (%LOCALAPPDATA%) and master on network drive (Y:\), verifying KB sizes before and after each sync.
- **In-Page JavaScript Telemetry Instrumentation (`Scripts/notification.js`)**:
  - **Error Modals and Popups Detection**: Automated scanner for dialogs containing "logged out", "disconnesso", "something went wrong", "use here", or "session expired", extracting text, buttons, and serialized HTML to host logs immediately.
  - **`console.error` and `console.warn` Interception**: Real-time forwarding of critical errors and warnings from WhatsApp Web client code to `AppLogger`.
  - **WebSocket Monitoring (`wss://web.whatsapp.com/ws/chat`)**: Captures every `open`, `error`, and `close` event (with numeric code, reason string, and `wasClean` flag).
  - **`indexedDB.deleteDatabase` Interception**: Captures full stack traces of any script attempting to delete the local session database (`wawc`).
  - **Global Error Handling**: Captures `window.onerror`, `unhandledrejection`, `visibilitychange`, and network events (`online`/`offline`).
- **Last-Known-Good Session Snapshot and Storage Collapse Protection (`NetworkProfileSync.vb`)**:
  - **Automated Protected Snapshot (`WV2Profile_{id}_Snapshot`)**: Continuously creates and updates a frozen session snapshot whenever local database size exceeds 2 MB (authenticated, functioning session).
  - **Block Destructive Master Deletion**: If local staging experiences an unexpected database collapse (< 500 KB) while master on network retains a healthy session (> 2 MB), mirror deletion is strictly blocked and a security alert is logged.
  - **Resilient Startup Recovery**: Fixed bug that skipped master download even with incomplete or damaged staging sessions; if staging is corrupted or empty, profile is restored automatically from master or Last-Known-Good snapshot.
- **Background Process and WebSocket Stability (`AppAccounts.vb`)**:
  - **Prevent Keepalive Suspension**: Reintroduced `--disable-background-timer-throttling` and `--disable-backgrounding-occluded-windows` to prevent Chromium from freezing overnight WebSocket ping timers, while keeping CPU load low via debounce and lightweight 25s polling in `notification.js`.
  - **Enabled DevTools and Response Inspection**: Enabled DevTools and listened to `WebResourceResponseReceived` and `ProcessFailed` events for comprehensive diagnostics.

## [1.1.2-beta] - 2026-10-02

### Pre-release / Beta — Final Resolution of WhatsApp Web Session Loss, Periodic Sync Timer, and Background Memory Optimization
- **Full Session Protection in Local Staging (`NetworkProfileSync.vb`)**:
  - **Skip Download When Active Session Exists**: Modified `SyncMasterToLocalStagingAsync` so that if local staging in `%LOCALAPPDATA%` already contains a session (populated `IndexedDB` or `Preferences` file), downloading from the master network folder `Y:\` is completely skipped. This prevents injecting outdated LevelDB SST/log files and eliminates storage corruption risks during startups or automatic recoveries.
  - **Safe Periodic Synchronization (`SyncLocalStagingToMasterAsync`)**: Added `isPeriodic` parameter that disables mirror purging during live syncs, protecting files being actively written by the running browser.
- **Memory Fix and Overnight WhatsApp Web Watchdog Timeout (`AppAccounts.vb`)**:
  - **Removed Forced Background Flags**: Removed `--disable-background-timer-throttling`, `--disable-backgrounding-occluded-windows`, and `--disable-renderer-backgrounding` that kept 4 WebView2 processes active as if in the foreground at 60 FPS 24/7, exhausting page tables (Memory Management 1a/3f) and causing JS main thread freezing with internal watchdog `unresponsiveness-events-v2` triggers.
  - **Removed Restrictive Cache Caps**: Removed small `--disk-cache-size` and `--media-cache-size` limits that caused continuous I/O thrashing, allowing Chromium to handle standard lifecycle management and V8 garbage collection overnight.
- **Radical Lightening of DOM and Notification Monitoring (`Scripts/notification.js`)**:
  - **Eliminated `document.body` Observer**: Completely removed whole-body document listening. Introduced selective deferred attachment specifically to the `#pane-side` chat panel as soon as it appears in the DOM.
  - **Low-Impact Polling**: Extended polling to 25 seconds when the window is in background or hidden, completely disabling `scanOnlineStatus`.
- **Continuous Periodic Synchronization to Network Drive (`MainWindow.xaml.vb`, `AccountManager.vb`)**:
  - **10-Minute Periodic Timer**: Introduced `_periodicNetworkSyncTimer` executing `SyncAllProfilesToNetworkAsync(isPeriodic:=True)` every 10 minutes in the background. Even if the application stays open for days or weeks without closing, data on network drive `Y:\` remains continuously up-to-date and protected from abrupt shutdowns or network drops.

## [1.1.1-beta] - 2026-10-01

### Pre-release / Beta — WhatsApp Web Overnight Session Error Fix, Local Staging Protection, and Background Throttling
- **Anti-Overwrite Protection for Local Staging (`NetworkProfileSync.vb`)**:
  - **Protected Unidirectional Synchronization (`onlyIfSourceNewer`)**: Resolved critical bug where `SyncMasterToLocalStagingAsync` on startup or during recovery unconditionally overwrote recent local session folders in `%LOCALAPPDATA%` with outdated files from network drive `Y:\`, corrupting LevelDB database coherence (unaligned `CURRENT`, logs, and manifests).
  - **Priority for Recent Local Data**: If the local staging folder exists and contains an active session, no destination file is overwritten if the network source is older than or equal to the local file (`LastWriteTimeUtc`).
  - **I/O Resilience and Lock Retry (`CopyFileWithRetry`)**: Increased retry count to 6 with incremental delays (150ms per retry) and dedicated handling of `UnauthorizedAccessException` for files momentarily locked by the browser engine.
- **WhatsApp Web Overnight Watchdog Timeout Resolution (`Scripts/notification.js`)**:
  - **Smart Background Throttling (`document.hidden`)**: Resolved WhatsApp Web freezes and subsequent *"Something went wrong"* modal popups caused by `unresponsiveness-events-v2` watchdog crash events triggered when resuming from PC sleep or after long overnight idle periods.
  - **Adaptive Polling**: Scan interval switches automatically from 2 seconds in the foreground to 12 seconds when the app is in the background or minimized, completely suspending online status checks (`scanOnlineStatus`) when the window is not visible.
  - **Lightweight DOM Observer**: Removed recursive `subtree: true` with `attributes: true` listener across `document.body` that captured thousands of CSS mutations during message syncs; introduced targeted listening on `#pane-side` with a 3-second throttle when hidden.
  - **Instant Wakeup on `visibilitychange`**: When the user unlocks the PC or brings the window to the foreground, unread and online status checks resume immediately.
- **Hardened Coordinated Shutdown and Resource Release (`MainWindow.xaml.vb`)**:
  - **Sync Timeout Extended to 30s**: Increased safety timer to 30 seconds for return mirror sync to network folders (`SyncAllProfilesToNetworkAsync`), ensuring SMB shares on NAS or Wi-Fi complete transfers without file truncations.
  - **Wait for WebView2 Lock Release**: Added controlled pause (600ms) after account `Dispose()` before copying back to master, allowing `msedgewebview2.exe` processes to flush buffers to disk and release file locks.

## [1.1.0] - 2026-09-30

### Stable Release — Network Drive Support (SMB/UNC), WebView2 Session Staging & Sync, Async Hardening, and Centralized Logging
- **Full Network Drive Support (SMB/UNC) with Transparent Local Staging (#73)**:
  - **Reliable WhatsApp Web Session Persistence**: Permanently resolved session loss and QR code resets when running from network paths or mapped drives (SMB/CIFS, `\\server\share`, `Z:\`).
  - **Automatic Local Staging on Startup**: Session databases (`IndexedDB`, `Local Storage`, `Network/Cookies`, preferences) are transferred to and executed from fast local memory `%LOCALAPPDATA%\HidaChat\NetworkProfiles\{AccountId}`, eliminating `STATUS_FILE_LOCK_CONFLICT` lock anomalies in LevelDB and SQLite.
  - **Smart Filtering of Volatile Caches**: Excluded bulky cache directories (`Cache`, `Code Cache`, `GPUCache`, `DawnGraphiteCache`, `ShaderCache`, `Crashpad`), ensuring ultra-fast transfers (a few megabytes) and instant startups.
  - **Atomic Return Synchronization to Network Share**: Upon graceful shutdown (`PrepareForShutdown` and `ForceExitForUpdateAsync`), all updated local profiles are mirrored incrementally back to the portable master folder `data/webview/{AccountId}` on the network share.
  - **Multi-PC Conflict Detection (#70, #73)**: Lightweight session lock mechanism (`.active_session`) detecting concurrent accesses from multiple machines on the same network directory.
  - **Multilingual Settings & UI**: New "Network Drives" section in Settings with dedicated toggle and synchronous alignment across all 5 language dictionaries (English, Italian, French, Spanish, German).
- **IPC Bridge Hardening, Async Resilience, and Exception Handling (#63)**:
  - **Full `Async Sub` Protection with Try/Catch**: Shielded all asynchronous event handlers in the WebView2 bridge and UI against unobserved exceptions and network interruptions.
  - **Deterministic Await with `SafeExecuteScriptAsync`**: Reliable execution and asynchronous unwrapping of JavaScript tasks without unobserved task exceptions.
  - **Centralized Portable File Logging (`data/logs/app_errors.log`)**: Thread-safe logging with automatic rotation (1 MB) for `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`, and `AppDomain.CurrentDomain.UnhandledException`.
- **Do Not Disturb (Focus Mode) Fix (#64)**:
  - Replaced comparison operator with `Nullable.Equals` on `DndUntil` property, ensuring clean DND state transitions and accurate UI notifications.
- **WebView Security Hardening and IPC Tokens (#61, #62)**:
  - Strict allowlist for external URI schemes (`http`, `https`, `mailto`), routing `target="_blank"` links to the system browser.
  - IPC bridge authentication tokens generated using 128-bit cryptographic CSPRNG (`RandomNumberGenerator`) with sender origin validation (`IsAuthorizedOrigin`).
- **Session Persistence on Windows Shutdown and Notification Polish**:
  - Intercepted `OnSessionEnding` (`WM_QUERYENDSESSION`) with coordinated save and graceful teardown of WebView2 processes before session termination.
  - Notification popups and Toasts enabled for background accounts with mouse hover support, `HWND_TOPMOST` z-order, and optimized visual styling.

## [1.0.12-beta] - 2026-09-28


### Pre-release / Beta — Network Drive Support (SMB/UNC) with Local Staging & WebView2 Session Sync, and Multi-PC Conflict Detection
- **Network Drive Support via Local Staging and Session Synchronization (`NetworkProfileSync.vb`, `AppAccounts.vb`, `AccountManager.vb`, `MainWindow.xaml.vb`) (#73)**:
  - **Resolution of WhatsApp Session Loss on SMB/UNC Shares**: Resolved a structural Chromium/WebView2 issue where running the application from network shares or mapped drives (e.g., `Z:\` or `\\server\share`) caused LevelDB and SQLite engines to lose file locks or encounter `STATUS_FILE_LOCK_CONFLICT` errors, leading to the automatic wiping of `IndexedDB` databases on restart and resetting WhatsApp Web back to the QR code screen.
  - **Transparent Local Staging on Startup (`SyncMasterToLocalStagingAsync`)**: During account initialization, if the application is running from a network path, essential session data (`IndexedDB`, `Local Storage`, `Network/Cookies`, preferences) are synchronized from the network master into a fast local staging folder at `%LOCALAPPDATA%\HidaChat\NetworkProfiles\{AccountId}`.
  - **Smart Exclusion of Volatile Caches**: Filtered out and excluded heavy, non-essential cache directories (`Cache`, `Code Cache`, `GPUCache`, `DawnGraphiteCache`, `ShaderCache`, `Crashpad`) during synchronization, enabling ultra-fast transfers (a few megabytes) and minimizing bandwidth usage and wait times.
  - **Native Local WebView2 Initialization**: `CoreWebView2Environment` points to the local staging directory, guaranteeing native POSIX/NTFS locking semantics, total elimination of corruption, and optimal I/O throughput.
  - **Atomic Reverse Sync on Shutdown (`SyncLocalStagingToMasterAsync`, `SyncAllProfilesToNetworkAsync`)**: During coordinated shutdown (`PrepareForShutdown` and `ForceExitForUpdateAsync`), after controlled release of browser processes, all local profiles are pushed back via incremental mirroring to the portable master folder `data/webview/{AccountId}` on the network share, preserving the application's 100% portability.
- **Multi-PC Conflict Detection with Session Lock (`NetworkProfileSync.vb`) (#70, #73)**:
  - **Active Machine Lock (`.active_session`)**: Introduced a lightweight session lock mechanism tracking computer name, user, PID, and UTC timestamp in the master profile folder.
  - **Concurrent Conflict Reporting**: If another computer has opened the same profile within the last 3 minutes, a conflict is detected and a warning is logged to prevent concurrent overwrites.
- **Multilingual Configuration and User Interface (`SettingsWindow.xaml`, `SettingsController.vb`, `Localization.vb`)**:
  - **New "Network Drives" Section in Settings**: Added the "Enable local staging for network drives" checkbox (enabled by default) to manage the feature and allow fallback when needed.
  - **Updated Network Drive Detection Message**: Replaced the previous error alert with an informational notice confirming that local staging has been activated to safeguard sessions.
  - **Full Synchronization of All 5 Dictionaries**: Simultaneously updated all language dictionaries in `Localization.vb` (English, Italian, French, Spanish, German) with new keys `enable_network_profile_staging`, `enable_network_profile_staging_hint`, `network_drive_staging_info`, `network_drive_section`, `network_drive_session_conflict`.

## [1.0.11-beta] - 2026-09-27


### Pre-release / Beta — WebView2 Bridge Async Handler Resilience, Global Exception Logging, and DndUntil Nullable Fix
- **Async Error Handling and IPC Bridge Hardening (`AppAccounts.vb`, `MainWindow.xaml.vb`) (#63)**:
  - **Comprehensive `Async Sub` Protection with Try/Catch**: Added exception handling with `Try/Catch` blocks and file logging to the `_navigationCompletedHandler` and `_webMessageReceivedHandler` asynchronous WebView2 event handlers, preventing unobserved exceptions or sudden navigation aborts from crashing the application.
  - **Asynchronous Invocation and Deterministic Task Await (`SafeExecuteScriptAsync`)**: Eliminated unawaited invocations of `WebView.Dispatcher.InvokeAsync(Async Function() ...)`. Introduced the `SafeExecuteScriptAsync` helper method, which executes the script directly if already on the UI thread or performs full task unwrapping and awaiting (`.Task.Unwrap()`), preventing unobserved task exceptions during single and batch translation communications.
  - **State Guarding and Post-Disposal Continuations (`IsDisposed`)**: Introduced the `IsDisposed` flag and public property on `AppAccounts`; stored a local reference `wv` in `Dispose()` before nulling the `WebView` property, adding defensive guards `If _isDisposed OrElse WebView?.CoreWebView2 Is Nothing Then Return` before and after each asynchronous call.
  - **UI Async Event Hardening (`MainWindow.xaml.vb`)**: Protected all main window asynchronous handlers (`MainWindow_Loaded`, `PopulateWebViews`, `OnAccountProcessFailedRecoveryRequested`, `AccountTabRename_Click`, `OnSettingsPropertyChanged`, and menu-driven account additions) with `Try/Catch` blocks and diagnostic logging.
- **Global Logging System for Unhandled Exceptions (`Application.xaml.vb`) (#63)**:
  - **Centralized Interception**: Hooked `DispatcherUnhandledException` (with controlled UI crash suppression via `e.Handled = True`), `TaskScheduler.UnobservedTaskException` (with `e.SetObserved()`), and `AppDomain.CurrentDomain.UnhandledException` in `Application.xaml.vb`.
  - **Thread-Safe Portable Tracking (`data/logs/app_errors.log`)**: Implemented the `LogUnhandledException` method, writing timestamps, exception types, messages, and stack traces to a portable log file within the application data folder, with automatic rotation at 1 MB (`app_errors.log.old`).
- **Fix for Nullable Evaluation in Do Not Disturb Mode (`SettingsController.vb`) (#64)**:
  - **Correct `Nullable.Equals` Comparison**: Replaced the `<>` operator with `Not Nullable.Equals(_dndUntil, value)` in the `DndUntil` property setter to eliminate VB.NET three-valued logic anomalies when an operand is `Nothing`.
  - **Deterministic Mutation via Public Setters**: Reconfigured `SetDndModeAsync` to mutate state strictly through public property setters (`IsDndEnabled`, `DndUntil`, `DndDurationMode`), guaranteeing correct and synchronous `PropertyChanged` event notifications and reliable updating of `IsDndActive`.

## [1.0.10-beta] - 2026-09-26

### Pre-release / Beta — WebView Security Hardening: URI Scheme Allow-List, CSPRNG IPC Tokens, and Origin Validation
- **URI Scheme Protection and External Window Handling (`AppAccounts.vb`) (#61)**:
  - **URI Scheme Allow-List in `NewWindowRequested`**: Applied a strict allow-list (`http`, `https`, `mailto`) prior to invoking `Process.Start` with `UseShellExecute = True`. Links using dangerous, system protocol handlers, or unmanaged schemes (`file:`, `ms-msdt:`, etc.) are blocked and logged.
  - **Scheme Validation in `NavigationStarting`**: Extended pre-navigation validation to `NavigationStarting`, permitting only safe web schemes (`http`, `https`, `data`, `blob`, `about`) while preserving internal routing for Telegram deep links (`tg://` and `t.me` domains).
  - **External Browser Routing for `target="_blank"` Links**: All links opened with `target="_blank"` are forwarded to the system's default browser instead of overriding WebView2's internal navigation, preserving the active session and primary chat view.
- **IPC Bridge and Sender Origin Security Hardening (`AppAccounts.vb`) (#62)**:
  - **Bridge Token Generation via 128-bit CSPRNG**: Replaced legacy pseudo-random token generation based on `System.Random` and timestamps with a cryptographically secure random number generator (`RandomNumberGenerator.Fill(randomBytes)`), producing unpredictable 128-bit tokens.
  - **Strict Sender Origin Validation `e.Source` (`IsAuthorizedOrigin`)**: Added pre-processing origin validation in `HandleWebMessageAsync`: IPC web messages are processed only when originating from the authorized host of the configured platform (`web.whatsapp.com` for WhatsApp, `web.telegram.org` for Telegram, configured server host or local `127.0.0.1` proxy for OpenClaw and Hermes).
  - **DevTools Disabled in Release**: Configured `AreDevToolsEnabled = False` in Release production builds, enabling Chromium developer tools strictly in Debug compilations (`#If DEBUG`).

## [1.0.9-beta] - 2026-09-25

### Pre-release / Beta — WhatsApp Session Persistence on Windows Shutdown and Network Drive Detection
- **Orderly Shutdown on Windows Session Ending and WebView2 Disposal (`Application.xaml.vb`, `MainWindow.xaml.vb`)**:
  - **System Shutdown/Restart Interception (`OnSessionEnding`)**: Implemented the `OnSessionEnding` override in `Application.xaml.vb` to promptly intercept Windows session-ending notifications (`WM_QUERYENDSESSION` / `WM_ENDSESSION`), preventing `MainWindow`'s `Closing` event from cancelling shutdown with `e.Cancel = True` and triggering an OS-level forced process kill.
  - **Centralized `PrepareForShutdown()` Routine**: Created a coordinated, synchronous routine in `MainWindow.xaml.vb` that forces settings and accounts persistence and performs controlled disposal (`acc.Dispose()`) of all WebView2 instances; this allows Chromium IndexedDB/LevelDB and SQLite databases to flush transactions and release disk locks prior to exit, preventing corruption and WhatsApp Web session loss on the next PC boot.
  - **Unified Application Teardown**: Reconfigured `ExitApplication()` and `MainWindow_Closing` to delegate their entire cleanup sequence to `PrepareForShutdown()`, removing redundant calls to `ClearBrowsingCacheAsync` that could stall termination.
- **Network Drive Execution Detection and Explanatory Warning (`MainWindow.xaml.vb`, `SettingsController.vb`, `Localization.vb`)**:
  - **Automated Path Verification (`CheckNetworkDriveWarning`)**: Added a startup check to detect whether the application is running from a network drive (SMB/CIFS, mapped drives such as `Y:\`, or UNC paths `\\server\share`).
  - **Localized Informational Warning**: When running from a network share, a clear notice explains that Microsoft WebView2 does not support storing user profiles on network drives (causing recurrent WhatsApp session loss due to LevelDB lock instability over SMB) and recommends copying and running the application from a local disk (e.g., `C:\HidaChat`).
  - **Suppression of Repeated Warning**: Added the `suppressNetworkDriveWarning` property persisted in `settings.json` to prevent re-displaying the alert on subsequent startups once acknowledged.
  - **Full Multilingual Synchronization**: Simultaneously updated all 5 dictionaries in `Localization.vb` (English, Italian, French, Spanish, German) with new warning strings `network_drive_warning_title` and `network_drive_warning_msg`.

## [1.0.8-beta] - 2026-09-25

### Pre-release / Beta — Background Account Popup Delivery, Overlay Notifications, Account Type Menu Coloring, and Delete Button Styling
- **Resolution of Missing Popups for Background Accounts (`Scripts/notification.js`, `AppAccounts.vb`)**:
  - **Popup and Toast Dispatch on Background Message Arrival**: Fixed an issue where notification popups appeared only for the currently active/foreground account (e.g., Telegram popups displayed, but WhatsApp popups failed to trigger when WhatsApp was in the background). When a background account detects new unread messages, both the `MessagePopup` visual overlay and a native Windows Toast notification are now emitted.
  - **Chat Details and Message Text Extraction from DOM and Title (`notification.js`)**: Enhanced DOM scraping for both WhatsApp Web and Telegram Web: the listener extracts the contact/group name (`latestChatTitle`) and message snippet (`latestMessageText`), relaying them to the WPF host to display context-rich popups even when the web application fails to invoke native browser notification APIs while hidden or inactive.
  - **Permanent Notification Permissions at WebView2 Profile Level (`AppAccounts.vb`)**: In `SetupWebViewInternalAsync`, explicitly registered notification permissions via `CoreWebView2.Profile.SetPermissionStateAsync` with `Allow` status for `https://web.whatsapp.com` and `https://web.telegram.org`, preventing the Chromium engine from throttling notifications or Service Workers in inactive or hidden tabs.
- **Popup Window Overlay Reliability and Usability (`MessagePopup.xaml.vb`, `AppAccounts.vb`)**:
  - **Native HWND_TOPMOST Z-Order Priority**: Added the Win32 `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)` call on popup launch to guarantee the overlay window floats above any active window without stealing focus, preventing it from getting hidden behind HidaChat's main window or other applications.
  - **Timer Pause on Mouse Hover**: Added `MouseEnter` and `MouseLeave` handlers to pause the auto-close timer (5s) when hovering over the popup, enabling effortless reading or clicking without premature dismissal.
  - **Unblocking Web Dialogs and Script Popups (`AppAccounts.vb`)**: In `_newWindowRequestedHandler`, removed the unconditional `e.Handled = True` block for empty or `about:blank` URIs, restoring proper handling for script modal dialogs, dynamic popups, and OAuth login flows spawned via `window.open`.
- **Account Type Selector Readability in Settings (`SettingsWindow.xaml`, `SettingsWindow.xaml.vb`)**:
  - **Dedicated `AccountPlatformComboBoxStyle` Style**: Introduced a specialized style for account platform ComboBoxes with foreground text set to `#8696a0` (matching other labels such as "Server URL / Tailscale") to ensure optimal contrast and prevent text from blending into the background.
  - **Dynamic Light/Dark Theme Sync (`StyleAccountItems`)**: The theming routine dynamically adapts platform dropdown foreground colors (`#8696a0` in dark theme, `#54656f` in light theme) along with corresponding background brushes.
- **Visual Highlighting of Account Delete Button with Red Accent (`SettingsWindow.xaml`, `SettingsWindow.xaml.vb`)**:
  - **Redesigned `DeleteBtnStyle`**: The account "Delete" button now features vivid red text (`Foreground="#ea0038"`) with a matching dark background `#2a3942` and subtle border, clearly highlighting the destructive action; on hover, visual feedback triggers a tinted background `#381e24`, red border, and brightened text `#ff4d4d`.

## [1.0.7-beta] - 2026-09-24

### Pre-release / Beta — UpdateChecker: UX, Multilingual Localization, SemVer Prereleases, and Portable Logging
- **Complete Localization of Update Dialogs (`UpdateChecker.vb`, `Localization.vb`) (#67)**:
  - **Full Translation of Dialog Boxes**: Eliminated all hardcoded Italian `MessageBox` strings. Introduced and synchronized 12 new translation keys across all 5 supported language dictionaries (`Localization.vb`: EN, IT, FR, ES, DE) for up-to-date status (`update_already_latest`, `update_already_latest_title`), remote version not newer (`update_no_new_version`, `update_no_new_version_title`), installation confirmation prompt (`update_available_prompt`, `update_available_title`), installation errors (`update_install_error`, `update_install_error_title`), failed update checks (`update_check_failed`, `update_check_failed_network`, `update_check_failed_title`), and missing zip assets (`update_no_asset`).
  - **Thread-Safe Dispatcher Invocation**: Implemented `ShowMessageBox` and `ShowMessageBoxConfirm` helpers to seamlessly dispatch visual messages onto the WPF UI thread regardless of the caller thread context.
- **Visual Feedback on Manual Forced Update Check (`UpdateChecker.vb`) (#67)**:
  - When the user triggers a manual update check (`force = True`), GitHub API timeouts, connection drops, rate limiting, or network exceptions now display a clear, localized informational dialog instead of failing silently.
- **SemVer Numeric Comparison of Prerelease Suffixes (`UpdateChecker.vb`) (#67)**:
  - **`ComparePrerelease` Routine**: Implemented natural comparison and parsing of prerelease suffixes using regex tokenization for numeric and alphanumeric identifiers (`\d+|[^\d\.\-_+]+`).
  - **Alphabetical Sorting Bug Fix**: Resolved an issue where versions like "beta10" were evaluated as older than "beta2" due to standard ordinal string comparison (`String.Compare`).
- **Unified GitHub Release Metadata Parsing (`UpdateChecker.vb`) (#67)**:
  - **Shared `ParseRelease(JsonElement)` Routine**: Consolidated release metadata extraction (tags, changelog notes, ZIP assets, and cryptographic SHA-256 checksums) into a unified, null-safe method, eliminating duplicate code between beta prerelease scans and stable endpoint queries.
- **Portable File Logging System (`UpdateChecker.vb`) (#67)**:
  - Replaced all update module `Debug.WriteLine` calls with a centralized `Log(message)` method that writes thread-safely (`SyncLock`) to `data/updates.log` in the application directory, strictly adhering to 100% portability requirements.
  - Integrated automatic log rotation (archiving to `updates.log.old`) upon exceeding 1 MB to prevent unbounded disk growth.

## [1.0.6-beta] - 2026-09-23

### Pre-release / Beta — OTA Update Stall Fix and Immediate Batch Script Execution
- **Early Launch and Standalone Execution of Updater (`UpdateChecker.vb`)**:
  - **Inverted Teardown Sequence**: The update batch script (`hidachat_update_*.bat`) is spawned via `Process.Start` before initiating HidaChat's teardown, making the update routine fully autonomous and immune to UI freezes or delays.
  - **Fast and Deterministic `Environment.Exit(0)` Termination**: After atomically persisting settings and accounts and releasing the single-instance Mutex, the process terminates immediately with `Environment.Exit(0)`, releasing all locks on binary files and allowing `robocopy` to swap files without waiting on forced timeouts.
- **Pre-Update Teardown Optimization and Resilience (`MainWindow.xaml.vb`)**:
  - **Removal of Sluggish and Deadlock-Prone Operations**: Removed slow or COM deadlock-prone calls (`ClearBrowsingCacheAsync` in WebView2, `CleanupTransientCachesAsync`, and `TsnetManager.Instance.ShutdownAsync`) from `ForceExitForUpdateAsync`, delegating process cleanup to `update.bat` and focusing the exit routine solely on atomic user data persistence (`SaveAccountsAsync`, `FlushNowAsync`).
  - **Safety Fallback Timer on `ForceExitForUpdate`**: Added a 3-second fallback timer on `DispatcherFrame` to prevent indeterminate message pump stalls during synchronous shutdown calls.

## [1.0.5-beta] - 2026-09-23

### Pre-release / Beta — MaxAccounts Event Synchronization and Non-Destructive Downgrade Policy
- **Centralized Account Limit Event Synchronization (`AccountManager.vb`, `SettingsController.vb`) (#66)**:
  - **Automatic `PropertyChanged` Subscription**: `AccountManager` subscribes directly in its constructor to `SettingsController`'s `PropertyChanged` event, automatically notifying dependent properties `MaxAccounts`, `CanAddAccount`, `HasExcessAccounts`, and `ExcessAccountsCount` whenever the user alters the limit in Settings.
  - **Clean Disposal and Lifecycle Management**: Implemented `IDisposable` in `AccountManager` with controlled detachment of the `OnSettingsPropertyChanged` handler, preventing memory leaks and dangling references.
- **Non-Destructive Account Downgrade Policy (`AccountManager.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`) (#66)**:
  - **Preservation of Excess Active Sessions**: If the user reduces `MaxAccounts` below the number of currently configured accounts, all existing accounts remain fully active, navigable, and functional without deleting any session data or profiles from disk.
  - **Conservative Addition Guard**: Adding further accounts is inhibited (`CanAddAccount = False`) across both UI controls and `AddAccountAsync` until the total account count falls strictly below the newly configured threshold.
  - **Downgrade Notice Banner in Settings**: Introduced a visual contextual banner (`BorderDowngradeNotice` with `TxtDowngradeNotice`) under the "Account Management" section in `SettingsWindow.xaml`, displaying the number of excess accounts relative to the configured threshold and explaining the non-destructive policy, styled for dark/light themes (`ApplyTheme`).
  - **Expanded Limit Range (2–10)**: Extended the `ComboMaxAccounts` dropdown in `SettingsWindow.xaml` to support any integer limit between 2 and 10 (previously restricted to four discrete tiers).
- **UX Improvements, Dynamic Tooltips, and Multilingual Localization (`MainWindow.xaml`, `MainWindow.xaml.vb`, `Localization.vb`) (#66)**:
  - **Removal of Hardcoded Tooltips**: Removed the legacy static tooltip `Aggiungi account (max 3)` from `BtnAddAccount` in `MainWindow.xaml`.
  - **Dynamic Contextual Tooltips**: The `+` tab button now displays localized, context-aware tooltips based on state (`add_account`, `accounts_downgrade_blocked`, or `max_accounts_reached`), displayed even on disabled controls via `ToolTipService.ShowOnDisabled="True"`.
  - **Full 5-Language Localization**: Added and synchronized new keys `max_accounts_downgrade_notice` and `accounts_downgrade_blocked` across all 5 supported language dictionaries in `Localization.vb` (IT, EN, FR, ES, DE).
- **Temp Folder Locking Resolution and OTA Update File Cleanup (`UpdateChecker.vb`) (#67)**:
  - **Session-Unique Temp Directories**: Replaced static path `%TEMP%\HidaChat_Update` with isolated, session-unique temp folders (`HidaChat_Update_<timestamp>_<guid>`), eliminating `The process cannot access the file ... because it is being used by another process` errors caused by lingering locks or antivirus scans.
  - **External Staging of `update.bat`**: Placed the update batch script directly into `%TEMP%` rather than inside the extracted payload directory, preventing `update.bat` from copying itself into the installation folder and avoiding locks on the extraction source by `cmd.exe`.
  - **Deterministic Cleanup and Preventive Purge**: Added `cd /d "%TEMP%"` before deleting the extracted directory (`rmdir /s /q`) and `.zip` archive (`del /f /q`); added preventive termination of any lingering `tsnetd.exe` instances; implemented asynchronous cleanup of leftover temporary files and directories from previous updates with reliable teardown in the `Finally` block.

## [1.0.4-beta] - 2026-09-22

### Pre-release / Beta — OTA Update Deadlock Resolution and Non-Blocking Shutdown
- **OTA Update Deadlock Resolution (`UpdateChecker.vb`, `MainWindow.xaml.vb`)**:
  - **Elimination of Synchronous `.GetAwaiter().GetResult()` Waiting**: Resolved an application hang triggered during update execution: `ForceExitForUpdate()` synchronously blocked the UI Dispatcher waiting on `ForceExitForUpdateAsync()`, causing an unrecoverable deadlock with asynchronous WebView2 COM calls (`ClearBrowsingDataAsync`), which require an active WPF message pump to complete callbacks. Consequently, `update.bat` was never reached and the app froze without initiating file copying.
  - **Fully Asynchronous Non-Blocking Shutdown**: In `UpdateChecker.vb`, the pre-update sequence directly awaits `Await mainWin.ForceExitForUpdateAsync()` using native asynchronous continuations without freezing the UI thread.
  - **DispatcherFrame with Active Message Pump for Synchronous Invocations**: In `ForceExitForUpdate()` and `MainWindow_Closing`, blocking waits were replaced with a `DispatcherFrame` and a 3-second fallback timer, allowing the message pump to process Windows messages without indefinite stalls.
  - **Protective Safety Timeouts (2s) on WebView2 and Companion Daemon**: Wrapped `ClearBrowsingCacheAsync()` and `TsnetManager.Instance.ShutdownAsync()` in `Task.WhenAny(..., Task.Delay(2000))` across both `ExitApplication` and `ForceExitForUpdateAsync`, preventing dangling WebView2 controls or Chromium child processes from hanging application exit.
  - **Visual Feedback During Download**: Switched to wait cursor (`Cursors.Wait`) upon update confirmation with guaranteed reset in the `Finally` block, giving immediate visual feedback while downloading and extracting packages.
  - **`FlushNowAsync` Optimization (`SettingsController.vb`)**: Added `.ConfigureAwait(False)` to internal tasks and the `_ioLock` semaphore to prevent affinity with the UI thread SynchronizationContext.

## [1.0.3-beta] - 2026-09-22

### Pre-release / Beta — Non-Destructive WebView2 Profile Cleanup with Recycle Bin, Deferred Deletion, and Session Preservation
- **Configuration Integrity Validation and Accidental Deletion Prevention (`SettingsController.vb`, `AccountManager.vb`)**:
  - **Configuration Validity Tracking via `IsLoadedFromValidConfig` (#59)**: Introduced the `IsLoadedFromValidConfig` property in `SettingsController` to verify whether settings and accounts were loaded successfully from a valid `settings.json` or `.bak` backup file. Unreferenced profile cleanup is enabled only when valid configuration is confirmed, preventing any deletions when files are missing, corrupted, or falling back to defaults.
  - **Auto-Discovery and Recovery of Existing Profiles (#59)**: When `settings.json` is missing or newly regenerated, `CreateDefaultAccountAsync` scans for valid `WV2Profile_*` directories on disk, reconstructing accounts for all detected profile folders rather than defaulting to just the first one, preventing existing sessions from becoming orphaned.
- **Non-Destructive Recycle Bin Staging and Deferred Background Deletion (`AccountManager.vb`)**:
  - **Safe Staging to Recycle Bin `data/webview/_trash/` (#59)**: Replaced destructive immediate deletion with an atomic move (`Directory.Move`) of unreferenced profiles to a `_trash` folder with a unique timestamp (`WV2Profile_<id>_<timestamp>`). In case of transient configuration errors, session data and QR tokens remain safely preserved and recoverable.
  - **Deferred Deletion with 24-Hour Retention (#59)**: Disk reclamation for recycled folders occurs on a deferred basis strictly for folders older than 24 hours, utilizing progressive backoff retries (`DeleteDirectoryWithRetryAsync`).
  - **Off-UI-Thread Execution via `Task.Run` (#59)**: The entire profile scanning, indexing, and cleanup routine was moved to a background thread inside `Task.Run`, removing any UI stalls and ensuring immediate attachment of the active tab at launch.
  - **Non-Blocking Account Removal (#59)**: In `RemoveAccountAsync`, user-initiated account deletion first moves the profile folder to the recycle bin and delegates purge execution to a deferred background task, allowing Chromium to release file locks without hanging the UI.
- **Pre-existing Profile Preservation During Orphan Migration (`AccountManager.vb`, `AppAccounts.vb`)**:
  - **Conservative Rename to `.bak` (#59)**: In both `MigrateOrphanProfile` (`AccountManager.vb`) and `SetupWebViewInternalAsync` (`AppAccounts.vb`), encountering an anonymous orphan profile (`WV2Profile_`) alongside an existing profile folder no longer triggers destructive deletion; the existing folder is safely preserved by renaming it to `.bak` with a collision-avoidance timestamp, complete with defensive rollback if moving the orphan fails.
  - **`.bak` Folder Filtering in Transient Cache Cleanup (#59)**: Excluded folders ending with `.bak` from periodic `CleanupTransientCachesAsync` routines.

## [1.0.2-beta] - 2026-09-21

### Pre-release / Beta — Atomic and Serialized settings.json Writes with Backup Auto-Recovery and Shutdown Flush
- **Atomic and Serialized Settings Writes (`SettingsController.vb`, `AccountManager.vb`)**:
  - **Asynchronous `_ioLock` Semaphore (#58)**: Introduced a `SemaphoreSlim(1, 1)` in `SettingsController` to strictly serialize concurrent read and write operations on `settings.json`, eliminating I/O collisions between asynchronous account persistence and settings debouncing.
  - **Atomic Writing via Temporary File and Automatic Backup (#58)**: JSON persistence now writes to a temporary file (`settings.json.tmp`) first and is promoted via `File.Replace(tmp, targetFile, targetFile & ".bak")` while maintaining the latest valid `.bak` backup (with copy-and-move fallback). On failure or interruption, the temp file is deterministically cleaned up in the `Finally` block.
  - **Immediate Flush on Shutdown and Pre-Update (#58)**: Implemented `FlushNowAsync()` which cancels pending debounced writes (500 ms) and forces an immediate flush of `_dirty` settings to disk, awaiting lock release; invoked alongside `SaveAccountsAsync()` during window teardown (`MainWindow_Closing`), final exit (`ExitApplication`), and prior to restarting for OTA updates (`ForceExitForUpdateAsync` / `ForceExitForUpdate`).
- **Resilience and Auto-Recovery from JSON Corruption (`SettingsController.vb`, `Localization.vb`)**:
  - **Automatic Quarantine of Corrupted Files (#58)**: During `ReadSettingsAsync`, if `settings.json` is corrupted, truncated, or unparseable, the compromised file is automatically quarantined as `settings.corrupt-<timestamp>.json` for forensic diagnostics.
  - **Seamless Recovery from Backup (.bak) (#58)**: The system immediately attempts a transparent recovery from the latest valid `settings.json.bak` file; if successful, the session starts normally and informs the user via a modal dialog.
  - **Localized User Notification (#58)**: When falling back to default values or restoring from backup, users receive an explicit notification dialog localized across all 5 official languages (`it`, `en`, `fr`, `es`, `de`).
- **Documentation and GitHub Topics Update (`README.md`, `README.it.md`)**:
  - **Comprehensive OpenClaw and Hermes Agent Connection Guides**: Integrated exhaustive step-by-step guides into both `README.md` and `README.it.md` for configuring and connecting OpenClaw and Hermes Agent accounts, covering gateway URLs, security tokens, automatic Bearer token injection in WebView2, Tailscale mesh VPN integration (`tsnet`) with deterministic proxies for stable device identity, and validation via "Test Connection".
  - **GitHub Repository Topics and Search Tags**: Updated official GitHub repository topics (`openclaw`, `hermes`, `hermes-agent`, `ai-agents`, `ai-gateway`, `tailscale`) and added dedicated badges and search tag sections to README files to maximize discoverability.

## [1.0.1-beta] - 2026-09-20

### Pre-release / Beta — OTA Update Download Hardening and Fail-Closed Integrity Verification
- **OTA Update Download Hardening and Integrity Verification (`UpdateChecker.vb`, `Localization.vb`)**:
  - **Direct Disk Streaming and RAM Bloat Elimination (#56)**: Replaced in-memory downloading with a dedicated HTTP client (`_downloadClient`) configured with an infinite connection timeout and a 10-minute global `CancellationTokenSource`. The `.zip` payload streams directly to disk asynchronously (`ResponseHeadersRead` and `CopyToAsync`), accommodating large files and slow connections without memory overhead.
  - **Stream-Based Hash Computation (#56)**: SHA-256 cryptographic hash computation is streamed directly from disk via `SHA256.HashDataAsync(stream)` rather than buffering full byte arrays in RAM.
  - **Fail-Closed Integrity Validation (#57)**: Immediate, fail-safe termination of the update process if a verifiable SHA-256 hash cannot be found (either from the `.sha256` release asset or GitHub release body).
  - **Checksum Regex Scoped to Release Archive (#57)**: Scoped hash extraction regex strictly to the specific ZIP archive filename or dedicated prefixes (`SHA256:`), eliminating unsafe fallbacks on arbitrary hex strings.
  - **Authenticode Digital Signature Verification (#57)**: Added Authenticode digital signature validation on the extracted `HidaChat.exe` binary before applying updates, rejecting tampered or invalid files.
  - **Atomic Version Marker Write (#57)**: Removed premature calls to `WriteLocalVersionMarker` from C#/VB code; the `.app_version` marker file is now written solely by `update.bat` after robocopy execution finishes with full success.
  - **Localized Error Dialogs and Permission Guidance (#57)**: Updated insufficient-permission messages to suggest standard user-writable locations (`Documents`, `Desktop`, USB flash drive) aligning with the app's 100% portable design; synchronized all new error strings across 5 languages (`it`, `en`, `fr`, `es`, `de`).

## [1.0.0] - 2026-09-19

### Stable Release (General Availability Milestone) — 4 Platforms Supported (WhatsApp, Telegram, OpenClaw, Hermes), Consolidated Stability, and 100% Portability
- **Official v1.0.0 Milestone (Stable Release)**:
  - First official General Availability stable release of HidaChat: 100% portable Windows desktop client (.NET 9 / WPF) for messaging and AI agents, completely free from bloated frameworks like Electron and engineered for peak performance.
- **Consolidated Multi-Platform Support across 4 Ecosystems (`AppAccounts.vb`, `AccountManager.vb`, `SettingsWindow.xaml`)**:
  - **WhatsApp Web**: Isolated WebView2 profile, fast QR scan, instant messaging, Bulk Sender integration, and real-time message translation.
  - **Telegram Web**: Full Telegram Web A / K / Z support with accurate unread badge synchronization (`navigator.setAppBadge`), false positive suppression, and real-time notifications.
  - **OpenClaw (Autonomous AI Agent Gateway & Web UI)**: Comprehensive web console integration with transparent Tailscale mesh VPN connectivity powered by the `tsnetd.exe` companion daemon (static Go binary), deterministic local proxy ports (`18800+`), deterministic authentication tokens, and persistent Ed25519 device identities (eliminating recurrent `openclaw devices approve` prompts).
  - **Hermes Agent (Nous Research)**: Integrated AI dashboard and web console for Hermes agents, automated Bearer Token injection in WebView2, direct local (`http://127.0.0.1:9119`) or remote Tailscale Mesh VPN connectivity (`18900+`), with dedicated branding (cyan-blue winged helmet icon `#00B0FF`).
- **Real-Time Notification Badges and Reactive Red Dot Indicator (`MainWindow.xaml`, `AppAccounts.vb`, `Scripts/notification.js`)**:
  - Consolidated dynamic unread badges on account tabs (compact dot or pill with exact counts `1`, `2`, `99+`), dispatched asynchronously onto the WPF UI thread.
- **Staggered Preload and V8 Cache Preservation (`MainWindow.xaml.vb`, `AccountManager.vb`)**:
  - Immediate priority given to the active tab (ready in 1–2 seconds), staggered background warm-up of other accounts at 1.5s intervals to eliminate resource spikes, and persistence of V8 bytecode directories (`Code Cache`) for near-instant subsequent cold starts.
- **Resilience and Auto-Recovery (`AppAccounts.vb`)**:
  - Seamless handling of WebView2 runtime failures (`ProcessFailed` / `RenderProcessExited`) with painless automatic session recovery.
- **Personalized Bulk Sender Engine**:
  - Automated message dispatch from Excel (`.xlsx`, `.xls`) and CSV spreadsheets featuring smart column auto-mapping, dynamic placeholder interpolation (`{Nome}`, `{Azienda}`, etc.), and natural anti-spam throttling with a mandatory 30-second jitter minimum.
- **Comprehensive Documentation Updates (`README.md`, `README.it.md`, `ANALISI_PROGETTO.md`)**:
  - Fully documented all 4 supported platforms with detailed step-by-step setup guides for OpenClaw and Hermes Agent accounts (gateway parameters, auth tokens, Tailscale Mesh configuration).

## [0.9.5-beta] - 2026-09-18

### Pre-release / Beta — Full and Accurate Notification and Badge Support for Telegram Web
- **Native Badging API Integration (`Scripts/notification.js`)**:
  - Hooked standard web APIs `navigator.setAppBadge(count)` and `navigator.clearAppBadge()` utilized by Telegram Web PWAs to obtain the internally computed unread count with 100% precision.
- **Over-Counting and False-Positive Resolution for Telegram Web A (`Scripts/notification.js`)**:
  - Fixed ghost notification badges: Teact (Telegram Web A) renders a `<div class="ChatBadge">` container for every chat row (used for pins, reactions, or badges). The previous generic selector treated these empty containers as unread messages.
  - Tightened selectors to require genuine unread class states (`.ChatBadge.unread`, `.ChatBadge .Badge`, `.Badge.unread`, `.dialog-subtitle-badge`, `.rp .unread`, `.rp .badge`).
  - Explicitly excluded pinned chats (pin icons `.icon-pin`).
- **Platform-Isolated Scanning and Advanced Fallbacks (`Scripts/notification.js`)**:
  - Added folder tab fallbacks (`.folders-tabs .Tab .Badge`) to capture the unread total even when the chatlist is virtualized or collapsed.
  - Added unread favicon inspection fallback.
- **Diagnostics and Automated Tracking (`AppAccounts.vb`, `Scripts/notification.js`)**:
  - Automatic generation of the `telegram_debug.txt` diagnostic report detailing detected messages, channels, chat titles, and HTML classes.

## [0.9.4-beta] - 2026-09-17

### Pre-release / Beta — IPC JavaScript Scope Fix and Red Dot Badge Restoration
- **Critical IPC Token Scope Resolution (`Scripts/notification.js`)**:
  - Resolved `ReferenceError: __bridgeToken is not defined`: `const __bridgeToken` was scoped inside the initial `try` block, making it inaccessible (ES6 block scope) to `checkAndNotifyUnreadCount` and `scanOnlineStatus`. As a result, all `UNREAD_COUNT_CHANGED` and `ONLINE_STATUS_CHANGED` dispatches to the WPF host were failing silently.
  - Hoisted the declaration of `__bridgeToken` and the `activeCustomNotifications` map to the top level of the enclosing IIFE to guarantee visibility across the entire script.
- **DOM Scraping Optimization and Notification Heartbeat (`Scripts/notification.js`)**:
  - Removed restrictive `offsetParent === null` checks that discarded badges rendered by modern WhatsApp Web and Telegram layouts (e.g., `position: fixed`, SVG, or flexbox containers).
  - Hardened the unread extraction algorithm by strictly differentiating native numeric badges on Telegram and WhatsApp Web from generic chat aria-labels to prevent false counts.
  - Introduced a continuous periodic heartbeat (every 3 seconds) for accounts with unread messages to ensure the WPF host state remains synchronized.
- **XAML Visual Rendering Hardening (`MainWindow.xaml`, `AppAccounts.vb`)**:
  - Removed the hardcoded `Width` constraint on the WPF `UnreadBadge` control, allowing adaptive sizing: a compact circle (9×9 px) for non-numeric notifications and an auto-sizing horizontal pill with 8 px corner radius and padding when unread counts are present (`1`, `2`, `99+`).
  - Added the `OnlineTooltip` property for proper tooltip data-binding on contact online status.

## [0.9.3-beta] - 2026-09-16

### Pre-release / Beta — Unread Count Ingestion Fix and Real-Time Notification Detection
- **IPC KeyNotFoundException Fix (`AppAccounts.vb`)**:
  - Fixed a silent failure in `HandleNotificationMessageAsync` caused by unprotected calls to `root.GetProperty("id")`: `UNREAD_COUNT_CHANGED` and `ONLINE_STATUS_CHANGED` payloads lacked an `id` field, throwing a `KeyNotFoundException` that halted assignments to `UnreadCount`, `HasNotification`, and online status.
  - Replaced all JSON property access (`type`, `id`, `title`, `body`, `channel`, `bridgeToken`) with `TryGetProperty` and defensive fallbacks.
- **Title Interception and DOM Scraping Improvements (`Scripts/notification.js`)**:
  - Added a direct setter trap on `document.title` via `Object.defineProperty` on `Document.prototype`, ensuring zero-latency reaction to title mutations across SPA frameworks (WhatsApp Web and Telegram).
  - Broadened unread count regex to `[\(\[](\d+)\+?[\)\]]` (supporting patterns like `(1) WhatsApp`, `WhatsApp (1)`, `(99+) WhatsApp`, and `[2] Telegram`).
  - Enriched DOM selector matrices for Telegram A/K/Z and WhatsApp Web to capture both numeric counters and dot indicators without explicit digits.
  - Always included an auto-generated `id` in IPC payloads for full backwards compatibility.
- **Thread-Safety and WPF Badge Rendering Hardening (`AppAccounts.vb`, `MainWindow.xaml`)**:
  - `PropertyChanged` event dispatches for `UnreadCount`, `HasNotification`, `IsContactOnline`, and online status are now marshaled strictly onto the WPF UI Dispatcher.
  - Added the `UnreadBadgeWidth` property with explicit data-binding in `MainWindow.xaml` to preserve a perfect circle (9×9 px) when numeric text is absent.

## [0.9.2-beta] - 2026-09-16

### Pre-release / Beta — Red Dot & Account Tab Notification Badges
- **Red Dot & Account Tab Notification Badges (`MainWindow.xaml`, `AppAccounts.vb`)**:
  - Added a dynamic "red dot" visual indicator beside each account tab header to immediately flag incoming messages or unread notifications.
  - Adaptive styling: when numeric counts exist, the badge renders the number of messages (`1`, `2`, `99+`) in white against a red pill; for generic alerts, it scales down to a compact circular indicator.
  - Resolved WPF style property precedence conflicts by binding visibility directly to reactive `INotifyPropertyChanged` properties (`UnreadBadgeVisibility` and `OnlineDotVisibility`).

## [0.9.1-beta] - 2026-09-15

### Pre-release / Beta — Hermes Agent Support via Tailscale Mesh VPN & Local Gateway
- **Hermes Agent Platform Support (`AppAccounts.vb`, `AccountManager.vb`, `MainWindow.xaml.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`)**:
  - **Dashboard and Web Console Integration**: Native support for Hermes Agent instances (Nous Research) with isolated WebView2 tabs, fast switching, and dedicated profiles.
  - **Deterministic Endpoints and Ports**: Default endpoint `http://127.0.0.1:9119` and automatic allocation of deterministic local proxy ports starting at `18900+` to avoid collisions with other platforms.
  - **Secure Authentication**: Automated header injection (`Authorization: Bearer <AuthToken>`) via WebView2 `WebResourceRequested` filter for direct connections and tsnet loopback proxies, alongside query-string token support.
  - **Tailscale Mesh Network Integration (`tsnetd`)**: Seamless remote routing to Tailnet endpoints (`*.ts.net` or `100.x.y.z:9119`) via built-in `tsnetd` companion daemon authenticated with local `X-HidaChat-Local-Token`.
  - **Branding & Visual Identity**: Custom vector SVG icon (winged helmet of Mercury / Hermes AI) and distinctive cyan accent color `#00B0FF`.
  - **Interface and Settings**: Platform selector, contextual settings form with dedicated labels and tooltips, quick-add menu, and real-time HTTP connectivity tester.
  - **Multilingual Localization**: Complete translations across Italian, English, French, Spanish, and German (`Localization.vb`).

## [0.9.0] - 2026-09-14

### Stable Release — OpenClaw Support via Tailscale Mesh VPN, Staggered Preload & Cache Optimization
- **New Supported Platform: OpenClaw (`AppAccounts.vb`, `MainWindow.xaml.vb`, `SettingsWindow.xaml`, `AccountManager.vb`)**:
  - **Comprehensive OpenClaw Gateway and Web UI Integration**: Native support for provisioning and managing OpenClaw platform accounts within HidaChat, featuring dedicated tabs, quick switching, and isolated WebView2 profiles.
  - **Tailscale Mesh Private Network Integration (tsnet)**: Transparent, secure connectivity to OpenClaw gateways hosted on private Tailnets via the `tsnetd.exe` companion daemon (static Go binary embedding gVisor and WireGuard stacks), without requiring the Tailscale client installed on the host OS.
  - **OpenClaw Device Identity Persistence**: Allocation and permanent storage in `settings.json` of deterministic local proxy ports (`localProxyPort` starting at `18800`) and a local token stored in `data/tsnet/local_token.txt`. Cryptographic keys and session tokens in IndexedDB remain stable, eliminating recurrent `openclaw devices approve` authorization prompts.
  - **Race Condition Resolution and Secure Startup Handshake**: Synchronous wait for WireGuard handshakes (`tsReady`), retry transport to absorb initial network latencies, and smooth fallback splash with automated meta-refresh.
  - **Non-Blocking Asynchronous Initialization**: Background initialization of the `tsnetd` daemon without stalling UI rendering.
- **Multi-Account Startup Performance Optimization (`MainWindow.xaml.vb`)**:
  - **Staggered Preload**: The active account loads immediately on startup with top priority (interactive UI in 1–2 seconds), while secondary accounts are preloaded in the background staggered at 1.5-second intervals, eliminating CPU, memory, and bandwidth contention.
  - **Instant On-Demand Loading**: Clicking an account tab prior to scheduled background loading attaches and initializes the account immediately without latency.
- **Smart Web Cache Preservation & Startup Speedup (`AppAccounts.vb`, `AccountManager.vb`)**:
  - **V8 Bytecode and App Shell Persistence**: Preserved `Code Cache` (precompiled V8 bytecode), `Cache` (HTTP disk cache), and `Service Worker\CacheStorage` folders across restarts. WhatsApp Web and Telegram no longer re-download over 50 MB of code and assets on each app launch.
  - **Targeted Ephemeral File Cleanup**: Automated cleanup strictly targets crash dumps (`Crashpad`) and volatile GPU caches (`ShaderCache`, `GPUCache`), letting Chromium manage its disk cache up to a strict 100 MB ceiling.
- **System Stability and Resilience**:
  - Built-in transparent resilience against WebView2 runtime failures (`ProcessFailed` / `RenderProcessExited`) with instant auto-recovery for the active session (TODO #55).

## [0.8.2-beta] - 2026-09-08

### Pre-release / Beta — Multi-Account Startup Performance Optimization & Web Cache Preservation
- **Multi-Account Startup Performance Optimization (`MainWindow.xaml.vb`)**:
  - **Staggered Preload**: The active account loads and renders with top priority on startup, becoming interactive in 1–2 seconds. Secondary accounts preload in the background at staggered 1.5-second intervals, avoiding simultaneous CPU, RAM, and bandwidth spikes.
  - **Instant On-Demand Attachment**: Selecting an unprimed tab immediately attaches and renders the account on the fly.
- **Smart Web Cache Preservation (`AppAccounts.vb`, `AccountManager.vb`)**:
  - **Code Cache & App Shell Persistence**: Disabled aggressive deletion of `Default\Code Cache` (precompiled V8 bytecode), `Default\Cache` (HTTP disk cache), and `Default\Service Worker\CacheStorage` on startup and exit. WhatsApp Web and Telegram avoid re-downloading 50+ MB of bundle scripts on every session.
  - **Targeted Ephemeral Purge**: Automated purging focuses strictly on crash dumps (`Crashpad`) and temporary GPU caches (`ShaderCache`, `GPUCache`), letting Chromium regulate disk cache within the 100 MB cap.
- **Non-Blocking Asynchronous Tsnet Launch for OpenClaw (`TsnetManager.vb`)**:
  - Removed blocking 5-second sleep loops during `tsnetd.exe` startup. Tailscale node initialization now runs completely in the background without delaying tab initialization.

## [0.8.1-beta] - 2026-09-04

### Pre-release / Beta — OpenClaw Support & Tailscale Connection and Stability Enhancements
- **New Supported Platform: OpenClaw (`AppAccounts.vb`, `MainWindow.xaml.vb`, `SettingsWindow.xaml`)**:
  - **Comprehensive OpenClaw Gateway and Web Client Integration**: Native support for creating and managing OpenClaw accounts, featuring dedicated tabs, quick switching, and isolated WebView2 profile sandboxing.
  - **Tailscale Private Network Integration (tsnet)**: Transparent and secure connections to OpenClaw instances hosted on private Tailnets via built-in `tsnetd` daemon, eliminating the need to install the Tailscale OS client.
- **OpenClaw Device Identity Persistence (`AppAccounts.vb`, `AccountManager.vb`, `tsnetd/main.go`)**:
  - **Deterministic Proxy Port**: Assigned and permanently stored a fixed local TCP port in `settings.json` (`localProxyPort` starting at `18800`) for each OpenClaw account configured with Tailscale.
  - **Elimination of Recurrent Re-authorization Prompts**: In Chromium/WebView2, the Origin includes the local port (`http://127.0.0.1:<port>`). By holding the port constant across application restarts, Ed25519 cryptographic keys and session tokens stored in IndexedDB (`openclaw-control-ui`) remain valid, removing the need to re-approve the device on every launch (`openclaw devices approve`).
  - **Persistent Local Token**: The local IPC security token for `tsnetd` is generated once and persisted in `data/tsnet/local_token.txt` instead of rotating every session.
- **Race Condition and 502 Bad Gateway Fix on Startup (`tsnetd/main.go`, `TsnetManager.vb`)**:
  - **Tailscale Startup Synchronization (`tsReady`)**: In `tsnetd`, HTTP requests to the OpenClaw gateway are queued during initial boot until the WireGuard handshake completes (`tsServer.Up`), preventing WebView2 connection reset errors.
  - **Automated Retry Transport (`retryTransport`)**: Implemented transparent retries with 1-second backoff (up to 3 attempts) to absorb initial remote Tailnet handshake latencies.
  - **Startup Running State Check**: `TsnetManager.StartAsync` awaits the node entering `Running` state before navigating the OpenClaw tab.
  - **Fallback Splash Page with Auto-Refresh**: If temporary network delay occurs, the proxy responds with a dark-themed splash screen featuring a loading spinner and a 2-second meta-refresh.

## [0.8.0] - 2026-09-03

### Stable Release — Storage Optimization, Privacy Hardening & WhatsApp/Telegram Core Upgrades
- **Storage Optimization and Automated Startup/Shutdown Purge (`AppAccounts.vb`, `AccountManager.vb`, `MainWindow.xaml.vb`)**:
  - **Massive Disk Footprint Reduction**: Reduced data directory usage from over 500 MB to ~28 MB per profile via automated purging of ephemeral cache folders (`Code Cache`, `Disk Cache`, `Service Worker\CacheStorage`, `component_crx_cache`, `Subresource Filter`, `Crashpad`, `ShaderCache`).
  - **Zero Disconnections**: Strict preservation of `IndexedDB`, `Local Storage`, and `Cookies`, ensuring WhatsApp and Telegram logins persist without requiring QR code rescans.
  - **Automated Ingress/Egress Sanitization**: Asynchronous pre-startup purge before browser processes attach, paired with native WebView2 clearance (`ClearBrowsingDataAsync`) during shutdown.
- **Privacy Hardening and Chromium Parameter Tuning (`AppAccounts.vb`)**:
  - **Privacy Hardening & Zero Telemetry**: Disabled remote crash dump uploads (`--no-crash-upload`), disabled network telemetry (`--disable-domain-reliability`), and blocked silent background component updates (`--disable-component-update`).
  - **Prevented Shader Micro-File Accumulation**: Disabled disk shader cache (`--disable-gpu-shader-disk-cache`) and capped HTTP disk cache at 100 MB (`--disk-cache-size=104857600`).
- **WhatsApp Session Persistence & Telegram Web A Client (`AppAccounts.vb`, `Scripts/`)**:
  - **Reliable WhatsApp Persistence**: Fixed session loss caused by runtime User-Agent discrepancies.
  - **Migration to Telegram Web A**: Adopted modern Web A client (`https://web.telegram.org/a/`) with full `tg://` and `t.me` deep linking.
- **Dynamic Multi-Account Configuration (`AccountManager.vb`, `SettingsWindow.xaml`)**:
  - Configurable maximum account limit in Settings (up to 10 simultaneous accounts).
  - Resilience to WebView2 runtime crashes with seamless automatic tab recovery.

## [0.7.12-beta] - 2026-09-03

### Pre-release / Beta — Automated Full Cache Purge on Startup & Exit (Code Cache, Disk Cache & Service Worker)
- **Automated Cache Purge on Startup and Exit (`AppAccounts.vb`, `AccountManager.vb`, `MainWindow.xaml.vb`)**:
  - **High-Volume Folder Purge Expansion (`CleanTransientCacheFolders`)**: Expanded the cleanup routine to purge folders responsible for 90% of disk bloat (`Default\Code Cache`, `Default\Cache`, `Default\Service Worker\CacheStorage`, `Default\Service Worker\ScriptCache`, `component_crx_cache`, `Subresource Filter`, and `Crashpad`), reducing profile size from over 500 MB to ~28 MB without touching auth keys, sessions, or message databases.
  - **Native Disk Cache Clearing on Exit (`ClearBrowsingCacheAsync`)**: Introduced pre-teardown calls to WebView2 API `ClearBrowsingDataAsync(DiskCache)` prior to disposing controls.
  - **Startup & Shutdown Sanitization**: Coordinated asynchronous cache clearing at startup (`LoadAccountsAsync`) before process locks are acquired, and during teardown (`ExitApplication` and `ForceExitForUpdate`).

## [0.7.11-beta] - 2026-09-03

### Pre-release / Beta — WebView2 Storage Optimization, Pure Cache Purge & Chromium Hardening
- **Data Directory Disk Footprint Optimization (`AppAccounts.vb`, `AccountManager.vb`)**:
  - **Automated Ephemeral Cache Cleanup (`CleanTransientCacheFolders`)**: Introduced a targeted cleanup routine for non-critical pure cache folders (`GPUCache`, `ShaderCache`, `GrShaderCache`, `DawnGraphiteCache`, `DawnWebGPUCache`, `GPUPersistentCache`, and `Crashpad/reports`), fully preserving cookies, session tokens, and offline IndexedDB databases without requiring QR rescans.
  - **Pre-emptive Startup Sanitization (`CleanupTransientCachesAsync`)**: Asynchronous cleanup run at startup across all isolated profiles (`WV2Profile_*`) before WebView2 browser processes acquire file locks.
- **Chromium Browser Argument Tuning (`AppAccounts.vb`)**:
  - **GPU Shader Disk Cache Disabled (`--disable-gpu-shader-disk-cache`)**: Stopped continuous generation of thousands of Direct3D/GPU shader micro-files unnecessary for 2D messaging interfaces.
  - **Disk Cache Cap (`--disk-cache-size=104857600`)**: Enforced a 100 MB hard ceiling on HTTP disk cache to curb runaway temporary data growth.
  - **Disabled Unused Components and Telemetry**: Added flags `--disable-component-update`, `--disable-domain-reliability`, and `--no-crash-upload` to halt background component updates, telemetry, and non-essential crash uploads.

## [0.7.10-beta] - 2026-09-02

### Pre-release / Beta — WhatsApp Session Persistence & Telegram Web A Migration
- **WhatsApp Web Persistent Session Preservation (`AppAccounts.vb`)**:
  - **Native User-Agent Restoration**: Removed runtime dynamic User-Agent overrides that caused cryptographic fingerprint mismatches in WhatsApp Web and prompted session logouts/QR scans. Credentials and IndexedDB pairing tokens now remain durable and stable across sessions.
- **Telegram Web Migration to Official Web A Client (`AppAccounts.vb`)**:
  - **Adoption of Telegram Web A (`https://web.telegram.org/a/`)**: Replaced legacy Web K (`/k/`) with the modern official Web A client, overcoming endless loading loops, WebAssembly thread locking, and WebView2 compatibility issues.
  - **Deep Linking Support for `tg://` & `t.me`**: Updated Telegram Web A link resolution for channels, groups, messages, and invites.
- **Iframe Guard, Sandbox, and Script Helpers (`Scripts/notification.js`, `Scripts/translation.js`, `JsScripts.vb`)**:
  - Added `if (window.top !== window.self) return;` guard to all injected scripts (`notification.js`, `translation.js`, `telegram.css`) to prevent unwanted injection into nested iframes, widgets, or Telegram Web Worker frames.
  - Light/Dark theme synchronization (`theme-light`/`theme-dark` and `night`) tuned for Telegram Web A's React/TypeScript architecture.

## [0.7.9-beta] - 2026-09-02

### Pre-release / Beta — Telegram Web Compatibility & Notification Override Hardening
- **Telegram Web Chat Loading Freeze Fix (`Scripts/notification.js`, `AppAccounts.vb`)**:
  - **`window.Notification` Prototype Chain Preservation**: Restructured the `Notification` override to maintain complete prototype inheritance from `OriginalNotification.prototype` (`Object.setPrototypeOf`), standard constructors, and static properties, preventing type errors and MTProto/WASM initialization hangs on Telegram Web K and Telegram Web A.
  - **Safe Interception of `navigator.permissions.query`**: Replaced bound delegates with safe invocation via `Reflect.apply`/`apply` wrapped in `try...catch` blocks to prevent Blink's native `TypeError: Illegal invocation` during permission checks on startup.
  - **WebView2 User-Agent Sanitization**: Stripped the identifying `WebView2/...` token from User-Agent strings to ensure native desktop browser detection without restrictions or fallback landing pages.
  - **Chromium Browser Flag Tuning**: Removed flags that could restrict WebAssembly (`OptimizationHints`) and consolidated `--disable-features` arguments into a single formatted directive.
  - **DevTools Window Restoration (`AppAccounts.vb`)**: Re-enabled opening Developer Tools from the Settings button to facilitate diagnostics and runtime inspection.

## [0.7.8-beta] - 2026-09-02

### Pre-release / Beta — Dynamic Max Accounts, Lazy Directory Enumeration & Notification Tuning
- **Configurable Maximum Account Count (`AccountManager.vb`, `SettingsController.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`)**:
  - **Flexible Dynamic Limit**: Removed the hardcoded 3-account ceiling; introduced the configurable `MaxAccounts` property (selectable in Settings among 3, 5, 8, and 10 accounts, defaulting to 5) persisted in `settings.json`.
  - **Dynamic UI and Localization Updates**: Updated counter labels and limit warning strings (`accounts_count_info` and `max_accounts_reached`) with a `{max}` placeholder across all 5 supported languages (EN, IT, FR, ES, DE).
- **Profile Cleanup I/O Optimization via Lazy Enumeration (`AccountManager.vb`)**:
  - Replaced eager `Directory.GetDirectories` calls in `CleanupUnusedProfilesAsync` with lazy, memory-efficient `Directory.EnumerateDirectories` iterator, aligning the pattern across the codebase.
- **`HandleNotificationStateChanged` Consistency and Tuning (`AccountManager.vb`)**:
  - Eliminated dead code: `hasNotif` and `accountId` parameters are now used to promptly update the target account's state and compute `HasAnyNotification` reactively without redundant iterations.

## [0.7.7-beta] - 2026-09-02

### Pre-release / Beta — IPC Security Hardening, JSON Escaping & Lifecycle Resilience
- **IPC Security Hardening & DevTools Disabled in Release (`AppAccounts.vb`, `Constants.vb`)**:
  - **Build-Conditional DevTools**: `AreDevToolsEnabled` is conditionally compiled using `#If DEBUG` (enabled strictly in Debug builds for development, disabled in Release builds to protect the runtime, DOM, and prevent IPC message spoofing).
  - **Encapsulated and Guarded IPC Token**: Verified bridge token encapsulation inside IIFE closures in `notification.js` and `translation.js` without any exposure on the global `window.__bridgeToken` object.
- **Strict JSON Serialization & Universal Escaping (`JsScripts.vb`, `Scripts/translation.js`, `AppAccounts.vb`)**:
  - **Safe Translation Parameter Injection**: Replaced raw string interpolation and manual escaping (`.Replace("'", "\'")`) with `JsonSerializer.Serialize` for all parameters (`langCode`, `langName`, `tooltipLabel`, and empty fallback strings in `HandleTranslationMessageAsync` and `UpdateWebviewLanguageAsync`).
  - **JS Template Sanitization**: Removed static quotes in script templates to inject valid JSON literals directly.
- **FIFO/LRU Notification Queue Management (`AppAccounts.vb`)**:
  - Replaced the wholesale `Clear()` on `ActiveNotificationIds` upon hitting the capacity limit (500) with progressive FIFO eviction (`ActiveNotificationIds.Remove(oldest)`), avoiding mismatches between notification close events and unread badges.
- **Account Removal and Profile Teardown Resilience (`AccountManager.vb`)**:
  - **NullReference Guard**: Added defensive `_currentAccount IsNot Nothing` check before accessing `_currentAccount.Id` in `RemoveAccountAsync`.
  - **Robust Profile Folder Deletion**: Increased retry attempts to 10 with exponential backoff in `DeleteDirectoryWithRetryAsync` to ensure Chromium file locks release cleanly on slower storage or active antivirus scanners.

## [0.7.6-beta] - 2026-09-02

### Pre-release / Beta — WebView2 Process Crash Resilience & Auto-Recovery
- **WebView2 Process Crash Resilience & Seamless Auto-Recovery (`AppAccounts.vb`, `MainWindow.xaml.vb`, `TODO.md`)**:
  - **`CoreProcessFailed` and `ProcessFailed` Event Interception**: Implemented strongly typed process failure event handlers at both WPF control (`WebView.CoreProcessFailed`) and CoreWebView2 environment (`WebView.CoreWebView2.ProcessFailed`) levels.
  - **Tiered Process Failure Handling**:
    - *Renderer Crash (`RenderProcessExited`)*: Rapid automatic page reload (`Reload()`) without destroying the instance or disrupting user session state.
    - *Main Browser Crash (`BrowserProcessExited` / OOM / GPU reset / Edge update)*: Crash state detection (`IsCrashed = True`), safe disposal of the invalidated control, and transparent automatic regeneration (`RecreateAccountWebViewAsync`) with environment re-initialization and navigation.
  - **Account Switching Guards (`SwitchToAccountAsync` & `AccountTab_Click`)**: Added pre-switch validity verification and automatic recovery with seamless re-switching, preventing blocking error popups when background browser processes crash or are silently updated by Windows Update.
  - **`BtnReloadActiveTab_Click` Hardening**: Guarded active tab reload with automated fallback to re-instantiation if the process has become invalid.
  - **Resource Cleanup & Memory Leak Prevention**: Defensive detachment of all process listeners in `Dispose()` and application exit paths (`ExitApplication` / `ForceExitForUpdate`).

## [0.7.5-beta] - 2026-09-01

### Pre-release / Beta — Do Not Disturb (Focus Mode)
- **Timed "Do Not Disturb" / Focus Mode (TODO #47) (`MainWindow.xaml`, `MainWindow.xaml.vb`, `SettingsController.vb`, `AppAccounts.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`, `TODO.md`)**:
  - **Quick TitleBar Button & Context Menu**: Placed the `BtnDnd` button in the title bar with a muted bell icon (`Bell-Off`), active visual badge, and popup menu offering preset durations (*For 30 minutes*, *For 1 hour*, *For 2 hours*, *For 8 hours*, *Until turned off*).
  - **Total Silencing and Synchronous Audio Muting**: Immediate suppression of Windows Toast notifications and `MessagePopup` overlays across all accounts, accompanied by web audio muting (`CoreWebView2.IsMuted = True`).
  - **Timer with Automatic Restoration**: Periodic status polling with automatic deactivation upon duration expiry, displaying a tooltip indicating the end time (e.g., *"Do Not Disturb active until 16:30"*).
  - **Settings Controls & Full 5-Language Localization**: Dedicated controls in the Notifications section of *Settings* and precompiled native translations across all 5 supported languages (`EnStrings`, `ItStrings`, `FrStrings`, `EsStrings`, `DeStrings`).

## [0.7.4-beta] - 2026-08-30

### Pre-release / Beta — Native Multilingual Spellchecker
- **Native Multilingual Spellchecker (TODO #44) (`AppAccounts.vb`, `SettingsController.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`, `TODO.md`)**:
  - **Chromium / WebView2 Native Spellchecker Integration**: Enabled and configured the native spellchecker (`--enable-features=Spellcheck`, `--lang=...`) across all isolated WebView2 profile instances for WhatsApp Web and Telegram Web.
  - **Dedicated Settings Section**: New section in *Settings* with a toggle to enable/disable spellchecking and a dictionary language selector ComboBox (*Match app language*, *English*, *Italiano*, *Français*, *Español*, *Deutsch*).
  - **Persistence & Full Multilingual Support**: Automatic persistence of spellchecker preferences in `settings.json` with synchronized translations across all 5 official dictionaries (`EnStrings`, `ItStrings`, `FrStrings`, `EsStrings`, `DeStrings`).

## [0.7.3-beta] - 2026-08-28

### Pre-release / Beta — Multilingual Expansion (French, Spanish, German)
- **Full Support for New Interface Languages (Français, Español, Deutsch) (`Localization.vb`, `SettingsController.vb`, `SettingsWindow.xaml.vb`, `AppAccounts.vb`, `AGENTS.md`)**:
  - **Native Precompiled Dictionaries**: Added full dictionaries `FrStrings` (French 🇫🇷), `EsStrings` (Spanish 🇪🇸), and `DeStrings` (German 🇩🇪) for 60+ UI strings and elements (Settings, Account Management, Notifications, DevTools, Excel/CSV Bulk Sender, About, Custom CSS, Online Indicator).
  - **Settings Dropdown & Live Language Switching**: Expanded `SupportedLanguages` to include all 5 official languages (English, Italiano, Français, Español, Deutsch), with instant UI updates and synchronous language notification dispatched to all WebView2 instances without requiring a restart.
  - **Translation Maintenance Guidelines**: Enforced mandatory guidelines in `AGENTS.md` requiring synchronized updates across all 5 dictionaries whenever new UI strings or features are added.

## [0.7.2-beta] - 2026-08-28

### Pre-release / Beta — Online Contact Indicator & Custom CSS Injector
- **Contact Status Indicator: "Online" and "Typing..." (TODO #42) (`Scripts/notification.js`, `AppAccounts.vb`, `MainWindow.xaml`, `MainWindow.xaml.vb`)**:
  - **Continuous Real-Time Detection**: Automated detection of active chat contact status via `MutationObserver` and periodic DOM scraping for both **WhatsApp Web** (`#main header`, `[data-testid="conversation-header"]`, `span[title]`) and **Telegram Web K** (`.chat-info .person-status`, `.chat-info .status`, `.topbar .status`, `.chat-subtitle`).
  - **Dynamic TitleBar Badge**: Sleek title bar visual badge (`OnlineIndicatorBorder`) featuring a status dot (`#25d366` for WhatsApp, `#24A1DE` for Telegram) and localized label (e.g., *"online"*, *"typing..."*).
  - **Account Tab Status Dot**: Green dot (`TabOnlineDot`) displayed on the account tab when the current contact is active.
  - **Multi-Account Synchronization**: Instant, automatic updates upon switching tabs or changing conversations.
- **Full Support for User Custom CSS Themes (TODO #43) (`SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `SettingsController.vb`, `JsScripts.vb`, `AppAccounts.vb`)**:
  - **Monospace CSS Editor in Settings**: Dedicated section in *Settings* with a multi-line fixed-width editor (`TxtCustomCss`, Consolas / Cascadia Mono), border highlighting, and scrollbars for custom WhatsApp and Telegram Web CSS rules.
  - **Built-in One-Click Style Presets**: Quick insertion of popular style configurations:
    - *Pure Black OLED*: True black background (`#000000`) optimized for power-saving on AMOLED/OLED displays.
    - *Compact Layout*: Reduced sidebar width and tighter padding on chat bubbles.
    - *Modern Font*: Clean modern typography (`Segoe UI Variable Display` / system fonts).
    - *Clear*: Instant reset of the custom CSS input.
  - **Dynamic Real-Time Injection**: Instant application of CSS modifications across all active WebView2 instances without needing an application restart or page reload (`hidachat-custom-user-css`).
  - **Persistence & Multilingual Support**: Automatically persisted in `settings.json` with full localization across languages in `Localization.vb`.

## [0.7.1-beta] - 2026-08-27

### Pre-release / Beta — Telegram Advanced Evolution & Desktop Shortcuts
- **Notification Badges & Unread Message Counters for Telegram and WhatsApp (TODO #41) (`Scripts/notification.js`, `AppAccounts.vb`, `MainWindow.xaml`)**:
  - **Hybrid Real-Time Monitoring**: Combined `<title>` tag `MutationObserver` with targeted periodic DOM scraping to detect native unread badges in **Telegram Web K** (`.badge.unread`, `.unread-count`, `.chatlist-chat .badge`, `.dialog-subtitle .badge`) and **WhatsApp Web** (`[data-testid="unread-count"]`).
  - **Visual Badging on Account Tabs**: Displays a red badge pill with numeric counter (`"1"`, `"5"`, `"99+"`) or notification dot (`"•"`) directly over the account tab label.
  - **System Tray Icon Synchronization**: Instant automatic update of the Windows System Tray icon to reflect active notifications (`icon_notification.ico`).
- **Telegram Deep Link Handling (`tg://` and `https://t.me/`) (TODO #41) (`AppAccounts.vb`)**:
  - Intercepted `tg://` protocol links (`tg://resolve?domain=...`, `tg://resolve?phone=...`, `tg://join?invite=...`, `tg://msg_url?...`) and web links `https://t.me/...` or `https://telegram.me/...` in `NavigationStarting` and `NewWindowRequested`.
  - Automated internal resolution and routing to the **Telegram Web K** interface (`https://web.telegram.org/k/#@...` / `https://web.telegram.org/k/#?tgaddr=...`), opening channels, groups, and chats directly within HidaChat without external client dependencies or navigation errors.
- **Global Window Keyboard Shortcuts (TODO #41) (`MainWindow.xaml`, `MainWindow.xaml.vb`)**:
  - `Ctrl + 1`, `Ctrl + 2`, `Ctrl + 3` (and numpad): instant switch to Account 1, 2, or 3.
  - `Ctrl + Tab`: cycle to the next tab.
  - `Ctrl + Shift + Tab`: cycle to the previous tab.
  - `Ctrl + T` / `Ctrl + N`: quick-add a new account (if limit allows).
  - `Ctrl + R` / `F5`: reload active tab/platform.
  - `Ctrl + ,` (Comma): quickly open Settings.
  - `Ctrl + B` / `Ctrl + E`: immediately open the Bulk Sender window.
  - `F1` / `Ctrl + Shift + A`: open the About window.

## [0.7.0] - 2026-08-26

### Stable Release — Multi-Platform Bulk Sender, Security & Performance Hardening
- **Multi-Platform Personalized Bulk Sender for WhatsApp and Telegram (`BulkSenderWindow.xaml`, `BulkSenderWindow.xaml.vb`, `ExcelContactService.vb`, `BulkSenderEngine.vb`, `BulkContactItem.vb`)**:
  - **Import from Excel and CSV Files**: Comprehensive module to import contact lists from **Excel spreadsheets (`.xlsx`, `.xls`)** and **CSV files (`.csv`)** with smart, automated column mapping (*Phone*, *First Name*, *Last Name*, *Company*, *Custom Text*, *Username*).
  - **Multi-Platform WhatsApp Web & Telegram Web Support**: Asynchronous sequential messaging via WebView2 for both WhatsApp (`web.whatsapp.com/send`) and Telegram (`tg://resolve?domain=...` / `tg://resolve?phone=...`), with JavaScript injection for message drafting, formatting, and dispatch.
  - **Dynamic Templates & Placeholders**: Dynamic editor with one-click insertion of placeholder tags (`{Nome}`, `{Cognome}`, `{Azienda}`, `{Telefono}`, `{Username}`, `{Testo}`) and live real-time preview.
  - **Detailed Preview Inspector & 1:1 Fidelity**: Dedicated pane displaying the full rendered message with multi-line support, emojis (`📍`, `📌`, `🕒`, `🥂`, `🎼`, `🍽`, `🏨`, `🚌`), and automated custom text pickup from source spreadsheets.
  - **Anti-Spam Protection (Jitter Delay with Mandatory 30s Minimum)**: Configurable safety delay between message sends with a strict 30-second minimum enforced and live countdown to protect accounts from spam bans.
  - **Execution Controls & Advanced UI**: Pause, Resume, Stop controls, per-row status tracking (`Pending`, `Sending...`, `Sent ✔`, `Error ✖`, `Invalid`), resizable window support, full-screen mode, and platform-contextual theming.
- **SHA-256 Cryptographic Integrity Verification for OTA Updates (`UpdateChecker.vb`)**:
  - Automatic cryptographic verification of downloaded GitHub Releases ZIP archives using SHA-256 hashes prior to installation to guard against corrupted or tampered payloads.
- **IPC Bridge Token Security Hardening (`AppAccounts.vb`, `JsScripts.vb`, `Scripts/notification.js`, `Scripts/translation.js`)**:
  - Removed global exposure of `window.__bridgeToken`, confining security tokens within private IIFE closures of injected JavaScript scripts.
  - Secure, deterministic serialization of JavaScript parameters and commands using `JsonSerializer.Serialize`.
- **Multi-Account Performance & Preloading (`MainWindow.xaml.vb`, `Scripts/telegram.css`)**:

- Complete elimination of flickering and black screens when switching accounts thanks to parallel off-screen WebView2 preloading.
  - Dedicated styles for Telegram Web K and A with optimized responsive layout and light/dark theme synchronization.

## [0.6.7-beta] - 2026-08-25

### New Features & Multi-Platform Bulk Sender
- **Bulk Sender Support for Telegram Web (`BulkSenderEngine.vb`, `BulkContactItem.vb`, `ExcelContactService.vb`, `BulkSenderWindow.xaml`, `MainWindow.xaml.vb`)**:
  - Extended the Excel/CSV bulk sender module to work seamlessly with both **WhatsApp Web** and **Telegram Web**.
  - **@Username Handling**: Introduced `Username` / `CleanUsername` properties and the corresponding `{Username}` / `{User}` placeholder for Telegram, supporting automatic chat resolution via domain links (`tg://resolve?domain=...`) or phone numbers (`tg://resolve?phone=...`).
  - **Automatic Column Recognition**: The Excel and CSV parser automatically detects columns named *Username*, *@username*, *Utente*, *User*, *Nick*, *Handle*, *Telegram*.
  - **JavaScript Injection for Telegram Web K**: Handles opening the message composer, typing formatted text, simulating DOM events, and automatically clicking the send button.
  - **Contextual Multi-Platform GUI**: The window dynamically adapts with platform-specific icons and colors (Green `#00a884` for WhatsApp, Light Blue `#24A1DE` for Telegram).
- **Anti-Spam Protection (Minimum 30-Second Constraint) (`BulkSenderWindow.xaml`, `BulkSenderWindow.xaml.vb`, `BulkSenderEngine.vb`)**:
  - Introduced strict validation and enforcement of a minimum **30-second** interval between individual message sends (for both minimum and maximum delay), protecting WhatsApp and Telegram accounts from spam bans.

## [0.6.6-beta] - 2026-08-25

### Improved & UI Refinements
- **1:1 Text and Preview Consistency in Bulk Sender (`BulkSenderWindow.xaml.vb`, `BulkContactItem.vb`)**:
  - Implemented automatic detection of custom text upon opening an Excel/CSV file: if rows already contain a populated *Testo* (Text) column, the template is automatically set to `{Testo}`, ensuring the preview and final message match the source file text exactly character-by-character without unwanted prefixes.
- **Detailed Preview Inspector (`BulkSenderWindow.xaml`, `BulkSenderWindow.xaml.vb`)**:
  - Added a dedicated pane beneath the contacts table for instant inspection of the complete message associated with the selected row, with full support for multiline formatting, line breaks, links, and emojis (`📍`, `📌`, `🕒`, `🥂`, `🎼`, `🍽`, `🏨`, `🚌`).
- **Fullscreen and Resizing Support (`BulkSenderWindow.xaml`, `BulkSenderWindow.xaml.vb`)**:
  - Added minimize (`—`) and maximize/restore buttons to the title bar.
  - Added title bar double-click support to toggle normal/fullscreen mode and smooth dragging to restore.
  - Integrated the Win32 `WM_GETMINMAXINFO` message hook to constrain maximization to the monitor's Working Area, preventing overlap with the Windows taskbar.

## [0.6.5-beta] - 2026-08-24

### New Features & Bulk Automation
- **Personalized Bulk Messaging from Excel / CSV Files (TODO #52) (`BulkSenderWindow.xaml`, `BulkSenderWindow.xaml.vb`, `ExcelContactService.vb`, `BulkSenderEngine.vb`, `BulkContactItem.vb`)**:
  - Implemented a complete module for importing contact lists from **Excel (`.xlsx`, `.xls`)** and **CSV (`.csv`)** files, with automatic detection and smart mapping of columns (*Phone*, *First Name*, *Last Name*, *Company*, *Custom Text*).
  - Automatic normalization and cleanup of phone numbers with special character removal and international prefix formatting.
  - Message editor supporting **Dynamic Templates** and automatic substitution of placeholder tags (`{Nome}`, `{Cognome}`, `{Azienda}`, `{Telefono}`, `{Testo}`) with single-click quick-insert buttons and real-time preview in the contacts table.
  - **Asynchronous Sequential Dispatch Engine via WebView2**:
    - Direct navigation to chat on WhatsApp Web (`web.whatsapp.com/send`) with JavaScript injection for automated sending and loading state monitoring.
    - Automatic detection and dismissal of error popups for numbers not registered on WhatsApp (`INVALID_NUMBER`).
    - **Anti-Spam Protection (Jitter Delay)**: Configurable wait interval (Min/Max seconds) with natural randomized jitter between individual dispatches and real-time countdown display.
    - Full execution controls with **Pause / Resume**, **Immediate Cancellation**, and per-row state tracking (`Pending`, `Sending...`, `Sent ✔`, `Error ✖`, `Invalid`).
  - Added dedicated button with Excel icon in the title bar (`MainWindow.xaml`) for instant access from the main screen.
  - Updated Italian and English localization definitions in `Localization.vb`.

## [0.6.4-beta] - 2026-08-24

### Security & Integrity
- **SHA-256 Integrity Verification for Automatic Updates (TODO #40) (`UpdateChecker.vb`)**:
  - Implemented cryptographic integrity checks of the ZIP archive prior to file extraction and replacement during OTA updates from GitHub Releases.
  - Added automatic parsing and downloading of checksum assets attached to releases (`.sha256`, `.sha256sum`, `SHA256SUMS`, `SHA256SUMS.txt`, `checksums.txt`) and smart recognition of SHA-256 hashes specified in the release notes body.
  - Preventive blocking of updates with a security alert if the hash computed via `SHA256.HashData` does not match, preventing the installation of incomplete or tampered packages.

## [0.6.3-beta] - 2026-08-21

### Security & Hardening
- **Private Encapsulation of IPC Bridge Token (TODO #39) (`AppAccounts.vb`, `JsScripts.vb`, `Scripts/notification.js`, `Scripts/translation.js`)**:
  - Eliminated the exposure of `bridgeToken` on the global `window.__bridgeToken` object.
  - The security token is now injected and strictly confined within the private closures (IIFE) of `notification.js` and `translation.js`, protecting it from any script, extension, or code running in the DOM.
  - Encapsulated the internal `activeCustomNotifications` map within the private closure of `notification.js`.

## [0.6.2-beta] - 2026-08-20

### Fixed & Improved
- **Telegram Web Layout & Message Composer Fix (`Scripts/telegram.css`)**:
  - Resolved a layout bug in Telegram Web (K and A) where the chat composer and input field were positioned underneath the contacts/user sidebar (`#column-left`).
  - Removed forced width overrides (`width: 100% !important;`, `--columns-width: 100% !important;`, `--composer-width: 100% !important;`) to preserve the native responsive column layout and ensure proper docking of the message composer within the active conversation.

## [0.6.1-beta] - 2026-08-20

### Fixed & Improved
- **Multi-Account Preloading Fix & Black Screen Elimination (`MainWindow.xaml.vb`)**:
  - Resolved black screen freezing when switching between accounts (e.g. from Telegram to WhatsApp): inactive WebView2 instances now remain in the visual tree but are placed smoothly off-screen (`Margin = -20000` with `ZIndex = 0`), allowing Chromium to complete HWND initialization, negotiate WebSockets, and render pages in background in parallel.
  - Set `DefaultBackgroundColor` synchronized with the application theme to eliminate any black/white flickering on startup and tab switching.
  - Switching between WhatsApp and Telegram tabs is now instantaneous with zero latency (0 ms).

## [0.6.0] - 2026-08-20

### Stable Release - Full Multi-Platform & Performance Hardening
- **Fullscreen / Responsive Adaptation for Telegram Web (`Scripts/telegram.css`, `JsScripts.vb`, `AppAccounts.vb`)**:
  - Resolved an issue where Telegram Web did not utilize the full screen width: introduced a dedicated `telegram.css` stylesheet removing width constraints (`--columns-width`, `--chat-width`, `max-width: none`) on primary containers (`#column-center`, `.chat`, `.chat-container`, `.chat-layout`, `#MiddleColumn`, `.messages-layout`, `.bubbles-inner`).
  - Automatic and persistent injection on document load (`TelegramInitJS`) and light/dark theme synchronization.
- **Instant Account Switching & Multi-Account Preloading (`MainWindow.xaml.vb`)**:
  - In-memory preloading of all configured WebView2 instances: immediate switching between WhatsApp and Telegram tabs with zero latency.
- **WebView2 Profile Deletion Optimization (TODO #37) (`AccountManager.vb`)**:
  - Implemented `DeleteDirectoryWithRetryAsync` with progressive backoff to ensure reliable deletion of orphaned or removed profile folders even when locked transiently.
- **JavaScript Escaping Hardening via `ExecuteScriptAsync` (TODO #38) (`AppAccounts.vb`, `MainWindow.xaml.vb`)**:
  - All parameters and identifiers interpolated into JavaScript scripts are now deterministically and safely serialized via `JsonSerializer.Serialize`.
- **Taskbar Integration & Win32 Window Handling (`MainWindow.xaml`, `MainWindow.xaml.vb`)**:
  - Fixed window maximization via Win32 `WM_GETMINMAXINFO` hook, preserving the Windows taskbar.
- **Continuous Background Notifications & Message Reception (`AppAccounts.vb`, `notification.js`)**:
  - Disabled timer throttling and occluded window backgrounding to ensure instantaneous message reception and toast/popup notifications across all accounts.

## [0.5.2-beta] - 2026-08-20

### Fixed & Improved
- **Fullscreen / Responsive Adaptation Fix for Telegram Web (`Scripts/telegram.css`, `JsScripts.vb`, `AppAccounts.vb`)**:
  - Resolved an issue where Telegram Web failed to adapt to fullscreen: created a dedicated `telegram.css` stylesheet that removes fixed width constraints (`--columns-width`, `--chat-width`, `max-width`) on primary containers (`#column-center`, `.chat`, `.chat-container`, `.chat-layout`, `#MiddleColumn`, `.messages-layout`, `.bubbles-inner`).
  - Automatic and persistent injection on document load (`TelegramInitJS`) and light/dark theme synchronization.
- **WebView2 Profile Deletion Optimization (TODO #37) (`AccountManager.vb`)**:
  - Implemented `DeleteDirectoryWithRetryAsync` with progressive backoff to guarantee reliable deletion of orphaned or removed profile folders even in the presence of transient file locks from Chromium processes or antivirus software.
- **JavaScript Escaping Hardening via `ExecuteScriptAsync` (TODO #38) (`AppAccounts.vb`, `MainWindow.xaml.vb`)**:
  - All parameters and identifiers interpolated into JavaScript scripts are now deterministically and safely serialized using `JsonSerializer.Serialize`, preventing special character or quote injection.

## [0.5.1-beta] - 2026-08-19

### Fixed & Improved
- **Restoration of Windows Taskbar Visibility on Maximization (`MainWindow.xaml`, `MainWindow.xaml.vb`)**:
  - Implemented a Win32 hook (`HwndSourceHook`) for the `WM_GETMINMAXINFO` message to constrain window maximization to the monitor's Working Area (`rcWork`).
  - The Windows Taskbar remains visible and accessible even when the window is maximized to fullscreen.
  - Dynamic adaptation of borders (`BorderThickness`), corner radii (`CornerRadius`), and outer shadow (`DropShadowEffect`) based on window state (maximized/normal).
  - Added interactive resizing via the bottom-right grip and smooth dragging to restore the window from its maximized state.
- **Instant Account Switching & Multi-Account Preloading (`MainWindow.xaml.vb`)**:
  - On application startup, WebView2 controls are now instantiated and preloaded for all configured accounts in parallel.
  - Upon switching tabs, inactive views remain active in memory (`Visibility.Hidden`), completely eliminating wait times and cold reloads when switching between WhatsApp and Telegram accounts.
- **Background Notifications & Message Reception (`WhatsAppAccount.vb`, `Scripts/notification.js`)**:
  - Removed background networking disable parameters (`--disable-background-networking`) and enabled Chromium operational continuity flags (`--disable-background-timer-throttling`, `--disable-backgrounding-occluded-windows`, `--disable-renderer-backgrounding`).
  - Background accounts continuously maintain active WebSocket connections and JavaScript timer execution, guaranteeing instant message reception even while using another account or when the window is minimized.
  - Strengthened `notification.js` with permission overriding via `navigator.permissions.query`, forced getter on `Notification.permission`, and dynamic monitoring of page title changes (`MutationObserver`).

## [0.5.0] - 2026-08-18

### Multi-Platform & Rebranding Major Release
- **Official Rebranding to HidaChat**:
  - Evolution of the application from an exclusive WhatsApp client to a unified multi-platform client (**HidaChat**).
  - Complete renaming of solution, projects, namespaces, and executables.
- **Multi-Account Support for WhatsApp and Telegram**:
  - Ability to concurrently manage WhatsApp and Telegram accounts in separate tabs with isolated profiles.
  - Platform selection during account creation and configuration in Settings.
  - Dedicated vector icons (green for WhatsApp, light blue for Telegram) in top tabs and account management.
- **Multi-Platform Light/Dark Theme Synchronization (`JsScripts.vb`, `WhatsAppAccount.vb`, `MainWindow.xaml.vb`)**:
  - Introduced dedicated `TelegramDarkModeJS` and `TelegramLightModeJS` scripts in `ThemeJsScripts` for dynamic synchronization of the system/WPF theme with the Telegram Web interface (`web.telegram.org/k/`).
  - Management of the `.night` class on `document.documentElement` and `body`, updating `tt-theme` and `theme` keys in `localStorage`, and integration with `themeController.setTheme`.
  - Targeted and differentiated styling based on platform type (`IsTelegram` / `IsWhatsApp`) both on startup/navigation (`NavigationCompleted`) and on real-time theme changes in Settings (`ApplyWpfThemeAsync`).
- **Multi-Platform Message Translation Engine (`translation.js`)**:
  - Extended `getMessageText` and listeners to recognize both WhatsApp containers (`[data-testid="msg-container"]`, `.selectable-text`, `.quoted-message`) and native Telegram Web K elements (`.bubble`, `.message`, `.text-content`, `.reply`, `.reply-content`).
  - Optimized hover translation button positioning for outgoing/incoming bubbles (`.is-out`, `.is-in`) and adapted accent colors (`#24A1DE` on Telegram, `#00a884` on WhatsApp).
  - Full support for dark theme detection across both platforms for the translation bubble and built-in full-page translation support (`scanAndTranslateDOM`).
- **Improved Popup Notification Routing (`MessagePopup.xaml.vb`)**:
  - Clicking a visual notification popup now automatically selects the account tab that received the message (`SwitchToAccountAsync`), ensuring consistency with Windows Toast notification behavior.
- **Localization Refinement (`Localization.vb`)**:
  - Updated labels to consistently and uniformly reflect the multi-platform nature of the application.
- **New Cyberpunk Visual Design**:
  - New application icon in cyberpunk neon style featuring a holographic chat bubble and digital circuitry.
  - New dedicated System Tray icon with an illuminated badge for active notifications.
- **Architecture & Portability**:
  - Complete isolation of all data files, settings, and WebView2 profiles inside the portable `data/` folder.
  - Smooth transition and forwarder for automatic updating from legacy versions.

## [0.4.2-beta] - 2026-08-18

### Multi-Platform (WhatsApp & Telegram)
- **WhatsApp & Telegram Account Support**:
  - Added platform type selection (`WhatsApp` or `Telegram`) during account creation and editing in Settings.
  - Dedicated vector icons (green for WhatsApp, light blue for Telegram) displayed on account tabs in the main bar and in the account management list.
  - Contextual quick menu on the `+` button and on *"Add Account"* to instantly select the desired platform.
  - Automatic URL routing (`https://web.whatsapp.com/` and `https://web.telegram.org/k/`) with isolated profiles, notification handling, and translation support.

## [0.4.1-beta] - 2026-08-18

### Visual & Assets
- **New Cyberpunk Icon & System Tray Notifications**:
  - Introduced new cyberpunk icon set featuring a neon holographic chat bubble (cyan/magenta) with digital traces on a dark background.
  - Generated complete multi-resolution `.ico` files (16x16 through 256x256) for the main application (`icon.ico`) and for System Tray notifications with an illuminated badge (`icon_notification.ico`).
  - Integrated bridge executable and automatic cleanup routines for smooth update transitions from previous versions.

## [0.4.0-beta] - 2026-08-18

### Rebranding & Multi-Platform Evolution
- **Complete Rebranding from WhatsAppH to HidaChat**:
  - Renamed the entire project, .NET solution (`HidaChat.sln`), project file (`HidaChat.vbproj`), and executable (`HidaChat.exe`) in preparation for multi-platform client evolution (WhatsApp, Telegram, Teams, etc.).
  - Updated XML namespaces and code-behind to `HidaChat`.
  - Updated single-instance Mutex to `Local\HidaChat_SingleInstance_Mutex`.
  - Updated all OTA update endpoints and links to the new official GitHub repository `https://github.com/hidaba/HidaChat`.
  - Updated GitHub Actions workflows, `publish.ps1` script, issue templates, and complete documentation.

## [0.3.3-beta] - 2026-08-18

### Portability & Refactoring
- **TODO Item 36 — Move `settings.json` and `translations_cache.json` to `data/` (`SettingsController.vb`, `Localization.vb`)**:
  - Configuration (`settings.json`) and translation cache (`translations_cache.json`) files are now managed and saved directly within the portable `data/` folder.
  - Implemented automatic and transparent migration of existing files in the root folder (`File.Move`) to `data/` on first launch, ensuring backward compatibility with previous installations while keeping the application root clean.
  - Added proactive creation of the destination `data/` directory for fresh portable installations.

## [0.3.2-beta] - 2026-08-17

### Performance & Memory
- **TODO Item 35 — Lazy Loading of JS/CSS Scripts (`JsScripts.vb`, `Scripts/`)**:
  - Extracted JavaScript and CSS scripts into dedicated assembly embedded resources (`EmbeddedResource`): `theme.css`, `notification.js`, and `translation.js`.
  - Rewrote `JsScripts.vb` to load templates lazily via `Lazy(Of String)` and perform in-place replacements using `StringBuilder`, eliminating all permanent static XML literals from RAM.
- **Translation Persistence on Disk (`translations_cache.json`)**:
  - Implemented `TranslationCacheService` to permanently store on disk (`data/translations_cache.json` with fallback in root) both UI translations and translated chat texts/messages.
  - Added proactive cache lookup prior to each translation request (single or batch): already translated texts are returned instantly from cache without making network calls to Google Translate.
  - In full-page batch translations (`translatePage`), only new sentences not yet present in cache are sent to Google Translate, drastically reducing bandwidth consumption.
- **RAM Memory Optimization for Translations**:
  - Only dictionaries for the currently selected language are kept in RAM.
  - When changing languages in `SettingsController`, previous language data is persisted to disk and removed from memory for garbage collection, loading only the newly selected language into RAM.

## [0.3.1-beta] - 2026-08-15

### Performance & Memory
- **TODO Items 10 & 32 — Optimization and WeakReference in Notification Popups (`MessagePopup.xaml.vb`)**:
  - Implemented `WeakReference(Of MessagePopup)` in the active popups static list (`_activePopups`) with automatic cleanup of GC-collected references, preventing memory leaks from abnormally closed popups.
  - Replaced forced recalculation and repositioning of all active popups with incremental Y-position calculation (`PositionNewPopup()`), stacking new notifications upward without repositioning or moving popups already visible on screen.
- **TODO Item 11 — Account Switching Optimization (`MainWindow.xaml.vb`)**:
  - Optimized `SwitchToAccountAsync()` by returning early if the requested account is already active and hiding only the `WebView` control of the previous account without iterating through the entire control hierarchy.

## [0.3.0] - 2026-08-13

### Added & Features
- **Multi-Account Support up to 3 Accounts**: Integrated management and fast tabs for switching and adding accounts in real time (`MainWindow.xaml`, `AccountManager.vb`).
- **About Window (AboutWindow)**: New modal interface with version details, Apache-2.0 license, portable path, and quick links (`AboutWindow.xaml`).
- **Automatic Cleanup of Orphaned Profiles**: Startup scanning and deletion of profile directories no longer in use (`AccountManager.vb`).
- **Bilingual Documentation & Community Standards**: Integrated `README.md` (EN) and `README.it.md` (IT), `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, and GitHub Actions workflow for automated builds.

### Fixed & Improved
- **OTA Update Robustness**: Proper handling of pre-release suffixes (`-beta`, `-rc`) and stability checks during version comparison (`UpdateChecker.vb`).
- **Toast Notification Handling**: Resolved crashes and abnormal behavior when clicking Windows notifications (`MainWindow.xaml.vb`).
- **WPF Brush Performance**: High-performance caching via the `BrushCache.vb` module.
- **Real-Time Account Renaming**: Account name changes in settings are reflected instantly across the interface.

## [0.2.5-beta] - 2026-08-10

### Fixed
- **OTA Version Comparison and Pre-releases Fix (`UpdateChecker.vb`)**: Refined version comparison in `IsNewerVersion` by separating base numeric components from pre-release suffixes (e.g. `-beta`, `-rc1`), properly detecting beta updates against previous versions.

## [0.2.4-beta] - 2026-08-08

### Fixed
- **Orphaned WebView2 Profile Cleanup (`AccountManager.vb`)**: Implemented `CleanupUnusedProfilesAsync` method to scan the `data/webview` folder and automatically delete orphaned profile folders (`WV2Profile_*`) at startup whose IDs are no longer associated with any active account.
- **Badge and Link Corrections (`README.md`, `README.it.md`)**: Corrected build badges and commit links to point to the `master` branch and removed build instructions containing invalid paths.

## [0.2.3-beta] - 2026-08-07

### Fixed
- **Toast Notification Crash Fix (MainWindow.xaml.vb)**: Resolved NullReferenceException when clicking Windows Toast notifications with missing or malformed arguments using the `ExtractArg` helper.
- **Toast Notification Click Fix (MainWindow.xaml.vb, MessagePopup.xaml.vb)**: Replaced `ToggleWindow()` with `ShowWindow()` call in the Toast notification handler to prevent clicking from hiding the window if it was already visible in the foreground.

## [0.2.2-beta] - 2026-08-06

### Added
- **Multi-Account Support up to 3 WhatsApp Accounts**:
  - Added `+` button in the horizontal tab bar in `MainWindow.xaml` for instant creation and switching to new accounts.
  - Added account control and counter (`Configured accounts: X of 3`) in `SettingsWindow.xaml`.
  - Implemented strict maximum limit validation in `AccountManager.vb` (`MaxAccounts = 3`, `CanAddAccount`).
  - Complete isolation of WhatsApp Web sessions and WebView2 profile data on disk (`WV2Profile_<id>`).
  - Added multilingual localization strings for limits and account management actions.
- **"About" Window (AboutWindow)**:
  - Created modal window `AboutWindow.xaml` accessible via the new `ⓘ` button in the title bar, from Settings, and from the tray icon context menu.
  - Displays author (`Massimo Balestrieri`), version (`v0.2.2`), release date (`2026-08-06`), license (`Apache-2.0`), runtime (`.NET 9` + `WebView2`), portable data path (`data/webview`), and direct links to GitHub, Releases, and bug reporting.

### Performance & Refactored
- **TODO Item 29 (`AccountManager.vb`)**: Eliminated anonymous type projection on every settings save. Direct serialization of the `_accounts` collection to reduce Garbage Collector pressure.
- **TODO Item 7 (`TODO.md`)**: Closed and marked obsolete item 7 regarding OTA updates via UNC network paths (`ReadVersionFromFileAsync()`), as network path updates were deprecated and entirely replaced by GitHub Releases REST APIs as of v0.2.0.

### Fixed
- **Real-Time Account Name Update in Tabs (`WhatsAppAccount.vb`)**: Added `PropertyChanged` event raising for the `Name` property in `WhatsAppAccount.vb`. Account renaming in Settings is now instantly reflected on tab buttons in the main window without restarting or reopening the application.

## [0.2.1-beta] - 2026-08-06

### Added & Refactored
- **Repository Visibility & Security Improvements**: Removed references to legacy IP and network paths in `ANALISI_PROGETTO.md`. Aligned license badge and text to Apache 2.0.
- **Bilingual Documentation & Graphic Assets**: Added `README.md` (English) and `README.it.md` (Italian) files with language selector, "Why HidaChat" comparison table, screenshot gallery (`images/`), and prerequisites.
- **Community Standards & CI/CD**: Introduced `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md` (v2.1), `SECURITY.md`, bug report and feature request templates, and GitHub Actions workflow `.github/workflows/build.yml` for automated `.NET 9` compilation.

## [0.2.0] - 2026-08-05

### Changed & Refactored
- **Centralization of OTA Updates on GitHub Releases**: Removed dependency and fallback logic for the deprecated local network path (`UpdateFilesPath`). The automatic OTA update system now operates exclusively via GitHub Releases REST APIs with background verification, download, and installation of the ZIP package.
- **Beta Channel (Pre-releases) Optimization**: Refined release scanning in the Beta channel (`UseBetaChannel`) in `UpdateChecker.vb` by iterating through releases to select the first available ZIP asset.
- **Publish Script Automation**: Updated `publish.ps1` to support automated building, ZIP packaging, and publishing of both stable and beta releases (with `-Beta` flag and `--prerelease` on GitHub).
- **Exclusion of AI Rule Files from Repository**: Added `.gitignore` rules to exclude `AGENTS.md` and `.agents/` from public Git tracking, preserving project memory and guidelines locally.

## [0.1.8-beta] - 2026-07-31

### Performance & Portability
- **Disk I/O and Portable Chromium Startup Optimization**: Configured WebView2 `AdditionalBrowserArguments` environment options in `WhatsAppAccount.vb` (`--disk-cache-size=104857600 --media-cache-size=52428800 --disable-background-networking --disable-features=Translate,OptimizationHints,MediaRouter`). Limits loose cache file proliferation while keeping the application 100% portable within the `data/webview` folder.

## [0.1.7-beta] - 2026-07-29

### Fixed
- **OTA Update Freeze and Single-Instance Mutex Fix**: Resolved an issue where the application froze on restart during OTA updates and could not be reopened after a forced close (requiring a PC reboot). Implemented handling for `AbandonedMutexException` with fallback to active process checking in `Application.xaml.vb`, included `taskkill` for stuck processes and removed `pause` in `update.bat`, and ensured proper release of single-instance Mutex and WebView2 controls in `ForceExitForUpdate()`.

## [0.1.6-beta] - 2026-07-29

### Performance
- **TODO Item 34**: Implemented `BrushCache` module with `ConcurrentDictionary` and `.Freeze()` for thread-safe, high-performance caching of `SolidColorBrush` instances. Eliminated continuous reallocations of WPF brushes upon every theme change or visual adjustment in `MainWindow.xaml.vb` and `SettingsWindow.xaml.vb`.

## [0.1.5-beta] - 2026-07-28

### Fixed & Performance
- **TODO Item 31**: Used `StringBuilder` in `UpdateChecker.vb` for assembling the `update.bat` update batch script.
- **TODO Item 33**: Centralized Windows system theme detection via the new `SystemThemeHelper` module with caching of the `AppsUseLightTheme` registry key and dynamic listening to `SystemEvents.UserPreferenceChanged`. Eliminated triplicate Registry reads across `MainWindow.xaml.vb`, `WhatsAppAccount.vb`, and `SettingsWindow.xaml.vb`.

## [0.1.4-beta] - 2026-07-28

### Changed
- **Git Commit Message Formatting**: Removed version prefix from Git commit messages on GitHub to keep file change descriptions clean.

## [0.1.3] - 2026-07-28

### Added
- **Download Button and Section in README.md**: Added section with direct links and badge to download the portable Windows executable from GitHub Releases.

### Fixed & Performance
- **TODO Item 30**: Replaced `Directory.GetDirectories` with `Directory.EnumerateDirectories` and `.FirstOrDefault()` in `AccountManager.vb` for lazy streaming discovery of orphaned profiles.

## [0.1.2-beta] - 2026-07-28

### Added
- Added XML doc documentation (`<summary>`, `<param>`, `<returns>`) and explanatory comments across all VB.NET source files in the project (`AccountManager`, `WhatsAppAccount`, `MainWindow`, `SettingsController`, `SettingsWindow`, `UpdateChecker`, `Localization`, `JsScripts`, `MessagePopup`, `Application`, `Constants`).

### Fixed & Refactored
- **TODO Item 25**: Reconverted `ApplyWpfTheme` into an asynchronous function (`ApplyWpfThemeAsync() As Task`), adding `Await` and `Try...Catch` exception handling to all `ExecuteScriptAsync` invocations in `MainWindow.xaml.vb`.

## [0.1.1-beta] - 2026-07-27

### Removed
- Completely removed `annoni.png` image from the repository to reduce compiled package size.
- Configured OTA release on GitHub Releases.

## [0.1.0] - 2026-07-27

### Changed
- Removed top logo from title bar in `MainWindow.xaml`.
- Removed entire chat backup and synchronization system (`ChatSyncBackgroundWorker`, `ChatJsonStorageService`, `CryptoHelper`, dedicated constants and JS scripts).
- Updated application version to `0.1.0`.

## [0.0.58] - 2026-07-24

### Performance
- **`GetTranslationJS` Optimization in `JsScripts.vb`**: Replaced the chain of 12 `.Replace()` calls on in-memory strings with in-place `StringBuilder.Replace()` for translation JS generation, eliminating intermediate allocations in the Large Object Heap (LOH).

## [0.0.57] - 2026-07-24

### Fixed
- **Line Deduplication in Backup File**:
  - Implemented automatic message deduplication by unique `id` in `ChatJsonStorageService.vb`. Each message is saved exactly once, eliminating duplicate lines in backup files.
- **Sender Extraction in Groups and Phone Numbers**:
  - Fixed an issue where group messages were saved with the group name instead of the sender's name.
  - Implemented reading of `data-pre-plain-text` and sender nodes (`span._ao3e`, `span._ap4a`) in DOM bubbles to extract the message author in groups (e.g. `Timmití`, `Marco`, etc.).
  - Improved phone number parsing from `data-id` JIDs (`..._393...@c.us`) and from `msg.author` to reliably populate the `senderPhone` field.

## [0.0.56] - 2026-07-24

### Fixed
- **Contact Name and Phone Number Extraction in Backups**:
  - Fixed a bug in the DOM fallback of `extractMessagesFromDOMAsync()` that erroneously captured the side panel title `"Profile details"` as the chat name, leaving `senderPhone` empty.
  - Corrected header selector to target `#main header` strictly while filtering out system labels.
  - Implemented automatic extraction of phone numbers from `chatId` (`39...` from `@c.us`) and contact names from sender/contact in JS for both Store and DOM fallback modes.
  - Added server-side security enrichment in `ChatJsonStorageService.vb` to guarantee that `senderPhone` and `senderName` are always populated with real contact data in every backup JSON line.

## [0.0.55] - 2026-07-24

### Performance
- **Lazy Enumeration in `UpdateChecker.vb`**: Replaced `Directory.GetFiles` with `Directory.EnumerateFiles` when copying files from the OTA repository. Streaming enumeration avoids eager allocation of large string arrays in memory across UNC network shares.

## [0.0.54] - 2026-07-24

### Added
- **Backup Encryption Option in `Constants.vb`**: Added `EnableBackupEncryption As Boolean` constant (defaults to `True`).
  - If `True`: Backups saved to disk are encrypted in AES-256-GCM format (encrypted filenames and JSON contents).
  - If `False`: Backup files and chat index are saved in plaintext (standard readable JSON format in the `Chats_Plain` folder).

## [0.0.53] - 2026-07-24

### Fixed & Performance (Memory & Cleanup)
- **IDisposable on WhatsAppAccount**: Managed lifecycle of WebView2 and its dependencies upon account removal.
- **Unsubscribe CoreWebView2 Handlers**: Registered handlers `PermissionRequested`, `NewWindowRequested`, `WebMessageReceived`, and `NavigationCompleted` are now detached via `RemoveHandler` upon account destruction to prevent memory leaks.
- **IDisposable on Storage and SyncWorker**: Implemented `IDisposable` on `ChatJsonStorageService` and `ChatSyncBackgroundWorker` to release the OS resource `SemaphoreSlim` (`_fileLock`).
- **Async/Await in SyncWorker**: Eliminated `.GetAwaiter().GetResult()` in `ProcessIncomingBatchAsync`, making message batch processing fully asynchronous and preventing `ThreadPool` starvation.
- **Active Notifications Limit**: Introduced an upper bound (500 items) for `ActiveNotificationIds` in `WhatsAppAccount` with automatic purging.
- **ArrayPool & Streaming in CryptoHelper**: Utilized `ArrayPool(Of Byte).Shared` for temporary buffers in `EncryptBytes`/`DecryptBytes` and implemented chunked streaming encryption for large media files.
- **Shared HttpClient in Localization**: Unified `HttpClient` management into a single shared instance to prevent socket exhaustion.
- **ObservableCollection for Accounts**: Replaced `List(Of WhatsAppAccount)` with `ObservableCollection` to prevent forced regeneration of WPF DataTemplates upon account removal.
- **Dirty Tracking**: Added `_isDirty` flag in `AccountManager` to skip disk writes when data remains unchanged.
- **MainWindow Lifecycle & Timer**: The background sync timer `_syncTimer` and `PropertyChanged` event handlers are cleanly unsubscribed and stopped upon application exit.

## [0.0.52] - 2026-07-21

### Performance
- **Adaptive Translation Chunking**: Replaced fixed 50-text chunking with adaptive chunking based on a maximum of 2,000 characters per chunk in `scanAndTranslateDOM()` (TranslationEngineJS). Chunks are assembled by accumulating texts up to the character limit, avoiding oversized chunks that could cause HTTP 414 errors.

## [0.0.51] - 2026-07-21

### Performance
- **WebView2 Script Injection**: Moved `ChatSyncJS` from `NavigationCompleted` (re-injected on every navigation) to `AddScriptToExecuteOnDocumentCreatedAsync` (executed automatically on every new document prior to page load).
- **Reduced Work in `NavigationCompleted`**: No longer re-injects chat sync JS on every navigation, taking advantage of the idempotency guard (`__chatSyncInstalled`) and automatic document creation injection.

## [0.0.50] - 2026-07-21

### Performance
- **SettingsWindow**: Memoized results of `FindLogicalChildren` / `FindVisualChildren` into caches (`_cachedCheckBoxes`, `_cachedTextBlocks`, `_cachedAccountBorders`, `_cachedAccountTextBoxes`). Visual/logical tree recursion now executes only once per control type instead of on every theme change.
- Introduced `GetCachedLogicalChildren(Of T)` helper for lazy caching with explicit invalidation when `AccountsList.ItemsSource` changes.

## [0.0.49] - 2026-07-21

### Performance
- **WebView2 Lazy Initialization**: WebView2 controls are no longer created for all accounts on startup, but only for the active account. Remaining accounts are created and initialized on-demand upon the first account switch, saving ~50-100MB of RAM per inactive account.
- Removed `Imports Microsoft.Web.WebView2.Wpf` from `AccountManager.vb` (no longer creates WebView2 instances).

### Fixed
- `ApplyWpfTheme`, `BtnReloadActiveTab_Click`, toast handler: Added null checks for `acc.WebView` / `activeAcc.WebView` (which caused NullReferenceExceptions under lazy init, crashing the app after a few seconds).
- `SettingsWindow.BtnDevTools_Click`: Added null check for `activeAcc.WebView`.
- `AccountManager.RemoveAccountAsync`: Added null check before `WebView.Dispose()`.

## [0.0.48] - 2026-07-21

### Performance
- Eliminated `UpdateAccountTabStyling()`: Active/inactive account styling is now handled by a WPF `DataTrigger` in `AccountTabButtonStyle` that automatically reacts to `WhatsAppAccount.IsActive`. Removed `FindVisualChildren`/`FindVisualChild` from MainWindow.
- `WhatsAppAccount.IsActive` now implements `INotifyPropertyChanged` to enable reactive binding.
- **Impact**: Eliminated unnecessary visual tree recursion on every account switch.

## [0.0.47] - 2026-07-21

### Performance
- `FetchTranslations()`: Translated all UI keys in parallel (`Task.WhenAll`) instead of 30+ serial HTTP requests.
- **Impact**: Reduced translation time from ~N round-trips to ~1 round-trip.

### Removed
- Hidden the "Add Account" button from the accounts bar.

## [0.0.46] - 2026-07-21

### Release
- First formal stable release.
- Released on GitHub under tag `v0.0.46`.
- Published via OTA.

## [0.0.45] - 2026-07-20

### Performance
- Optimized I/O on `settings.json`: Settings changes are accumulated in in-memory cache and flushed to disk with a 500ms debounce, eliminating redundant file reads.
- `SaveSettingAsync`, `SaveThemeAsync`, and `UpdateLanguageAsync` now mutate the in-memory cache without rereading the file from disk.

## [0.0.44] - 2026-07-20

### Fixed
- `MigrateOrphanProfile` now deletes stale profile `WV2Profile_{Id}` if it already exists, then renames orphan `WV2Profile_` with the most recent session.
- `SetupWebViewAsync`: Orphan recovery executed even when destination already exists (replaces stale profile).
- When both `WV2Profile_` and `WV2Profile_{Id}` exist, the stale profile is deleted and replaced with `WV2Profile_` to preserve the freshest WhatsApp session.

## [0.0.43] - 2026-07-20

### Fixed
- Added fallback recovery for orphaned WebView2 profile (`WV2Profile_`) directly in `SetupWebViewAsync` as a last line of defense before creating a new profile.
- Added diagnostic logging in `LoadAccountsAsync`, `MigrateOrphanProfile`, and `SetupWebViewAsync` to track WebView2 profile migrations.

## [0.0.42] - 2026-07-20

### Fixed
- Resolved obsolete version text (v0.0.27) displayed in the window title during initial loading: removed hardcoded text in XAML and set immediate initialization in MainWindow `Sub New()` constructor.

## [0.0.41] - 2026-07-20

### Changed
- Removed media backup and refocused exclusively on text chat backup.
- Implemented periodic rotation of chat backup files every X days configured via the compile-time constant `BackupRotationDays`.
- Applied deterministic AES-256 encryption to filenames stored in the `Chats_Encrypted` folder.

## [0.0.40] - 2026-07-18

### Fixed
- Identified and fixed the exact root cause of anomalous media file sizes (1 KB, 368 KB, 490 KB):
  - **1 KB**: Caused by attempts to directly download encrypted remote HTTPS URLs from WhatsApp (`mmg.whatsapp.net`), returning HTTP 403 Forbidden. Blocked direct `fetch` of remote `http/https` URLs.
  - **368 KB / 490 KB**: Caused by downloading only preview `renderableUrl` Blobs.
  - Integrated extraction exclusively from decrypted in-memory `Blob` objects (`msg.mediaData.blob`, `msg.mediaObject._blob`, `msg.file`) or by invoking WhatsApp Web native `DownloadManager.downloadAndSave(msg)` to retrieve the full original binary file.

## [0.0.39] - 2026-07-18

### Fixed
- Definitively resolved the 291 KB truncation of media files:
  - Implemented `getMediaDataForContainer` querying `Store.Msg.get(id)` with the DOM message ID to retrieve the original video/audio/document binary `mediaData.blob`.
  - In DOM fallback mode, prioritized scanning `<video>`, `<audio>` tags and download `<a>` links over preview poster images (`<img>`).

## [0.0.38] - 2026-07-18

### Fixed
- Resolved truncation of video and media files (previously all saved at 291 KB):
  - Added `getMediaBlobFromMsg(msg)` in JavaScript to directly access `msg.mediaData.blob` or invoke WhatsApp Web internal `msg.downloadMedia()`.
  - Replaced extraction of JPEG preview/poster thumbnails from the DOM with downloading the full original binary video/audio/document file without size limits.

## [0.0.37] - 2026-07-18

### Fixed
- Resolved media files saved at 1 KB:
  - Replaced simple `blob:` URL string with asynchronous `fetchBlobAsBase64` in JavaScript to download and transfer the full original Base64 binary payload of images, audio, and video to VB.NET.
  - Added `rawMediaBytes.Length > 0` validation prior to writing the `.enc` encrypted file to disk.

## [0.0.36] - 2026-07-18

### Fixed
- Resolved population of `Chats_Encrypted/` and `Media_Encrypted/` folders:
  - Implemented a **dynamic DOM extractor** paired with a **real-time `MutationObserver`** in JavaScript.
  - As soon as the page loads, or as the user browses conversations and scrolls through history, every message and media item displayed on screen is immediately captured, AES-256 encrypted, and saved to the `Backup/` folder.

## [0.0.35] - 2026-07-18

### Fixed
- Resolved disconnection / account loss on application restart:
  - Fixed casing mismatch in JSON serialization between the `WhatsAppAccount` class (Id, Name, IsActive) and `settings.json` (`id`, `name`, `isActive`) using `[JsonPropertyName]` attributes and `PropertyNameCaseInsensitive = True`.
  - Prevented loading empty account objects (`Id = Nothing`), which led to opening unauthenticated anonymous profile folders (`WV2Profile_`).

## [0.0.34] - 2026-07-18

### Fixed
- Resolved empty backup folders: Added active wait loop (polling up to 75 seconds) in JavaScript to wait for complete initialization of WhatsApp Web modules (`window.Store.Chat`) before extracting chats.
- Added automatic periodic synchronization every 2 minutes and upon clicking the tab reload button.

## [0.0.33] - 2026-07-18

### Fixed
- Improved WhatsApp session persistence: If `settings.json` is missing or regenerated, the application automatically detects and reuses existing on-disk `WV2Profile_account_*` profile directories instead of creating a fresh empty one.
- Disabled automatic deletion of unmapped profiles in `settings.json` to prevent accidental loss of authenticated sessions.

## [0.0.32] - 2026-07-18

### Added
- Encrypted background chat backup (.enc) in AES-256 JSON Lines format and media retention with anonymized filenames (Hash/GUID) in Backup folder.
- Support for initial synchronization (3 months history) and automatic incremental synchronization.

## [0.0.31] - 2026-07-18

### Added
- Beta version published for testing with updated OTA paths.

## [0.0.30] - 2026-07-18

### Changed
- Centralized OTA repository path in `Constants.vb` (`UpdateFilesPath`), making it the single source of truth from which `publish.ps1` dynamically extracts the deployment destination.

## [0.0.29] - 2026-07-15

### Added
- New "Show message popup" notification setting: Allows disabling WPF popups while retaining native Windows toast notifications.

## [0.0.28] - 2026-07-14

### Fixed
- Removed `x:Static` reference to `Constants` (VB Module) from XAML, replaced with code-behind binding to avoid "Constants does not exist in namespace" compilation errors.

## [0.0.27] - 2026-07-14

### Fixed
- "Add Account" button disabled during initialization, account switching, and in-progress operations.

## [0.0.26] - 2026-07-14

### Changed
- OTA wait loop timeout reduced from 20s to 10s.

## [0.0.25] - 2026-07-14

### Added
- OTA test from 0.0.24.

## [0.0.24] - 2026-07-14

### Fixed
- Timeout in OTA update batch script: After 20s without being able to close the process, update proceeds anyway.

## [0.0.23] - 2026-07-14

### Added
- OTA update test from 0.0.22.

## [0.0.22] - 2026-07-14

### Added
- OTA update test.

## [0.0.21] - 2026-07-14

### Fixed
- **Undetected Update**: The `.app_version` check blocked update verification when the marker matched the current version, preventing newly published newer versions from being found. Removed early return: `IsNewerVersion` comparison already handles identical version cases.

## [0.0.20] - 2026-07-13

### Fixed
- **Blocked OTA Update**: The window did not actually close during updates (`_allowExit = False` blocked closure), leaving the batch waiting indefinitely for process termination. `ForceExitForUpdate()` is now called before shutdown.
- Added `.update_log.txt` log in the installation directory to trace every step of the update batch script.

### Changed
- Main window now resizable: Added maximize/restore button, double-click title bar to maximize, and bottom-right resize grip.
- WhatsApp WebView automatically adapts to window dimensions.

### Fixed
- "Add Account" button disabled during operations (settings open or account addition in progress) to prevent multiple clicks.
- OTA version comparison: Now only updates if the remote version is newer, preventing downgrades.

## [0.0.19] - 2026-07-13

### Added
- Support for beta publication channel with separate OTA path.
- "Use beta update channel" option in settings: When enabled, the app checks for updates from the beta repository instead of the stable one.

### Fixed
- "Add Account" button disabled during operations (settings open or account addition in progress) to prevent multiple clicks.
- OTA version comparison: Now only updates if the remote version is newer, preventing downgrades.

### Changed
- Main window now resizable: Added maximize/restore button, double-click title bar to maximize, and bottom-right resize grip.
- WhatsApp WebView automatically adapts to window dimensions.

## [0.0.18] - 2026-07-13

### Fixed
- Fixed JS bridge payload double-stringification that prevented VB.NET from parsing Notification and Translation messages.

## [0.0.17] - 2026-07-13

### Fixed
- Intercepted ServiceWorkerRegistration.prototype.showNotification instead of blocking service workers to fix notifications.

## [0.0.16] - 2026-07-13

### Changed
- Added debug logging for notifications.

## [0.0.15] - 2026-07-10

### Fixed
- Disabled and removed Service Workers upon page load to force WhatsApp Web to use standard page notifications (WebSockets), fixing an issue where notifications were not intercepted by the wrapper.

## [0.0.14] - 2026-07-10

### Fixed
- Fixed notification loading (toast and popup): Early injection of override scripts prior to page loading (`AddScriptToExecuteOnDocumentCreatedAsync`), automatic permission granting, and native exception handling on Toasts to prevent popup blocking.

## [0.0.13] - 2026-07-10

### Fixed
- Non-functional notifications (toast and popup): JavaScript executing `fetch()` on blob URLs and VB icon handling caused hangs. Restored synchronous notification dispatch without icon fetching and removed avatar download code (WhatsApp Web blob URLs are not accessible via `HttpClient`).

## [0.0.12] - 2026-07-10

### Removed
- Removed "Translate notification messages" and "Show translate button in notifications" checkboxes from Settings along with all associated code (`TranslateNotifications`, `ShowTranslateNotificationButton` properties, localization keys).

## [0.0.11] - 2026-07-10

### Fixed
- Non-functional notifications (toast and popup): JavaScript attempted synchronous `fetch()` on `blob:` URLs, blocking `NOTIFICATION_RECEIVED` dispatch in WebView2. Messages are now sent immediately (empty icon), with icon fetching occurring in the background via `NOTIFICATION_ICON`.
- NullReferenceException in `BtnDeleteAccount_Click` (`SettingsWindow.xaml.vb`): `btn.Tag` could be null. Now uses `TryCast(btn.Tag, String)` with null checks.

### Added
- `publish.ps1` script: Automates version bump → Release build → OTA copy → `version.txt` update.
- `publish.ps1` automatically excludes unnecessary files (`.pdb`, `.xml`) from OTA publication.

## [0.0.10] - 2026-07-10

### Added
- Visual popup (`MessagePopup.xaml`/`.vb`): Borderless WPF window appearing in the bottom-right upon message arrival, displaying contact initials, name, message preview, and auto-closing after 5 seconds. Click to restore main window.
- Toasts now include contact avatars (`AddAppLogoOverride` with circular cropping) downloaded from the notification icon (HTTP URL or Base64 data URL).

### Changed
- `HandleNotificationMessageAsync` changed from `Function` to `Async Function` to support asynchronous icon downloads.
- Used non-blocking `Dispatcher.BeginInvoke` instead of `Dispatcher.Invoke` to display popups.
- `DispatcherTimer` replaces `System.Timers.Timer` in popups (proper UI thread marshaling).

## [0.0.9] - 2026-07-10

### Fixed
- Application crash when switching accounts with 2+ accounts was caused by re-initializing existing WebView2 controls in `PopulateWebViews()`: when an account was added, all WebViews were removed and re-added to the grid, invoking `SetupWebViewAsync` on already initialized instances and breaking the WhatsApp connection. Fixed by skipping `SetupWebViewAsync` if `wv.CoreWebView2` is already populated.
- Added try-catch in `AccountTab_Click` to display potential account switching errors.

## [0.0.8] - 2026-07-10

### Fixed
- Resolved infinite update loop: `version.txt` is now excluded from OTA copy, and local `.app_version` marker introduced to prevent false positives and re-updating on every startup.
- The `.app_version` marker is written both before restart (by `PerformUpdateAsync`) and after copying (by `update.bat`).
- `IsLocalVersionCurrent()` skips check if local marker already matches the current version.

## [0.0.7] - 2026-07-10

### Added
- Context menu (right-click) on account tabs in the main window with "Rename" option to rename accounts directly without opening Settings.

## [0.0.6] - 2026-07-10

### Fixed
- OTA updates no longer overwrite `settings.json` and `translations_cache.json` (user settings and translation cache preserved).
- New configuration keys introduced in future releases are automatically **merged** into local `settings.json` during update without losing existing settings.

### Added
- `MergeSettingsFromOta()` in `UpdateChecker.vb`: Prior to restart, compares OTA `settings.json` with local file and adds missing keys with default values.

## [0.0.5] - 2026-07-10

### Fixed
- Account list in Settings ("Account Management") remained dark/unreadable in light theme — `FindVisualChildren` on `ItemsControl` could not locate containers because they were not yet generated when `ApplyTheme()` executed. Fixed by moving styling to `StyleAccountItems()` using `Dispatcher.BeginInvoke(DispatcherPriority.Background)` to wait for item container generation.
- Removed hardcoded `Background`/`BorderBrush` from Border DataTemplate in `SettingsWindow.xaml`.

### Changed
- `SettingsWindow` enlarged by 20px in width and 30px in height: `500×550` → `520×580`.

## [0.0.4] - 2026-07-10

### Added
- WebView2 Runtime check on startup (`GetAvailableBrowserVersionString()`): If missing, displays MessageBox with download link and exits app.
- `FindVisualChildren(Of T)` helper in `SettingsWindow.xaml.vb` to style elements inside DataTemplates.
- `StyleAccountItems()` in `ApplyTheme()` to update account list colors in light/dark themes.

### Fixed
- OTA updates no longer overwrite the `data\` directory (user settings preserved).
- `SettingsFile` fallback to `data\settings.json` if missing in base directory.
- Account deletion confirmation MessageBox is now localized (`delete_account_confirm`, `delete_account_last` keys).
- Theme-adaptive ComboBox (background and text adapt to light/dark theme).
- "Add Account" button dynamically translated upon language change.
- Italian language was not applied properly due to `KeepAppInEnglish` flag.
- Account name in Settings was unreadable in light theme.
- Language persistence across application restarts.

### Removed
- Removed "Keep app UI in English" checkbox and all associated logic.
- Removed Google Translate API usage for fetching supported languages and UI translations (hardcoded to English/Italian with precompiled translations).

## [0.0.3] - 2026-07-09

### Added
- Precompiled Italian translations (`ItStrings` dictionary in `Localization.vb`).
- Windows system theme detection via registry (`AppsUseLightTheme`).

### Changed
- Restricted interface language to English and Italian (removed `FetchSupportedLanguages`).
- Removed `LoadSupportedLanguagesAsync` and `LoadTranslationsAsync` (dead code).

## [0.0.2] - 2026-07-09

### Added
- Initial implementation of multilingual support with precompiled UI translations.
- Fallback logic for unsupported languages (falls back to English).

### Changed
- Localization system refactored: From Google Translate API calls to static dictionaries for the UI.

## [0.0.1] - 2026-07-08

### Added
- Initial fork from [whatsappPortable](https://github.com/Faeq-F/whatsappPortable) by Faeq-F.
- WPF wrapper for web.whatsapp.com using WebView2.
- Multi-account support with separate tabs.
- Customizable light/dark theme.
- Integrated message translation (Google Translate API).
- Native Windows Toast notifications.
- System tray integration with minimize-to-tray.
- Automatic updates from local network OTA repository.
- Isolated WebView2 profiles per account.
