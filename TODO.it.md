# Attività da completare

[![Italiano](https://img.shields.io/badge/Lingua-Italiano-green.svg)](TODO.it.md)
[![English](https://img.shields.io/badge/Language-English-blue.svg)](TODO.md)

---

## Sicurezza, Sessioni e Affidabilità Core

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

- [ ] **TASK-0091** [Alta] Marker di versione scritto prima della copia robocopy, naming univoco directory temporanea e pulizia residui temporanei
  - **File**: `UpdateChecker.vb` (metodo `PerformUpdateFromGitHubAsync`, `WriteLocalVersionMarker`), `Application.xaml.vb`
  - **Problema / Esigenza**:
    1. `WriteLocalVersionMarker` viene invocato dal codice VB prima che la copia dei file sia completata: se robocopy fallisce o viene interrotto, il file `.app_version` dichiara comunque la nuova versione anche se i file dell'applicazione non sono stati aggiornati. Inoltre l'updater riscrive già il marker dopo una copia riuscita, rendendo la chiamata preventiva in VB inutile e dannosa, con incongruenza tra formati (es. `0.7.1` in VB vs `v0.7.1`).
    2. Lo script di aggiornamento veniva creato all'interno di `tempDir`. Se l'archivio ZIP contiene `HidaChat.exe` alla radice, `sourceDir = tempDir`: robocopy copia quindi anche lo script nella cartella di installazione dell'applicazione.
    3. La cartella temporanea estratta `tempDir` (potenzialmente centinaia di MB) e i file zip temporanei non venivano cancellati in modo affidabile al termine dell'aggiornamento, lasciando spazio occupato inutilmente.
    4. L'uso di un nome fisso per la directory temporanea (`%TEMP%\HidaChat_Update`) provoca collisioni e conflitti di file lock se due utenti o istanze portabili eseguono l'aggiornamento contemporaneamente.
  - **Fix da implementare**:
    - Rimuovere la chiamata a `WriteLocalVersionMarker` dal flusso VB, delegando la scrittura del marker unicamente all'updater solo a seguito di copia completata con esito favorevole (`%RC% < 8`). Unificare il formato della versione.
    - Generare nomi univoci basati su sessione per le directory temporanee (es. `HidaChat_Update_{updateSessionId}`) per prevenire collisioni tra istanze o utenti concorrenti.
    - Posizionare gli script temporanei di avvio all'esterno sia di `tempDir` sia di `sourceDir` (es. direttamente in `Path.GetTempPath()`), evitando che robocopy li includa nella destinazione finale.
    - Garantire la rimozione completa della directory temporanea estratta (`rd /s /q "<tempDir>"`) e dei file zip temporanei al termine o in caso di errore.
  - **Test di accettazione**: Eseguire una procedura di aggiornamento: verificare che gli script non siano presenti nella cartella di installazione finale, che `tempDir` venga completamente rimossa al termine, che aggiornamenti concorrenti utilizzino cartelle temporanee isolate e che `.app_version` non venga scritto se la copia fallisce.
  - **Priorità**: P1 (Gravità alta) | **Impatto**: Alto | **Sforzo**: Basso-Medio

- [ ] **TASK-0092** [Alta] Script batch di aggiornamento fragile (codepage OEM/UTF-8, caratteri accentati, caratteri speciali, parentesi e unità root USB)
  - **File**: `UpdateChecker.vb` (generazione batch in `PerformUpdateFromGitHubAsync`)
  - **Problema / Esigenza**:
    1. Lo script viene scritto con `File.WriteAllText` in UTF-8, mentre `cmd.exe` legge i file con codepage OEM (es. CP437/CP850). Se il percorso contiene caratteri accentati (es. vocali italiane `è`, `à`, `ò` nel nome utente o nella cartella), robocopy interpreta male il percorso e fallisce.
    2. Caratteri speciali come `%` o `!` nel percorso compromettono il parsing delle variabili e la delayed expansion in `cmd.exe`.
    3. All'interno dei blocchi `if (...)`, i comandi come `echo ... {logFile}` con percorsi non racchiusi tra virgolette o contenenti parentesi tonde chiuse `)` (es. `HidaChat (1)` o `Program Files (x86)`) chiudono anticipatamente il blocco `if`, causando errori di sintassi cmd e l'interruzione dello script.
    4. Se l'applicazione è alla radice di un drive USB (es. `E:\HidaChat.exe`, scenario esplicitamente supportato per uso portabile), `installDir` risulta essere `E:\`. In robocopy la notazione `"E:\"` viene interpretata come virgoletta escapata (`\"`), rompendo il comando. È necessario normalizzare con `installDir.TrimEnd("\""c) & "\."`.
    5. In caso di errore robocopy, l'esecuzione di `exit /b 1` senza un comando `pause` fa chiudere istantaneamente la console senza permettere all'utente di leggere l'errore.
  - **Fix da implementare**:
    - Inserire `chcp 65001 >nul` all'inizio dello script batch (oppure scrivere il file batch con codepage OEM `Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage)`) per supportare correttamente i caratteri accentati.
    - Rimuovere o racchiudere tra virgolette doppie ed escapare i percorsi nei comandi `echo` all'interno dei blocchi `if`, gestendo adeguatamente i caratteri speciali `%` e `!`.
    - Racchiudere sempre tutti i percorsi tra virgolette doppie e normalizzare la destinazione robocopy per unità radice con `installDir.TrimEnd("\""c) & "\."`.
    - In caso di errore robocopy (`%RC% GEQ 8`), inserire una `pause` prima di uscire per consentire la lettura del log.
  - **Test di accettazione**: Eseguire un aggiornamento con percorsi contenenti caratteri accentati (`C:\Users\è_test\HidaChat`), parentesi tonde (`HidaChat (1)`), simboli `%` e da radice drive USB (`E:\`). Verificare la corretta esecuzione di robocopy senza errori di sintassi.
  - **Priorità**: P1 (Gravità alta) | **Impatto**: Alto | **Sforzo**: Basso-Medio

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

- [ ] **TASK-0117** [Alta] Verifica ed enforcement client di download dedicato, gestione timeout e streaming su file
  - **File**: `UpdateChecker.vb`, `UpdateProgressDialog.xaml`, `UpdateProgressDialog.xaml.vb`
  - **Correlato a**: TASK-0088
  - **Problema / Esigenza**:
    - Se il download dello ZIP di aggiornamento utilizza il client `_httpClient` standard configurato con un timeout di 15 secondi, su connessioni lente il download di archivi di decine di MB fallisce per `TaskCanceledException` con il messaggio generico "Errore durante l'installazione".
    - Il download tramite `GetByteArrayAsync` carica l'intero archivio nella RAM prima della scrittura, aumentando la pressione sulla memoria nei dispositivi portatili.
    - `UpdateProgressDialog` deve essere garantito come attivo e visibile, riportando in tempo reale l'avanzamento del download (MB scaricati / totali e percentuale) anziché risultare inutilizzato o non sincronizzato.
  - **Fix da implementare**:
    - Verificare ed assicurare l'uso rigoroso di un `_downloadClient` separato con `Timeout = Timeout.InfiniteTimeSpan` protetto da un `CancellationTokenSource` di 10 minuti per tutti i percorsi di download degli asset.
    - Effettuare lo streaming progressivo dei byte direttamente dal flusso HTTP al file su disco (`File.Create(tempZipPath)`) tramite buffer a blocchi (80 KB) senza caricare l'intero ZIP in memoria RAM.
    - Garantire l'istanziamento e l'aggiornamento costante di `UpdateProgressDialog` sul dispatcher UI con il progresso in tempo reale.
  - **Test di accettazione**: Simulare una connessione lenta con download limitato: verificare che il download prosegua oltre i 15 secondi senza interruzioni per timeout, che la RAM rimanga costante e che `UpdateProgressDialog` mostri la barra di avanzamento e i MB scaricati.
  - **Priorità**: P1 (Gravità alta) | **Impatto**: Alto | **Sforzo**: Basso-Medio

- [ ] **TASK-0118** [Alta] Terminazione mirata dei processi per PID e attesa chiusura del processo figlio tsnetd.exe durante l'aggiornamento
  - **File**: `UpdateChecker.vb`, `TsnetManager.vb`, `MainWindow.xaml.vb`
  - **Problema / Esigenza**:
    1. L'uso del comando di chiusura per nome immagine (`taskkill /f /im HidaChat.exe /t`) termina indistintamente tutte le istanze di HidaChat in esecuzione sul sistema. Trattandosi di un'applicazione portabile, un'altra copia in esecuzione da una cartella o unità differente (es. pendrive USB) viene terminata dopo 10 secondi.
    2. Lo script di aggiornamento non attende la chiusura di `tsnetd.exe` (processo demone Tailscale): se `tsnetd.exe` è ancora attivo, la copia robocopy fallisce nel momento in cui tenta di sovrascrivere il suo binario eseguibile.
  - **Fix da implementare**:
    - Passare il PID del processo chiamante (`Environment.ProcessId`) all'updater e attendere specificamente l'uscita di quel PID e dei suoi processi figli (`tasklist /fi "PID eq {pid}"` o `Process.GetProcessById`).
    - In `ForceExitForUpdateAsync`, attendere esplicitamente l'arresto ordinato e la chiusura di `tsnetd.exe` (`TsnetManager.ShutdownAsync()`), verificando che il processo sia terminato prima del passaggio di consegne alla copia dei file.
  - **Test di accettazione**: Avviare due istanze portabili indipendenti di HidaChat da cartelle diverse. Avviare l'aggiornamento su una sola copia: verificare che solo l'istanza chiamante venga terminata mentre la seconda istanza prosegue normalmente. Verificare che `tsnetd.exe` sia completamente terminato prima che robocopy inizi la sovrascrittura.
  - **Priorità**: P1 (Gravità alta) | **Impatto**: Alto | **Sforzo**: Basso-Medio

- [ ] **TASK-0119** [Alta] Backup della cartella di installazione, rollback automatico e rilancio dell'applicazione in caso di fallimento della copia
  - **File**: `UpdateChecker.vb`, `Application.xaml.vb`
  - **Problema / Esigenza**:
    1. Se la sostituzione dei file (robocopy con codice di uscita >= 8 o errore di I/O) fallisce a metà operazione, lo script esegue `exit /b 1`, lasciando l'installazione in uno stato inconsistente e corrotto con file misti vecchi e nuovi, senza possibilità di ripristino.
    2. La finestra di console scompare istantaneamente, lasciando l'applicazione chiusa e l'utente senza alcuna indicazione o interfaccia.
    3. La scrittura anticipata di `.app_version` lascia il marker impostato sulla nuova versione nonostante i binari non siano stati aggiornati.
  - **Fix da implementare**:
    - Creare una copia di backup di sicurezza dei binari della cartella di installazione (escludendo la cartella dati `data/`) prima di avviare la sostituzione dei file.
    - In caso di errore durante la copia robocopy: ripristinare automaticamente i vecchi binari dal backup, eliminare la cartella di backup temporanea, mostrare una finestra di dialogo esplicita di errore e rilanciare la precedente versione funzionante di `HidaChat.exe`.
    - Garantire che la scrittura del marcatore `.app_version` avvenga solo a copia robocopy completata con successo e verificata.
  - **Test di accettazione**: Simulare un fallimento di robocopy durante l'aggiornamento (es. bloccando un file dll di destinazione): verificare che l'updater rilevi l'errore, ripristini i vecchi binari dal backup e rilanci la versione precedente mostrando un messaggio di errore chiaro.
  - **Priorità**: P1 (Gravità alta) | **Impatto**: Alto | **Sforzo**: Medio

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

- [ ] **TASK-0123** [Media] Restyling moderno e riorganizzazione strutturata della finestra Impostazioni (SettingsWindow)
  - **File**: `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`
  - **Problema / Esigenza**:
    - Attualmente tutte le impostazioni dell'applicazione (Aspetto e Tema, Lingua e Traduzioni, Notifiche e Non Disturbare, Gestione Multi-Account, Configurazione Tailscale tsnet, Editor CSS personalizzato con preset, Correttore ortografico, Staging locale dischi di rete, Ibernazione schede, Limite cache disco Chromium e ottimizzazione spazio, Canale aggiornamenti Beta e Strumenti di sviluppo) sono raggruppate in un unico elenco a scorrimento verticale lineare all'interno di una finestra fissa di 520x580 pixel.
    - L'assenza di una suddivisione per categorie rende la navigazione disordinata e scomoda con il continuo aggiungersi di nuove funzionalità.
    - Controlli fuori contesto: il pulsante "Controlla ora" per gli aggiornamenti è collocato arbitrariamente nella sezione DevTools anziché in Aggiornamenti/Info.
    - Localizzazione incompleta o fissa nel markup: diversi testi e pulsanti (es. "Usa nodo Tailscale integrato", "Pulisci Cache", "Compatto", "Svuota", "Applica CSS") sono scritti direttamente in italiano nel file XAML anziché utilizzare i dizionari di `Localization.vb`.
    - I pannelli dinamici per i fornitori di traduzione (LibreTranslate, Ollama, OpenAI) creano disordine visivo e continui salti di scorrimento nella vista principale.
    - Gestione account sacrificata: le credenziali, l'URL del server, l'integrazione Tailscale e il test di connessione sono compressi in spazi ristretti.
  - **Specifiche & Soluzione da implementare**:
    - **Architettura di Navigazione Categorizzata (Schede o Sidebar moderna)**:
      - Sostituire l'unica colonna a scorrimento con un'interfaccia a categorie pulita e intuitiva (sidebar laterale o barra a schede superiore):
        - **Generale**: Tema applicazione (Sistema, Scuro, Chiaro), Lingua interfaccia, Notifiche popup, Durata Non Disturbare (DND), Lingua correttore ortografico.
        - **Account**: Schede spaziose per la gestione account con selettore piattaforma, nome, URL del server, token di autenticazione, switch Tailscale e test di connessione.
        - **Traduzioni**: Abilitazione pulsanti e traduzione intera pagina, scelta fornitore (Google, LibreTranslate, Ollama, OpenAI), pannelli di configurazione dedicati, consenso privacy e test di connettività.
        - **Rete e Tailscale**: Badge di stato nodo Tailscale (tsnet), AuthKey, pulsante di accesso e opzione di staging locale per dischi di rete (SMB/UNC).
        - **Aspetto e CSS**: Editor CSS personalizzato con font monospace, pulsanti di preset (OLED, Compatto, Font, Svuota) e pulsante di applicazione immediata.
        - **Archiviazione e Prestazioni**: Timeout ibernazione schede inattive, limite cache disco Chromium, compattazione live dello spazio e pulizia cache sicura.
        - **Info e Aggiornamenti**: Informazioni sulla versione, selezione canale (Beta/Stabile), pulsante di verifica aggiornamenti (spostato da DevTools), link alle release note e opzione per DevTools.
    - **Rifinitura Grafica e Layout a Schede/Card**:
      - Organizzare le impostazioni correlate in box/card visivamente distinti con bordi arrotondati, sfondi armonici e sottotitoli descrittivi.
      - Garantire uniformità tipografica, allineamenti, altezze dei pulsanti e margini coerenti.
    - **Localizzazione Completa**:
      - Spostare tutte le stringhe hardcoded in `Localization.vb` sincronizzando tutti i 5 dizionari supportati (`EnStrings`, `ItStrings`, `FrStrings`, `EsStrings`, `DeStrings`).
    - **Compatibilità con i Temi e Dimensionamento**:
      - Assicurare il perfetto funzionamento di `ApplyTheme()` su tutti i componenti di navigazione, card e campi di input sia in tema Scuro che in tema Chiaro.
      - Ampliare le dimensioni della finestra (es. larghezza ~680–720px, altezza ~540–580px) per offrire una visualizzazione confortevole senza conflitti di scrolling.
  - **Test di accettazione**: Aprire le Impostazioni sia in tema Scuro che in tema Chiaro; verificare che tutte le categorie siano accessibili tramite navigazione intuitiva, che ogni impostazione mantenga lo stato e funzioni correttamente (modifiche account, CSS, test traduzione, pulizia cache, verifica aggiornamenti), che non vi siano stringhe fisse non localizzate e che il cambio lingua aggiorni dinamicamente tutte le etichette e sezioni nelle 5 lingue previste.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Alto | **Sforzo**: Medio



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
