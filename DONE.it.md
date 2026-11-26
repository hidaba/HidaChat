# Attività Completate

[![Italiano](https://img.shields.io/badge/Lingua-Italiano-green.svg)](DONE.it.md)
[![English](https://img.shields.io/badge/Language-English-blue.svg)](DONE.md)

## 2026-11-26

- [x] **TASK-0118** [Alta] Terminazione mirata dei processi per PID e attesa chiusura del processo figlio tsnetd.exe durante l'aggiornamento
  - Risultato:
    - Aggiunte le proprietà `IsProcessRunning`, `ProcessId` e il metodo `WaitForProcessExitAsync` in `TsnetManager.vb` per monitorare e garantire l'avvenuta chiusura del processo demone figlio.
    - Aggiornato `ForceExitForUpdateAsync` in `MainWindow.xaml.vb` per attendere esplicitamente l'arresto ordinato e la chiusura di `tsnetd.exe` (`TsnetManager.Instance.ShutdownAsync(overallTimeoutMs:=3000)` e `WaitForProcessExitAsync(timeoutMs:=2000)`) prima di rilasciare il mutex di istanza singola e cedere il controllo all'updater.
    - Aggiunta la verifica di uscita in `UpdateChecker.vb` prima del lancio dell'aggiornamento.
    - Perfezionato `Application.xaml.vb` (`HandleApplyUpdateStartup`): attende specificamente l'uscita del PID chiamante (`Process.GetProcessById(callerPid)`). Se il chiamante non termina entro il timeout, termina esclusivamente quel determinato PID e il relativo albero dei processi figli (`caller.Kill(entireProcessTree:=True)`), evitando l'uso di comandi generici per nome immagine (`taskkill /f /im HidaChat.exe /t`).
    - Aggiunto il controllo in `Application.xaml.vb` per accertare che qualsiasi istanza residua di `tsnetd.exe` associata alla cartella di installazione sia terminata prima della copia dei file.
    - Irrobustito lo script batch di fallback in `UpdateScriptPolicy.vb` (`GenerateUpdateBatchScript`): esegue la chiusura mirata per PID (`taskkill /f /pid %CALLER_PID% /t`), preservando intatte le eventuali altre copie portabili in esecuzione da percorsi differenti.
  - Verifica: Aggiunte 13 asserzioni di regressione in `Tests/SessionTests.vb` a verifica della sintassi di terminazione per PID nello script batch, assenza di terminazioni per nome immagine, comportamento di `IsProcessRunning`, `ProcessId` e `WaitForProcessExitAsync` (per un totale di **919 asserzioni di regressione con 0 errori**). Compilazione del progetto eseguita con successo con 0 avvisi e 0 errori.

## 2026-11-25

- [x] **TASK-0127** [Alta] Protezione sincronizzazione profili multi-PC contro l'esportazione di staging obsoleti e corruzione sessioni
  - Risultato:
    - Diagnosticata la causa della perdita di sessione: un'istanza su `PC-STUDIO` (utente `hidab`) con importazione fallita per file contesi ha mantenuto uno staging locale vuoto e, all'uscita alle 17:28, ha esportato tale profilo non autenticato sul Master di rete (`Y:\`), portando `PC-MIMMO` ad importarlo e a richiedere il codice QR su WhatsApp e il login su Hermes.
    - Ripristinati i profili autenticati integri per tutti gli account (`1784925628238` WhatsApp, `b63821a1add040ad9ea8f9fd96ba88bb` Hermes, `1788428413731` OpenClaw, `f7b6555d88a84b56bce69f0ba1610c33` Discord) dagli snapshot `.previous` preservati su `PC-MIMMO` e ripubblicati autorevolmente sul Master di rete `Y:\`.
    - Implementati `MarkStagingStale`, `ClearStagingStale` e `IsStagingStale` in `NetworkProfileSync.vb` con tracciamento in memoria e file marker persistente `.sync_stale` su disco.
    - Aggiornato `SyncMasterToLocalStagingAsync`: contrassegna lo staging locale come obsoleto/stale quando il refresh dal master fallisce o viene saltato per lock, impedendo all'istanza di esportare dati non sincronizzati.
    - Irrobustito `SyncLocalStagingToMasterAsync` con due guardie di esportazione:
      1. Rifiuta l'esportazione (`[EXPORT_REFUSED_STALE_STAGING]`) se lo staging locale è contrassegnato come stale o non allineato al master.
      2. Rifiuta l'esportazione (`[EXPORT_REFUSED_MASTER_NEWER]`) se il Master di rete è stato aggiornato da un'altra macchina/utente con un timestamp più recente rispetto alla baseline locale.
  - Verifica: Aggiunte 7 asserzioni di regressione in `Tests/SessionTests.vb` a verifica del tracciamento di staging stale, persistenza del marker file, rilevamento cross-process e pulizia (totale di **906 asserzioni di regressione con 0 errori**). Verificato il riavvio live di `HidaChat` su `Y:\` con navigazione corretta di WhatsApp, Hermes, Telegram, OpenClaw e Discord direttamente alle pagine autenticate senza richieste di login.

- [x] **TASK-0126** [Alta] Risoluzione del loop infinito di login su Hermes Agent causato dall'iniezione di Bearer token scaduto
  - Risultato:
    - Implementato `TryGetTokenExpiration` in `LocalAuthPolicy.vb` per l'estrazione sicura del timestamp Unix `exp` sia da JWT standard a 3 parti che da token binari con firma base64url (formato token Hermes).
    - Implementato `IsTokenExpired` in `LocalAuthPolicy.vb` per il confronto tra il timestamp `exp` del token e l'orario UTC corrente.
    - Implementati `IsAuthenticationEndpoint` e `ShouldAttachBearerToken` in `LocalAuthPolicy.vb` per bloccare l'iniezione preventiva dell'header `Authorization: Bearer` quando un token è scaduto o quando le richieste sono dirette verso endpoint di autenticazione (`/login`, `/signin`, `/auth/password-login`, `/auth/login`, `/auth/token`, `/auth/refresh`).
    - Aggiornato `AppAccounts.vb` per disabilitare `_activeRequiresAuthToken` in `UpdateWebResourceAuthFilter` in presenza di un `AuthToken` scaduto e protetta l'iniezione in `_webResourceRequestedHandler` tramite `LocalAuthPolicy.ShouldAttachBearerToken`.
    - Aggiunto il meccanismo di auto-ripristino in `AccountManager.LoadAccountsAsync` per rilevare e azzerare in sicurezza i token di autenticazione scaduti all'avvio, prevenendo reindirizzamenti continui al login.
    - Ripulito il token di sessione scaduto sull'account Hermes locale ("Ermanno") in `Y:\HidaChat\data\settings.json`, distribuiti i binari Release aggiornati su `Y:\HidaChat\` e verificato il riavvio corretto dell'applicazione.
  - Verifica: Aggiunte 28 asserzioni unitarie di regressione in `Tests/SessionTests.vb` a verifica dell'estrazione di scadenza, discriminazione token validi/scaduti, rilevamento endpoint di login e iniezione selettiva degli header (totale di **899 asserzioni automatiche con 0 errori**). Compilazione completa del progetto eseguita con 0 avvisi e 0 errori.

- [x] **TASK-0082** [Media] La verifica dei certificati HTTPS viene disattivata
  - Risultato:
    - Eliminato il bypass globale silenzioso e incondizionato `InsecureSkipVerify = true` nel reverse proxy companion tsnet, ripristinando la validazione standard dei certificati TLS e del nome host come comportamento predefinito.
    - Introdotto il supporto per certificati CA radice personalizzati (`cmd.CaCert` / `CustomCaCertificatePath`) e certificate pinning con impronta SHA-256 (`cmd.PinnedCertHash` / `PinnedCertificateHash`) per proteggere le comunicazioni da attacchi MITM.
    - Aggiunta l'opzione esplicita `AllowInsecureTls` (disabilitata per impostazione predefinita) per consentire il collegamento a server di sviluppo con certificati autofirmati senza bypass globale silenzioso.
    - Creata la policy `TlsCertificatePolicy.vb` per la validazione della catena dei certificati, la discriminazione degli errori e il calcolo delle impronte SHA-256.
    - Rimosso il bypass incondizionato dei certificati in `BtnTestConnection_Click` e in `TsnetManager.vb`.
    - Distinti gli errori di certificato SSL/TLS (certificati scaduti, root non attendibili, hostname errati, mismatch di pinning) dai problemi generali di raggiungibilità, mostrando uno stato localizzato dedicato (`connection_tls_error` / `tailscale_error_tls_failure`) e tooltip tecnici dettagliati invece di notifiche ambigue di irraggiungibilità.
    - Configurato il proxy companion tsnet per emettere l'header `X-HidaChat-Error-Type: TLS_CERTIFICATE_ERROR` e una pagina diagnostica esplicita non ciclica in caso di fallimento della verifica TLS verso l'upstream.
    - Aggiornati e sincronizzati i dizionari localizzati in tutte le 5 lingue ufficiali (inglese, italiano, francese, spagnolo, tedesco).
  - Verifica: Aggiunte 29 asserzioni di regressione automatica in `SessionTests.vb` che verificano normalizzazione impronte, generazione certificati in-memory, errori di catena standard, discrepanze hostname, corrispondenza/mismatch di certificate pinning e discriminazione errori TLS vs raggiungibilità (per un totale di **871 asserzioni di regressione con 0 errori**). Compilazione Release completata con 0 avvisi e 0 errori.

## 2026-11-24

- [x] **TASK-0092** [Alta] Script batch di aggiornamento fragile (codepage OEM/UTF-8, caratteri accentati, caratteri speciali, parentesi e unità root USB)
  - Risultato:
    - Creato `UpdateScriptPolicy.vb` con metodi dedicati di normalizzazione (`NormalizePathForRobocopy` e `NormalizePathForArgument`) per unità radice USB (es. `E:\`, `E:`) e percorsi con barra finale verso `\.`, risolvendo il difetto di escape delle virgolette Windows (`\"`) nel parsing degli argomenti riga di comando e in robocopy.
    - Implementato `GenerateUpdateBatchScript` con `chcp 65001 >nul` per il pieno supporto della codepage UTF-8, garantendo che cartelle e nomi utente con caratteri accentati (es. vocali italiane `è`, `à`, `ò`) non provochino corruzione dei percorsi.
    - Protette le assegnazioni delle variabili con sintassi `set "VAR=VAL"` ed eliminati i percorsi non racchiusi da virgolette negli `echo` all'interno dei blocchi `if (...)`, evitando la chiusura anticipata dei blocchi generata da parentesi tonde nei nomi delle cartelle (es. `HidaChat (1)` o `Program Files (x86)`).
    - Inserito il comando `pause` obbligatorio in caso di errore robocopy (`%RC% GEQ 8`) prima di `exit /b 1`, impedendo la chiusura immediata della finestra di console e consentendo la lettura dell'errore e del log.
    - Aggiunto il fallback automatico allo script batch resiliente in `UpdateChecker.vb` qualora l'avvio del processo updater compilato fallisca.
    - Aggiornato `Application.xaml.vb` per canonizzare i percorsi sorgente, destinazione e temporanei e formattare in sicurezza gli argomenti di riavvio e pulizia.
  - Verifica: Aggiunte 28 asserzioni di regressione automatizzate in `SessionTests.vb` che verificano la normalizzazione dei percorsi per drive USB e cartelle con accenti/parentesi, l'escape delle parentesi tonde negli echo, la scrittura senza BOM UTF-8 e la gestione della pausa su errore robocopy (raggiungendo **842 asserzioni di regressione con 0 errori**). Compilazione completata con 0 avvisi e 0 errori.

- [x] **TASK-0117** [Alta] Client di download dedicato, gestione timeout e streaming progressivo su file
  - Risultato:
    - Creato `UpdateDownloadPolicy.vb` che fornisce `CreateDedicatedDownloadClient` con `Timeout = Timeout.InfiniteTimeSpan` e intestazione User-Agent ProductInfo dell'applicazione.
    - Implementato lo streaming a blocchi (`DownloadStreamToFileAsync` / `DownloadFileStreamingAsync`) con buffer da 80 KB che scrive direttamente nel file di destinazione su disco, azzerando l'occupazione in RAM dell'intero file ZIP.
    - Applicato un timeout operativo globale di 10 minuti tramite `CancellationTokenSource` in `UpdateChecker.vb`, prevenendo fallimenti prematuri su connessioni lente e proteggendo da blocchi indefiniti.
    - Aggiunto il tracciamento dettagliato del progresso determinato e indeterminato via `IProgress(Of DownloadProgressReport)`, con byte letti, byte totali, percentuale e formattazione MB.
    - Collegato l'avanzamento in streaming direttamente al dispatcher UI di `UpdateProgressDialog` durante il download degli archivi di aggiornamento da GitHub.
  - Verifica: Aggiunti 18 test di regressione automatizzati in `SessionTests.vb` che convalidano il timeout del client, la dimensione del buffer chunk, l'integrità bit-per-bit dello streaming su file, gli eventi di progresso e la gestione della cancellazione (raggiungendo **794 asserzioni di regressione con 0 errori**). Compilazione completata con 0 avvisi e 0 errori.

- [x] **TASK-0125** [Media] Prevenzione cambio accidentale piattaforma con rotella mouse nelle impostazioni e ripristino account Hermes
  - Risultato:
    - Aggiunto il gestore `ComboAccountPlatform_PreviewMouseWheel` in `SettingsWindow.xaml` e `SettingsWindow.xaml.vb` per bloccare il cambio piattaforma accidentale tramite rotella del mouse a menu chiuso, inoltrando l'evento di scorrimento al contenitore genitore.
    - Ripristinato `Y:\HidaChat\data\settings.json` e `.bak`, reimpostando l'account `account_1789733553499` ("Ermanno") sulla piattaforma `"Hermes"` con porta proxy `18900`.
    - Rimosso l'account di test ridondante creato durante le verifiche manuali.
  - Verifica: Compilazione completata con 0 errori e 0 avvisi. Eseguiti 776 test di regressione automatizzati con esito positivo e 0 errori.

## 2026-11-23

- [x] **TASK-0124** [Alta] Inversione porte proxy e race condition tra OpenClaw ed Hermes in TsnetManager
  - Risultato:
    - Applicata la convalida rigorosa dell'intervallo porte dedicato per piattaforma (`[18800, 18899]` per OpenClaw, `[18900, 18999]` per Hermes) tramite `AccountLoadSafetyPolicy.IsValidProxyPortForPlatform`.
    - Aggiunta l'assegnazione deterministica delle porte con risoluzione delle collisioni (`ResolveDeterministicPort`), impedendo che account multipli si sovrappongano.
    - Implementato un meccanismo di auto-ripristino a due passaggi durante `AccountManager.LoadAccountsAsync` per rilevare porte fuori intervallo o invertite (es. OpenClaw con porta Hermes o viceversa), azzerandole e riassegnando porte deterministiche coerenti con la piattaforma.
    - Reimpostate e riassegnate le porte proxy e gli URL locali contestualmente al cambio piattaforma in `AppAccounts.SetPlatform`.
    - Riprogettata la gestione delle risposte IPC in `TsnetManager.EnsureRouteAsync` e `CheckStatusAsync` correlando rigorosamente le risposte JSON alla richiesta emessa (`accountId` per le rotte, `nodeState` per lo stato).
    - Le risposte intercalate da altri comandi o account vengono registrate nello stato interno senza attribuirle al chiamante corrente, prevenendo la desincronizzazione della pipe.
    - Ripristinata la configurazione nell'installazione reale in `Y:\HidaChat\data\settings.json` e `.bak`, riallineando OpenClaw a 18800 ed Hermes a 18900.
  - Verifica: Aggiunte 29 asserzioni di regressione automatizzate in `SessionTests.vb` verificando la convalida degli intervalli, gli overload per stringhe, l'evitamento delle collisioni, l'auto-ripristino di porte invertite e l'esclusione di piattaforme non proxy (raggiungendo **776 asserzioni di regressione con 0 errori**). Compilazione pulita con 0 avvisi e 0 errori.

- [x] **TASK-0091** [Alta] Marker di versione scritto prima della copia robocopy, naming univoco directory temporanea e pulizia residui temporanei
  - Risultato:
    - Rimossa la scrittura preventiva di .app_version prima della copia dei file; la scrittura del marcatore è ora demandata unicamente a HandleApplyUpdateStartup solo dopo che la copia dei file è completata con successo.
    - Standardizzato il formato di .app_version in "v" & version.TrimStart("v"c) lungo tutto il ciclo di vita, garantendo piena coerenza.
    - Rafforzata la generazione delle directory temporanee basate su timestamp e GUID (HidaChat_Update_{sessionId}) per prevenire conflitti tra istanze portabili concorrenti e ambienti multi-utente.
    - Aggiornata la bonifica in background per ignorare tassativamente la sessione corrente e preservare le sessioni recenti (< 2 ore), prevenendo race condition e cancellazioni tra istanze parallele.
    - Passata la directory temporanea principale (	empDir) negli argomenti CLI --apply-update e --cleanup-update, garantendo la rimozione dell'intero albero di cartelle estratte dopo il rilancio dell'app aggiornata.
    - Aggiunta logica di retry con backoff in --cleanup-update per eliminare file zip e cartelle temporanee anche in presenza di lock momentanei.
    - Aggiunta la bonifica di cartelle e zip temporanei nel blocco di errore di HandleApplyUpdateStartup, evitando sprechi di spazio su disco in caso di fallimento della copia.
    - Esclusi script di avvio, file .bat, .cmd, .ps1 e .tmp dalla copia verso la cartella dell'applicazione in CopyUpdateFiles.
  - Verifica: Aggiunte 28 asserzioni di regressione automatizzate in SessionTests.vb per verificare l'esclusione degli script, il formato del marcatore di versione, la generazione degli argomenti CLI con directory temporanea radice e i filtri di pulizia sicura (totale di **747 asserzioni di regressione automatizzate con 0 errori**). Compilazione pulita con 0 avvisi e 0 errori.

## 2026-11-22

- [x] **TASK-0081** [Alta] Un blocco di tsnetd può impedire indefinitamente la chiusura
  - Risultato:
    - In `TsnetManager.vb`, potenziato `SendCommandAsync` per gestire un timeout cancellabile (default 3000ms) che copre sia l'acquisizione del semaforo (`_ioLock.WaitAsync`) sia l'I/O (`WriteLineAsync`, `ReadLineAsync`), prevenendo blocchi indefiniti in caso di daemon non responsivo.
    - Se un comando va in timeout o viene annullato, `_ioLock` viene rilasciato in sicurezza nel blocco `Finally`, evitando la contesa del lock e consentendo l'esecuzione di comandi successivi o dello shutdown.
    - Implementato un CancellationTokenSource legato al ciclo di vita del processo (`_processCts`), segnalato immediatamente all'inizio di `ShutdownAsync()`, che annulla tempestivamente i comandi pendenti in background (es. timer di polling, gestione rotte) e libera il lock.
    - Riprogettato `ShutdownAsync(Optional overallTimeoutMs As Integer = 2000)` con una scadenza complessiva tassativa e indipendente dal canale comandi:
      - Riserva una frazione del budget (fino a 600ms) per tentare uno shutdown cooperativo con il comando `"shutdown"`, senza che un canale bloccato paralizzi la chiusura.
      - Chiude `StandardInput` (inviando EOF al loop scanner del daemon Go) per sbloccare le letture su stdin.
      - Attende l'uscita del processo per il tempo residuo del budget complessivo.
      - Se il daemon resta attivo oltre la scadenza complessiva, ne forza la terminazione immediata con `proc.Kill(entireProcessTree:=True)`.
      - Chiude gli stream, cancella la lettura degli errori e pulisce lo stato senza lasciare letture pendenti in background.
    - Aggiunte le proprietà `CustomExecutablePath` e `CustomExecutableArguments` in `TsnetManager.vb` per consentire test automatizzati con daemon fittizi.
  - Verifica: Aggiunte 11 nuove asserzioni di regressione automatizzate in `SessionTests.vb` simulando un daemon non responsivo (PowerShell dormiente che non scrive su stdout): verificato che `SendCommandAsync` scada entro il budget restituendo `Nothing`, verificato che comandi successivi procedano senza stallo del semaforo, verificato che `ShutdownAsync` termini entro il limite complessivo forzando l'arresto del daemon e verificato che `ShutdownAsync` rispetti la scadenza anche quando un comando precedente è attivamente bloccato (totale di **719 asserzioni di regressione automatizzate con 0 errori**). Compilazione pulita con 0 avvisi e 0 errori.

## 2026-11-21

- [x] **TASK-0080** [Alta] Stop e riavvio possono sovrapporre due esecuzioni dell’invio massivo
  - Risultato:
    - Incapsulato il coordinamento della concorrenza e la determinazione dell'esito dell'invio massivo in `BulkSenderExecutionCoordinator` e integrato con `BulkSenderEngine.vb`.
    - Il motore impedisce le esecuzioni concorrenti in modo autonomo e indipendente dalla UI: `RunAsync` verifica `TryBeginExecution()` sotto lock e respinge i tentativi di avvio concorrente con `InvalidOperationException` durante l'esecuzione o l'arresto.
    - Implementato il metodo `CancelAsync()` che richiede l'arresto (`BeginStop()`), sblocca l'evento di pausa, annulla il CancellationTokenSource e attende la conclusione effettiva del task attivo con timeout di salvaguardia.
    - Introdotto lo stato UI "Arresto in corso..." (`SetUiStoppingState()`) in `BulkSenderWindow.xaml.vb` che disabilita i pulsanti Avvio, Pausa e Stop, aggiornando la barra di stato.
    - Separati nettamente gli esiti finali di esecuzione in stati distinti: `Completed`, `Cancelled` e `Failed`. Un'esecuzione interrotta non passa più dal riepilogo "Completato" e non porta la barra al 100%.
    - L'annullamento durante la trasmissione di un messaggio o una chiamata JavaScript imposta lo stato del contatto a `Interrotto` (`bulk_status_interrupted`) anziché `Errore`, non incrementa erroneamente `ErrorCount` e riporta conteggi reali esatti di messaggi inviati, errori e non inviati.
    - Aggiunto il gestore del ciclo di vita della finestra (`BulkSenderWindow_Closing`) che gestisce la chiusura sicura con conferma e arresto controllato quando l'invio è attivo.
    - Aggiornati contestualmente tutti i 5 dizionari di localizzazione in `Localization.vb` (`EnStrings`, `ItStrings`, `FrStrings`, `EsStrings`, `DeStrings`) con `bulk_status_interrupted` e corretti i caratteri di codifica.
  - Verifica: Aggiunte 13 nuove asserzioni di regressione in `SessionTests.vb` verificando le transizioni di stato di arresto, il blocco rigoroso di avvii concorrenti durante operazioni JavaScript asincrone pendenti, la classificazione corretta dell'esito come Cancelled, la preservazione del conteggio inviati, l'assenza di falsi errori e il conteggio esatto dei contatti non inviati (totale di **708 asserzioni di regressione automatizzate con 0 errori**). Compilazione pulita con 0 avvisi e 0 errori.

## 2026-11-18

- [x] **TASK-0090** [Alta] MigrateOrphanProfile cancella o sovrascrive un profilo valido
  - Risultato:
    - Incapsulata la valutazione di sicurezza della migrazione del profilo orfano in `AccountLoadSafetyPolicy.CanSafelyMigrateOrphanProfile(sharedDir, accountId)`.
    - Verifica preliminarmente l'esistenza della cartella legacy di destinazione `WV2Profile_{id}`, della cartella del profilo nativo `EBWebView/wv2profile_{id}`, di pattern di denominazione nativa alternativi e di tutte le varianti di snapshot/backup (`.previous`, `.bak`, `.migrated`, `_Snapshot`).
    - Se la cartella di destinazione o uno snapshot esiste già su disco, `MigrateOrphanProfile` interrompe immediatamente e rigorosamente la migrazione (`Return`), preservando il profilo valido esistente e la sessione attiva dell'utente intatta al 100%.
    - Conservata la directory orfana `WV2Profile_` in sicurezza come backup, evitando sovrascritture distruttive ed escludendola da cancellazioni automatiche.
    - In `AccountManager.CreateDefaultAccountAsync`, aggiunto il controllo di esistenza preventivo prima dello spostamento dell'orfano anonimo per prevenire potenziali collisioni.
  - Verifica: Aggiunte 21 nuove asserzioni di regressione in `SessionTests.vb` verificando il rifiuto di parametri nulli/vuoti, cartella orfana mancante, destinazione valida preesistente e tutte le varianti di snapshot/profili nativi. Eseguito il test di accettazione esatto: con `WV2Profile_` e `WV2Profile_{id}` coesistenti con file differenti, la migrazione è stata respinta preservando `WV2Profile_{id}` intatto al 100% (totale di **695 asserzioni di regressione automatizzate con 0 errori**). Compilazione pulita con 0 avvisi e 0 errori.

## 2026-11-16

- [x] **TASK-0089** [Alta] Possibile perdita di tutte le sessioni su errore di deserializzazione (AccountManager.LoadAccountsAsync)
  - Risultato:
    - Incapsulata la logica di protezione e non distruttività in `AccountLoadSafetyPolicy.vb` ed integrata in `AccountManager.vb`.
    - Nel blocco Catch di `AccountManager.LoadAccountsAsync`: creazione immediata della copia di backup di sicurezza `settings.json.bak` prima di qualsiasi altra operazione di fallback.
    - Introdotto il flag di sicurezza `_loadFailed = True` e la proprietà pubblica `LoadFailed` in `AccountManager`.
    - Inibito rigorosamente il salvataggio degli account (`SaveAccountsAsync`), la cancellazione o lo spostamento di cartelle di profilo (`CleanupUnusedProfilesAsync`, `MigrateOrphanProfile`, `MigrateLegacyProfilesToNativeProfiles`, `RemoveAccountAsync` e la rinomina dell'orfano in `CreateDefaultAccountAsync`) quando `_loadFailed` è attivo.
    - Implementata la finestra di dialogo modale di avviso `NotifyAccountsCorruptedAndProtected` per informare esplicitamente l'utente del problema di configurazione, dell'avvenuto backup e del blocco preventivo dei salvataggi per proteggere tutte le sessioni di accesso esistenti.
    - Aggiornati contestualmente tutti i 5 dizionari di localizzazione UI in `Localization.vb` (`EnStrings`, `ItStrings`, `FrStrings`, `EsStrings`, `DeStrings`) con le chiavi `accounts_corrupt_title` e `accounts_corrupt_msg`.
  - Verifica: aggiunti test di regressione unitari in `SessionTests.vb` che verificano il rifiuto di errori di sintassi JSON, tipi incompatibili, array vuoti, la creazione non distruttiva del backup e che directory di profilo e database di sessione rimangano intatti su disco (totale di **674 asserzioni di regressione automatizzate con 0 errori**). Compilazione pulita con 0 avvisi e 0 errori.

## 2026-11-15

- [x] **TASK-0115** [Alta] Revisione del codice integrazione Discord, rafforzamento sicurezza, permessi multimediali, protezione chiamate vocali e correzione notifiche
  - Risultato:
    - Risolta l'interruzione delle chiamate vocali durante l'ibernazione delle schede in `AppAccounts.vb`: `TryHibernateAsync()` ora verifica `IsPlayingAudio` e `CoreWebView2.IsDocumentPlayingAudio`, impedendo rigorosamente la sospensione di schede con chiamate attive, canali vocali o riproduzione audio in corso.
    - Implementati permessi multimediali sicuri in `_permissionRequestedHandler` (`AppAccounts.vb`): autorizza esplicitamente richieste `Microphone` e `Camera` esclusivamente per origini autorizzate (`IsAuthorizedOrigin`), negando richieste da origini non attendibili o cross-origin.
    - Estesa la gestione della navigazione in nuove finestre in `PlatformDefinition.vb`: `IsAllowedNewWindowHost` ora consente le route di autenticazione Discord (`/login`, `/register`, `/verify`, `/oauth2/`, `/popout/`, `/guild-discovery`, `/store`), evitando che i flussi di accesso e verifica vengano impropriamente aperti nel browser esterno.
    - Migliorata la risoluzione dei deep link in `PlatformDefinition.vb`: aggiunto supporto per route `login`, `register` e `oauth2` nello schema `discord://`.
    - Rafforzata la sicurezza degli script JavaScript: sostituito il controllo vulnerabile `location.hostname.includes('discord')` e `.includes('telegram')` con validazione rigorosa del dominio in `Scripts/notification.js` e `Scripts/translation.js`.
    - Risolto il bug di doppio conteggio dei badge in `Scripts/notification.js`: filtrati i contenitori quando un badge interno è presente, eliminando conteggi duplicati genitore/figlio.
    - Esclusi canali e server silenziati dai badge unread pill in `Scripts/notification.js`.
    - Ripulita l'estrazione del titolo chat in `Scripts/notification.js` rimuovendo il suffisso ` - Discord` e ` | Discord`.
    - Esteso il monitoraggio dello stato sessione e persistenza storage in `Scripts/session-health.js` per includere `https://discord.com`.
  - Verifica: superate tutte le 653 asserzioni di regressione in `SessionTests.vb` con 0 errori. Compilazione pulita con 0 avvisi e 0 errori.

## 2026-11-14

- [x] **TASK-0088** [Alta] Timeout di 15 s sul download dello ZIP e buffering in RAM dell'archivio (UpdateChecker.vb)~~
  - Risultato: utilizzato client dedicato _downloadClient configurato con Timeout = Timeout.InfiniteTimeSpan e richiesta protetta da CancellationTokenSource con timeout esteso a 10 minuti (TimeSpan.FromMinutes(10)). Implementato download in streaming progressivo direttamente su file temporaneo su disco (File.Create(tempZipPath)) tramite HttpCompletionOption.ResponseHeadersRead con buffer di lettura a blocchi da 80 KB (uffer(81919)). Calcolo dell'hash SHA-256 di verifica eseguito direttamente dallo stream del file su disco tramite SHA256.HashDataAsync(fs) in ComputeSha256Async, senza caricare l'archivio in memoria RAM.
  - Verifica: superate 640 asserzioni di regressione automatizzate con 0 errori in SessionTests.vb. Compilazione Release con 0 avvisi e 0 errori.


## 2026-11-03

- [x] **TASK-0054** [Alta] Integrazione Dashboard & Web Console di Hermes Agent via Tailscale (Mesh VPN & API/Token Auth)~~
  - **Descrizione**: Aggiunto il supporto nativo a **Hermes Agent** (Nous Research) come piattaforma di account in HidaChat, consentendo agli utenti di avere una tab dedicata in WebView2 per interagire con l'assistente (chat / TUI integrato), monitorare i log, gestire le skill e i task automatici (cron jobs) e configurare i parametri del gateway Hermes sia in locale che tramite rete mesh privata Tailscale.
  - **Funzionalità Implementate**:
    - Estensione del modello account (`AppAccounts.vb`, `AccountManager.vb`) con piattaforma `"Hermes"`, proprietà `IsHermes`, branding grafico (icona vettoriale SVG a tema elmo alato e brush distintivo `#00B0FF`), e porta proxy locale deterministica isolata (`18900+`).
    - Configurazione endpoint flessibile con URL predefinito `http://127.0.0.1:9119` (istanza dashboard locale standard) o host Tailscale privati.
    - Iniezione sicura del token di autenticazione o API Key tramite header HTTP `Authorization: Bearer <AuthToken>` con filtro WebView2 `WebResourceRequested` e supporto parametri URL.
    - Routing di rete con Tailscale mesh proxy (`tsnetd`) integrato con header di autenticazione e token locale `X-HidaChat-Local-Token`.
    - Aggiornamento della UI (`SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `MainWindow.xaml.vb`) con selezione piattaforma Hermes Agent, form di configurazione contestuale con label e tooltip dedicati, menu di aggiunta rapida e test di connettività HTTP in tempo reale.
    - Localizzazione completa multilingua (IT, EN, FR, ES, DE) in `Localization.vb`.
  - **Impatto**: Alto | **Sforzo**: Medio-Alto

- [x] **TASK-0053** [Alta] Integrazione Console Web GUI di OpenClaw via Tailscale (Mesh VPN & Gateway Token)~~
  - **Descrizione**: Aggiungere il supporto nativo a **OpenClaw** come piattaforma di account in HidaChat (accanto a WhatsApp e Telegram), consentendo di gestire sessioni di chat, monitoraggio log, esecuzione skill e configurazione del gateway AI OpenClaw all'interno di una tab dedicata isolata in WebView2.
  - **Specifiche Tecniche & Architettura**:
  -   1. **Modello Account & Configurazione (`AppAccounts.vb`, `AccountManager.vb`)**:
       - Estensione enum/proprietà `Platform`: aggiunta supporto `"OpenClaw"` con property booleana `IsOpenClaw`.
       - Nuove proprietà persistenti per l'account:
          - `ServerUrl` (String): URL del gateway OpenClaw (default: `http://127.0.0.1:18789` o dominio Tailnet).
          - `TailscaleIntegration` (Boolean): Flag per abilitare il routing tramite nodo Tailscale embedded (`tsnetd.exe`) con reverse proxy loopback e token di sicurezza locale.
       - Palette grafica dedicata: icona vettoriale SVG a tema (robot/claw/terminale) e brush distintivo (es. `#FF5722` arancione o `#7C4DFF` viola).
  -   2. **Integrazione di Rete & Tailscale Mesh VPN (tsnet Userspace)**:
       - **Companion `tsnetd.exe`**: Binario statico autonomo Go con stack gVisor e WireGuard (Zero Installation, nessun driver o client Tailscale di sistema richiesto).
       - **Loopback Reverse Proxy (`127.0.0.1:<randomPort>`)**: Ascolto su porta dinamica casuale ad uso esclusivo di WebView2, protetto da header/cookie `X-HidaChat-Local-Token`.
       - **Persistenza Portabile (`data/tsnet/`)**: Lo stato del nodo viaggia con la cartella dati di HidaChat mantenendo stabile l'identità del dispositivo sulla Tailnet.
       - **Flusso Autenticazione & Login**: Gestione stati `Running`, `NeedsLogin` (con apertura browser) e supporto AuthKey cifrata con Windows DPAPI.
         - Fallback e validazione: test di connettività asincrono (ping HTTP/healthcheck su `/api/health` o endpoint radice) prima della navigazione, con visualizzazione dello stato online/offline del nodo gateway.
  -   3. **Gestione del Token & Flusso di Pairing**:
       - Composizione automatica dell'URL di bootstrap con token di autenticazione (`?token=<AuthToken>`) o iniezione sicura via header HTTP `Authorization: Bearer <AuthToken>` tramite evento `WebResourceRequested` di CoreWebView2.
       - Supporto al *token stripping* automatico di OpenClaw (il token viene rimosso dalla barra indirizzi dopo l'autenticazione per evitare leak visivi).
  -   4. **Notifiche & Monitoraggio Eventi**:
       - Intercettazione degli alert del Control UI di OpenClaw (completamento task agentici, errori del gateway, nuovi messaggi ricevuti) tramite bridge JavaScript dedicato (`NotificationJsScripts.vb`) e conversione in notifiche native **Windows Toast** e badge di notifica sulla scheda.
  -   5. **UI di Configurazione (`SettingsWindow.xaml`)**:
       - Selettore piattaforma esteso con opzione OpenClaw.
       - Form dinamico con campi specifici (URL Gateway, Token segreto con opzione visualizza/incolla, pulsante "Verifica Connessione").
  - **Impatto**: Alto | **Sforzo**: Medio-Alto


## 2026-11-01

- [x] **TASK-0108** [Alta] Ottimizzazioni estreme salvataggio chiusura: copia parallela bounded, log asincroni accorpati e skip account invariati~~
  - **File**: `ColdProfileCopy.vb`, `AppLogger.vb`, `NetworkProfileSync.vb`, `MainWindow.xaml.vb`, `Tests/SessionTests.vb`
  - **Problema / Esigenza**: Nonostante i progressi del TODO #107, la chiusura su dischi di rete Samba presentava ancora margini di miglioramento:
  -   1. I file di ciascun profilo venivano trasferiti sequenzialmente con flussi gestiti manuali e operazioni separate di impostazione dei metadati orari (`SetLastWriteTimeUtc`).
  -   2. Ogni singolo messaggio di log inviato ad `AppLogger` eseguiva controlli sincroni del file ed `AppendAllText` protetto da un lock globale, generando decine di round-trip I/O su rete SMB anche durante la fase di arresto.
  -   3. Gli account aperti ma senza nuove chat o modifiche venivano comunque esportati per intero.
  -   4. L'arresto del demone Tailscale `tsnetd` attendeva in modo seriale prima del completamento.
  - **Funzionalità Implementate**:
    - `ColdProfileCopy.vb`: copia parallela intra-account con `Parallel.ForEach` limitata a 4 thread per account (`MaxDegreeOfParallelism = 4`). Adozione di `File.Copy(src, dst, overwrite:=True)` per sfruttare Win32 `CopyFileEx`, preservazione nativa dei timestamp senza chiamate separate ai metadati e cancellazione non bloccante `.trash-*` via `Task.Run`.
    - `AppLogger.vb`: introduzione di una coda thread-safe non bloccante in memoria `ConcurrentQueue(Of LogEntry)` e scritture accorpate batch via timer (250ms) e `FlushNow()`, riducendo decine di accessi Samba a un'unica scrittura.
    - `NetworkProfileSync.vb`: rilevamento differenziale smart `HasModifications(source, master)` per saltare completamente la sincronizzazione degli account non modificati (`[EXPORT_UNCHANGED]`, 0 ms I/O).
    - `MainWindow.xaml.vb`: avvio concorrente di `TsnetManager.Instance.ShutdownAsync()` durante la chiusura di WebView2, mascherando la latenza di spegnimento (~300ms).
    - `Tests/SessionTests.vb`: aggiunte nuove asserzioni unitarie per `HasModifications` e batched `AppLogger` (totale 601 test superati con 0 errori).
  - **Test di accettazione**: Verificare la chiusura ultra-rapida su unità di rete `Y:` con sincronizzazione immediata dei profili modificati, 0 I/O per account non modificati e log compatti senza latenze residue.
  - **Priorità**: P1 (Gravità alta) | **Impatto**: Altissimo | **Sforzo**: Basso

- [x] **TASK-0107** [Alta] Ottimizzazione sincronizzazione profili di rete e salvataggio ultra-rapido alla chiusura~~
  - **File**: `ColdProfileCopy.vb`, `NetworkProfileSync.vb`, `AccountManager.vb`, `Tests/SessionTests.vb`
  - **Problema / Esigenza**: Quando HidaChat viene eseguito da un'unità di rete (SMB, percorso UNC o mapped network drive come `Y:`), la chiusura dell'applicazione richiedeva fino a 90-120 secondi. La funzione `ShouldSkip` cercava prefissi cablati `EBWebView/Default/Cache` non corrispondenti alla radice dei profili nativi WebView2 (`EBWebView\wv2profile_account_*`), causando la copia indiscriminata di oltre 2.200 file e 550 MB di sola cache per account. Inoltre, `output.Flush(flushToDisk:=True)` forzava round-trip sincroni `FlushFileBuffers` su SMB per ogni file, `.previous` veniva cancellata ricorsivamente in modo bloccante generando errori `Access Denied`, e tutti gli account venivano sincronizzati in modo sequenziale con un unico semaforo globale.
  - **Funzionalità Implementate**:
    - Filtro selettivo `SkipFolderNames` in `ColdProfileCopy.vb`: esclusione immediata alla radice di `Cache`, `Code Cache`, `GPUCache`, `DawnCache`, `ShaderCache`, `Crashpad`, `CacheStorage`, `ScriptCache`, ecc., preservando al 100% `IndexedDB`, `Local Storage`, `Cookies`, `Preferences`, `Local State` e `Service Worker/Database`. Riduzione del 94% dei file e del 97% del volume da trasferire.
    - Rimozione del `Flush(flushToDisk:=True)` sincrono in `CopyTree` per permettere la saturazione della banda SMB di rete.
    - Rotazione atomica tramite rename transitorio `.trash-<guid>` (5 ms) prima di muovere `target` a `.previous` e `incoming` a `target`, eliminando qualsiasi conflitto o eccezione `Access Denied` su SMB.
    - Sostituzione del lock globale con semafori per account (`ConcurrentDictionary(Of String, SemaphoreSlim)`) in `NetworkProfileSync.vb`.
    - Parallelizzazione della sincronizzazione di tutti gli account con `Task.WhenAll` in `AccountManager.SyncAllProfilesToNetworkAsync()`.
    - 26 nuove asserzioni unitarie di regressione in `Tests/SessionTests.vb` (totale 589 test con 0 fallimenti).
  - **Test di accettazione**: Su disco di rete `Y:`, verificare che la chiusura dell'applicazione completi la sincronizzazione di tutti i profili in meno di 2 secondi complessivi senza errori `Access Denied` nei log e con tutte le sessioni preservate al riavvio.
  - **Priorità**: P1 (Gravità alta) | **Impatto**: Altissimo | **Sforzo**: Basso

- [x] **TASK-0101** [Bassa] Finestra About: icona sbiadita e descrizione troncata senza a capo~~
  - **File**: AboutWindow.xaml, AboutWindow.xaml.vb
  - **Problema / Esigenza**:
  -   1. L'icona dell'applicazione nell'header della finestra About risultava sbiadita a causa del caricamento diretto del file .ico senza opzioni di scaling e rendering ad alta fedeltà.
  -   2. Il testo descrittivo dell'applicazione (TxtDescription) veniva tagliato orizzontalmente senza andare a capo poiché inserito in uno StackPanel orizzontale privo di vincoli di larghezza (AvailableSize.Width = Infinity).
  - **Implementazione**:
    - Aggiornato AboutWindow.xaml impostando l'icona con la sorgente ad alta risoluzione images/icon.png, corredata di RenderOptions.BitmapScalingMode="HighQuality", SnapsToDevicePixels="True" e UseLayoutRounding="True" per una resa visiva perfetta e nitida.
    - Sostituito lo StackPanel orizzontale con una Grid a 2 colonne (Auto per l'icona e * per il contenitore testi), garantendo a TxtDescription una larghezza delimitata su cui applicare TextWrapping="Wrap" per mandare a capo fluidamente il testo descrittivo in tutte le lingue supportate (EN, IT, FR, ES, DE) senza alcun troncamento ai margini.
    - Rifinito ApplyTheme() in AboutWindow.xaml.vb con gestione dinamica del Foreground di TxtDescription (#aebac1 in Dark Mode e #54656f in Light Mode).
  - **Priorità**: P3 (Gravità bassa) | **Impatto**: Basso-Medio | **Sforzo**: Basso

- [x] **TASK-0100** [Bassa] Dichiarazione di variabile locale non utilizzata subDirs in UpdateChecker.vb~~
  - **File**: UpdateChecker.vb
  - **Problema / Esigenza**: In UpdateChecker.vb era presente la riga Dim subDirs = Directory.GetDirectories(tempDir) non utilizzata, che generava warning del compilatore e provocava un'inutile operazione di I/O sul filesystem durante l'estrazione.
  - **Implementazione**: Rimossa completamente la dichiarazione orfana subDirs nel flusso di estrazione degli aggiornamenti, adottando la scansione mirata dell'eseguibile tramite Directory.GetFiles(tempDir, "HidaChat.exe", SearchOption.AllDirectories). Compilazione verificata con zero avvisi (0 warning) e zero errori.
  - **Priorità**: P3 (Gravità bassa) | **Impatto**: Basso | **Sforzo**: Minimo

- [x] **TASK-0073** [Alta] Supporto Drive di Rete tramite Local Staging & Sync delle Sessioni (Network Drive Session Persistence)~~
  - **File**: `NetworkProfileSync.vb`, `AppAccounts.vb`, `AccountManager.vb`, `MainWindow.xaml.vb`, `SettingsController.vb`, `SettingsWindow.xaml`
  - **Problema**: Eseguendo l'applicazione portabile direttamente da una cartella o unità di rete (condivisione SMB/CIFS, percorsi UNC `\\server\share` o drive mappati `Z:\`), la sessione di WhatsApp Web viene frequentemente persa o resettata alla schermata del QR code al riavvio dell'applicazione. La causa è architetturale di Chromium/WebView2: il motore LevelDB e SQLite (impiegati da WhatsApp Web tramite IndexedDB per memorizzare chiavi crittografiche, session token e messaggi) richiedono semantiche di lock POSIX/NTFS locali. Su filesystem SMB di rete si verificano ritardi o conflitti di lock (`STATUS_FILE_LOCK_CONFLICT`) che portano Chromium a marcare i database come corrotti o inaccessibili al riavvio, innescandone la cancellazione/reset automatico.
  - **Implementazione**:
  -   1. *Rilevamento drive di rete*: `NetworkProfileSync.IsRunningOnNetwork` rileva percorsi UNC e unità di rete mappate SMB (`DriveType.Network`).
  -   2. *Local Staging trasparente*: `NetworkProfileSync.SyncMasterToLocalStagingAsync` sincronizza all'avvio solo i dati essenziali (`IndexedDB`, `Local Storage`, `Network/Cookies`, preferenze) nella directory locale `%LOCALAPPDATA%\HidaChat\NetworkProfiles\{AccountId}`, escludendo le cartelle di cache volatile (`Cache`, `Code Cache`, `GPUCache`, `Crashpad`) per trasferimenti ultra-rapidi.
  -   3. *Esecuzione WebView2 in locale*: `CoreWebView2Environment` viene inizializzato sulla cartella locale garantendo velocità nativa e lock locali senza conflitti SMB.
  -   4. *Sincronizzazione di ritorno atomica*: Alla chiusura in `PrepareForShutdown` o prima degli aggiornamenti in `ForceExitForUpdateAsync`, `SyncAllProfilesToNetworkAsync` esegue il mirror incrementale dei profili aggiornati verso `data/webview/{AccountId}` sulla share di rete.
  -   5. *Lock multi-PC*: `AcquireSessionLock` rileva se l'account è già aperto su un altro computer avvisando l'utente.
  -   6. *Opzione nelle Impostazioni*: Checkbox dedicata e stringhe sincronizzate in tutte le 5 lingue supportate (EN, IT, FR, ES, DE).
  - **Impatto**: Alto | **Sforzo**: Medio-Alto


## 2026-10-31

- [x] **TASK-0105** [Media] Routine di pulizia automatica cartelle spazzatura all'avvio~~
  - **File**: `StartupJunkCleanupPolicy.vb`, `Application.xaml.vb`, `AccountManager.vb`, `Tests/SessionTests.vb`
  - **Descrizione**: Implementata la rimozione automatica e sicura all'avvio dell'applicazione di directory temporanee di diagnostica, cache shader GPU e file orfani (.dmp, .pma, .old) prima dell'inizializzazione dei processi WebView2, preservando integralmente cookie, IndexedDB (.leveldb / .blob), LocalStorage e credenziali master in Local State.
  - **Funzionalità Implementate**:
    - Modulo dedicato `StartupJunkCleanupPolicy.vb` con whitelist rigorosa di cartelle spazzatura (`JunkFolderNames`: Crashpad, GPUCache, ShaderCache, GrShaderCache, DawnCache, DawnGraphiteCache, DawnWebGPUCache, GPUPersistentCache, Code Cache, Cache, BrowserMetrics, component_crx_cache, OptimizationGuidePredictionModels, Safe Browsing, Subresource Filter) ed estensioni spazzatura (`.dmp`, `.pma`, `.old`).
    - Protezione insuperabile delle strutture di sessione (`ProtectedFolderNames`: IndexedDB, Local Storage, Session Storage, Sync Data, Network, Default, EBWebView, e cartelle `.leveldb`/`.blob`; `ProtectedFileNames`: Local State, Preferences, Cookies, settings.json, .lock, .app_version).
    - Algoritmo di scansione sicura ad albero guidato da coda che arresta l'esplorazione quando incontra cartelle spazzatura ed esclude qualsiasi accesso alle cartelle protette di sessione, prevenendo errori di concorrenza su filesystem ed eliminazioni accidentali di dati di login.
    - Invocazione automatica non bloccante in `Application.xaml.vb` all'interno di `OnStartup` subito dopo l'acquisizione del folder lock e prima del rilascio verso i controlli UI/WebView2.
    - Invocazione automatica in `AccountManager.LoadAccountsAsync()` e in `CleanupTransientCachesAsync()` sulla directory condivisa `data/webview`.
    - Suite di test automatizzati estesa in `Tests/SessionTests.vb`: aggiunte 106 nuove asserzioni a copertura di riconoscimento cartelle spazzatura, immunità delle cartelle di sessione (.leveldb/.blob/Local Storage), individuazione estensioni e simulazione completa di pulizia su gerarchia di directory (totale **563 asserzioni automatiche con 0 errori**).
  - **File**: `Application.xaml.vb`, `AccountManager.vb`
  - **Problema / Esigenza**: All'avvio dell'applicazione, prima che i processi `msedgewebview2.exe` vengano instanziati e blocchino i file su disco, rimangono file e sottocartelle temporanee create da crash, diagnostica o shader che occupano spazio non necessario nella cartella `data/webview`.
  - **Soluzione proposta / Fix da implementare**:
  -   Eseguire prima dell'inizializzazione dei controlli WebView2 la rimozione sicura delle cartelle temporanee che non contengono mai informazioni di login o sessione:
  -   ```vb
  -   Imports System.IO
  -   Public Sub CleanWebViewJunkFolders(baseDataPath As String)
  -       If Not Directory.Exists(baseDataPath) Then Return
  -       ' Cartelle sicure da eliminare al 100% (non contengono mai dati di login)
  -       Dim junkFolderNames As String() = {
  -           "Crashpad",
  -           "GrShaderCache",
  -           "ShaderCache",
  -           "GraphiteDawnCache",
  -           "DawnCache",
  -           "GPUCache",
  -           "Code Cache",
  -           "Cache",
  -           "BrowserMetrics",
  -           "component_crx_cache",
  -           "OptimizationGuidePredictionModels",
  -           "Safe Browsing"
  -       }
  -       Try
  -           For Each dirPath In Directory.EnumerateDirectories(baseDataPath, "*", SearchOption.AllDirectories)
  -               Dim dirName As String = Path.GetFileName(dirPath)
  -               If junkFolderNames.Contains(dirName, StringComparer.OrdinalIgnoreCase) Then
  -                   Try
  -                       Directory.Delete(dirPath, recursive:=True)
  -                   Catch
  -                       ' Ignora se un file è bloccato o già rimosso
  -                   End Try
  -               End If
  -           Next
  -           ' Elimina anche eventuali file .dmp o .pma (metriche e crash dump)
  -           For Each filePath In Directory.EnumerateFiles(baseDataPath, "*.*", SearchOption.AllDirectories)
  -               Dim ext As String = Path.GetExtension(filePath).ToLowerInvariant()
  -               If ext = ".dmp" OrElse ext = ".pma" OrElse ext = ".old" Then
  -                   Try
  -                       File.Delete(filePath)
  -                   Catch
  -                   End Try
  -               End If
  -           Next
  -       Catch ex As Exception
  -           ' Log opzionale
  -       End Try
  -   End Sub
  -   ```
  -   *Attenzione alle cartelle IndexedDB*: WhatsApp memorizza i file multimediali scaricati in `IndexedDB\https_web.whatsapp.com_0.indexeddb.blob`, mentre i token crittografici e di sessione Signal si trovano nella gemella `.leveldb`. La cartella `.leveldb` non deve mai essere rimossa per evitare la perdita della sessione utente.
  - **Test di accettazione**: Simulare la presenza di file `.dmp`, `.pma` e cartelle `Crashpad` o `GPUCache` nella cartella dati; verificare all'avvio che vengano rimosse senza toccare `.leveldb` o i cookie e che l'avvio prosegua regolarmente.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Medio | **Sforzo**: Basso


## 2026-10-30

- [x] **TASK-0104** [Media] Pulizia chirurgica a caldo della cache con ClearBrowsingDataAsync senza deautenticare gli account~~
  - **File**: `CacheCompactionPolicy.vb`, `AppAccounts.vb`, `AccountManager.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`, `Tests/SessionTests.vb`
  - **Descrizione**: Implementata la pulizia chirurgica a caldo della cache di rete e dei Service Worker a runtime tramite l'API `ClearBrowsingDataAsync` isolata sui dati rigenerabili, preservando al 100% cookie, LocalStorage, IndexedDB e le sessioni attive di WhatsApp Web, Telegram Web, OpenClaw ed Hermes.
  - **Funzionalità Implementate**:
    - Modulo dedicato `CacheCompactionPolicy.vb` con le maschere deterministiche `SafeRegenerableDataKinds` (`DiskCache Or CacheStorage`) e `DangerousSessionKindsMask` (Cookies, IndexedDB, LocalStorage, password, credenziali, impostazioni).
    - Funzione di validazione di sicurezza `IsSafeCompactionKind` e `IsSafeRegenerableDataKinds` per inibire tassativamente qualsiasi cancellazione accidentale di token di login.
    - Metodi `CompactWebViewProfileAsync(webView)` e `CompactProfileAsync(profile)` per l'esecuzione della compattazione non bloccante a caldo sul profilo attivo.
    - Metodo `CompactAllWebViewsProfileAsync()` in `AccountManager.vb` che itera e compatta a caldo tutti i profili con istanza WebView2 attiva.
    - Aggiornamento di `ClearBrowsingCacheAsync` in `AppAccounts.vb` per includere `CacheStorage` assieme a `DiskCache`.
    - Pulsante dedicato "Ottimizza Spazio" (`BtnOptimizeStorage`) e hint descrittivo nella sezione "Cache & Storage" delle Impostazioni (`SettingsWindow.xaml` e `SettingsWindow.xaml.vb`), con riscontro visivo del completamento.
    - Sincronizzazione integrale dei 5 dizionari multilingua ufficiali (`Localization.vb`: Inglese, Italiano, Francese, Spagnolo, Tedesco) con 4 nuove chiavi (`optimize_storage_btn`, `optimize_storage_hint`, `optimize_storage_success_title`, `optimize_storage_success_msg`).
    - Suite di test unitari automatizzati in `Tests/SessionTests.vb`: aggiunte 46 nuove asserzioni di regressione per validare le maschere di sicurezza, il respingimento dei flag sensibili, il calcolo delle directory di cache rigenerabili e la formattazione dimensioni (totale **457 asserzioni automatiche con 0 errori**).
  - **File**: `SettingsWindow.xaml.vb`, `AccountManager.vb`
  - **Problema / Esigenza**: Durante l'uso prolungato, WebView2 accumula centinaia di megabyte di `DiskCache` e `CacheStorage` (Service Worker cache). I metodi tradizionali di cancellazione cartella rischiano di spazzare via Cookie, LocalStorage e IndexedDB provocando il logout forzato da WhatsApp Web e Telegram Web.
  - **Soluzione proposta / Fix da implementare**:
  -   Fornire una funzione di compattazione a caldo richiamabile manualmente (es. pulsante "Ottimizza Spazio" nelle Impostazioni) o su base periodica programmata, invocando `CoreWebView2.Profile.ClearBrowsingDataAsync` solo sui dati rigenerabili:
  -   ```vb
  -   Imports Microsoft.Web.WebView2.Core
  -   Public Async Function CompactWebViewProfileAsync(webView As Microsoft.Web.WebView2.Wpf.WebView2) As Task
  -       If webView?.CoreWebView2?.Profile Is Nothing Then Return
  -       ' Selezioniamo SOLO le cache rigenerabili, escludendo Cookies, LocalStorage e IndexedDB!
  -       Dim dataKinds As CoreWebView2BrowsingDataKinds =
  -           CoreWebView2BrowsingDataKinds.DiskCache Or
  -           CoreWebView2BrowsingDataKinds.CacheStorage
  -       Await webView.CoreWebView2.Profile.ClearBrowsingDataAsync(dataKinds)
  -   End Function
  -   ```
  - **Test di accettazione**: Invocare la pulizia a caldo su una sessione attiva di WhatsApp Web con messaggi aperti; verificare che la dimensione della cache su disco si riduca sensibilmente e che la sessione non venga disconnessa (nessuna richiesta di nuovo QR code al riavvio/refresh).
  - **Priorità**: P2 (Gravità media) | **Impatto**: Medio-Alto | **Sforzo**: Basso


## 2026-10-29

- [x] **TASK-0103** [Media] Condivisione di un unico CoreWebView2Environment con profili nativi (ProfileName)~~
  - **File**: `ProfileIsolationPolicy.vb`, `AppAccounts.vb`, `AccountManager.vb`, `MainWindow.xaml.vb`
  - **Problema / Esigenza**: La creazione di percorsi cartella isolati separati per ciascun account (`data/webview/account_1`, `data/webview/account_2`, ecc.) con un `CoreWebView2Environment` differente per ciascuna scheda moltiplica per ogni account tutti i file di sistema, log e binari interni di Chromium (`EBWebView`).
  - **Soluzione proposta / Fix da implementare**:
  -   Utilizzare una sola cartella dati base condivisa (`data/webview`) con un singolo `CoreWebView2Environment`, separando gli account tramite i profili nativi di WebView2 con `CoreWebView2ControllerOptions.ProfileName`:
  -   ```vb
  -   ' Usa lo stesso _sharedEnvironment ("data/webview") per tutte le tab:
  -   Dim controllerOptions = _sharedEnvironment.CreateCoreWebView2ControllerOptions()
  -   controllerOptions.ProfileName = $"Account_{account.Id}" ' Es. "Account_1", "Account_2"
  -   controllerOptions.IsInPrivateModeEnabled = False
  -   Await webView.EnsureCoreWebView2Async(_sharedEnvironment, controllerOptions)
  -   ```
  -   In questo modo Cookie, IndexedDB e LocalStorage rimangono isolati al 100% (in sottocartelle del profilo), mentre l'infrastruttura motore viene allocata una sola volta, con notevole risparmio di RAM e spazio su disco.
  - **Test di accettazione**: Aprire più account contemporaneamente (es. WhatsApp 1 e WhatsApp 2) verificando che le sessioni non entrino in conflitto, che i cookie/IndexedDB rimangano separati e che la cartella `EBWebView` non duplichi i file di sistema.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Alto | **Sforzo**: Medio


## 2026-10-27

- [x] **TASK-0079** [Alta] L’invio massivo può spedire una bozza e dichiarare un falso successo~~
  - **File**: `BulkSenderPolicy.vb`, `BulkSenderEngine.vb`, `Localization.vb`, `Tests/SessionTests.vb`
  - **Problema / Esigenza**: Su Telegram il messaggio previsto viene inserito soltanto se il campo è vuoto: una bozza preesistente può essere inviata al suo posto. Inoltre, su entrambe le piattaforme, il risultato diventa SENT dopo un click o un evento tastiera, senza verificare che il client abbia realmente accettato il messaggio. Manca anche una verifica esplicita dell’identità della chat prima dell’invio. Le prove con DOM simulato confermano sia l’invio della bozza sia il ritorno SENT senza alcuna conferma effettiva.
  - **Implementazione completata**:
    - Creato il modulo deterministico `BulkSenderPolicy.vb` con le routine di validazione `IsDraftConflict`, `MatchesRecipient`, `IsSendConfirmed`, `BuildWhatsAppScript`, `BuildTelegramScript` e `ParseScriptResult`.
    - **Rilevamento e Blocco Bozze Preesistenti**: Sia su Telegram che su WhatsApp, prima di procedere con l'azione di invio viene controllato il testo attualmente presente nella casella di digitazione; in presenza di una bozza in conflitto con il messaggio atteso, l'invio viene immediatamente interrotto con stato d'errore (`DRAFT_CONFLICT`) senza sovrascrivere o spedire la bozza.
    - **Conferma Osservabile del Client**: Eliminata qualsiasi dichiarazione immediata di successo dopo il click o evento tastiera; l'esito passa dallo stato intermedio `TRIGGERED` e attende la conferma osservabile del client (`CONFIRMED_SENT`: svuotamento completo dell'input e/o commutazione del pulsante di invio in icona microfono) con timeout controllato e segnalazione puntuale in caso di invio respinto o ignorato (`SEND_REJECTED`).
    - **Localizzazione Multilingua Integrale**: Aggiunte 8 nuove chiavi dedicate agli errori e stati di invio massivo sincronizzate in tutti i 5 dizionari ufficiali di `Localization.vb` (Inglese, Italiano, Francese, Spagnolo, Tedesco).
    - **Suite di Test Automatizzati**: Aggiunte 76 asserzioni unitarie di regressione in `Tests/SessionTests.vb` per normalizzazione testo, rilevamento bozze, matching destinatari, conferma osservabile e parsing JSON (totale 333 asserzioni automatiche con 0 errori).
  - **Test di accettazione**: Bozza esistente, navigazione lenta, chat precedente ancora visibile e pressione di Invio ignorata non devono produrre un falso “Inviato”.
  - **Priorità**: P1 | **Impatto**: Alto | **Sforzo**: Medio


## 2026-10-26

- [x] **TASK-0087** [Alta] Finestra grafica dedicata per l'aggiornamento con stato di avanzamento (eliminazione script .ps1 / .bat)~~
  - **File**: `UpdateChecker.vb`, `UpdateProgressWindow.xaml` / `UpdateProgressWindow.xaml.vb` (o eseguibile helper compatto standalone `HidaChat.Updater.exe`), `Localization.vb`
  - **Problema / Esigenza**:
    - Attualmente il flusso di aggiornamento OTA si appoggia a script temporanei generati al volo (`.bat` o precedentemente ipotizzati `.ps1`) avviati tramite interprete/processo di shell Windows.
    - L'uso di script esterni presenta notevoli criticità e limiti:
  -     1. Assenza di feedback visivo grafico moderno per l'utente, che rischia di vedere finestre nere del prompt dei comandi / console oppure nessun indicatore chiaro mentre l'applicazione principale si chiude per consentire la sostituzione dei file;
  -     2. Vulnerabilità ai blocchi euristici degli antivirus o alle policy di esecuzione restrittive del sistema operativo (es. restrizioni su script PowerShell `.ps1` o comandi batch non firmati);
  -     3. Rischio che l'utente ritenga l'applicazione terminata o bloccata e tenti di riavviarla manualmente prima che la copia dei file e la sovrascrittura siano completate.
  - **Fix da implementare**:
  -   1. *Eliminazione definitiva di script esterni (`.ps1` e `.bat`)*:
       - Rimuovere completamente la generazione e l'esecuzione di script `.ps1` o `.bat`.
       - Gestire l'intero ciclo di aggiornamento tramite codice compilato nativo (.NET / WPF) o un piccolo processo helper compilato dedicato (es. `HidaChat.Updater.exe` o finestra modale interna con handoff a processo secondario) completamente autonomo e privo di dipendenze da interpreti shell esterni.
  -   2. *Finestra grafica con indicatore di avanzamento (Progress Dialog)*:
       - Creare una finestra grafica dedicata con interfaccia scura moderna e stile coerente con HidaChat (in linea con `ShutdownProgressDialog` e `StorageRecoveryProgressDialog`).
       - Barra di avanzamento (ProgressBar determinata con percentuale di completamento e MB scaricati durante il download; ProgressBar fluida/indeterminata durante verifica hash ed estrazione/copia file).
       - Visualizzazione chiara e in tempo reale delle fasi dell'aggiornamento:
         - *Scaricamento del pacchetto di aggiornamento da GitHub*;
         - *Verifica integrità crittografica SHA-256 e firma Authenticode*;
         - *Chiusura ordinata e attesa rilascio risorse dei processi HidaChat e WebView2*;
         - *Estrazione e sovrascrittura sicura dei file dell'applicazione* (preservando rigorosamente la cartella dati `data/` al 100% portabile);
         - *Riavvio automatico di HidaChat*.
  -   3. *Gestione degli errori e resilienza*:
       - In caso di errore durante una qualsiasi fase (timeout download, discrepanza checksum SHA-256, lock I/O su file in uso), mostrare un messaggio chiaro e localizzato all'interno della finestra grafica con pulsante "Annulla" o "Riprova", senza mai corrompere l'installazione o chiudersi silenziosamente.
  -   4. *Localizzazione multilingua integrale*:
       - Aggiungere e sincronizzare tutti i testi, messaggi di avanzamento e titoli delle fasi nei 5 dizionari ufficiali (`Localization.vb`: Inglese, Italiano, Francese, Spagnolo, Tedesco).
  - **Test di accettazione**:
    - Eseguire la procedura di aggiornamento e verificare che appaia la finestra grafica con barra di progresso e cambio di stato visibile;
    - Verificare che non venga eseguito né creato alcun file script `.ps1` o `.bat` nella cartella `%TEMP%` o altrove;
    - Verificare il corretto handoff: chiusura dell'app principale, copia dei file, riavvio automatico e preservazione integrale di tutti i dati in `data/`.
  - **Priorità**: P2 | **Impatto**: Alto | **Sforzo**: Medio
  - ---


## 2026-10-24

- [x] **TASK-0106** [Bassa] Compressione trasparente NTFS (compact.exe) per la cartella dati portabile~~
  - **File**: `NtfsCompressionPolicy.vb`, `Application.xaml.vb`, `AccountManager.vb`, `Tests/SessionTests.vb`
  - **Problema / Esigenza**: La cartella `data/webview` contiene file altamente comprimibili (database LevelDB, JSON, JavaScript, log di testo). Comprimere la cartella in un archivio ZIP/7z in chiusura e decomprimerla all'avvio comporterebbe sensibili ritardi di avvio e il rischio critico di corruzione in caso di crash o arresto improvviso del sistema.
  - **Funzionalità Implementate**:
    - Modulo dedicato `NtfsCompressionPolicy.vb` con metodo `EnableNtfsCompression(folderPath)` e costruttore argomenti `BuildCompactArguments` (`/C /S:"..." /I /Q`).
    - Invocazione non bloccante in background di `compact.exe` all'avvio in `Application.xaml.vb` sulla cartella principale `data/` al momento dell'acquisizione del folder lock.
    - Invocazione automatica in `AccountManager.LoadAccountsAsync()` sulla directory condivisa `data/webview`.
    - Gestione sicura di filesystem rimovibili (FAT32/exFAT): il parametro `/I` ignora silenziosamente filesystem non supportati senza errori o ritardi.
    - Test automatizzati unitari in `Tests/SessionTests.vb` a copertura di validazione parametri CLI, gestione percorsi null/whitespace e creazione directory.
  - **Test di accettazione**: Su disco NTFS, verificare che le proprietà della cartella `data/webview` presentino l'attributo "Compresso" attivo, con una riduzione di spazio occupato su disco di circa il 40-50% senza impatto negativo sulle prestazioni di lettura/scrittura di WebView2.
  - **Priorità**: P3 (Gravità bassa) | **Impatto**: Medio | **Sforzo**: Basso

- [x] **TASK-0045** [Media] Utility Bar & Gestione Rapida Audio / Dispositivi~~
  - **Descrizione**: Aggiungere opzioni di utilità rapida nella barra del titolo o nel menu contestuale delle schede:
    - **Mute/Unmute rapido** dell'audio dell'account corrente (`CoreWebView2.IsMuted = True/False`).
    - **Indicatore multimediale** (icona altoparlante sulla scheda quando un account sta riproducendo note vocali, notifiche sonore o video).
    - Scorciatoia rapida "Copia link chat" o "Cancella cache account".
  - **Funzionalità Implementate**:
    - Pulsante dedicato `BtnMuteActiveTab` nella barra del titolo con icona dinamica (altoparlante attivo `#00a884` quando in riproduzione o `#8696a0`, barrato in magenta `#ec4899` se silenziato) e scorciatoia globale `Ctrl+M`.
    - Pulsante dedicato `BtnCopyLink` nella barra del titolo e scorciatoia globale `Ctrl+Shift+C` per copiare istantaneamente il link/URL della chat o dell'istanza attiva negli appunti con feedback Toast discreto (`MessagePopup`).
    - Indicatore multimediale interattivo `TabMediaButton` integrato su ogni singola scheda: mostra `??` in caso di audio/video in riproduzione o `??` se l'account è silenziato, con possibilità di silenziare/riattivare l'audio con un singolo click diretto senza abbandonare la scheda attiva.
    - Voci dedicate nel menu contestuale delle schede (`AccountTab_ContextMenuOpened`): toggle contestuale dinamico "Disattiva/Attiva audio scheda" (`account_mute_tab` / `account_unmute_tab`), "Copia link chat / URL" (`account_copy_link`) e pulizia mirata cache (`account_clean_cache`).
    - Supporto multilingua completo nei 5 dizionari ufficiali (`Localization.vb`: EN, IT, FR, ES, DE) e test di regressione unitari.
  - **Impatto**: Medio | **Sforzo**: Basso-Medio


## 2026-10-23

- [x] **TASK-0102** [Media] Limitare la cache WebView2 alla fonte tramite AdditionalBrowserArguments~~
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
  -   Passare argomenti di avvio specifici a Chromium tramite la proprietà `AdditionalBrowserArguments` di `CoreWebView2EnvironmentOptions` per imporre un tetto massimo (hard limit) su disco ed escludere componenti ridondanti:
  -   ```vb
  -   Imports Microsoft.Web.WebView2.Core
  -   Public Async Function CreateOptimizedEnvironmentAsync(userDataFolder As String) As Task(Of CoreWebView2Environment)
  -       ' Argomenti Chromium per limitare drasticamente lo spazio su disco
  -       Dim args As String = String.Join(" ", {
  -           "--disk-cache-size=52428800",        ' Max 50 MB di cache HTTP per profilo
  -           "--media-cache-size=20971520",       ' Max 20 MB di cache audio/video
  -           "--aggressive-cache-discard",        ' Scarta subito la cache vecchia
  -           "--disable-gpu-shader-disk-cache",   ' Non salvare gli shader GPU su disco
  -           "--disable-component-update",        ' Blocca il download di componenti CRX in background
  -           "--disable-background-networking",   ' Evita download di telemetria/modelli Edge
  -           "--disable-features=OptimizationGuideModelDownloading,OptimizationHintsFetching,Translate"
  -       })
  -       Dim options As New CoreWebView2EnvironmentOptions(
  -           additionalBrowserArguments:=args,
  -           language:=Nothing,
  -           targetCompatibleBrowserVersion:=Nothing,
  -           allowSingleSignOnUsingOSPrimaryAccount:=False
  -       )
  -       Return Await CoreWebView2Environment.CreateAsync(Nothing, userDataFolder, options)
  -   End Function
  -   ```
  - **Test di accettazione**: Verificare all'avvio che le istanze WebView2 vengano inizializzate con i flag specificati, che la cache HTTP non ecceda il tetto prefissato e che la navigazione su WhatsApp e Telegram resti pienamente funzionante.
  - **Priorità**: P2 (Gravità media) | **Impatto**: Alto | **Sforzo**: Basso


## 2026-10-20

- [x] **TASK-0069** [Media] Traduzione: privacy e backend self-hosted~~
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


## 2026-10-19

- [x] **TASK-0068** [Alta] Piattaforme: da stringhe a modello tipizzato~~
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


## 2026-10-18

- [x] **TASK-0078** [Alta] I token vengono aggiunti alle richieste verso qualsiasi porta localhost~~
  - **File**: `LocalAuthPolicy.vb`, `AppAccounts.vb`, `SettingsWindow.xaml.vb`, `Tests/SessionTests.vb`, `Tests/HidaChat.SessionTests.vbproj`
  - **Problema / Esigenza**: Il filtro utilizzato era `http://127.0.0.1:*/*`. Alle richieste intercettate venivano aggiunti il token locale e, quando disponibile, il Bearer token dell’account senza verificare che la porta fosse effettivamente quella assegnata al proxy di quell’account. Una richiesta verso un altro servizio locale poteva quindi ricevere credenziali che non gli appartenevano.
  - **Implementazione completata**:
    - Creato il modulo `LocalAuthPolicy.vb` con il metodo di convalida rigorosa `ShouldAttachCredentials(requestUriStr, expectedScheme, expectedHost, expectedPort)` che verifica l'esatta corrispondenza di schema, host e porta prima dell'iniezione dei token, e `BuildFilterPattern` che vincola il pattern di filtro WebView2 alla porta TCP assegnata (`http://127.0.0.1:{port}/*`).
    - Refactoring completo di `EnsureLocalProxyAsync`, `UpdateWebResourceAuthFilter` e `RemoveWebResourceAuthFilter` in `AppAccounts.vb`: rimozione controllata dei vecchi filtri (`CoreWebView2.RemoveWebResourceRequestedFilter`) e distacco handler al variare di porta, target `ServerUrl`, credenziali o stato Tailscale (`TailscaleIntegration`).
    - Validazione rigorosa sia per il proxy locale tsnet (`127.0.0.1:{LocalProxyPort}` per `X-HidaChat-Local-Token` e `Bearer`) sia per connessioni gateway dirette (porta e authority esatte di `ServerUrl`).
    - Creata suite di test unitari automatizzati in `Tests/SessionTests.vb` che valida l'accettazione su porta corretta e il respingimento su porte differenti, host non coincidenti, schemi errati e tentativi di spoofing o subdomain attack.
  - **Priorità**: P1 | **Impatto**: Alto | **Sforzo**: Basso-Medio


## 2026-10-17

- [x] **TASK-0077** [Alta] Il proxy permette di saltare l’autenticazione locale~~
  - **File**: `tsnetd/main.go`, `tsnetd/main_test.go`
  - **Problema / Esigenza**: La condizione che riconosce una richiesta WebSocket la considerava automaticamente autorizzata (`isWs`). Bastava quindi la presenza di `Sec-WebSocket-Key`, anche senza un handshake valido o credenziali, per evitare il controllo del token ed ottenere nel Set-Cookie il token locale segreto.
  - **Implementazione completata**:
    - Rimosso qualsiasi bypass basato sulla presenza di header WebSocket (`Sec-WebSocket-Key` o `Upgrade: websocket`): sia le richieste HTTP che gli handshake WebSocket devono autenticarsi rigorosamente tramite header `X-HidaChat-Local-Token`, query parameter `__htok` o cookie `__htok`.
    - Verifica preventiva dell'Origin originale (`isAllowedOrigin`) prima della riscrittura o inoltro al backend: l'Origin deve corrispondere all'endpoint locale (`127.0.0.1:{port}`, `localhost:{port}`, `[::1]:{port}`) o all'endpoint target Tailscale atteso; qualsiasi Origin esterno malevolo o 'null' viene respinto con `403 Forbidden: Invalid Origin`, prevenendo Cross-Site WebSocket Hijacking (CSWSH) e attacchi cross-origin.
    - Rafforzato il cookie locale impostando `HttpOnly: true` e `SameSite: Lax` per prevenire l'esfiltrazione del token da script della pagina.
    - Creati test unitari completi in `tsnetd/main_test.go` per verificare che richieste HTTP e WebSocket senza credenziali falliscano (anche con header contraffatti), che Origin non autorizzati vengano bloccati e che handshake autenticati continuino a funzionare.
  - **Priorità**: P1 | **Impatto**: Alto | **Sforzo**: Medio


## 2026-10-15

- [x] **TASK-0076** [Alta] Controllo integrità storage/IndexedDB all'avvio con ProgressBar di ripristino dati corrotti~~
  - **File**: `StorageIntegrityService.vb`, `StorageRecoveryProgressDialog.xaml`, `StorageRecoveryProgressDialog.xaml.vb`, `AppAccounts.vb`, `MainWindow.xaml.vb`, `Localization.vb`
  - **Problema / Esigenza**: 
    - In seguito a chiusure improvvise, crash hardware, blocchi di sistema o spegnimenti forzati del PC durante la scrittura dei dati, i database IndexedDB di Chromium (come `tt-data` di Telegram Web A o i database Signal/LevelDB di WhatsApp Web) possono incorrere nella perdita di file blob su disco pur mantenendo i riferimenti attivi nei file di log/manifest di LevelDB.
    - Al successivo avvio, Chromium lancia l'eccezione irreversibile `NotReadableError: Data lost due to missing file. Affected record should be considered irrecoverable`. Le web app (in particolare Telegram Web) non gestiscono questa Promise rejection durante il bootstrap iniziale e interrompono l'esecuzione JavaScript prima del montaggio del DOM, lasciando all'utente una schermata completamente bianca ("pagina bianca").
  - **Implementazione completata**:
  -   1. *Controllo Preventivo all'Avvio / Caricamento Profilo*:
       - Creato il servizio `StorageIntegrityService.vb` con metodo `CheckIndexedDbCorruption(profileDir)` che analizza strutturalmente le cartelle `EBWebView\Default\IndexedDB\*.indexeddb.leveldb` prima dell'avvio di WebView2 (verifica esistenza ed integrità di `CURRENT`, validità e non-vacuità del file `MANIFEST-*` referenziato, e integrità delle directory blob associate).
  -   2. *Feedback Visivo con Finestra / ProgressBar di Recupero*:
       - Creata la finestra grafica dedicata `StorageRecoveryProgressDialog` (`WindowStyle="None"`, trasparente con bordo arrotondato e stile scuro nativo, `Topmost="True"`, ProgressBar indeterminata e indicatore di fase dinamico).
  -   3. *Esecuzione del Ripristino Mirato*:
       - Procedura automatizzata `PerformStorageRecoveryWithDialogAsync`: rimozione pulita delle cartelle IndexedDB/ServiceWorker corrotte salvaguardando integralmente `Local Storage`, file di preferenze, token DPAPI, cookie di sessione e credenziali d'accesso.
       - Integrazione completa sia all'avvio preventivo a freddo, sia a caldo tramite evento diagnostico `INDEXEDDB_CORRUPTED` in `HandleDiagnosticMessageAsync`, sia dallo strumento "Ripara Scheda" del menu contestuale.
  -   4. *Conclusione e Localizzazione*:
       - Chiusura automatica della ProgressBar e ricaricamento della pagina ad operazione conclusa;
       - Localizzazione completa dei messaggi e degli stati in tutte le 5 lingue supportate (EN, IT, FR, ES, DE).
  - **Impatto**: Alto | **Sforzo**: Medio

- [x] **TASK-0075** [Alta] Rilevamento pagina bianca su profili Hermes / OpenClaw e controllo stato connessione Tailscale~~
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


## 2026-10-14

- [x] **TASK-0074** [Media] Finestra / Dialog di avanzamento durante il salvataggio e chiusura dell'applicazione~~
  - **File**: `ShutdownProgressDialog.xaml`, `ShutdownProgressDialog.xaml.vb`, `MainWindow.xaml.vb`, `Localization.vb`
  - **Problema / Esigenza**: Quando l'utente chiude l'applicazione, vengono eseguite operazioni asincrone critiche in background (sincronizzazione dei profili verso percorsi di rete SMB/UNC, flush e salvataggio delle impostazioni `settings.json`, rilascio lock `data/.lock` e lock di sessione, dismissione controllata dei processi WebView2 e spegnimento del nodo/proxy tsnet). Senza un feedback visivo immediato, l'utente potrebbe percepire l'applicazione come bloccata o tentare di forzarne la chiusura dal Task Manager, causando potenziale troncamento del salvataggio o file lock pendenti.
  - **Implementazione completata**:
    - Creata la finestra grafica dedicata `ShutdownProgressDialog` (`WindowStyle="None"`, trasparente con bordo arrotondato e design scuro WhatsApp, `Topmost="True"`, ProgressBar indeterminata e indicatore di fase).
    - Gestite e notificate in tempo reale le fasi di spegnimento durante `ShutdownCoreAsync`:
  -     1. Salvataggio impostazioni e preferenze account;
  -     2. Chiusura controllata delle istanze browser WebView2;
  -     3. Sincronizzazione profili e chat su percorsi di rete (SMB/UNC) se abilitata;
  -     4. Arresto controllato del proxy Tailscale `tsnetd` e rilascio lock di sessione/directory;
  -     5. Finalizzazione e chiusura ordinata dell'applicazione.
    - Sincronizzate le stringhe e le descrizioni degli stati in tutte le 5 lingue supportate (EN, IT, FR, ES, DE).
  - **Impatto**: Medio-Alto | **Sforzo**: Medio


## 2026-10-13

- [x] **TASK-0072** [Media] Pulizia cache "preservando il login" (Smart Cache Cleanup)~~
  - **File**: `AccountManager.vb`, `AppAccounts.vb`, `MainWindow.xaml`, `MainWindow.xaml.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`
  - **Descrizione**: Implementata la pulizia cache mirata e non distruttiva che cancella unicamente cache volatile, file temporanei, shader e GPU cache (`Cache`, `Code Cache`, `GPUCache`, `DawnWebGPUCache`, `DawnGraphiteCache`, `Crashpad`), lasciando intatti `Local Storage`, `IndexedDB`, `Network\Cookies`, credenziali e sessioni di login.
  - **Implementazione**:
    - Metodo `SmartCleanCachePreservingLoginAsync()` in `AppAccounts.vb` che combina l'invocazione nativa `Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.DiskCache Or DownloadHistory)` a runtime con l'eliminazione dei file su disco non bloccati sia per il profilo principale sia per lo staging locale su drive di rete.
    - Metodo aggregato `SmartCleanCachePreservingLoginAsync()` in `AccountManager.vb` che itera e bonifica tutti i profili contemporaneamente.
    - Pulsante dedicato "Pulisci Cache (preserva login)" nella sezione "Cache & Archiviazione" di `SettingsWindow.xaml` con finestra di conferma e notifica di successo.
    - Voce di menu contestuale con click destro sulla scheda dell'account in `MainWindow.xaml` ("Pulisci cache account") per pulizia puntuale per singola sessione.
    - Localizzazione sincrona in tutte le 5 lingue ufficiali (EN, IT, FR, ES, DE).
  - **Impatto**: Medio | **Sforzo**: Basso-Medio

- [x] **TASK-0071** [Media] Limitazione della cache Chromium via AdditionalBrowserArguments (`--disk-cache-size`)~~
  - **File**: `AppAccounts.vb` (`SetupWebViewInternalAsync`), `SettingsController.vb`, `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `Localization.vb`
  - **Descrizione**: Limitata la cache su disco di Chromium via `AdditionalBrowserArguments` in fase di creazione dell'environment WebView2 con lo switch standard `--disk-cache-size=<bytes>`.
  - **Implementazione**:
    - Configurazione dinamica in `SettingsController.vb` con proprietà `DiskCacheLimitMb` (default: 256 MB, opzioni: 128 MB, 256 MB, 512 MB, 1024 MB, 0 = Illimitata) e salvataggio in `settings.json`.
    - Iniezione del parametro `--disk-cache-size={CLng(DiskCacheLimitMb) * 1024L * 1024L}` nella creazione di `CoreWebView2Environment` in `AppAccounts.vb`.
    - Menu a tendina dedicato `ComboDiskCacheLimit` nelle Impostazioni con localizzazione multilingua completa (EN, IT, FR, ES, DE).
  - **Impatto**: Medio | **Sforzo**: Basso

- [x] **TASK-0050** [Media] Ibernazione Intelligente delle Schede Inattive (Tab Hibernation)~~
  - **Descrizione**: Ridurre l'impronta di memoria RAM sospendendo lo stato di rendering delle schede in background non utilizzate da più di 15-60 minuti (configurabile, default 30 min), risvegliandole istantaneamente al click dell'utente.
  - **Implementazione**:
    - Aggiunti metodi `TryHibernateAsync()` e `ResumeFromHibernation()` in `AppAccounts.vb` basati sulle API native `CoreWebView2.TrySuspendAsync()` e `CoreWebView2.Resume()`, con tracciamento `LastActiveTime` e proprietà `IsHibernated`.
    - Timer periodico in background in `MainWindow.xaml.vb` (`_hibernationTimer`, 60s) che verifica l'inattività delle schede non in primo piano e le sospende in modo trasparente.
    - Risveglio istantaneo (0 ms di ritardo) al passaggio del focus sulla scheda in `SwitchToAccountAsync` o impostando `IsActive = True`.
    - Indicatore visivo `??` sulle schede ibernate nella barra orizzontale di `MainWindow.xaml`.
    - Configurazione nelle Impostazioni (`SettingsWindow.xaml`): toggle di attivazione `ChkEnableTabHibernation` e selettore intervallo `ComboTabHibernationTimeout` (15, 30, 60 min) con persistenza in `settings.json` e supporto multilingua completo in tutte le 5 lingue (EN, IT, FR, ES, DE).
  - **Impatto**: Medio | **Sforzo**: Medio


## 2026-10-11

- [x] **TASK-0070** [Media] Uso portabile: lock multi-PC e coerenza del README~~
  - **File**: `Application.xaml.vb` (`TryAcquireFolderLock`, `ReleaseFolderLock`, `OnStartup`, `OnExit`), `README.md`, `README.it.md`, `Localization.vb`
  - **Problema**: Il file README raccomandava di non eseguire la stessa istanza portabile contemporaneamente da postazioni differenti (rischio concreto di lock file e corruzione del profilo WebView2), ma l'applicazione non adottava alcun meccanismo di prevenzione a livello di filesystem. Inoltre, l'indicazione "Zero Installation" contrastava con la necessità del runtime .NET 9 per le pubblicazioni standard framework-dependent.
  - **Fix Implementato**:
    - Implementato a livello di avvio applicazione (`Application.OnStartup`) un file lock esclusivo sul filesystem `data/.lock` mantenuto aperto per l'intera durata della sessione;
    - Registrazione strutturata in formato JSON nel lock file dei metadati del processo (hostname, username, PID, data/ora ISO di avvio);
    - In caso di rilevamento di una contesa o sessione già attiva sulla medesima cartella (da altra macchina su rete o altro processo locale), visualizzazione di un messaggio bloccante all'utente (localizzato in tutte le lingue con dettaglio di macchina, PID e orario) ed arresto controllato dell'applicazione per impedire la corruzione dei profili WebView2;
    - Rilascio pulito e rimozione automatica di `data/.lock` alla chiusura dell'applicazione in `OnExit` e nei percorsi di shutdown;
    - Armonizzazione della documentazione in `README.md` e `README.it.md`: chiarito il meccanismo di protezione attiva del lock multi-PC `data/.lock`, ed esplicitato il requisito del .NET 9 Desktop Runtime chiarendo che l'indicazione "Zero Installazione" si riferisce alla distribuzione portabile (nessun installer di sistema necessario, nessun dato in registry o percorsi esterni).
  - **Impatto**: Medio | **Sforzo**: Basso-Medio
  - ---

- [x] **TASK-0065** [Bassa] SettingsFile: I/O e migrazione a ogni accesso~~
  - **File**: `SettingsController.vb` (proprietà `SettingsFile`, metodo `InitializeSettingsFilePath`)
  - **Problema**: Il getter della proprietà `SettingsFile` eseguiva verifiche I/O su filesystem (`File.Exists`, `Directory.CreateDirectory`) e tentava la migrazione `File.Move` a ogni singola operazione di lettura o scrittura delle impostazioni.
  - **Fix Implementato**:
    - Calcolo e memorizzazione persistente del percorso tramite `Lazy(Of String)` thread-safe (`_settingsFilePathLazy`), risolto una sola volta al primo accesso per l'intero ciclo di vita dell'applicazione;
    - Spostamento della logica di migrazione una tantum dalla cartella radice a `data/` all'interno della procedura di inizializzazione dedicata `InitializeSettingsFilePath`;
    - Rimozione totale di qualsiasi operazione di I/O o accesso al disco dal getter della proprietà `SettingsFile`.
  - **Impatto**: Basso | **Sforzo**: Basso

- [x] **TASK-0060** [Media] Segreti portabili: DPAPI CurrentUser vs portabilità~~
  - **File**: `SettingsController.vb` (`SaveTsnetAuthKeyAsync`, `LoadSettingsAsync`, `ClearTsnetAuthKeyAfterEnrollmentAsync`), `TsnetManager.vb` (`GetMachineStateDir`, `StartAsync`, `CheckStatusAsync`), `AppAccounts.vb` (`WebUrl`, `SetupWebViewInternalAsync`, `EnsureLocalProxyAsync`), `MainWindow.xaml.vb`, `Localization.vb`
  - **Problema**: L'uso di `ProtectedData` con `DataProtectionScope.CurrentUser` vincola i segreti all'utente e alla macchina Windows locale: spostando la cartella dell'applicazione su un altro PC (su chiavetta USB o disco esterno) `Unprotect` fallisce e il blocco `Catch` azzera la chiave in silenzio. Inoltre, in `AppAccounts.vb` il token di autenticazione per OpenClaw ed Hermes veniva accodato in chiaro come query parameter (`?token=`) nell'URL di navigazione, restando visibile nei log e nella cronologia. Questo comprometteva il principio di portabilità 100%.
  - **Fix Implementato**:
    - In caso di errore di decifratura DPAPI (`CryptographicException`), l'applicazione non azzera più il segreto silenziosamente: imposta `TsnetAuthKeyDecryptionFailed = True`, registra un log esplicito e mostra un dialogo di notifica all'utente richiedendo il reinserimento delle credenziali nelle Impostazioni;
    - Isolamento della directory di stato del nodo tsnet per singola macchina (`data/tsnet/<hostname>`), prevenendo conflitti e la clonazione dell'identità del nodo su host differenti in ambiente portabile;
    - Rilascio e rimozione automatica della `tsnetAuthKey` salvata una volta completato l'enrollment con successo (transizione a stato `Running`), evitando la persistenza permanente di chiavi monouso nel file di configurazione portabile;
    - Rimozione totale del parametro `token=` in chiaro dalla query string dell'URL per OpenClaw ed Hermes; il token di autenticazione viene ora iniettato in modo sicuro e trasparente tramite header HTTP `Authorization: Bearer <token>` intercettato via evento `WebResourceRequested` di CoreWebView2 (sia in modalità diretta che tramite proxy locale tsnet);
    - Aggiunte e sincronizzate le chiavi di notifica errore decifratura (`secret_decrypt_error_title`, `secret_decrypt_error_msg`) in tutti i 5 dizionari linguistici (`Localization.vb`: IT, EN, FR, ES, DE).
  - **Impatto**: Medio-Alto | **Sforzo**: Medio
  - ---

- [x] **TASK-0058** [Alta] `settings.json`: scrittura atomica, serializzata, con flush in chiusura~~
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


## 2026-09-30

- [x] **TASK-0064** [Bassa] DndUntil: confronto Nullable in VB.NET~~
  - **File**: `SettingsController.vb` (`DndUntil`, `SetDndModeAsync`)
  - **Problema**: L'istruzione `If _dndUntil <> value Then` con tipo `Nullable(Of DateTime)` adotta la logica a tre valori di VB.NET: il confronto con `Nothing` restituisce `Nothing` (valutato come `False` nelle condizioni `If`), impedendo al setter di salvare il nuovo valore quando uno dei due operandi è `Nothing`. Il difetto era precedentemente mascherato dal fatto che `SetDndModeAsync` e `LoadSettingsAsync` assegnavano direttamente la variabile di campo privata `_dndUntil`.
  - **Fix Implementato**:
    - Sostituito il confronto nel setter pubblico di `DndUntil` con `If Not Nullable.Equals(_dndUntil, value) Then ...`;
    - Riconfigurato `SetDndModeAsync` per mutare lo stato passando sempre attraverso i setter pubblici delle proprietà (`IsDndEnabled`, `DndUntil`, `DndDurationMode`), garantendo la corretta e sincrona emissione degli eventi `PropertyChanged` e `IsDndActive`.
  - **Impatto**: Basso | **Sforzo**: Basso
  - ---

- [x] **TASK-0063** [Alta] Async Sub senza gestione errori e Task non attesi nel bridge~~
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


## 2026-09-26

- [x] **TASK-0062** [Media] Bridge IPC: token da CSPRNG e validazione dell'origine~~
  - **File**: `AppAccounts.vb` (`GenerateBridgeToken`, `HandleWebMessageAsync`, `IsAuthorizedOrigin`, `SetupWebViewInternalAsync`)
  - **Problema**: Il token di sicurezza del bridge IPC viene generato combinando un timestamp Unix con `System.Random` (pseudo-casuale non crittografico, teoricamente prevedibile); l'handler `WebMessageReceived` / `HandleWebMessageAsync` valida il token ma non controlla l'origine (`e.Source`) del mittente del messaggio; la proprietà `AreDevToolsEnabled` viene impostata a `True` in modo incondizionato anche in ambienti di produzione.
  - **Fix Implementato**:
    - Generazione del token IPC di sicurezza della WebView tramite CSPRNG a 128 bit (`RandomNumberGenerator.Fill(randomBytes)`);
    - Validazione rigorosa dell'origine di provenienza del messaggio (`e.Source`) tramite la funzione dedicata `IsAuthorizedOrigin` prima del parsing e dell'elaborazione del payload IPC (`web.whatsapp.com` per WhatsApp, `web.telegram.org` per Telegram, host di `ServerUrl` o `127.0.0.1` per OpenClaw ed Hermes);
    - Disabilitazione dei DevTools (`AreDevToolsEnabled = False`) nelle build di produzione Release (abilitati unicamente sotto compilazione `#If DEBUG`).
  - **Impatto**: Medio | **Sforzo**: Basso

- [x] **TASK-0061** [Alta] NewWindowRequested: ShellExecute su qualsiasi schema URI~~
  - **File**: `AppAccounts.vb` (`_newWindowRequestedHandler`, `_navigationStartingHandler`)
  - **Problema**: Per qualsiasi URL che non appartiene agli host noti della piattaforma corrente, l'handler invoca `Process.Start(..., UseShellExecute = True)` senza validare lo schema dell'URI: link con schemi pericolosi o non controllati (`file:`, `ms-msdt:`, protocol handler registrati a livello di sistema) vengono eseguiti arbitrariamente. Inoltre, i link con attributo `target="_blank"` appartenenti alla stessa origine navigano l'istanza WebView corrente sovrascrivendo la sessione di chat aperta.
  - **Fix Implementato**:
    - Applicata una allow-list rigorosa degli schemi prima dell'apertura esterna (`http`, `https`, `mailto`), bloccando l'esecuzione arbitraria di protocol handler di sistema o schemi non autorizzati;
    - Estesa la validazione dello schema URI a `NavigationStarting`, consentendo solo schemi web standard (`http`, `https`, `data`, `blob`, `about`) e gestendo internamente i deep link Telegram (`tg://` e `t.me`);
    - Apertura nel browser predefinito di sistema di tutti i link `target="_blank"` o esterni per preservare integra la vista principale della sessione di chat all'interno della WebView2.
  - **Impatto**: Alto | **Sforzo**: Basso


## 2026-09-24

- [x] **TASK-0067** [Media] UpdateChecker: UX, localizzazione, confronto versioni, duplicazioni~~
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


## 2026-09-22

- [x] **TASK-0066** [Media] Limite massimo account: sincronizzazione e policy di downgrade~~
  - **File**: `AccountManager.vb` (`MaxAccounts`, `CanAddAccount`, `HasExcessAccounts`, `ExcessAccountsCount`, `AddAccountAsync`, `IDisposable`), `SettingsController.vb` (`MaxAccounts`), `SettingsWindow.xaml`, `SettingsWindow.xaml.vb`, `MainWindow.xaml`, `MainWindow.xaml.vb`, `Localization.vb`
  - **Problema**: In origine `AccountManager.MaxAccounts` era definito come costante rigida a 3. La proprietà era stata resa dinamica in funzione di `SettingsController.MaxAccounts` (range 2–10) ed era integrata nei controlli di `CanAddAccount` e `AddAccountAsync`. Mancava tuttavia l'ascolto automatico di `_settingsController.PropertyChanged` all'interno di `AccountManager` per notificare le modifiche a `CanAddAccount` e `MaxAccounts` indipendentemente dalla UI di `SettingsWindow`, nonché una policy formale nel caso in cui l'utente imposti un limite inferiore al numero di account già configurati.
  - **Fix Implementato**:
    - Sottoscrizione centralizzata dell'evento `PropertyChanged` di `SettingsController` nel costruttore di `AccountManager` per notificare automaticamente le modifiche a `MaxAccounts`, `CanAddAccount`, `HasExcessAccounts` ed `ExcessAccountsCount`; implementazione dell'interfaccia `IDisposable` in `AccountManager` per rilasciare pulitamente i listener;
    - Policy formale di downgrade non distruttivo: qualora il limite `MaxAccounts` venga abbassato al di sotto degli account configurati, gli account esistenti vengono interamente preservati e restano funzionanti, mentre viene bloccata l'aggiunta di ulteriori account e mostrato un banner esplicativo con avviso nelle Impostazioni (`BorderDowngradeNotice`);
    - Rimozione del tooltip statico hardcoded "(max 3)" dal pulsante `BtnAddAccount` in `MainWindow.xaml` e sincronizzazione dinamica di tooltip, stato abilitato/disabilitato e blocco aggiunta sia in `MainWindow` che in `SettingsWindow`;
    - Aggiunta e sincronizzazione delle chiavi di downgrade (`max_accounts_downgrade_notice`, `accounts_downgrade_blocked`) nei dizionari di tutte e 5 le lingue supportate (`Localization.vb`: IT, EN, FR, ES, DE).
  - **Impatto**: Medio | **Sforzo**: Basso

- [x] **TASK-0059** [Alta] Pulizia dei profili WebView2 non distruttiva~~
  - **File**: `AccountManager.vb` (`LoadAccountsAsync`, `CleanupUnusedProfilesAsync`, `MigrateOrphanProfile`, `CreateDefaultAccountAsync`, `RemoveAccountAsync`), `AppAccounts.vb` (`SetupWebViewInternalAsync`), `SettingsController.vb` (`IsLoadedFromValidConfig`, `ReadSettingsAsync`)
  - **Problema**:
    - Catena di perdita irreversibile dati: `settings.json` corrotto/illeggibile ? caricamento account predefinito di fallback ? riscrittura file ? al riavvio successivo `CleanupUnusedProfilesAsync` elimina definitivamente i profili su disco di tutti gli altri account configurati (sessioni perse, QR code da scansionare nuovamente);
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


## 2026-09-20

- [x] **TASK-0057** [Media] Verifica integrità aggiornamento: fail-closed e autenticità~~
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

- [x] **TASK-0056** [Alta] Download aggiornamento: timeout di 15 s e ZIP interamente in RAM~~
  - **File**: `UpdateChecker.vb` (`Shared Sub New`, `PerformUpdateFromGitHubAsync`)
  - **Problema**: `_httpClient.Timeout = 15s` copre l'intera operazione: `GetByteArrayAsync(downloadUrl)` fallisce con uno ZIP di grandi dimensioni o con connessioni lente. Inoltre l'intero archivio viene caricato in memoria prima della scrittura su disco.
  - **Fix Implementato**:
    - Introdotto client `_downloadClient` dedicato con `Timeout = InfiniteTimeSpan` e header User-Agent di prodotto;
    - Streaming HTTP diretto su file temporaneo su disco (`File.Create(tempZipPath)`) tramite `ResponseHeadersRead` e `CopyToAsync`, evitando di caricare l'intero archivio in memoria RAM;
    - CancellationTokenSource con timeout globale esteso a 10 minuti per consentire il download affidabile anche su connessioni lente;
    - Calcolo asincrono dell'hash SHA-256 in streaming direttamente dal file su disco tramite `SHA256.HashDataAsync(stream)`.
  - **Impatto**: Alto | **Sforzo**: Basso


## 2026-09-14

- [x] **TASK-0055** [Alta] Resilienza Crash WebView2 & Auto-Recovery Trasparente (`ProcessFailed`)~~
  - **Descrizione**: Gestione degli errori irreversibili del runtime Microsoft Edge WebView2 (`BrowserProcessExited`, `RenderProcessExited`, crash GPU o aggiornamenti silenziosi del runtime in background).
  - **Funzionalità Implementate**:
    - Intercettazione strongly-typed di `CoreProcessFailed` e `ProcessFailed` a livello di controllo WPF e CoreWebView2.
    - Auto-reload immediato per crash isolati del processo di rendering (`RenderProcessExited`).
    - Ricreazione completa e trasparente del controllo (`RecreateAccountWebViewAsync`) per crash dell'intero browser Chromium o invalidamento dell'HWND.
    - Prevenzione pop-up di errore e re-switch automatico trasparente al cambio scheda (`SwitchToAccountAsync` & `AccountTab_Click`).
    - Hardening del pulsante di ricarica (`BtnReloadActiveTab_Click`) e disiscrizione sicura dei listener in `Dispose()`.
  - **Impatto**: Alto | **Sforzo**: Basso-Medio
  - ---


## 2026-09-01

- [x] **TASK-0047** [Alta] Modalità "Non Disturbare" / Focus Mode Temporizzata~~
  - **Descrizione**: Silenziamento completo e istantaneo di tutte le notifiche (suoni WebView2, popup overlay e Windows Toast) per tutti gli account con timer configurabili e ripristino automatico alla scadenza.
  - **Funzionalità Implementate**: Pulsante dedicato `BtnDnd` nella barra del titolo con icona Bell-Off e badge attivo con indicatore orario di fine nel tooltip, menu contestuale per scelta rapida della durata (*30m*, *1h*, *2h*, *8h*, *Indefinito*), muting audio sincrono delle WebView2 (`IsMuted = True`), soppressione totale di Toast/Popup e configurazione nelle Impostazioni.
  - **Impatto**: Alto | **Sforzo**: Basso


## 2026-08-30

- [x] **TASK-0044** [Media] Correttore Ortografico Nativo Multilingua (Spellchecker WebView2)~~
  - **Descrizione**: Abilitazione e gestione del motore di correzione ortografica nativo di Microsoft Edge WebView2 / Chromium nei campi di digitazione di WhatsApp Web e Telegram Web.
  - **Funzionalità Implementate**: Sezione dedicata nelle **Impostazioni** con toggle di attivazione/disattivazione `ChkEnableSpellcheck`, selettore lingua dizionario `ComboSpellcheckLanguage` (Automatico da lingua app, Italiano `it-IT`, English `en-US`, Français `fr-FR`, Español `es-ES`, Deutsch `de-DE`), persistenza in `settings.json` e configurazione dinamica dei parametri Chromium (`--enable-features=Spellcheck`, `--lang=...`).
  - **Impatto**: Medio | **Sforzo**: Basso


## 2026-08-28

- [x] **TASK-0043** [Media] Supporto a Temi CSS Personalizzati Utente (Custom CSS Injector)~~
  - **Descrizione**: Sezione dedicata nelle **Impostazioni** per inserire e modificare regole CSS personalizzate per WhatsApp Web e Telegram Web (font, colori delle bolle dei messaggi, larghezza sidebar, sfondo chat, trasparenze, tema OLED).
  - **Funzionalità Implementate**: Editor monospace con scrollbar, checkbox di attivazione immediata, persistenza in `settings.json`, pulsanti preset rapidi (*OLED*, *Compatto*, *Font*, *Svuota*) e iniezione live sincrona su tutte le WebView2 tramite bridge IPC.
  - **Impatto**: Medio | **Sforzo**: Basso-Medio

- [x] **TASK-0042** [Media] Indicatore Stato "Online" dei Contatti (Online Indicator)~~
  - **Descrizione**: Intercettare tramite uno script JavaScript iniettato (`MutationObserver` su intestazione chat) quando il contatto o la chat attualmente aperta è "online" (es. rilevando la dicitura `online` / `in linea` o elementi di stato nel DOM della conversazione attiva sia su WhatsApp Web che su Telegram Web).
  - **Integrazione UI**: Visualizzazione di un badge elegante animato nella `TitleBar` (`OnlineIndicatorBorder`) e pallino verde sulla scheda account attiva per WhatsApp e Telegram Web.
  - **Impatto**: Medio | **Sforzo**: Basso


## 2026-08-27

- [x] **TASK-0041** [Alta] Integrazione Telegram — Funzionalità Avanzate e Affinamenti UI/UX~~
  - **Stato Base**: ? Completato nella v0.5.0 (Routing su `web.telegram.org/k/`, isolamento profili in `data/webview/`, icone vettoriali dedicate azzurre e selettore piattaforma nelle Impostazioni).
  - **Rifiniture Multi-Piattaforma**: ? Completato nella v0.5.0 (Sincronizzazione tema Scuro/Chiaro con classi `.night` e `themeController`, traduzione selettiva hover e batch con selettori DOM Telegram, routing click popup su account notificante).
  - **Evoluzione e Rifiniture Avanzate**: ? Completato nella v0.7.1-beta:
    - **Badge Notifiche e Contatore Non Letti Telegram**: Rilevamento continuo del conteggio messaggi non letti (MutationObserver combinato su `<title>` e scansione DOM selettori `.badge.unread`, `.unread-count`, ecc.) con pillola badge numerica colorata sulla scheda dell'account e icona tray dinamica.
    - **Gestione Deep Link Telegram (`tg://` e `t.me/`)**: Intercettazione in `NavigationStarting` e `NewWindowRequested` di protocolli `tg://resolve?domain=...`, `tg://join?invite=...` e link web `https://t.me/...` con traduzione automatica in route Telegram Web K interne (`https://web.telegram.org/k/#@...` / `https://web.telegram.org/k/#?tgaddr=...`).
    - **Scorciatoie da Tastiera Globali**: Aggiunte scorciatoie da tastiera per cambio rapido account (`Ctrl+1`, `Ctrl+2`, `Ctrl+3` e NumPad), navigazione circolare schede (`Ctrl+Tab`, `Ctrl+Shift+Tab`), nuovo account (`Ctrl+T`/`Ctrl+N`), ricarica (`Ctrl+R`/`F5`), impostazioni (`Ctrl+,`) e invio massivo (`Ctrl+B`).
  - **Impatto**: Alto | **Sforzo**: Medio
  - ---


## 2026-08-24

- [x] **TASK-0052** [Alta] Invio Massivo Personalizzato da File Excel / CSV (.xlsx, .xls, .csv)~~
  - **Descrizione**: Modulo integrato per importare elenchi contatti da fogli di calcolo Excel (`.xlsx`, `.xls`) o file CSV (`.csv`) e inviare messaggi personalizzati uno alla volta tramite l'account WhatsApp Web attivo su WebView2.
  - **Funzionalità Implementate**:
    - Rilevamento e mappatura automatica delle intestazioni di colonna (*Telefono*, *Nome*, *Cognome*, *Azienda*, *Testo personalizzato*).
    - Normalizzazione, pulizia caratteri non numerici e formattazione con prefisso internazionale.
    - Editor per template dinamici con segnaposto (`{Nome}`, `{Cognome}`, `{Azienda}`, `{Telefono}`, `{Testo}`) e pulsanti di inserimento rapido.
    - Finestra dedicata `BulkSenderWindow` con DataGrid interattivo, selezione puntuale o multipla e badge colorati di avanzamento per riga (`In attesa`, `Inviando...`, `Inviato ?`, `Errore ?`, `Non valido`).
    - Motore di automazione sequenziale `BulkSenderEngine` con rilevamento modali errore numeri non registrati (`INVALID_NUMBER`), injection script per l'invio chat e intervallo di attesa Anti-Spam regolabile (Jitter Delay) con countdown in tempo reale.
    - Pulsante dedicato con icona Excel integrato nella barra del titolo (`MainWindow.xaml`) e supporto multilingua completo in `Localization.vb`.
  - **Impatto**: Alto | **Sforzo**: Medio

- [x] **TASK-0040** [Bassa] Nessuna verifica di integrità sullo ZIP di aggiornamento automatico~~
  - **File**: `UpdateChecker.vb`, `PerformUpdateFromGitHubAsync`, `FetchGitHubReleaseInfoAsync`
  - Implementata la verifica crittografica di integrità SHA-256 prima dell'estrazione dell'archivio ZIP di aggiornamento. Il sistema intercetta automaticamente asset di checksum dedicati (`.sha256`, `.sha256sum`, `SHA256SUMS`, `SHA256SUMS.txt`, `checksums.txt`) o impronte SHA-256 dichiarate nelle note di rilascio GitHub, calcola l'hash del file scaricato (`SHA256.HashData`) e blocca tempestivamente l'installazione in caso di discrepanza, garantendo la massima resilienza e protezione contro manomissioni o download parziali.
  - **Impatto**: Basso | **Sforzo**: Basso
  - ---


## 2026-08-21

- [x] **TASK-0039** [Bassa] Il bridge token è esposto come variabile globale leggibile dalla pagina~~
  - **File**: `AppAccounts.vb`, `JsScripts.vb`, `Scripts/notification.js`, `Scripts/translation.js`
  - Il `bridgeToken` non viene più esposto nell'oggetto globale `window.__bridgeToken`. Viene ora interpolato tramite serializzazione sicura `JsonSerializer.Serialize` ed incapsulato privatamente all'interno dello scope e closure (IIFE) di `notification.js` e `translation.js`. Nessuno script o estensione in esecuzione nel DOM/pagina web può leggere, ispezionare o manomettere il token di sicurezza IPC.
  - **Impatto**: Basso | **Sforzo**: Basso


## 2026-08-20

- [x] **TASK-0038** [Bassa] Escaping incoerente dei valori interpolati nel JavaScript eseguito via `ExecuteScriptAsync`~~
  - **File**: `AppAccounts.vb` (`HandleTranslationMessageAsync`, `UpdateWebviewLanguageAsync`) e `MainWindow.xaml.vb` (`onNotificationClicked`)
  - Tutti i valori e identificatori interpolati (`id`, `langCode`, `langName`, `tooltipLabel`, `notificationId`) vengono ora serializzati in modo coerente e sicuro tramite `JsonSerializer.Serialize`, prevenendo injection di caratteri speciali o apici e garantendo conformità rigorosa al formato JSON.
  - **Impatto**: Basso | **Sforzo**: Basso

- [x] **TASK-0037** [Bassa] Cancellazione dei profili basata su `Task.Delay` fisso invece di un segnale deterministico~~
  - **File**: `AccountManager.vb`, `RemoveAccountAsync`, `MigrateOrphanProfileAsync`, `CleanupUnusedProfilesAsync`
  - Implementato metodo helper asincrono `DeleteDirectoryWithRetryAsync` con backoff progressivo (fino a 5 tentativi con attesa crescente) per garantire la corretta eliminazione dei profili WebView2 anche su macchine lente o in presenza di lock momentanei da parte dell'antivirus/Chromium.
  - **Impatto**: Basso | **Sforzo**: Basso


## 2026-08-18

- [x] **TASK-0036** [Media] File `settings.json` e `translations_cache.json` nella cartella radice invece che in `data/`~~
  - **File**: `SettingsController.vb`, proprietà `SettingsFile` e `Localization.vb`, proprietà `CacheFilePath`
  - I file `settings.json` e `translations_cache.json` vengono creati e salvati direttamente all'interno della cartella portabile `data/`. Implementata la migrazione trasparente e automatica (`File.Move`) dei file esistenti nella cartella radice verso `data/` al primo avvio, preservando le impostazioni e la cache.
  - **Impatto**: Medio | **Sforzo**: Basso

- [x] **TASK-0035** [Bassa] `JsScripts.vb` — ~300 righe di JS traduzioni in memoria permanente~~
  - Script JS caricate all'avvio e mai rilasciate; copiate a ogni setup account
  - **Ottimizzazione**: caricate da risorse incorporate `EmbeddedResource` in modo lazy (`Lazy(Of String)`); persistenza delle traduzioni su file (`data/translations_cache.json`) per evitare download ripetuti da internet; ottimizzazione RAM per mantenere in memoria solo i dizionari della lingua impostata.
  - **Impatto**: Basso | **Sforzo**: Medio
  - ---

- [x] **TASK-0034** [Bassa] `SolidColorBrush` — Creato a ogni cambio tema in MainWindow e SettingsWindow~~
  - `New SolidColorBrush(...)` chiamato ogni volta che si applica il tema
  - **Ottimizzazione**: cache delle istanze per colore
  - **Impatto**: Basso | **Sforzo**: Basso

- [x] **TASK-0009** [Media] Traduzioni batch — Chunking fisso a 50 testi~~
  - Dimensione chunk hardcoded a 50, non considera la lunghezza dei testi
  - **Ottimizzazione**: chunking adattivo basato sulla lunghezza totale dei caratteri (es. max 2000 caratteri per chunk)
  - **Impatto**: Medio | **Sforzo**: Basso

- [x] **TASK-0008** [Alta] `AccountManager.LoadAccountsAsync()` — Crea tutti i WebView2 all'avvio~~
  - Crea un WebView2 per OGNI account al caricamento (~50-100MB per istanza)
  - **Ottimizzazione**: lazy initialization — creare WebView2 solo per l'account attivo, gli altri on-demand
  - **Impatto**: Alto | **Sforzo**: Alto


## 2026-08-15

- [x] **TASK-0033** [Bassa] Registro `AppsUseLightTheme` — Letto 3 volte senza caching~~
  - Stessa chiave registry letta in `MainWindow.xaml.vb`, `WhatsAppAccount.vb`, `SettingsWindow.xaml.vb`
  - **Ottimizzazione**: modulo `SystemThemeHelper` centralizzato con cache del registro e ascolto dell'evento `SystemEvents.UserPreferenceChanged`
  - **Impatto**: Basso | **Sforzo**: Basso

- [x] **TASK-0032** [Bassa] `MessagePopup._activePopups` — Lista statica senza WeakReference~~
  - Trattiene riferimenti forti a tutti i popup — leak se non chiusi normalmente
  - **Ottimizzazione**: `WeakReference(Of MessagePopup)` con rimozione automatica dei riferimenti raccolti dal GC
  - **Impatto**: Basso | **Sforzo**: Basso

- [x] **TASK-0031** [Bassa] `UpdateChecker.vb` — `batchContent` con string concatenation~~
  - Batch file build con concatenazione di ~35 righe
  - **Ottimizzazione**: `StringBuilder` per assemblare lo script batch in streaming
  - **Impatto**: Basso | **Sforzo**: Basso

- [x] **TASK-0030** [Bassa] `Directory.GetDirectories` in `AccountManager.vb`~~
  - Array eager invece di enumerazione lazy
  - **Ottimizzazione**: `Directory.EnumerateDirectories` e `FirstOrDefault()` per interrompere la scansione alla prima corrispondenza
  - **Impatto**: Basso | **Sforzo**: Basso

- [x] **TASK-0029** [Bassa] `AccountManager.vb` — Anonymous type a ogni salvataggio~~
  - Crea istanze di tipo anonimo per ogni account a ogni salvataggio — GC pressure
  - **Ottimizzazione**: serializzare `WhatsAppAccount` direttamente (ha già `JsonPropertyName`)
  - **Impatto**: Basso | **Sforzo**: Basso

- [x] **TASK-0028** [Bassa] `WhatsAppAccount.vb` — `New Random()` ogni chiamata~~
  - System clock seed può produrre sequenze identiche se chiamate ravvicinate
  - **Ottimizzazione**: `Random.Shared` (.NET 9) o `Shared` con lock
  - **Impatto**: Basso | **Sforzo**: Basso

- [x] **TASK-0027** [Media] `AccountManager` e `SettingsController` — Nessun dirty tracking~~
  - `SaveAccountsAsync()` e `WriteSettingsAsync()` riscrivono tutto anche se nulla è cambiato
  - **Ottimizzazione**: flag `_dirty` + scrittura solo su modifiche effettive
  - **Impatto**: Medio | **Sforzo**: Basso

- [x] **TASK-0026** [Media] `JsScripts.vb` — `.Replace()` multipli su template grandi~~
  - `GetTranslationJS` chiama `.Replace()` 10+ volte sul template enorme — crea una nuova stringa ogni volta
  - **Ottimizzazione**: `StringBuilder` con `Replace` in-place
  - **Impatto**: Medio | **Sforzo**: Basso

- [x] **TASK-0025** [Media] `ExecuteScriptAsync` fire-and-forget in `MainWindow.xaml.vb`~~
  - Task restituiti da `ExecuteScriptAsync` non vengono awaitati né catturati — eccezioni inosservabili
  - **Ottimizzazione**: catturare i task o usare `Async Sub`/`Async Function` con `Await` e gestione errori (`Try...Catch`)
  - **Impatto**: Medio | **Sforzo**: Basso

- [x] **TASK-0023** [Media] `MainWindow.xaml.vb` — Event handler `PropertyChanged` mai rimossi~~
  - `_settingsController.PropertyChanged` e `_accountManager.PropertyChanged` registrati ma mai rimossi in `OnClosed`/`OnUnloaded`
  - **Ottimizzazione**: aggiungere `RemoveHandler` negli eventi di chiusura finestra
  - **Impatto**: Medio | **Sforzo**: Basso

- [x] **TASK-0022** [Media] `CancellationTokenSource` in `SettingsController.FlushAfterDebounceAsync` — Non disposto~~
  - Il CTS precedente viene solo cancellato (`Cancel()`) ma mai chiamato `.Dispose()` — tiene `WaitHandle` (OS resource)
  - **Ottimizzazione**: disporre il vecchio `_flushCts` prima di sostituirlo col nuovo
  - **Impatto**: Medio | **Sforzo**: Basso

- [x] **TASK-0021** [Media] `Directory.GetFiles` in `UpdateChecker.vb` — Enumerazione eager su network share~~
  - Carica TUTTI i percorsi file in array `String()` — potenzialmente migliaia di stringhe su percorso UNC
  - **Ottimizzazione**: `Directory.EnumerateFiles` (streaming lazy)
  - **Impatto**: Medio | **Sforzo**: Basso


## 2026-08-05

- [x] **TASK-0020** [Media] `Localization` — `HttpClient` creato 3 volte (non condiviso)~~
  - `New HttpClient()` in ogni metodo — socket exhaustion sotto carico
  - **Ottimizzazione**: `Shared ReadOnly HttpClient` a livello di classe/modulo
  - **Impatto**: Medio | **Sforzo**: Basso

- [x] **TASK-0019** [Alta] `ActiveNotificationIds` — HashSet cresce senza limiti~~
  - ID notifiche aggiunti su `NOTIFICATION_RECEIVED` ma mai rimossi se `NOTIFICATION_CLOSED` non arriva
  - **Ottimizzazione**: limite massimo dimensioni + cleanup periodico per ID vecchi
  - **Impatto**: Alto | **Sforzo**: Basso

- [x] **TASK-0015** [Alta] Event handler su `CoreWebView2` mai rimossi~~
  - `PermissionRequested`, `NewWindowRequested`, `WebMessageReceived`, `NavigationCompleted` registrati ma mai rimossi con `RemoveHandler`
  - WebView2 trattiene riferimenti forti all'account — memory leak certo alla rimozione
  - **Ottimizzazione**: salvati riferimenti handler e chiamato `RemoveHandler` prima di rilasciare il WebView2
  - **Impatto**: Alto | **Sforzo**: Basso

- [x] **TASK-0013** [Alta] `WhatsAppAccount` — Implementare `IDisposable`~~
  - WebView2 (COM/GPU) e `ActiveNotificationIds` mai rilasciati quando un account viene rimosso
  - **Ottimizzazione**: implementato `IDisposable` e chiamato da `AccountManager` alla rimozione dell'account
  - **Impatto**: Alto | **Sforzo**: Medio

- [x] **TASK-0012** [Bassa] Translation cache — Scrittura completa su ogni cambio lingua~~
  - `SaveCacheFileAsync()` riscrive l'intero file cache anche per modifiche minime
  - **Ottimizzazione**: scrittura differita o incrementale
  - **Impatto**: Basso | **Sforzo**: Basso
  - *Obsoleto: traduzioni UI ormai hardcoded (EN/IT). Cache scritta solo all'inizializzazione, non a ogni cambio lingua.*
  - ---

- [x] **TASK-0011** [Bassa] `SwitchToAccountAsync()` — Visibilità di tutti i WebView~~
  - Itera TUTTI i WebView per impostare `Visibility = Collapsed` e solo uno a `Visible`
  - **Ottimizzazione**: tenere traccia dell'ultimo account attivo e nascondere solo quello con controllo preventivo di account già attivo
  - **Impatto**: Basso | **Sforzo**: Basso

- [x] **TASK-0010** [Bassa] `MessagePopup.RepositionAll()` — Itera tutti i popup attivi~~
  - Ogni volta che un popup viene mostrato/chiuso, ricalcola la posizione di TUTTI i popup attivi
  - **Ottimizzazione**: tenere traccia incrementale della posizione Y del prossimo popup impilandolo verso l'alto senza riposizionare i popup già attivi
  - **Impatto**: Basso | **Sforzo**: Basso

- [x] **TASK-0007** [Media] Aggiornamento OTA — `ReadVersionFromFileAsync()` su UNC path~~
  - Obsoleto nella v0.2.0: la lettura da percorso di rete UNC e `ReadVersionFromFileAsync()` sono stati completamente rimossi. Il sistema OTA opera esclusivamente tramite API REST di GitHub Releases.

- [x] **TASK-0006** [Media] WebView2 — Script injection su ogni navigazione~~
  - A ogni `NavigationCompleted`, reinietta tema CSS e script di traduzione inutilmente su navigazioni secondarie
  - **Ottimizzazione**: iniettare solo su navigazione iniziale, usare `AddScriptToExecuteOnDocumentCreatedAsync` per script permanenti
  - **Impatto**: Medio | **Sforzo**: Basso

- [x] **TASK-0005** [Bassa] `FindVisualChildren()` e `FindVisualChild()` — Ricorsione multipla~~
  - Funzioni ricorsive chiamate 3-4 volte per finestra che visitano TUTTO il visual tree
  - **Ottimizzazione**: memoizzazione (cache dell'albero) o sostituzione con binding/stili dichiarativi
  - **Impatto**: Basso | **Sforzo**: Basso

- [x] **TASK-0004** [Media] `AccountsList.ItemsSource = Nothing` + riassegnazione~~
  - Dopo eliminazione account, azzera e riassegna `ItemsSource` forzando rigenerazione di TUTTI i DataTemplate
  - **Ottimizzazione**: usare `ObservableCollection` invece di `List(Of WhatsAppAccount)`
  - **Impatto**: Medio | **Sforzo**: Basso

- [x] **TASK-0003** [Bassa] `StyleAccountItems()` — Ricorsione visual tree ogni volta~~
  - Chiama `FindVisualChildren(Of Border)` e `FindVisualChildren(Of TextBox)` sull'intero `AccountsList`
  - **Ottimizzazione**: usare stili WPF con DataTrigger invece di code-behind, oppure usare `Loaded` event handler sugli elementi del DataTemplate
  - **Impatto**: Basso | **Sforzo**: Basso

- [x] **TASK-0002** [Alta] `FetchTranslations()` — N richieste HTTP seriali~~
  - Per ogni chiave di lingua (30+), fa una richiesta HTTP individuale a Google Translate, una dopo l'altra
  - **Ottimizzazione**: raggruppare le traduzioni in un'unica richiesta batch, o usare `HttpClient` con richieste parallele (`Task.WhenAll`)
  - **Impatto**: Alto | **Sforzo**: Medio

- [x] **TASK-0001** [Alta] I/O su `settings.json` — Letture multiple ridondanti~~
  - `SaveSettingAsync()` fa `ReadSettingsAsync()` + `WriteSettingsAsync()` ogni volta che cambia una singola chiave
  - **Ottimizzazione**: accumulare le modifiche in memoria e scrivere su disco con un debounce (es. 500ms) oppure riscrivere solo il file senza rileggerlo
  - **Impatto**: Alto | **Sforzo**: Basso


## 2026-07-27

- [x] **TASK-0024** [Bassa] `DispatcherTimer` per sincronizzazione chat — Rimosso~~
  - Timer rimosso nella v0.1.0 insieme al sistema di sincronizzazione backup.

- [x] **TASK-0018** [Media] `EncryptBytes` / `DecryptBytes` — Rimozione sistema backup~~
  - Metodi eliminati nella v0.1.0.

- [x] **TASK-0017** [Media] `SaveMessageBatchAsync` — Rimozione sistema backup~~
  - Metodo eliminato nella v0.1.0.

- [x] **TASK-0016** [Media] `CryptoHelper` e cifratura backup — Rimozione sistema backup~~
  - Classe `CryptoHelper` eliminata nella v0.1.0 con la rimozione del backup.

- [x] **TASK-0014** [Media] `ChatJsonStorageService` e `ChatSyncBackgroundWorker` — Rimozione sistema backup~~
  - Classi eliminate nella v0.1.0 assieme all'intero sistema di sincronizzazione e salvataggio chat su disco.
