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

## ~~50. Ibernazione Intelligente delle Schede Inattive (Tab Hibernation)~~ ✅
- **Descrizione**: Ridurre l'impronta di memoria RAM sospendendo lo stato di rendering delle schede in background non utilizzate da più di 15-60 minuti (configurabile, default 30 min), risvegliandole istantaneamente al click dell'utente.
- **Implementazione**:
  - Aggiunti metodi `TryHibernateAsync()` e `ResumeFromHibernation()` in `AppAccounts.vb` basati sulle API native `CoreWebView2.TrySuspendAsync()` e `CoreWebView2.Resume()`, con tracciamento `LastActiveTime` e proprietà `IsHibernated`.
  - Timer periodico in background in `MainWindow.xaml.vb` (`_hibernationTimer`, 60s) che verifica l'inattività delle schede non in primo piano e le sospende in modo trasparente.
  - Risveglio istantaneo (0 ms di ritardo) al passaggio del focus sulla scheda in `SwitchToAccountAsync` o impostando `IsActive = True`.
  - Indicatore visivo `💤` sulle schede ibernate nella barra orizzontale di `MainWindow.xaml`.
  - Configurazione nelle Impostazioni (`SettingsWindow.xaml`): toggle di attivazione `ChkEnableTabHibernation` e selettore intervallo `ComboTabHibernationTimeout` (15, 30, 60 min) con persistenza in `settings.json` e supporto multilingua completo in tutte le 5 lingue (EN, IT, FR, ES, DE).
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

## ~~58. `settings.json`: scrittura atomica, serializzata, con flush in chiusura~~ ✅
- **File**: `SettingsController.vb` (`WriteSettingsAsync`, `WriteSettingsInternalAsync`, `ReadSettingsAsync`, `FlushAfterDebounceAsync`, `FlushNowAsync`, campo `_lastFlushTask`, `_ioLock`), `AccountManager.vb` (`SaveAccountsAsync`), `MainWindow.xaml.vb` (`ExitApplication`, `ForceExitForUpdateAsync`, `ForceExitForUpdate`, `MainWindow_Closing`), `Localization.vb`
- **Problema**:
  - La scrittura non è atomica: un arresto anomalo o interruzione a metà scrittura lascia un file JSON troncato o corrotto;
  - Assenza di meccanismo di lock: `SaveAccountsAsync` e il flush con debounce possono accedere allo stesso file in parallelo (`IOException` silenziata = scrittura persa);
  - `ReadSettingsAsync` sopprime qualsiasi eccezione e restituisce un dizionario vuoto: in caso di corruzione momentanea si perdono le impostazioni e l'intero elenco account;
  - `_lastFlushTask` è dichiarato ma mai assegnato né atteso: le modifiche effettuate meno di 500 ms prima della chiusura dell'app vanno perse.
- **Fix Implementato**:
  - Introdotto semaforo asincrono `_ioLock As New SemaphoreSlim(1, 1)` in `SettingsController.vb` che serializza rigorosamente tutte le letture e le scritture di `settings.json`, prevenendo conflitti e corruzioni I/O concorrenti;
  - Scrittura atomica a doppio stadio: salvataggio preventivo su file temporaneo `.tmp`, seguito da sostituzione atomica tramite `File.Replace(tmp, targetFile, targetFile & ".bak")` con rotazione automatica del backup `.bak` (e fallback con `File.Copy` + `File.Move`);
  - Pulizia deterministica del file `.tmp` nel blocco `Finally` in caso di interruzione o errore;
  - Tracciamento attivo di `_lastFlushTask` nelle scritture e nel debounce a 500 ms;
  - Metodo `FlushNowAsync()` che annulla qualsiasi debounce pendente, attende il completamento dell'I/O corrente e scrive subito su disco le impostazioni se `_dirty = True`;
  - Invocazione sicura di `SaveAccountsAsync()` e `FlushNowAsync()` in chiusura applicazione (`ExitApplication`), prima del riavvio per aggiornamento OTA (`ForceExitForUpdateAsync` / `ForceExitForUpdate`) e nell'evento `MainWindow_Closing`;
  - Resilienza e auto-recovery in lettura (`ReadSettingsAsync`): in caso di JSON illeggibile o troncato, quarantena automatica del file in `settings.corrupt-<timestamp>.json`, tentato ripristino trasparente dall'ultimo backup `.bak` valido e notifica all'utente con messaggi localizzati in tutte le 5 lingue supportate (`Localization.vb`: IT, EN, FR, ES, DE).
- **Impatto**: Alto | **Sforzo**: Basso-Medio

## ~~59. Pulizia dei profili WebView2 non distruttiva~~ ✅
- **File**: `AccountManager.vb` (`LoadAccountsAsync`, `CleanupUnusedProfilesAsync`, `MigrateOrphanProfile`, `CreateDefaultAccountAsync`, `RemoveAccountAsync`), `AppAccounts.vb` (`SetupWebViewInternalAsync`), `SettingsController.vb` (`IsLoadedFromValidConfig`, `ReadSettingsAsync`)
- **Problema**:
  - Catena di perdita irreversibile dati: `settings.json` corrotto/illeggibile → caricamento account predefinito di fallback → riscrittura file → al riavvio successivo `CleanupUnusedProfilesAsync` elimina definitivamente i profili su disco di tutti gli altri account configurati (sessioni perse, QR code da scansionare nuovamente);
  - `CleanupUnusedProfilesAsync` (tramite `DeleteDirectoryWithRetryAsync`) esegue cancellazioni ricorsive sul thread UI all'avvio dell'applicazione per profili Chromium che possono pesare centinaia di MB;
  - `MigrateOrphanProfileAsync` e `SetupWebViewInternalAsync` eliminano direttamente una cartella di profilo valida `WV2Profile_{id}` per sostituirla con quella orfana.
- **Fix Implementato**:
  - Tracciamento della validità del file di configurazione con proprietà `IsLoadedFromValidConfig` in `SettingsController.vb`: la pulizia dei profili orfani/non referenziati viene eseguita esclusivamente se l'elenco account è stato caricato con successo da un file JSON integro o dal relativo backup `.bak`, escludendo qualsiasi esecuzione in caso di fallback o corruzione;
  - Spostamento non distruttivo dei profili non referenziati nella cartella di cestino `data/webview/_trash/` (`Directory.Move` istantanea a livello di filesystem) con timestamp univoco, preservando le sessioni utente in caso di riconfigurazioni accidentali;
  - Eliminazione differita in background (`Task.Run`) per gli elementi in `_trash/` con retention superiore alle 24 ore mediante tentativi multipli con backoff esponenziale (`DeleteDirectoryWithRetryAsync`);
  - Spostamento dell'intera routine di scansione e pulizia profili in `Task.Run` fuori dal thread UI, evitando freeze o ritardi nel rendering dell'interfaccia grafica e nell'aggancio immediato della scheda attiva;
  - Preservazione non distruttiva dei profili esistenti in `MigrateOrphanProfile` (`AccountManager.vb`) e `SetupWebViewInternalAsync` (`AppAccounts.vb`): in presenza di un profilo orfano anonimo (`WV2Profile_`), l'eventuale profilo esistente su disco viene rinominato in `.bak` (con timestamp anticollisione) anziché cancellato preventivamente, con rollback automatico in caso di errore nello spostamento dell'orfano;
  - Auto-discovery e ricostruzione automatica di tutti i profili esistenti su disco in `CreateDefaultAccountAsync`: in caso di primo avvio o perdita del file di configurazione, vengono rilevate e registrate tutte le cartelle di profilo `WV2Profile_*` presenti a livello principale, evitando che vengano isolate o perse;
  - Rimozione account non distruttiva e non bloccante in `RemoveAccountAsync`: spostamento preliminare nel cestino `_trash/` e cancellazione differita asincrona in background con backoff per consentire il rilascio dei file di lock da parte del processo Chromium.
- **Impatto**: Alto | **Sforzo**: Medio

## ~~60. Segreti portabili: DPAPI CurrentUser vs portabilità~~ ✅
- **File**: `SettingsController.vb` (`SaveTsnetAuthKeyAsync`, `LoadSettingsAsync`, `ClearTsnetAuthKeyAfterEnrollmentAsync`), `TsnetManager.vb` (`GetMachineStateDir`, `StartAsync`, `CheckStatusAsync`), `AppAccounts.vb` (`WebUrl`, `SetupWebViewInternalAsync`, `EnsureLocalProxyAsync`), `MainWindow.xaml.vb`, `Localization.vb`
- **Problema**: L'uso di `ProtectedData` con `DataProtectionScope.CurrentUser` vincola i segreti all'utente e alla macchina Windows locale: spostando la cartella dell'applicazione su un altro PC (su chiavetta USB o disco esterno) `Unprotect` fallisce e il blocco `Catch` azzera la chiave in silenzio. Inoltre, in `AppAccounts.vb` il token di autenticazione per OpenClaw ed Hermes veniva accodato in chiaro come query parameter (`?token=`) nell'URL di navigazione, restando visibile nei log e nella cronologia. Questo comprometteva il principio di portabilità 100%.
- **Fix Implementato**:
  - In caso di errore di decifratura DPAPI (`CryptographicException`), l'applicazione non azzera più il segreto silenziosamente: imposta `TsnetAuthKeyDecryptionFailed = True`, registra un log esplicito e mostra un dialogo di notifica all'utente richiedendo il reinserimento delle credenziali nelle Impostazioni;
  - Isolamento della directory di stato del nodo tsnet per singola macchina (`data/tsnet/<hostname>`), prevenendo conflitti e la clonazione dell'identità del nodo su host differenti in ambiente portabile;
  - Rilascio e rimozione automatica della `tsnetAuthKey` salvata una volta completato l'enrollment con successo (transizione a stato `Running`), evitando la persistenza permanente di chiavi monouso nel file di configurazione portabile;
  - Rimozione totale del parametro `token=` in chiaro dalla query string dell'URL per OpenClaw ed Hermes; il token di autenticazione viene ora iniettato in modo sicuro e trasparente tramite header HTTP `Authorization: Bearer <token>` intercettato via evento `WebResourceRequested` di CoreWebView2 (sia in modalità diretta che tramite proxy locale tsnet);
  - Aggiunte e sincronizzate le chiavi di notifica errore decifratura (`secret_decrypt_error_title`, `secret_decrypt_error_msg`) in tutti i 5 dizionari linguistici (`Localization.vb`: IT, EN, FR, ES, DE).
- **Impatto**: Medio-Alto | **Sforzo**: Medio

---

# REVISIONE CODICE — SICUREZZA WEBVIEW

## ~~61. NewWindowRequested: ShellExecute su qualsiasi schema URI~~ ✅
- **File**: `AppAccounts.vb` (`_newWindowRequestedHandler`, `_navigationStartingHandler`)
- **Problema**: Per qualsiasi URL che non appartiene agli host noti della piattaforma corrente, l'handler invoca `Process.Start(..., UseShellExecute = True)` senza validare lo schema dell'URI: link con schemi pericolosi o non controllati (`file:`, `ms-msdt:`, protocol handler registrati a livello di sistema) vengono eseguiti arbitrariamente. Inoltre, i link con attributo `target="_blank"` appartenenti alla stessa origine navigano l'istanza WebView corrente sovrascrivendo la sessione di chat aperta.
- **Fix Implementato**:
  - Applicata una allow-list rigorosa degli schemi prima dell'apertura esterna (`http`, `https`, `mailto`), bloccando l'esecuzione arbitraria di protocol handler di sistema o schemi non autorizzati;
  - Estesa la validazione dello schema URI a `NavigationStarting`, consentendo solo schemi web standard (`http`, `https`, `data`, `blob`, `about`) e gestendo internamente i deep link Telegram (`tg://` e `t.me`);
  - Apertura nel browser predefinito di sistema di tutti i link `target="_blank"` o esterni per preservare integra la vista principale della sessione di chat all'interno della WebView2.
- **Impatto**: Alto | **Sforzo**: Basso

