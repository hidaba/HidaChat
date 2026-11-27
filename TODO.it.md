# Attività da completare

[![Italiano](https://img.shields.io/badge/Lingua-Italiano-green.svg)](TODO.it.md)
[![English](https://img.shields.io/badge/Language-English-blue.svg)](TODO.md)

---

## Sicurezza, Sessioni e Affidabilità Core

- [ ] **TASK-0083** [Media] “Test connessione” mostra Online anche quando il servizio non funziona
  - **File**: `SettingsWindow.xaml.vb`, circa righe 295–348, metodo `BtnTestConnection_Click()`
  - **Problema / Esigenza**: Dopo `GetAsync()` il codice assegna `isOnline = True` senza controllare lo status HTTP. Anche 401, 403, 500 e 502 possono quindi produrre l’indicazione verde. Per OpenClaw, inoltre, questo test non aggiunge il Bearer token: l’header viene impostato soltanto nel ramo Hermes.
  - **Fix da implementare**: Distinguere almeno raggiungibilità, autenticazione e disponibilità del servizio. Usare un endpoint appropriato per la piattaforma e non considerare riuscito il test solo perché è arrivata una risposta HTTP.
  - **Test di accettazione**: Verificare risposte 200, 401, 403, 500, 502 e timeout. Credenziali errate e backend irraggiungibile devono dare indicazioni diverse, entrambe diverse da “connessione riuscita”.
  - **Priorità**: P2 (intervento successivo) | **Impatto**: Medio | **Sforzo**: Basso-Medio

- [ ] **TASK-0086** [Media] La pulizia dell’updater può cancellare una sessione di aggiornamento ancora attiva
  - **File**: `UpdateChecker.vb`, circa righe 516–545 e successive
  - **Problema / Esigenza**: La pulizia viene avviata con `Task.Run()` e non attesa. Cerca tutte le cartelle e gli archivi con i prefissi degli aggiornamenti, senza distinguere quelli vecchi da quelli attivi. Se la pulizia parte in ritardo, può interferire con i file appena creati. Due copie portabili in cartelle diverse, ma con lo stesso `%TEMP%`, possono inoltre cancellarsi reciprocamente i file temporanei.
  - **Fix da implementare**: Identificare prima le sessioni effettivamente obsolete, escludere sempre quella corrente e proteggere quelle attive con un lock. Evitare aggiornamenti concorrenti della stessa installazione.
  - **Test di accettazione**: Ritardare artificialmente la pulizia e avviare due aggiornamenti da copie distinte. Nessuna sessione deve eliminare file ancora usati dall’altra.
  - **Priorità**: P2 (intervento successivo) | **Impatto**: Medio | **Sforzo**: Medio

- [ ] **TASK-0096** [Bassa] CleanupUnusedProfilesAsync esegue cancellazione ricorsiva sincrona nel thread UI con possibile freeze all'avvio
  - **File**: `AccountManager.vb` (metodo `CleanupUnusedProfilesAsync`)
  - **Problema / Esigenza**: Il metodo ha firma asincrona ma, se invocato direttamente o se esegue `Directory.Delete(..., True)` in modo sincrono nel contesto del thread principale all'avvio, può causare blocchi e freeze dell'interfaccia utente in presenza di profili Chromium voluminosi (migliaia di file di cache, LevelDB e blob) o contesi temporaneamente.
  - **Fix da implementare**:
    - Eseguire l'intera enumerazione ed eliminazione ricorsiva all'interno di `Task.Run` fuori dal thread UI.
    - Adottare un helper di retry con backoff progressivo (es. `DeleteDirectoryWithRetryAsync`, pattern analogo a TODO #37 / #72) per gestire eventuali lock temporanei senza mai bloccare la reattività della finestra principale.
  - **Test di accettazione**: Creare profili orfani con migliaia di file nella cartella profili; all'avvio dell'applicazione la UI deve apparire istantaneamente fluida e priva di blocchi durante il cleanup in background.
  - **Priorità**: P3 (Gravità bassa) | **Impatto**: Basso-Medio | **Sforzo**: Basso

- [ ] **TASK-0098** [Bassa] Mancanza di controlli di validità e null-safety in UpdateAccountNameAsync e RemoveAccountAsync
  - **File**: `AccountManager.vb` (metodi `UpdateAccountNameAsync`, `RemoveAccountAsync`)
  - **Problema / Esigenza**:
    1. `UpdateAccountNameAsync` accetta stringhe vuote, composte solo da spazi o nomi duplicati già in uso da altri account, degradando la leggibilità delle schede.
    2. `RemoveAccountAsync` accede a `_currentAccount.Id` senza verifica preventiva `_currentAccount IsNot Nothing`, con potenziale rischio di `NullReferenceException` se la selezione corrente è in uno stato transitorio o non inizializzato.
  - **Fix da implementare**:
    - In `UpdateAccountNameAsync`, validare `newName`: scartare o rifiutare stringhe vuote/spazi (`String.IsNullOrWhiteSpace`) e nomi già esistenti nella collezione `_accounts`.
    - In `RemoveAccountAsync`, inserire il controllo di null-safety su `_currentAccount` prima di accedere alla proprietà `Id`.
  - **Test di accettazione**: Verificare che tentativi di rinominare un account con nome vuoto o duplicato vengano ignorati e che la rimozione di un account con `_currentAccount` nullo non sollevi eccezioni.
  - **Priorità**: P3 (Gravità bassa) | **Impatto**: Basso | **Sforzo**: Basso

## Invio Massivo e Traduzioni

- [ ] **TASK-0084** [Media] Il parser CSV rompe i messaggi multilinea e può scegliere il separatore sbagliato
  - **File**: `ExcelContactService.vb`, circa righe 122–131 e 200–238
  - **Problema / Esigenza**: Il file viene letto per righe fisiche e ciascuna riga viene analizzata separatamente. Un campo tra virgolette contenente un ritorno a capo viene quindi spezzato. Il rilevamento del delimitatore, inoltre, conta anche virgole e punti e virgola dentro il testo quotato. Nelle prove isolate, un contatto con un messaggio su due righe produce una riga aggiuntiva; un CSV separato da virgole viene interpretato come separato da `;` quando il messaggio ne contiene abbastanza.
  - **Fix da implementare**: Usare un parser che legga record completi, mantenendo lo stato delle virgolette tra le righe. Rilevare il separatore fuori dai campi quotati e consentire una scelta esplicita quando il risultato è ambiguo (conforme RFC 4180).
  - **Test di accettazione**: Messaggi multilinea, virgolette escape e punteggiatura devono lasciare invariati numero di contatti e contenuto del messaggio.
  - **Priorità**: P2 (intervento successivo) | **Impatto**: Medio | **Sforzo**: Basso-Medio

- [ ] **TASK-0085** [Media] La traduzione batch può associare il testo tradotto al messaggio sbagliato
  - **File**: `Localization.vb`, circa righe 364–430, metodo `TranslateBatchAsync()`
  - **Problema / Esigenza**: I messaggi vengono uniti con un separatore testuale `###` e successivamente divisi usando lo stesso separatore. Non viene verificato che il numero delle parti restituito corrisponda al numero dei messaggi. Le associazioni ottenute vengono poi inserite nella cache (es. messaggio contenente `###` che sfasa gli indici).
  - **Fix da implementare**: Preferire risposte strutturate con un identificatore per elemento. Finché si usa un separatore testuale, controllare collisioni e numero delle parti; in caso di incongruenza ripiegare sulle traduzioni individuali, senza memorizzare risultati ambigui.
  - **Test di accettazione**: Un messaggio contenente `###`, un separatore alterato e un errore HTTP non devono contaminare la cache.
  - **Priorità**: P2 (intervento successivo) | **Impatto**: Medio | **Sforzo**: Basso-Medio

## Aggiornamenti OTA e Ciclo di Vita

- [ ] **TASK-0093** [Media] Verifica SHA-256: falsi mismatch con file multipli, formato nome: hash e fail-open se manca il checksum
  - **File**: `UpdateChecker.vb` (metodo `ExtractSha256FromText`)
  - **Correlato a**: TASK-0040
  - **Problema / Esigenza**:
    1. Se la release non ha né file di checksum né hash nelle note, l'installazione procede comunque ("Verified via HTTPS transport"). Questo comportamento fail-open contraddice quanto dichiarato nell'item TASK-0040. L'assenza di hash verificabile deve essere considerata un errore (o avviso esplicito bloccante) anziché procedere silenziosamente.
    2. In `ExtractSha256FromText`, il fallback che preleva la prima stringa esadecimale da 64 caratteri rischia di estrarre un hash non pertinente in presenza di file `SHA256SUMS` multi-asset o commit SHA nelle note, bloccando l'aggiornamento per falso "mismatch".
    3. I formati di checksum del tipo `nomefile: hash` o `hash *nomefile` con prefissi relativi `./` non vengono gestiti correttamente.
  - **Fix da implementare**:
    - Rimuovere il fallback permissivo sulla prima stringa da 64 caratteri hex; estrarre l'hash solo se esplicitamente associato al nome del file zip o se nel testo è presente un unico e inequivocabile hash.
    - Supportare i formati standard di checksum (`hash nomefile`, `hash *nomefile` e `nomefile: hash`), rimuovendo prefissi `./` e caratteri escape `*`.
    - Applicare rigorosamente la policy Fail-Closed: se nessun checksum valido e univoco viene individuato per il file scaricato, bloccare l'aggiornamento con un avviso chiaro anziché consentire l'installazione non verificata.
  - **Test di accettazione**: Fornire un file `SHA256SUMS` con più file, un file con formato `nomefile: hash` e una release priva di hash. Verificare l'estrazione corretta dell'hash corrispondente allo zip e che l'assenza di checksum blocchi l'installazione con notifica di errore integrità.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Medio-Alto | **Sforzo**: Basso-Medio

- [ ] **TASK-0094** [Media] Controllo manuale aggiornamenti privo di feedback all'utente su errori di rete o assenza asset e mancato retry all'avvio
  - **File**: `UpdateChecker.vb` (metodi `CheckForUpdatesAsync`, `FetchGitHubReleaseInfoAsync`)
  - **Problema / Esigenza**:
    1. Quando l'utente preme esplicitamente "Verifica aggiornamenti" (`force = True`), errori di rete, limitazioni rate limit 403 di GitHub (60 richieste/ora non autenticate) o release senza asset ZIP terminano con un semplice `Debug.WriteLine`. L'utente non riceve alcun feedback visivo e il pulsante sembra non funzionare.
    2. La variabile `_hasChecked = True` viene impostata prima del controllo: se all'avvio si verifica un errore temporaneo di rete, il controllo viene marcato come già eseguito e non viene più ritentato fino al riavvio dell'applicazione.
  - **Fix da implementare**:
    - Quando `force = True`, mostrare sempre una MessageBox localizzata in caso di errore di connessione, risposta HTTP 403/404 da GitHub o release priva di asset ZIP.
    - Impostare `_hasChecked = True` solo ad avvenuto completamento con successo del controllo, o reimpostarla a `False` in caso di errore di rete all'avvio per consentire successivi tentativi.
  - **Test di accettazione**: Eseguire il controllo manuale da disconnessi o simulando risposta 403: verificare la comparsa della MessageBox informativa. Verificare che l'assenza iniziale di rete consenta il successivo retry del controllo.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Medio | **Sforzo**: Basso

- [ ] **TASK-0095** [Media] Confronto errato delle versioni pre-release/beta e selezione della release remota
  - **File**: `UpdateChecker.vb` (metodi `ComparePrerelease`, `IsNewerVersion`, `FetchGitHubReleaseInfoAsync`)
  - **Problema / Esigenza**:
    1. Il confronto lessicografico `String.Compare("beta10", "beta2")` produce il risultato non corretto `beta10 < beta2` poiché il carattere '1' precede '2', impedendo a chi è su `beta9` di rilevare `beta10`.
    2. Nel canale beta, l'endpoint GitHub restituisce una lista di release: il codice seleziona la prima release dell'elenco per data, che potrebbe non corrispondere alla versione con numero semantico più alto in caso di release non sequenziali o hotfix.
  - **Fix da implementare**:
    - Implementare un parsing conforme allo standard SemVer 2.0: estrarre i suffissi numerici nei tag di pre-release, confrontando i segmenti composti unicamente da cifre con comparazione numerica (`Integer.TryParse`) e i segmenti alfanumerici con comparazione lessicografica.
    - Nel canale beta, analizzare tutte le release fornite dall'API aventi uno ZIP allegato e selezionare quella con la versione semantica massima reale, anziché limitarsi alla prima riga temporale.
  - **Test di accettazione**: Validare che `v1.2.10-beta` risulti correttamente più recente di `v1.2.9-beta`. Con una lista di release GitHub fuori ordine temporale, verificare che venga selezionata la versione semantica più elevata.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Medio | **Sforzo**: Medio

- [ ] **TASK-0099** [Bassa] Messaggi MessageBox in UpdateChecker.vb hardcoded in italiano anziché multilingua (Localization.vb)
  - **File**: `UpdateChecker.vb`, `Localization.vb`
  - **Problema / Esigenza**: Diverse finestre di dialogo e messaggi di avviso/errore in `UpdateChecker.vb` contengono stringhe fisse in lingua italiana, non rispettando la lingua selezionata dall'utente nell'applicazione (EN, IT, FR, ES, DE).
  - **Fix da implementare**:
    - Rivedere tutti i richiami a `ShowMessageBox` e `ShowMessageBoxConfirm` in `UpdateChecker.vb`.
    - Aggiungere e sincronizzare le chiavi di traduzione corrispondenti in tutti i 5 dizionari ufficiali di `Localization.vb` (`EnStrings`, `ItStrings`, `FrStrings`, `EsStrings`, `DeStrings`).
    - Utilizzare `loc.Get(...)` per recuperare titoli e testi localizzati in base alla lingua impostata.
  - **Test di accettazione**: Cambiare la lingua dell'applicazione in inglese o francese; verificare che tutti i popup di `UpdateChecker.vb` appaiano tradotti nella lingua scelta.
  - **Priorità**: P3 (Gravità bassa) | **Impatto**: Basso | **Sforzo**: Basso

- [ ] **TASK-0120** [Bassa] Chiamata residua Directory.GetFiles ed enumerazione Directory.GetDirectories non utilizzata in tempDir
  - **File**: `UpdateChecker.vb`
  - **Correlato a**: TASK-0021
  - **Problema / Esigenza**:
    - Nonostante l'attività #21 sia contrassegnata come completata (`[x] TASK-0021`), nel codice sorgente è ancora presente `Directory.GetFiles(tempDir, "HidaChat.exe", SearchOption.AllDirectories)` insieme a `Directory.GetDirectories(tempDir)` assegnato a `subDirs` e mai utilizzato.
    - Questa enumerazione ridondante di directory è presente nel codice contrariamente a quanto documentato.
  - **Fix da implementare**:
    - Rimuovere l'assegnazione non utilizzata a `subDirs` ed ottimizzare la ricerca di `HidaChat.exe` in `tempDir` tramite verifica a singolo livello o risoluzione diretta del percorso.
  - **Test di accettazione**: Verificare che in `UpdateChecker.vb` non vi siano enumerazioni di directory inutilizzate e che il progetto compili senza avvisi.
  - **Priorità**: P3 (Gravità bassa) | **Impatto**: Basso | **Sforzo**: Basso

- [ ] **TASK-0121** [Bassa] Messaggio di errore sui permessi errato che suggerisce la cartella di sistema protetta C:\Programmi\HidaChat
  - **File**: `UpdateChecker.vb`, `Localization.vb`
  - **Problema / Esigenza**:
    - Quando il controllo rileva permessi di scrittura insufficienti nella cartella dell'applicazione, il messaggio di avviso suggerisce all'utente di spostare l'applicazione in `C:\Programmi\HidaChat`. In ambienti Windows standard, `C:\Programmi` (`C:\Program Files`) richiede privilegi di amministratore, riproducendo esattamente il medesimo errore di permessi e contraddicendo la natura 100% portabile di HidaChat.
  - **Fix da implementare**:
    - Aggiornare le stringhe di localizzazione in tutti i 5 dizionari di `Localization.vb` (`update_insufficient_permissions`) e il messaggio di fallback in `UpdateChecker.vb`.
    - Suggerire percorsi utente con permessi di scrittura (es. `Documenti`, `Desktop` o unità USB) e sconsigliare esplicitamente percorsi di sistema protetti come `C:\Programmi`.
  - **Test di accettazione**: Provocare l'avviso di permessi insufficienti (es. cartella di sola lettura): verificare che il messaggio raccomandi cartelle utente ed escluda `C:\Programmi`.
  - **Priorità**: P3 (Gravità bassa) | **Impatto**: Basso | **Sforzo**: Basso

- [ ] **TASK-0122** [Bassa] Gestione del fallimento di Process.Start dello script o updater dopo ForceExitForUpdate per evitare stato zombie orfano
  - **File**: `UpdateChecker.vb`
  - **Problema / Esigenza**:
    - Se l'esecuzione di `Process.Start` del processo di aggiornamento o dello script batch fallisce (es. bloccato da antivirus, policy di sicurezza che vietano l'avvio da `%TEMP%` o file mancante) dopo che `ForceExitForUpdateAsync` ha già rilasciato le WebView e chiuso l'interfaccia, l'applicazione rimane attiva in uno stato zombie inutilizzabile senza aggiornarsi né riavviarsi.
  - **Fix da implementare**:
    - Proteggere la chiamata a `Process.Start` dell'updater all'interno di un blocco try/catch prima di invocare `Environment.Exit(0)`.
    - In caso di fallimento dell'avvio, mostrare una MessageBox esplicita all'utente con l'errore riscontrato, registrare l'errore nel file di log e consentire il rilancio dell'interfaccia o la chiusura controllata.
  - **Test di accettazione**: Simulare il fallimento di `Process.Start` (es. percorso updater non valido): verificare che venga mostrata una finestra modale di errore all'utente anziché lasciare l'applicazione bloccata silenziosamente in background.
  - **Priorità**: P3 (Gravità bassa) | **Impatto**: Basso | **Sforzo**: Basso

## UI, UX e Rifiniture Visive

- [ ] **TASK-0046** [Alta] Modalità Privacy / Anti-Sbircio (Blur Mode / Screen Share Shield)
  - **Descrizione**: Aggiungere una modalità privacy ad attivazione rapida (pulsante nella barra del titolo o scorciatoia `Alt+P`) che applica una sfocatura CSS (`filter: blur(5px)`) su messaggi, anteprime recenti nella sidebar, immagini/video e nomi dei contatti.
  - **Interazione**: Il contenuto sfocato viene svelato temporaneamente e in modo fluido solo al passaggio del puntatore del mouse (`:hover`).
  - **Utilità**: Protegge la riservatezza delle conversazioni in ufficio, in luoghi pubblici o durante la condivisione dello schermo (Teams, Zoom, Google Meet).
  - **Impatto**: Alto | **Sforzo**: Basso

- [ ] **TASK-0048** [Media] Blocco dell'Applicazione con PIN / Master Password (App Lock)
  - **Descrizione**: Possibilità di proteggere HidaChat con un PIN o password all'avvio o dopo un periodo configurabile di inattività (es. 5, 15, 30 minuti), oltre alla possibilità di bloccare istantaneamente la schermata con `Ctrl+L`.
  - **Implementazione**: Overlay modale WPF che oscura la finestra e disabilita le WebView2 fino all'inserimento del PIN corretto (memorizzato con hash sicuro in `settings.json`).
  - **Impatto**: Medio-Alto | **Sforzo**: Medio

- [ ] **TASK-0049** [Media] Quick Switcher & Command Palette (`Ctrl+K`)
  - **Descrizione**: Finestra modale di ricerca e navigazione rapida a scomparsa (`Ctrl+K` o `Ctrl+P`) in stile VS Code / Ferdium per:
    - Passare al volo tra gli account attivi digitando il nome.
    - Aprire le impostazioni o cambiare lingua/tema da tastiera.
    - Attivare al volo modalità privacy, traduzioni o ricaricare la scheda.
  - **Impatto**: Medio | **Sforzo**: Medio

- [ ] **TASK-0097** [Bassa] Mancata notifica PropertyChanged di CanAddAccount in LoadAccountsAsync e CreateDefaultAccountAsync
  - **File**: `AccountManager.vb` (metodi `LoadAccountsAsync`, `CreateDefaultAccountAsync`)
  - **Problema / Esigenza**: Al termine del caricamento da `settings.json` o nella creazione dell'account predefinito, la mancata emissione tempestiva di `NotifyPropertyChanged(NameOf(CanAddAccount))` può lasciare il pulsante di aggiunta account abilitato nell'interfaccia anche se sono già presenti 3 account (limite massimo predefinito).
  - **Fix da implementare**: Assicurarsi che in tutti i percorsi di completamento di `LoadAccountsAsync` e `CreateDefaultAccountAsync` vengano sollevati `NotifyPropertyChanged(NameOf(CanAddAccount))`, `NotifyPropertyChanged(NameOf(HasExcessAccounts))` e `NotifyPropertyChanged(NameOf(ExcessAccountsCount))`.
  - **Test di accettazione**: Avviare l'applicazione con 3 account già censiti e verificare che il pulsante "+" risulti immediatamente disabilitato all'avvio.
  - **Priorità**: P3 (Gravità bassa) | **Impatto**: Basso | **Sforzo**: Basso

- [ ] **TASK-0109** [Media] Notifiche popup in-app (MessagePopup): logo chat/piattaforma, nome mittente e anteprima messaggio
  - **File**: `MessagePopup.xaml`, `MessagePopup.xaml.vb`, `AppAccounts.vb`, `Scripts/notification.js`
  - **Problema / Esigenza**: Quando si riceve un nuovo messaggio, la notifica popup grafica in-app (`MessagePopup`) deve fornire informazioni complete e immediatamente distinguibili ad un colpo d'occhio:
    1. **Logo della chat di appartenenza**: visualizzare il logo/icona del servizio/piattaforma di messaggistica a cui appartiene la chat (WhatsApp, Telegram, Discord, OpenClaw, Hermes) o l'icona/avatar della conversazione.
    2. **Nome di chi scrive**: mostrare in evidenza il nome del contatto, utente o gruppo che ha inviato il messaggio.
    3. **Messaggio**: mostrare chiaramente il corpo del testo o l'anteprima del messaggio ricevuto con corretta formattazione.
  - **Soluzione proposta / Fix da implementare**:
    - Revisionare il layout di `MessagePopup.xaml` per posizionare in modo chiaro ed armonico il logo della piattaforma/chat di appartenenza accanto all'avatar del contatto.
    - Assicurare in `AppAccounts.vb` e nello script `Scripts/notification.js` che all'evento di notifica vengano estratti e passati al costruttore di `MessagePopup` tutti i metadati necessari (mittente, testo effettivo del messaggio e indicatore della chat di provenienza).
    - Verificare leggibilità del testo e troncamento corretto con ellissi in caso di messaggi lunghi.
  - **Test di accettazione**: Alla ricezione di un nuovo messaggio su qualsiasi account configurato (WhatsApp, Telegram, ecc.), verificare che il popup toast mostri il logo distintivo della chat/piattaforma, il nome del mittente e l'anteprima del testo del messaggio.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Medio-Alto | **Sforzo**: Basso

- [ ] **TASK-0110** [Media] Correzione contrasto e colori menu a tendina "Fornitore di Traduzioni" in SettingsWindow
  - **File**: `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`
  - **Problema / Esigenza**: Nella schermata delle Impostazioni (`SettingsWindow`), il menu a tendina per la selezione del Fornitore di Traduzioni (`ComboTranslationProvider`) risulta illeggibile. A differenza degli altri menu a tendina (es. Tema, Lingua, Modalità DND, Numero Massimo Account, Correttore Ortografico, Timeout Ibernazione, Limite Cache Disco), `ComboTranslationProvider` non è gestito all'interno del metodo `ApplyTheme()`, causando contrasto errato tra testo e sfondo (es. testo chiaro su sfondo bianco in tema chiaro).
  - **Soluzione proposta / Fix da implementare**:
    - Aggiornare `ApplyTheme()` in `SettingsWindow.xaml.vb` applicando a `ComboTranslationProvider.Background` e `ComboTranslationProvider.Foreground` la stessa logica coerente e gli stessi pennelli (`fgBrush`, sfondo scuro `#2a3942` o chiaro `#ffffff`) utilizzati per gli altri controlli `ComboBox`.
    - Verificare che i `ComboBoxItem` interni (`CbiProviderGoogle`, `CbiProviderLibreTranslate`, `CbiProviderOllama`, `CbiProviderOpenAi`) ereditino correttamente lo stile o ricevano lo stile `CustomComboBoxStyle` con colori di testo e hover coerenti con il resto delle impostazioni.
  - **Test di accettazione**: Aprire la finestra Impostazioni sia con tema Scuro che con tema Chiaro; verificare che le voci del menu a tendina "Fornitore di Traduzioni" abbiano lo stesso colore di testo e la stessa piena leggibilità di tutti gli altri menu a tendina presenti.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Medio | **Sforzo**: Basso

- [ ] **TASK-0111** [Media] Adattamento cromatico e leggibilità della schermata / sezione Tailscale
  - **File**: `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `TailscaleConnectionErrorDialog.xaml`, `TailscaleConnectionErrorDialog.xaml.vb`
  - **Problema / Esigenza**: La sezione integrata di Tailscale (`tsnet`) nelle Impostazioni e la finestra di dialogo modale di stato/errore connettività (`TailscaleConnectionErrorDialog`) presentano caratteri poco visibili e colorazioni scure fisse (`#202c33`, `#111b21`, `#8696a0`) che non si adattano al tema dell'applicazione. Di conseguenza, i testi esplicativi, le etichette di autenticazione e lo stato del nodo risultano difficili da leggere e visivamente disallineati rispetto al resto dell'interfaccia.
  - **Soluzione proposta / Fix da implementare**:
    - In `SettingsWindow.xaml` e `SettingsWindow.xaml.vb`:
      - Adattare dinamicamente il contenitore della sezione Tailscale nel metodo `ApplyTheme()` (sfondo, bordi, testi esplicativi ed etichetta AuthKey) utilizzando i colori standard dei blocchi impostazioni.
      - Migliorare il contrasto del badge di stato (`BadgeTsnetStatus`, `TxtTsnetStatus`) e del campo `TxtTsnetAuthKey` affinché i caratteri siano nitidi sia in tema scuro che in tema chiaro.
    - In `TailscaleConnectionErrorDialog.xaml` e `.vb`:
      - Rivedere la palette cromatica (sfondo finestra, bordi, testi secondari `TxtHeader` e `TxtMessage`, pulsanti di azione) garantendo contrasto elevato conforme agli standard di accessibilità e piena coerenza stilistica con l'UI di HidaChat.
  - **Test di accettazione**: Verificare la sezione Tailscale nelle Impostazioni e aprire il dialog di stato/errore Tailscale con entrambi i temi (scuro e chiaro): accertarsi che tutti i testi e le etichette siano chiaramente visibili e leggibili con colorazioni armoniche e coerenti.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Medio | **Sforzo**: Basso

- [ ] **TASK-0112** [Media] Contrasto e visibilità del titolo "HidaChat" nella finestra Informazioni (AboutWindow)
  - **File**: `AboutWindow.xaml`, `AboutWindow.xaml.vb`
  - **Problema / Esigenza**: Nella finestra Informazioni sull'applicazione (`AboutWindow`), il titolo principale "HidaChat" risulta poco visibile e con contrasto insufficiente. Nel layout XAML, l'elemento `<TextBlock Text="HidaChat" .../>` ha un colore fisso chiaro (`#ffffff` o grigio chiaro) e non dispone di un identificatore `x:Name` per essere aggiornato dinamicamente dal metodo `ApplyTheme()`, rendendolo scarsamente leggibile e non coerente quando viene applicato il tema dell'interfaccia.
  - **Soluzione proposta / Fix da implementare**:
    - Assegnare un identificatore esplicito (es. `x:Name="TxtAppTitle"`) al `TextBlock` dell'intestazione in `AboutWindow.xaml`.
    - In `AboutWindow.xaml.vb`, aggiornare il metodo `ApplyTheme()` per sincronizzare la proprietà `Foreground` del titolo sia per il tema Scuro (bianco brillante `#ffffff` o `#e9edef`) sia per il tema Chiaro (colore scuro ad alto contrasto `#111b21`), garantendo massima nitidezza e piena leggibilità.
  - **Test di accettazione**: Aprire la finestra Informazioni con entrambi i temi (scuro e chiaro): verificare che il titolo "HidaChat" sia ben marcato, nitido e perfettamente leggibile su qualsiasi sfondo.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Basso-Medio | **Sforzo**: Basso

## Integrazione Piattaforme

- [ ] **TASK-0051** [Alta] Estensione a Nuove Piattaforme Web (Microsoft Teams, Slack)
  - **Descrizione**: Estendere l'architettura `AppAccounts` per consentire all'utente di aggiungere schede per **Microsoft Teams** (`https://teams.microsoft.com/v2/`) o **Slack** (`https://app.slack.com/client`) con icone vettoriali e temi dedicati. (Nota: integrazione Discord completata e verificata in TASK-0115).
  - **Impatto**: Alto | **Sforzo**: Medio

- [ ] **TASK-0113** [Bassa] Integrazione e supporto nativo per la chat Microsoft Teams (Teams Web)
  - **File**: `PlatformDefinition.vb`, `AppAccounts.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Scripts/notification.js`
  - **Problema / Esigenza**: Consentire agli utenti di aggiungere e gestire account per **Microsoft Teams** direttamente all'interno di HidaChat, affiancandolo a WhatsApp, Telegram e Discord per una gestione unificata della messaggistica di lavoro e collaborazione.
  - **Specifiche & Soluzione da implementare**:
    - **Modello Piattaforma (`PlatformDefinition.vb`)**:
      - Aggiungere il valore `Teams` (o `MicrosoftTeams`) all'enumerazione `ChatPlatform`.
      - Creare la classe `TeamsPlatformDefinition` con URL predefinito (`https://teams.microsoft.com/v2/`), colore distintivo brand indigo/viola (`#505ac9` / `#6264a7`) e tracciato vettoriale SVG dell'icona Teams per tab, popup e indicatori.
    - **Isolamento & Autenticazione (`AppAccounts.vb`)**:
      - Profilo dedicato e isolato in `data/webview/` per preservare login Microsoft 365 / Entra ID e sessioni aziendali/personali senza contaminazione tra account.
      - Gestione dei popup di autenticazione SSO (`NewWindowRequested`) per il corretto completamento dell'accesso Microsoft OAuth.
    - **Notifiche & Badge non letti (`Scripts/notification.js`)**:
      - Intercettazione del contatore messaggi e menzioni non lette (monitoraggio titolo pagina `(N) Teams` o selettori badge attività) e propagazione a Windows Toast e al popup in-app `MessagePopup`.
    - **Interfaccia Utente (`SettingsWindow.xaml`)**:
      - Aggiunta della voce "Microsoft Teams" nel menu a tendina di selezione piattaforma account.
  - **Test di accettazione**: Aggiungere un nuovo account impostando la piattaforma "Microsoft Teams", effettuare il login su Teams Web, inviare/ricevere messaggi e verificare la corretta comparsa dei badge di notifica e del popup con icona Teams.
  - **Priorità**: P3 (Gravità bassa) | **Impatto**: Medio-Alto | **Sforzo**: Medio

- [ ] **TASK-0114** [Bassa] Integrazione e supporto nativo per la chat Facebook Messenger (Messenger Web)
  - **File**: `PlatformDefinition.vb`, `AppAccounts.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Scripts/notification.js`
  - **Problema / Esigenza**: Aggiungere il supporto a **Facebook Messenger** (`https://www.messenger.com/`) come piattaforma di chat integrata in HidaChat, consentendo agli utenti di comunicare via Messenger in un'istanza WebView2 isolata e portatile.
  - **Specifiche & Soluzione da implementare**:
    - **Modello Piattaforma (`PlatformDefinition.vb`)**:
      - Aggiungere il valore `Messenger` all'enumerazione `ChatPlatform`.
      - Creare la classe `MessengerPlatformDefinition` con URL predefinito `https://www.messenger.com/`, colore identificativo blu/gradient (`#0084ff` / `#00b2ff`) e icona SVG vettoriale ufficiale (nuvola con fulmine).
    - **Isolamento & Autenticazione (`AppAccounts.vb`)**:
      - Profilo WebView2 isolato al 100% per sessioni indipendenti da altri servizi Meta (es. WhatsApp Web).
      - Gestione sicura dei cookie e salvataggio persistente in `data/webview/`.
    - **Notifiche & Badge non letti (`Scripts/notification.js`)**:
      - Rilevamento continuo dei messaggi non letti (osservatore su mutazioni titolo `(N) Messenger` o badge DOM) e bridging verso notifiche di sistema e toast personalizzati in-app.
    - **Interfaccia Utente (`SettingsWindow.xaml`)**:
      - Aggiunta di "Facebook Messenger" nell'elenco delle piattaforme selezionabili durante la creazione/modifica account.
  - **Test di accettazione**: Creare un account Messenger, accedere a `messenger.com`, verificare la ricezione di messaggi, l'incremento del badge non letti e la corretta visualizzazione di notifiche e avatar/logo della piattaforma.
  - **Priorità**: P3 (Gravità bassa) | **Impatto**: Medio-Alto | **Sforzo**: Medio

- [ ] **TASK-0116** [Alta] Soppressione del popup ripetitivo di errore Tailscale all'annullamento e banner agente offline per Hermes/OpenClaw
  - **File**: `AppAccounts.vb`, `TailscaleConnectionErrorDialog.xaml`, `TailscaleConnectionErrorDialog.xaml.vb`, `MainWindow.xaml.vb`, `Localization.vb`
  - **Problema / Esigenza**: Quando Tailscale (`tsnet`) è attivo e funzionante ma l'endpoint dell'assistente remoto (Hermes Agent o OpenClaw Gateway) non è raggiungibile o è offline (es. servizio demone arrestato, porta chiusa, timeout gateway 502/504), compare la finestra modale `TailscaleConnectionErrorDialog`. Se l'utente clicca su "Annulla" / "Chiudi" (`BtnClose`), il popup si chiude ma i controlli ciclici in background o gli eventi di navigazione successivi rieseguono la diagnosi riaprendo immediatamente il popup, generando un loop fastidioso e bloccante. Inoltre, la scheda dell'account rimane su una pagina bianca o su un errore generico del browser senza una chiara indicazione nello spazio di lavoro che l'agente è offline.
  - **Specifiche & Soluzione da implementare**:
    - **Soppressione dei controlli ripetitivi su Annulla (`AppAccounts.vb`, `TailscaleConnectionErrorDialog.xaml.vb`)**:
      - Introdurre un flag di soppressione a livello di account (es. `_suppressTailscaleErrorPrompt As Boolean`).
      - Quando l'utente annulla il dialogo (`dialog.RetryRequested` = False e `dialog.OpenSettingsRequested` = False), impostare il flag di soppressione per impedire la ricomparsa automatica del popup modale per quell'account fino a una nuova azione esplicita dell'utente.
      - Resettare il flag di soppressione quando l'utente richiede una ricarica manuale (tasto Ricarica nella toolbar o scorciatoie F5/Ctrl+R) o quando modifica l'URL dell'account nelle Impostazioni.
    - **Inserimento banner "Agente Offline" nella pagina (`AppAccounts.vb`, `Localization.vb`)**:
      - Quando il controllo di connessione fallisce e l'utente annulla il popup, visualizzare nella WebView2 di Hermes o OpenClaw un banner/overlay integrato con il tema dell'applicazione:
        - Intestazione: "Agente Offline / Non Raggiungibile" (localizzato in IT, EN, FR, ES, DE in `Localization.vb`).
        - Dettaglio: segnala che Tailscale è connesso, ma il servizio dell'agente non risponde all'indirizzo configurato.
        - Azioni interattive: pulsante "Riprova connessione" (resetta la soppressione e riavvia la diagnosi) e pulsante "Impostazioni account" (apre le impostazioni per verificare URL e token).
  - **Test di accettazione**: Configurare un account Hermes o OpenClaw con Tailscale attivo verso un endpoint non raggiungibile. Alla comparsa di `TailscaleConnectionErrorDialog`, premere "Annulla": verificare che il dialogo si chiuda e non ricompaia più ciclicamente, e verificare che nella scheda dell'account compaia il banner "Agente Offline" con pulsanti funzionanti per riprovare la connessione o accedere alle impostazioni.
  - **Priorità**: P1 (Gravità alta) | **Impatto**: Alto | **Sforzo**: Medio

## Modernizzazione UI (Fluent Design System)

- [ ] **TASK-0131** [Media] Fase 2 Modernizzazione UI: Migrare i dialoghi modali al Fluent Design System
  - **File**: `AboutWindow.xaml`, `AboutWindow.xaml.vb`, `TranslationPrivacyConsentDialog.xaml`, `TranslationPrivacyConsentDialog.xaml.vb`, `TailscaleConnectionErrorDialog.xaml`, `TailscaleConnectionErrorDialog.xaml.vb`, `UpdateProgressDialog.xaml`, `UpdateProgressDialog.xaml.vb`, `ShutdownProgressDialog.xaml`, `ShutdownProgressDialog.xaml.vb`, `StorageRecoveryProgressDialog.xaml`, `StorageRecoveryProgressDialog.xaml.vb`
  - **Correlato a**: TASK-0130, TASK-0112 (`docs/ui/audit-report.md` — Fase 2)
  - **Problema / Esigenza**: Tutti i 6 dialoghi modali esistenti utilizzano colori hex hardcoded (83 occorrenze complessive), stili pulsante duplicati, `AllowsTransparency="True"` + `DropShadowEffect` e risultano privi di `UseLayoutRounding`/`SnapsToDevicePixels` (eccetto `AboutWindow`) e scorciatoie da tastiera uniformi (`Invio`/`Esc`). `AboutWindow.xaml` presenta inoltre un titolo XAML fisso in italiano e manca di `MinWidth`.
  - **Soluzione da implementare**:
    - Unire `UI/Themes/FluentDark.xaml` + `UI/Themes/ModernControls.xaml` e registrare ogni dialogo con `ThemeManager.Instance.RegisterWindow(Me)` (deregistrandolo in `OnClosed`).
    - Adottare `ModernCard`, `ModernButton` e i pennelli semantici `{DynamicResource ...}`, eliminando tutti i colori hex hardcoded.
    - Abilitare `UseLayoutRounding="True"`, `SnapsToDevicePixels="True"`, la navigabilità da tastiera (rimuovendo `Focusable="False"` dai pulsanti interattivi) e le scorciatoie `Invio`/`Esc`.
    - Sostituire i titoli XAML predefiniti in italiano con titoli neutri in inglese prima della localizzazione a runtime.
  - **Test di accettazione**: Aprire tutti e 6 i dialoghi modali in tema Chiaro e Scuro con scaling 100%–200%; verificare nitidezza, contrasto WCAG AA, navigazione da tastiera e 0 colori hex hardcoded tramite `scripts/ui/Find-HardcodedUiIssues.ps1`.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Medio | **Sforzo**: Basso-Medio

- [ ] **TASK-0132** [Media] Fase 3 Modernizzazione UI: Migrare `MessagePopup` ai token Fluent `ToastNotification` e al tema dinamico
  - **File**: `MessagePopup.xaml`, `MessagePopup.xaml.vb`
  - **Correlato a**: TASK-0130 (`docs/ui/audit-report.md` — Fase 3)
  - **Problema / Esigenza**: `MessagePopup.xaml` utilizza colori hardcoded per il tema scuro (`#1f2c34`, `#2f3e46`, `#e9edef`, `#8696a0`), manca di `UseLayoutRounding`/`SnapsToDevicePixels` e non si adatta dinamicamente ai temi Chiaro/Scuro/Sistema né ai colori accent personalizzati.
  - **Soluzione da implementare**:
    - Unire `UI/Themes/FluentDark.xaml` + `UI/Themes/ModernControls.xaml` e vincolare superfici, bordi e tipografia di `MessagePopup` ai token `{DynamicResource ...}` gestiti da `ThemeManager`.
    - Abilitare `UseLayoutRounding="True"` e `SnapsToDevicePixels="True"`, preservando il posizionamento multi-monitor sicuro e i badge colore delle piattaforme.
  - **Test di accettazione**: Simulare notifiche in arrivo in modalità Chiara e Scura su configurazioni mono e multi-monitor; verificare adattamento dinamico del tema, icone vettoriali nitide e chiusura automatica senza leak di timer.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Medio | **Sforzo**: Basso

- [ ] **TASK-0133** [Media] Fase 4 Modernizzazione UI: Migrare `BulkSenderWindow` ai controlli Fluent (`ModernDataGrid`, `SearchBox`, `LoadingOverlay`, `EmptyState`)
  - **File**: `BulkSenderWindow.xaml`, `BulkSenderWindow.xaml.vb`
  - **Correlato a**: TASK-0130 (`docs/ui/audit-report.md` — Fase 4)
  - **Problema / Esigenza**: `BulkSenderWindow.xaml` contiene 80 occorrenze di colori hex hardcoded, stili pulsante duplicati (`PrimaryButtonStyle`, `SecondaryButtonStyle`, `TagButtonStyle`), `AllowsTransparency="True"` + `DropShadowEffect` e manca di `UseLayoutRounding`, tematizzazione `DynamicResource` e stati vuoti/caricamento standardizzati.
  - **Soluzione da implementare**:
    - Registrare `BulkSenderWindow` su `ThemeManager.Instance` e sostituire i colori hardcoded con i token `{DynamicResource ...}`.
    - Sostituire i controlli legacy con `ModernDataGrid` (virtualizzato), `SearchBox`, `ModernButton`, `StatusBadge`, `LoadingOverlay` ed `EmptyState`, preservando integralmente la logica di invio massivo e tutti gli eventi esistenti.
    - Rimuovere `AllowsTransparency="True"` + `DropShadowEffect` passando a composizione accelerata hardware e abilitare `UseLayoutRounding="True" SnapsToDevicePixels="True"`.
  - **Test di accettazione**: Caricare contatti Excel/CSV, filtrare con `SearchBox`, verificare cambio tema Chiaro/Scuro in tempo reale, ridimensionamento fino a `880x600` e piena operatività del flusso di invio massivo.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Alto | **Sforzo**: Medio

- [ ] **TASK-0134** [Media] Fase 5 Modernizzazione UI: Migrare `SettingsWindow` al `ThemeManager` centralizzato e ai controlli Fluent standardizzati
  - **File**: `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`
  - **Correlato a**: TASK-0130, TASK-0129 (`docs/ui/audit-report.md` — Fase 5)
  - **Problema / Esigenza**: Sebbene TASK-0129 abbia introdotto pennelli dinamici locali in `SettingsWindow`, `SettingsWindow.xaml` e `SettingsWindow.xaml.vb` contengono ancora 133 occorrenze di colori hex hardcoded (91 in `ApplyTheme()` nel code-behind + 42 in XAML), mancano di `UseLayoutRounding`/`SnapsToDevicePixels` e duplicano `CardBorderStyle`, `NavBtnStyle`, `CustomComboBoxStyle` e `InputTextBoxStyle`.
  - **Soluzione da implementare**:
    - Collegare `SettingsWindow` a `ThemeManager.Instance` e `UI/Themes/ModernControls.xaml`, sostituendo la popolazione manuale dei colori hex in `ApplyTheme()` con i token centralizzati.
    - Adottare progressivamente `NavigationSidebar`, `ModernCard`, `ModernTextBox`, `ModernComboBox`, `ModernButton` e `DialogManager.ShowConfirm` per le azioni distruttive (es. eliminazione account o pulizia cache).
    - Rimuovere `Focusable="False"` dai pulsanti di navigazione/azione e aggiungere `UseLayoutRounding="True" SnapsToDevicePixels="True"`.
  - **Test di accettazione**: Navigare tra tutte e 7 le categorie di impostazioni nei temi Chiaro, Scuro e Sistema con accent personalizzati; verificare zero regressioni visive, piena accessibilità da tastiera e superamento di tutti i test in `SessionTests`.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Alto | **Sforzo**: Medio

- [ ] **TASK-0135** [Media] Fase 6 Modernizzazione UI: Modernizzare `MainWindow` (dimensioni minime, DPI rounding, token tema e valutazione `WindowChrome`)
  - **File**: `MainWindow.xaml`, `MainWindow.xaml.vb`
  - **Correlato a**: TASK-0130 (`docs/ui/audit-report.md` — Fase 6)
  - **Problema / Esigenza**: `MainWindow.xaml` e `MainWindow.xaml.vb` contengono 53 occorrenze di colori hex hardcoded, mancano di `MinWidth`/`MinHeight` e `UseLayoutRounding`/`SnapsToDevicePixels`, usano `Focusable="False"` sui pulsanti header/tab e si basano su `AllowsTransparency="True"` + `DropShadowEffect`, introducendo overhead di composizione software attorno all'host WebView2.
  - **Soluzione da implementare**:
    - Impostare `MinWidth="800"` e `MinHeight="560"`, oltre a `UseLayoutRounding="True"` e `SnapsToDevicePixels="True"`.
    - Collegare barra del titolo, barra delle schede account e bordo radice ai token `{DynamicResource ...}` di `ThemeManager`.
    - Valutare ed eseguire la migrazione da `AllowsTransparency="True"` + `DropShadowEffect` a `WindowChrome` accelerato hardware, verificando rigorosamente airspace WebView2, fluidità di ridimensionamento, massimizzazione multi-monitor e ripristino dalla system tray.
  - **Test di accettazione**: Ridimensionare `MainWindow` su scaling 100%–200% e configurazioni multi-monitor, cambiare tema in tempo reale, verificare che tutte le schede WebView2 (WhatsApp, Telegram, Discord, OpenClaw, Hermes) renderizzino senza sfarfallii o tagli e verificare minimizzazione/ripristino nella tray.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Alto | **Sforzo**: Medio-Alto

- [ ] **TASK-0136** [Bassa] Fase 7 Test UI Automatizzati: Aggiungere progetto opzionale di smoke test UI con `FlaUI.UIA3`
  - **File**: `Tests/HidaChat.UiTests/` (nuovo progetto di test, escluso dallo ZIP portabile di release)
  - **Correlato a**: TASK-0130 (`docs/ui/dependencies.md`, `docs/ui/audit-report.md` — Fase 7)
  - **Problema / Esigenza**: Attualmente la verifica UI si basa su test unitari (`HidaChat.SessionTests`) e audit statici PowerShell (`scripts/ui/`). L'aggiunta di smoke test desktop automatizzati tramite UI Automation (`FlaUI.UIA3`, licenza MIT) consentirà di intercettare automaticamente regressioni di layout, navigazione da tastiera e cambio tema.
  - **Soluzione da implementare**:
    - Creare un progetto separato `Tests/HidaChat.UiTests/HidaChat.UiTests.vbproj` con riferimento a `FlaUI.UIA3` (MIT), completamente esterno a `HidaChat.vbproj` così da non aggiungere alcuna DLL al pacchetto portabile.
    - Implementare smoke test automatizzati che avviino `HidaChat.exe --ui-showcase`, alternino i temi Chiaro/Scuro, filtrino `ModernDataGrid` tramite `SearchBox`, aprano/annullino `ConfirmDialog` con `Esc` e verifichino il confronto screenshot.
  - **Test di accettazione**: Eseguire la suite `HidaChat.UiTests` su Windows; verificare il superamento di tutti gli smoke test UIA3 e confermare che lo ZIP generato da `publish.ps1` escluda gli assembly di test.
  - **Priorità**: P3 (Gravità bassa) | **Impatto**: Medio | **Sforzo**: Medio
