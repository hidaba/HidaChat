# Changelog

## [0.7.5-beta] - 2026-09-01

### Pre-release / Beta — Do Not Disturb (Focus Mode)
- **Modalità "Non Disturbare" / Focus Mode Temporizzata (TODO #47) (`MainWindow.xaml`, `MainWindow.xaml.vb`, `SettingsController.vb`, `AppAccounts.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`, `TODO.md`)**:
  - **Pulsante Rapido nella TitleBar & Menu Contestuale**: Inserito il pulsante `BtnDnd` nella barra del titolo con icona campana silenziata (`Bell-Off`), badge visivo attivo e menu a comparsa per impostare durate predefinite (*Per 30 minuti*, *Per 1 ora*, *Per 2 ore*, *Per 8 ore*, *Fino a disattivazione manuale*).
  - **Silenziamento Totale e Muting Audio Sincrono**: Soppressione immediata delle notifiche Toast di Windows e dei popup `MessagePopup` per tutti gli account, con muting hardware dei suoni web (`CoreWebView2.IsMuted = True`).
  - **Timer con Ripristino Automatico**: Controllo periodico dello stato e disattivazione automatica alla scadenza della durata impostata con tooltip indicante l'orario di fine (es. *"Non Disturbare attivo fino alle 16:30"*).
  - **Configurazione nelle Impostazioni & Multilingua Completo**: Controlli dedicati nella sezione Notifiche delle *Impostazioni* e traduzioni native precompilate per tutte le 5 lingue supportate (`EnStrings`, `ItStrings`, `FrStrings`, `EsStrings`, `DeStrings`).

## [0.7.4-beta] - 2026-08-30

### Pre-release / Beta — Native Multilingual Spellchecker
- **Correttore Ortografico Nativo Multilingua (TODO #44) (`AppAccounts.vb`, `SettingsController.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`, `TODO.md`)**:
  - **Integrazione Motore Ortografico Chromium / WebView2**: Abilitazione e configurazione del correttore ortografico nativo (`--enable-features=Spellcheck`, `--lang=...`) su tutte le istanze isolate dei profili WebView2 per WhatsApp Web e Telegram Web.
  - **Sezione Dedicata nelle Impostazioni**: Nuova sezione nelle *Impostazioni* con checkbox per attivare o disattivare il correttore e selettore ComboBox della lingua del dizionario (*Automatica da lingua app*, *English*, *Italiano*, *Français*, *Español*, *Deutsch*).
  - **Persistenza & Supporto Multilingua Completo**: Salvataggio automatico delle preferenze ortografiche in `settings.json` con traduzioni allineate nei 5 dizionari ufficiali (`EnStrings`, `ItStrings`, `FrStrings`, `EsStrings`, `DeStrings`).

## [0.7.3-beta] - 2026-08-28

### Pre-release / Beta — Multilingual Expansion (French, Spanish, German)
- **Supporto Completo Nuove Lingue Interfaccia (Français, Español, Deutsch) (`Localization.vb`, `SettingsController.vb`, `SettingsWindow.xaml.vb`, `AppAccounts.vb`, `AGENTS.md`)**:
  - **Integrazione Dizionari Nativi Precompilati**: Aggiunti i dizionari completi `FrStrings` (Francese 🇫🇷), `EsStrings` (Spagnolo 🇪🇸) e `DeStrings` (Tedesco 🇩🇪) per tutte le oltre 60 stringhe ed elementi dell'interfaccia utente (Impostazioni, Gestione account, Notifiche, DevTools, Invio Massivo Excel/CSV, Informazioni, CSS Personalizzato, Indicatore Online).
  - **Menu a Tendina Impostazioni & Switching Live**: Estesa la lista `SupportedLanguages` con le 5 lingue ufficiali (English, Italiano, Français, Español, Deutsch), con aggiornamento dinamico immediato dell'interfaccia e notifica sincrona di traduzione verso tutti i controlli WebView2 senza richiedere il riavvio dell'applicazione.
  - **Linee Guida di Mantenimento Traduzioni**: Istituita la regola vincolante in `AGENTS.md` per la sincronizzazione obbligatoria di tutti i dizionari ad ogni nuova stringa o funzionalità aggiunta al progetto.

## [0.7.2-beta] - 2026-08-28

### Pre-release / Beta — Online Contact Indicator & Custom CSS Injector
- **Indicatore di Stato "Online" e "Sta scrivendo..." dei Contatti (TODO #42) (`Scripts/notification.js`, `AppAccounts.vb`, `MainWindow.xaml`, `MainWindow.xaml.vb`)**:
  - **Rilevamento Continuo in Tempo Reale**: Intercettazione automatica dello stato del contatto aperto nella chat attiva tramite `MutationObserver` e scansione periodica del DOM sia per **WhatsApp Web** (`#main header`, `[data-testid="conversation-header"]`, `span[title]`) che per **Telegram Web K** (`.chat-info .person-status`, `.chat-info .status`, `.topbar .status`, `.chat-subtitle`).
  - **Badge Dinamico nella TitleBar**: Indicatore visivo elegante nella barra del titolo (`OnlineIndicatorBorder`) con pallino colorato (`#25d366` per WhatsApp, `#24A1DE` per Telegram) e dicitura di stato (es. *"in linea"*, *"online"*, *"sta scrivendo..."*, *"typing..."*).
  - **Indicatore Grafico sulle Schede Account**: Pallino verde (`TabOnlineDot`) visualizzato sulla scheda dell'account quando il contatto corrente è attivo.
  - **Sincronizzazione Multi-Account**: Aggiornamento automatico e immediato al passaggio tra schede e al cambio di chat.
- **Supporto Completo a Temi CSS Personalizzati Utente (TODO #43) (`SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `SettingsController.vb`, `JsScripts.vb`, `AppAccounts.vb`)**:
  - **Editor CSS Monospace nelle Impostazioni**: Nuova sezione dedicata nelle *Impostazioni* con editor multiriga (`TxtCustomCss`) a spaziatura fissa (Consolas / Cascadia Mono), evidenziazione bordi e scrollbar per inserire regole CSS personalizzate per WhatsApp e Telegram Web.
  - **Pulsanti Preset Rapidi Integrati**: Inserimento con un click di configurazioni stilistiche popolari:
    - *OLED Nero Puro*: Sfondo nero assoluto (`#000000`) per display AMOLED/OLED a risparmio energetico.
    - *Layout Compatto*: Riduzione larghezza sidebar e padding elementi chat.
    - *Font Moderno*: Tipografia moderna pulita (`Segoe UI Variable Display` / system fonts).
    - *Svuota*: Reset immediato del campo di testo.
  - **Iniezione Dinamica in Tempo Reale**: Applicazione istantanea delle modifiche CSS a tutti i controlli WebView2 attivi senza dover riavviare l'applicazione o ricaricare la pagina (`hidachat-custom-user-css`).
  - **Persistenza & Supporto Multilingua**: Salvataggio automatico in `settings.json` con localizzazione completa in Italiano ed Inglese in `Localization.vb`.

## [0.7.1-beta] - 2026-08-27

### Pre-release / Beta — Telegram Advanced Evolution & Desktop Shortcuts
- **Badge Notifiche & Contatore Messaggi Non Letti Telegram e WhatsApp (TODO #41) (`Scripts/notification.js`, `AppAccounts.vb`, `MainWindow.xaml`)**:
  - **Monitoraggio Ibrido in Tempo Reale**: Combinazione di `MutationObserver` sul titolo (`<title>`) e scansione periodica mirata del DOM per intercettare i badge non letti nativi di **Telegram Web K** (`.badge.unread`, `.unread-count`, `.chatlist-chat .badge`, `.dialog-subtitle .badge`) e **WhatsApp Web** (`[data-testid="unread-count"]`).
  - **Badge Grafico sulle Schede Account**: Visualizzazione di una pillola badge rossa con contatore numerico (`"1"`, `"5"`, `"99+"`) o pallino di notifica (`"•"`) direttamente sopra l'etichetta dell'account.
  - **Sincronizzazione Tray Icon**: Aggiornamento automatico e immediato dell'icona nella System Tray Windows con stato di notifica attiva (`icon_notification.ico`).
- **Gestione Deep Link Telegram (`tg://` e `https://t.me/`) (TODO #41) (`AppAccounts.vb`)**:
  - Intercettazione in `NavigationStarting` e `NewWindowRequested` di collegamenti con protocollo `tg://` (`tg://resolve?domain=...`, `tg://resolve?phone=...`, `tg://join?invite=...`, `tg://msg_url?...`) e link web `https://t.me/...` o `https://telegram.me/...`.
  - Risoluzione e routing interno automatico verso l'interfaccia **Telegram Web K** (`https://web.telegram.org/k/#@...` / `https://web.telegram.org/k/#?tgaddr=...`), permettendo di aprire canali, gruppi e chat direttamente dentro HidaChat senza richiedere l'installazione di client esterni né generare errori di navigazione.
- **Scorciatoie da Tastiera Globali per la Finestra (TODO #41) (`MainWindow.xaml`, `MainWindow.xaml.vb`)**:
  - `Ctrl + 1`, `Ctrl + 2`, `Ctrl + 3` (e tastierino numerico): switch istantaneo all'Account 1, 2 o 3.
  - `Ctrl + Tab`: passaggio circolare alla scheda successiva.
  - `Ctrl + Shift + Tab`: passaggio circolare alla scheda precedente.
  - `Ctrl + T` / `Ctrl + N`: aggiunta rapida di un nuovo account (se limite consentito).
  - `Ctrl + R` / `F5`: ricarica della scheda/piattaforma attiva.
  - `Ctrl + ,` (Virgola): apertura rapida delle Impostazioni.
  - `Ctrl + B` / `Ctrl + E`: apertura immediata della finestra di Invio Massivo (Bulk Sender).
  - `F1` / `Ctrl + Shift + A`: apertura della finestra Informazioni (About).

## [0.7.0] - 2026-08-26

### Stable Release - Multi-Platform Bulk Sender, Security & Performance Hardening
- **Invio Massivo Personalizzato Multi-Piattaforma per WhatsApp e Telegram (`BulkSenderWindow.xaml`, `BulkSenderWindow.xaml.vb`, `ExcelContactService.vb`, `BulkSenderEngine.vb`, `BulkContactItem.vb`)**:
  - **Importazione da File Excel e CSV**: Modulo completo per l'importazione di elenchi contatti da fogli di calcolo **Excel (`.xlsx`, `.xls`)** e file **CSV (`.csv`)** con mappatura automatica e intelligente delle colonne (*Telefono*, *Nome*, *Cognome*, *Azienda*, *Testo personalizzato*, *Username*).
  - **Supporto Multi-Piattaforma WhatsApp Web & Telegram Web**: Invio sequenziale asincrono tramite WebView2 sia per WhatsApp (`web.whatsapp.com/send`) che per Telegram (`tg://resolve?domain=...` / `tg://resolve?phone=...`), con iniezione JavaScript per la composizione, formattazione ed invio del messaggio.
  - **Template Dinamici & Segnaposto**: Editor dinamico con pulsanti rapidi per l'inserimento dei tag segnaposto (`{Nome}`, `{Cognome}`, `{Azienda}`, `{Telefono}`, `{Username}`, `{Testo}`) e anteprima in tempo reale.
  - **Ispettore di Anteprima Dettagliata & Coerenza 1:1**: Riquadro dedicato per visualizzare il messaggio completo con supporto multilinea, emoji (`📍`, `📌`, `🕒`, `🥂`, `🎼`, `🍽`, `🏨`, `🚌`) e rilevamento automatico del testo personalizzato da foglio sorgente.
  - **Protezione Anti-Spam (Jitter Delay con Vincolo Minimo 30s)**: Intervallo di sicurezza regolabile tra gli invii con vincolo minimo di 30 secondi e conto alla rovescia in tempo reale per proteggere gli account da blocchi per spam.
  - **Controlli di Esecuzione & UI Avanzata**: Pausa, Riprendi, Interrompi, tracciamento dello stato di ogni riga (`In attesa`, `Inviando...`, `Inviato ✔`, `Errore ✖`, `Non valido`), supporto a finestra ridimensionabile, schermo intero e tema grafico contestuale alla piattaforma attiva.
- **Verifica Crittografica Integrità SHA-256 Aggiornamenti OTA (`UpdateChecker.vb`)**:
  - Validazione crittografica automatica dell'archivio ZIP scaricato da GitHub Releases tramite hash SHA-256 prima dell'installazione per prevenire pacchetti corrotti o manomessi.
- **Hardening di Sicurezza IPC Bridge Token (`AppAccounts.vb`, `JsScripts.vb`, `Scripts/notification.js`, `Scripts/translation.js`)**:
  - Rimossa l'esposizione globale di `window.__bridgeToken` e confinato il token di sicurezza all'interno delle closure private (IIFE) degli script JavaScript iniettati.
  - Serializzazione sicura e deterministica di parametri e comandi JavaScript con `JsonSerializer.Serialize`.
- **Prestazioni & Pre-caricamento Multi-Account (`MainWindow.xaml.vb`, `Scripts/telegram.css`)**:
  - Eliminazione completa di sfarfallii e schermo nero al passaggio tra account grazie al precaricamento parallelo delle WebView2 fuori schermo.
  - Stili dedicati per Telegram Web K e A con layout responsive ottimizzato e sincronizzazione temi chiaro/scuro.

## [0.6.7-beta] - 2026-08-25

### New Features & Multi-Platform Bulk Sender
- **Supporto Invio Massivo per Telegram Web (`BulkSenderEngine.vb`, `BulkContactItem.vb`, `ExcelContactService.vb`, `BulkSenderWindow.xaml`, `MainWindow.xaml.vb`)**:
  - Esteso il modulo di invio massivo da Excel/CSV per funzionare senza soluzione di continuità sia con **WhatsApp Web** che con **Telegram Web**.
  - **Gestione @Username**: introdotta la proprietà `Username` / `CleanUsername` e il relativo segnaposto `{Username}` / `{Utente}` per Telegram, con supporto alla risoluzione automatica delle chat via link di dominio (`tg://resolve?domain=...`) o via numero telefonico (`tg://resolve?phone=...`).
  - **Riconoscimento Automatico Colonne**: il parser di file Excel e CSV rileva automaticamente colonne denominate *Username*, *@username*, *Utente*, *User*, *Nick*, *Handle*, *Telegram*.
  - **Iniezione JavaScript per Telegram Web K**: gestione dell'apertura del composer di messaggio, digitazione del testo formattato, simulazione eventi DOM e click automatico del pulsante di invio.
  - **Interfaccia Grafica Multi-Piattaforma Contestuale**: la finestra si adatta dinamicamente con icone e colori specifici per piattaforma (Verde `#00a884` per WhatsApp, Azzurro `#24A1DE` per Telegram).
- **Protezione Anti-Spam (Vincolo Minimo 30 Secondi) (`BulkSenderWindow.xaml`, `BulkSenderWindow.xaml.vb`, `BulkSenderEngine.vb`)**:
  - Introdotto il controllo e la validazione rigorosa per imporre un intervallo minimo di **30 secondi** tra i singoli invii (sia per il delay minimo che per il massimo), proteggendo gli account WhatsApp e Telegram dal rischio di blocchi per spam.

## [0.6.6-beta] - 2026-08-25

### Improved & UI Refinements
- **Coerenza 1:1 Testo e Anteprima Invio Massivo (`BulkSenderWindow.xaml.vb`, `BulkContactItem.vb`)**:
  - Implementato il rilevamento automatico del testo personalizzato all'apertura del file Excel/CSV: se le righe contengono già una colonna *Testo* valorizzata, il modello viene impostato automaticamente su `{Testo}`, assicurando che l'anteprima e il messaggio finale coincidano esattamente carattere per carattere con il testo del file sorgente senza prefissi indesiderati.
- **Ispettore di Anteprima Dettagliata (`BulkSenderWindow.xaml`, `BulkSenderWindow.xaml.vb`)**:
  - Aggiunto un riquadro dedicato sotto la tabella dei contatti per l'ispezione immediata del messaggio completo associato alla riga selezionata, con supporto a formattazione multilinea, ritorni a capo, link ed emoji (`📍`, `📌`, `🕒`, `🥂`, `🎼`, `🍽`, `🏨`, `🚌`).
- **Supporto a Schermo Intero e Ridimensionamento (`BulkSenderWindow.xaml`, `BulkSenderWindow.xaml.vb`)**:
  - Inseriti i pulsanti di riduzione a icona (`—`) e massimizzazione/ripristino nella barra del titolo.
  - Aggiunto il supporto al doppio click sulla barra del titolo per alternare la modalità normale/schermo intero e al trascinamento fluido per il ripristino.
  - Integrato il message hook Win32 `WM_GETMINMAXINFO` per limitare l'ingrandimento alla Working Area del monitor evitando la sovrapposizione alla barra delle applicazioni di Windows.

## [0.6.5-beta] - 2026-08-24

### New Features & Bulk Automation
- **Invio Massivo Personalizzato da File Excel / CSV (TODO #52) (`BulkSenderWindow.xaml`, `BulkSenderWindow.xaml.vb`, `ExcelContactService.vb`, `BulkSenderEngine.vb`, `BulkContactItem.vb`)**:
  - Implementato il modulo completo per l'importazione di elenchi contatti da file **Excel (`.xlsx`, `.xls`)** e **CSV (`.csv`)**, con rilevamento automatico e mappatura intelligente delle colonne (*Telefono*, *Nome*, *Cognome*, *Azienda*, *Testo personalizzato*).
  - Normalizzazione e pulizia automatica dei numeri telefonici con rimozione di caratteri speciali e formattazione con prefisso internazionale.
  - Editor del messaggio con supporto a **Template Dinamici** e sostituzione automatica dei tag segnaposto (`{Nome}`, `{Cognome}`, `{Azienda}`, `{Telefono}`, `{Testo}`) con pulsanti di inserimento rapido a click singolo e anteprima in tempo reale nella tabella contatti.
  - **Motore di Invio Sequenziale Asincrono via WebView2**:
    - Navigazione diretta alla chat su WhatsApp Web (`web.whatsapp.com/send`) con injection JavaScript per l'invio automatico e il monitoraggio dello stato di caricamento.
    - Rilevamento automatico e chiusura dei popup di errore per numeri non registrati su WhatsApp (`INVALID_NUMBER`).
    - **Protezione Anti-Spam (Jitter Delay)**: intervallo di attesa configurabile (Min/Max secondi) con ritardo casuale naturale tra i singoli invii e visualizzazione del conto alla rovescia in tempo reale.
    - Controlli completi di esecuzione con supporto a **Pausa / Riprendi**, **Interruzione immediata** e tracciamento dello stato di ogni riga (`In attesa`, `Inviando...`, `Inviato ✔`, `Errore ✖`, `Non valido`).
  - Aggiunto il pulsante dedicato con icona Excel nella barra del titolo (`MainWindow.xaml`) per l'accesso immediato dallo schermo principale.
  - Aggiornate le definizioni di localizzazione in Italiano e Inglese in `Localization.vb`.

## [0.6.4-beta] - 2026-08-24

### Security & Integrity
- **Verifica Integrità SHA-256 Aggiornamenti Automatici (TODO #40) (`UpdateChecker.vb`)**:
  - Implementato il controllo crittografico di integrità dell'archivio ZIP prima dell'estrazione e della sostituzione dei file applicativi durante gli aggiornamenti OTA da GitHub Releases.
  - Aggiunto il parsing e download automatico degli asset di checksum allegati alle release (`.sha256`, `.sha256sum`, `SHA256SUMS`, `SHA256SUMS.txt`, `checksums.txt`) e il riconoscimento intelligente delle impronte SHA-256 specificate nel testo delle note di rilascio (`release body`).
  - Blocco preventivo dell'aggiornamento con avviso di sicurezza in caso di mancata corrispondenza dell'impronta calcolata con `SHA256.HashData`, prevenendo l'installazione di pacchetti incompleti o manomessi.

## [0.6.3-beta] - 2026-08-21

### Security & Hardening
- **Incapsulamento Privato Bridge Token IPC (TODO #39) (`AppAccounts.vb`, `JsScripts.vb`, `Scripts/notification.js`, `Scripts/translation.js`)**:
  - Eliminata l'esposizione del `bridgeToken` dall'oggetto globale `window.__bridgeToken`.
  - Il token di sicurezza viene ora iniettato e confinato rigorosamente all'interno delle closure (IIFE) private di `notification.js` e `translation.js`, proteggendolo da qualsiasi script, estensione o codice in esecuzione nel DOM.
  - Incapsulata la mappa interna `activeCustomNotifications` nella closure privata di `notification.js`.

## [0.6.2-beta] - 2026-08-20

### Fixed & Improved
- **Fix Layout e Barra di Digitazione Telegram Web (`Scripts/telegram.css`)**:
  - Risolto il problema di spaginazione su Telegram Web (K e A) in cui la barra di digitazione della chat e il campo di testo finivano posizionati sotto la sidebar dei contatti/utenti (`#column-left`).
  - Rimossi i selettori forzati di larghezza (`width: 100% !important;`, `--columns-width: 100% !important;`, `--composer-width: 100% !important;`) per preservare il layout nativo e responsive delle colonne e l'ancoraggio corretto del campo messaggio all'interno della conversazione attiva.

## [0.6.1-beta] - 2026-08-20

### Fixed & Improved
- **Fix Pre-caricamento Multi-Account & Eliminazione Schermo Nero (`MainWindow.xaml.vb`)**:
  - Risolto il problema del blocco su schermo nero al passaggio tra account (es. da Telegram a WhatsApp): le istanze WebView2 non attive rimangono ora visibili nel visual tree ma posizionate fluidamente fuori dallo schermo (`Margin = -20000` con `ZIndex = 0`), consentendo a Chromium di completare l'inizializzazione dell'HWND, negoziare i WebSocket e renderizzare la pagina in background in parallelo.
  - Impostato `DefaultBackgroundColor` sincronizzato con il tema dell'applicazione per eliminare qualsiasi sfarfallio nero/bianco all'avvio e al cambio scheda.
  - Passaggio tra schede WhatsApp e Telegram ora istantaneo a zero latenza (0 ms).

## [0.6.0] - 2026-08-20

### Stable Release - Full Multi-Platform & Performance Hardening
- **Adattamento Fullscreen / Responsive per Telegram Web (`Scripts/telegram.css`, `JsScripts.vb`, `AppAccounts.vb`)**:
  - Risolto il problema per cui Telegram Web non sfruttava l'intera larghezza dello schermo: introdotto il foglio di stile dedicato `telegram.css` che rimuove i vincoli di larghezza (`--columns-width`, `--chat-width`, `max-width: none`) sui contenitori principali (`#column-center`, `.chat`, `.chat-container`, `.chat-layout`, `#MiddleColumn`, `.messages-layout`, `.bubbles-inner`).
  - Iniezione automatica e persistente al caricamento della pagina (`TelegramInitJS`) e sincronizzazione con i temi chiaro/scuro.
- **Passaggio Istantaneo tra Account & Pre-caricamento Multi-Account (`MainWindow.xaml.vb`)**:
  - Pre-caricamento in memoria di tutte le istanze WebView2 configurate: passaggio immediato tra schede WhatsApp e Telegram a zero latenza.
- **Ottimizzazione Cancellazione Profili WebView2 (TODO #37) (`AccountManager.vb`)**:
  - Implementato `DeleteDirectoryWithRetryAsync` con backoff progressivo per garantire l'eliminazione affidabile delle cartelle di profilo orfane o rimosse anche in presenza di lock transitori.
- **Hardening Escaping JavaScript via `ExecuteScriptAsync` (TODO #38) (`AppAccounts.vb`, `MainWindow.xaml.vb`)**:
  - Tutti i parametri e identificatori interpolati negli script JavaScript vengono ora serializzati in modo deterministico e sicuro tramite `JsonSerializer.Serialize`.
- **Integrazione Taskbar & Gestione Finestra Win32 (`MainWindow.xaml`, `MainWindow.xaml.vb`)**:
  - Risolto l'ingrandimento della finestra tramite hook Win32 `WM_GETMINMAXINFO` preservando la barra delle applicazioni di Windows.
- **Notifiche & Ricezione in Background Continua (`AppAccounts.vb`, `notification.js`)**:
  - Disattivato il throttling dei timer e il backgrounding occluso per assicurare ricezione istantanea dei messaggi e notifiche toast/popup su tutti gli account.

## [0.5.2-beta] - 2026-08-20

### Fixed & Improved
- **Fix Adattamento Fullscreen / Responsive per Telegram Web (`Scripts/telegram.css`, `JsScripts.vb`, `AppAccounts.vb`)**:
  - Risolto il problema di mancato adattamento a schermo intero di Telegram Web: creato il foglio di stile dedicato `telegram.css` che rimuove i vincoli di larghezza fissa (`--columns-width`, `--chat-width`, `max-width`) sui contenitori principali (`#column-center`, `.chat`, `.chat-container`, `.chat-layout`, `#MiddleColumn`, `.messages-layout`, `.bubbles-inner`).
  - Iniezione automatica e persistente al caricamento del documento (`TelegramInitJS`) e sincronizzazione con i temi chiaro/scuro.
- **Ottimizzazione Cancellazione Profili WebView2 (TODO #37) (`AccountManager.vb`)**:
  - Implementato `DeleteDirectoryWithRetryAsync` con backoff progressivo per garantire l'eliminazione affidabile delle cartelle di profilo orfane o rimosse anche in presenza di lock transitori da parte di processi Chromium o antivirus.
- **Hardening Escaping JavaScript via `ExecuteScriptAsync` (TODO #38) (`AppAccounts.vb`, `MainWindow.xaml.vb`)**:
  - Tutti i parametri e identificatori interpolati negli script JavaScript vengono ora serializzati in modo deterministico e sicuro tramite `JsonSerializer.Serialize`, prevenendo injection di caratteri speciali o apici.

## [0.5.1-beta] - 2026-08-19

### Fixed & Improved
- **Ripristino Visibilità Barra di Windows all'Ingrandimento (`MainWindow.xaml`, `MainWindow.xaml.vb`)**:
  - Implementato l'aggancio Win32 (`HwndSourceHook`) per il messaggio `WM_GETMINMAXINFO` limitando l'ingrandimento della finestra all'Area di Lavoro (`rcWork`) dello schermo.
  - La barra delle applicazioni di Windows (Taskbar) resta sempre visibile e accessibile anche a finestra ingrandita a schermo intero.
  - Adattamento dinamico di bordi (`BorderThickness`), angoli arrotondati (`CornerRadius`) e ombreggiatura esterna (`DropShadowEffect`) al cambio di stato (ingrandita/normale).
  - Aggiunto il ridimensionamento interattivo dal grip in basso a destra e il trascinamento fluido per ripristinare la finestra dallo stato ingrandito.
- **Passaggio Istantaneo tra Account & Pre-caricamento Multi-Account (`MainWindow.xaml.vb`)**:
  - All'avvio dell'applicazione vengono ora istanziati e pre-caricati i controlli WebView2 per tutti gli account configurati in parallelo.
  - Al cambio scheda, le viste inattive rimangono attive e in memoria (`Visibility.Hidden`), eliminando completamente i tempi di attesa e i ricaricamenti da zero ad ogni passaggio tra account WhatsApp e Telegram.
- **Notifiche & Ricezione Messaggi in Background (`WhatsAppAccount.vb`, `Scripts/notification.js`)**:
  - Rimossi i parametri di disattivazione del networking in background (`--disable-background-networking`) e abilitati i flag di continuità operativa di Chromium (`--disable-background-timer-throttling`, `--disable-backgrounding-occluded-windows`, `--disable-renderer-backgrounding`).
  - Gli account in secondo piano mantengono costantemente attiva la connessione WebSocket e l'esecuzione dei timer JavaScript, garantendo la ricezione istantanea dei messaggi anche mentre si utilizza un altro account o la finestra è ridotta a icona.
  - Rafforzato lo script `notification.js` con l'override dei permessi via `navigator.permissions.query`, getter forzato su `Notification.permission` e monitoraggio dinamico delle modifiche al titolo della pagina (`MutationObserver`).

## [0.5.0] - 2026-08-18

### Multi-Platform & Rebranding Major Release
- **Rebranding Ufficiale in HidaChat**:
  - Evoluzione dell'applicazione da client esclusivo per WhatsApp a client multi-piattaforma unificato (**HidaChat**).
  - Ridenominazione completa di soluzione, progetti, namespace ed eseguibili.
- **Supporto Multi-Account per WhatsApp e Telegram**:
  - Possibilità di gestire contemporaneamente account WhatsApp e Telegram in schede separate con profili isolati.
  - Selezione della piattaforma in fase di creazione e configurazione dell'account nelle Impostazioni.
  - Icone vettoriali dedicate (verde per WhatsApp, azzurro per Telegram) nelle schede in alto e nella gestione account.
- **Sincronizzazione Tema Scuro/Chiaro Multi-Piattaforma (`JsScripts.vb`, `WhatsAppAccount.vb`, `MainWindow.xaml.vb`)**:
  - Introdotti gli script dedicati `TelegramDarkModeJS` e `TelegramLightModeJS` in `ThemeJsScripts` per la sincronizzazione dinamica del tema di sistema/WPF con l'interfaccia di Telegram Web (`web.telegram.org/k/`).
  - Gestione della classe `.night` su `document.documentElement` e `body`, aggiornamento delle chiavi `tt-theme` e `theme` in `localStorage` ed integrazione con `themeController.setTheme`.
  - Applicazione mirata e differenziata degli stili in base al tipo di piattaforma (`IsTelegram` / `IsWhatsApp`) sia all'avvio/navigazione (`NavigationCompleted`) sia al cambio tema in tempo reale nelle Impostazioni (`ApplyWpfThemeAsync`).
- **Motore di Traduzione Messaggi Multi-Piattaforma (`translation.js`)**:
  - Esteso `getMessageText` e i listener per riconoscere sia i contenitori WhatsApp (`[data-testid="msg-container"]`, `.selectable-text`, `.quoted-message`) sia gli elementi nativi di Telegram Web K (`.bubble`, `.message`, `.text-content`, `.reply`, `.reply-content`).
  - Ottimizzato il posizionamento del pulsante hover di traduzione per le bolle in uscita/entrata (`.is-out`, `.is-in`) e adattato il colore di accento (`#24A1DE` su Telegram, `#00a884` su WhatsApp).
  - Pieno supporto al rilevamento del tema scuro per entrambe le piattaforme per la bolla di traduzione e supporto integrato alla traduzione dell'intera pagina (`scanAndTranslateDOM`).
- **Miglioramento Routing Notifiche Popup (`MessagePopup.xaml.vb`)**:
  - Al click sul popup visivo di notifica, l'applicazione ora seleziona automaticamente la scheda dell'account che ha ricevuto il messaggio (`SwitchToAccountAsync`), garantendo coerenza con il comportamento delle notifiche Toast di Windows.
- **Rifinitura Localizzazione (`Localization.vb`)**:
  - Aggiornate le etichette per riflettere in modo coerente e uniforme la natura multi-piattaforma dell'applicazione.
- **Nuovo Design Visivo Cyberpunk**:
  - Nuova icona applicativa in stile cyberpunk neon con fumetto di chat olografico e circuiti digitali.
  - Nuova icona dedicata per la System Tray con badge luminoso per le notifiche attive.
- **Architettura & Portabilità**:
  - Isolamento completo di tutti i file dati, impostazioni e profili WebView2 all'interno della cartella portabile `data/`.
  - Transizione fluida e forwarder per l'aggiornamento automatico dalle versioni legacy.

## [0.4.2-beta] - 2026-08-18

### Multi-Platform (WhatsApp & Telegram)
- **Supporto Account WhatsApp & Telegram**:
  - Aggiunta la selezione del tipo di piattaforma (`WhatsApp` o `Telegram`) durante la creazione e la modifica degli account nelle Impostazioni.
  - Visualizzazione delle icone vettoriali dedicate (verde per WhatsApp, azzurro per Telegram) nelle schede account della barra principale e nella lista di gestione account.
  - Menu rapido contestuale sul pulsante `+` e su *"Aggiungi account"* per selezionare istantaneamente la piattaforma desiderata.
  - Routing automatico dell'URL specifico (`https://web.whatsapp.com/` e `https://web.telegram.org/k/`) con profili isolati, gestione notifiche e supporto traduzioni.

## [0.4.1-beta] - 2026-08-18

### Visual & Assets
- **Nuova Icona Cyberpunk & Notifiche System Tray**:
  - Introdotto il nuovo set di icone in stile cyberpunk con fumetto di chat olografico al neon (ciano/magenta) e tracciati digitali su sfondo dark.
  - Generati file `.ico` multi-risoluzione completi (da 16x16 a 256x256) per l'applicazione (`icon.ico`) e per le notifiche della System Tray con badge luminoso (`icon_notification.ico`).
  - Integrazione dell'eseguibile ponte e routine di pulizia automatica per la transizione fluida degli aggiornamenti dalle versioni precedenti.

## [0.4.0-beta] - 2026-08-18

### Rebranding & Multi-Platform Evolution
- **Rebranding Completo da WhatsAppH a HidaChat**:
  - Ridenominazione dell'intero progetto, soluzione .NET (`HidaChat.sln`), file di progetto (`HidaChat.vbproj`) ed eseguibile (`HidaChat.exe`) in vista dell'evoluzione a client multi-piattaforma (WhatsApp, Telegram, Teams, ecc.).
  - Aggiornati i namespace XML e code-behind in `HidaChat`.
  - Aggiornato il Mutex di istanza singola a `Local\HidaChat_SingleInstance_Mutex`.
  - Aggiornati tutti gli endpoint di aggiornamento automatico OTA e i link al nuovo repository ufficiale GitHub `https://github.com/hidaba/HidaChat`.
  - Aggiornati i workflow di GitHub Actions, lo script di pubblicazione `publish.ps1`, i template di issue e la documentazione completa.

## [0.3.3-beta] - 2026-08-18

### Portability & Refactoring
- **Punto 36 TODO — Spostamento `settings.json` e `translations_cache.json` in `data/` (`SettingsController.vb`, `Localization.vb`)**:
  - I file di configurazione (`settings.json`) e della cache traduzioni (`translations_cache.json`) vengono ora gestiti e salvati direttamente all'interno della cartella portabile `data/`.
  - Implementata la migrazione automatica e trasparente dei file esistenti nella cartella principale (`File.Move`) verso `data/` al primo avvio, garantendo la compatibilità con le installazioni precedenti e mantenendo pulita la radice dell'applicazione.
  - Aggiunta la creazione preventiva della directory di destinazione `data/` in caso di prima installazione portabile pulita.

## [0.3.2-beta] - 2026-08-17

### Performance & Memory
- **Punto 35 TODO — Caricamento Lazy degli Script JS/CSS (`JsScripts.vb`, `Scripts/`)**:
  - Estratti gli script JavaScript e CSS in file dedicati incorporati nell'assembly (`EmbeddedResource`): `theme.css`, `notification.js` e `translation.js`.
  - Riscritto `JsScripts.vb` per caricare i template in modalità `Lazy(Of String)` ed effettuare sostituzioni in-place con `StringBuilder`, eliminando tutte le costanti XML literals statiche permanenti in RAM.
- **Persistenza Traduzioni su File (`translations_cache.json`)**:
  - Implementato `TranslationCacheService` per archiviare in modo permanente su disco (`data/translations_cache.json` con fallback in root) sia le traduzioni dell'interfaccia utente sia i testi/messaggi tradotti delle chat.
  - Aggiunta la consultazione preventiva della cache prima di ogni richiesta di traduzione (singola o batch): i testi già tradotti vengono restituiti istantaneamente dalla cache senza effettuare chiamate di rete a Google Translate.
  - Nelle traduzioni batch dell'intera pagina (`translatePage`), solo le nuove frasi non ancora presenti in cache vengono inviate a Google Translate, riducendo drasticamente il consumo di banda.
- **Ottimizzazione Memoria RAM per Traduzioni**:
  - In memoria RAM vengono mantenuti **esclusivamente** i dizionari della lingua attualmente impostata.
  - Al cambio lingua in `SettingsController`, i dati della lingua precedente vengono salvati su disco e rimossi dalla memoria per essere raccolti dal GC, caricando in RAM solo la nuova lingua selezionata.

## [0.3.1-beta] - 2026-08-15

### Performance & Memory
- **Punto 10 & 32 TODO — Ottimizzazione e WeakReference nei Popup di Notifica (`MessagePopup.xaml.vb`)**:
  - Implementato `WeakReference(Of MessagePopup)` nella lista statica dei popup attivi (`_activePopups`) con eliminazione automatica dei riferimenti raccolti dal GC, prevenendo memory leak in caso di popup non chiusi normalmente.
  - Sostituito il ricalcolo forzato e riposizionamento di tutti i popup attivi con il calcolo incrementale della posizione Y (`PositionNewPopup()`), impilando le nuove notifiche verso l'alto senza riposizionare o muovere i popup già visibili a schermo.
- **Punto 11 TODO — Ottimizzazione Cambio Account (`MainWindow.xaml.vb`)**:
  - Ottimizzato `SwitchToAccountAsync()` gestendo il ritorno anticipato se l'account richiesto è già attivo e nascondendo unicamente il controllo `WebView` del precedente account senza iterare l'intera gerarchia dei controlli.

## [0.3.0] - 2026-08-13

### Added & Features
- **Supporto Multi-Account fino a 3 Account**: Gestione integrata e schede veloci per il cambio e aggiunta account in tempo reale (`MainWindow.xaml`, `AccountManager.vb`).
- **Finestra Informazioni (AboutWindow)**: Nuova interfaccia modale con dettagli versione, licenza Apache-2.0, percorso portabile e collegamenti rapidi (`AboutWindow.xaml`).
- **Pulizia Automatica Profili Orfani**: Scansione ed eliminazione all'avvio delle directory profilate non più utilizzate (`AccountManager.vb`).
- **Documentazione Bilingue & Standard Community**: Integrazione di `README.md` (EN) e `README.it.md` (IT), `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md` e workflow GitHub Actions per la build automatica.

### Fixed & Improved
- **Robustezza Aggiornamenti OTA**: Gestione corretta dei suffissi di pre-release (`-beta`, `-rc`) e verifica stabilità nel confronto versioni (`UpdateChecker.vb`).
- **Gestione Notifiche Toast**: Risolti crash e comportamenti anomali al click sulle notifiche di Windows (`MainWindow.xaml.vb`).
- **Performance Pennelli WPF**: Caching ad alte prestazioni tramite il modulo `BrushCache.vb`.
- **Rinominazione Account in Tempo Reale**: Le modifiche ai nomi degli account nelle impostazioni si riflettono istantaneamente sull'interfaccia.

## [0.2.5-beta] - 2026-08-10

### Fixed
- **Fix Confronto Versione OTA e Pre-release (`UpdateChecker.vb`)**: Perfezionato il confronto versioni in `IsNewerVersion` separando le componenti numeriche base dai suffissi di pre-release (es. `-beta`, `-rc1`). Permette di rilevare correttamente gli aggiornamenti beta rispetto alle versioni precedenti.

## [0.2.4-beta] - 2026-08-08

### Fixed
- **Pulizia Profili WebView2 Orfani (`AccountManager.vb`)**: Implementato il metodo `CleanupUnusedProfilesAsync` per scansionare la cartella `data/webview` ed eliminare automaticamente all'avvio le cartelle di profilo orfane (`WV2Profile_*`) i cui ID non sono più associati ad alcun account attivo.
- **Correzione Badge e Link (`README.md`, `README.it.md`)**: Corretti i badge di build ed i link commit per puntare al branch `master` ed eliminate le istruzioni di build contenenti percorsi non validi.

## [0.2.3-beta] - 2026-08-07

### Fixed
- **Fix Crash Notifiche Toast (MainWindow.xaml.vb)**: Risolto NullReferenceException al click su notifiche Toast di Windows con argomenti mancanti o malformati mediante l'helper `ExtractArg`.
- **Fix Click Notifiche Toast (MainWindow.xaml.vb, MessagePopup.xaml.vb)**: Sostituita la chiamata a `ToggleWindow()` con `ShowWindow()` nel gestore notifiche Toast per evitare che un click nasconda la finestra se già visibile in primo piano.

## [0.2.2-beta] - 2026-08-06

### Added
- **Supporto Multi-Account fino a 3 Account WhatsApp**:
  - Aggiunto il pulsante `+` nella barra orizzontale delle schede in `MainWindow.xaml` per la creazione immediata ed il passaggio al nuovo account.
  - Aggiunto il controllo ed il contatore di account (`Account configurati: X su 3`) nella finestra `SettingsWindow.xaml`.
  - Implementata la verifica ed il blocco rigido del limite massimo in `AccountManager.vb` (`MaxAccounts = 3`, `CanAddAccount`).
  - Isolamento completo delle sessioni WhatsApp Web e dei dati di profilo WebView2 su disco (`WV2Profile_<id>`).
  - Aggiunte le stringhe di localizzazione multilingua per i limiti e le azioni di gestione account.
- **Finestra "Informazioni" (AboutWindow)**:
  - Creata la finestra modale `AboutWindow.xaml` accessibile tramite il nuovo pulsante `ⓘ` nella barra del titolo, dalle Impostazioni e dal menu contestuale della tray icon.
  - Mostra l'autore (`Massimo Balestrieri`), la versione (`v0.2.2`), la data di rilascio (`2026-08-06`), la licenza (`Apache-2.0`), il runtime (`.NET 9` + `WebView2`), il percorso dati portabile (`data/webview`) ed i link diretti a GitHub, Releases e segnalazione bug.

### Performance & Refactored
- **Punto 29 TODO (`AccountManager.vb`)**: Eliminata la proiezione con tipi anonimi a ogni salvataggio delle impostazioni. Serializzazione diretta della collezione `_accounts` per ridurre la pressione sul Garbage Collector.
- **Punto 7 TODO (`TODO.md`)**: Chiuso e contrassegnato come obsoleto il punto 7 sull'aggiornamento OTA tramite percorso di rete UNC (`ReadVersionFromFileAsync()`), in quanto il percorso di rete è stato dismesso ed interamente sostituito dalle API REST di GitHub Releases fin dalla v0.2.0.

### Fixed
- **Aggiornamento Nome Account in Tempo Reale nelle Schede (`WhatsAppAccount.vb`)**: Aggiunto il sollevamento dell'evento `PropertyChanged` per la proprietà `Name` in `WhatsAppAccount.vb`. La rinomina degli account nelle Impostazioni viene ora riflessa istantaneamente sui pulsanti delle schede nella finestra principale senza dover riavviare o riaprire l'applicazione.

## [0.2.1-beta] - 2026-08-06

### Added & Refactored
- **Miglioramento Visibilità & Sicurezza Repository**: Rimossi i riferimenti all'IP e percorso di rete legacy in `ANALISI_PROGETTO.md`. Allineato il distintivo ed il testo della licenza ad Apache 2.0.
- **Documentazione Bilingue & Asset Grafici**: Aggiunti i file `README.md` (inglese) e `README.it.md` (italiano) con selettore di lingua, tabella comparativa "Why HidaChat", galleria di screenshot (`images/`) e requisiti.
- **Standard Community & CI/CD**: Introdotti `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md` (v2.1), `SECURITY.md`, template per segnalazione bug e feature request, e workflow GitHub Actions `.github/workflows/build.yml` per la compilazione automatica `.NET 9`.

## [0.2.0] - 2026-08-05

### Changed & Refactored
- **Centralizzazione Aggiornamenti OTA su GitHub Releases**: Rimossa la dipendenza e la logica di fallback sul percorso di rete locale dismesso (`UpdateFilesPath`). Il sistema di aggiornamento automatico OTA opera ora esclusivamente tramite le API REST di GitHub Releases con verifica, download ed installazione in background dello ZIP.
- **Ottimizzazione Canale Beta (Pre-releases)**: Perfezionata la scansione delle release nel canale Beta (`UseBetaChannel`) in `UpdateChecker.vb` con iterazione nell'elenco release per selezionare il primo asset ZIP disponibile.
- **Automazione Script di Pubblicazione**: Aggiornato `publish.ps1` per supportare la compilazione, creazione pacchetto ZIP e pubblicazione automatica di release stabili e beta (con flag `-Beta` e `--prerelease` su GitHub).
- **Esclusione File Regole AI da Repository**: Aggiunte regole in `.gitignore` per escludere `AGENTS.md` e `.agents/` dal tracciamento Git pubblico, preservando la memoria e le linee guida del progetto in locale.

## [0.1.8-beta] - 2026-07-31

### Performance & Portability
- **Ottimizzazione I/O disco e avvio Chromium portabile**: Configurate le opzioni di ambiente `AdditionalBrowserArguments` per WebView2 in `WhatsAppAccount.vb` (`--disk-cache-size=104857600 --media-cache-size=52428800 --disable-background-networking --disable-features=Translate,OptimizationHints,MediaRouter`). Limita la proliferazione di file sciolti di cache mantenendo l'applicazione al 100% portabile nella cartella `data/webview`.

## [0.1.7-beta] - 2026-07-29

### Fixed
- **Fix Blocco Aggiornamento OTA e Mutex Istanza Singola**: Risolto il problema del blocco dell'applicazione al riavvio durante l'aggiornamento OTA e l'impossibilità di riaprire l'app dopo una chiusura forzata (richiedeva il riavvio del PC). Implementata la gestione di `AbandonedMutexException` e il ripiegamento sul controllo dei processi attivi in `Application.xaml.vb`, l'inclusione di `taskkill` per il processo bloccato e la rimozione di `pause` in `update.bat`, ed il corretto rilascio del Mutex dell'istanza singola e dei controlli WebView2 in `ForceExitForUpdate()`.

## [0.1.6-beta] - 2026-07-29

### Performance
- **Punto 34 TODO**: Implementato il modulo `BrushCache` con `ConcurrentDictionary` e congelamento `.Freeze()` per il caching thread-safe e ad alte prestazioni delle istanze `SolidColorBrush`. Eliminata la riallocazione continua di pennelli WPF a ogni cambio tema o aggiustamento grafico in `MainWindow.xaml.vb` e `SettingsWindow.xaml.vb`.

## [0.1.5-beta] - 2026-07-28

### Fixed & Performance
- **Punto 31 TODO**: Utilizzato `StringBuilder` in `UpdateChecker.vb` per la composizione dello script batch di aggiornamento `update.bat`.
- **Punto 33 TODO**: Centralizzato il rilevamento del tema di sistema Windows tramite il nuovo modulo `SystemThemeHelper` con caching della chiave `AppsUseLightTheme` ed ascolto dinamico dell'evento `SystemEvents.UserPreferenceChanged`. Eliminata la triplicazione delle letture dal Registro in `MainWindow.xaml.vb`, `WhatsAppAccount.vb` e `SettingsWindow.xaml.vb`.

## [0.1.4-beta] - 2026-07-28


### Changed
- **Formattazione Messaggi di Commit Git**: Rimozione del prefisso di versione dai messaggi di commit di Git su GitHub per mantenere pulite le descrizioni dei file.

## [0.1.3] - 2026-07-28


### Added
- **Pulsante e sezione Download in README.md**: Aggiunta la sezione con i link diretti ed il badge per scaricare l'eseguibile portatile per Windows dalle GitHub Releases.

### Fixed & Performance
- **Punto 30 TODO**: Sostituito `Directory.GetDirectories` con `Directory.EnumerateDirectories` e `.FirstOrDefault()` in `AccountManager.vb` per la ricerca in streaming lazy del profilo orfano.

## [0.1.2-beta] - 2026-07-28


### Added
- Aggiunta documentazione XML doc (`<summary>`, `<param>`, `<returns>`) e commenti esplicativi in italiano in tutti i file di codice sorgente VB.NET del progetto (`AccountManager`, `WhatsAppAccount`, `MainWindow`, `SettingsController`, `SettingsWindow`, `UpdateChecker`, `Localization`, `JsScripts`, `MessagePopup`, `Application`, `Constants`).

### Fixed & Refactored
- **Punto 25 TODO**: Riconvertito `ApplyWpfTheme` in funzione asincrona (`ApplyWpfThemeAsync() As Task`) aggiungendo `Await` e gestione eccezioni `Try...Catch` su tutti i richiami `ExecuteScriptAsync` in `MainWindow.xaml.vb`.

## [0.1.1-beta] - 2026-07-27


### Removed
- Eliminata completamente l'immagine `annoni.png` dal repository per alleggerire il pacchetto compilato.
- Configurato il rilascio OTA su GitHub Releases.

## [0.1.0] - 2026-07-27

### Changed
- Rimosso il logo superiore dalla barra del titolo in `MainWindow.xaml`.
- Rimosso l'intero sistema di backup e sincronizzazione chat (`ChatSyncBackgroundWorker`, `ChatJsonStorageService`, `CryptoHelper`, costanti e script JS dedicati).
- Aggiornata la versione dell'applicazione a `0.1.0`.

## [0.0.58] - 2026-07-24

### Performance
- **Ottimizzazione `GetTranslationJS` in `JsScripts.vb`**: sostituita la catena di 12 chiamate `.Replace()` su stringa in-memory con `StringBuilder.Replace()` in-place per la generazione dei JS di traduzione, eliminando allocazioni intermedie nella Large Object Heap (LOH).

## [0.0.57] - 2026-07-24

### Fixed
- **Deduplicazione righe nel file di backup**:
  - Implementata la deduplicazione automatica dei messaggi per `id` univoco in `ChatJsonStorageService.vb`. Ogni messaggio viene salvato esattamente una volta sola, eliminando righe duplicate nel file di backup.
- **Estrazione mittenti nei gruppi e numeri di telefono**:
  - Risolto un problema per cui nei gruppi venivano salvati i messaggi con il nome del gruppo al posto del nome della persona.
  - Implementata la lettura di `data-pre-plain-text` e dei nodi mittente (`span._ao3e`, `span._ap4a`) nelle bolle del DOM per estrarre l'autore del messaggio nei gruppi (es. `Timmití`, `Marco`, ecc.).
  - Migliorata la parsificazione dei numeri di telefono dai JID `data-id` (`..._393...@c.us`) e da `msg.author` per popolare sempre il campo `senderPhone`.

## [0.0.56] - 2026-07-24

### Fixed
- **Estrazione nome contatto e numero di telefono nei backup**:
  - Risolto un bug nel fallback DOM di `extractMessagesFromDOMAsync()` che catturava erroneamente il titolo del pannello laterale `"Dettagli profilo"` come nome della chat, impostando `senderPhone` vuoto.
  - Corretto il selettore dell'header per puntare rigorosamente a `#main header` filtrando le etichette di sistema.
  - Implementata l'estrazione automatica del numero di telefono dal `chatId` (`39...` da `@c.us`) e del nome dal mittente/contatto in JS sia per Store che per fallback DOM.
  - Aggiunto un arricchimento di sicurezza server-side in `ChatJsonStorageService.vb` per garantire che `senderPhone` e `senderName` siano sempre valorizzati con i dati reali del contatto in ogni riga JSON del backup.

## [0.0.55] - 2026-07-24

### Performance
- **Enumerazione lazy in `UpdateChecker.vb`**: sostituito `Directory.GetFiles` con `Directory.EnumerateFiles` per la copia dei file dall'OTA repository. L'enumerazione in streaming evita l'allocazione eager di grandi array di stringhe in memoria su condivisioni di rete UNC.

## [0.0.54] - 2026-07-24

### Added
- **Opzione cifratura backup in `Constants.vb`**: aggiunta la costante `EnableBackupEncryption As Boolean` (impostata di default a `True`).
  - Se `True`: il backup salvato su disco viene cifrato in formato AES-256-GCM (nomi file e contenuti JSON cifrati).
  - Se `False`: i file di backup e l'indice delle chat vengono salvati in chiaro (formato JSON standard leggibile nella cartella `Chats_Plain`).

## [0.0.53] - 2026-07-24

### Fixed & Performance (Memoria & Clean-up)
- **IDisposable su WhatsAppAccount**: gestito il ciclo di vita del WebView2 e delle sue dipendenze alla rimozione degli account.
- **Unsubscribe handler CoreWebView2**: salvati e rimossi con `RemoveHandler` gli handler `PermissionRequested`, `NewWindowRequested`, `WebMessageReceived` e `NavigationCompleted` alla distruzione dell'account per prevenire memory leak.
- **IDisposable su Storage e SyncWorker**: implementato `IDisposable` su `ChatJsonStorageService` e `ChatSyncBackgroundWorker` rilasciando la risorsa OS `SemaphoreSlim` (`_fileLock`).
- **Async/Await in SyncWorker**: eliminato `.GetAwaiter().GetResult()` in `ProcessIncomingBatchAsync`, rendendo l'elaborazione dei batch di messaggi totalmente asincrona ed evitando la starvation del `ThreadPool`.
- **Limite notifiche attive**: introdotto limite massimo (500 elementi) per `ActiveNotificationIds` in `WhatsAppAccount` con svuotamento automatico.
- **ArrayPool & Streaming in CryptoHelper**: impiegato `ArrayPool(Of Byte).Shared` per buffer temporanei in `EncryptBytes`/`DecryptBytes` e implementata la cifratura in streaming a blocchi per file multimediali grandi.
- **Shared HttpClient in Localization**: unificata la gestione di `HttpClient` in una singola istanza condivisa per prevenire socket exhaustion.
- **ObservableCollection per Accounts**: sostituita `List(Of WhatsAppAccount)` con `ObservableCollection` per evitare la rigenerazione forzata dei DataTemplate WPF alla rimozione di un account.
- **Dirty Tracking**: aggiunto flag `_isDirty` in `AccountManager` ed eliminati salvataggi su disco se i dati non sono modificati.
- **Lifecycle MainWindow & Timer**: il timer di sincronizzazione background `_syncTimer` e gli event handler `PropertyChanged` vengono disiscritti e fermati correttamente all'uscita dall'applicazione.

## [0.0.52] - 2026-07-21

### Performance
- **Chunking adattivo traduzioni**: sostituito il chunking fisso a 50 testi con chunking adattivo basato su max 2000 caratteri per chunk in `scanAndTranslateDOM()` (TranslationEngineJS). I chunk vengono formati accumulando testi fino al raggiungimento del limite di caratteri, evitando chunk sovradimensionati che potrebbero causare errori HTTP 414.

## [0.0.51] - 2026-07-21

### Performance
- **Script injection WebView2**: `ChatSyncJS` spostato da `NavigationCompleted` (re-iniettato a ogni navigazione) a `AddScriptToExecuteOnDocumentCreatedAsync` (eseguito automaticamente su ogni nuovo documento prima del caricamento pagina).
- **Riduzione lavoro in `NavigationCompleted`**: non re-inietta più il chat sync JS a ogni navigazione, sfruttando l'idempotency guard (`__chatSyncInstalled`) e l'iniezione automatica via document creation.

## [0.0.50] - 2026-07-21

### Performance
- **SettingsWindow**: memoizzati i risultati di `FindLogicalChildren` / `FindVisualChildren` in cache (`_cachedCheckBoxes`, `_cachedTextBlocks`, `_cachedAccountBorders`, `_cachedAccountTextBoxes`). La ricorsione del visual/logical tree avviene una sola volta per tipo di controllo invece che a ogni cambio tema.
- Introdotto helper `GetCachedLogicalChildren(Of T)` per lazy caching con invalidazione esplicita quando `AccountsList.ItemsSource` cambia.

## [0.0.49] - 2026-07-21

### Performance
- **Lazy initialization WebView2**: i WebView2 non vengono più creati per tutti gli account all'avvio, ma solo per l'account attivo. Gli altri vengono creati e inizializzati on-demand al primo cambio account. Risparmio di ~50-100MB di RAM per account inattivo.
- Rimosso `Imports Microsoft.Web.WebView2.Wpf` da `AccountManager.vb` (non crea più WebView2).

### Fixed
- `ApplyWpfTheme`, `BtnReloadActiveTab_Click`, toast handler: aggiunti null check per `acc.WebView` / `activeAcc.WebView` (causavano NullReferenceException con la lazy init, chiudendo l'app dopo pochi secondi).
- `SettingsWindow.BtnDevTools_Click`: aggiunto null check per `activeAcc.WebView`.
- `AccountManager.RemoveAccountAsync`: aggiunto null check prima di `WebView.Dispose()`.

## [0.0.48] - 2026-07-21

### Performance
- `UpdateAccountTabStyling()` eliminato: lo stile attivo/inattivo degli account è ora gestito da un `DataTrigger` WPF nel `AccountTabButtonStyle` che reagisce automaticamente a `IsActive` di `WhatsAppAccount`. Rimossi `FindVisualChildren`/`FindVisualChild` da MainWindow.
- `WhatsAppAccount.IsActive` ora implementa `INotifyPropertyChanged` per abilitare il binding reattivo.
- **Impatto**: eliminata ricorsione superflua del visual tree a ogni cambio account.

## [0.0.47] - 2026-07-21

### Performance
- `FetchTranslations()`: tradotte tutte le chiavi UI in parallelo (`Task.WhenAll`) invece di 30+ richieste HTTP seriali.
- **Impatto**: riduzione del tempo di traduzione da ~N round-trip a ~1 round-trip.

### Removed
- Nascosto il pulsante "Add Account" dalla barra degli account.

## [0.0.46] - 2026-07-21

### Release
- Prima release formale stabile.
- Rilasciata su GitHub con tag `v0.0.46`.
- Pubblicata OTA.

## [0.0.45] - 2026-07-20

### Performance
- Ottimizzato I/O su `settings.json`: le modifiche alle impostazioni vengono accumulate in cache in memoria e scritte su disco con debounce di 500ms, eliminando letture ridondanti del file.
- `SaveSettingAsync`, `SaveThemeAsync`, `UpdateLanguageAsync` ora modificano la cache in memoria senza rileggere il file da disco.

## [0.0.44] - 2026-07-20

### Fixed
- `MigrateOrphanProfile` ora elimina il profilo stale `WV2Profile_{Id}` se esiste già, poi rinomina l'orfano `WV2Profile_` con la sessione più recente.
- `SetupWebViewAsync`: recupero orfano eseguito anche quando la destinazione esiste già (sostituisce lo stale).
- Quando sia `WV2Profile_` che `WV2Profile_{Id}` esistono, il profilo stale viene eliminato e sostituito con `WV2Profile_` per preservare la sessione WhatsApp più recente.

## [0.0.43] - 2026-07-20

### Fixed
- Aggiunto recupero fallback del profilo WebView2 orfano (`WV2Profile_`) direttamente in `SetupWebViewAsync` come ultima difesa prima di creare un nuovo profilo.
- Aggiunto logging diagnostico in `LoadAccountsAsync`, `MigrateOrphanProfile` e `SetupWebViewAsync` per tracciare la migrazione del profilo WebView2.

## [0.0.42] - 2026-07-20

### Fixed
- Risolto il problema del testo di versione obsoleto (v0.0.27) mostrato nel titolo durante il caricamento iniziale: rimosso il testo hardcoded in XAML e impostata l'inzializzazione immediata nel costruttore `Sub New()` della finestra principale.

## [0.0.41] - 2026-07-20

### Changed
- Rimozione del backup dei media e focalizzazione esclusiva sul backup delle chat di testo.
- Implementata la rotazione periodica dei file di backup delle chat ogni X giorni configurata tramite la costante in fase di compilazione `BackupRotationDays`.
- Applicata la cifratura deterministica AES-256 dei nomi di file conservati nella cartella `Chats_Encrypted`.

## [0.0.40] - 2026-07-18

### Fixed
- Risolta l'origine precisa delle dimensioni anomale dei file multimediali (1 KB, 368 KB, 490 KB):
  - **1 KB**: Causato dal tentativo di scaricare direttamente gli URL HTTPS remoti criptati di WhatsApp (`mmg.whatsapp.net`), che restituiscono HTTP 403 Forbidden. Impedita la `fetch` diretta degli URL `http/https` remoti.
  - **368 KB / 490 KB**: Causato dal download dei soli Blob di anteprima `renderableUrl`.
  - Integrata l'estrazione dai soli oggetti `Blob` decifrati in memoria (`msg.mediaData.blob`, `msg.mediaObject._blob`, `msg.file`) o tramite l'invocazione di `DownloadManager.downloadAndSave(msg)` nativo di WhatsApp Web per ottenere il file binario originale intero.

## [0.0.39] - 2026-07-18

### Fixed
- Risolto in modo definitivo il troncamento a 291 KB dei file multimediali:
  - Implementata la funzione `getMediaDataForContainer` che interroga `Store.Msg.get(id)` con l'ID del messaggio DOM per recuperare il binario video/audio/documento originale `mediaData.blob`.
  - In caso di fallback DOM, prioritizzata la scansione dei tag `<video>`, `<audio>` e collegamenti `<a>` di download rispetto alle immagini poster di anteprima (`<img>`).

## [0.0.38] - 2026-07-18

### Fixed
- Risolto il troncamento dei file video e media (precedentemente salvati tutti a 291 KB):
  - Aggiunta la funzione `getMediaBlobFromMsg(msg)` in JavaScript per accedere direttamente all'oggetto `msg.mediaData.blob` o invocare `msg.downloadMedia()` interno a WhatsApp Web.
  - Sostituita l'estrazione della sola miniatura preview/poster JPEG dal DOM con lo scaricamento del file video/audio/documento binario originale completo senza limiti di dimensione.

## [0.0.37] - 2026-07-18

### Fixed
- Risolto problema file media salvati a 1KB:
  - Sostituita la semplice stringa dell'URL `blob:` con la conversione asincrona `fetchBlobAsBase64` in JavaScript per scaricare ed inviare a VB.NET l'intero payload binario Base64 originale di immagini, audio e video.
  - Aggiunta la validazione `rawMediaBytes.Length > 0` prima di scrivere il file cifrato `.enc` su disco.

## [0.0.36] - 2026-07-18

### Fixed
- Risolto il popolamento delle cartelle `Chats_Encrypted/` e `Media_Encrypted/`:
  - Implementato un **estrattore DOM dinamico** abbinato a un **`MutationObserver` in tempo reale** in JavaScript.
  - Non appena la pagina viene caricata, o man mano che l'utente naviga tra le conversazioni e scorrere lo storico, ogni messaggio e media visualizzato a schermo viene immediatamente catturato, cifrato in AES-256 e salvato nella cartella `Backup/`.

## [0.0.35] - 2026-07-18

### Fixed
- Risolto il problema di disconnessione / perdita account al riavvio dell'applicazione:
  - Corretta l'incompatibilità maiuscole/minuscole nella serializzazione JSON tra la classe `WhatsAppAccount` (Id, Name, IsActive) e `settings.json` (`id`, `name`, `isActive`) tramite attributi `[JsonPropertyName]` e `PropertyNameCaseInsensitive = True`.
  - Impedito il caricamento di oggetti account vuoti (`Id = Nothing`) che causavano l'apertura di cartelle di profilo anonime non autenticate (`WV2Profile_`).

## [0.0.34] - 2026-07-18

### Fixed
- Risolto problema cartelle backup vuote: aggiunto un ciclo di attesa attiva (polling fino a 75 secondi) in JavaScript per attendere l'inizializzazione completa dei moduli WhatsApp Web (`window.Store.Chat`) prima di estrarre le chat.
- Aggiunta sincronizzazione periodica automatica ogni 2 minuti e al click sul pulsante di ricarica tab.

## [0.0.33] - 2026-07-18

### Fixed
- Migliorata la persistenza della sessione WhatsApp: in caso di assenza o rigenerazione del file `settings.json`, l'applicazione rileva e riutilizza automaticamente la cartella di profilo `WV2Profile_account_*` esistente sul disco invece di crearne una nuova vuota.
- Disabilitata la cancellazione automatica dei profili non mappati in `settings.json` per prevenire la perdita accidentale delle sessioni di autenticazione.

## [0.0.32] - 2026-07-18

### Added
- Salvataggio cifrato in background delle chat (.enc) in formato JSON Lines AES-256 e conservazione media con nomi file anonimizzati (Hash/GUID) in cartella Backup.
- Supporto alla prima sincronizzazione (storico 3 mesi) e sincronizzazione incrementale automatica.

## [0.0.31] - 2026-07-18

### Added
- Versione beta pubblicata per test con percorsi OTA aggiornati.

## [0.0.30] - 2026-07-18

### Changed
- Centralizzato il percorso del repository OTA nel file `Constants.vb` (`UpdateFilesPath`), rendendolo l'unica sorgente di verità da cui lo script `publish.ps1` estrae dinamicamente la destinazione per la pubblicazione dell'aggiornamento.

## [0.0.29] - 2026-07-15

### Added
- Nuova impostazione "Mostra popup messaggio" nelle notifiche: permette di disabilitare il popup WPF mantenendo attivi i toast nativi Windows.

## [0.0.28] - 2026-07-14

### Fixed
- Rimosso riferimento `x:Static` a `Constants` (Module VB) dal XAML, sostituito con binding via code-behind per evitare errore "Constants non esiste nello spazio dei nomi" in fase di compilazione.

## [0.0.27] - 2026-07-14

### Fixed
- Tasto "Aggiungi account" disabilitato durante inizializzazione, cambio account e operazioni in corso.

## [0.0.26] - 2026-07-14

### Changed
- Timeout wait loop OTA ridotto da 20s a 10s.

## [0.0.25] - 2026-07-14

### Added
- Test OTA da 0.0.24.

## [0.0.24] - 2026-07-14

### Fixed
- Timeout nel batch di aggiornamento OTA: dopo 20s senza riuscire a chiudere il processo, l'aggiornamento prosegue comunque.

## [0.0.23] - 2026-07-14

### Added
- Test OTA update da 0.0.22.

## [0.0.22] - 2026-07-14

### Added
- Test OTA update.

## [0.0.21] - 2026-07-14

### Fixed
- **Update non rilevato**: il controllo `.app_version` bloccava la verifica degli aggiornamenti quando il marker corrispondeva alla versione corrente, impedendo di trovare versioni più recenti pubblicate successivamente. Rimosso l'early return: il confronto `IsNewerVersion` gestisce già i casi di versione identica.

## [0.0.20] - 2026-07-13

### Fixed
- **Aggiornamento OTA bloccato**: la finestra non si chiudeva realmente durante l'update (`_allowExit = False` bloccava la chiusura), quindi il batch restava in attesa infinita del termine del processo. Ora `ForceExitForUpdate()` viene chiamato prima dello shutdown.
- Aggiunto log `.update_log.txt` nella cartella d'installazione per tracciare ogni passo del batch di aggiornamento.

### Changed
- Finestra principale ora ridimensionabile: aggiunto pulsante massimizza/ripristina, doppio click sulla barra del titolo per massimizzare, grip di resize in basso a destra.
- La WebView di WhatsApp si adatta automaticamente alle dimensioni della finestra.

### Fixed
- Tasto "Aggiungi account" disabilitato durante operazioni (settings aperto o aggiunta account in corso) per evitare click multipli.
- Confronto versioni OTA: ora aggiorna solo se la versione remota è più recente, evitando downgrade.

## [0.0.19] - 2026-07-13

### Added
- Supporto per canale di pubblicazione beta con percorso OTA separato.
- Impostazione "Usa canale aggiornamenti beta" nelle impostazioni: se abilitata, l'app controlla gli aggiornamenti dal repository beta invece che da quello stabile.

### Fixed
- Tasto "Aggiungi account" disabilitato durante operazioni (settings aperto o aggiunta account in corso) per evitare click multipli.
- Confronto versioni OTA: ora aggiorna solo se la versione remota è più recente, evitando downgrade.

### Changed
- Finestra principale ora ridimensionabile: aggiunto pulsante massimizza/ripristina, doppio click sulla barra del titolo per massimizzare, grip di resize in basso a destra.
- La WebView di WhatsApp si adatta automaticamente alle dimensioni della finestra.

## [0.0.18] - 2026-07-13

### Fixed
- Fixed JS bridge payload double-stringification that prevented VB.NET from parsing the Notification and Translation messages.

## [0.0.17] - 2026-07-13

### Fixed
- Intercepted ServiceWorkerRegistration.prototype.showNotification instead of blocking service workers to fix notifications.

## [0.0.16] - 2026-07-13

### Changed
- Added debug logging for notifications

## [0.0.15] - 2026-07-10

### Fixed
- Disabilitati e rimossi i Service Worker all'avvio della pagina per forzare WhatsApp Web a usare le notifiche standard della pagina (WebSocket), risolvendo il problema delle notifiche che non venivano intercettate dal wrapper.

## [0.0.14] - 2026-07-10

### Fixed
- Corretto il caricamento delle notifiche (toast e popup): iniezione anticipata degli script di override prima del caricamento della pagina (`AddScriptToExecuteOnDocumentCreatedAsync`), abilitazione automatica dei permessi e gestione delle eccezioni native sui Toast per evitare il blocco dei popup.

## [0.0.13] - 2026-07-10

### Fixed
- Notifiche (toast e popup) non funzionanti: il JavaScript con `fetch()` su blob URL e la gestione icona in VB causavano blocchi. Ripristinato invio sincrono della notifica senza fetch icona e rimosso codice download avatar (i blob URL di WhatsApp Web non sono accessibili da `HttpClient`).

## [0.0.12] - 2026-07-10

### Removed
- Rimosse dalle Impostazioni le checkbox "Traduci messaggi delle notifiche" e "Mostra pulsante traduci nelle notifiche" e tutto il codice collegato (`TranslateNotifications`, `ShowTranslateNotificationButton` properties, chiavi localizzazione).

## [0.0.11] - 2026-07-10

### Fixed
- Notifiche (toast e popup) non funzionanti: il JavaScript tentava `fetch()` su URL `blob:` in modo sincrono, bloccando l'invio di `NOTIFICATION_RECEIVED` in WebView2. Ora il messaggio viene inviato immediatamente (icona vuota), e il fetch dell'icona avviene in secondo piano come `NOTIFICATION_ICON`.
- NullReferenceException in `BtnDeleteAccount_Click` (`SettingsWindow.xaml.vb`): `btn.Tag` poteva essere null. Ora usa `TryCast(btn.Tag, String)` con null check.

### Added
- Script `publish.ps1`: automatizza bump versione → build Release → copia su OTA → update `version.txt`.
- `publish.ps1` esclude automaticamente file superflui (`.pdb`, `.xml`) dalla pubblicazione OTA.

## [0.0.10] - 2026-07-10

### Added
- Popup visivo (`MessagePopup.xaml`/`.vb`): finestra WPF borderless che appare in basso a destra all'arrivo di un messaggio, con iniziali del contatto, nome, messaggio e auto-close dopo 5 secondi. Clicca per ripristinare la finestra principale.
- Il toast ora include l'avatar del contatto (`AddAppLogoOverride` con crop circolare) scaricato dall'icona della notifica (URL http o data URL base64).

### Changed
- `HandleNotificationMessageAsync` passata da `Function` a `Async Function` per supportare download asincrono dell'icona.
- Usato `Dispatcher.BeginInvoke` (non bloccante) invece di `Dispatcher.Invoke` per mostrare il popup.
- `DispatcherTimer` sostituisce `System.Timers.Timer` nel popup (corretto threading).

## [0.0.9] - 2026-07-10

### Fixed
- L'uscita dal programma al cambio account con 2+ account era causata dalla re-inizializzazione del WebView2 esistente in `PopulateWebViews()`: quando si aggiungeva un account, tutti i WebView venivano rimossi e re-aggiunti al grid, chiamando `SetupWebViewAsync` anche per quelli già inizializzati, causando la rottura della connessione WhatsApp. Risolto skippando `SetupWebViewAsync` se `wv.CoreWebView2` è già popolato.
- Aggiunto try-catch in `AccountTab_Click` per mostrare eventuali errori di cambio account.

## [0.0.8] - 2026-07-10

### Fixed
- Risolto loop di aggiornamento infinito: `version.txt` ora escluso dalla copia OTA, e introdotto marker `.app_version` locale per evitare che l'app rilevi un falso positivo e si riaggiorni a ogni avvio.
- Il marker `.app_version` viene scritto sia prima del riavvio (da `PerformUpdateAsync`) sia dopo la copia (dal batch `update.bat`).
- `IsLocalVersionCurrent()` salta il check se il marker locale corrisponde già alla versione corrente.

## [0.0.7] - 2026-07-10

### Added
- Menu contestuale (tasto destro) sulle tab account nella finestra principale con opzione "Rename" per rinominare l'account direttamente senza aprire Impostazioni.

## [0.0.6] - 2026-07-10

### Fixed
- Aggiornamento OTA non sovrascrive più `settings.json` e `translations_cache.json` (impostazioni utente e cache traduzioni preservate).
- Nuove chiavi di configurazione introdotte in versioni future vengono automaticamente **mergeate** nel `settings.json` locale durante l'aggiornamento, senza perdere le impostazioni esistenti.

### Added
- `MergeSettingsFromOta()` in `UpdateChecker.vb`: prima del riavvio, confronta il `settings.json` dell'OTA con quello locale e aggiunge le chiavi mancanti con i valori di default.

## [0.0.5] - 2026-07-10

### Fixed
- Account list in Settings ("Gestione Account") rimaneva scura/illeggibile in tema chiaro — `FindVisualChildren` su `ItemsControl` non trovava i container perché non ancora generati al momento di `ApplyTheme()`. Risolto spostando lo styling in `StyleAccountItems()` con `Dispatcher.BeginInvoke(DispatcherPriority.Background)` per attendere la generazione degli item containers.
- Rimosso `Background`/`BorderBrush` hardcoded dal DataTemplate della Border in `SettingsWindow.xaml`.

### Changed
- `SettingsWindow` allargata di 20px e allungata di 30px: `500×550` → `520×580`.

## [0.0.4] - 2026-07-10

### Added
- Controllo WebView2 Runtime all'avvio (`GetAvailableBrowserVersionString()`): se mancante mostra MessageBox con link al download e chiude l'app.
- Helper `FindVisualChildren(Of T)` in `SettingsWindow.xaml.vb` per stilizzare elementi dentro DataTemplate.
- `StyleAccountItems()` in `ApplyTheme()` per aggiornare colore account list in tema chiaro/scuro.

### Fixed
- Aggiornamento OTA non sovrascrive più la cartella `data\` (impostazioni utente preservate).
- `SettingsFile` fallback a `data\settings.json` se non esiste nella base directory.
- MessageBox di conferma eliminazione account ora localizzato (chiavi `delete_account_confirm`, `delete_account_last`).
- ComboBox adattivo al tema (sfondo e testo cambiano con tema chiaro/scuro).
- Pulsante "Add Account" si traduce dinamicamente al cambio lingua.
- Lingua italiana non veniva applicata correttamente a causa del flag `KeepAppInEnglish`.
- Nome account in Settings illeggibile in tema chiaro.
- Persistenza lingua al riavvio.

### Removed
- Rimossa checkbox "Keep app UI in English" e tutta la logica correlata.
- Rimosso uso di Google Translate API per fetch lingue supportate e traduzioni UI (lingue hardcoded a Inglese/Italiano, traduzioni pre-compilate).

## [0.0.3] - 2026-07-09

### Added
- Traduzioni italiano pre-compilate (`ItStrings` dizionario in `Localization.vb`).
- Rilevamento tema di sistema Windows via registry (`AppsUseLightTheme`).

### Changed
- Limitata lingua interfaccia a Inglese e Italiano (rimosso `FetchSupportedLanguages`).
- Rimosso `LoadSupportedLanguagesAsync` e `LoadTranslationsAsync` (codice morto).

## [0.0.2] - 2026-07-09

### Added
- Prima implementazione supporto multilingua con traduzioni UI pre-compilate.
- Logica fallback per lingue non più supportate (ricade in inglese).

### Changed
- Refactoring del sistema di localizzazione: da chiamate Google Translate API a dizionari statici per la UI.

## [0.0.1] - 2026-07-08

### Added
- Fork iniziale da [whatsappPortable](https://github.com/Faeq-F/whatsappPortable) di Faeq-F.
- Wrapper WPF per web.whatsapp.com con WebView2.
- Multi-account con tab separati.
- Tema scuro/chiaro personalizzabile.
- Traduzione integrata messaggi (Google Translate API).
- Notifiche native Windows (Toast).
- System tray con chiusura a vassoio.
- Aggiornamento automatico da repository OTA di rete locale.
- Profili WebView2 isolati per account.
