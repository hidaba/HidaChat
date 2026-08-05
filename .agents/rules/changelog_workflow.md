# Gestione Obbligatoria del CHANGELOG.md e Versionamento

Per ogni modifica effettuata al progetto:

1. **Aggiornamento del CHANGELOG.md**:
   - Aggiornare sempre il file `CHANGELOG.md`.

2. **Incremento di versione Patch (0.2.x)**:
   - Incrementare di `1` il terzo numero della versione (patch), seguendo il formato `0.2.x` (Esempio: `0.2.3` -> `0.2.4`).

3. **Raggruppamento per data**:
   - Se vengono effettuate più modifiche nello stesso giorno:
     - Non creare versioni separate;
     - Accorpare tutte le modifiche in un'unica versione;
     - Utilizzare un'unica sezione del `CHANGELOG.md` per quella data.

4. **Dettaglio delle voci**:
   - Ogni voce deve descrivere chiaramente:
     - Le funzionalità aggiunte;
     - Le modifiche effettuate;
     - Gli errori corretti;
     - Gli eventuali cambiamenti di configurazione.
