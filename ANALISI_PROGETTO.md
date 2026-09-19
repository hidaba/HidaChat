# Analisi del Progetto HidaChat

## 1. SCOPO DELL'APPLICAZIONE

**HidaChat** (versione **1.0.0**, precedentemente nota come WhatsAppVB / "WhatsApp Portable") è un **client WPF desktop multipiattaforma Windows** per **WhatsApp Web**, **Telegram Web**, **OpenClaw** (Gateway e Web UI per agenti IA autonomi) ed **Hermes Agent** (Nous Research). È un **wrapper avanzato** che carica le piattaforme all'interno di controlli **WebView2** (Chromium Edge) indipendenti, aggiungendo funzionalità esclusive non disponibili nei browser standard:

- **Multi-piattaforma & Multi-account**: gestione simultanea di account WhatsApp, Telegram, OpenClaw ed Hermes Agent in schede separate con precaricamento istantaneo
- **Integrazione Rete Mesh VPN Tailscale (tsnet)**: companion daemon `tsnetd.exe` statico in Go per connettere istanze OpenClaw ed Hermes su reti mesh private senza installare client esterni sul sistema operativo host
- **Persistenza Identità Dispositivo Deterministica**: porte proxy fisse (`18800+` per OpenClaw, `18900+` per Hermes) e token locale sicuro per azzerare continue riapprovazioni crittografiche (IndexedDB Ed25519)
- **Invio Massivo Personalizzato da Excel / CSV**: importazione rubriche con segnaposto dinamici (`{Nome}`, `{Cognome}`, `{Azienda}`, `{Testo}`) e delay anti-spam
- **Tema scuro/chiaro personalizzato** con rilevamento automatico del tema di sistema Windows e sincronizzazione completa (inclusa interfaccia Telegram Web)
- **Traduzione integrata dei messaggi**: hover button per tradurre singoli messaggi, traduzione batch dell'intera pagina e traduzione notifiche
- **Notifiche native Windows** (Toast & Popup overlay) per i messaggi in arrivo con routing selettivo della scheda
- **System tray** (icona nell'area di notifica) con chiusura/minimizzazione a vassoio e badge non letti
- **Aggiornamento automatico sicuro (OTA)** con controllo crittografico dell'integrità SHA-256 via GitHub Releases
- **Isolamento e Portabilità Assoluta (100% Portable)**: ogni account ha una directory di profilo WebView2 isolata all'interno del percorso applicativo locale (`data/webview/`)

---

## 2. STACK TECNOLOGICO

| Componente | Tecnologia |
|---|---|---|
| Linguaggio | **VB.NET** (Visual Basic .NET) |
| Framework | **.NET 9.0** con target **Windows 10.0.19041.0** (Windows 10 20H1+) |
| UI | **WPF** (Windows Presentation Foundation) + `UseWindowsForms` per System Tray |
| Embedded Browser | **Microsoft.Web.WebView2** v1.0.4078.44 (Chromium Edge) |
| Notifiche native | **Microsoft.Toolkit.Uwp.Notifications** v7.1.3 (Toast notifications) |
| Invio massivo / Excel | **ExcelDataReader** v3.9.0 e **ExcelDataReader.DataSet** v3.9.0 |
| Companion Mesh VPN | **tsnetd.exe** (Go static binary con stack gVisor e WireGuard Tailscale) |
| IDE | Visual Studio 2022 |
| Serializzazione | `System.Text.Json` |
| Traduzioni UI | **Pre-compilate** (dizionari `EnStrings`, `ItStrings`, `FrStrings`, `EsStrings`, `DeStrings`) |
| Traduzione messaggi | **Google Translate API** non ufficiale (`translate.googleapis.com`) |
| Tema di sistema rilevato | Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` |
| Repository OTA | GitHub Releases API (`https://api.github.com/repos/hidaba/HidaChat/releases`) |

---

## 3. ARCHITETTURA GENERALE

L'architettura è **monolitica single-window** con un pattern **Model-View (MV-like)**, dove la logica è nei code-behind delle finestre XAML e in classi di servizio. Non c'è un vero pattern MVVM con binding complessi: il data binding è usato solo per `ItemsControl`, mentre i cambi di stato sono propagati via eventi `INotifyPropertyChanged`.

**Schema dei flussi:**

```
Application.xaml
    └── MainWindow.xaml (finestra principale)
          ├── TitleBar (barra personalizzata)
          ├── AccountTabs (barra orizzontale degli account)
          └── WebViewsGrid (contenitore WebView2 per ogni account)
                ├── SettingsWindow (dialog modale impostazioni)
                ├── BulkSenderWindow (invio massivo Excel/CSV)
                └── AboutWindow (informazioni versione e licenza)
```

**Classi di servizio (core logic):**

```
SettingsController  ←→  settings.json       (persistenza impostazioni)
AccountManager      ←→  AccountManager      (gestione lista account)
AppAccounts         ──  WebView2 (per-account WhatsApp/Telegram/OpenClaw/Hermes)
TsnetManager        ──  tsnetd.exe          (demone companion Tailscale Mesh VPN)
UpdateChecker       ──  GitHub Releases API (check OTA con verifica SHA-256)
AppLocalizations    ──  Google Translate API (localizzazione UI + traduzione messaggi)
JsScripts           ──  JavaScript injection in WebView2
BulkSenderEngine    ──  Automazione invio sequenziale WhatsApp / Telegram Web
ExcelContactService ──  Parsing e normalizzazione rubriche Excel (.xlsx, .xls) e CSV
```

---

## 4. DESCRIZIONE DI OGNI FILE/CLASSE PRINCIPALE

### `HidaChat.vbproj` – File di progetto
- SDK: `Microsoft.NET.Sdk`
- Output: `WinExe` (eseguibile Windows)
- Target: `net9.0-windows10.0.19041.0`
- Packages: `Microsoft.Web.WebView2` (1.0.4078.44), `Microsoft.Toolkit.Uwp.Notifications` (7.1.3), `ExcelDataReader` (3.9.0), `ExcelDataReader.DataSet` (3.9.0)
- Importa tutti i namespace WPF standard

### `Application.xaml` / `Application.xaml.vb` – Entry point
- StartupUri punta a `MainWindow.xaml`
- Code-behind: **Mutex** per single instance (impedisce avvio di copie multiple)

### `MainWindow.xaml` – UI principale
- Finestra `1000x700`, `WindowStyle=None`, `AllowsTransparency=True` (bordi personalizzati con DropShadow)
- Layout a 3 righe:
  1. **TitleBar** personalizzata (icona, titolo con versione `v{0}`, pulsanti traduzione pagina/ricarica/impostazioni/minimizza/chiudi)
  2. **Account bar** (`ItemsControl` orizzontale) con pulsante "+ Add Account" (tradotto)
  3. **WebViewsGrid** container per i controlli WebView2
- Stili personalizzati per bottoni header, tab account, pulsante aggiunta account

### `MainWindow.xaml.vb` – Code-behind principale (342 righe)
- **Campi**: `_settingsController`, `_accountManager`, `_trayIcon` (NotifyIcon), `_allowExit`
- **Loaded**:
  1. Carica impostazioni (`_settingsController.LoadSettingsAsync()`)
  2. Carica account (`_accountManager.LoadAccountsAsync()`)
  3. Applica tema WPF
  4. Configura system tray con contestuale (Toggle Window, Exit)
  5. Popola `AccountsList.ItemsSource` con `_accountManager.Accounts`
  6. Crea dinamicamente WebView2 per ogni account (`PopulateWebViews()`)
  7. Sottoscrive `PropertyChanged` di SettingsController e AccountManager
  8. Configura Toast notifications click handler
  9. Check aggiornamenti in background
  10. **RefreshLocalization** per testi tradotti (es. pulsante "Add Account")
- **System Tray**: icona dinamica (normale/notifica), doppio click toggle finestra, chiusura a tray (nasconde invece di chiudere)
- **Closing**: se `_allowExit = False`, cancella la chiusura e nasconde la finestra
- **AccountTab_Click**: switch account via `_accountManager.SwitchAccountAsync(accountId)`
- **BtnTranslatePage_Click**: esegue `window.translatePage()` JS nel WebView attivo
- **BtnReloadActiveTab_Click**: ricarica il WebView2 attivo
- **BtnSettings_Click**: apre `SettingsWindow` come dialog modale, bloccando `_accountManager.IsDialogOpen`
- **ApplyWpfTheme**: imposta colori scuri/chiari su `RootBorder` e `TitleBar`, e inietta CSS tema in tutti i WebView
- **RefreshLocalization**: aggiorna `BtnAddAccount.Content` con traduzione corrente
- **OnSettingsPropertyChanged**: reagisce a cambio tema (riapplica) e cambio lingua (refresh localizzazione)
- **Helper**: `FindVisualChild`, `FindVisualChildren` per navigazione visual tree WPF

### `AccountManager.vb` – Gestore account (258 righe)
- Implementa `INotifyPropertyChanged`
- **Proprietà**: `Accounts` (List(Of WhatsAppAccount)), `CurrentAccount`, `HasAnyNotification`, `IsDialogOpen`
- **LoadAccountsAsync**: legge JSON `settings["accounts"]` dal file settings, deserializza in `List(Of WhatsAppAccount)`, crea WebView2 per ognuno, pulisce profili orfani
- **CreateDefaultAccountAsync**: se nessun account, crea "Account 1"
- **SaveAccountsAsync**: serializza account (id/name/isActive) in settings["accounts"]
- **AddAccountAsync**: aggiunge account con ID generato via timestamp, crea WebView2
- **RemoveAccountAsync**: rimuove account (minimo 1), elimina directory profilo WebView2 dopo 500ms
- **SwitchAccountAsync**: cambia `CurrentAccount`, salva su file
- **UpdateAccountNameAsync**: rinomina account
- **HandleNotificationStateChanged**: aggiorna `HasAnyNotification` flag
- **Extension**: `IEnumerableExtensions.Map` (wrapper per LINQ Select)

### `AppAccounts.vb` – Modello account e gestione WebView2 per-account
- **Proprietà**:
  - `Id` (string), `Name` (string), `IsActive` (bool)
  - `Platform` (string): `"WhatsApp"`, `"Telegram"`, `"OpenClaw"` o `"Hermes"`
  - `IsOpenClaw` (bool), `IsHermes` (bool), `IsTelegram` (bool), `IsWhatsApp` (bool)
  - `ServerUrl` (string): URL per OpenClaw (default `http://127.0.0.1:18789`) o Hermes (default `http://127.0.0.1:9119`)
  - `AuthToken` (string): Token di accesso o API Key per il gateway
  - `TailscaleIntegration` (bool): abilita tunneling privato sicuro tramite companion demone `tsnetd`
  - `LocalProxyPort` (int): porta loopback deterministica (`18800+` per OpenClaw, `18900+` per Hermes)
  - `HasNotification` (bool), `UnreadCount` (int), `IsContactOnline` (bool)
  - `BridgeToken` (string, JsonIgnore) – token univoco di sicurezza per ponte JS↔VB.NET
  - `WebView` (WebView2, JsonIgnore)
- **SharedDataDirectory**: `baseDir/data/webview/`
- **SetupWebViewAsync**:
  1. Crea directory profilo `WV2Profile_{id}` isolata per ciascuna sessione
  2. Crea `CoreWebView2Environment` isolato puntando al profilo
  3. Per OpenClaw ed Hermes con Tailscale: avvia istanza `TsnetManager` dedicata con porta locale deterministica
  4. Per Hermes: inietta header `Authorization: Bearer <token>` tramite filtro `WebResourceRequested`
  5. Configura bridge JSON bidirezionale e script di notifica e traduzione
  6. Naviga verso la destinazione appropriata:
     - **WhatsApp**: `https://web.whatsapp.com/`
     - **Telegram**: `https://web.telegram.org/a/`
     - **OpenClaw**: `http://127.0.0.1:<LocalProxyPort>` (se Tailscale) o `ServerUrl`
     - **Hermes**: `http://127.0.0.1:<LocalProxyPort>` (se Tailscale) o `ServerUrl`
- **Auto-Recovery**: intercetta l'evento `ProcessFailed` del runtime WebView2 ed esegue un ripristino automatico istantaneo.

### `TsnetManager.vb` – Controller Companion Tailscale Mesh VPN
- Gestisce l'esecuzione e il ciclo di vita del demone Go companion `tsnetd.exe`.
- Avvia il processo in background passando parametri di configurazione isolati (`-target`, `-local-port`, `-local-token`, `-node-name`, `-state-dir`).
- Monitora l'endpoint interno `/tsnet-status` fino al completamento dell'handshake WireGuard (`Running`), garantendo che la WebView2 non riceva errori di caricamento pagina.
- Rilascia e termina in modo pulito il demone alla chiusura della scheda o dell'applicazione.

### `Constants.vb` – Costanti globali
- `AppVersion = "1.0.0"`
- `AppReleaseDate = "2026-09-19"`
- `GitHubReleasesApiUrl = "https://api.github.com/repos/hidaba/HidaChat/releases"`
- `GitHubLatestReleaseApiUrl = "https://api.github.com/repos/hidaba/HidaChat/releases/latest"`
- `MutexId = "Local\HidaChat_SingleInstance_Mutex"`

### `SettingsController.vb` – Controller impostazioni (345 righe)
- Implementa `INotifyPropertyChanged`
- **Proprietà**: `Theme`, `AlwaysShowTabBar`, `CheckForUpdates`, `TranslateMessageButton`, `FullPageTranslation`, `ShowTranslateAllMessagesButton`, `TranslateNotifications`, `ShowTranslateNotificationButton`, `Language`, `Localizations` (AppLocalizations), `SupportedLanguages`, `IsTranslating`
- **Persistenza**: file `settings.json` + cache traduzioni `translations_cache.json`
- **LoadSettingsAsync**: carica da JSON, setta tutte le proprietà, carica cache traduzioni, applica lingua (fallback a inglese per lingue non supportate)
- **SaveThemeAsync**: salva tema, notifica cambiamento
- **UpdateLanguageAsync**: cambia lingua, carica traduzioni pre-compilate (en/it), salva in cache
- **GetBoolSetting**: helper per parsare booleani da JSON (supporta JsonElement + convert)
- **FallbackOrLoadTranslations**: seleziona traduzioni inglese o italiano pre-compilate

### `SettingsWindow.xaml` / `SettingsWindow.xaml.vb` – Finestra impostazioni
- Dialog modale `520x580`, Owner = MainWindow
- Sezioni: **Tema** (ComboBox System/Light/Dark), **Lingua** (ComboBox Inglese/Italiano, checkboxes traduzione), **Notifiche** (checkboxes), **Gestione Account** (ItemsControl con TextBox e pulsante Delete), **DevTools** (Debug + Check Updates)
- **Tutti i label tradotti** dinamicamente via `RefreshLocalization()` (legge da dizionario italiano/inglese)
- Stili personalizzati: chiudi, sezioni, checkbox, combobox, bottoni azione, delete
- **Combo adattivi al tema**: sfondo e testo cambiano con tema scuro/chiaro
- **Loaded**: popola dropdown, setta checkbox, applica tema, applica traduzioni
- **ComboTheme_SelectionChanged**: salva tema, applica colore alla finestra
- **ComboLanguage_SelectionChanged**: aggiorna lingua, aggiorna UI, notifica tutti i WebView
- **ChkSetting_Changed**: switch su nome checkbox per salvare impostazione, notifica hover state ai WebView
- **TxtAccountName_LostFocus**: rinomina account via `_accountManager.UpdateAccountNameAsync`
- **BtnDeleteAccount_Click**: conferma eliminazione account, chiama `RemoveAccountAsync`
- **BtnDevTools_Click**: apre DevTools del WebView attivo
- **BtnCheckUpdates_Click**: chiama `UpdateChecker.CheckForUpdatesAsync(force:=True)`
- **ApplyTheme**: imposta background/foreground sulla finestra e su tutti i controlli figli via `FindLogicalChildren` (combo inclusi); stile account items via `StyleAccountItems` con `Dispatcher.BeginInvoke` (differito per attesa generazione item containers)
- **RefreshLocalization**: carica tutti i label dal dizionario `Localizations`
- **Helper**: `FindLogicalChildren(Of T)` per navigazione logical tree, `FindVisualChildren(Of T)` per visual tree

### `Localization.vb` – Sistema di localizzazione e traduzione (165 righe)

Due componenti:

- **`AppLanguages` (Module)**:
  - `IsRtl(code)`: controlla se una lingua è RTL (arabo, ebraico, farsi, urdu, ecc.)

- **`AppLocalizations` (Class)**:
  - `EnStrings`: dizionario con tutte le chiavi di localizzazione (76 chiavi: settings, theme, manage_accounts, translate_to_lang, ecc.)
  - `ItStrings`: dizionario con tutte le chiavi tradotte in italiano
  - `[Get](key, args)`: recupera traduzione con supporto placeholder `{name}`, `{lang}`

### `JsScripts.vb` – Script JavaScript injection (606 righe)

Tre classi statiche che contengono JavaScript inline:

- **`ThemeJsScripts`**:
  - `DarkModeJS`: inietta CSS con sfondo nero, angoli arrotondati, nasconde "Download for Windows" panel, setta `classList = ["dark"]`
  - `LightModeJS`: simile ma sfondo bianco, `classList = [""]`
  - `RoundedCorners`: CSS `border-radius: 15px` su `#app`
  - `RemoveDownloadForWindows`: nasconde introduzione e icona WA quadrata

- **`NotificationJsScripts`**:
  - `NotificationOverrideJS`: override completo di `window.Notification` con implementazione personalizzata:
    - Intercetta `new Notification(title, options)` e invia via `chrome.webview.postMessage` al canale `NotificationChannel`
    - Supporta `permission = 'granted'`, `requestPermission`
    - `onNotificationClicked(id)` e `onNotificationClosedFromServer(id)` per gestione callback
    - Tracciamento notifiche attive in `window.activeCustomNotifications`

- **`TranslationJsScripts`**:
  - Sistema di traduzione lato client composto da più moduli:
    - `TranslationStyles`: CSS per hover button e loading animation
    - `TranslationHoverButton`: crea bottone hover su `[data-testid="msg-container"]` con icona traduzione
    - `TranslationBubbleUI`: `performTranslation(text, container)`: crea bubble di traduzione sotto il messaggio con header "Translation", close button, chiamata via `postMessage` canale `TranslationChannel`
    - `TranslateAllMessagesJS`: `window.translateAllMessages()` per tradurre tutti i messaggi visibili
    - `TranslationEngineJS`: `scanAndTranslateDOM()` con `MutationObserver` per traduzione automatica full-page (batch processing, chunk da 50 nodi)
    - `TranslationCallbacksJS`: `onBatchTranslationReceived`, `onTranslationReceived` per ricevere risultati traduzione dal backend VB.NET
  - `GetTranslationJS(...)`: metodo che assembla lo script completo sostituendo placeholder (`$$LANG_CODE$$`, `$$LANG_NAME$$`, ecc.) con i valori correnti

### `UpdateChecker.vb` – Controllo aggiornamenti (207 righe)
- `_hasChecked` statico per evitare doppio check
- `CheckForUpdatesAsync(settings, accountManager, force)`: legge versione remota da file OTA di rete (`version.txt`), confronta con `Constants.AppVersion`, se diverso esegue `PerformUpdateAsync`
- `ReadVersionFromFileAsync()`: legge `Constants.UpdateVersionFile` (percorso UNC)
- `PerformUpdateAsync(latestVersion, installDir)`:
   1. Verifica permessi scrittura nella cartella d'installazione
   2. Copia tutti i file dall'OTA a una cartella temporanea (esclusi `data\`, `settings.json`, `translations_cache.json`)
   3. `MergeSettingsFromOta()`: confronta chiavi tra OTA e settings locale, aggiunge quelle mancanti (merge nuove impostazioni)
   4. Genera `update.bat` che attende la chiusura dell'app, copia con robocopy, riavvia

### `AssemblyInfo.vb`
- Attribute `ThemeInfo` per WPF (nessun dizionario tematico esterno, solo assembly)

---

## 5. LIBRERIE ESTERNE / DIPENDENZE

| Pacchetto | Versione | Utilizzo |
|---|---|---|
| `Microsoft.Web.WebView2` | 1.0.4078.44 | Controllo browser Chromium embedded |
| `Microsoft.Toolkit.Uwp.Notifications` | 7.1.3 | Toast notification native Windows 10+ |
| `System.Text.Json` | (built-in .NET 9) | Serializzazione settings, cache, messaggi bridge |
| `System.Net.Http` | (built-in) | Chiamate API Google Translate (traduzione messaggi) |
| **Google Translate API** | (non ufficiale) | Solo per traduzione messaggi in webview (`translate_a/single`) |
| **Repository OTA** | GitHub Releases | Check versione e auto-update via GitHub Releases API |
| **System.Windows.Forms** | (built-in) | `NotifyIcon` per system tray |
| `System.Windows.Interop` | (built-in) | Interop WPF/Win32 |

---

## 6. FLUSSI DI FUNZIONAMENTO PRINCIPALI

### Avvio applicazione
1. MainWindow.Loaded
2. → SettingsController.LoadSettingsAsync() (legge settings.json + traduzioni cache)
3. → AccountManager.LoadAccountsAsync() (legge accounts da settings, crea WebView2)
4. → PopulateWebViews() (aggiunge ogni WebView2 al grid, chiama SetupWebViewAsync)
5. → SetupWebViewAsync per ogni account:
   - Crea environment WebView2 isolato
   - Naviga a web.whatsapp.com
   - Al NavigationCompleted: inietta bridge token, tema CSS, override Notification, script traduzione
6. → Configura system tray
7. → Check aggiornamenti in background

### Ricezione notifica messaggio
1. WhatsApp Web JS chiama `new Notification(title, {body})`
2. Notification override JS intercetta → postMessage a VB.NET su canale NotificationChannel
3. WhatsAppAccount.HandleWebMessageAsync → HandleNotificationMessageAsync
4. → Crea Toast notification nativa via `Microsoft.Toolkit.Uwp.Notifications`
5. → Invoca callback `onNotificationChanged(accountId, True)`
6. → AccountManager.HandleNotificationStateChanged aggiorna `HasAnyNotification`
7. → MainWindow.OnAccountManagerPropertyChanged aggiorna icona tray

### Click su notifica Toast
1. ToastNotificationManagerCompat.OnActivated
2. Parsa argument (accountId, notificationId)
3. Dispatcher.Invoke → ToggleWindow(), SwitchToAccountAsync(accountId)
4. Esegue JS `onNotificationClicked('{notificationId}')` nel WebView

### Traduzione messaggio
1. Utente clicca pulsante traduzione hover su messaggio
2. JS `performTranslation(text, bubble)` → postMessage canale TranslationChannel
3. WhatsAppAccount.HandleTranslationMessageAsync → AppLocalizations.TranslateSingle()
4. Chiamata HTTP a Google Translate API
5. Risultato reinserito via `ExecuteScriptAsync("onTranslationReceived(...)")` → bubble UI aggiornata

### Cambio / rinomina account
1. Click tab account → SwitchToAccountAsync
2. Tasto destro → "Rename" → InputBox → UpdateAccountNameAsync
3. AccountManager.SwitchAccountAsync: imposta IsActive, salva
4. MainWindow aggiorna visibilità WebView2 e stile tab

### Chiusura a tray
1. Click pulsante chiudi → `Me.Hide()` (nasconde finestra)
2. Click ✕ nel system tray context menu → `ExitApplication()` → _allowExit=True → dispose tray → Application.Shutdown()

### Apertura impostazioni
1. Click ingranaggio
2. → _accountManager.IsDialogOpen = True
3. → Apre SettingsWindow ShowDialog
4. Alla chiusura: ApplyWpfTheme(), UpdateAccountTabStyling()
5. → _accountManager.IsDialogOpen = False

---

## 7. CRITICITÀ E NOTE

| Criticità | Descrizione | Stato |
|---|---|---|
| **Google Translate API non ufficiale** | Le chiamate a `translate.googleapis.com` senza API key sono un endpoint non documentato, potrebbe smettere di funzionare o rate-limitare | ~~Usata per fetch lingue e traduzioni UI~~ → **RIMOSSA** (lingue hardcoded, traduzioni pre-compilate). Ancora usata per traduzione messaggi in webview |
| ~~**Traduzioni UI one-by-one**~~ | ~~`FetchTranslations` traduce ogni chiave singolarmente (76 richieste HTTP separate)~~ | ~~**RISOLTA**: non più usata, traduzioni pre-compilate~~ |
| ~~**Messaggio di delete_account**~~ | ~~La finestra modale SettingsWindow non è tradotta: il MessageBox di conferma eliminazione usa stringa hardcoded in inglese~~ | ~~**RISOLTA**: MessageBox localizzato con chiavi `delete_account_last` e `delete_account_confirm`~~ |
| **Race condition in LoadAccountsAsync** | `SaveAccountsAsync` può essere chiamato mentre `LoadSettingsAsync` è ancora in esecuzione | ❌ **ANCORA DA FARE** |
| ~~**Assenza gestione errori WebView2**~~ | ~~Se WebView2 non è installato, l'applicazione crasha senza messaggio informativo~~ | ~~**RISOLTA**: check all'avvio con `GetAvailableBrowserVersionString()`, mostra MessageBox con link download e chiude l'app~~ |
| **IsDialogOpen non bloccante** | La variabile `IsDialogOpen` non impedisce effettivamente apertura multipla (usata solo per notifiche) | ❌ **ANCORA DA FARE** |
| **No MVVM pattern** | Codice UI e logica mescolati nei code-behind, difficile da testare e mantenere | ❌ **ANCORA DA FARE** |
| ~~**Dipendenze fisse**~~ | ~~WebView2 versione 1.0.4078.44 e UWP Notifications 7.1.3 non aggiornate automaticamente~~ | ~~Non critico, funzionano~~ |
| ~~**Spike di CPU a ogni richiesta HTTP**~~ | ~~`LoadSupportedLanguagesAsync` e `FetchTranslations` bloccano il thread UI~~ | ~~**RISOLTA**: metodi rimossi~~ |
| **Bridge token con timestamp** | `GenerateBridgeToken` usa timestamp millisecondi + random 6-digit, non è crittograficamente sicuro ma sufficiente per uso locale | ⚠️ Accettabile per uso locale |
| ~~**Localizzazione solo UI inglese**~~ | ~~Le chiavi di traduzione sono tutte in inglese; la UI è sempre basata su quelle, la traduzione UI usa Google Translate live~~ | ~~**RISOLTA**: italiano pre-compilato, UI si aggiorna dinamicamente~~ |
| **Progetto open source** | Il repository originale è `Faeq-F/whatsappPortable` su GitHub, nome "WhatsApp Portable" | ℹ️ Fork personalizzato per uso interno |
| **Tema System letto da registry** | Il rilevamento tema Windows via registry key `AppsUseLightTheme` è specifico Windows 10/11 | ℹ️ Funziona solo su Windows 10+ |
| ~~**Account list tema scuro in chiaro**~~ | ~~Gli account in SettingsWindow rimangono scuri/illeggibili in tema chiaro: FindVisualChildren su ItemsControl non trova i container perché non ancora generati~~ | ~~**RISOLTA**: styling differito con `Dispatcher.BeginInvoke(DispatcherPriority.Background)`~~ |

---

## 8. STRUTTURA FILE

```
WhatsAppVB/
├── HidaChat.sln                       ← Soluzione Visual Studio
├── HidaChat.vbproj                    ← File di progetto .NET 9 WPF
├── ANALISI_PROGETTO.md                ← Questo documento di analisi tecnica
├── Application.xaml / .vb             ← Entry point WPF + Mutex single instance
├── AssemblyInfo.vb                    ← Informazioni assembly e tema WPF
├── Constants.vb                       ← Versione (v1.0.0), metadati e costanti OTA
├── MainWindow.xaml / .vb              ← Finestra principale e tab manager
├── AccountManager.vb                  ← Gestione accounts (load/save/switch/create)
├── AppAccounts.vb                     ← Modello account (WhatsApp/Telegram/OpenClaw/Hermes)
├── TsnetManager.vb                    ← Gestione companion daemon Tailscale Mesh VPN
├── SettingsController.vb              ← Controller impostazioni e persistenza JSON
├── SettingsWindow.xaml / .vb          ← Finestra impostazioni modale
├── BulkSenderEngine.vb                ← Motore invio sequenziale automatizzato
├── BulkSenderWindow.xaml / .vb        ← Finestra invio massivo da Excel/CSV
├── BulkContactItem.vb                 ← Modello contatto per invio massivo
├── ExcelContactService.vb             ← Parser e normalizzatore Excel e CSV
├── AboutWindow.xaml / .vb             ← Finestra Informazioni su HidaChat
├── Localization.vb                    ← Dizionari localizzazione (IT, EN, FR, ES, DE)
├── JsScripts.vb                       ← Script JS iniettati in WebView2
├── UpdateChecker.vb                   ← Controllo aggiornamenti OTA via GitHub Releases
├── tsnetd/                            ← Companion daemon Tailscale Mesh VPN (Go)
│   ├── main.go                        ← Sorgente Go con stack gVisor/WireGuard
│   ├── go.mod
│   └── go.sum
├── tsnetd.exe                         ← Eseguibile companion Go compilato
├── images/                            ← Icone e asset grafici
├── manifests/winget/                  ← Manifest per Windows Package Manager
├── publish.ps1                        ← Script PowerShell di build e pubblicazione
└── data/                              ← Dati applicazione e profili WebView2 isolati (creato a runtime)
```
