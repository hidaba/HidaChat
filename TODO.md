# TODO — Ottimizzazioni Prestazionali

## ~~1. I/O su `settings.json` — Letture multiple ridondanti~~ ✅
- ~~`SaveSettingAsync()` fa `ReadSettingsAsync()` + `WriteSettingsAsync()` ogni volta che cambia una singola chiave~~
- ~~**Ottimizzazione**: accumulare le modifiche in memoria e scrivere su disco con un debounce (es. 500ms) oppure riscrivere solo il file senza rileggerlo~~
- ~~**Impatto**: Alto | **Sforzo**: Basso~~

## ~~2. `FetchTranslations()` — N richieste HTTP seriali~~ ✅
- ~~Per ogni chiave di lingua (30+), fa una richiesta HTTP individuale a Google Translate, una dopo l'altra~~
- ~~**Ottimizzazione**: raggruppare le traduzioni in un'unica richiesta batch, o usare `HttpClient` con richieste parallele (`Task.WhenAll`)~~
- ~~**Impatto**: Alto | **Sforzo**: Medio~~

## 3. `StyleAccountItems()` — Ricorsione visual tree ogni volta
- Chiama `FindVisualChildren(Of Border)` e `FindVisualChildren(Of TextBox)` sull'intero `AccountsList`
- **Ottimizzazione**: usare stili WPF con DataTrigger invece di code-behind, oppure usare `Loaded` event handler sugli elementi del DataTemplate
- **Impatto**: Basso | **Sforzo**: Basso

## 4. `AccountsList.ItemsSource = Nothing` + riassegnazione
- Dopo eliminazione account, azzera e riassegna `ItemsSource` forzando rigenerazione di TUTTI i DataTemplate
- **Ottimizzazione**: usare `ObservableCollection` invece di `List(Of WhatsAppAccount)`
- **Impatto**: Medio | **Sforzo**: Basso

## 5. `FindVisualChildren()` e `FindVisualChild()` — Ricorsione multipla
- Funzioni ricorsive chiamate 3-4 volte per finestra che visitano TUTTO il visual tree
- **Ottimizzazione**: memoizzazione (cache dell'albero) o sostituzione con binding/stili dichiarativi
- **Impatto**: Basso | **Sforzo**: Basso

## 6. WebView2 — Script injection su ogni navigazione
- A ogni `NavigationCompleted`, reinietta tema CSS e script di traduzione inutilmente su navigazioni secondarie
- **Ottimizzazione**: iniettare solo su navigazione iniziale, usare `AddScriptToExecuteOnDocumentCreatedAsync` per script permanenti
- **Impatto**: Medio | **Sforzo**: Basso

## 7. Aggiornamento OTA — `ReadVersionFromFileAsync()` su UNC path
- Il check aggiornamenti legge un file da percorso di rete UNC, operazione bloccante se il server è lento
- **Ottimizzazione**: timeout più breve (3s), check asincrono senza bloccare l'avvio, caching della versione
- **Impatto**: Medio | **Sforzo**: Basso

## 8. `AccountManager.LoadAccountsAsync()` — Crea tutti i WebView2 all'avvio
- Crea un WebView2 per OGNI account al caricamento (~50-100MB per istanza)
- **Ottimizzazione**: lazy initialization — creare WebView2 solo per l'account attivo, gli altri on-demand
- **Impatto**: Alto | **Sforzo**: Alto

## 9. Traduzioni batch — Chunking fisso a 50 testi
- Dimensione chunk hardcoded a 50, non considera la lunghezza dei testi
- **Ottimizzazione**: chunking adattivo basato sulla lunghezza totale dei caratteri (es. max 2000 caratteri per chunk)
- **Impatto**: Medio | **Sforzo**: Basso

## 10. `MessagePopup.RepositionAll()` — Itera tutti i popup attivi
- Ogni volta che un popup viene mostrato/chiuso, ricalcola la posizione di TUTTI i popup attivi
- **Ottimizzazione**: tenere traccia incrementale della posizione Y del prossimo popup
- **Impatto**: Basso | **Sforzo**: Basso

## 11. `SwitchToAccountAsync()` — Visibilità di tutti i WebView
- Itera TUTTI i WebView per impostare `Visibility = Collapsed` e solo uno a `Visible`
- **Ottimizzazione**: tenere traccia dell'ultimo account attivo e nascondere solo quello
- **Impatto**: Basso | **Sforzo**: Basso

## 12. Translation cache — Scrittura completa su ogni cambio lingua
- `SaveCacheFileAsync()` riscrive l'intero file cache anche per modifiche minime
- **Ottimizzazione**: scrittura differita o incrementale
- **Impatto**: Basso | **Sforzo**: Basso

---

**Priorità consigliate**: 1 → 8 → 2 → 4 → 6 → 7
