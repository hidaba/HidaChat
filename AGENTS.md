# Linee Guida Progetto WhatsAppH

## Pubblicazione e Aggiornamenti OTA
- **Host OTA**: Esclusivamente su **GitHub Releases** (`hidaba/WhatsAppH`). Il percorso di rete locale è stato rimosso ed abbandonato.
- **Pacchetto unico**: Un solo file `.zip` (`WhatsappH-vX.Y.Z.zip`) per nuove installazioni e per gli aggiornamenti OTA.
- **Pubblicazione Stabile**: `.\publish.ps1 [-Bump major|minor|patch]` -> Pubblica la release su GitHub come stabile.
- **Pubblicazione Beta**: `.\publish.ps1 [-Bump major|minor|patch] -Beta` -> Pubblica la release su GitHub contrassegnata come `--prerelease` (Beta).