## ~~62. Bridge IPC: token da CSPRNG e validazione dell'origine~~ ✅
- **File**: `AppAccounts.vb` (`GenerateBridgeToken`, `HandleWebMessageAsync`, `IsAuthorizedOrigin`, `SetupWebViewInternalAsync`)
- **Problema**: Il token di sicurezza del bridge IPC viene generato combinando un timestamp Unix con `System.Random` (pseudo-casuale non crittografico, teoricamente prevedibile); l'handler `WebMessageReceived` / `HandleWebMessageAsync` valida il token ma non controlla l'origine (`e.Source`) del mittente del messaggio; la proprietà `AreDevToolsEnabled` viene impostata a `True` in modo incondizionato anche in ambienti di produzione.
- **Fix Implementato**:
  - Generazione del token IPC di sicurezza della WebView tramite CSPRNG a 128 bit (`RandomNumberGenerator.Fill(randomBytes)`);
  - Validazione rigorosa dell'origine di provenienza del messaggio (`e.Source`) tramite la funzione dedicata `IsAuthorizedOrigin` prima del parsing e dell'elaborazione del payload IPC (`web.whatsapp.com` per WhatsApp, `web.telegram.org` per Telegram, host di `ServerUrl` o `127.0.0.1` per OpenClaw ed Hermes);
  - Disabilitazione dei DevTools (`AreDevToolsEnabled = False`) nelle build di produzione Release (abilitati unicamente sotto compilazione `#If DEBUG`).
- **Impatto**: Medio | **Sforzo**: Basso

## ~~63. Async Sub senza gestione errori e Task non attesi nel bridge~~ ✅
- **File**: `AppAccounts.vb` (`_navigationCompletedHandler`, `_webMessageReceivedHandler`, `HandleTranslationMessageAsync`, `SafeExecuteScriptAsync`, `Dispose`), `Application.xaml.vb` (`OnStartup`, `OnDispatcherUnhandledException`, `OnUnobservedTaskException`, `OnAppDomainUnhandledException`, `LogUnhandledException`), `MainWindow.xaml.vb`
- **Problema**:
  - `_navigationCompletedHandler` e `_webMessageReceivedHandler` erano dichiarati come `Async Sub` privi di blocco `Try/Catch`: eventuali chiamate ad `ExecuteScriptAsync` su una WebView disposta o durante un'interruzione di navigazione sollevavano eccezioni in contesto `async void`, con conseguente crash fatale dell'applicazione;
  - `Await WebView.Dispatcher.InvokeAsync(Async Function() ...)` attendeva esclusivamente il completamento della `DispatcherOperation` esterna: il `Task` restituito dalla lambda asincrona interna non veniva atteso e le sue eccezioni rimanevano inosservate (unobserved exceptions);
  - Il metodo `Dispose` assegnava `WebView = Nothing`: eventuali continuazioni asincrone ancora in corso (es. traduzioni batch o messaggi IPC in elaborazione) incorrevano in `NullReferenceException`;
  - In `Application.xaml.vb` non erano configurati listener globali per le eccezioni non gestite.
- **Fix Implementato**:
  - Introdotti blocchi `Try/Catch` e guardie difensive `If _isDisposed OrElse WebView?.CoreWebView2 Is Nothing Then Return` in tutti gli handler asincroni di `AppAccounts.vb`;
  - Introdotto metodo helper `SafeExecuteScriptAsync` che verifica il thread di appartenenza, attende il `Task` interno unwrappato via `.Task.Unwrap()` qualora sia necessario il Dispatcher e previene accessi post-disposizione;
  - Aggiunto flag e proprietà `IsDisposed` su `AppAccounts`, con salvataggio di un riferimento locale `wv` in `Dispose()` prima di azzerare la proprietà `WebView`;
  - Registrati in `Application.xaml.vb` gli eventi `DispatcherUnhandledException` (con `e.Handled = True`), `TaskScheduler.UnobservedTaskException` (con `e.SetObserved()`) e `AppDomain.UnhandledException` con logging thread-safe su file portabile `data/logs/app_errors.log` (con rotazione automatica a 1 MB);
  - Protetti con `Try/Catch` e logging globale tutti gli eventi asincroni `Async Sub` anche in `MainWindow.xaml.vb` (`MainWindow_Loaded`, `PopulateWebViews`, `OnAccountProcessFailedRecoveryRequested`, `AccountTabRename_Click`, `OnSettingsPropertyChanged`, aggiunta account).
- **Impatto**: Alto | **Sforzo**: Basso

## ~~64. DndUntil: confronto Nullable in VB.NET~~ ✅
- **File**: `SettingsController.vb` (`DndUntil`, `SetDndModeAsync`)
- **Problema**: L'istruzione `If _dndUntil <> value Then` con tipo `Nullable(Of DateTime)` adotta la logica a tre valori di VB.NET: il confronto con `Nothing` restituisce `Nothing` (valutato come `False` nelle condizioni `If`), impedendo al setter di salvare il nuovo valore quando uno dei due operandi è `Nothing`. Il difetto era precedentemente mascherato dal fatto che `SetDndModeAsync` e `LoadSettingsAsync` assegnavano direttamente la variabile di campo privata `_dndUntil`.
- **Fix Implementato**:
  - Sostituito il confronto nel setter pubblico di `DndUntil` con `If Not Nullable.Equals(_dndUntil, value) Then ...`;
  - Riconfigurato `SetDndModeAsync` per mutare lo stato passando sempre attraverso i setter pubblici delle proprietà (`IsDndEnabled`, `DndUntil`, `DndDurationMode`), garantendo la corretta e sincrona emissione degli eventi `PropertyChanged` e `IsDndActive`.
- **Impatto**: Basso | **Sforzo**: Basso

---

# REVISIONE CODICE — MINORI & PREPARAZIONE ROADMAP

## ~~65. SettingsFile: I/O e migrazione a ogni accesso~~ ✅
- **File**: `SettingsController.vb` (proprietà `SettingsFile`, metodo `InitializeSettingsFilePath`)
- **Problema**: Il getter della proprietà `SettingsFile` eseguiva verifiche I/O su filesystem (`File.Exists`, `Directory.CreateDirectory`) e tentava la migrazione `File.Move` a ogni singola operazione di lettura o scrittura delle impostazioni.
- **Fix Implementato**:
  - Calcolo e memorizzazione persistente del percorso tramite `Lazy(Of String)` thread-safe (`_settingsFilePathLazy`), risolto una sola volta al primo accesso per l'intero ciclo di vita dell'applicazione;
  - Spostamento della logica di migrazione una tantum dalla cartella radice a `data/` all'interno della procedura di inizializzazione dedicata `InitializeSettingsFilePath`;
  - Rimozione totale di qualsiasi operazione di I/O o accesso al disco dal getter della proprietà `SettingsFile`.
- **Impatto**: Basso | **Sforzo**: Basso

