# Changelog

## [1.3.35] - 2026-07-20

### Performance
- Ottimizzato I/O su `settings.json`: le modifiche alle impostazioni vengono accumulate in cache in memoria e scritte su disco con debounce di 500ms, eliminando letture ridondanti del file.
- `SaveSettingAsync`, `SaveThemeAsync`, `UpdateLanguageAsync` ora modificano la cache in memoria senza rileggere il file da disco.

## [1.3.34] - 2026-07-20

### Fixed
- `MigrateOrphanProfile` ora elimina il profilo stale `WV2Profile_{Id}` se esiste già, poi rinomina l'orfano `WV2Profile_` con la sessione più recente.
- `SetupWebViewAsync`: recupero orfano eseguito anche quando la destinazione esiste già (sostituisce lo stale).
- Quando sia `WV2Profile_` che `WV2Profile_{Id}` esistono, il profilo stale viene eliminato e sostituito con `WV2Profile_` per preservare la sessione WhatsApp più recente.

## [1.3.33] - 2026-07-20

### Fixed
- Aggiunto recupero fallback del profilo WebView2 orfano (`WV2Profile_`) direttamente in `SetupWebViewAsync` come ultima difesa prima di creare un nuovo profilo.
- Aggiunto logging diagnostico in `LoadAccountsAsync`, `MigrateOrphanProfile` e `SetupWebViewAsync` per tracciare la migrazione del profilo WebView2.

## [1.3.32] - 2026-07-20

### Fixed
- Risolto il problema del testo di versione obsoleto (v1.3.17) mostrato nel titolo durante il caricamento iniziale: rimosso il testo hardcoded in XAML e impostata l'inzializzazione immediata nel costruttore `Sub New()` della finestra principale.

## [1.3.31] - 2026-07-20

### Changed
- Rimozione del backup dei media e focalizzazione esclusiva sul backup delle chat di testo.
- Implementata la rotazione periodica dei file di backup delle chat ogni X giorni configurata tramite la costante in fase di compilazione `BackupRotationDays`.
- Applicata la cifratura deterministica AES-256 dei nomi di file conservati nella cartella `Chats_Encrypted`.

## [1.3.30] - 2026-07-18

### Fixed
- Risolta l'origine precisa delle dimensioni anomale dei file multimediali (1 KB, 368 KB, 490 KB):
  - **1 KB**: Causato dal tentativo di scaricare direttamente gli URL HTTPS remoti criptati di WhatsApp (`mmg.whatsapp.net`), che restituiscono HTTP 403 Forbidden. Impedita la `fetch` diretta degli URL `http/https` remoti.
  - **368 KB / 490 KB**: Causato dal download dei soli Blob di anteprima `renderableUrl`.
  - Integrata l'estrazione dai soli oggetti `Blob` decifrati in memoria (`msg.mediaData.blob`, `msg.mediaObject._blob`, `msg.file`) o tramite l'invocazione di `DownloadManager.downloadAndSave(msg)` nativo di WhatsApp Web per ottenere il file binario originale intero.

## [1.3.29] - 2026-07-18

### Fixed
- Risolto in modo definitivo il troncamento a 291 KB dei file multimediali:
  - Implementata la funzione `getMediaDataForContainer` che interroga `Store.Msg.get(id)` con l'ID del messaggio DOM per recuperare il binario video/audio/documento originale `mediaData.blob`.
  - In caso di fallback DOM, prioritizzata la scansione dei tag `<video>`, `<audio>` e collegamenti `<a>` di download rispetto alle immagini poster di anteprima (`<img>`).

## [1.3.28] - 2026-07-18

### Fixed
- Risolto il troncamento dei file video e media (precedentemente salvati tutti a 291 KB):
  - Aggiunta la funzione `getMediaBlobFromMsg(msg)` in JavaScript per accedere direttamente all'oggetto `msg.mediaData.blob` o invocare `msg.downloadMedia()` interno a WhatsApp Web.
  - Sostituita l'estrazione della sola miniatura preview/poster JPEG dal DOM con lo scaricamento del file video/audio/documento binario originale completo senza limiti di dimensione.

## [1.3.27] - 2026-07-18

### Fixed
- Risolto problema file media salvati a 1KB:
  - Sostituita la semplice stringa dell'URL `blob:` con la conversione asincrona `fetchBlobAsBase64` in JavaScript per scaricare ed inviare a VB.NET l'intero payload binario Base64 originale di immagini, audio e video.
  - Aggiunta la validazione `rawMediaBytes.Length > 0` prima di scrivere il file cifrato `.enc` su disco.

