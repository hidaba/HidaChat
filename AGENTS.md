# Linee Guida Progetto WhatsAppH

## Portabilità Assoluta (100% Portable)
- **Portabilità 100%**: L'applicazione deve rimanere al 100% portabile. **TUTTI** i dati, profili WebView2, cache e file di configurazione devono risiedere sempre esclusivamente all'interno della cartella dell'applicazione (`AppDomain.CurrentDomain.BaseDirectory/data/webview`).
- **Nessuna Scrittura Esterna**: Non salvare o reindirizzare mai dati in `%LOCALAPPDATA%`, `%APPDATA%`, `%TEMP%` o altre cartelle esterne al percorso di installazione dell'applicazione.

## Pubblicazione e Aggiornamenti OTA
- **Host OTA**: Esclusivamente su **GitHub Releases** (`hidaba/WhatsAppH`). Il percorso di rete locale è stato rimosso ed abbandonato.
- **Pacchetto unico**: Un solo file `.zip` (`WhatsappH-vX.Y.Z.zip`) per nuove installazioni e per gli aggiornamenti OTA.
- **Pubblicazione Stabile**: `.\publish.ps1 [-Bump major|minor|patch]` -> Pubblica la release su GitHub come stabile.
- **Pubblicazione Beta**: `.\publish.ps1 [-Bump major|minor|patch] -Beta` -> Pubblica la release su GitHub contrassegnata come `--prerelease` (Beta).
