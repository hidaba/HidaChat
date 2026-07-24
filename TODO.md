# TODO — Ottimizzazioni Prestazionali

## ~~1. I/O su `settings.json` — Letture multiple ridondanti~~ ✅
- ~~`SaveSettingAsync()` fa `ReadSettingsAsync()` + `WriteSettingsAsync()` ogni volta che cambia una singola chiave~~
- ~~**Ottimizzazione**: accumulare le modifiche in memoria e scrivere su disco con un debounce (es. 500ms) oppure riscrivere solo il file senza rileggerlo~~
- ~~**Impatto**: Alto | **Sforzo**: Basso~~

## ~~2. `FetchTranslations()` — N richieste HTTP seriali~~ ✅
- ~~Per ogni chiave di lingua (30+), fa una richiesta HTTP individuale a Google Translate, una dopo l'altra~~
- ~~**Ottimizzazione**: raggruppare le traduzioni in un'unica richiesta batch, o usare `HttpClient` con richieste parallele (`Task.WhenAll`)~~
- ~~**Impatto**: Alto | **Sforzo**: Medio~~

## ~~3. `StyleAccountItems()` — Ricorsione visual tree ogni volta~~ ✅
- ~~Chiama `FindVisualChildren(Of Border)` e `FindVisualChildren(Of TextBox)` sull'intero `AccountsList`~~
- ~~**Ottimizzazione**: usare stili WPF con DataTrigger invece di code-behind, oppure usare `Loaded` event handler sugli elementi del DataTemplate~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~

## 4. `AccountsList.ItemsSource = Nothing` + riassegnazione
- Dopo eliminazione account, azzera e riassegna `ItemsSource` forzando rigenerazione di TUTTI i DataTemplate
- **Ottimizzazione**: usare `ObservableCollection` invece di `List(Of WhatsAppAccount)`
- **Impatto**: Medio | **Sforzo**: Basso

