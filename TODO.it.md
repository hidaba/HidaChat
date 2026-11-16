# Attività da completare

[![Italiano](https://img.shields.io/badge/Lingua-Italiano-green.svg)](TODO.it.md)
[![English](https://img.shields.io/badge/Language-English-blue.svg)](TODO.md)

---

## Sicurezza, Sessioni e Affidabilità Core

- [ ] **TASK-0080** [Alta] Stop e riavvio possono sovrapporre due esecuzioni dell’invio massivo
  - **File**: `BulkSenderWindow.xaml.vb`, circa righe 343–438; `BulkSenderEngine.vb`, metodi `Cancel()` e `RunAsync()`
  - **Problema / Esigenza**: Il pulsante Stop richiede l’annullamento e riabilita immediatamente Avvio, senza attendere la conclusione dell’esecuzione precedente. Quest’ultima può ancora trovarsi dentro una chiamata asincrona. Inoltre, anche un’esecuzione interrotta passa dal riepilogo “Completato”, che porta la barra al 100%.
  - **Fix da implementare**: Introdurre uno stato “Arresto in corso” e un `CancelAsync()` che attenda il task attivo. Il motore deve impedire esecuzioni concorrenti indipendentemente dalla UI. Separare gli esiti Completed, Cancelled e Failed.
  - **Test di accettazione**: Premere Stop durante una chiamata JavaScript e tentare subito un nuovo avvio. Non devono partire due elaborazioni sulla stessa WebView; il riepilogo deve conservare i conteggi reali e indicare l’interruzione.
  - **Priorità**: P1 (da sistemare prima della prossima release) | **Impatto**: Alto | **Sforzo**: Basso-Medio

- [ ] **TASK-0081** [Alta] Un blocco di tsnetd può impedire indefinitamente la chiusura
  - **File**: `TsnetManager.vb`, circa righe 247–263 e 386–420
  - **Problema / Esigenza**: `SendCommandAsync()` attende il semaforo e una riga da stdout senza timeout. `ShutdownAsync()` chiama questo metodo prima di arrivare a `WaitForExit(2000)`. Di conseguenza, se il daemon resta vivo ma non risponde, il limite di due secondi non viene mai raggiunto. Anche un comando precedente bloccato può trattenere il semaforo e impedire l’invio di shutdown.
  - **Fix da implementare**: Applicare un timeout cancellabile all’intera operazione, compresa l’acquisizione del semaforo. Lo shutdown deve avere una scadenza complessiva indipendente dal canale comandi. Alla scadenza, chiudere il canale e gestire il processo senza lasciare letture pendenti.
  - **Test di accettazione**: Usare un daemon di prova che non risponde più su stdout. Chiusura e aggiornamento devono terminare o fallire con un errore chiaro entro il limite previsto, non restare appesi.
  - **Priorità**: P1 | **Impatto**: Alto | **Sforzo**: Medio

- [ ] **TASK-0082** [Media] La verifica dei certificati HTTPS viene disattivata
  - **File**: `tsnetd/main.go`, circa righe 259–269; `SettingsWindow.xaml.vb`, circa righe 320–322
  - **Problema / Esigenza**: Nel transport del proxy viene impostato `InsecureSkipVerify = true`; il test di connessione usa inoltre una callback che accetta sempre il certificato. La verifica HTTPS del servizio viene quindi aggirata nei percorsi interessati. In Go questa impostazione disabilita la normale verifica della catena e del nome del server. La protezione del tunnel Tailscale è distinta dalla verifica dell’identità HTTPS del servizio.
  - **Fix da implementare**: Validazione standard come comportamento predefinito. Per server con certificati privati, consentire una CA esplicita o il pinning; evitare un bypass globale e silenzioso.
  - **Test di accettazione**: Un certificato scaduto, non attendibile o con hostname errato deve produrre un errore TLS distinguibile da un semplice problema di raggiungibilità.
  - **Priorità**: P2 (intervento successivo) | **Impatto**: Medio | **Sforzo**: Medio

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

- [ ] **TASK-0090** [Alta] MigrateOrphanProfile cancella o sovrascrive un profilo valido
  - **File**: `AccountManager.vb` (metodo `MigrateOrphanProfile`)
  - **Problema / Esigenza**: Se sul filesystem coesistono sia la cartella `WV2Profile_` (profilo orfano o legacy senza ID specificato) sia la cartella `WV2Profile_{id}` associata al primo account, una logica di migrazione errata o distruttiva rischia di eliminare il profilo valido del primo account per spostarvi sopra la cartella orfana, causando la perdita della sessione reale e attiva dell'utente.
  - **Fix da implementare**:
    - All'interno di `MigrateOrphanProfile`, verificare preliminarmente l'esistenza della cartella di destinazione `WV2Profile_{id}` (e di eventuali snapshot `.previous`).
    - Se la cartella di destinazione esiste già, interrompere immediatamente la migrazione (`Continue For` / `Return`), lasciando intatto il profilo valido esistente.
    - Lasciare che la cartella orfana venga gestita in sicurezza dai meccanismi di cleanup non distruttivi o conservata come backup.
  - **Test di accettazione**: Creare manualmente sia `data/webview/WV2Profile_` che `data/webview/WV2Profile_{id}` con file differenti. All'avvio dell'applicazione, il contenuto di `WV2Profile_{id}` deve rimanere inalterato e non essere sostituito o cancellato dall'orfano.
  - **Priorità**: P1 (Gravità alta) | **Impatto**: Alto | **Sforzo**: Basso

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

- [ ] **TASK-0091** [Alta] Marker di versione scritto prima della copia robocopy e residui temporanei dello script batch
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

- [ ] **TASK-0092** [Media] Script batch di aggiornamento fragile (parentesi nei percorsi, unità root USB e gestione errori)
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

- [ ] **TASK-0093** [Media] Verifica SHA-256: falsi mismatch con file multipli e fail-open se manca il checksum
  - **File**: `UpdateChecker.vb` (metodo `ExtractSha256FromText`)
  - **Problema / Esigenza**: Se il nome del file ZIP non viene individuato esattamente nel testo del checksum (ad esempio perché indicato come `./HidaChat.zip` o con prefissi di percorso), un fallback che preleva la prima stringa esadecimale da 64 caratteri del testo rischia di estrarre un hash non pertinente (ad esempio l'hash di un altro archivio in un file `SHA256SUMS` multi-asset o un commit SHA nelle note di rilascio), bloccando l'aggiornamento per falso "mismatch". Se invece non viene trovato alcun hash e non viene applicato un fail-closed rigoroso, l'update rischierebbe di procedere senza alcun controllo di integrità.
  - **Fix da implementare**:
    - Normalizzare il matching del filename rimuovendo prefissi relativi come `./` o caratteri escape `*`.
    - Se è stato specificato un `filename`, ignorare il fallback generico arbitrario o accettarlo esclusivamente se nel testo è presente un unico e inequivocabile hash da 64 caratteri.
    - Applicare rigorosamente la policy Fail-Closed: se nessun checksum valido e univoco viene individuato per il file scaricato, bloccare l'aggiornamento e informare l'utente con un messaggio di errore chiaro.
  - **Test di accettazione**: Fornire un file `SHA256SUMS` contenente gli hash di più file e verificare che venga estratto solo l'hash associato allo zip di HidaChat. Verificare che l'assenza di checksum blocchi l'aggiornamento con avviso e senza tentare l'installazione non verificata.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Medio-Alto | **Sforzo**: Basso-Medio

- [ ] **TASK-0094** [Media] Controllo manuale aggiornamenti privo di feedback all'utente su errori di rete o assenza asset
  - **File**: `UpdateChecker.vb` (metodi `CheckForUpdatesAsync`, `FetchGitHubReleaseInfoAsync`)
  - **Problema / Esigenza**: Quando l'utente preme esplicitamente il pulsante "Verifica aggiornamenti" (`force = True`), se l'API di GitHub risponde con un errore (rate limit 403, offline, endpoint non raggiungibile) oppure se la release remota non ha un archivio ZIP allegato tra gli asset, l'esecuzione termina con un semplice `Debug.WriteLine`. L'utente non riceve alcun feedback visivo e il pulsante appare non funzionante.
  - **Fix da implementare**:
    - Quando `force = True`, mostrare sempre una finestra di dialogo esplicita nei tre casi critici:
      1. Eccezione o mancata connettività di rete;
      2. Risposta HTTP non positiva dalle API GitHub (compreso rate limit 403 e 404);
      3. Nuova versione individuata ma priva di asset ZIP valido per l'aggiornamento.
  - **Test di accettazione**: Eseguire il controllo manuale con rete disconnessa o simulando una risposta 403 da GitHub: deve apparire una MessageBox informativa con spiegazione dell'errore anziché una chiusura silenziosa.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Medio | **Sforzo**: Basso

- [ ] **TASK-0095** [Media] Confronto errato delle versioni pre-release/beta e selezione della release remota
  - **File**: `UpdateChecker.vb` (metodi `ComparePrerelease`, `IsNewerVersion`, `FetchGitHubReleaseInfoAsync`)
  - **Problema / Esigenza**:
    1. Il confronto lessicografico `String.Compare("beta10", "beta2")` produce il risultato non corretto `beta10 < beta2` poiché il carattere '1' precede '2', impedendo il rilevamento di release beta con numerazione a più cifre.
    2. Nel canale beta, l'endpoint GitHub restituisce una lista di release: il codice seleziona la prima release dell'elenco per data, che potrebbe non corrispondere alla versione con numero semantico più alto in caso di release non sequenziali o hotfix.
  - **Fix da implementare**:
    - Implementare un parsing conforme allo standard SemVer 2.0: suddividere i token alfanumerici e numerici nei tag di pre-release, confrontando i segmenti composti unicamente da cifre con comparazione numerica (`Integer.TryParse`) e i segmenti alfanumerici con comparazione lessicografica.
    - Nel canale beta, analizzare tutte le release fornite dall'API aventi uno ZIP allegato e selezionare quella con la versione semantica massima reale, anziché limitarsi alla prima riga temporale.
  - **Test di accettazione**: Validare che `v1.2.10-beta` risulti correttamente più recente di `v1.2.2-beta`. Con una lista di release GitHub fuori ordine temporale, verificare che venga selezionata la versione semantica più elevata.
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
