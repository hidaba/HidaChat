# Workflow di Pubblicazione Release e OTA

- **Canale di Distribuzione ed OTA**: Gli aggiornamenti e le release sono gestiti **esclusivamente tramite GitHub Releases** (`https://github.com/hidaba/WhatsAppH`). La cartella di rete locale è dismessa e non viene più utilizzata.
- **Pacchetto Unico ZIP**: Lo script `publish.ps1` genera un singolo pacchetto ZIP (`WhatsappH-vX.Y.Z.zip`) che serve sia per le nuove installazioni portatili che per gli aggiornamenti automatici OTA.
- **Release Stabili**:
  - Pubblicate eseguendo lo script di pubblicazione (es. `.\publish.ps1 -Bump patch`).
  - La release GitHub viene creata **senza** flag `--prerelease`.
  - Vengono scaricate automaticamente dai client con l'opzione "Usa canale di aggiornamento beta" disattivata tramite l'endpoint `/releases/latest`.
- **Release Beta**:
  - Pubblicate eseguendo lo script con il parametro `-Beta` (es. `.\publish.ps1 -Bump patch -Beta` o `.\publish.ps1 -Beta`).
  - La release GitHub viene creata **con** il flag `--prerelease`.
  - Vengono notificate e scaricate **solo** dai client che hanno spuntato "Usa canale di aggiornamento beta" nelle Impostazioni dell'app tramite l'endpoint `/releases`.