## ~~5. `FindVisualChildren()` e `FindVisualChild()` — Ricorsione multipla~~ ✅
- ~~Funzioni ricorsive chiamate 3-4 volte per finestra che visitano TUTTO il visual tree~~
- ~~**Ottimizzazione**: memoizzazione (cache dell'albero) o sostituzione con binding/stili dichiarativi~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~

## ~~6. WebView2 — Script injection su ogni navigazione~~ ✅
- ~~A ogni `NavigationCompleted`, reinietta tema CSS e script di traduzione inutilmente su navigazioni secondarie~~
- ~~**Ottimizzazione**: iniettare solo su navigazione iniziale, usare `AddScriptToExecuteOnDocumentCreatedAsync` per script permanenti~~
- ~~**Impatto**: Medio | **Sforzo**: Basso~~

## 7. Aggiornamento OTA — `ReadVersionFromFileAsync()` su UNC path
- Il check aggiornamenti legge un file da percorso di rete UNC, operazione bloccante se il server è lento
- **Ottimizzazione**: timeout più breve (3s), check asincrono senza bloccare l'avvio, caching della versione
- **Impatto**: Medio | **Sforzo**: Basso

## ~~8. `AccountManager.LoadAccountsAsync()` — Crea tutti i WebView2 all'avvio~~ ✅
- ~~Crea un WebView2 per OGNI account al caricamento (~50-100MB per istanza)~~
- ~~**Ottimizzazione**: lazy initialization — creare WebView2 solo per l'account attivo, gli altri on-demand~~
- ~~**Impatto**: Alto | **Sforzo**: Alto~~

## ~~9. Traduzioni batch — Chunking fisso a 50 testi~~ ✅
- ~~Dimensione chunk hardcoded a 50, non considera la lunghezza dei testi~~
- ~~**Ottimizzazione**: chunking adattivo basato sulla lunghezza totale dei caratteri (es. max 2000 caratteri per chunk)~~
- ~~**Impatto**: Medio | **Sforzo**: Basso~~

## 10. `MessagePopup.RepositionAll()` — Itera tutti i popup attivi
- Ogni volta che un popup viene mostrato/chiuso, ricalcola la posizione di TUTTI i popup attivi
- **Ottimizzazione**: tenere traccia incrementale della posizione Y del prossimo popup
- **Impatto**: Basso | **Sforzo**: Basso

## 11. `SwitchToAccountAsync()` — Visibilità di tutti i WebView
- Itera TUTTI i WebView per impostare `Visibility = Collapsed` e solo uno a `Visible`
- **Ottimizzazione**: tenere traccia dell'ultimo account attivo e nascondere solo quello
- **Impatto**: Basso | **Sforzo**: Basso

## ~~12. Translation cache — Scrittura completa su ogni cambio lingua~~ ✅
- ~~`SaveCacheFileAsync()` riscrive l'intero file cache anche per modifiche minime~~
- ~~**Ottimizzazione**: scrittura differita o incrementale~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~
- *Obsoleto: traduzioni UI ormai hardcoded (EN/IT). Cache scritta solo all'inizializzazione, non a ogni cambio lingua.*

---

# Memoria — Ottimizzazioni e Fix Leak

## 12. `WhatsAppAccount` — Implementare `IDisposable`
- WebView2 (COM/GPU), `ChatSyncBackgroundWorker`, `ActiveNotificationIds` mai rilasciati quando un account viene rimosso
- **Ottimizzazione**: implementare `IDisposable` e chiamarlo da `AccountManager` alla rimozione dell'account
- **Impatto**: Alto | **Sforzo**: Medio

## 13. `ChatJsonStorageService` e `ChatSyncBackgroundWorker` — `IDisposable` mancante
- `SemaphoreSlim` (`_fileLock`) mai disposto — OS handle leak
- **Ottimizzazione**: implementare `IDisposable` su entrambe le classi con dispose di `SemaphoreSlim`
- **Impatto**: Alto | **Sforzo**: Basso

## 14. Event handler su `CoreWebView2` mai rimossi
- `PermissionRequested`, `NewWindowRequested`, `WebMessageReceived`, `NavigationCompleted` registrati ma mai rimossi con `RemoveHandler`
- WebView2 trattiene riferimenti forti all'account — memory leak certo alla rimozione
- **Ottimizzazione**: salvare riferimenti handler e chiamare `RemoveHandler` prima di rilasciare il WebView2
- **Impatto**: Alto | **Sforzo**: Basso

## 15. `CryptoHelper.EncryptFileAsync` / `DecryptFileAsync` — File interi in memoria
- `ReadAllBytesAsync` carica l'intero file in byte array; per file multimediali (centinaia di MB) raddoppia/triplica la memoria
- **Ottimizzazione**: usare streaming — leggere in chunk, cifrare per chunk, scrivere per chunk
- **Impatto**: Alto | **Sforzo**: Alto

## 16. `SaveMessageBatchAsync` — 4-5 allocazioni intermedie per messaggio
- Per ogni messaggio: `GetRawText()` → `JsonNode.Parse()` → `ToJsonString()` → `EncryptString()` → `List(Of String)`
- Per batch da 10.000 messaggi: ~40.000+ oggetti intermedi
- **Ottimizzazione**: usare `Utf8JsonReader` per streaming read, cifrare i byte UTF-8 direttamente eliminando il roundtrip GetRawText+ToJsonString
- **Impatto**: Alto | **Sforzo**: Alto

## 17. `EncryptBytes` / `DecryptBytes` — Array allocati senza pooling
- Ogni operazione alloca 4 array (nonce, tag, cipherText, result) — pressione alta sul GC
- **Ottimizzazione**: usare `ArrayPool(Of Byte)` con `Rent`/`Return`
- **Impatto**: Alto | **Sforzo**: Basso

## 18. `ChatSyncBackgroundWorker` — `.GetAwaiter().GetResult()` blocca thread pool
- Dentro `Task.Run`, chiama `.GetAwaiter().GetResult()` su operazione I/O — thread pool starvation
- **Ottimizzazione**: rendere `ProcessIncomingBatchInternal` `Async Sub`/`Async Function` con `Await` vero
- **Impatto**: Alto | **Sforzo**: Basso

## 19. `ActiveNotificationIds` — HashSet cresce senza limiti
- ID notifiche aggiunti su `NOTIFICATION_RECEIVED` ma mai rimossi se `NOTIFICATION_CLOSED` non arriva
- **Ottimizzazione**: limite massimo dimensioni + cleanup periodico per ID vecchi
- **Impatto**: Alto | **Sforzo**: Basso

## 20. `Localization` — `HttpClient` creato 3 volte (non condiviso)
- `New HttpClient()` in ogni metodo — socket exhaustion sotto carico
- **Ottimizzazione**: `Shared ReadOnly HttpClient` a livello di classe/modulo
- **Impatto**: Medio | **Sforzo**: Basso

## 21. `Directory.GetFiles` in `UpdateChecker.vb` — Enumerazione eager su network share
- Carica TUTTI i percorsi file in array `String()` — potenzialmente migliaia di stringhe su percorso UNC
- **Ottimizzazione**: `Directory.EnumerateFiles` (streaming lazy)
- **Impatto**: Medio | **Sforzo**: Basso

## 22. `CancellationTokenSource` in `SettingsController.FlushAfterDebounceAsync` — Non disposto
- Il CTS precedente viene solo cancellato (`Cancel()`) ma mai chiamato `.Dispose()` — tiene `WaitHandle` (OS resource)
- **Ottimizzazione**: disporre il vecchio `_flushCts` prima di sostituirlo col nuovo
- **Impatto**: Medio | **Sforzo**: Basso

## 23. `MainWindow.xaml.vb` — Event handler `PropertyChanged` mai rimossi
- `_settingsController.PropertyChanged` e `_accountManager.PropertyChanged` registrati ma mai rimossi in `OnClosed`/`OnUnloaded`
- **Ottimizzazione**: aggiungere `RemoveHandler` negli eventi di chiusura finestra
- **Impatto**: Medio | **Sforzo**: Basso

## 24. `DispatcherTimer` in `MainWindow.xaml.vb` — Mai fermato
- Timer tick ogni 2 minuti con lambda closure su `_accountManager`; mai fermato o disposto
- **Ottimizzazione**: fermare (`Stop()`) il timer in `OnClosed`; trasformare lambda in metodo nominato
- **Impatto**: Medio | **Sforzo**: Basso

## 25. `ExecuteScriptAsync` fire-and-forget in `MainWindow.xaml.vb:341-357`
- Task restituiti da `ExecuteScriptAsync` non vengono awaitati né catturati — eccezioni inosservabili
- **Ottimizzazione**: catturare i task o usare `Async Sub` con gestione errori
- **Impatto**: Medio | **Sforzo**: Basso

## 26. `JsScripts.vb:641-653` — `.Replace()` multipli su template grandi
- `GetTranslationJS` chiama `.Replace()` 10+ volte sul template enorme — crea una nuova stringa ogni volta
- **Ottimizzazione**: `StringBuilder` con `Replace`, o template con interpolazione
- **Impatto**: Medio | **Sforzo**: Basso

## 27. `AccountManager` e `SettingsController` — Nessun dirty tracking
- `SaveAccountsAsync()` e `WriteSettingsAsync()` riscrivono tutto anche se nulla è cambiato
- **Ottimizzazione**: flag `_dirty` + scrittura solo su modifiche effettive
- **Impatto**: Medio | **Sforzo**: Basso

## 28. `WhatsAppAccount.vb:68` — `New Random()` ogni chiamata
- System clock seed può produrre sequenze identiche se chiamate ravvicinate
- **Ottimizzazione**: `Random.Shared` (.NET 9) o `Shared` con lock
- **Impatto**: Basso | **Sforzo**: Basso

## 29. `AccountManager.vb:232-236` — Anonymous type a ogni salvataggio
- Crea istanze di tipo anonimo per ogni account a ogni salvataggio — GC pressure
- **Ottimizzazione**: serializzare `WhatsAppAccount` direttamente (ha già `JsonPropertyName`)
- **Impatto**: Basso | **Sforzo**: Basso

## 30. `Directory.GetDirectories` in `AccountManager.vb:195`
- Array eager invece di enumerazione lazy
- **Ottimizzazione**: `Directory.EnumerateDirectories`
- **Impatto**: Basso | **Sforzo**: Basso

## 31. `UpdateChecker.vb:171-204` — `batchContent` con string concatenation
- Batch file build con concatenazione di ~35 righe
- **Ottimizzazione**: `StringBuilder`
- **Impatto**: Basso | **Sforzo**: Basso

## 32. `ChatJsonStorageService.vb:74,209` — Codice duplicato LoadIndex
- `LoadIndexAsync` e `LoadIndexInternalAsync` condividono la stessa logica
- **Ottimizzazione**: unificare in un unico metodo privato
- **Impatto**: Basso | **Sforzo**: Basso

## 33. `MessagePopup._activePopups` — Lista statica senza WeakReference
- Trattiene riferimenti forti a tutti i popup — leak se non chiusi normalmente
- **Ottimizzazione**: `WeakReference(Of MessagePopup)` o `ConditionalWeakTable`
- **Impatto**: Basso | **Sforzo**: Basso

## 34. Registro `AppsUseLightTheme` — Letto 3 volte senza caching
- Stessa chiave registry letta in `MainWindow.xaml.vb:323-333`, `WhatsAppAccount.vb:176-185`, `SettingsWindow.xaml.vb:247-256`
- **Ottimizzazione**: leggere una volta e fare cache col pattern `SystemEvents.UserPreferenceChanged`
- **Impatto**: Basso | **Sforzo**: Basso

## 35. `SolidColorBrush` — Creato a ogni cambio tema in MainWindow e SettingsWindow
- `New SolidColorBrush(...)` chiamato ogni volta che si applica il tema
- **Ottimizzazione**: cache delle istanze per colore
- **Impatto**: Basso | **Sforzo**: Basso

## 36. `JsScripts.vb` — ~900 righe di JS in memoria permanente
- Centinaia di KB di stringhe JavaScript caricate all'avvio e mai rilasciate; copiate in `initScript` a ogni setup account
- **Ottimizzazione**: caricare da file esterno o risorsa embedded lazy; eventualmente comprimere
- **Impatto**: Basso | **Sforzo**: Medio

---

**Priorità memoria consigliate**: 12 → 13 → 14 → 19 → 18 → 17 → 16 → 15 → 20 → 21 → 23
