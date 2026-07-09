# Analisi del Progetto WhatsAppVB

## 1. SCOPO DELL'APPLICAZIONE

**WhatsAppVB** (versione 2.4.0, alias "WhatsApp Portable") è un **client WPF desktop multipiattaforma Windows** per WhatsApp Web. Non è un'app ufficiale, bensì un **wrapper** che carica `web.whatsapp.com` all'interno di un controllo **WebView2** (Chromium Edge), aggiungendo funzionalità non disponibili nel browser standard:

- **Multi-account**: gestione simultanea di più account WhatsApp con tab separati
- **Tema scuro/chiaro personalizzato** con rilevamento automatico del tema di sistema Windows
- **Traduzione integrata dei messaggi**: hover button per tradurre singoli messaggi, traduzione batch dell'intera pagina, traduzione delle notifiche
- **Notifiche native Windows** (Toast) per i messaggi in arrivo, con click routing
- **System tray** (icona nella barra delle notifiche) con chiusura a vassoio
- **Aggiornamento automatico** con check su GitHub alla versione più recente
- **Isolamento profili**: ogni account ha una propria directory di profilo WebView2 separata

---

## 2. STACK TECNOLOGICO

| Componente | Tecnologia |
|---|---|
| Linguaggio | **VB.NET** (Visual Basic .NET) |
| Framework | **.NET 9.0** con target **Windows 10.0.19041.0** (Windows 10 20H1+) |
| UI | **WPF** (Windows Presentation Foundation) + `UseWindowsForms` per System Tray |
| Embedded Browser | **Microsoft.Web.WebView2** v1.0.4078.44 (Chromium Edge) |
| Notifiche native | **Microsoft.Toolkit.Uwp.Notifications** v7.1.3 (Toast notifications) |
| IDE | Visual Studio 2022 17.14 |
| Serializzazione | `System.Text.Json` |
| Traduzioni | **Google Translate API** non ufficiale (`translate.googleapis.com`) |
| Tema di sistema rilevato | Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` |

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
                └── SettingsWindow (dialog modale)
                      ├── Theme/Language settings
                      └── Account management
```

**Classi di servizio (core logic):**

```
SettingsController  ←→  settings.json       (persistenza impostazioni)
AccountManager      ←→  AccountManager       (gestione lista account)
WhatsAppAccount     ──  WebView2 (per-account)
UpdateChecker       ──  GitHub raw version
AppLocalizations    ──  Google Translate API (localizzazione UI + traduzione messaggi)
JsScripts           ──  JavaScript injection in WebView2
```

---

## 4. DESCRIZIONE DI OGNI FILE/CLASSE PRINCIPALE

### `WhatsAppVB.vbproj` – File di progetto
- SDK: `Microsoft.NET.Sdk`
- Output: `WinExe` (eseguibile Windows)
- Target: `net9.0-windows10.0.19041.0`
- Packages: `Microsoft.Web.WebView2` (1.0.4078.44), `Microsoft.Toolkit.Uwp.Notifications` (7.1.3)
- Importa tutti i namespace WPF standard

### `Application.xaml` / `Application.xaml.vb` – Entry point
- StartupUri punta a `MainWindow.xaml`
- Code-behind vuoto (nessuna gestione eventi applicazione)

### `MainWindow.xaml` – UI principale
- Finestra `1000x700`, `WindowStyle=None`, `AllowsTransparency=True` (bordi personalizzati con DropShadow)
- Layout a 3 righe:
  1. **TitleBar** personalizzata (icona, titolo, pulsanti traduzione pagina/ricarica/impostazioni/minimizza/chiudi)
  2. **Account bar** (`ItemsControl` orizzontale) con pulsante "+ Add Account"
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
- **System Tray**: icona dinamica (normale/notifica), doppio click toggle finestra, chiusura a tray (nasconde invece di chiudere)
- **Closing**: se `_allowExit = False`, cancella la chiusura e nasconde la finestra
- **AccountTab_Click**: switch account via `_accountManager.SwitchAccountAsync(accountId)`
- **BtnTranslatePage_Click**: esegue `window.translatePage()` JS nel WebView attivo
- **BtnReloadActiveTab_Click**: ricarica il WebView2 attivo
- **BtnSettings_Click**: apre `SettingsWindow` come dialog modale, bloccando `_accountManager.IsDialogOpen`
- **ApplyWpfTheme**: imposta colori scuri/chiari su `RootBorder` e `TitleBar`, e inietta CSS tema in tutti i WebView
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