## [1.3.26] - 2026-07-18

### Fixed
- Risolto il popolamento delle cartelle `Chats_Encrypted/` e `Media_Encrypted/`:
  - Implementato un **estrattore DOM dinamico** abbinato a un **`MutationObserver` in tempo reale** in JavaScript.
  - Non appena la pagina viene caricata, o man mano che l'utente naviga tra le conversazioni e scorrere lo storico, ogni messaggio e media visualizzato a schermo viene immediatamente catturato, cifrato in AES-256 e salvato nella cartella `Backup/`.

## [1.3.25] - 2026-07-18

### Fixed
- Risolto il problema di disconnessione / perdita account al riavvio dell'applicazione:
  - Corretta l'incompatibilità maiuscole/minuscole nella serializzazione JSON tra la classe `WhatsAppAccount` (Id, Name, IsActive) e `settings.json` (`id`, `name`, `isActive`) tramite attributi `[JsonPropertyName]` e `PropertyNameCaseInsensitive = True`.
  - Impedito il caricamento di oggetti account vuoti (`Id = Nothing`) che causavano l'apertura di cartelle di profilo anonime non autenticate (`WV2Profile_`).

## [1.3.24] - 2026-07-18

### Fixed
- Risolto problema cartelle backup vuote: aggiunto un ciclo di attesa attiva (polling fino a 75 secondi) in JavaScript per attendere l'inizializzazione completa dei moduli WhatsApp Web (`window.Store.Chat`) prima di estrarre le chat.
- Aggiunta sincronizzazione periodica automatica ogni 2 minuti e al click sul pulsante di ricarica tab.

## [1.3.23] - 2026-07-18

### Fixed
- Migliorata la persistenza della sessione WhatsApp: in caso di assenza o rigenerazione del file `settings.json`, l'applicazione rileva e riutilizza automaticamente la cartella di profilo `WV2Profile_account_*` esistente sul disco invece di crearne una nuova vuota.
- Disabilitata la cancellazione automatica dei profili non mappati in `settings.json` per prevenire la perdita accidentale delle sessioni di autenticazione.

## [1.3.22] - 2026-07-18

### Added
- Salvataggio cifrato in background delle chat (.enc) in formato JSON Lines AES-256 e conservazione media con nomi file anonimizzati (Hash/GUID) in cartella Backup.
- Supporto alla prima sincronizzazione (storico 3 mesi) e sincronizzazione incrementale automatica.

## [1.3.21] - 2026-07-18

### Added
- Versione beta pubblicata per test con percorsi OTA aggiornati.

## [1.3.20] - 2026-07-18

### Changed
- Centralizzato il percorso del repository OTA nel file `Constants.vb` (`UpdateFilesPath`), rendendolo l'unica sorgente di verità da cui lo script `publish.ps1` estrae dinamicamente la destinazione per la pubblicazione dell'aggiornamento.

## [1.3.19] - 2026-07-15

### Added
- Nuova impostazione "Mostra popup messaggio" nelle notifiche: permette di disabilitare il popup WPF mantenendo attivi i toast nativi Windows.

## [1.3.18] - 2026-07-14

### Fixed
- Rimosso riferimento `x:Static` a `Constants` (Module VB) dal XAML, sostituito con binding via code-behind per evitare errore "Constants non esiste nello spazio dei nomi" in fase di compilazione.

## [1.3.17] - 2026-07-14

### Fixed
- Tasto "Aggiungi account" disabilitato durante inizializzazione, cambio account e operazioni in corso.

## [1.3.16] - 2026-07-14

### Changed
- Timeout wait loop OTA ridotto da 20s a 10s.

## [1.3.15] - 2026-07-14

### Added
- Test OTA da 1.3.14.

## [1.3.14] - 2026-07-14

### Fixed
- Timeout nel batch di aggiornamento OTA: dopo 20s senza riuscire a chiudere il processo, l'aggiornamento prosegue comunque.

## [1.3.13] - 2026-07-14

### Added
- Test OTA update da 1.3.12.

## [1.3.12] - 2026-07-14

### Added
- Test OTA update.

## [1.3.11] - 2026-07-14

### Fixed
- **Update non rilevato**: il controllo `.app_version` bloccava la verifica degli aggiornamenti quando il marker corrispondeva alla versione corrente, impedendo di trovare versioni più recenti pubblicate successivamente. Rimosso l'early return: il confronto `IsNewerVersion` gestisce già i casi di versione identica.

