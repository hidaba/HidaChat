# WhatsappH

Client desktop WPF in VB.NET (.NET 9) per WhatsApp Web con supporto multi-account, traduzione istantanea dei messaggi e notifiche native Windows.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.txt)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)

---

## 🌟 Caratteristiche principali

- 👥 **Multi-Account**: Gestione contemporanea di più account WhatsApp in schede separate isolati tramite profili WebView2 dedicati.
- 🎨 **Design Moderno e Temi**: Interfaccia scura/chiara con rilevamento automatico del tema di sistema Windows.
- 🌐 **Traduzione Integrata**:
  - Pulsante hover per tradurre singoli messaggi direttamente nella chat.
  - Traduzione automatica o su richiesta dell'intera pagina.
  - Traduzione in tempo reale in italiano o inglese.
- 🔔 **Notifiche Native Windows**: Toast notifications native con instradamento del click direttamente all'account ed al messaggio corretto.
- 📌 **System Tray Integration**: Riduzione a vassoio di sistema con supporto alle notifiche badge ed estrazione rápida.
- 🚀 **Aggiornamenti OTA**: Verifica ed installazione automatica degli aggiornamenti in background.

---

## 💻 Requisiti di Sistema

- **OS**: Windows 10 (build 19041 o successiva) / Windows 11
- **Runtime**: [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (incluso di serie su Windows 11)
- **Framework**: .NET 9.0 Runtime

---

## 🛠️ Compilazione da sorgente

Per compilare ed eseguire il progetto localmente:

```bash
# Clona il repository
git clone https://github.com/hidaba/WhatsAppH.git
cd WhatsAppH

# Ripristina le dipendenze e compila
dotnet restore
dotnet build -c Release
```

In alternativa, puoi aprire la soluzione `WhatsappH.sln` con **Visual Studio 2022** (.NET 9 SDK installato) e premere `F5`.

---

## 📖 Guida all'uso

1. **Aggiunta Account**: Avvia l'applicazione e rinomina o aggiungi un account dal menu delle schede o dalle **Impostazioni** (⚙️).
2. **Accesso WhatsApp**: Inquadra il codice QR con WhatsApp sul tuo smartphone per sincronizzare la sessione.
3. **Traduzioni**: Passa il mouse su qualsiasi messaggio per visualizzare l'icona di traduzione istantanea 🌐.

### 🕹️ Controlli della Barra del Titolo

| Icona | Azione |
| :---: | :--- |
| ⚙️ | Apre la finestra Impostazioni (tema, lingua, gestione account) |
| 🔄 | Ricarica la scheda dell'account attivo |
| ✕ / — | Minimizza o riduci nella barra delle applicazioni / tray |

---

## 🧰 Stack Tecnologico

- **Linguaggio**: VB.NET
- **UI**: WPF (Windows Presentation Foundation) su .NET 9.0
- **Engine Web**: `Microsoft.Web.WebView2`
- **Notifiche**: `Microsoft.Toolkit.Uwp.Notifications`

---

## 📄 Licenza

Distribuito sotto licenza **MIT**. Consulta il file [LICENSE.txt](LICENSE.txt) per i dettagli.