## ~~66. Limite massimo account: sincronizzazione e policy di downgrade~~ ✅
- **File**: `AccountManager.vb` (`MaxAccounts`, `CanAddAccount`, `HasExcessAccounts`, `ExcessAccountsCount`, `AddAccountAsync`, `IDisposable`), `SettingsController.vb` (`MaxAccounts`), `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `MainWindow.xaml`, `MainWindow.xaml.vb`, `Localization.vb`
- **Problema**: In origine `AccountManager.MaxAccounts` era definito come costante rigida a 3. La proprietà era stata resa dinamica in funzione di `SettingsController.MaxAccounts` (range 2–10) ed era integrata nei controlli di `CanAddAccount` e `AddAccountAsync`. Mancava tuttavia l'ascolto automatico di `_settingsController.PropertyChanged` all'interno di `AccountManager` per notificare le modifiche a `CanAddAccount` e `MaxAccounts` indipendentemente dalla UI di `SettingsWindow`, nonché una policy formale nel caso in cui l'utente imposti un limite inferiore al numero di account già configurati.
- **Fix Implementato**:
  - Sottoscrizione centralizzata dell'evento `PropertyChanged` di `SettingsController` nel costruttore di `AccountManager` per notificare automaticamente le modifiche a `MaxAccounts`, `CanAddAccount`, `HasExcessAccounts` ed `ExcessAccountsCount`; implementazione dell'interfaccia `IDisposable` in `AccountManager` per rilasciare pulitamente i listener;
  - Policy formale di downgrade non distruttivo: qualora il limite `MaxAccounts` venga abbassato al di sotto degli account configurati, gli account esistenti vengono interamente preservati e restano funzionanti, mentre viene bloccata l'aggiunta di ulteriori account e mostrato un banner esplicativo con avviso nelle Impostazioni (`BorderDowngradeNotice`);
  - Rimozione del tooltip statico hardcoded "(max 3)" dal pulsante `BtnAddAccount` in `MainWindow.xaml` e sincronizzazione dinamica di tooltip, stato abilitato/disabilitato e blocco aggiunta sia in `MainWindow` che in `SettingsWindow`;
  - Aggiunta e sincronizzazione delle chiavi di downgrade (`max_accounts_downgrade_notice`, `accounts_downgrade_blocked`) nei dizionari di tutte e 5 le lingue supportate (`Localization.vb`: IT, EN, FR, ES, DE).
- **Impatto**: Medio | **Sforzo**: Basso

## ~~67. UpdateChecker: UX, localizzazione, confronto versioni, duplicazioni~~ ✅
- **File**: `UpdateChecker.vb` (`CheckForUpdatesAsync`, `PerformUpdateFromGitHubAsync`, `FetchGitHubReleaseInfoAsync`, `ParseRelease`, `IsNewerVersion`, `ComparePrerelease`, `Log`), `Localization.vb`
- **Problema**:
  - Le finestre di dialogo `MessageBox` erano cablate in lingua italiana in un'applicazione localizzata in 5 lingue;
  - Con controllo forzato dall'utente (`force = True`), un eventuale errore di rete, mancata risposta delle API GitHub o rate limit non mostrava alcun feedback visivo (registrato unicamente con `Debug.WriteLine`);
  - `IsNewerVersion` confrontava i suffissi di pre-release come stringhe ordinali (`String.Compare`): la versione "beta10" risultava antecedente a "beta2";
  - La logica di parsing JSON dei dati di release era duplicata quasi per intero nei due rami di `FetchGitHubReleaseInfoAsync`;
  - La cartella temporanea `%TEMP%\HidaChat_Update` non veniva rimossa al termine della procedura di estrazione ed aggiornamento, causando lock I/O nelle sessioni successive (risolto con percorsi univoci di sessione e bonifica in `update.bat`).
- **Fix Implementato**:
  - Aggiunte e sincronizzate 12 nuove chiavi di localizzazione per i messaggi e i titoli dei dialoghi di aggiornamento (`update_already_latest`, `update_already_latest_title`, `update_no_new_version`, `update_no_new_version_title`, `update_available_prompt`, `update_available_title`, `update_install_error`, `update_install_error_title`, `update_check_failed`, `update_check_failed_network`, `update_check_failed_title`, `update_no_asset`) nei dizionari di tutte e 5 le lingue supportate (`Localization.vb`: EN, IT, FR, ES, DE);
  - Implementato feedback visivo informativo tramite finestre di dialogo localizzate quando l'utente forza il controllo aggiornamenti (`force = True`) in caso di endpoint non raggiungibile, errori HTTP/rate limiting o eccezioni di rete;
  - Implementata la funzione `ComparePrerelease` con tokenizzazione numerica SemVer (`\d+|[^\d\.\-_+]+`) per consentire il confronto numerico corretto dei suffissi di pre-release (es. "beta10" > "beta2");
  - Unificata ed isolata l'estrazione dei metadati di release nella funzione helper condivisa e robusta `ParseRelease(JsonElement)`, eliminando la duplicazione del codice tra canale beta e canale stabile;
  - Implementato un sistema di tracciamento e logging portabile thread-safe su file `data/updates.log` (con rotazione automatica al superamento di 1 MB), sostituendo le invocazioni dirette di `Debug.WriteLine`.
- **Impatto**: Medio | **Sforzo**: Basso-Medio

## ~~68. Piattaforme: da stringhe a modello tipizzato~~ ✅
- **File**: `PlatformDefinition.vb`, `AppAccounts.vb`, `AccountManager.vb`, `SettingsWindow.xaml.vb`, `MainWindow.xaml.vb`, `BulkSenderWindow.xaml.vb`, `BulkSenderEngine.vb`, `MessagePopup.xaml.vb`, `Tests/SessionTests.vb`
- **Problema**: La gestione delle piattaforme era basata su stringhe non tipizzate (`"WhatsApp"`, `"Telegram"`, `"OpenClaw"`, `"Hermes"`). Sebbene le voci #53 e #54 fossero già state integrate implementando le rispettive piattaforme, la configurazione degli URL, icone SVG, colori, script iniettati e regole host era articolata attraverso lunghe serie di condizioni `If / ElseIf` e flag booleani sparsi (`IsOpenClaw`, `IsHermes`, `IsTelegram`, `IsWhatsApp`).
- **Fix Implementato**:
  - Creata l'astrazione tipizzata `PlatformDefinition` e l'enumerazione `ChatPlatform` (`WhatsApp`, `Telegram`, `OpenClaw`, `Hermes`);
  - Implementate le classi concrete `WhatsAppPlatformDefinition`, `TelegramPlatformDefinition`, `OpenClawPlatformDefinition` e `HermesPlatformDefinition` che incapsulano URL predefiniti, porte proxy locali, colori brand/tema hex, percorsi SVG icone, controlli di autorizzazione origine (`IsAuthorizedOrigin`), gestione finestre interne (`IsAllowedNewWindowHost`), risoluzione deep-link (`ResolveDeepLink`) e script JS di bootstrap e temi;
  - Creato il registro centrale thread-safe `PlatformRegistry` per la risoluzione e l'estensione dinamica delle piattaforme;
  - Refactoring completo di `AppAccounts.vb` per delegare alla definizione della piattaforma (`PlatformDefinition`) le logiche di rendering visivo, URL, proxy locale e script, mantenendo al contempo la proprietà `Platform As String` compatibile al 100% con `settings.json` e XAML data binding;
  - Refactoring di `AccountManager.vb`, `SettingsWindow.xaml.vb`, `MainWindow.xaml.vb`, `BulkSenderWindow.xaml.vb`, `BulkSenderEngine.vb` e `MessagePopup.xaml.vb` eliminando catene rigide di `If/ElseIf` string-based;
  - Aggiunti test di regressione unitari dedicati in `Tests/SessionTests.vb` per la risoluzione per enum e stringa, fallback, porte proxy, deep-link e autorizzazione origini.
- **Impatto**: Alto | **Sforzo**: Medio

## ~~69. Traduzione: privacy e backend self-hosted~~ ✅
- **File**: `TranslationProvider.vb`, `Localization.vb` (`TranslateSingleDetailedAsync`, `TranslateBatchDetailedAsync`, `TranslationCacheService`), `SettingsController.vb`, `TranslationPrivacyConsentDialog.xaml`, `TranslationPrivacyConsentDialog.xaml.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `AppAccounts.vb`, `Scripts/translation.js`, `Tests/SessionTests.vb`
- **Problema**: Il testo dei messaggi non ancora presente nella cache locale veniva trasmesso direttamente a Google Translate (`translate.googleapis.com`): il contenuto testuale delle chat lasciava il computer dell'utente senza una preventiva richiesta di autorizzazione esplicita, in contrasto con le esigenze di privacy e riservatezza dei dati aziendali o personali.
- **Fix Implementato**:
  - **Astrazione e Architettura Provider Unificata (`ITranslationProvider`, `TranslationProvider.vb`)**:
    - Definita l'interfaccia tipizzata `ITranslationProvider` con metodi asincroni `TranslateSingleAsync`, `TranslateBatchAsync` e `TestConnectionAsync`;
    - Implementato `GoogleTranslationProvider`: client HTTPS pubblico ottimizzato con chunking protetto contro split errati;
    - Implementato `LibreTranslateProvider`: supporto completo ad istanze locali o server privati LibreTranslate con supporto sia a traduzioni singole sia ad array batch nativi e autenticazione opzionale via API key;
    - Implementato `OllamaTranslationProvider`: integrazione diretta con LLM locali (es. `llama3.2`, `qwen2.5`) tramite API `/api/generate`, prompt ottimizzati e sanitizzazione automatica dell'output;
    - Implementato `OpenAiTranslationProvider`: compatibilità con tutti gli endpoint chat completions compatibili OpenAI (LM Studio, LocalAI, vLLM, OpenAI, Groq) con normalizzazione automatica degli URL e header Bearer token opzionale;
    - Creato il registro centrale `TranslationProviderRegistry` per la risoluzione e factory dei provider in base alla configurazione.
  - **Controllo Privacy Opt-In Obbligatorio al Primo Utilizzo (`TranslationPrivacyConsentDialog.xaml`)**:
    - La funzione di traduzione è ora rigorosamente opt-in (`TranslationPrivacyConsent = False` per impostazione predefinita);
    - Al primo tentativo di traduzione (singola o batch), viene presentata una finestra modale in tema scuro che informa con trasparenza l'utente sulla trasmissione del testo al provider configurato, spiegando la differenza tra server cloud esterni e provider self-hosted locali/on-premise;
    - In assenza di consenso esplicito, nessun dato lascia il computer e l'interfaccia restituisce un avviso discreto;
    - Lo stato del consenso può essere accordato o revocato in qualsiasi momento dalle Impostazioni.
  - **Gestione Errori Chiara e Discreta per Provider Irraggiungibile**:
    - In caso di errore di connessione, timeout o server offline (es. container LibreTranslate o Ollama non avviato), il sistema non va in crash e non inquina la cache con traduzioni errate;
    - Nella bolla di traduzione (`translation.js`) viene visualizzato in modo discreto il motivo del mancato recapito (es. *"Translation failed: Provider unreachable"* o *"Translation cancelled: privacy consent required"* in corsivo colorato tenue).
  - **Pannello di Configurazione e Test Connessione in Impostazioni (`SettingsWindow.xaml`)**:
    - Aggiunto il selettore del provider, i campi dinamici di configurazione (URL endpoint, modello, chiave API) e il pulsante interattivo "Testa Connessione" con visualizzazione in tempo reale di stato e latenza di rete in millisecondi.
  - **Localizzazione Multilingua Integrale (`Localization.vb`)**:
    - Sincronizzate 15 nuove chiavi di localizzazione in tutti e 5 i dizionari ufficiali (Inglese, Italiano, Francese, Spagnolo, Tedesco).
  - **Suite di Test Unitari Automatizzata (`Tests/SessionTests.vb`)**:
    - Aggiunti 33 nuovi test di regressione (per un totale di 156 asserzioni automatiche con 0 errori) per validare risoluzione provider, flag self-hosted, isolamento Opt-in e gestione resiliente dei guasti di rete.
- **Impatto**: Medio | **Sforzo**: Medio

## ~~70. Uso portabile: lock multi-PC e coerenza del README~~ ✅
- **File**: `Application.xaml.vb` (`TryAcquireFolderLock`, `ReleaseFolderLock`, `OnStartup`, `OnExit`), `README.md`, `README.it.md`, `Localization.vb`
- **Problema**: Il file README raccomandava di non eseguire la stessa istanza portabile contemporaneamente da postazioni differenti (rischio concreto di lock file e corruzione del profilo WebView2), ma l'applicazione non adottava alcun meccanismo di prevenzione a livello di filesystem. Inoltre, l'indicazione "Zero Installation" contrastava con la necessità del runtime .NET 9 per le pubblicazioni standard framework-dependent.
- **Fix Implementato**:
  - Implementato a livello di avvio applicazione (`Application.OnStartup`) un file lock esclusivo sul filesystem `data/.lock` mantenuto aperto per l'intera durata della sessione;
  - Registrazione strutturata in formato JSON nel lock file dei metadati del processo (hostname, username, PID, data/ora ISO di avvio);
  - In caso di rilevamento di una contesa o sessione già attiva sulla medesima cartella (da altra macchina su rete o altro processo locale), visualizzazione di un messaggio bloccante all'utente (localizzato in tutte le lingue con dettaglio di macchina, PID e orario) ed arresto controllato dell'applicazione per impedire la corruzione dei profili WebView2;
  - Rilascio pulito e rimozione automatica di `data/.lock` alla chiusura dell'applicazione in `OnExit` e nei percorsi di shutdown;
  - Armonizzazione della documentazione in `README.md` e `README.it.md`: chiarito il meccanismo di protezione attiva del lock multi-PC `data/.lock`, ed esplicitato il requisito del .NET 9 Desktop Runtime chiarendo che l'indicazione "Zero Installazione" si riferisce alla distribuzione portabile (nessun installer di sistema necessario, nessun dato in registry o percorsi esterni).
- **Impatto**: Medio | **Sforzo**: Basso-Medio

---

# GESTIONE CACHE & MEMORIA CHROMIUM (WEBVIEW2)

## ~~71. Limitazione della cache Chromium via AdditionalBrowserArguments (`--disk-cache-size`)~~ ✅
- **File**: `AppAccounts.vb` (`SetupWebViewInternalAsync`), `SettingsController.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`
- **Descrizione**: Limitata la cache su disco di Chromium via `AdditionalBrowserArguments` in fase di creazione dell'environment WebView2 con lo switch standard `--disk-cache-size=<bytes>`.
- **Implementazione**:
  - Configurazione dinamica in `SettingsController.vb` con proprietà `DiskCacheLimitMb` (default: 256 MB, opzioni: 128 MB, 256 MB, 512 MB, 1024 MB, 0 = Illimitata) e salvataggio in `settings.json`.
  - Iniezione del parametro `--disk-cache-size={CLng(DiskCacheLimitMb) * 1024L * 1024L}` nella creazione di `CoreWebView2Environment` in `AppAccounts.vb`.
  - Menu a tendina dedicato `ComboDiskCacheLimit` nelle Impostazioni con localizzazione multilingua completa (EN, IT, FR, ES, DE).
- **Impatto**: Medio | **Sforzo**: Basso

## ~~72. Pulizia cache "preservando il login" (Smart Cache Cleanup)~~ ✅
- **File**: `AccountManager.vb`, `AppAccounts.vb`, `MainWindow.xaml`, `MainWindow.xaml.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`
- **Descrizione**: Implementata la pulizia cache mirata e non distruttiva che cancella unicamente cache volatile, file temporanei, shader e GPU cache (`Cache`, `Code Cache`, `GPUCache`, `DawnWebGPUCache`, `DawnGraphiteCache`, `Crashpad`), lasciando intatti `Local Storage`, `IndexedDB`, `Network\Cookies`, credenziali e sessioni di login.
- **Implementazione**:
  - Metodo `SmartCleanCachePreservingLoginAsync()` in `AppAccounts.vb` che combina l'invocazione nativa `Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.DiskCache Or DownloadHistory)` a runtime con l'eliminazione dei file su disco non bloccati sia per il profilo principale sia per lo staging locale su drive di rete.
  - Metodo aggregato `SmartCleanCachePreservingLoginAsync()` in `AccountManager.vb` che itera e bonifica tutti i profili contemporaneamente.
  - Pulsante dedicato "Pulisci Cache (preserva login)" nella sezione "Cache & Archiviazione" di `SettingsWindow.xaml` con finestra di conferma e notifica di successo.
  - Voce di menu contestuale con click destro sulla scheda dell'account in `MainWindow.xaml` ("Pulisci cache account") per pulizia puntuale per singola sessione.
  - Localizzazione sincrona in tutte le 5 lingue ufficiali (EN, IT, FR, ES, DE).
- **Impatto**: Medio | **Sforzo**: Basso-Medio