## [1.3.10] - 2026-07-13

### Fixed
- **Aggiornamento OTA bloccato**: la finestra non si chiudeva realmente durante l'update (`_allowExit = False` bloccava la chiusura), quindi il batch restava in attesa infinita del termine del processo. Ora `ForceExitForUpdate()` viene chiamato prima dello shutdown.
- Aggiunto log `.update_log.txt` nella cartella d'installazione per tracciare ogni passo del batch di aggiornamento.

### Changed
- Finestra principale ora ridimensionabile: aggiunto pulsante massimizza/ripristina, doppio click sulla barra del titolo per massimizzare, grip di resize in basso a destra.
- La WebView di WhatsApp si adatta automaticamente alle dimensioni della finestra.

### Fixed
- Tasto "Aggiungi account" disabilitato durante operazioni (settings aperto o aggiunta account in corso) per evitare click multipli.
- Confronto versioni OTA: ora aggiorna solo se la versione remota è più recente, evitando downgrade.

## [1.3.9] - 2026-07-13

### Added
- Supporto per canale di pubblicazione beta con percorso OTA separato.
- Impostazione "Usa canale aggiornamenti beta" nelle impostazioni: se abilitata, l'app controlla gli aggiornamenti dal repository beta invece che da quello stabile.

### Fixed
- Tasto "Aggiungi account" disabilitato durante operazioni (settings aperto o aggiunta account in corso) per evitare click multipli.
- Confronto versioni OTA: ora aggiorna solo se la versione remota è più recente, evitando downgrade.

### Changed
- Finestra principale ora ridimensionabile: aggiunto pulsante massimizza/ripristina, doppio click sulla barra del titolo per massimizzare, grip di resize in basso a destra.
- La WebView di WhatsApp si adatta automaticamente alle dimensioni della finestra.

## [1.3.8] - 2026-07-13

### Fixed
- Fixed JS bridge payload double-stringification that prevented VB.NET from parsing the Notification and Translation messages.

## [1.3.7] - 2026-07-13

### Fixed
- Intercepted ServiceWorkerRegistration.prototype.showNotification instead of blocking service workers to fix notifications.

## [1.3.6] - 2026-07-13

### Changed
- Added debug logging for notifications

## [1.3.5] - 2026-07-10

### Fixed
- Disabilitati e rimossi i Service Worker all'avvio della pagina per forzare WhatsApp Web a usare le notifiche standard della pagina (WebSocket), risolvendo il problema delle notifiche che non venivano intercettate dal wrapper.

## [1.3.4] - 2026-07-10

### Fixed
- Corretto il caricamento delle notifiche (toast e popup): iniezione anticipata degli script di override prima del caricamento della pagina (`AddScriptToExecuteOnDocumentCreatedAsync`), abilitazione automatica dei permessi e gestione delle eccezioni native sui Toast per evitare il blocco dei popup.

## [1.3.3] - 2026-07-10

### Fixed
- Notifiche (toast e popup) non funzionanti: il JavaScript con `fetch()` su blob URL e la gestione icona in VB causavano blocchi. Ripristinato invio sincrono della notifica senza fetch icona e rimosso codice download avatar (i blob URL di WhatsApp Web non sono accessibili da `HttpClient`).

## [1.3.2] - 2026-07-10

### Removed
- Rimosse dalle Impostazioni le checkbox "Traduci messaggi delle notifiche" e "Mostra pulsante traduci nelle notifiche" e tutto il codice collegato (`TranslateNotifications`, `ShowTranslateNotificationButton` properties, chiavi localizzazione).

## [1.3.1] - 2026-07-10

### Fixed
- Notifiche (toast e popup) non funzionanti: il JavaScript tentava `fetch()` su URL `blob:` in modo sincrono, bloccando l'invio di `NOTIFICATION_RECEIVED` in WebView2. Ora il messaggio viene inviato immediatamente (icona vuota), e il fetch dell'icona avviene in secondo piano come `NOTIFICATION_ICON`.
- NullReferenceException in `BtnDeleteAccount_Click` (`SettingsWindow.xaml.vb`): `btn.Tag` poteva essere null. Ora usa `TryCast(btn.Tag, String)` con null check.

### Added
- Script `publish.ps1`: automatizza bump versione → build Release → copia su OTA → update `version.txt`.
- `publish.ps1` esclude automaticamente file superflui (`.pdb`, `.xml`) dalla pubblicazione OTA.

