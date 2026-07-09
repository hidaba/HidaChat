# WhatsAppVB

Client desktop WPF per WhatsApp Web con supporto multi-account, traduzione integrata e notifiche native Windows.

## Caratteristiche

- **Multi-account**: gestione simultanea di più account WhatsApp con tab separati
- **Tema scuro/chiaro**: personalizzabile con rilevamento automatico del tema di sistema Windows
- **Traduzione integrata**: hover button per tradurre singoli messaggi, traduzione completa della pagina
- **Notifiche native Windows**: Toast notifications con click routing all'account corretto
- **System tray**: chiusura a vassoio con icona nella barra delle notifiche
- **Aggiornamento automatico**: aggiornamento da repository di rete locale
- **Profili isolati**: ogni account ha una directory WebView2 separata

## Requisiti di sistema

- Windows 10 20H1 (build 19041) o successivo
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (installato automaticamente su Windows 11)
- .NET 9.0 Runtime

## Installazione

Scarica l'ultima release o compila il progetto con Visual Studio 2022.

### Da codice sorgente

```bash
git clone <repository-url>
cd WhatsAppVB
dotnet restore
dotnet build -c Release
```

Apri `WhatsAppVB.sln` con Visual Studio 2022 e avvia la build.

## Aggiornamenti

L'applicazione controlla automaticamente la presenza di aggiornamenti all'avvio leggendo il file `version.txt` dal repository di rete configurato. Se viene rilevata una nuova versione, i file vengono copiati localmente e l'applicazione si riavvia automaticamente.

Percorso di default: `\\172.17.10.135\annoni-new\IT\OTARepository\Whatsapp\`

## Utilizzo

1. Avvia l'applicazione
2. Clicca su **+ Add Account** per aggiungere un account WhatsApp
3. Inquadra il codice QR con WhatsApp sul telefono
4. Passa da un account all'altro cliccando sulle tab nella barra superiore

### Scorciatoie

| Pulsante | Funzione |
|---|---|
| ⚙️ | Impostazioni (tema, lingua, gestione account) |
| 🔄 | Ricarica la scheda attiva |
| 🌐 | Traduci tutti i messaggi nella pagina |

## Impostazioni

- **Tema**: System (automatico), Chiaro, Scuro
- **Lingua e traduzione**: lingua interfaccia, traduzione hover, traduzione notifiche
- **Notifiche**: abilita/disabilita notifiche native
- **Gestione account**: rinomina o elimina account

## Tecnologie

- **Linguaggio**: VB.NET
- **Framework**: .NET 9.0
- **UI**: WPF (Windows Presentation Foundation)
- **Browser**: Microsoft.Web.WebView2
- **Notifiche**: Microsoft.Toolkit.Uwp.Notifications

## Progetto originale

Basato su [whatsappPortable](https://github.com/Faeq-F/whatsappPortable) di Faeq-F.

## Licenza

Distribuito sotto licenza MIT. Vedi il file `LICENSE` per maggiori informazioni.