## ~~73. Supporto Drive di Rete tramite Local Staging & Sync delle Sessioni (Network Drive Session Persistence)~~ ✅
- **File**: `NetworkProfileSync.vb`, `AppAccounts.vb`, `AccountManager.vb`, `MainWindow.xaml.vb`, `SettingsController.vb`, `SettingsWindow.xaml`
- **Problema**: Eseguendo l'applicazione portabile direttamente da una cartella o unità di rete (condivisione SMB/CIFS, percorsi UNC `\\server\share` o drive mappati `Z:\`), la sessione di WhatsApp Web viene frequentemente persa o resettata alla schermata del QR code al riavvio dell'applicazione. La causa è architetturale di Chromium/WebView2: il motore LevelDB e SQLite (impiegati da WhatsApp Web tramite IndexedDB per memorizzare chiavi crittografiche, session token e messaggi) richiedono semantiche di lock POSIX/NTFS locali. Su filesystem SMB di rete si verificano ritardi o conflitti di lock (`STATUS_FILE_LOCK_CONFLICT`) che portano Chromium a marcare i database come corrotti o inaccessibili al riavvio, innescandone la cancellazione/reset automatico.
- **Implementazione**:
  1. *Rilevamento drive di rete*: `NetworkProfileSync.IsRunningOnNetwork` rileva percorsi UNC e unità di rete mappate SMB (`DriveType.Network`).
  2. *Local Staging trasparente*: `NetworkProfileSync.SyncMasterToLocalStagingAsync` sincronizza all'avvio solo i dati essenziali (`IndexedDB`, `Local Storage`, `Network/Cookies`, preferenze) nella directory locale `%LOCALAPPDATA%\HidaChat\NetworkProfiles\{AccountId}`, escludendo le cartelle di cache volatile (`Cache`, `Code Cache`, `GPUCache`, `Crashpad`) per trasferimenti ultra-rapidi.
  3. *Esecuzione WebView2 in locale*: `CoreWebView2Environment` viene inizializzato sulla cartella locale garantendo velocità nativa e lock locali senza conflitti SMB.
  4. *Sincronizzazione di ritorno atomica*: Alla chiusura in `PrepareForShutdown` o prima degli aggiornamenti in `ForceExitForUpdateAsync`, `SyncAllProfilesToNetworkAsync` esegue il mirror incrementale dei profili aggiornati verso `data/webview/{AccountId}` sulla share di rete.
  5. *Lock multi-PC*: `AcquireSessionLock` rileva se l'account è già aperto su un altro computer avvisando l'utente.
  6. *Opzione nelle Impostazioni*: Checkbox dedicata e stringhe sincronizzate in tutte le 5 lingue supportate (EN, IT, FR, ES, DE).
- **Impatto**: Alto | **Sforzo**: Medio-Alto

## ~~74. Finestra / Dialog di avanzamento durante il salvataggio e chiusura dell'applicazione~~ [COMPLETATO] ✅
- **File**: `ShutdownProgressDialog.xaml`, `ShutdownProgressDialog.xaml.vb`, `MainWindow.xaml.vb`, `Localization.vb`
- **Problema / Esigenza**: Quando l'utente chiude l'applicazione, vengono eseguite operazioni asincrone critiche in background (sincronizzazione dei profili verso percorsi di rete SMB/UNC, flush e salvataggio delle impostazioni `settings.json`, rilascio lock `data/.lock` e lock di sessione, dismissione controllata dei processi WebView2 e spegnimento del nodo/proxy tsnet). Senza un feedback visivo immediato, l'utente potrebbe percepire l'applicazione come bloccata o tentare di forzarne la chiusura dal Task Manager, causando potenziale troncamento del salvataggio o file lock pendenti.
- **Implementazione completata**:
  - Creata la finestra grafica dedicata `ShutdownProgressDialog` (`WindowStyle="None"`, trasparente con bordo arrotondato e design scuro WhatsApp, `Topmost="True"`, ProgressBar indeterminata e indicatore di fase).
  - Gestite e notificate in tempo reale le fasi di spegnimento durante `ShutdownCoreAsync`:
    1. Salvataggio impostazioni e preferenze account;
    2. Chiusura controllata delle istanze browser WebView2;
    3. Sincronizzazione profili e chat su percorsi di rete (SMB/UNC) se abilitata;
    4. Arresto controllato del proxy Tailscale `tsnetd` e rilascio lock di sessione/directory;
    5. Finalizzazione e chiusura ordinata dell'applicazione.
  - Sincronizzate le stringhe e le descrizioni degli stati in tutte le 5 lingue supportate (EN, IT, FR, ES, DE).
- **Impatto**: Medio-Alto | **Sforzo**: Medio

