# HidaChat - patch sessione WhatsApp `sessionfix1`

**Data:** 7 ottobre 2026. **Stato:** patch candidata al collaudo, solo sorgenti.

Base: ZIP HidaChat-master fornito dall'utente; versione dichiarata `1.1.5-beta`.
Commento Git del ZIP originale: `0948eab40d22a39144a0cf9ea893e48cb668e38a`.
Versione identificativa della patch: `1.1.5-beta-sessionfix1`.

## Esito dell'analisi

Il problema segnalato si presenta anche eseguendo HidaChat da disco locale. Non e' quindi corretto attribuirlo esclusivamente alla condivisione di rete. L'analisi ha individuato meccanismi rischiosi effettivamente presenti nel codice, ma senza i log dell'evento non dimostra quale abbia provocato la specifica richiesta del QR.

La patch corregge questi meccanismi e aggiunge dati diagnostici. Non recupera una sessione gia' revocata, non ripara automaticamente database precedentemente danneggiati e non impedisce a WhatsApp di scollegare un dispositivo.

### Difetti riscontrati nella base originale

| Area | Comportamento originale | Correzione |
|---|---|---|
| `AppAccounts.HandleProcessFailed` | Dopo il caso renderer, ricreava tutto il browser anche per eventi GPU, utility e renderer non responsivo. | Distingue i guasti: osservazione degli eventi non fatali, reload limitato del renderer, ricreazione per browser terminato. Evita richieste concorrenti e attende la chiusura precedente. |
| `AppAccounts.Dispose`, chiusura di `MainWindow` | La chiusura del controllo era trattata come chiusura del browser; alcune operazioni proseguivano subito o dopo una pausa fissa di 600 ms. | Traccia `BrowserProcessExited` del PID associato. Nessuna esportazione del profilo senza conferma; nessuna cancellazione automatica delle cache in chiusura. |
| `UpdateChecker` | Avviava il batch prima della chiusura ordinata e, dopo circa 10 secondi, poteva eseguire `taskkill /f /im HidaChat.exe /t`. | Il batch parte dopo la chiusura dei browser. Non termina forzatamente processi, aspetta il PID dell'host ed esclude `data` dalla copia dell'aggiornamento. |
| Recupero profilo anonimo | `WV2Profile_` poteva sostituire il profilo di un account gia' esistente. | Nessuna sostituzione automatica. La migrazione residua richiede un unico account e destinazione assente, senza `.previous` da recuperare. |
| Sincronizzazione di rete | Copiava file di generazioni diverse, usava dimensioni e date come indizi di validita', eseguiva copie periodiche parziali del profilo aperto. | Import solo in staging vuoto, nessun merge, nessun ripristino basato su dimensioni/date, esportazione completa solo dopo chiusura. |
| Esclusione multi-PC | Il file con timestamp non era un lock detenuto dal sistema operativo; il conflitto veniva rilevato dopo l'importazione. | Handle esclusivo acquisito prima dell'importazione e mantenuto fino al termine dell'esportazione. |
| Persistenza storage | La richiesta JavaScript non era accompagnata dalla gestione esplicita del permesso host. | Permesso `PersistentStorage` solo per le origini HTTPS esatte di WhatsApp/Telegram; viene registrato il risultato effettivo. |

I primi tre interventi riguardano anche l'esecuzione locale. Il fatto che un riavvio del browser sia superfluo non dimostra, da solo, che provochi un logout: questa rimane un'ipotesi da verificare sul sistema reale.

## Cosa cambia, e cosa rimane invariato

I percorsi esistenti vengono mantenuti: nessuna migrazione generale verso un nuovo percorso che obbligherebbe a rifare l'accesso.

```text
Esecuzione locale:
<cartella-HidaChat>\data\webview\WV2Profile_<id-account>\

Esecuzione da rete con staging abilitato:
%LOCALAPPDATA%\HidaChat\NetworkProfiles\WV2Profile_<id-account>\
```

Restano invariati gli ID degli account gia' salvati; solo i nuovi ID sono generati con GUID. Il percorso effettivo e il runtime WebView2 vengono registrati in `data\logs\app.log`. La patch non forza lo staging quando l'opzione esistente e' disabilitata: il profilo aperto direttamente su rete resta sconsigliato.

Per la rete viene eliminata la copia periodica mentre il browser e' aperto. La copia verso la share avviene all'uscita ordinaria e prima dell'aggiornamento, non alla disconnessione/arresto di Windows: in quest'ultimo caso il profilo locale resta la copia aggiornata. Uno staging locale esistente viene conservato integralmente, anche se piccolo. Questo privilegia la continuita' sul medesimo PC, non il roaming automatico tra PC.

`ColdProfileCopy` prepara una directory `.incoming-*` completa, poi esegue due rinomine con rollback e mantiene `.previous`. Non e' una transazione atomica del filesystem: un'interruzione tra le rinomine richiede il recupero della directory precedente. La copia precedente non e' un login sicuramente valido e contiene dati sensibili; serve anche spazio per le copie temporanee e precedenti. Una perdita di connessione durante la copia viene registrata e non pubblica un insieme incompleto di database.

Il lock e' rispettato dalle copie aggiornate dell'applicazione; non modifica il comportamento di eseguibili vecchi. Non avviare contemporaneamente vecchia e nuova versione sul medesimo profilo. La portabilita' delle credenziali tra utenti Windows/PC non e' garantita da questa patch.

Il ripristino automatico in caso di blocco temporaneo non ricrea piu' il browser: un blocco persistente puo' richiedere un riavvio manuale. Un timeout di chiusura impedisce le copie o l'aggiornamento, invece di forzare la terminazione.

