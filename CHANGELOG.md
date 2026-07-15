# Changelog

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