## ~~75. Rilevamento pagina bianca su profili Hermes / OpenClaw e controllo stato connessione Tailscale~~ [COMPLETATO] ✅
- **File**: `AppAccounts.vb`, `MainWindow.xaml.vb`, `TsnetManager.vb`, `TailscaleConnectionErrorDialog.xaml`, `TailscaleConnectionErrorDialog.xaml.vb`, `Localization.vb`
- **Problema / Esigenza**: I profili di tipo Hermes e OpenClaw comunicano frequentemente con istanze e web server ospitati su nodi Tailscale (sia tramite il nodo tsnet integrato nell'applicazione, sia tramite Tailscale client installato nel sistema operativo o reverse proxy locale). Se la rete Tailscale non è connessa, il nodo remoto è spento o tsnet non ha completato l'avvio/autenticazione, la WebView2 mostra una schermata bianca (o errore di mancata risoluzione/connessione rifiutata), disorientando l'utente.
- **Implementazione completata**:
  - Monitoraggio attivo della navigazione dei profili Hermes e OpenClaw in `NavigationCompleted` con rilevamento fallimenti di rete (`IsSuccess = False`, errori DNS/connessione) e controllo automatico post-caricamento del DOM tramite script di introspezione (rilevamento pagina bianca persistente `empty_body` o errori gateway `502 Bad Gateway` / `504 Gateway Timeout`).
  - Implementato in `TsnetManager.vb` il metodo di diagnostica approfondita `CheckTailscaleConnectivityAsync`:
    - Verifica dello stato del modulo tsnet embedded (`NeedsLogin`, `Stopped`, o handshake HTTP/proxy con `X-HidaChat-Local-Token` verso `127.0.0.1:{port}`);
    - Verifica raggiungibilità TCP dell'endpoint remoto Tailscale e rilevamento della presenza del client nativo Tailscale su Windows.
  - Creata la finestra di dialogo modale dedicata `TailscaleConnectionErrorDialog` con stile scuro WhatsApp:
    - Messaggio esplicativo chiaro e diagnosi dettagliata (tsnet richiede login, tsnet fermo, host remoto non raggiungibile, o client Tailscale disconnesso nel sistema);
    - Pulsanti di azione interattivi: "Riprova" (ricarica automatica della scheda con refresh della rotta), "Accedi a Tailscale" (apre direttamente l'URL di autenticazione del nodo se richiesto), "Impostazioni" (apertura delle preferenze di HidaChat), "Chiudi".
  - Sincronizzate tutte le stringhe, titoli e pulsanti in tutte le 5 lingue supportate (EN, IT, FR, ES, DE).
- **Impatto**: Alto | **Sforzo**: Medio

## ~~76. Controllo integrità storage/IndexedDB all'avvio con ProgressBar di ripristino dati corrotti~~ [COMPLETATO] ✅
- **File**: `StorageIntegrityService.vb`, `StorageRecoveryProgressDialog.xaml`, `StorageRecoveryProgressDialog.xaml.vb`, `AppAccounts.vb`, `MainWindow.xaml.vb`, `Localization.vb`
- **Problema / Esigenza**: 
  - In seguito a chiusure improvvise, crash hardware, blocchi di sistema o spegnimenti forzati del PC durante la scrittura dei dati, i database IndexedDB di Chromium (come `tt-data` di Telegram Web A o i database Signal/LevelDB di WhatsApp Web) possono incorrere nella perdita di file blob su disco pur mantenendo i riferimenti attivi nei file di log/manifest di LevelDB.
  - Al successivo avvio, Chromium lancia l'eccezione irreversibile `NotReadableError: Data lost due to missing file. Affected record should be considered irrecoverable`. Le web app (in particolare Telegram Web) non gestiscono questa Promise rejection durante il bootstrap iniziale e interrompono l'esecuzione JavaScript prima del montaggio del DOM, lasciando all'utente una schermata completamente bianca ("pagina bianca").
- **Implementazione completata**:
  1. *Controllo Preventivo all'Avvio / Caricamento Profilo*:
     - Creato il servizio `StorageIntegrityService.vb` con metodo `CheckIndexedDbCorruption(profileDir)` che analizza strutturalmente le cartelle `EBWebView\Default\IndexedDB\*.indexeddb.leveldb` prima dell'avvio di WebView2 (verifica esistenza ed integrità di `CURRENT`, validità e non-vacuità del file `MANIFEST-*` referenziato, e integrità delle directory blob associate).
  2. *Feedback Visivo con Finestra / ProgressBar di Recupero*:
     - Creata la finestra grafica dedicata `StorageRecoveryProgressDialog` (`WindowStyle="None"`, trasparente con bordo arrotondato e stile scuro nativo, `Topmost="True"`, ProgressBar indeterminata e indicatore di fase dinamico).
  3. *Esecuzione del Ripristino Mirato*:
     - Procedura automatizzata `PerformStorageRecoveryWithDialogAsync`: rimozione pulita delle cartelle IndexedDB/ServiceWorker corrotte salvaguardando integralmente `Local Storage`, file di preferenze, token DPAPI, cookie di sessione e credenziali d'accesso.
     - Integrazione completa sia all'avvio preventivo a freddo, sia a caldo tramite evento diagnostico `INDEXEDDB_CORRUPTED` in `HandleDiagnosticMessageAsync`, sia dallo strumento "Ripara Scheda" del menu contestuale.
  4. *Conclusione e Localizzazione*:
     - Chiusura automatica della ProgressBar e ricaricamento della pagina ad operazione conclusa;
     - Localizzazione completa dei messaggi e degli stati in tutte le 5 lingue supportate (EN, IT, FR, ES, DE).
- **Impatto**: Alto | **Sforzo**: Medio


## ~~77. [P1] Il proxy permette di saltare l’autenticazione locale~~ [COMPLETATO] ✅
- **File**: `tsnetd/main.go`, `tsnetd/main_test.go`
- **Problema / Esigenza**: La condizione che riconosce una richiesta WebSocket la considerava automaticamente autorizzata (`isWs`). Bastava quindi la presenza di `Sec-WebSocket-Key`, anche senza un handshake valido o credenziali, per evitare il controllo del token ed ottenere nel Set-Cookie il token locale segreto.
- **Implementazione completata**:
  - Rimosso qualsiasi bypass basato sulla presenza di header WebSocket (`Sec-WebSocket-Key` o `Upgrade: websocket`): sia le richieste HTTP che gli handshake WebSocket devono autenticarsi rigorosamente tramite header `X-HidaChat-Local-Token`, query parameter `__htok` o cookie `__htok`.
  - Verifica preventiva dell'Origin originale (`isAllowedOrigin`) prima della riscrittura o inoltro al backend: l'Origin deve corrispondere all'endpoint locale (`127.0.0.1:{port}`, `localhost:{port}`, `[::1]:{port}`) o all'endpoint target Tailscale atteso; qualsiasi Origin esterno malevolo o 'null' viene respinto con `403 Forbidden: Invalid Origin`, prevenendo Cross-Site WebSocket Hijacking (CSWSH) e attacchi cross-origin.
  - Rafforzato il cookie locale impostando `HttpOnly: true` e `SameSite: Lax` per prevenire l'esfiltrazione del token da script della pagina.
  - Creati test unitari completi in `tsnetd/main_test.go` per verificare che richieste HTTP e WebSocket senza credenziali falliscano (anche con header contraffatti), che Origin non autorizzati vengano bloccati e che handshake autenticati continuino a funzionare.
- **Priorità**: P1 | **Impatto**: Alto | **Sforzo**: Medio

## ~~78. [P1] I token vengono aggiunti alle richieste verso qualsiasi porta localhost~~ [COMPLETATO] ✅
- **File**: `LocalAuthPolicy.vb`, `AppAccounts.vb`, `SettingsWindow.xaml.vb`, `Tests/SessionTests.vb`, `Tests/HidaChat.SessionTests.vbproj`
- **Problema / Esigenza**: Il filtro utilizzato era `http://127.0.0.1:*/*`. Alle richieste intercettate venivano aggiunti il token locale e, quando disponibile, il Bearer token dell’account senza verificare che la porta fosse effettivamente quella assegnata al proxy di quell’account. Una richiesta verso un altro servizio locale poteva quindi ricevere credenziali che non gli appartenevano.
- **Implementazione completata**:
  - Creato il modulo `LocalAuthPolicy.vb` con il metodo di convalida rigorosa `ShouldAttachCredentials(requestUriStr, expectedScheme, expectedHost, expectedPort)` che verifica l'esatta corrispondenza di schema, host e porta prima dell'iniezione dei token, e `BuildFilterPattern` che vincola il pattern di filtro WebView2 alla porta TCP assegnata (`http://127.0.0.1:{port}/*`).
  - Refactoring completo di `EnsureLocalProxyAsync`, `UpdateWebResourceAuthFilter` e `RemoveWebResourceAuthFilter` in `AppAccounts.vb`: rimozione controllata dei vecchi filtri (`CoreWebView2.RemoveWebResourceRequestedFilter`) e distacco handler al variare di porta, target `ServerUrl`, credenziali o stato Tailscale (`TailscaleIntegration`).
  - Validazione rigorosa sia per il proxy locale tsnet (`127.0.0.1:{LocalProxyPort}` per `X-HidaChat-Local-Token` e `Bearer`) sia per connessioni gateway dirette (porta e authority esatte di `ServerUrl`).
  - Creata suite di test unitari automatizzati in `Tests/SessionTests.vb` che valida l'accettazione su porta corretta e il respingimento su porte differenti, host non coincidenti, schemi errati e tentativi di spoofing o subdomain attack.
- **Priorità**: P1 | **Impatto**: Alto | **Sforzo**: Basso-Medio

## 79. [P1] L’invio massivo può spedire una bozza e dichiarare un falso successo
- **File**: `BulkSenderEngine.vb`, circa righe 175–250 e 265–341
- **Problema / Esigenza**: Su Telegram il messaggio previsto viene inserito soltanto se il campo è vuoto: una bozza preesistente può essere inviata al suo posto. Inoltre, su entrambe le piattaforme, il risultato diventa SENT dopo un click o un evento tastiera, senza verificare che il client abbia realmente accettato il messaggio. Manca anche una verifica esplicita dell’identità della chat prima dell’invio. Le prove con DOM simulato confermano sia l’invio della bozza sia il ritorno SENT senza alcuna conferma effettiva.
- **Fix da implementare**: Verificare destinatario e testo prima dell’azione; in presenza di una bozza, fermarsi senza sovrascriverla silenziosamente. Attendere poi una conferma osservabile del client, distinguendola dalla consegna al destinatario.
- **Test di accettazione**: Bozza esistente, navigazione lenta, chat precedente ancora visibile e pressione di Invio ignorata non devono produrre un falso “Inviato”.
- **Priorità**: P1 (da sistemare prima della prossima release) | **Impatto**: Alto | **Sforzo**: Medio

## 80. [P1] Stop e riavvio possono sovrapporre due esecuzioni dell’invio massivo
- **File**: `BulkSenderWindow.xaml.vb`, circa righe 343–438; `BulkSenderEngine.vb`, metodi `Cancel()` e `RunAsync()`
- **Problema / Esigenza**: Il pulsante Stop richiede l’annullamento e riabilita immediatamente Avvio, senza attendere la conclusione dell’esecuzione precedente. Quest’ultima può ancora trovarsi dentro una chiamata asincrona. Inoltre, anche un’esecuzione interrotta passa dal riepilogo “Completato”, che porta la barra al 100%.
- **Fix da implementare**: Introdurre uno stato “Arresto in corso” e un `CancelAsync()` che attenda il task attivo. Il motore deve impedire esecuzioni concorrenti indipendentemente dalla UI. Separare gli esiti Completed, Cancelled e Failed.
- **Test di accettazione**: Premere Stop durante una chiamata JavaScript e tentare subito un nuovo avvio. Non devono partire due elaborazioni sulla stessa WebView; il riepilogo deve conservare i conteggi reali e indicare l’interruzione.
- **Priorità**: P1 (da sistemare prima della prossima release) | **Impatto**: Alto | **Sforzo**: Basso-Medio

## 81. [P1] Un blocco di tsnetd può impedire indefinitamente la chiusura
- **File**: `TsnetManager.vb`, circa righe 247–263 e 386–420
- **Problema / Esigenza**: `SendCommandAsync()` attende il semaforo e una riga da stdout senza timeout. `ShutdownAsync()` chiama questo metodo prima di arrivare a `WaitForExit(2000)`. Di conseguenza, se il daemon resta vivo ma non risponde, il limite di due secondi non viene mai raggiunto. Anche un comando precedente bloccato può trattenere il semaforo e impedire l’invio di shutdown.
- **Fix da implementare**: Applicare un timeout cancellabile all’intera operazione, compresa l’acquisizione del semaforo. Lo shutdown deve avere una scadenza complessiva indipendente dal canale comandi. Alla scadenza, chiudere il canale e gestire il processo senza lasciare letture pendenti.
- **Test di accettazione**: Usare un daemon di prova che non risponde più su stdout. Chiusura e aggiornamento devono terminare o fallire con un errore chiaro entro il limite previsto, non restare appesi.
- **Priorità**: P1 (da sistemare prima della prossima release) | **Impatto**: Alto | **Sforzo**: Medio

## 82. [P2] La verifica dei certificati HTTPS viene disattivata
- **File**: `tsnetd/main.go`, circa righe 259–269; `SettingsWindow.xaml.vb`, circa righe 320–322
- **Problema / Esigenza**: Nel transport del proxy viene impostato `InsecureSkipVerify = true`; il test di connessione usa inoltre una callback che accetta sempre il certificato. La verifica HTTPS del servizio viene quindi aggirata nei percorsi interessati. In Go questa impostazione disabilita la normale verifica della catena e del nome del server. La protezione del tunnel Tailscale è distinta dalla verifica dell’identità HTTPS del servizio.
- **Fix da implementare**: Validazione standard come comportamento predefinito. Per server con certificati privati, consentire una CA esplicita o il pinning; evitare un bypass globale e silenzioso.
- **Test di accettazione**: Un certificato scaduto, non attendibile o con hostname errato deve produrre un errore TLS distinguibile da un semplice problema di raggiungibilità.
- **Priorità**: P2 (intervento successivo) | **Impatto**: Medio | **Sforzo**: Medio

## 83. [P2] “Test connessione” mostra Online anche quando il servizio non funziona
- **File**: `SettingsWindow.xaml.vb`, circa righe 295–348, metodo `BtnTestConnection_Click()`
- **Problema / Esigenza**: Dopo `GetAsync()` il codice assegna `isOnline = True` senza controllare lo status HTTP. Anche 401, 403, 500 e 502 possono quindi produrre l’indicazione verde. Per OpenClaw, inoltre, questo test non aggiunge il Bearer token: l’header viene impostato soltanto nel ramo Hermes.
- **Fix da implementare**: Distinguere almeno raggiungibilità, autenticazione e disponibilità del servizio. Usare un endpoint appropriato per la piattaforma e non considerare riuscito il test solo perché è arrivata una risposta HTTP.
- **Test di accettazione**: Verificare risposte 200, 401, 403, 500, 502 e timeout. Credenziali errate e backend irraggiungibile devono dare indicazioni diverse, entrambe diverse da “connessione riuscita”.
- **Priorità**: P2 (intervento successivo) | **Impatto**: Medio | **Sforzo**: Basso-Medio

## 84. [P2] Il parser CSV rompe i messaggi multilinea e può scegliere il separatore sbagliato
- **File**: `ExcelContactService.vb`, circa righe 122–131 e 200–238
- **Problema / Esigenza**: Il file viene letto per righe fisiche e ciascuna riga viene analizzata separatamente. Un campo tra virgolette contenente un ritorno a capo viene quindi spezzato. Il rilevamento del delimitatore, inoltre, conta anche virgole e punti e virgola dentro il testo quotato. Nelle prove isolate, un contatto con un messaggio su due righe produce una riga aggiuntiva; un CSV separato da virgole viene interpretato come separato da `;` quando il messaggio ne contiene abbastanza.
- **Fix da implementare**: Usare un parser che legga record completi, mantenendo lo stato delle virgolette tra le righe. Rilevare il separatore fuori dai campi quotati e consentire una scelta esplicita quando il risultato è ambiguo (conforme RFC 4180).
- **Test di accettazione**: Messaggi multilinea, virgolette escape e punteggiatura devono lasciare invariati numero di contatti e contenuto del messaggio.
- **Priorità**: P2 (intervento successivo) | **Impatto**: Medio | **Sforzo**: Basso-Medio

## 85. [P2] La traduzione batch può associare il testo tradotto al messaggio sbagliato
- **File**: `Localization.vb`, circa righe 364–430, metodo `TranslateBatchAsync()`
- **Problema / Esigenza**: I messaggi vengono uniti con un separatore testuale `###` e successivamente divisi usando lo stesso separatore. Non viene verificato che il numero delle parti restituito corrisponda al numero dei messaggi. Le associazioni ottenute vengono poi inserite nella cache (es. messaggio contenente `###` che sfasa gli indici).
- **Fix da implementare**: Preferire risposte strutturate con un identificatore per elemento. Finché si usa un separatore testuale, controllare collisioni e numero delle parti; in caso di incongruenza ripiegare sulle traduzioni individuali, senza memorizzare risultati ambigui.
- **Test di accettazione**: Un messaggio contenente `###`, un separatore alterato e un errore HTTP non devono contaminare la cache.
- **Priorità**: P2 (intervento successivo) | **Impatto**: Medio | **Sforzo**: Basso-Medio

## 86. [P2] La pulizia dell’updater può cancellare una sessione di aggiornamento ancora attiva
- **File**: `UpdateChecker.vb`, circa righe 516–545 e successive
- **Problema / Esigenza**: La pulizia viene avviata con `Task.Run()` e non attesa. Cerca tutte le cartelle e gli archivi con i prefissi degli aggiornamenti, senza distinguere quelli vecchi da quelli attivi. Se la pulizia parte in ritardo, può interferire con i file appena creati. Due copie portabili in cartelle diverse, ma con lo stesso `%TEMP%`, possono inoltre cancellarsi reciprocamente i file temporanei.
- **Fix da implementare**: Identificare prima le sessioni effettivamente obsolete, escludere sempre quella corrente e proteggere quelle attive con un lock. Evitare aggiornamenti concorrenti della stessa installazione.
- **Test di accettazione**: Ritardare artificialmente la pulizia e avviare due aggiornamenti da copie distinte. Nessuna sessione deve eliminare file ancora usati dall’altra.
- **Priorità**: P2 (intervento successivo) | **Impatto**: Medio | **Sforzo**: Medio

## 87. Finestra grafica dedicata per l'aggiornamento con stato di avanzamento (eliminazione script .ps1 / .bat)
- **File**: `UpdateChecker.vb`, `UpdateProgressWindow.xaml` / `UpdateProgressWindow.xaml.vb` (o eseguibile helper compatto standalone `HidaChat.Updater.exe`), `Localization.vb`
- **Problema / Esigenza**:
  - Attualmente il flusso di aggiornamento OTA si appoggia a script temporanei generati al volo (`.bat` o precedentemente ipotizzati `.ps1`) avviati tramite interprete/processo di shell Windows.
  - L'uso di script esterni presenta notevoli criticità e limiti:
    1. Assenza di feedback visivo grafico moderno per l'utente, che rischia di vedere finestre nere del prompt dei comandi / console oppure nessun indicatore chiaro mentre l'applicazione principale si chiude per consentire la sostituzione dei file;
    2. Vulnerabilità ai blocchi euristici degli antivirus o alle policy di esecuzione restrittive del sistema operativo (es. restrizioni su script PowerShell `.ps1` o comandi batch non firmati);
    3. Rischio che l'utente ritenga l'applicazione terminata o bloccata e tenti di riavviarla manualmente prima che la copia dei file e la sovrascrittura siano completate.
- **Fix da implementare**:
  1. *Eliminazione definitiva di script esterni (`.ps1` e `.bat`)*:
     - Rimuovere completamente la generazione e l'esecuzione di script `.ps1` o `.bat`.
     - Gestire l'intero ciclo di aggiornamento tramite codice compilato nativo (.NET / WPF) o un piccolo processo helper compilato dedicato (es. `HidaChat.Updater.exe` o finestra modale interna con handoff a processo secondario) completamente autonomo e privo di dipendenze da interpreti shell esterni.
  2. *Finestra grafica con indicatore di avanzamento (Progress Dialog)*:
     - Creare una finestra grafica dedicata con interfaccia scura moderna e stile coerente con HidaChat (in linea con `ShutdownProgressDialog` e `StorageRecoveryProgressDialog`).
     - Barra di avanzamento (ProgressBar determinata con percentuale di completamento e MB scaricati durante il download; ProgressBar fluida/indeterminata durante verifica hash ed estrazione/copia file).
     - Visualizzazione chiara e in tempo reale delle fasi dell'aggiornamento:
       - *Scaricamento del pacchetto di aggiornamento da GitHub*;
       - *Verifica integrità crittografica SHA-256 e firma Authenticode*;
       - *Chiusura ordinata e attesa rilascio risorse dei processi HidaChat e WebView2*;
       - *Estrazione e sovrascrittura sicura dei file dell'applicazione* (preservando rigorosamente la cartella dati `data/` al 100% portabile);
       - *Riavvio automatico di HidaChat*.
  3. *Gestione degli errori e resilienza*:
     - In caso di errore durante una qualsiasi fase (timeout download, discrepanza checksum SHA-256, lock I/O su file in uso), mostrare un messaggio chiaro e localizzato all'interno della finestra grafica con pulsante "Annulla" o "Riprova", senza mai corrompere l'installazione o chiudersi silenziosamente.
  4. *Localizzazione multilingua integrale*:
     - Aggiungere e sincronizzare tutti i testi, messaggi di avanzamento e titoli delle fasi nei 5 dizionari ufficiali (`Localization.vb`: Inglese, Italiano, Francese, Spagnolo, Tedesco).
- **Test di accettazione**:
  - Eseguire la procedura di aggiornamento e verificare che appaia la finestra grafica con barra di progresso e cambio di stato visibile;
  - Verificare che non venga eseguito né creato alcun file script `.ps1` o `.bat` nella cartella `%TEMP%` o altrove;
  - Verificare il corretto handoff: chiusura dell'app principale, copia dei file, riavvio automatico e preservazione integrale di tutti i dati in `data/`.
- **Priorità**: P2 | **Impatto**: Alto | **Sforzo**: Medio

---

# FIX & BUG ROADMAP (Sessioni, Aggiornamenti OTA & Resilienza)

## 88. [P1] Timeout di 15 s sul download dello ZIP e buffering in RAM dell'archivio (UpdateChecker.vb)
- **File**: `UpdateChecker.vb` (metodo `PerformUpdateFromGitHubAsync`)
- **Problema / Esigenza**: `_httpClient.Timeout = 15s` vale per tutte le richieste eseguite con il client HTTP predefinito, compreso `GetByteArrayAsync(downloadUrl)` (o scaricamento dell'asset zip). Con un archivio di aggiornamento da decine o centinaia di MB su connessioni lente o instabili, l'operazione fallisce sistematicamente con l'eccezione `"A task was canceled"` (timeout). Inoltre, scaricare l'intero payload con `GetByteArrayAsync` carica decine di MB direttamente nella memoria RAM prima della scrittura.
- **Fix da implementare**:
  - Utilizzare un client dedicato (`_downloadClient`) configurato con `Timeout = Timeout.InfiniteTimeSpan` e proteggere la richiesta con un `CancellationTokenSource` temporizzato a qualche minuto (es. 5–10 minuti).
  - Effettuare il download in streaming progressivo direttamente su file temporaneo su disco utilizzando `HttpCompletionOption.ResponseHeadersRead` con buffer di copia (`src.CopyToAsync(dst)`).
  - Calcolare l'hash di verifica direttamente dallo stream del file su disco tramite `SHA256.HashDataAsync(File.OpenRead(tempZipPath))`.
- **Test di accettazione**: Simulare un download con larghezza di banda limitata (tempo di trasferimento > 15s); il download deve completarsi con successo senza eccezioni di timeout e con footprint RAM minimo e costante.
- **Priorità**: P1 (Gravità alta) | **Impatto**: Alto | **Sforzo**: Basso-Medio

## 89. [P1] Possibile perdita di tutte le sessioni su errore di deserializzazione (AccountManager.LoadAccountsAsync)
- **File**: `AccountManager.vb` (metodo `LoadAccountsAsync`)
- **Problema / Esigenza**: Se la deserializzazione dell'elenco `accounts` fallisce (ad esempio a causa di un campo con tipo errato, JSON corrotto, modifiche manuali dell'utente o formati tra versioni differenti), il blocco `Catch` ricade nell'invocazione di `CreateDefaultAccountAsync()`. Quest'ultimo chiama `SaveAccountsAsync(force:=True)`, sovrascrivendo l'intero file `settings.json` con un unico account predefinito. Al successivo riavvio o ciclo di manutenzione, `CleanupUnusedProfilesAsync` elimina tutte le cartelle `WV2Profile_*` non più censite nella configurazione, provocando la distruzione irreversibile di tutte le sessioni di login di WhatsApp, Telegram, OpenClaw ed Hermes.
- **Fix da implementare**:
  - Nel blocco `Catch` di deserializzazione di `LoadAccountsAsync`, creare immediatamente una copia di salvataggio `settings.json.bak` prima di qualsiasi altra operazione.
  - Impostare un flag di sicurezza `_loadFailed = True` a livello di gestore.
  - Inibire sia il salvataggio forzato (`SaveAccountsAsync`) sia la pulizia dei profili (`CleanupUnusedProfilesAsync`) quando `_loadFailed` è attivo.
  - Mostrare all'utente una finestra di dialogo di avviso/errore per segnalare la corruzione del file di configurazione, invitando al ripristino o alla correzione manuale anziché azzerare silenziosamente gli account.
- **Test di accettazione**: Introdurre un errore di sintassi o tipo incompatibile nel blocco `accounts` di `settings.json`. All'avvio il file originale deve essere salvaguardato in `settings.json.bak`, nessun profilo su disco deve essere cancellato o sovrascritto, e deve apparire un avviso esplicito.
- **Priorità**: P1 (Gravità alta) | **Impatto**: Critico | **Sforzo**: Medio

## 90. [P1] MigrateOrphanProfile cancella o sovrascrive un profilo valido
- **File**: `AccountManager.vb` (metodo `MigrateOrphanProfile`)
- **Problema / Esigenza**: Se sul filesystem coesistono sia la cartella `WV2Profile_` (profilo orfano o legacy senza ID specificato) sia la cartella `WV2Profile_{id}` associata al primo account, una logica di migrazione errata o distruttiva rischia di eliminare il profilo valido del primo account per spostarvi sopra la cartella orfana, causando la perdita della sessione reale e attiva dell'utente.
- **Fix da implementare**:
  - All'interno di `MigrateOrphanProfile`, verificare preliminarmente l'esistenza della cartella di destinazione `WV2Profile_{id}` (e di eventuali snapshot `.previous`).
  - Se la cartella di destinazione esiste già, interrompere immediatamente la migrazione (`Continue For` / `Return`), lasciando intatto il profilo valido esistente.
  - Lasciare che la cartella orfana venga gestita in sicurezza dai meccanismi di cleanup non distruttivi o conservata come backup.
- **Test di accettazione**: Creare manualmente sia `data/webview/WV2Profile_` che `data/webview/WV2Profile_{id}` con file differenti. All'avvio dell'applicazione, il contenuto di `WV2Profile_{id}` deve rimanere inalterato e non essere sostituito o cancellato dall'orfano.
- **Priorità**: P1 (Gravità alta) | **Impatto**: Alto | **Sforzo**: Basso

## 91. [P1] Marker di versione scritto prima della copia robocopy e residui temporanei dello script batch
- **File**: `UpdateChecker.vb` (metodo `PerformUpdateFromGitHubAsync`, `WriteLocalVersionMarker`)
- **Problema / Esigenza**:
  1. `WriteLocalVersionMarker` viene invocato dal codice VB prima che lo script batch completi la sostituzione dei file: se il comando robocopy fallisce o viene interrotto, il file `.app_version` dichiara comunque la nuova versione anche se i file dell'applicazione non sono stati aggiornati. Inoltre lo script batch riscrive già il marker dopo una copia riuscita, rendendo la chiamata preventiva in VB inutile e dannosa, con incongruenza tra formati (es. `0.7.1` in VB vs `v0.7.1` nel batch).
  2. Lo script `update.bat` viene creato all'interno di `tempDir`. Se l'archivio ZIP contiene `HidaChat.exe` alla radice, `sourceDir = tempDir`: robocopy copia quindi anche `update.bat` nella cartella di installazione dell'applicazione.
  3. La cartella temporanea estratta `tempDir` (potenzialmente centinaia di MB) non viene cancellata al termine dell'aggiornamento, lasciando spazio occupato inutilmente.
- **Fix da implementare**:
  - Rimuovere la chiamata a `WriteLocalVersionMarker` dal flusso VB, delegando la scrittura del marker unicamente allo script batch (o al processo updater) solo a seguito di robocopy completato con esito favorevole (`%RC% < 8`). Unificare il formato della versione.
  - Posizionare lo script batch in `Path.GetTempPath()` all'esterno sia di `tempDir` sia di `sourceDir`, evitando che robocopy lo includa nella cartella di destinazione.
  - Aggiungere a fine script la rimozione forzata della directory temporanea estratta (`rd /s /q "<tempDir>"`) e del file zip temporaneo.
- **Test di accettazione**: Eseguire una procedura di aggiornamento: verificare che `update.bat` non sia presente nella cartella di installazione finale, che `tempDir` venga completamente rimossa al termine e che il marker `.app_version` non venga scritto se la copia robocopy fallisce.
- **Priorità**: P1 (Gravità alta) | **Impatto**: Alto | **Sforzo**: Basso-Medio

## 92. [P2] Script batch di aggiornamento fragile (parentesi nei percorsi, unità root USB e gestione errori)
- **File**: `UpdateChecker.vb` (generazione batch in `PerformUpdateFromGitHubAsync`)
- **Problema / Esigenza**:
  1. All'interno del blocco `if %RC% GEQ 8 ( ... )`, il comando `echo ... {logFile}` non racchiude il percorso tra virgolette doppie. In un percorso con parentesi come `C:\Program Files (x86)\...`, la parentesi chiusa `)` chiude anticipatamente il blocco `if`, causando un errore di sintassi cmd e il fallimento dello script.
  2. Se l'applicazione è alla radice di un drive USB (es. `E:\HidaChat.exe`, scenario esplicitamente supportato dal README per uso portabile), `installDir` risulta essere `E:\`. In robocopy la notazione `"E:\"` viene interpretata come virgoletta escapata (`\"`), rompendo il comando. È necessario normalizzare con `installDir.TrimEnd("\""c) & "\."`.
  3. In caso di errore robocopy, lo script esegue `exit /b 1` senza un comando `pause`: la console si chiude istantaneamente senza che l'utente possa leggere l'errore e l'applicazione non viene riavviata, lasciando l'utente senza interfaccia.
  4. L'uso di `taskkill /f /im HidaChat.exe` termina forzatamente qualsiasi istanza di HidaChat in esecuzione anziché verificare il PID del processo chiamante.
- **Fix da implementare**:
  - Racchiudere sempre tra virgolette doppie i percorsi espansi nei comandi echo del batch (`echo ... "%logFile%"` o `"%LOG%"`).
  - Gestire le radici disco normalizzando il percorso di destinazione per robocopy con `installDir.TrimEnd("\""c) & "\."`.
  - In caso di errore nel batch (`%RC% GEQ 8`), inserire una `pause` prima di uscire o rilanciare l'applicazione originale in modalità di emergenza per non lasciare l'utente con l'app terminata.
  - Attendere la chiusura ordinata verificando specificamente il PID (`tasklist /fi "PID eq {Environment.ProcessId}"`) anziché ricorrere a terminazioni forzate su tutti i processi.
- **Test di accettazione**: Eseguire un aggiornamento con percorso contenente parentesi tonde (es. `C:\Program Files (x86)\HidaChat`) e da radice drive (`E:\`). Verificare la corretta esecuzione di robocopy e, a fronte di un errore forzato, la presenza del messaggio leggibile prima della chiusura.
- **Priorità**: P2 (Gravità media) | **Impatto**: Medio-Alto | **Sforzo**: Basso-Medio

## 93. [P2] Verifica SHA-256: falsi mismatch con file multipli e fail-open se manca il checksum
- **File**: `UpdateChecker.vb` (metodo `ExtractSha256FromText`)
- **Problema / Esigenza**: Se il nome del file ZIP non viene individuato esattamente nel testo del checksum (ad esempio perché indicato come `./HidaChat.zip` o con prefissi di percorso), un fallback che preleva la prima stringa esadecimale da 64 caratteri del testo rischia di estrarre un hash non pertinente (ad esempio l'hash di un altro archivio in un file `SHA256SUMS` multi-asset o un commit SHA nelle note di rilascio), bloccando l'aggiornamento per falso "mismatch". Se invece non viene trovato alcun hash e non viene applicato un fail-closed rigoroso, l'update rischierebbe di procedere senza alcun controllo di integrità.
- **Fix da implementare**:
  - Normalizzare il matching del filename rimuovendo prefissi relativi come `./` o caratteri escape `*`.
  - Se è stato specificato un `filename`, ignorare il fallback generico arbitrario o accettarlo esclusivamente se nel testo è presente un unico e inequivocabile hash da 64 caratteri.
  - Applicare rigorosamente la policy Fail-Closed: se nessun checksum valido e univoco viene individuato per il file scaricato, bloccare l'aggiornamento e informare l'utente con un messaggio di errore chiaro.
- **Test di accettazione**: Fornire un file `SHA256SUMS` contenente gli hash di più file e verificare che venga estratto solo l'hash associato allo zip di HidaChat. Verificare che l'assenza di checksum blocchi l'aggiornamento con avviso e senza tentare l'installazione non verificata.
- **Priorità**: P2 (Gravità media) | **Impatto**: Medio-Alto | **Sforzo**: Basso-Medio

## 94. [P2] Controllo manuale aggiornamenti privo di feedback all'utente su errori di rete o assenza asset
- **File**: `UpdateChecker.vb` (metodi `CheckForUpdatesAsync`, `FetchGitHubReleaseInfoAsync`)
- **Problema / Esigenza**: Quando l'utente preme esplicitamente il pulsante "Verifica aggiornamenti" (`force = True`), se l'API di GitHub risponde con un errore (rate limit 403, offline, endpoint non raggiungibile) oppure se la release remota non ha un archivio ZIP allegato tra gli asset, l'esecuzione termina con un semplice `Debug.WriteLine`. L'utente non riceve alcun feedback visivo e il pulsante appare non funzionante.
- **Fix da implementare**:
  - Quando `force = True`, mostrare sempre una finestra di dialogo esplicita nei tre casi critici:
    1. Eccezione o mancata connettività di rete;
    2. Risposta HTTP non positiva dalle API GitHub (compreso rate limit 403 e 404);
    3. Nuova versione individuata ma priva di asset ZIP valido per l'aggiornamento.
- **Test di accettazione**: Eseguire il controllo manuale con rete disconnessa o simulando una risposta 403 da GitHub: deve apparire una MessageBox informativa con spiegazione dell'errore anziché una chiusura silenziosa.
- **Priorità**: P2 (Gravità media) | **Impatto**: Medio | **Sforzo**: Basso

## 95. [P2] Confronto errato delle versioni pre-release/beta e selezione della release remota
- **File**: `UpdateChecker.vb` (metodi `ComparePrerelease`, `IsNewerVersion`, `FetchGitHubReleaseInfoAsync`)
- **Problema / Esigenza**:
  1. Il confronto lessicografico `String.Compare("beta10", "beta2")` produce il risultato non corretto `beta10 < beta2` poiché il carattere '1' precede '2', impedendo il rilevamento di release beta con numerazione a più cifre.
  2. Nel canale beta, l'endpoint GitHub restituisce una lista di release: il codice seleziona la prima release dell'elenco per data, che potrebbe non corrispondere alla versione con numero semantico più alto in caso di release non sequenziali o hotfix.
- **Fix da implementare**:
  - Implementare un parsing conforme allo standard SemVer 2.0: suddividere i token alfanumerici e numerici nei tag di pre-release, confrontando i segmenti composti unicamente da cifre con comparazione numerica (`Integer.TryParse`) e i segmenti alfanumerici con comparazione lessicografica.
  - Nel canale beta, analizzare tutte le release fornite dall'API aventi uno ZIP allegato e selezionare quella con la versione semantica massima reale, anziché limitarsi alla prima riga temporale.
- **Test di accettazione**: Validare che `v1.2.10-beta` risulti correttamente più recente di `v1.2.2-beta`. Con una lista di release GitHub fuori ordine temporale, verificare che venga selezionata la versione semantica più elevata.
- **Priorità**: P2 (Gravità media) | **Impatto**: Medio | **Sforzo**: Medio

## 96. [P3] CleanupUnusedProfilesAsync esegue cancellazione ricorsiva sincrona nel thread UI con possibile freeze all'avvio
- **File**: `AccountManager.vb` (metodo `CleanupUnusedProfilesAsync`)
- **Problema / Esigenza**: Il metodo ha firma asincrona ma, se invocato direttamente o se esegue `Directory.Delete(..., True)` in modo sincrono nel contesto del thread principale all'avvio, può causare blocchi e freeze dell'interfaccia utente in presenza di profili Chromium voluminosi (migliaia di file di cache, LevelDB e blob) o contesi temporaneamente.
- **Fix da implementare**:
  - Eseguire l'intera enumerazione ed eliminazione ricorsiva all'interno di `Task.Run` fuori dal thread UI.
  - Adottare un helper di retry con backoff progressivo (es. `DeleteDirectoryWithRetryAsync`, pattern analogo a TODO #37 / #72) per gestire eventuali lock temporanei senza mai bloccare la reattività della finestra principale.
- **Test di accettazione**: Creare profili orfani con migliaia di file nella cartella profili; all'avvio dell'applicazione la UI deve apparire istantaneamente fluida e priva di blocchi durante il cleanup in background.
- **Priorità**: P3 (Gravità bassa) | **Impatto**: Basso-Medio | **Sforzo**: Basso

## 97. [P3] Mancata notifica PropertyChanged di CanAddAccount in LoadAccountsAsync e CreateDefaultAccountAsync
- **File**: `AccountManager.vb` (metodi `LoadAccountsAsync`, `CreateDefaultAccountAsync`)
- **Problema / Esigenza**: Al termine del caricamento da `settings.json` o nella creazione dell'account predefinito, la mancata emissione tempestiva di `NotifyPropertyChanged(NameOf(CanAddAccount))` può lasciare il pulsante di aggiunta account abilitato nell'interfaccia anche se sono già presenti 3 account (limite massimo predefinito).
- **Fix da implementare**: Assicurarsi che in tutti i percorsi di completamento di `LoadAccountsAsync` e `CreateDefaultAccountAsync` vengano sollevati `NotifyPropertyChanged(NameOf(CanAddAccount))`, `NotifyPropertyChanged(NameOf(HasExcessAccounts))` e `NotifyPropertyChanged(NameOf(ExcessAccountsCount))`.
- **Test di accettazione**: Avviare l'applicazione con 3 account già censiti e verificare che il pulsante "+" risulti immediatamente disabilitato all'avvio.
- **Priorità**: P3 (Gravità bassa) | **Impatto**: Basso | **Sforzo**: Basso

## 98. [P3] Mancanza di controlli di validità e null-safety in UpdateAccountNameAsync e RemoveAccountAsync
- **File**: `AccountManager.vb` (metodi `UpdateAccountNameAsync`, `RemoveAccountAsync`)
- **Problema / Esigenza**:
  1. `UpdateAccountNameAsync` accetta stringhe vuote, composte solo da spazi o nomi duplicati già in uso da altri account, degradando la leggibilità delle schede.
  2. `RemoveAccountAsync` accede a `_currentAccount.Id` senza verifica preventiva `_currentAccount IsNot Nothing`, con potenziale rischio di `NullReferenceException` se la selezione corrente è in uno stato transitorio o non inizializzato.
- **Fix da implementare**:
  - In `UpdateAccountNameAsync`, validare `newName`: scartare o rifiutare stringhe vuote/spazi (`String.IsNullOrWhiteSpace`) e nomi già esistenti nella collezione `_accounts`.
  - In `RemoveAccountAsync`, inserire il controllo di null-safety su `_currentAccount` prima di accedere alla proprietà `Id`.
- **Test di accettazione**: Verificare che tentativi di rinominare un account con nome vuoto o duplicato vengano ignorati e che la rimozione di un account con `_currentAccount` nullo non sollevi eccezioni.
- **Priorità**: P3 (Gravità bassa) | **Impatto**: Basso | **Sforzo**: Basso

## 99. [P3] Messaggi MessageBox in UpdateChecker.vb hardcoded in italiano anziché multilingua (Localization.vb)
- **File**: `UpdateChecker.vb`, `Localization.vb`
- **Problema / Esigenza**: Diverse finestre di dialogo e messaggi di avviso/errore in `UpdateChecker.vb` contengono stringhe fisse in lingua italiana, non rispettando la lingua selezionata dall'utente nell'applicazione (EN, IT, FR, ES, DE).
- **Fix da implementare**:
  - Rivedere tutti i richiami a `ShowMessageBox` e `ShowMessageBoxConfirm` in `UpdateChecker.vb`.
  - Aggiungere e sincronizzare le chiavi di traduzione corrispondenti in tutti i 5 dizionari ufficiali di `Localization.vb` (`EnStrings`, `ItStrings`, `FrStrings`, `EsStrings`, `DeStrings`).
  - Utilizzare `loc.Get(...)` per recuperare titoli e testi localizzati in base alla lingua impostata.
- **Test di accettazione**: Cambiare la lingua dell'applicazione in inglese o francese; verificare che tutti i popup di `UpdateChecker.vb` appaiano tradotti nella lingua scelta.
- **Priorità**: P3 (Gravità bassa) | **Impatto**: Basso | **Sforzo**: Basso

## 100. [P3] Dichiarazione di variabile locale non utilizzata subDirs in UpdateChecker.vb
- **File**: `UpdateChecker.vb`
- **Problema / Esigenza**: In `UpdateChecker.vb` è presente (o residua) la riga `Dim subDirs = Directory.GetDirectories(tempDir)` non utilizzata, che genera warning del compilatore e provoca un'inutile operazione di I/O sul filesystem durante l'estrazione.
- **Fix da implementare**: Rimuovere la riga di dichiarazione orfana `Dim subDirs = ...` dal codice di `UpdateChecker.vb`.
- **Test di accettazione**: Compilazione pulita senza warning relativi a variabili non utilizzate nel file.
- **Priorità**: P3 (Gravità bassa) | **Impatto**: Basso | **Sforzo**: Minimo

## 101. [P3] Finestra About: icona sbiadita e descrizione troncata senza a capo
- **File**: `AboutWindow.xaml`
- **Problema / Esigenza**:
  1. L'icona dell'applicazione nell'header della finestra About (`<Image Source="images/icon.ico" ... />`) risulta sbiadita o a bassa fedeltà/risoluzione a causa del caricamento diretto del file `.ico` senza opzioni di rendering avanzate (o mancato impiego dell'asset PNG nitido `images/icon.png` e proprietà `RenderOptions.BitmapScalingMode="HighQuality"`).
  2. Il testo descrittivo dell'applicazione (`TxtDescription`, *"Client desktop multi-account portabile e leggero..."*) viene tagliato orizzontalmente e non va a capo: trovandosi all'interno di uno `StackPanel` orizzontale privo di vincoli di larghezza, la proprietà `TextWrapping="Wrap"` non limita la dimensione e il testo fuoriesce dal bordo visibile della finestra.
- **Fix da implementare**:
  - Utilizzare la sorgente immagine ad alta definizione (es. `images/icon.png`) oppure impostare `RenderOptions.BitmapScalingMode="HighQuality"` e `UseLayoutRounding="True"` sull'icona per eliminare la resa sbiadita/sfocata.
  - Sostituire lo `StackPanel` orizzontale contenitore con un layout che vincoli la larghezza (es. `Grid` con colonna `Auto` per l'icona e colonna `*` per i testi, oppure `DockPanel` / `MaxWidth`), consentendo a `TxtDescription` di andare a capo correttamente (`TextWrapping="Wrap"`) all'interno dell'area visibile della finestra.
- **Test di accettazione**: Aprire la finestra "Informazioni su HidaChat": verificare che l'icona appaia nitida e con colori corretti e che il testo descrittivo vada a capo automaticamente senza essere troncato ai margini.
- **Priorità**: P3 (Gravità bassa) | **Impatto**: Basso-Medio | **Sforzo**: Basso

## ~~102. [P2] Limitare la cache WebView2 alla fonte tramite AdditionalBrowserArguments~~ ✅
- **File**: BrowserArgumentsPolicy.vb, AppAccounts.vb, SettingsController.vb, SettingsWindow.xaml, SettingsWindow.xaml.vb, Tests/SessionTests.vb
- **Descrizione**: Implementata la limitazione hard limit della cache su disco e disabilitazione di componenti Chromium/Edge superflui via AdditionalBrowserArguments.
- **Implementazione**:
  - Creato il modulo BrowserArgumentsPolicy.vb con il costruttore deterministico BuildAdditionalBrowserArguments(diskCacheLimitMb, enableSpellcheck, effectiveLang).
  - Imposto hard limit default per cache HTTP (--disk-cache-size=52428800, 50 MB, configurabile tramite SettingsController.DiskCacheLimitMb con nuova opzione "50 MB (Compatto)" e fallback a 0 per illimitata).
  - Imposto hard limit per cache audio/video --media-cache-size=20971520 (20 MB).
  - Attivato flag di eliminazione immediata della cache obsoleta --aggressive-cache-discard.
  - Disabilitato il salvataggio su disco degli shader GPU --disable-gpu-shader-disk-cache (prevenendo l'accumulo in GPUCache, GrShaderCache, ecc.).
  - Bloccato il download in background di aggiornamenti componenti CRX --disable-component-update.
  - Bloccata la telemetria e modelli di sfondo Edge --disable-background-networking.
  - Disabilitate le feature non necessarie --disable-features=Translate,MediaRouter,OptimizationGuideModelDownloading,OptimizationHintsFetching (oltre a Spellcheck se non abilitato).
  - Integrata suite di test automatizzati in Tests/SessionTests.vb (207 asserzioni con 0 fallimenti).
- **File**: `MainWindow.xaml.vb`, `AccountManager.vb`
- **Problema / Esigenza**: All'inizializzazione del `CoreWebView2Environment`, Chromium può accumulare centinaia di megabyte di cache su disco e scaricare componenti Edge/Chromium non necessari (incluso il traduttore nativo di Chromium, superfluo dato che HidaChat include già il proprio motore in `JsScripts.vb` / `Scripts/translation.js`, oltre a modelli di ottimizzazione e telemetria in background).
- **Soluzione proposta / Fix da implementare**:
  Passare argomenti di avvio specifici a Chromium tramite la proprietà `AdditionalBrowserArguments` di `CoreWebView2EnvironmentOptions` per imporre un tetto massimo (hard limit) su disco ed escludere componenti ridondanti:
  ```vb
  Imports Microsoft.Web.WebView2.Core

  Public Async Function CreateOptimizedEnvironmentAsync(userDataFolder As String) As Task(Of CoreWebView2Environment)
      ' Argomenti Chromium per limitare drasticamente lo spazio su disco
      Dim args As String = String.Join(" ", {
          "--disk-cache-size=52428800",        ' Max 50 MB di cache HTTP per profilo
          "--media-cache-size=20971520",       ' Max 20 MB di cache audio/video
          "--aggressive-cache-discard",        ' Scarta subito la cache vecchia
          "--disable-gpu-shader-disk-cache",   ' Non salvare gli shader GPU su disco
          "--disable-component-update",        ' Blocca il download di componenti CRX in background
          "--disable-background-networking",   ' Evita download di telemetria/modelli Edge
          "--disable-features=OptimizationGuideModelDownloading,OptimizationHintsFetching,Translate"
      })

      Dim options As New CoreWebView2EnvironmentOptions(
          additionalBrowserArguments:=args,
          language:=Nothing,
          targetCompatibleBrowserVersion:=Nothing,
          allowSingleSignOnUsingOSPrimaryAccount:=False
      )

      Return Await CoreWebView2Environment.CreateAsync(Nothing, userDataFolder, options)
  End Function
  ```
- **Test di accettazione**: Verificare all'avvio che le istanze WebView2 vengano inizializzate con i flag specificati, che la cache HTTP non ecceda il tetto prefissato e che la navigazione su WhatsApp e Telegram resti pienamente funzionante.
- **Priorità**: P2 (Gravità media) | **Impatto**: Alto | **Sforzo**: Basso

## 103. [P2] Condivisione di un unico CoreWebView2Environment con profili nativi (ProfileName)
- **File**: `AccountManager.vb`, `MainWindow.xaml.vb`
- **Problema / Esigenza**: La creazione di percorsi cartella isolati separati per ciascun account (`data/webview/account_1`, `data/webview/account_2`, ecc.) con un `CoreWebView2Environment` differente per ciascuna scheda moltiplica per ogni account tutti i file di sistema, log e binari interni di Chromium (`EBWebView`).
- **Soluzione proposta / Fix da implementare**:
  Utilizzare una sola cartella dati base condivisa (`data/webview`) con un singolo `CoreWebView2Environment`, separando gli account tramite i profili nativi di WebView2 con `CoreWebView2ControllerOptions.ProfileName`:
  ```vb
  ' Usa lo stesso _sharedEnvironment ("data/webview") per tutte le tab:
  Dim controllerOptions = _sharedEnvironment.CreateCoreWebView2ControllerOptions()
  controllerOptions.ProfileName = $"Account_{account.Id}" ' Es. "Account_1", "Account_2"
  controllerOptions.IsInPrivateModeEnabled = False

  Await webView.EnsureCoreWebView2Async(_sharedEnvironment, controllerOptions)
  ```
  In questo modo Cookie, IndexedDB e LocalStorage rimangono isolati al 100% (in sottocartelle del profilo), mentre l'infrastruttura motore viene allocata una sola volta, con notevole risparmio di RAM e spazio su disco.
- **Test di accettazione**: Aprire più account contemporaneamente (es. WhatsApp 1 e WhatsApp 2) verificando che le sessioni non entrino in conflitto, che i cookie/IndexedDB rimangano separati e che la cartella `EBWebView` non duplichi i file di sistema.
- **Priorità**: P2 (Gravità media) | **Impatto**: Alto | **Sforzo**: Medio

## 104. [P2] Pulizia chirurgica a caldo della cache con ClearBrowsingDataAsync senza deautenticare gli account
- **File**: `SettingsWindow.xaml.vb`, `AccountManager.vb`
- **Problema / Esigenza**: Durante l'uso prolungato, WebView2 accumula centinaia di megabyte di `DiskCache` e `CacheStorage` (Service Worker cache). I metodi tradizionali di cancellazione cartella rischiano di spazzare via Cookie, LocalStorage e IndexedDB provocando il logout forzato da WhatsApp Web e Telegram Web.
- **Soluzione proposta / Fix da implementare**:
  Fornire una funzione di compattazione a caldo richiamabile manualmente (es. pulsante "Ottimizza Spazio" nelle Impostazioni) o su base periodica programmata, invocando `CoreWebView2.Profile.ClearBrowsingDataAsync` solo sui dati rigenerabili:
  ```vb
  Imports Microsoft.Web.WebView2.Core

  Public Async Function CompactWebViewProfileAsync(webView As Microsoft.Web.WebView2.Wpf.WebView2) As Task
      If webView?.CoreWebView2?.Profile Is Nothing Then Return

      ' Selezioniamo SOLO le cache rigenerabili, escludendo Cookies, LocalStorage e IndexedDB!
      Dim dataKinds As CoreWebView2BrowsingDataKinds =
          CoreWebView2BrowsingDataKinds.DiskCache Or
          CoreWebView2BrowsingDataKinds.CacheStorage

      Await webView.CoreWebView2.Profile.ClearBrowsingDataAsync(dataKinds)
  End Function
  ```
- **Test di accettazione**: Invocare la pulizia a caldo su una sessione attiva di WhatsApp Web con messaggi aperti; verificare che la dimensione della cache su disco si riduca sensibilmente e che la sessione non venga disconnessa (nessuna richiesta di nuovo QR code al riavvio/refresh).
- **Priorità**: P2 (Gravità media) | **Impatto**: Medio-Alto | **Sforzo**: Basso

## 105. [P2] Routine di pulizia automatica cartelle spazzatura all'avvio
- **File**: `Application.xaml.vb`, `AccountManager.vb`
- **Problema / Esigenza**: All'avvio dell'applicazione, prima che i processi `msedgewebview2.exe` vengano instanziati e blocchino i file su disco, rimangono file e sottocartelle temporanee create da crash, diagnostica o shader che occupano spazio non necessario nella cartella `data/webview`.
- **Soluzione proposta / Fix da implementare**:
  Eseguire prima dell'inizializzazione dei controlli WebView2 la rimozione sicura delle cartelle temporanee che non contengono mai informazioni di login o sessione:
  ```vb
  Imports System.IO

  Public Sub CleanWebViewJunkFolders(baseDataPath As String)
      If Not Directory.Exists(baseDataPath) Then Return

      ' Cartelle sicure da eliminare al 100% (non contengono mai dati di login)
      Dim junkFolderNames As String() = {
          "Crashpad",
          "GrShaderCache",
          "ShaderCache",
          "GraphiteDawnCache",
          "DawnCache",
          "GPUCache",
          "Code Cache",
          "Cache",
          "BrowserMetrics",
          "component_crx_cache",
          "OptimizationGuidePredictionModels",
          "Safe Browsing"
      }

      Try
          For Each dirPath In Directory.EnumerateDirectories(baseDataPath, "*", SearchOption.AllDirectories)
              Dim dirName As String = Path.GetFileName(dirPath)
              If junkFolderNames.Contains(dirName, StringComparer.OrdinalIgnoreCase) Then
                  Try
                      Directory.Delete(dirPath, recursive:=True)
                  Catch
                      ' Ignora se un file è bloccato o già rimosso
                  End Try
              End If
          Next

          ' Elimina anche eventuali file .dmp o .pma (metriche e crash dump)
          For Each filePath In Directory.EnumerateFiles(baseDataPath, "*.*", SearchOption.AllDirectories)
              Dim ext As String = Path.GetExtension(filePath).ToLowerInvariant()
              If ext = ".dmp" OrElse ext = ".pma" OrElse ext = ".old" Then
                  Try
                      File.Delete(filePath)
                  Catch
                  End Try
              End If
          Next
      Catch ex As Exception
          ' Log opzionale
      End Try
  End Sub
  ```
  *Attenzione alle cartelle IndexedDB*: WhatsApp memorizza i file multimediali scaricati in `IndexedDB\https_web.whatsapp.com_0.indexeddb.blob`, mentre i token crittografici e di sessione Signal si trovano nella gemella `.leveldb`. La cartella `.leveldb` non deve mai essere rimossa per evitare la perdita della sessione utente.
- **Test di accettazione**: Simulare la presenza di file `.dmp`, `.pma` e cartelle `Crashpad` o `GPUCache` nella cartella dati; verificare all'avvio che vengano rimosse senza toccare `.leveldb` o i cookie e che l'avvio prosegua regolarmente.
- **Priorità**: P2 (Gravità media) | **Impatto**: Medio | **Sforzo**: Basso

## 106. [P3] Compressione trasparente NTFS (compact.exe) per la cartella dati portabile
- **File**: `Application.xaml.vb`, `AccountManager.vb`
- **Problema / Esigenza**: La cartella `data/webview` contiene file altamente comprimibili (database LevelDB, JSON, JavaScript, log di testo). Comprimere la cartella in un archivio ZIP/7z in chiusura e decomprimerla all'avvio comporterebbe sensibili ritardi di avvio e il rischio critico di corruzione in caso di crash o arresto improvviso del sistema.
- **Soluzione proposta / Fix da implementare**:
  Abilitare la compressione trasparente a livello di filesystem NTFS direttamente sulla cartella `data/` alla sua creazione eseguendo l'utility nativa `compact.exe` in background:
  ```vb
  Imports System.Diagnostics
  Imports System.IO

  Public Sub EnableNtfsCompression(folderPath As String)
      If Not Directory.Exists(folderPath) Then Directory.CreateDirectory(folderPath)

      ' Esegue il comando nativo di Windows compact.exe in background sulla cartella data
      Dim psi As New ProcessStartInfo("compact.exe", $"/C /S:""{folderPath}"" /I /Q") With {
          .CreateNoWindow = True,
          .UseShellExecute = False,
          .WindowStyle = ProcessWindowStyle.Hidden
      }
      Using proc = Process.Start(psi)
          ' Non serve bloccare il thread principale
      End Using
  End Sub
  ```
  *Nota*: Su supporti rimovibili formattati in FAT32/exFAT (es. chiavette USB portabili), il parametro `/I` permette a `compact.exe` di ignorare silenziosamente l'operazione senza generare errori o bloccare l'esecuzione.
- **Test di accettazione**: Su disco NTFS, verificare che le proprietà della cartella `data/webview` presentino l'attributo "Compresso" attivo, con una riduzione di spazio occupato su disco di circa il 40-50% senza impatto negativo sulle prestazioni di lettura/scrittura di WebView2.
- **Priorità**: P3 (Gravità bassa) | **Impatto**: Medio | **Sforzo**: Basso



