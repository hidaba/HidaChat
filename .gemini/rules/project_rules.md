# Regole di Sviluppo del Progetto

1. **Commenti nei Sorgenti**:
   - Non inserire mai numeri di versione nei commenti all'interno del codice sorgente (`.vb`).

2. **Changelog ed Incremento Versione (`AppVersion`)**:
   - A ogni modifica/push occorre **SEMPRE** compilare il file `CHANGELOG.md` registrando i cambiamenti ed incrementare il terzo numero di versione (patch version, es. `0.1.2` -> `0.1.3`) sia in `Constants.vb` che in `CHANGELOG.md`.

3. **Messaggi di Commit (Git)**:
   - Nei messaggi di commit di Git (la descrizione delle modifiche visibile su GitHub) **NON** inserire mai il prefisso o riferimenti al numero di versione (es. NON scrivere `v0.1.3:`). Usare descrizioni sintetiche e chiare focalizzate solo sulle modifiche apportate.

4. **Pacchetto di Release su GitHub ed OTA**:
   - La creazione e pubblicazione del pacchetto compilato (ZIP/Release) su GitHub Releases e dell'aggiornamento OTA si eseguono **solo ed esclusivamente quando richiesto esplicitamente dall'utente**.
