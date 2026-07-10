# Changelog

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