## Nuova diagnostica

`Scripts/session-health.js` registra ogni 60 secondi persistenza effettiva dello storage, uso/quota e connettivita'. Segnala anche il passaggio tra lista chat, QR visibile e stato sconosciuto. Questi sono indizi del DOM, non una prova di autenticazione: i selettori possono cambiare.

Il nuovo script non legge QR, numeri di telefono, chiavi o messaggi e non sostituisce WebSocket/IndexedDB. Le intercettazioni diagnostiche gia' presenti in `notification.js` sono rimaste: non e' una riscrittura completa degli script preesistenti.

Log utili, rispetto alla cartella dell'eseguibile:

```text
data\logs\app.log
data\logs\whatsapp.log
data\logs\sync.log
```

Cercare `PROFILE_OPEN`, `PROFILE_READY`, `PROCESS_FAILED`, `BROWSER_EXIT`, `BROWSER_EXIT_TIMEOUT`, `STORAGE_PERSIST`, `STORAGE_HEALTH`, `SESSION_UI_STATE` e gli eventuali errori originali di database/WebSocket.

Conservare orario preciso, log di alcuni minuti prima/dopo e informazione se l'app era rimasta aperta oppure era stata appena riavviata/aggiornata. I log preesistenti possono includere nomi, URL o testo di errori: oscurare i dati privati prima di condividerli. Non inviare profili, cookie o QR.

## Compilazione su Windows

Questa consegna non include un eseguibile. Non sostituire il programma installato copiandoci sopra lo ZIP dei sorgenti.

Prerequisiti del percorso di build incluso: Windows, SDK .NET 9, Node.js 20 o superiore e accesso alle dipendenze NuGet. WebView2 conserva la versione NuGet gia' dichiarata dal progetto (`1.0.4078.44`). Lo script non installa dipendenze, non modifica i dati dell'utente e si interrompe se test/build falliscono.

Estrarre lo ZIP in una cartella di lavoro separata e, da PowerShell, entrare nella cartella contenente `HidaChat.sln`:

```powershell
.\BUILD_SESSIONFIX.ps1
```

Il risultato viene pubblicato in:

```text
artifacts\sessionfix-win-x64\
```

Lo script esegue test JavaScript, test .NET, build WPF e pubblicazione self-contained x64. La presenza del runtime WebView2 sul PC resta necessaria. Per le funzioni Tailscale mantenere il componente `tsnetd.exe` gia' utilizzato, se non incluso nell'output del progetto.

In alternativa, la patch unificata puo' essere applicata con `git apply` alla base indicata sopra. Su revisioni differenti verificare prima `git apply --check`; non forzare l'applicazione ignorando i conflitti. Il workflow Windows aggiornato include build e test, ma non e' stato avviato da questa consegna.

## Installazione e collaudo

1. Compilare e superare i test prima di toccare l'installazione. Chiudere HidaChat tramite **Esci** nell'icona vicino all'orologio: la X della finestra normalmente lo nasconde soltanto. Attendere la chiusura dei relativi processi WebView2, senza terminare browser di altre applicazioni.
2. A programma chiuso, fare un backup dell'intera installazione, inclusa `data`; se usato, conservare anche lo staging locale indicato sopra. Aggiornare i file pubblicati del programma e le relative dipendenze, mantenendo la cartella e i file `data` esistenti. Non usare una copia con cancellazione speculare dei file di destinazione.
3. Avviare un'unica istanza, stesso account e percorso. Controllare la versione e il log `PROFILE_READY`. Se la sessione e' gia' stata revocata o danneggiata, puo' essere necessario scansionare il QR una volta; non cancellare il profilo come procedura standard.
4. Provare uscita/riavvio ordinati, permanenza aperta per una notte, sospensione/ripresa, temporanea perdita di rete e cambio account. Annotare se la richiesta del QR compare durante l'uso o dopo un riavvio. Evitare aggiornamenti a release che non contengano la patch durante il collaudo.

Per tornare indietro, chiudere la nuova versione e ripristinare i soli binari precedenti. Non ripristinare automaticamente una vecchia copia dei dati di autenticazione: tornare a vecchi cookie/chiavi non equivale a riattivare una sessione valida. Le precedenti versioni conservano i difetti descritti, quindi il rollback non e' una cura del problema.

## Verifiche e limiti della consegna

Consultare `VERIFICA_TEST.txt` per i risultati realmente eseguiti. Nel contenitore sono disponibili Node.js e controlli dei sorgenti, non SDK .NET, MSBuild o runtime Windows. Quindi **compilazione VB/WPF, test .NET, comportamento WebView2 e sessione WhatsApp reale non sono stati verificati**. I test .NET inclusi sono da eseguire su Windows, non risultati gia' acquisiti.

La patch e' un candidato verificabile, non una release con risoluzione definitiva dimostrata. Gli altri account/piattaforme, aggiornamento automatico, multi-PC e chiusura durante inizializzazione richiedono anche un collaudo di regressione su Windows.

## Fonti tecniche

- Microsoft, eventi dei processi WebView2 e sincronizzazione tramite BrowserProcessExited: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/process-related-events
- Microsoft, cartelle dati WebView2 e gestione a browser chiuso: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder
- Microsoft, threading e rientranza dei callback WebView2: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/threading-model
- Microsoft, permessi WebView2, PersistentStorage: https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2permissionkind

Queste fonti motivano le scelte sul ciclo di vita del browser; non provano la causa del logout specifico dell'utente. Le affermazioni storiche di "causa radice" e "fix definitivo" rimangono nel changelog originale per tracciabilita', ma non vengono assunte come dimostrazioni dalla presente analisi.
