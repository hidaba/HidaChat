# Regola di Portabilità Strict (100% Portable)

- **Portabilità Assoluta**: L'applicazione WhatsappH è al **100% portabile**.
- **Vincolo di Salvataggio**: Non salvare, migrare o reindirizzare MAI file, profili WebView2, impostazioni, cache o dati temporanei al di fuori della cartella dell'applicazione (`AppDomain.CurrentDomain.BaseDirectory`).
- **Nessuna Scrittura Esterna**: È severamente vietato scrivere in `%LOCALAPPDATA%`, `%APPDATA%`, `%TEMP%` o qualsiasi altra directory esterna di sistema. Tutti i profili ed i dati devono risiedere sempre all'interno della sottocartella `data/webview` dell'applicazione.
