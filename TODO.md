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

## ~~44. Correttore Ortografico Nativo Multilingua (Spellchecker WebView2)~~ ✅
- **Descrizione**: Abilitazione e gestione del motore di correzione ortografica nativo di Microsoft Edge WebView2 / Chromium nei campi di digitazione di WhatsApp Web e Telegram Web.
- **Funzionalità Implementate**: Sezione dedicata nelle **Impostazioni** con toggle di attivazione/disattivazione `ChkEnableSpellcheck`, selettore lingua dizionario `ComboSpellcheckLanguage` (Automatico da lingua app, Italiano `it-IT`, English `en-US`, Français `fr-FR`, Español `es-ES`, Deutsch `de-DE`), persistenza in `settings.json` e configurazione dinamica dei parametri Chromium (`--enable-features=Spellcheck`, `--lang=...`).
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

## ~~47. Modalità "Non Disturbare" / Focus Mode Temporizzata~~ ✅
- **Descrizione**: Silenziamento completo e istantaneo di tutte le notifiche (suoni WebView2, popup overlay e Windows Toast) per tutti gli account con timer configurabili e ripristino automatico alla scadenza.
- **Funzionalità Implementate**: Pulsante dedicato `BtnDnd` nella barra del titolo con icona Bell-Off e badge attivo con indicatore orario di fine nel tooltip, menu contestuale per scelta rapida della durata (*30m*, *1h*, *2h*, *8h*, *Indefinito*), muting audio sincrono delle WebView2 (`IsMuted = True`), soppressione totale di Toast/Popup e configurazione nelle Impostazioni.
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