### `WhatsAppAccount.vb` – Modello account (324 righe)
- **Proprietà**:
  - `Id` (string), `Name` (string), `IsActive` (bool)
  - `HasNotification` (bool, JsonIgnore)
  - `BridgeToken` (string, JsonIgnore) – token univoco di sicurezza per ponte JS↔VB.NET
  - `WebView` (WebView2, JsonIgnore)
  - `ActiveNotificationIds` (HashSet(Of String))
- **SharedDataDirectory**: `baseDir/data/webview/`
- **GenerateId**: `"account_" + UnixTimeMs`
- **GenerateBridgeToken**: `"bt_" + timestamp + "_" + random 6-digit`
- **SetupWebViewAsync**:
  1. Crea directory profilo `WV2Profile_{id}`
  2. Crea `CoreWebView2Environment` isolato puntando al profilo
  3. Configura: WebMessage abilitato, DevTools abilitati
  4. **NewWindowRequested**: link WhatsApp naviga nello stesso WebView, altri link si aprono nel browser di sistema
  5. **WebMessageReceived**: bridge JSON bidirezionale (canali Notification e Translation)
  6. **NavigationCompleted**: inietta bridge token, tema CSS, override notifiche JS, script di traduzione
  7. Naviga a `https://web.whatsapp.com/`
- **HandleWebMessageAsync**: verifica `bridgeToken`, smista su canale Notification o Translation
- **HandleNotificationMessageAsync**: gestisce NOTIFICATION_RECEIVED (crea Toast notification nativa) e NOTIFICATION_CLOSED
- **HandleTranslationMessageAsync**: gestisce singola traduzione (`type="BATCH_TRANSLATE"` o singola), chiama `AppLocalizations.TranslateSingle`, restituisce via `ExecuteScriptAsync` callback JS
- **UpdateWebviewLanguageAsync**: chiama `window.setTargetLanguage()` nel WebView

### `Constants.vb` – Costanti globali
- `AppVersion = "2.4.0"`
- `RemoteVersionUrl = "https://raw.githubusercontent.com/Faeq-F/whatsappPortable/main/Version"`
- `RepoReleasesUrl = "https://github.com/Faeq-F/whatsappPortable/releases"`

### `SettingsController.vb` – Controller impostazioni (411 righe)
- Implementa `INotifyPropertyChanged`
- **Proprietà**: `Theme`, `AlwaysShowTabBar`, `CheckForUpdates`, `TranslateMessageButton`, `KeepAppInEnglish`, `FullPageTranslation`, `ShowTranslateAllMessagesButton`, `TranslateNotifications`, `ShowTranslateNotificationButton`, `Language`, `Localizations` (AppLocalizations), `SupportedLanguages`, `IsTranslating`
- **Persistenza**: file `settings.json` + cache traduzioni `translations_cache.json`
- **LoadSettingsAsync**: carica da JSON, setta tutte le proprietà, carica cache lingue/traduzioni, fetch async lingue supportate da Google Translate
- **SaveThemeAsync**: salva tema, notifica cambiamento
- **UpdateLanguageAsync**: cambia lingua, carica traduzioni (da cache o fetch), notifica
- **GetBoolSetting**: helper per parsare booleani da JSON (supporta JsonElement + convert)
- **FallbackOrLoadTranslations**: carica traduzioni dalla cache o le fetch
- **LoadSupportedLanguagesAsync**: fetch lingue da Google Translate API, salva in cache
- **SaveCacheFileAsync**: salva lingue e traduzioni cache su disco

