# TODO — Ottimizzazioni Prestazionali e Memoria

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

## ~~4. `AccountsList.ItemsSource = Nothing` + riassegnazione~~ ✅
- ~~Dopo eliminazione account, azzera e riassegna `ItemsSource` forzando rigenerazione di TUTTI i DataTemplate~~
- ~~**Ottimizzazione**: usare `ObservableCollection` invece di `List(Of WhatsAppAccount)`~~
- ~~**Impatto**: Medio | **Sforzo**: Basso~~

## ~~5. `FindVisualChildren()` e `FindVisualChild()` — Ricorsione multipla~~ ✅
- ~~Funzioni ricorsive chiamate 3-4 volte per finestra che visitano TUTTO il visual tree~~
- ~~**Ottimizzazione**: memoizzazione (cache dell'albero) o sostituzione con binding/stili dichiarativi~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~

## ~~6. WebView2 — Script injection su ogni navigazione~~ ✅
- ~~A ogni `NavigationCompleted`, reinietta tema CSS e script di traduzione inutilmente su navigazioni secondarie~~
- ~~**Ottimizzazione**: iniettare solo su navigazione iniziale, usare `AddScriptToExecuteOnDocumentCreatedAsync` per script permanenti~~
- ~~**Impatto**: Medio | **Sforzo**: Basso~~

## ~~7. Aggiornamento OTA — `ReadVersionFromFileAsync()` su UNC path~~ ✅
- ~~Obsoleto nella v0.2.0: la lettura da percorso di rete UNC e `ReadVersionFromFileAsync()` sono stati completamente rimossi. Il sistema OTA opera esclusivamente tramite API REST di GitHub Releases.~~

## ~~8. `AccountManager.LoadAccountsAsync()` — Crea tutti i WebView2 all'avvio~~ ✅
- ~~Crea un WebView2 per OGNI account al caricamento (~50-100MB per istanza)~~
- ~~**Ottimizzazione**: lazy initialization — creare WebView2 solo per l'account attivo, gli altri on-demand~~
- ~~**Impatto**: Alto | **Sforzo**: Alto~~

## ~~9. Traduzioni batch — Chunking fisso a 50 testi~~ ✅
- ~~Dimensione chunk hardcoded a 50, non considera la lunghezza dei testi~~
- ~~**Ottimizzazione**: chunking adattivo basato sulla lunghezza totale dei caratteri (es. max 2000 caratteri per chunk)~~
- ~~**Impatto**: Medio | **Sforzo**: Basso~~

## ~~10. `MessagePopup.RepositionAll()` — Itera tutti i popup attivi~~ ✅
- ~~Ogni volta che un popup viene mostrato/chiuso, ricalcola la posizione di TUTTI i popup attivi~~
- ~~**Ottimizzazione**: tenere traccia incrementale della posizione Y del prossimo popup impilandolo verso l'alto senza riposizionare i popup già attivi~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~

## ~~11. `SwitchToAccountAsync()` — Visibilità di tutti i WebView~~ ✅
- ~~Itera TUTTI i WebView per impostare `Visibility = Collapsed` e solo uno a `Visible`~~
- ~~**Ottimizzazione**: tenere traccia dell'ultimo account attivo e nascondere solo quello con controllo preventivo di account già attivo~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~

## ~~12. Translation cache — Scrittura completa su ogni cambio lingua~~ ✅
- ~~`SaveCacheFileAsync()` riscrive l'intero file cache anche per modifiche minime~~
- ~~**Ottimizzazione**: scrittura differita o incrementale~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~
- *Obsoleto: traduzioni UI ormai hardcoded (EN/IT). Cache scritta solo all'inizializzazione, non a ogni cambio lingua.*

---

# Memoria — Ottimizzazioni e Fix Leak

## ~~13. `WhatsAppAccount` — Implementare `IDisposable`~~ ✅
- ~~WebView2 (COM/GPU) e `ActiveNotificationIds` mai rilasciati quando un account viene rimosso~~
- ~~**Ottimizzazione**: implementato `IDisposable` e chiamato da `AccountManager` alla rimozione dell'account~~
- ~~**Impatto**: Alto | **Sforzo**: Medio~~

## ~~14. `ChatJsonStorageService` e `ChatSyncBackgroundWorker` — Rimozione sistema backup~~ ✅
- ~~Classi eliminate nella v0.1.0 assieme all'intero sistema di sincronizzazione e salvataggio chat su disco.~~

## ~~15. Event handler su `CoreWebView2` mai rimossi~~ ✅
- ~~`PermissionRequested`, `NewWindowRequested`, `WebMessageReceived`, `NavigationCompleted` registrati ma mai rimossi con `RemoveHandler`~~
- ~~WebView2 trattiene riferimenti forti all'account — memory leak certo alla rimozione~~
- ~~**Ottimizzazione**: salvati riferimenti handler e chiamato `RemoveHandler` prima di rilasciare il WebView2~~
- ~~**Impatto**: Alto | **Sforzo**: Basso~~

## ~~16. `CryptoHelper` e cifratura backup — Rimozione sistema backup~~ ✅
- ~~Classe `CryptoHelper` eliminata nella v0.1.0 con la rimozione del backup.~~

## ~~17. `SaveMessageBatchAsync` — Rimozione sistema backup~~ ✅
- ~~Metodo eliminato nella v0.1.0.~~

## ~~18. `EncryptBytes` / `DecryptBytes` — Rimozione sistema backup~~ ✅
- ~~Metodi eliminati nella v0.1.0.~~

## ~~19. `ActiveNotificationIds` — HashSet cresce senza limiti~~ ✅
- ~~ID notifiche aggiunti su `NOTIFICATION_RECEIVED` ma mai rimossi se `NOTIFICATION_CLOSED` non arriva~~
- ~~**Ottimizzazione**: limite massimo dimensioni + cleanup periodico per ID vecchi~~
- ~~**Impatto**: Alto | **Sforzo**: Basso~~

## ~~20. `Localization` — `HttpClient` creato 3 volte (non condiviso)~~ ✅
- ~~`New HttpClient()` in ogni metodo — socket exhaustion sotto carico~~
- ~~**Ottimizzazione**: `Shared ReadOnly HttpClient` a livello di classe/modulo~~
- ~~**Impatto**: Medio | **Sforzo**: Basso~~

## ~~21. `Directory.GetFiles` in `UpdateChecker.vb` — Enumerazione eager su network share~~ ✅
- ~~Carica TUTTI i percorsi file in array `String()` — potenzialmente migliaia di stringhe su percorso UNC~~
- ~~**Ottimizzazione**: `Directory.EnumerateFiles` (streaming lazy)~~
- ~~**Impatto**: Medio | **Sforzo**: Basso~~

## ~~22. `CancellationTokenSource` in `SettingsController.FlushAfterDebounceAsync` — Non disposto~~ ✅
- ~~Il CTS precedente viene solo cancellato (`Cancel()`) ma mai chiamato `.Dispose()` — tiene `WaitHandle` (OS resource)~~
- ~~**Ottimizzazione**: disporre il vecchio `_flushCts` prima di sostituirlo col nuovo~~
- ~~**Impatto**: Medio | **Sforzo**: Basso~~

## ~~23. `MainWindow.xaml.vb` — Event handler `PropertyChanged` mai rimossi~~ ✅
- ~~`_settingsController.PropertyChanged` e `_accountManager.PropertyChanged` registrati ma mai rimossi in `OnClosed`/`OnUnloaded`~~
- ~~**Ottimizzazione**: aggiungere `RemoveHandler` negli eventi di chiusura finestra~~
- ~~**Impatto**: Medio | **Sforzo**: Basso~~

## ~~24. `DispatcherTimer` per sincronizzazione chat — Rimosso~~ ✅
- ~~Timer rimosso nella v0.1.0 insieme al sistema di sincronizzazione backup.~~

## ~~25. `ExecuteScriptAsync` fire-and-forget in `MainWindow.xaml.vb`~~ ✅
- ~~Task restituiti da `ExecuteScriptAsync` non vengono awaitati né catturati — eccezioni inosservabili~~
- ~~**Ottimizzazione**: catturare i task o usare `Async Sub`/`Async Function` con `Await` e gestione errori (`Try...Catch`)~~
- ~~**Impatto**: Medio | **Sforzo**: Basso~~


## ~~26. `JsScripts.vb` — `.Replace()` multipli su template grandi~~ ✅
- ~~`GetTranslationJS` chiama `.Replace()` 10+ volte sul template enorme — crea una nuova stringa ogni volta~~
- ~~**Ottimizzazione**: `StringBuilder` con `Replace` in-place~~
- ~~**Impatto**: Medio | **Sforzo**: Basso~~

## ~~27. `AccountManager` e `SettingsController` — Nessun dirty tracking~~ ✅
- ~~`SaveAccountsAsync()` e `WriteSettingsAsync()` riscrivono tutto anche se nulla è cambiato~~
- ~~**Ottimizzazione**: flag `_dirty` + scrittura solo su modifiche effettive~~
- ~~**Impatto**: Medio | **Sforzo**: Basso~~

## ~~28. `WhatsAppAccount.vb` — `New Random()` ogni chiamata~~ ✅
- ~~System clock seed può produrre sequenze identiche se chiamate ravvicinate~~
- ~~**Ottimizzazione**: `Random.Shared` (.NET 9) o `Shared` con lock~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~

## ~~29. `AccountManager.vb` — Anonymous type a ogni salvataggio~~ ✅
- ~~Crea istanze di tipo anonimo per ogni account a ogni salvataggio — GC pressure~~
- ~~**Ottimizzazione**: serializzare `WhatsAppAccount` direttamente (ha già `JsonPropertyName`)~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~

## ~~30. `Directory.GetDirectories` in `AccountManager.vb`~~ ✅
- ~~Array eager invece di enumerazione lazy~~
- ~~**Ottimizzazione**: `Directory.EnumerateDirectories` e `FirstOrDefault()` per interrompere la scansione alla prima corrispondenza~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~


## ~~31. `UpdateChecker.vb` — `batchContent` con string concatenation~~ ✅
- ~~Batch file build con concatenazione di ~35 righe~~
- ~~**Ottimizzazione**: `StringBuilder` per assemblare lo script batch in streaming~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~


## ~~32. `MessagePopup._activePopups` — Lista statica senza WeakReference~~ ✅
- ~~Trattiene riferimenti forti a tutti i popup — leak se non chiusi normalmente~~
- ~~**Ottimizzazione**: `WeakReference(Of MessagePopup)` con rimozione automatica dei riferimenti raccolti dal GC~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~

## ~~33. Registro `AppsUseLightTheme` — Letto 3 volte senza caching~~ ✅
- ~~Stessa chiave registry letta in `MainWindow.xaml.vb`, `WhatsAppAccount.vb`, `SettingsWindow.xaml.vb`~~
- ~~**Ottimizzazione**: modulo `SystemThemeHelper` centralizzato con cache del registro e ascolto dell'evento `SystemEvents.UserPreferenceChanged`~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~


## ~~34. `SolidColorBrush` — Creato a ogni cambio tema in MainWindow e SettingsWindow~~ ✅
- ~~`New SolidColorBrush(...)` chiamato ogni volta che si applica il tema~~
- ~~**Ottimizzazione**: cache delle istanze per colore~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~

## ~~35. `JsScripts.vb` — ~300 righe di JS traduzioni in memoria permanente~~ ✅
- ~~Script JS caricate all'avvio e mai rilasciate; copiate a ogni setup account~~
- ~~**Ottimizzazione**: caricate da risorse incorporate `EmbeddedResource` in modo lazy (`Lazy(Of String)`); persistenza delle traduzioni su file (`data/translations_cache.json`) per evitare download ripetuti da internet; ottimizzazione RAM per mantenere in memoria solo i dizionari della lingua impostata.~~
- ~~**Impatto**: Basso | **Sforzo**: Medio~~

---

# TUNING / HARDENING — non bloccanti, migliorano robustezza e coerenza

## ~~36. File `settings.json` e `translations_cache.json` nella cartella radice invece che in `data/`~~ ✅
- ~~**File**: `SettingsController.vb`, proprietà `SettingsFile` e `Localization.vb`, proprietà `CacheFilePath`~~
- ~~I file `settings.json` e `translations_cache.json` vengono creati e salvati direttamente all'interno della cartella portabile `data/`. Implementata la migrazione trasparente e automatica (`File.Move`) dei file esistenti nella cartella radice verso `data/` al primo avvio, preservando le impostazioni e la cache.~~
- ~~**Impatto**: Medio | **Sforzo**: Basso~~

## ~~37. Cancellazione dei profili basata su `Task.Delay` fisso invece di un segnale deterministico~~ ✅
- ~~**File**: `AccountManager.vb`, `RemoveAccountAsync`, `MigrateOrphanProfileAsync`, `CleanupUnusedProfilesAsync`~~
- ~~Implementato metodo helper asincrono `DeleteDirectoryWithRetryAsync` con backoff progressivo (fino a 5 tentativi con attesa crescente) per garantire la corretta eliminazione dei profili WebView2 anche su macchine lente o in presenza di lock momentanei da parte dell'antivirus/Chromium.~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~

## ~~38. Escaping incoerente dei valori interpolati nel JavaScript eseguito via `ExecuteScriptAsync`~~ ✅
- ~~**File**: `AppAccounts.vb` (`HandleTranslationMessageAsync`, `UpdateWebviewLanguageAsync`) e `MainWindow.xaml.vb` (`onNotificationClicked`)~~
- ~~Tutti i valori e identificatori interpolati (`id`, `langCode`, `langName`, `tooltipLabel`, `notificationId`) vengono ora serializzati in modo coerente e sicuro tramite `JsonSerializer.Serialize`, prevenendo injection di caratteri speciali o apici e garantendo conformità rigorosa al formato JSON.~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~

## ~~39. Il bridge token è esposto come variabile globale leggibile dalla pagina~~ ✅
- ~~**File**: `AppAccounts.vb`, `JsScripts.vb`, `Scripts/notification.js`, `Scripts/translation.js`~~
- ~~Il `bridgeToken` non viene più esposto nell'oggetto globale `window.__bridgeToken`. Viene ora interpolato tramite serializzazione sicura `JsonSerializer.Serialize` ed incapsulato privatamente all'interno dello scope e closure (IIFE) di `notification.js` e `translation.js`. Nessuno script o estensione in esecuzione nel DOM/pagina web può leggere, ispezionare o manomettere il token di sicurezza IPC.~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~

## ~~40. Nessuna verifica di integrità sullo ZIP di aggiornamento automatico~~ ✅
- ~~**File**: `UpdateChecker.vb`, `PerformUpdateFromGitHubAsync`, `FetchGitHubReleaseInfoAsync`~~
- ~~Implementata la verifica crittografica di integrità SHA-256 prima dell'estrazione dell'archivio ZIP di aggiornamento. Il sistema intercetta automaticamente asset di checksum dedicati (`.sha256`, `.sha256sum`, `SHA256SUMS`, `SHA256SUMS.txt`, `checksums.txt`) o impronte SHA-256 dichiarate nelle note di rilascio GitHub, calcola l'hash del file scaricato (`SHA256.HashData`) e blocca tempestivamente l'installazione in caso di discrepanza, garantendo la massima resilienza e protezione contro manomissioni o download parziali.~~
- ~~**Impatto**: Basso | **Sforzo**: Basso~~

---

# ROADMAP MULTI-PIATTAFORMA — TELEGRAM

## ~~41. Integrazione Telegram — Funzionalità Avanzate e Affinamenti UI/UX~~ ✅
- **Stato Base**: ✅ Completato nella v0.5.0 (Routing su `web.telegram.org/k/`, isolamento profili in `data/webview/`, icone vettoriali dedicate azzurre e selettore piattaforma nelle Impostazioni).
- **Rifiniture Multi-Piattaforma**: ✅ Completato nella v0.5.0 (Sincronizzazione tema Scuro/Chiaro con classi `.night` e `themeController`, traduzione selettiva hover e batch con selettori DOM Telegram, routing click popup su account notificante).
- **Evoluzione e Rifiniture Avanzate**: ✅ Completato nella v0.7.1-beta:
  - **Badge Notifiche e Contatore Non Letti Telegram**: Rilevamento continuo del conteggio messaggi non letti (MutationObserver combinato su `<title>` e scansione DOM selettori `.badge.unread`, `.unread-count`, ecc.) con pillola badge numerica colorata sulla scheda dell'account e icona tray dinamica.
  - **Gestione Deep Link Telegram (`tg://` e `t.me/`)**: Intercettazione in `NavigationStarting` e `NewWindowRequested` di protocolli `tg://resolve?domain=...`, `tg://join?invite=...` e link web `https://t.me/...` con traduzione automatica in route Telegram Web K interne (`https://web.telegram.org/k/#@...` / `https://web.telegram.org/k/#?tgaddr=...`).
  - **Scorciatoie da Tastiera Globali**: Aggiunte scorciatoie da tastiera per cambio rapido account (`Ctrl+1`, `Ctrl+2`, `Ctrl+3` e NumPad), navigazione circolare schede (`Ctrl+Tab`, `Ctrl+Shift+Tab`), nuovo account (`Ctrl+T`/`Ctrl+N`), ricarica (`Ctrl+R`/`F5`), impostazioni (`Ctrl+,`) e invio massivo (`Ctrl+B`).
- **Impatto**: Alto | **Sforzo**: Medio

---

# NUOVE FUNZIONALITÀ & IDEE (Ispirate da Altus e Client Desktop Moderni)

## ~~42. Indicatore Stato "Online" dei Contatti (Online Indicator)~~ ✅
- **Descrizione**: Intercettare tramite uno script JavaScript iniettato (`MutationObserver` su intestazione chat) quando il contatto o la chat attualmente aperta è "online" (es. rilevando la dicitura `online` / `in linea` o elementi di stato nel DOM della conversazione attiva sia su WhatsApp Web che su Telegram Web).
- **Integrazione UI**: Visualizzazione di un badge elegante animato nella `TitleBar` (`OnlineIndicatorBorder`) e pallino verde sulla scheda account attiva per WhatsApp e Telegram Web.
- **Impatto**: Medio | **Sforzo**: Basso

## ~~43. Supporto a Temi CSS Personalizzati Utente (Custom CSS Injector)~~ ✅
- **Descrizione**: Sezione dedicata nelle **Impostazioni** per inserire e modificare regole CSS personalizzate per WhatsApp Web e Telegram Web (font, colori delle bolle dei messaggi, larghezza sidebar, sfondo chat, trasparenze, tema OLED).
- **Funzionalità Implementate**: Editor monospace con scrollbar, checkbox di attivazione immediata, persistenza in `settings.json`, pulsanti preset rapidi (*OLED*, *Compatto*, *Font*, *Svuota*) e iniezione live sincrona su tutte le WebView2 tramite bridge IPC.
- **Impatto**: Medio | **Sforzo**: Basso-Medio

## 44. Correttore Ortografico Nativo Multilingua (Spellchecker WebView2)
- **Descrizione**: Abilitare il motore di correzione ortografica nativo di Edge WebView2 e permettere all'utente di configurare o alternare la lingua del dizionario (italiano, inglese, spagnolo, ecc.) per evidenziare e correggere errori nei campi di digitazione di WhatsApp e Telegram.
- **Implementazione**: Configurazione dei parametri Chromium via `AdditionalBrowserArguments` o impostazioni WebView2 (`CoreWebView2Settings`).
- **Impatto**: Medio | **Sforzo**: Basso

## 45. Utility Bar & Gestione Rapida Audio / Dispositivi
- **Descrizione**: Aggiungere opzioni di utilità rapida nella barra del titolo o nel menu contestuale delle schede:
  - **Mute/Unmute rapido** dell'audio dell'account corrente (`CoreWebView2.IsMuted = True/False`).
  - **Indicatore multimediale** (icona altoparlante sulla scheda quando un account sta riproducendo note vocali, notifiche sonore o video).
  - Scorciatoia rapida "Copia link chat" o "Cancella cache account".
- **Impatto**: Medio | **Sforzo**: Basso-Medio

## 46. Modalità Privacy / Anti-Sbircio (Blur Mode / Screen Share Shield)
- **Descrizione**: Aggiungere una modalità privacy ad attivazione rapida (pulsante nella barra del titolo o scorciatoia `Alt+P`) che applica una sfocatura CSS (`filter: blur(5px)`) su messaggi, anteprime recenti nella sidebar, immagini/video e nomi dei contatti.
- **Interazione**: Il contenuto sfocato viene svelato temporaneamente e in modo fluido solo al passaggio del puntatore del mouse (`:hover`).
- **Utilità**: Protegge la riservatezza delle conversazioni in ufficio, in luoghi pubblici o durante la condivisione dello schermo (Teams, Zoom, Google Meet).
- **Impatto**: Alto | **Sforzo**: Basso

## 47. Modalità "Non Disturbare" / Focus Mode Temporizzata
- **Descrizione**: Aggiungere un pulsante rapido per silenziare tutte le notifiche (suoni, popup overlay e Windows Toast) per tutti gli account con un solo click.
- **Funzionalità**: Supporto a timer predefiniti (30 minuti, 1 ora, 8 ore, fino a disattivazione manuale) con ripristino automatico alla scadenza e indicatore visivo di stato nella barra del titolo.
- **Impatto**: Alto | **Sforzo**: Basso

## 48. Blocco dell'Applicazione con PIN / Master Password (App Lock)
- **Descrizione**: Possibilità di proteggere HidaChat con un PIN o password all'avvio o dopo un periodo configurabile di inattività (es. 5, 15, 30 minuti), oltre alla possibilità di bloccare istantaneamente la schermata con `Ctrl+L`.
- **Implementazione**: Overlay modale WPF che oscura la finestra e disabilita le WebView2 fino all'inserimento del PIN corretto (memorizzato con hash sicuro in `settings.json`).
- **Impatto**: Medio-Alto | **Sforzo**: Medio

## 49. Quick Switcher & Command Palette (`Ctrl+K`)
- **Descrizione**: Finestra modale di ricerca e navigazione rapida a scomparsa (`Ctrl+K` o `Ctrl+P`) in stile VS Code / Ferdium per:
  - Passare al volo tra gli account attivi digitando il nome.
  - Aprire le impostazioni o cambiare lingua/tema da tastiera.
  - Attivare al volo modalità privacy, traduzioni o ricaricare la scheda.
- **Impatto**: Medio | **Sforzo**: Medio

## 50. Ibernazione Intelligente delle Schede Inattive (Tab Hibernation)
- **Descrizione**: Ridurre l'impronta di memoria RAM sospendendo lo stato di rendering delle schede in background non utilizzate da più di 30-60 minuti, risvegliandole istantaneamente al click dell'utente.
- **Impatto**: Medio | **Sforzo**: Medio

## 51. Estensione a Nuove Piattaforme Web (Microsoft Teams, Slack, Discord)
- **Descrizione**: Estendere l'architettura `AppAccounts` per consentire all'utente di aggiungere schede per **Microsoft Teams** (`https://teams.microsoft.com/v2/`), **Slack** (`https://app.slack.com/client`) o **Discord** (`https://discord.com/app`) con icone vettoriali e temi dedicati.
- **Impatto**: Alto | **Sforzo**: Medio

## ~~52. Invio Massivo Personalizzato da File Excel / CSV (.xlsx, .xls, .csv)~~ ✅
- **Descrizione**: Modulo integrato per importare elenchi contatti da fogli di calcolo Excel (`.xlsx`, `.xls`) o file CSV (`.csv`) e inviare messaggi personalizzati uno alla volta tramite l'account WhatsApp Web attivo su WebView2.
- **Funzionalità Implementate**:
  - Rilevamento e mappatura automatica delle intestazioni di colonna (*Telefono*, *Nome*, *Cognome*, *Azienda*, *Testo personalizzato*).
  - Normalizzazione, pulizia caratteri non numerici e formattazione con prefisso internazionale.
  - Editor per template dinamici con segnaposto (`{Nome}`, `{Cognome}`, `{Azienda}`, `{Telefono}`, `{Testo}`) e pulsanti di inserimento rapido.
  - Finestra dedicata `BulkSenderWindow` con DataGrid interattivo, selezione puntuale o multipla e badge colorati di avanzamento per riga (`In attesa`, `Inviando...`, `Inviato ✔`, `Errore ✖`, `Non valido`).
  - Motore di automazione sequenziale `BulkSenderEngine` con rilevamento modali errore numeri non registrati (`INVALID_NUMBER`), injection script per l'invio chat e intervallo di attesa Anti-Spam regolabile (Jitter Delay) con countdown in tempo reale.
  - Pulsante dedicato con icona Excel integrato nella barra del titolo (`MainWindow.xaml`) e supporto multilingua completo in `Localization.vb`.
- **Impatto**: Alto | **Sforzo**: Medio