## ~~53. Integrazione Console Web GUI di OpenClaw via Tailscale (Mesh VPN & Gateway Token)~~ ✅
- **Descrizione**: Aggiungere il supporto nativo a **OpenClaw** come piattaforma di account in HidaChat (accanto a WhatsApp e Telegram), consentendo di gestire sessioni di chat, monitoraggio log, esecuzione skill e configurazione del gateway AI OpenClaw all'interno di una tab dedicata isolata in WebView2.
- **Specifiche Tecniche & Architettura**:
  1. **Modello Account & Configurazione (`AppAccounts.vb`, `AccountManager.vb`)**:
     - Estensione enum/proprietà `Platform`: aggiunta supporto `"OpenClaw"` con property booleana `IsOpenClaw`.
     - Nuove proprietà persistenti per l'account:
        - `ServerUrl` (String): URL del gateway OpenClaw (default: `http://127.0.0.1:18789` o dominio Tailnet).
        - `TailscaleIntegration` (Boolean): Flag per abilitare il routing tramite nodo Tailscale embedded (`tsnetd.exe`) con reverse proxy loopback e token di sicurezza locale.
     - Palette grafica dedicata: icona vettoriale SVG a tema (robot/claw/terminale) e brush distintivo (es. `#FF5722` arancione o `#7C4DFF` viola).
  2. **Integrazione di Rete & Tailscale Mesh VPN (tsnet Userspace)**:
     - **Companion `tsnetd.exe`**: Binario statico autonomo Go con stack gVisor e WireGuard (Zero Installation, nessun driver o client Tailscale di sistema richiesto).
     - **Loopback Reverse Proxy (`127.0.0.1:<randomPort>`)**: Ascolto su porta dinamica casuale ad uso esclusivo di WebView2, protetto da header/cookie `X-HidaChat-Local-Token`.
     - **Persistenza Portabile (`data/tsnet/`)**: Lo stato del nodo viaggia con la cartella dati di HidaChat mantenendo stabile l'identità del dispositivo sulla Tailnet.
     - **Flusso Autenticazione & Login**: Gestione stati `Running`, `NeedsLogin` (con apertura browser) e supporto AuthKey cifrata con Windows DPAPI.
       - Fallback e validazione: test di connettività asincrono (ping HTTP/healthcheck su `/api/health` o endpoint radice) prima della navigazione, con visualizzazione dello stato online/offline del nodo gateway.
  3. **Gestione del Token & Flusso di Pairing**:
     - Composizione automatica dell'URL di bootstrap con token di autenticazione (`?token=<AuthToken>`) o iniezione sicura via header HTTP `Authorization: Bearer <AuthToken>` tramite evento `WebResourceRequested` di CoreWebView2.
     - Supporto al *token stripping* automatico di OpenClaw (il token viene rimosso dalla barra indirizzi dopo l'autenticazione per evitare leak visivi).
  4. **Notifiche & Monitoraggio Eventi**:
     - Intercettazione degli alert del Control UI di OpenClaw (completamento task agentici, errori del gateway, nuovi messaggi ricevuti) tramite bridge JavaScript dedicato (`NotificationJsScripts.vb`) e conversione in notifiche native **Windows Toast** e badge di notifica sulla scheda.
  5. **UI di Configurazione (`SettingsWindow.xaml`)**:
     - Selettore piattaforma esteso con opzione OpenClaw.
     - Form dinamico con campi specifici (URL Gateway, Token segreto con opzione visualizza/incolla, pulsante "Verifica Connessione").
- **Impatto**: Alto | **Sforzo**: Medio-Alto

## ~~54. Integrazione Dashboard & Web Console di Hermes Agent via Tailscale (Mesh VPN & API/Token Auth)~~ ✅
- **Descrizione**: Aggiunto il supporto nativo a **Hermes Agent** (Nous Research) come piattaforma di account in HidaChat, consentendo agli utenti di avere una tab dedicata in WebView2 per interagire con l'assistente (chat / TUI integrato), monitorare i log, gestire le skill e i task automatici (cron jobs) e configurare i parametri del gateway Hermes sia in locale che tramite rete mesh privata Tailscale.
- **Funzionalità Implementate**:
  - Estensione del modello account (`AppAccounts.vb`, `AccountManager.vb`) con piattaforma `"Hermes"`, proprietà `IsHermes`, branding grafico (icona vettoriale SVG a tema elmo alato e brush distintivo `#00B0FF`), e porta proxy locale deterministica isolata (`18900+`).
  - Configurazione endpoint flessibile con URL predefinito `http://127.0.0.1:9119` (istanza dashboard locale standard) o host Tailscale privati.
  - Iniezione sicura del token di autenticazione o API Key tramite header HTTP `Authorization: Bearer <AuthToken>` con filtro WebView2 `WebResourceRequested` e supporto parametri URL.
  - Routing di rete con Tailscale mesh proxy (`tsnetd`) integrato con header di autenticazione e token locale `X-HidaChat-Local-Token`.
  - Aggiornamento della UI (`SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `MainWindow.xaml.vb`) con selezione piattaforma Hermes Agent, form di configurazione contestuale con label e tooltip dedicati, menu di aggiunta rapida e test di connettività HTTP in tempo reale.
  - Localizzazione completa multilingua (IT, EN, FR, ES, DE) in `Localization.vb`.
- **Impatto**: Alto | **Sforzo**: Medio-Alto

## ~~55. Resilienza Crash WebView2 & Auto-Recovery Trasparente (`ProcessFailed`)~~ ✅
- **Descrizione**: Gestione degli errori irreversibili del runtime Microsoft Edge WebView2 (`BrowserProcessExited`, `RenderProcessExited`, crash GPU o aggiornamenti silenziosi del runtime in background).
- **Funzionalità Implementate**:
  - Intercettazione strongly-typed di `CoreProcessFailed` e `ProcessFailed` a livello di controllo WPF e CoreWebView2.
  - Auto-reload immediato per crash isolati del processo di rendering (`RenderProcessExited`).
  - Ricreazione completa e trasparente del controllo (`RecreateAccountWebViewAsync`) per crash dell'intero browser Chromium o invalidamento dell'HWND.
  - Prevenzione pop-up di errore e re-switch automatico trasparente al cambio scheda (`SwitchToAccountAsync` & `AccountTab_Click`).
  - Hardening del pulsante di ricarica (`BtnReloadActiveTab_Click`) e disiscrizione sicura dei listener in `Dispose()`.
- **Impatto**: Alto | **Sforzo**: Basso-Medio

---

# REVISIONE CODICE — AFFIDABILITÀ & AGGIORNAMENTI

## ~~56. Download aggiornamento: timeout di 15 s e ZIP interamente in RAM~~ ✅
- **File**: `UpdateChecker.vb` (`Shared Sub New`, `PerformUpdateFromGitHubAsync`)
- **Problema**: `_httpClient.Timeout = 15s` copre l'intera operazione: `GetByteArrayAsync(downloadUrl)` fallisce con uno ZIP di grandi dimensioni o con connessioni lente. Inoltre l'intero archivio viene caricato in memoria prima della scrittura su disco.
- **Fix Implementato**:
  - Introdotto client `_downloadClient` dedicato con `Timeout = InfiniteTimeSpan` e header User-Agent di prodotto;
  - Streaming HTTP diretto su file temporaneo su disco (`File.Create(tempZipPath)`) tramite `ResponseHeadersRead` e `CopyToAsync`, evitando di caricare l'intero archivio in memoria RAM;
  - CancellationTokenSource con timeout globale esteso a 10 minuti per consentire il download affidabile anche su connessioni lente;
  - Calcolo asincrono dell'hash SHA-256 in streaming direttamente dal file su disco tramite `SHA256.HashDataAsync(stream)`.
- **Impatto**: Alto | **Sforzo**: Basso

## ~~57. Verifica integrità aggiornamento: fail-closed e autenticità~~ ✅
- **File**: `UpdateChecker.vb` (`PerformUpdateFromGitHubAsync`, `ExtractSha256FromText`, `WriteLocalVersionMarker`), `Localization.vb`
- **Problema**:
  - Se non viene reperito alcun checksum l'aggiornamento viene installato comunque ("verificato via HTTPS");
  - L'hash proviene dalla stessa release dello ZIP: protegge da errori di trasmissione, non da una release compromessa su GitHub;
  - `WriteLocalVersionMarker` viene invocato prima della copia dei file: se lo script robocopy fallisce, il marker dichiara ugualmente la nuova versione (mentre lo scrive già il batch al termine della copia);
  - Il messaggio di errore per permessi insufficienti suggerisce `C:\Programmi\HidaChat`, cartella di norma non scrivibile per utenti standard senza privilegi elevati (mentre l'applicazione scrive in `data/` accanto all'eseguibile);
  - Il fallback regex su qualsiasi stringa esadecimale da 64 caratteri nelle note di rilascio può intercettare un hash non pertinente.
- **Fix Implementato**:
  - Controllo di integrità rigoroso fail-closed: blocco immediato della procedura se non viene reperito un checksum SHA-256 valido (nell'asset `.sha256` o nel corpo del rilascio);
  - Restrizione della ricerca dell'impronta crittografica esclusivamente al nome del file ZIP della release corrente (`<hash> <filename>`, `<filename> <hash>`, o etichetta `SHA256:`), eliminando il fallback generico a qualsiasi stringa hex da 64 caratteri;
  - Verifica della firma digitale Authenticode sull'eseguibile estratto `HidaChat.exe` (se firmato ne valida certificato e periodo di validità, bloccando l'aggiornamento in caso di firma corrotta);
  - Rimozione della chiamata anticipata a `WriteLocalVersionMarker`: il marcatore di versione viene scritto unicamente da `update.bat` solo dopo il completamento effettivo della copia robocopy;
  - Riformulazione del messaggio sui permessi di scrittura insufficienti, raccomandando cartelle utente scrivibili (`Documenti`, `Desktop`, unità USB) in conformità con la portabilità 100%;
  - Aggiunta e sincronizzazione dei messaggi di diagnostica ed errore dell'aggiornamento nei dizionari di tutte e 5 le lingue supportate (`Localization.vb`: IT, EN, FR, ES, DE).
- **Impatto**: Medio-Alto | **Sforzo**: Medio

## 58. `settings.json`: scrittura atomica, serializzata, con flush in chiusura
- **File**: `SettingsController.vb` (`WriteSettingsAsync`, `ReadSettingsAsync`, `FlushAfterDebounceAsync`, campo `_lastFlushTask`), `AccountManager.vb` (`SaveAccountsAsync`), finestra principale (`OnClosing`/chiusura) e `ForceExitForUpdate`
- **Problema**:
  - La scrittura non è atomica: un arresto anomalo o interruzione a metà scrittura lascia un file JSON troncato o corrotto;
  - Assenza di meccanismo di lock: `SaveAccountsAsync` e il flush con debounce possono accedere allo stesso file in parallelo (`IOException` silenziata = scrittura persa);
  - `ReadSettingsAsync` sopprime qualsiasi eccezione e restituisce un dizionario vuoto: in caso di corruzione momentanea si perdono le impostazioni e l'intero elenco account;
  - `_lastFlushTask` è dichiarato ma mai assegnato né atteso: le modifiche effettuate meno di 500 ms prima della chiusura dell'app vanno perse.
- **Fix**:
  ```vb
  Private ReadOnly _ioLock As New SemaphoreSlim(1, 1)
  ' in WriteSettingsAsync, dopo la serializzazione:
  Await _ioLock.WaitAsync()
  Try
      Dim tmp = targetFile & ".tmp"
      Await File.WriteAllTextAsync(tmp, json)
      If File.Exists(targetFile) Then
          File.Replace(tmp, targetFile, targetFile & ".bak")
      Else
          File.Move(tmp, targetFile)
      End If
  Finally
      _ioLock.Release()
  End Try
  ```
  In lettura: se il JSON non è valido, rinominare in `settings.corrupt-<timestamp>.json`, tentare il ripristino dal file di backup `.bak`, e solo come ultima risorsa partire da valori di default (notificando l'utente con un avviso esplicito). Aggiungere un metodo sincrono/immediato `FlushNowAsync()` (che annulla il debounce e scrive subito su disco se `_dirty`) da invocare in chiusura finestra e in `ForceExitForUpdate`; utilizzare attivamente `_lastFlushTask` oppure rimuoverlo.
- **Impatto**: Alto | **Sforzo**: Basso-Medio

## 59. Pulizia dei profili WebView2 non distruttiva
- **File**: `AccountManager.vb` (`CleanupUnusedProfilesAsync`, `MigrateOrphanProfileAsync`, `CreateDefaultAccountAsync`), `AppAccounts.vb` (`SetupWebViewInternalAsync`)
- **Problema**:
  - Catena di perdita irreversibile dati: `settings.json` corrotto/illeggibile → caricamento account predefinito di fallback → riscrittura file → al riavvio successivo `CleanupUnusedProfilesAsync` elimina definitivamente i profili su disco di tutti gli altri account configurati (sessioni perse, QR code da scansionare nuovamente);
  - `CleanupUnusedProfilesAsync` (tramite `DeleteDirectoryWithRetryAsync`) esegue cancellazioni ricorsive sul thread UI all'avvio dell'applicazione per profili Chromium che possono pesare centinaia di MB;
  - `MigrateOrphanProfileAsync` e `SetupWebViewInternalAsync` eliminano direttamente una cartella di profilo valida `WV2Profile_{id}` per sostituirla con quella orfana.
- **Fix**: Eseguire la pulizia dei profili solo se l'elenco account è stato caricato con successo da un file di configurazione valido (escludendo il fallback di default); spostare i profili non referenziati in `data/webview/_trash/` con eliminazione differita e tentativi con retry (`DeleteDirectoryWithRetryAsync`, vedi #37) anziché invocare `Delete` immediato; spostare l'intera scansione e pulizia in `Task.Run` fuori dal thread UI; per il profilo orfano rinominare l'eventuale profilo esistente in `.bak` anziché cancellarlo preventivamente.
- **Impatto**: Alto | **Sforzo**: Medio

## 60. Segreti portabili: DPAPI CurrentUser vs portabilità
- **File**: `SettingsController.vb` (`SaveTsnetAuthKeyAsync`, `LoadSettingsAsync`), `AppAccounts.vb` (`AuthToken`); impatta anche #53 e #54 (`AuthToken` / `ApiKey`)
- **Problema**: L'uso di `ProtectedData` con `DataProtectionScope.CurrentUser` vincola i segreti all'utente e alla macchina Windows locale: spostando la cartella dell'applicazione su un altro PC (su chiavetta USB o disco esterno) `Unprotect` fallisce e il blocco `Catch` azzera la chiave in silenzio. Inoltre, in `AppAccounts.vb` il token di autenticazione per OpenClaw ed Hermes viene accodato in chiaro come query parameter (`?token=`) nell'URL di navigazione, restando visibile nei log e nella cronologia. Questo compromette il principio di portabilità 100%.
- **Fix**:
  - Su errore di decifratura mostrare un messaggio esplicito all'utente richiedendo il reinserimento delle credenziali anziché azzerare silenziosamente il segreto;
  - Non persistere l'`AuthKey` tsnet dopo il completamento del primo enrollment se il nodo mantiene il proprio stato nella state directory locale; predisporre una state directory separata per macchina (es. `data/tsnet/<hostname>`) per prevenire la clonazione dell'identità del nodo su host differenti;
  - Opzionale: implementare master password con cifratura AES-GCM e derivazione della chiave (PBKDF2/Argon2) per segreti portabili indipendenti dalla macchina, riutilizzabile per la funzionalità App Lock (#48);
  - Per i gateway OpenClaw/Hermes iniettare il token tramite header HTTP `Authorization: Bearer` via evento `WebResourceRequested` di CoreWebView2 invece di esporlo in query string nell'URL.
- **Impatto**: Medio-Alto | **Sforzo**: Medio

---

# REVISIONE CODICE — SICUREZZA WEBVIEW

## 61. NewWindowRequested: ShellExecute su qualsiasi schema URI
- **File**: `AppAccounts.vb` (`_newWindowRequestedHandler`)
- **Problema**: Per qualsiasi URL che non appartiene agli host noti della piattaforma corrente, l'handler invoca `Process.Start(..., UseShellExecute = True)` senza validare lo schema dell'URI: link con schemi pericolosi o non controllati (`file:`, `ms-msdt:`, protocol handler registrati a livello di sistema) vengono eseguiti arbitrariamente. Inoltre, i link con attributo `target="_blank"` appartenenti alla stessa origine navigano l'istanza WebView corrente sovrascrivendo la sessione di chat aperta.
- **Fix**: Applicare una allow-list rigorosa degli schemi prima dell'apertura esterna (es. solo `http`, `https`, `mailto`); estendere la medesima validazione a `NavigationStarting` (i deep link Telegram `tg://` restano gestiti dalla traduzione interna verso Web K/A, vedi #41):
  ```vb
  Dim allowed = {Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeMailto}
  If allowed.Contains(uri.Scheme) Then
      Process.Start(New ProcessStartInfo(e.Uri) With {.UseShellExecute = True})
  End If
  ```
  Aprire nel browser predefinito di sistema anche i link `target="_blank"` della medesima origine per evitare la perdita della vista di chat principale all'interno della WebView2.
- **Impatto**: Alto | **Sforzo**: Basso

## 62. Bridge IPC: token da CSPRNG e validazione dell'origine
- **File**: `AppAccounts.vb` (`GenerateBridgeToken`, `HandleWebMessageAsync`, `SetupWebViewInternalAsync`)
- **Problema**: Il token di sicurezza del bridge IPC viene generato combinando un timestamp Unix con `System.Random` (pseudo-casuale non crittografico, teoricamente prevedibile); l'handler `WebMessageReceived` / `HandleWebMessageAsync` valida il token ma non controlla l'origine (`e.Source`) del mittente del messaggio; la proprietà `AreDevToolsEnabled` viene impostata a `True` in modo incondizionato anche in ambienti di produzione.
- **Fix**: Generare il token IPC tramite CSPRNG crittograficamente sicuro (`Convert.ToHexString(RandomNumberGenerator.GetBytes(16))`); in `WebMessageReceived` verificare preventivamente che l'URI di provenienza `e.Source` corrisponda all'host autorizzato per la specifica piattaforma dell'account; abilitare i DevTools (`AreDevToolsEnabled`) esclusivamente nelle build di Debug o tramite opzione diagnostica avanzata nelle Impostazioni.
- **Impatto**: Medio | **Sforzo**: Basso

## 63. Async Sub senza gestione errori e Task non attesi nel bridge
- **File**: `AppAccounts.vb` (`_navigationCompletedHandler`, `HandleTranslationMessageAsync`, `Dispose`), `Application.xaml.vb`
- **Problema**:
  - `_navigationCompletedHandler` è dichiarato come `Async Sub` privo di blocco `Try/Catch`: eventuali chiamate ad `ExecuteScriptAsync` su una WebView disposta o durante un'interruzione di navigazione sollevano eccezioni in contesto `async void`, con conseguente crash fatale dell'applicazione;
  - `Await WebView.Dispatcher.InvokeAsync(Async Function() ...)` attende esclusivamente il completamento della `DispatcherOperation` esterna: il `Task` restituito dalla lambda asincrona interna non viene atteso e le sue eccezioni rimangono inosservate (unobserved exceptions);
  - Il metodo `Dispose` assegna `WebView = Nothing`: eventuali continuazioni asincrone ancora in corso (es. traduzioni batch o messaggi IPC in elaborazione) incorrono in `NullReferenceException`;
  - In `Application.xaml.vb` non sono configurati listener globali per le eccezioni non gestite.
- **Fix**: Introdurre blocchi `Try/Catch` e guardie difensive `If WebView?.CoreWebView2 Is Nothing Then Return` in tutti gli handler asincroni; rimuovere `Dispatcher.InvokeAsync` quando il codice è già in esecuzione sul thread UI oppure attendere esplicitamente il `Task` interno (`Await Await ...InvokeAsync(...)`); proteggere le continuazioni post-disposizione; registrare in `Application.xaml.vb` gli eventi `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException` e `AppDomain.UnhandledException` con scrittura dei dettagli su file di diagnostica in `data/logs/`.
- **Impatto**: Alto | **Sforzo**: Basso

## 64. DndUntil: confronto Nullable in VB.NET
- **File**: `SettingsController.vb` (`DndUntil`, `SetDndModeAsync`, `LoadSettingsAsync`)
- **Problema**: L'istruzione `If _dndUntil <> value Then` con tipo `Nullable(Of DateTime)` adotta la logica a tre valori di VB.NET: il confronto con `Nothing` restituisce `Nothing` (valutato come `False` nelle condizioni `If`), impedendo al setter di salvare il nuovo valore quando uno dei due operandi è `Nothing`. Il difetto è attualmente mascherato dal fatto che `SetDndModeAsync` e `LoadSettingsAsync` assegnano direttamente la variabile di campo privata `_dndUntil`.
- **Fix**: Sostituire il controllo con `If Not Nullable.Equals(_dndUntil, value) Then ...` e far transitare tutte le mutazioni di stato attraverso il setter pubblico della proprietà per garantire l'invio corretto degli eventi `PropertyChanged`.
- **Impatto**: Basso | **Sforzo**: Basso

---

# REVISIONE CODICE — MINORI & PREPARAZIONE ROADMAP

## 65. SettingsFile: I/O e migrazione a ogni accesso
- **File**: `SettingsController.vb` (proprietà `SettingsFile`)
- **Problema**: Il getter della proprietà `SettingsFile` esegue verifiche I/O su filesystem (`File.Exists`, `Directory.CreateDirectory`) e tenta la migrazione `File.Move` a ogni singola operazione di lettura o scrittura delle impostazioni.
- **Fix**: Calcolare e memorizzare il percorso una sola volta all'avvio (tramite `Lazy(Of String)` o durante `LoadSettingsAsync`) e spostare la migrazione iniziale da radice a `data/` all'interno di una procedura di inizializzazione dedicata.
- **Impatto**: Basso | **Sforzo**: Basso

## 66. Limite massimo account: sincronizzazione e policy di downgrade
- **File**: `AccountManager.vb` (`MaxAccounts`, `CanAddAccount`, `AddAccountAsync`), `SettingsController.vb` (`MaxAccounts`), `SettingsWindow.xaml.vb`
- **Problema**: In origine `AccountManager.MaxAccounts` era definito come costante rigida a 3. La proprietà è già stata resa dinamica in funzione di `SettingsController.MaxAccounts` (range 2–10) ed è già integrata nei controlli di `CanAddAccount` e `AddAccountAsync`. Manca tuttavia l'ascolto automatico di `_settingsController.PropertyChanged` all'interno di `AccountManager` per notificare le modifiche a `CanAddAccount` indipendentemente dalla UI di `SettingsWindow`, nonché una policy formale nel caso in cui l'utente imposti un limite inferiore al numero di account già configurati.
- **Fix**: Sottoscrivere l'evento `PropertyChanged` di `SettingsController` nel costruttore di `AccountManager` per sollevare automaticamente le notifiche per `MaxAccounts` e `CanAddAccount`; definire la policy per il downgrade del limite (es. mantenimento non distruttivo degli account eccedenti con blocco delle nuove aggiunte e messaggio esplicativo nelle Impostazioni).
*(Risolto in parte: `MaxAccounts` dinamico e controlli di aggiunta già implementati; mancano sincronizzazione eventi centralizzata e gestione downgrade)*
- **Impatto**: Medio | **Sforzo**: Basso

## 67. UpdateChecker: UX, localizzazione, confronto versioni, duplicazioni
- **File**: `UpdateChecker.vb` (`CheckForUpdatesAsync`, `PerformUpdateFromGitHubAsync`, `FetchGitHubReleaseInfoAsync`, `IsNewerVersion`)
- **Problema**:
  - Le finestre di dialogo `MessageBox` sono cablate in lingua italiana in un'applicazione localizzata in 5 lingue;
  - Con controllo forzato dall'utente (`force = True`), un eventuale errore di rete, mancata risposta delle API GitHub o rate limit non mostra alcun feedback visivo (registrato unicamente con `Debug.WriteLine`);
  - `IsNewerVersion` confronta i suffissi di pre-release come stringhe ordinali (`String.Compare`): la versione "beta10" risulta antecedente a "beta2";
  - La logica di parsing JSON dei dati di release è duplicata quasi per intero nei due rami di `FetchGitHubReleaseInfoAsync`;
  - La cartella temporanea `%TEMP%\HidaChat_Update` non viene rimossa al termine della procedura di estrazione ed aggiornamento.
- **Fix**: Introdurre le chiavi corrispondenti nei dizionari di `Localization.vb` e richiamarle tramite `Localizations.Get(...)` per tutte le lingue supportate; mostrare un dialogo informativo di errore quando `force = True`; implementare il parsing numerico dei suffissi di pre-release (o comparatore SemVer); unificare l'estrazione dati della release in una funzione condivisa `ParseRelease(JsonElement)`; pianificare l'eliminazione della cartella temporanea al primo avvio successivo; adottare un sistema di logging su file anziché limitarsi a `Debug.WriteLine`.
- **Impatto**: Medio | **Sforzo**: Basso-Medio

## 68. Piattaforme: da stringhe a modello tipizzato
- **File**: `AccountManager.vb`, `AppAccounts.vb`, `SettingsWindow.xaml.vb`
- **Problema**: La gestione delle piattaforme è basata su stringhe non tipizzate (`"WhatsApp"`, `"Telegram"`, `"OpenClaw"`, `"Hermes"`). Sebbene le voci #53 e #54 siano già state integrate implementando le rispettive piattaforme, la configurazione degli URL, icone SVG, colori, script iniettati e regole host è articolata attraverso lunghe serie di condizioni `If / ElseIf` e flag booleani sparsi (`IsOpenClaw`, `IsHermes`, `IsTelegram`, `IsWhatsApp`).
- **Fix**: Introdurre un'astrazione tipizzata come enum `ChatPlatform` o classe `PlatformDefinition` (responsabile di URL predefiniti, icone, palette colori, script di tema/notifiche/traduzioni e host consentiti per `NewWindowRequested`), garantendo la compatibilità con il valore stringa già persistito in `settings.json`. Refactoring architetturale propedeutico all'estensione verso nuove piattaforme (#51 come Teams, Slack, Discord).
- **Impatto**: Alto | **Sforzo**: Medio

## 69. Traduzione: privacy e backend self-hosted
- **File**: `Localization.vb` (`TranslateSingleAsync`, `TranslateBatchAsync`, `TranslateTextHttpAsync`)
- **Problema**: Il testo dei messaggi non ancora presente nella cache locale viene trasmesso direttamente a Google Translate (`translate.googleapis.com`): il contenuto testuale delle chat lascia il PC dell'utente senza una preventiva richiesta di autorizzazione, in contrasto con le esigenze di privacy e riservatezza.
- **Fix**: Rendere la funzione di traduzione opt-in presentando un avviso esplicativo al primo utilizzo; definire un'interfaccia `ITranslationProvider` per supportare sia Google Translate (provider predefinito attuale) sia provider self-hosted o locali (LibreTranslate, Ollama o endpoint compatibili OpenAI con URL configurabile); restituire un messaggio d'errore chiaro e discreto qualora il provider configurato risulti irraggiungibile.
- **Impatto**: Medio | **Sforzo**: Medio

## 70. Uso portabile: lock multi-PC e coerenza del README
- **File**: `Application.xaml.vb`, `README.md`, `README.it.md`, `publish.ps1`
- **Problema**: Il file README raccomanda di non eseguire la stessa istanza portabile contemporaneamente da postazioni differenti (rischio concreto di lock file e corruzione del profilo WebView2), ma l'applicazione non adotta alcun meccanismo di prevenzione a livello di filesystem. Inoltre, l'indicazione "Zero Installation" contrasta con la necessità del runtime .NET 9 per le pubblicazioni standard framework-dependent.
- **Fix**: Creare all'avvio un file di lock `data/.lock` (registrando hostname, ID processo e timestamp) con rilascio controllato all'uscita e segnalazione bloccante all'utente se la cartella risulta già utilizzata da un'altra macchina o processo; valutare la compilazione in formato self-contained (gestendo il conseguente aumento dimensionale dello ZIP, vedi #56) oppure armonizzare il testo del README esplicitando il requisito del runtime .NET 9.
- **Impatto**: Medio | **Sforzo**: Basso-Medio