### `SettingsWindow.xaml` / `SettingsWindow.xaml.vb` – Finestra impostazioni
- Dialog modale `500x550`, Owner = MainWindow
- Sezioni: **Tema** (ComboBox System/Light/Dark), **Lingua e Traduzione** (ComboBox lingue, checkboxes), **Notifiche** (checkboxes), **Gestione Account** (ItemsControl con TextBox e pulsante Delete), **DevTools** (Debug + Check Updates)
- Stili personalizzati: chiudi, sezioni, checkbox, combobox, bottoni azione, delete
- **Loaded**: popola dropdown, setta checkbox, applica tema
- **ComboTheme_SelectionChanged**: salva tema, applica colore alla finestra
- **ComboLanguage_SelectionChanged**: aggiorna lingua, notifica tutti i WebView (`UpdateWebviewLanguageAsync`)
- **ChkSetting_Changed**: switch su nome checkbox per salvare impostazione, notifica hover state ai WebView
- **TxtAccountName_LostFocus**: rinomina account via `_accountManager.UpdateAccountNameAsync`
- **BtnDeleteAccount_Click**: conferma eliminazione account, chiama `RemoveAccountAsync`
- **BtnDevTools_Click**: apre DevTools del WebView attivo
- **BtnCheckUpdates_Click**: chiama `UpdateChecker.CheckForUpdatesAsync(force:=True)`
- **ApplyTheme**: imposta background/foreground sulla finestra e su tutti i controlli figli via `FindLogicalChildren`
- **Helper**: `FindLogicalChildren(Of T)` per navigazione logical tree

### `Localization.vb` – Sistema di localizzazione e traduzione (165 righe)

Due componenti:

- **`AppLanguages` (Module)**:
  - `IsRtl(code)`: controlla se una lingua è RTL (arabo, ebraico, farsi, urdu, ecc.)
  - `FetchSupportedLanguages()`: chiamata a `translate.googleapis.com/translate_a/l?client=gtx&hl=en` per ottenere lista lingue supportate

- **`AppLocalizations` (Class)**:
  - `EnStrings`: dizionario con tutte le chiavi di localizzazione (76 chiavi: settings, theme, manage_accounts, translate_to_lang, ecc.)
  - `[Get](key, args)`: recupera traduzione con supporto placeholder `{name}`, `{lang}`
  - `FetchTranslations(targetLang)`: traduce tutte le 76 chiavi via Google Translate API una per una (con escaping placeholder `___` per `delete_account_confirm` e prefisso per `translate_to_lang`)
  - `TranslateSingle(text, targetLang)`: traduce un singolo testo via Google Translate API
  - `TranslateTextInternal`: chiamata HTTP a `translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={targetLang}&dt=t&q={text}`, parsifica array JSON annidato per estrarre testo tradotto

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

### `UpdateChecker.vb` – Controllo aggiornamenti (96 righe)
- `_hasChecked` statico per evitare doppio check
- `CheckForUpdatesAsync(settings, accountManager, force)`: fetch versione remota da GitHub raw, confronta con `Constants.AppVersion`, se diverso mostra MessageBox con link alle release
- `FetchLatestVersionAsync()`: HTTP GET a `Constants.RemoteVersionUrl` con timeout 5s
- `ShowUpdateDialog`: MessageBox su UI thread

### `AssemblyInfo.vb`
- Attribute `ThemeInfo` per WPF (nessun dizionario tematico esterno, solo assembly)

---

## 5. LIBRERIE ESTERNE / DIPENDENZE

