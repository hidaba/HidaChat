# Completed Tasks

[![English](https://img.shields.io/badge/Language-English-blue.svg)](DONE.md)
[![Italiano](https://img.shields.io/badge/Lingua-Italiano-green.svg)](DONE.it.md)

## 2026-11-14

- [x] **TASK-0088** [High] 15s timeout on ZIP download and buffering archive in RAM (UpdateChecker.vb)
  - Result: used dedicated `_downloadClient` configured with `Timeout = Timeout.InfiniteTimeSpan` and guarded the request with 10-minute `CancellationTokenSource`. Implemented direct-to-disk progressive chunked streaming download (`File.Create(tempZipPath)`) using `HttpCompletionOption.ResponseHeadersRead` and 80 KB buffer (`buffer(81919)`). Performed SHA-256 verification directly from the disk file stream via `SHA256.HashDataAsync(fs)` in `ComputeSha256Async` without loading archive into RAM.
  - Verification: 640 regression assertions in `SessionTests.vb` passed with 0 errors. Clean build with 0 warnings and 0 errors.
