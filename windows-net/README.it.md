<div align="center">

<img src="app/Assets/128x128.png" width="96" alt="Icona di Coucou">

# Coucou per Windows — .NET

**Su un PC Mochi non ha un notch dove stare, quindi vive in cima allo schermo.**

Approvi i permessi di Claude Code, segui le sessioni mentre lavorano, trascini un
file, chatti con Claude e tieni d'occhio i tuoi servizi, senza mollare quello che
stai facendo.

![Windows 10/11](https://img.shields.io/badge/Windows-10%2F11-0078D4?logo=windows)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![WebView2](https://img.shields.io/badge/WebView2-evergreen-0078D4?logo=microsoftedge&logoColor=white)
![Licenza: MIT](https://img.shields.io/badge/licenza-MIT-green)

[English](README.md) · **Italiano**

</div>

---

Questa cartella è la versione Windows di Coucou riscritta in .NET: un'app C#
attorno alla stessa isola, con lo stesso Mochi e gli stessi suoni della build
Tauri in [`windows/`](../windows/README.md). Le due versioni convivono nel
repository finché questa non sarà stata usata per un po'.

## Novità

### Riscritta in C# e .NET 10

- **App:** C# 14 su .NET 10, con WebView2 ospitato direttamente. Prende il posto
  di Rust e Tauri. L'isola, cioè la parte grafica in TypeScript, è la stessa.
- **Relay di Claude Code:** `coucou-hook.exe` è ora in C# compilato Native AOT.
  Pesa 1,5 MB e parte in circa 20 ms.
- **Installer MSI per utente:** niente richiesta di password di amministratore.
  Per aggiornare basta aprire il nuovo MSI sopra quello vecchio (vedi
  [Aggiornare](#aggiornare)).
- **Nessuna migrazione:** stesse preferenze in `%APPDATA%\Coucou\settings.json`,
  stesse chiavi nel Credential Manager e stessi hook in `~/.claude/settings.json`.
  Quello che avevi configurato con la build Tauri funziona subito.
- **0 % di CPU a isola nascosta:** le animazioni CSS si fermano quando l'isola si
  ritira. Prima consumava qualche punto percentuale anche quando non si vedeva.

### In italiano

- **Tutta l'app:** isola, finestra Impostazioni, menu dell'icona nell'area di
  notifica e messaggi di errore.
- **Dove si sceglie:** **Impostazioni… → Generali → Lingua**: *Automatica*
  (segue la lingua di Windows, ed è il valore predefinito), *English* o
  *Italiano*. Il cambio è immediato.
- **Il tono:** quando parla Mochi, i testi sono un po' scherzosi
  ("Gnam! E adesso cosa ne facciamo?"). Quelli su sicurezza, permessi e file
  restano seri.

### Visual Studio, non solo VS Code

- **Apri il progetto**, nella scheda di Claude Code, porta la cartella della
  sessione nel tuo editor. Si sceglie in **Impostazioni… → Generali → Apri i
  progetti con**:
  - **Automatica:** se nella cartella c'è una soluzione (`.slnx` o `.sln`), la
    apre in Visual Studio. Altrimenti prova VS Code, poi Visual Studio.
  - **Visual Studio**, **Visual Studio Code** o **Esplora file**.
- **Come trova Visual Studio:** con `vswhere`, cercando solo Community,
  Professional ed Enterprise. SQL Server Management Studio, che `vswhere`
  confonde con Visual Studio, non viene mai aperto per sbaglio.

### Barra mini a riposo

Prima l'isola a riposo era larga 288 px e copriva le schede del browser.

- **A riposo:** 80×24 px con il solo Mochi. Un pallino colorato segnala se c'è
  qualcosa in attesa: arancione per un permesso, rosso per un errore, verde per
  una sessione finita.
- **Con il mouse sopra:** si allarga a 240×24 e mostra i mini-Mochi delle
  integrazioni. Un clic apre l'isola.
- **Clic:** a riposo prende solo i clic sulla propria forma, e le animazioni di
  Mochi (mani, gocce di sudore, zeta del sonno) restano dentro la barra. Le schede
  sotto restano cliccabili.
- **Misure:** sono quelle della versione Mac per gli schermi senza notch.

### Domande con più scelte: «Rispondi in Claude»

Quando Claude ti propone delle scelte (lo strumento AskUserQuestion), prima
Coucou mostrava **Nega / Consenti**. Era sbagliato: premendo Consenti la domanda
sarebbe passata senza le tue risposte.

- **Le scelte restano a Claude:** Coucou gli restituisce subito la domanda, e le
  opzioni compaiono all'istante nella finestra della sessione.
- **Nell'isola:** vedi la domanda con le opzioni elencate e il pulsante
  **Rispondi in Claude**, che porta in primo piano la finestra della sessione:
  Claude Desktop, il terminale o VS Code.
- **Anche per le notifiche:** il pulsante compare anche quando Claude ti fa una
  domanda con una notifica.
- **Come trova la finestra:** il relay risale ai processi da cui è partito, e lo
  fa solo per gli eventi che aspettano una tua risposta e quando una sessione
  finisce.

### Fine sessione: il messaggio di Claude e «Apri»

- **Il messaggio intero:** quando Claude finisce, la card mostra il suo ultimo
  messaggio completo, ripulito dal markdown. Prima mostrava solo l'ultimo passo
  (per esempio "Legge views.ts"). La card si allunga per farci stare circa sei
  righe, e il resto scorre.
- **«Apri»:** porta in primo piano la finestra della sessione: Claude Desktop, il
  terminale o VS Code. Se la finestra non si trova, apre la cartella del progetto
  come faceva prima "Apri il progetto".

### Testi lunghi: ora scorrono

Prima i testi lunghi venivano tagliati. Ora scorrono dentro la card con la
rotella del mouse, mentre la riga in alto e i pulsanti restano sempre al loro
posto.

- **Domande:** il testo della domanda scorre, le opzioni stanno su una riga.
- **Fine sessione:** il messaggio di Claude.
- **Errori:** il dettaglio dell'errore.
- **Permessi:** il comando da approvare si legge tutto, andando a capo, invece
  di finire con i puntini. Sai sempre esattamente cosa stai consentendo.

### Chiusura automatica: 3, 5 o 10 secondi

- **Le scelte:** l'isola aperta si richiude dopo 3, 5 o 10 secondi da quando
  sposti il mouse altrove. Prima erano 10, 15 o 30.
- **Dove si sceglie:** dalla schermata impostazioni dell'isola oppure da
  **Impostazioni… → Generali → Chiusura automatica**.
- **Predefinito:** 5 secondi. Un valore salvato con una versione precedente passa
  alla scelta più vicina, per esempio da 15 a 10.

### Correzioni

- **Testo che scorre:** nel riquadro delle integrazioni, la riga che cambia non
  si sovrappone più a metà animazione.
- **Senza integrazioni:** se non hai servizi collegati, il riquadro principale
  occupa tutta la larghezza invece di lasciare uno spazio vuoto.
- **Schermi ridimensionati (125%, 150%):** se la scala cambiava mentre Coucou era
  aperto, per esempio passando dal monitor esterno al portatile, Mochi veniva
  disegnato troppo grande, spostato in basso e tagliato dalla barra. Ora si
  adatta da solo alla nuova scala.

## Installare

1. **Apri l'MSI:** doppio clic su `Coucou-Windows-X.Y.Z-x64.msi`. Installa in
   `%LOCALAPPDATA%\Programs\Coucou`, crea il collegamento nel menu Start e avvia
   Coucou.
2. **Se compare SmartScreen:** il file non è firmato, quindi Windows può mostrare
   "Windows ha protetto il PC". Clicca **Ulteriori informazioni → Esegui
   comunque**.
3. **Requisiti:** .NET non va installato, perché è già incluso. WebView2 è già
   presente in Windows 10 e 11.

Se usavi la build Tauri, chiudila o disinstallala prima: due Coucou vorrebbero
lo stesso canale del relay, e lo prende solo il primo.

**Disinstallare:** da Impostazioni di Windows → App. Coucou viene chiuso e i suoi
file temporanei in `%LOCALAPPDATA%\Coucou` vengono cancellati. Preferenze e
chiavi restano, e `~/.claude/settings.json` non viene mai toccato
dall'installer.

## Primo avvio

Clic destro sull'icona di Mochi nell'area di notifica → **Impostazioni…**

1. **Claude Code → Installa gli hook…:** ti mostra esattamente cosa cambierà in
   `%USERPROFILE%\.claude\settings.json` e dove salverà il backup datato. Scrive
   solo dopo il tuo clic, e i tuoi hook non vengono toccati.
2. **Claude → Chiave API:** serve solo per la chat.
3. **Integrazioni:** le chiavi dei servizi da seguire, tutte facoltative.
4. **Generali:** lingua, suono, chiusura automatica, editor, schermo su cui
   mostrare l'isola, avvio con Windows.

Poi apri una **nuova** sessione di Claude Code: quelle già aperte potrebbero non
vedere subito gli hook appena installati.

## Cosa fa

| Cosa fai | Cosa succede |
|---|---|
| Porti il mouse in alto al centro dello schermo | Mochi si affaccia |
| Clic sulla barra | L'isola si apre |
| Clic su Mochi | Si arrabbia. Tre clic di fila e gli gira la testa |
| Lasci il puntatore su Mochi per due secondi | Cuori |
| Trascini un file sull'isola | Mochi diventa una scatola, lo ingoia e poi risponde a domande sul file |
| `Esc` | Chiude l'isola |
| Icona nell'area di notifica | Apri Coucou, Impostazioni…, Pausa, Esci |

**Sessioni di Claude Code**
- Segui la sessione in tempo reale: prompt, strumenti usati passo per passo,
  errori, subagent, fine sessione.
- Quando Claude Code chiede un permesso, l'isola si apre con il comando
  completo e i pulsanti **Nega / Consenti**. Nessun permesso viene mai concesso
  senza un tuo clic.
- Quando una sessione finisce, vedi l'ultimo messaggio di Claude e con **Apri**
  torni alla sua finestra.
- Funziona con Claude Desktop (tab Code), Windows Terminal, PowerShell,
  VS Code e Git Bash.
- **Non blocca mai Claude Code.** Il relay ha 300 ms per raggiungere Coucou, poi
  esce. Se Coucou è chiuso, lento o in pausa, Claude Code ti chiede il permesso
  come al solito.

**Chat con Claude:** dall'isola, con ricerca web e conversazione su più turni.
Il modello si sceglie in **Impostazioni… → Claude**. Usa una chiave API della
Console Anthropic, quindi si paga a consumo e non rientra in un abbonamento a
Claude.

**File trascinati:** il file viene copiato in una cartella inbox e l'originale
non viene toccato. Poi puoi fare domande su quel file a Claude. Accetta PDF,
immagini (JPG, PNG, GIF, WebP) e file di testo o codice. La copia si cancella da
sola dopo 7 giorni.

**Integrazioni:** ogni servizio ha il suo mini-Mochi colorato, con un badge e un
suono quando c'è una novità. In **Impostazioni… → Integrazioni** scegli quali
mostrare, fino a 4.
- **Stripe:** saldo e ultimi pagamenti.
- **GitHub:** repository e stelle.
- **Vercel:** ultimi deploy, riusciti o falliti.
- **n8n:** ultime esecuzioni dei workflow, con il dettaglio degli errori.
- **Resend:** ultime email inviate.
- **Notion:** pagine modificate di recente.
- **Cal.com:** prossimi appuntamenti.

**Pausa**, dal menu dell'icona, ferma anche tutte le chiamate di rete, non solo
l'isola.

## Con Claude Desktop

Coucou si aggancia alle sessioni del tab **Code** di Claude Desktop attraverso
gli hook di Claude Code, gli stessi della riga di comando. Nel Desktop non c'è
niente da configurare.

- **Cosa vede:** le sessioni del tab Code, oltre a Claude Code da terminale o
  da VS Code.
- **Cosa non vede:** i tab Chat e Cowork, dove gli hook non partono.
- **Più sessioni insieme:** il mini-Mochi di Claude segue la più recente.
- **Due permessi nello stesso momento:** solo il primo arriva nell'isola. Il
  secondo torna subito alla finestra della sessione.
- **Notifiche doppie:** anche il Desktop ti avvisa quando una sessione aspetta.
  Se ti danno fastidio, spegnine una delle due.
- **Avvio:** apri Coucou dal menu Start, non chiedendo a Claude di eseguirlo.
  Il Desktop è un'app MSIX, e ciò che lancia finisce nel suo contenitore, dove
  le cartelle in `%LOCALAPPDATA%` vengono virtualizzate.

## Aggiornare

1. **Apri il nuovo MSI:** Coucou viene chiuso, aggiornato e riavviato.
2. **Cosa resta:** preferenze, chiavi e hook di Claude Code. Non serve
   disinstallare prima.

Per chi compila: alza `<Version>` in `Directory.Build.props` prima di
`npm run pack`. Un MSI con lo stesso numero di versione di quello installato
non lo sostituisce.

## Privacy e sicurezza

- **Chiavi:** stanno nel Credential Manager di Windows, mai su disco né
  nell'interfaccia. L'isola può solo chiedere se una chiave esiste.
- **Niente telemetria:** le uniche connessioni di rete vanno verso i servizi che
  configuri tu.
- **`~/.claude/settings.json`:** non viene mai sovrascritto. Prima il diff e il
  backup datato, poi la scrittura, solo dopo la tua conferma.
- **Canale del relay:** è una named pipe accessibile solo dal tuo utente
  Windows, e il relay controlla di parlare davvero con un Coucou dello stesso
  utente.
- **Log:** `%LOCALAPPDATA%\Coucou\coucou.log` resta sul tuo PC.

## Compilarlo da sé

Servono l'[SDK di .NET 10](https://dot.net), [Node 20+](https://nodejs.org) e
gli **MSVC build tools** (Visual Studio o Build Tools con "Sviluppo di
applicazioni desktop con C++"). Questi ultimi servono solo per compilare il
relay in un exe nativo.

**In Visual Studio:** apri `windows-net/Coucou.slnx` e premi F5.

**Da riga di comando**, nella cartella `windows-net/`:

| npm | dotnet | |
|---|---|---|
| `npm install` | | le dipendenze delle pagine, in `web/` |
| `npm start` | `dotnet run --project app` | compila e avvia Coucou |
| `npm test` | `dotnet test --project tests` | i test, senza Node né MSVC |
| `npm run build` | `dotnet build Coucou.slnx` | app, relay e test |
| `npm run pack` | `dotnet build installer -c Release` | la release, in `release/` |
| `npm run dev` | | Vite con ricaricamento automatico, per l'aspetto dell'isola |

I comandi npm sono scorciatoie per quelli dotnet: Visual Studio, `dotnet` e
`npm` fanno la stessa identica compilazione MSBuild.

**La release:** `npm run pack` ripubblica l'app da zero, costruisce l'MSI e
lascia in `windows-net/release/` l'installer con il numero di versione, lo
stesso installer con un nome fisso e lo zip portabile. L'MSI predefinito è
autonomo, circa 42 MB. Due proprietà cambiano il risultato:

```powershell
dotnet build installer -c Release -p:AppSelfContained=false   # circa 3,5 MB, richiede il .NET 10 Desktop Runtime
dotnet build installer -c Release -p:AppRuntime=win-arm64     # per ARM64
```

Il [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
serve solo sul PC che installa la versione da 3,5 MB.

Per lavorare sull'aspetto dell'isola con il ricaricamento automatico, avvia
Coucou sulle pagine servite da Vite:

```powershell
npm run dev                             # Vite su http://127.0.0.1:1420
dotnet run --project app -- --dev       # in un secondo terminale
```

**Aggiungere una lingua:** i testi nel codice sono in inglese e fanno anche da
chiave. Basta aggiungere una tabella in `web/src/core/i18n.ts` (le pagine) e una
in `app/Loc.cs` (l'app). `npm run build` si rifiuta di compilare se un testo
delle pagine non ha la traduzione, e un test fa lo stesso per l'app.

I dettagli tecnici (struttura delle cartelle, come funziona la finestra
trasparente, il relay) sono nel [README in inglese](README.md).

## Rispetto alla versione Mac

- **Posizione:** niente notch, quindi l'isola vive in alto al centro dello
  schermo e si ritira nel bordo superiore.
- **Più terminali:** i permessi si approvano da qualsiasi terminale. La versione
  Mac ascolta solo le sessioni di VS Code.
- **Mancano:** l'invio di un file per email e il trascinamento di Mochi su una
  finestra per allegarla come contesto.
- **Cal.com:** mostra una lista di appuntamenti invece del calendario.