## [1.3.0] - 2026-07-10

### Added
- Popup visivo (`MessagePopup.xaml`/`.vb`): finestra WPF borderless che appare in basso a destra all'arrivo di un messaggio, con iniziali del contatto, nome, messaggio e auto-close dopo 5 secondi. Clicca per ripristinare la finestra principale.
- Il toast ora include l'avatar del contatto (`AddAppLogoOverride` con crop circolare) scaricato dall'icona della notifica (URL http o data URL base64).

### Changed
- `HandleNotificationMessageAsync` passata da `Function` a `Async Function` per supportare download asincrono dell'icona.
- Usato `Dispatcher.BeginInvoke` (non bloccante) invece di `Dispatcher.Invoke` per mostrare il popup.
- `DispatcherTimer` sostituisce `System.Timers.Timer` nel popup (corretto threading).

## [1.2.3] - 2026-07-10

### Fixed
- L'uscita dal programma al cambio account con 2+ account era causata dalla re-inizializzazione del WebView2 esistente in `PopulateWebViews()`: quando si aggiungeva un account, tutti i WebView venivano rimossi e re-aggiunti al grid, chiamando `SetupWebViewAsync` anche per quelli già inizializzati, causando la rottura della connessione WhatsApp. Risolto skippando `SetupWebViewAsync` se `wv.CoreWebView2` è già popolato.
- Aggiunto try-catch in `AccountTab_Click` per mostrare eventuali errori di cambio account.

## [1.2.2] - 2026-07-10

### Fixed
- Risolto loop di aggiornamento infinito: `version.txt` ora escluso dalla copia OTA, e introdotto marker `.app_version` locale per evitare che l'app rilevi un falso positivo e si riaggiorni a ogni avvio.
- Il marker `.app_version` viene scritto sia prima del riavvio (da `PerformUpdateAsync`) sia dopo la copia (dal batch `update.bat`).
- `IsLocalVersionCurrent()` salta il check se il marker locale corrisponde già alla versione corrente.

## [1.2.1] - 2026-07-10

### Added
- Menu contestuale (tasto destro) sulle tab account nella finestra principale con opzione "Rename" per rinominare l'account direttamente senza aprire Impostazioni.

## [1.2.0] - 2026-07-10

### Fixed
- Aggiornamento OTA non sovrascrive più `settings.json` e `translations_cache.json` (impostazioni utente e cache traduzioni preservate).
- Nuove chiavi di configurazione introdotte in versioni future vengono automaticamente **mergeate** nel `settings.json` locale durante l'aggiornamento, senza perdere le impostazioni esistenti.

### Added
- `MergeSettingsFromOta()` in `UpdateChecker.vb`: prima del riavvio, confronta il `settings.json` dell'OTA con quello locale e aggiunge le chiavi mancanti con i valori di default.

## [1.1.9] - 2026-07-10

### Fixed
- Account list in Settings ("Gestione Account") rimaneva scura/illeggibile in tema chiaro — `FindVisualChildren` su `ItemsControl` non trovava i container perché non ancora generati al momento di `ApplyTheme()`. Risolto spostando lo styling in `StyleAccountItems()` con `Dispatcher.BeginInvoke(DispatcherPriority.Background)` per attendere la generazione degli item containers.
- Rimosso `Background`/`BorderBrush` hardcoded dal DataTemplate della Border in `SettingsWindow.xaml`.

### Changed
- `SettingsWindow` allargata di 20px e allungata di 30px: `500×550` → `520×580`.

## [1.1.8] - 2026-07-10

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

## [1.1.5] - 2026-07-09

### Added
- Traduzioni italiano pre-compilate (`ItStrings` dizionario in `Localization.vb`).
- Rilevamento tema di sistema Windows via registry (`AppsUseLightTheme`).

### Changed
- Limitata lingua interfaccia a Inglese e Italiano (rimosso `FetchSupportedLanguages`).
- Rimosso `LoadSupportedLanguagesAsync` e `LoadTranslationsAsync` (codice morto).

## [1.1.0] - 2026-07-09

### Added
- Prima implementazione supporto multilingua con traduzioni UI pre-compilate.
- Logica fallback per lingue non più supportate (ricade in inglese).

### Changed
- Refactoring del sistema di localizzazione: da chiamate Google Translate API a dizionari statici per la UI.

## [1.0.0] - 2026-07-08

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
