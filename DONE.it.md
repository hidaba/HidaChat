# Attività Completate

[![Italiano](https://img.shields.io/badge/Lingua-Italiano-green.svg)](DONE.it.md)
[![English](https://img.shields.io/badge/Language-English-blue.svg)](DONE.md)

## 2026-11-14

- [x] **TASK-0088** [Alta] Timeout di 15 s sul download dello ZIP e buffering in RAM dell'archivio (UpdateChecker.vb)
  - Risultato: utilizzato client dedicato `_downloadClient` configurato con `Timeout = Timeout.InfiniteTimeSpan` e richiesta protetta da `CancellationTokenSource` con timeout esteso a 10 minuti (`TimeSpan.FromMinutes(10)`). Implementato download in streaming progressivo direttamente su file temporaneo su disco (`File.Create(tempZipPath)`) tramite `HttpCompletionOption.ResponseHeadersRead` con buffer di lettura a blocchi da 80 KB (`buffer(81919)`). Calcolo dell'hash SHA-256 di verifica eseguito direttamente dallo stream del file su disco tramite `SHA256.HashDataAsync(fs)` in `ComputeSha256Async`, senza caricare l'archivio in memoria RAM.
  - Verifica: superate 640 asserzioni di regressione automatizzate con 0 errori in `SessionTests.vb`. Compilazione Release con 0 avvisi e 0 errori.