| Pacchetto | Versione | Utilizzo |
|---|---|---|
| `Microsoft.Web.WebView2` | 1.0.4078.44 | Controllo browser Chromium embedded |
| `Microsoft.Toolkit.Uwp.Notifications` | 7.1.3 | Toast notification native Windows 10+ |
| `System.Text.Json` | (built-in .NET 9) | Serializzazione settings, cache, messaggi bridge |
| `System.Net.Http` | (built-in) | Chiamate API Google Translate e GitHub |
| **Google Translate API** | (non ufficiale) | `translate.googleapis.com/translate_a/single` e `translate_a/l` |
| **GitHub raw** | (nessun pacchetto) | Check versione da `raw.githubusercontent.com/Faeq-F/whatsappPortable/main/Version` |
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

### Cambio account
1. Click tab account
2. AccountTab_Click → SwitchToAccountAsync(accountId)
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

| Criticità | Descrizione |
|---|---|
| **Google Translate API non ufficiale** | Le chiamate a `translate.googleapis.com` senza API key sono un endpoint non documentato, potrebbe smettere di funzionare o rate-limitare |
| **Traduzioni UI one-by-one** | `FetchTranslations` traduce ogni chiave singolarmente (76 richieste HTTP separate), molto inefficiente |
| **Messaggio di delete_account** | La finestra modale SettingsWindow non è tradotta: il MessageBox di conferma eliminazione usa stringa hardcoded in inglese |
| **Race condition in LoadAccountsAsync** | `SaveAccountsAsync` può essere chiamato mentre `LoadSettingsAsync` è ancora in esecuzione |
| **Assenza gestione errori WebView2** | Se WebView2 non è installato, l'applicazione crasha senza messaggio informativo |
| **IsDialogOpen non bloccante** | La variabile `IsDialogOpen` non impedisce effettivamente apertura multipla (usata solo per notifiche) |
| **No MVVM pattern** | Codice UI e logica mescolati nei code-behind, difficile da testare e mantenere |
| **Dipendenze fisse** | WebView2 versione 1.0.4078.44 e UWP Notifications 7.1.3 non aggiornate automaticamente |
| **Spike di CPU a ogni richiesta HTTP** | LoadSupportedLanguagesAsync e FetchTranslations bloccano il thread UI se non gestiti correttamente (usano `Await`) |
| **Bridge token con timestamp** | `GenerateBridgeToken` usa timestamp millisecondi + random 6-digit, non è crittograficamente sicuro ma sufficiente per uso locale |
| **Localizzazione solo UI inglese** | Le chiavi di traduzione sono tutte in inglese; la UI è sempre basata su quelle, la traduzione UI usa Google Translate live (e non file .resx) |
| **Progetto open source** | Il repository originale è `Faeq-F/whatsappPortable` su GitHub, nome "WhatsApp Portable" |
| **Tema System letto da registry** | Il rilevamento tema Windows via registry key `AppsUseLightTheme` è specifico Windows 10/11 |

---

## 8. STRUTTURA FILE

```
WhatsAppVB/
├── WhatsAppVB.sln
├── WhatsAppVB.vbproj
├── Application.xaml / .vb          ← Entry point WPF
├── AssemblyInfo.vb                  ← Tema WPF
├── Constants.vb                     ← Versioni e URL
├── MainWindow.xaml / .vb            ← Finestra principale (342 righe)
├── AccountManager.vb               ← Gestione multi-account (258 righe)
├── WhatsAppAccount.vb              ← Modello account + WebView bridge (324 righe)
├── SettingsController.vb           ← Impostazioni e persistenza (411 righe)
├── SettingsWindow.xaml / .vb       ← Finestra impostazioni modale (240+205 righe)
├── Localization.vb                  ← Traduzioni UI e messaggi (165 righe)
├── JsScripts.vb                     ← Script JS iniettati (606 righe — il file più lungo)
├── UpdateChecker.vb                 ← Controllo aggiornamenti (96 righe)
├── images/
│   ├── icon.ico
│   └── icon_notification.ico
├── settings.json                    ← Impostazioni utente (generato a runtime)
└── translations_cache.json          ← Cache traduzioni (generato a runtime)
```
