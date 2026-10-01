<div align="center">

<img src="../NotchBuddy/Assets.xcassets/AppIcon.appiconset/icon_256x256.png" width="96" alt="Icona di Coucou">

# Coucou

**Un piccolo amico che vive nel notch del tuo Mac, o in cima allo schermo su Windows, e tiene d'occhio le tue sessioni di Claude Code.**

Approvi i permessi, guardi lavorare i tuoi agenti, trascini un file, chatti con Claude, senza mollare quello che stai facendo.

![macOS 15+](https://img.shields.io/badge/macOS-15%2B-black?logo=apple)
![Windows 10/11](https://img.shields.io/badge/Windows-10%2F11-0078D4?logo=windows&logoColor=white)
![Swift 6](https://img.shields.io/badge/Swift-6-F05138?logo=swift&logoColor=white)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![Licenza: MIT](https://img.shields.io/badge/licenza-MIT-green)

**Italiano** · [English](../README.md)

<img src="../docs/media/demo.gif" width="760" alt="Coucou in azione">

</div>

---

> Questo è un fork di [Louis-CFM/coucou](https://github.com/Louis-CFM/coucou),
> il progetto originale di Louis Raillé. Aggiunge una **versione Windows
> riscritta in C# e .NET**, tradotta in italiano, in
> [`windows-net/`](../windows-net/README.it.md). Il resto del progetto è quello
> originale.

## Novità di questo fork

La versione Windows in [`windows-net/`](../windows-net/README.it.md) prende la
build Tauri (Rust) del progetto originale e la riscrive in **C# 14 su .NET 10**,
con la stessa isola, lo stesso Mochi e gli stessi suoni.

- 🧱 **Riscritta in .NET:** WebView2 ospitato direttamente, relay di Claude Code
  in C# Native AOT (1,5 MB, parte in circa 20 ms) e installer **MSI** che non
  chiede la password di amministratore e si aggiorna sopra la versione
  precedente.
- 🇮🇹 **In italiano:** isola, finestra Impostazioni, menu e messaggi di errore.
  La lingua si sceglie in **Impostazioni… → Generali → Lingua** (in automatico
  segue quella di Windows). Quando parla Mochi, i testi sono un po' scherzosi.
- 🛠️ **Visual Studio:** "Apri il progetto" apre la soluzione `.sln`/`.slnx` in
  Visual Studio, oppure la cartella in VS Code o in Esplora file, a tua scelta.
- 📏 **Barra mini a riposo:** 80×24 px con il solo Mochi e un pallino colorato
  quando qualcosa ti aspetta. Si allarga con il mouse sopra, e non copre più le
  schede del browser.
- ❓ **Domande con più scelte:** quando Claude ti propone delle opzioni, l'isola
  te le mostra e il pulsante **Rispondi in Claude** porta in primo piano la
  finestra della sessione: Claude Desktop, il terminale o VS Code.
- 🏁 **Fine sessione:** l'isola mostra l'ultimo messaggio di Claude per intero,
  e **Apri** ti riporta alla finestra della sessione.
- 📜 **Testi lunghi che scorrono** invece di essere tagliati: domande, messaggi,
  errori e il comando da approvare, che ora si legge tutto.
- ⏱️ **Chiusura automatica** a 3, 5 o 10 secondi.
- 🔋 **0 % di CPU** quando l'isola è nascosta, più alcune correzioni grafiche.
- ✅ **43 test automatici** e un workflow GitHub Actions che compila l'installer.

Tutti i dettagli sono nel [README della versione Windows](../windows-net/README.it.md).

## Perché

Alcuni studi di design hanno mostrato compagni da notch bellissimi… e non li
hanno mai fatti usare a nessuno. **Coucou è la versione aperta.** Ogni riga di
codice, ogni animazione, ogni suono: liberi da usare, leggere, forkare e
rimescolare.

Ti presento **Mochi**: un morbido quadratino arrotondato con gli occhioni, che
spunta dal notch, saluta, ti segue con lo sguardo, si arrabbia se lo punzecchi
(e gli gira la testa se insisti) e ti avvisa appena Claude Code ha bisogno di te.

## Funzioni

- 🤖 **Claude Code in diretta:** vedi ogni sessione mentre lavora, cosa legge,
  modifica ed esegue, passo per passo. Ha finito? Mochi fa un saltello di gioia.
- ✅ **Approvi dall'isola:** le richieste di permesso di Claude Code arrivano con
  **Consenti / Nega**. Un clic e torni al lavoro.
- 🧑‍💻 **La finestra giusta:** su macOS apre la finestra del terminale della
  sessione; su Windows **Rispondi in Claude** e **Apri** portano in primo piano
  la finestra della sessione quando Claude ti fa una domanda o ha finito.
- 💬 **Chiedi a Claude:** una chat integrata, direttamente dall'isola.
- 📎 **Trascini un file sull'isola:** Mochi diventa una scatola e lo ingoia,
  poi puoi fare domande sul file o, su macOS, mandarlo per email con Mail.
- 🪟 **Trascini Mochi su una finestra** per allegarla come contesto per Claude
  *(macOS)*.
- 🔌 **Integrazioni:** pagamenti Stripe, workflow n8n, GitHub, deploy Vercel,
  email Resend, Notion, Cal.com. Ognuna ha il suo piccolo Mochi colorato.
- 🎭 **Un vero personaggio:** respira, sbatte le palpebre, ha occhi su una sfera
  che seguono il mouse, espressioni, 28 suoni fatti a mano e un saluto
  all'avvio.
- 🫥 **Invisibile quando non serve:** si nasconde quando non succede niente e
  si affaccia quando porti il mouse sul notch (sul bordo superiore dello schermo
  su Windows).
- 🖥️ **Qualsiasi Mac, con o senza notch:** su iMac, Mac mini o MacBook chiuso
  con uno schermo esterno, Mochi sta in una piccola barra in cima allo schermo.
- 🔒 **Privato per progetto:** niente telemetria, niente account. Le chiavi
  stanno nel Portachiavi di macOS o nel Credential Manager di Windows, e l'app
  parla solo con i servizi che colleghi tu.

<table>
<tr>
<td><img src="../docs/media/claude-code.png" alt="Sessione di Claude Code"></td>
<td><img src="../docs/media/stripe.png" alt="Pagamenti Stripe"></td>
</tr>
<tr>
<td><img src="../docs/media/chat.png" alt="Chat con Claude"></td>
<td><img src="../docs/media/dizzy.png" alt="Troppi clic"></td>
</tr>
</table>

## Installare

### Windows (questo fork)

L'installer si costruisce dal codice in pochi minuti (vedi sotto):
`.\scripts\pack.ps1` produce `Coucou-Windows-X.Y.Z-x64.msi`.

1. **Apri l'MSI** con un doppio clic: installa solo per il tuo utente, senza
   password di amministratore, e avvia Coucou.
2. **SmartScreen:** il file non è firmato, quindi Windows può mostrare "Windows
   ha protetto il PC". Clicca **Ulteriori informazioni → Esegui comunque**.
3. **Requisiti:** .NET è già incluso. WebView2 c'è già in Windows 10 e 11.

Su un PC non c'è il notch, quindi l'isola scende dal bordo superiore dello
schermo invece di nascondersi dentro. Le altre differenze sono nel
[README della versione Windows](../windows-net/README.it.md).

### macOS

1. Scarica l'ultimo `Coucou.zip` dalle
   [Release del progetto originale](https://github.com/Louis-CFM/coucou/releases).
2. Decomprimi e sposta **Coucou.app** nella cartella **Applicazioni**.
3. Avvia. Questa build non è ancora notarizzata da Apple, quindi la prima volta
   macOS dice che non può verificare lo sviluppatore: apri **Impostazioni di
   Sistema → Privacy e sicurezza**, scorri in basso e clicca **Apri comunque**
   (solo la prima volta).

### Compilare dal codice

**Windows** — servono l'[SDK di .NET 10](https://dot.net), Node 20+ e gli MSVC
build tools (Visual Studio con "Sviluppo di applicazioni desktop con C++").

```powershell
git clone https://github.com/Mirko390/coucou.git
cd coucou\windows-net
.\scripts\pack.ps1          # l'installer finisce in windows-net\release\
```

**macOS** — servono macOS 15+, Xcode 16+ e
[XcodeGen](https://github.com/yonaskolb/XcodeGen).

```bash
brew install xcodegen
git clone https://github.com/Mirko390/coucou.git
cd coucou/NotchBuddy
xcodegen
open NotchBuddy.xcodeproj   # poi ⌘R
```

La build Windows originale in Tauri (Rust) è ancora in
[`windows/`](../windows/README.md).

## Configurazione

Clic sull'icona di Coucou nella barra dei menu (macOS) o nell'area di notifica
(Windows) → **Impostazioni…**

| Cosa | A cosa serve | Dove va la chiave |
|---|---|---|
| **Hook di Claude Code** | sessioni in diretta e approvazioni | **Installa gli hook**: Coucou fa un backup di `~/.claude/settings.json`, unisce i suoi hook e ti mostra il diff prima di scrivere qualsiasi cosa |
| **Chiave API Anthropic** | chat e domande sui file | Portachiavi / Credential Manager di Windows |
| Stripe, n8n, GitHub, Vercel, Resend, Notion, Cal.com | i mini-Mochi delle integrazioni | Portachiavi / Credential Manager di Windows, tutte facoltative |

Se Coucou non è in esecuzione, l'hook esce subito: **Claude Code non viene mai
bloccato.**

## Da provare

| Fai questo | Mochi fa quello |
|---|---|
| Porti il mouse sul notch (bordo superiore su Windows) | si affaccia e saluta 👋 |
| Ci clicchi | si apre |
| Passi sopra Mochi | sbatte le palpebre, gli occhi si ingrandiscono |
| Clicchi Mochi | si schiaccia e si arrabbia |
| Clicchi 3 volte di fila | 😵‍💫 gli gira la testa per qualche secondo |
| Trascini un file sull'isola | diventa una scatola e lo ingoia |
| Trascini Mochi su una finestra *(macOS)* | la allega come contesto |

## Come funziona

**macOS**

- **Isola:** un `NSPanel` senza bordi attaccato al notch, guidato da una piccola
  macchina a stati (`hidden → petit → home`).
- **Personaggio:** disegnato in SwiftUI con `Canvas` + `TimelineView` a 60 fps:
  corpo arrotondato, occhi proiettati su una sfera, animazioni a molla. Niente
  Rive, niente Lottie, niente immagini.
- **Claude Code:** un piccolo script `nb-hook` riceve gli eventi degli hook e li
  inoltra all'app su un socket Unix. Per le approvazioni aspetta il tuo clic,
  poi risponde all'hook.
- **Integrazioni:** controlli periodici leggeri, in pausa quando nessuno guarda.
- **Suoni:** 28 brevi WAV riprodotti con `AVAudioPlayer` precaricati.

L'app macOS è Swift 6 / SwiftUI / AppKit nativo, **senza dipendenze di terze
parti**.

**Windows (`windows-net/`)**

- **App:** C# 14 su .NET 10. L'isola è una finestra trasparente, sempre in
  primo piano, che non ruba mai il focus. Mochi è disegnato in Canvas 2D con le
  stesse forme, gli stessi tempi e gli stessi suoni del Mac, dentro WebView2.
- **Claude Code:** gli hook passano da un piccolo `coucou-hook.exe` (Native
  AOT) e da una named pipe accessibile solo dal tuo utente.
- **Chiavi:** nel Credential Manager di Windows.
- **Dettagli:** nel [README della versione Windows](../windows-net/README.it.md).

## Contribuire

Segnalazioni e proposte per il progetto originale vanno su
[Louis-CFM/coucou](https://github.com/Louis-CFM/coucou): nuove integrazioni,
nuove espressioni, nuovi suoni, correzioni. Vedi
[CONTRIBUTING.md](../CONTRIBUTING.md).

## Crediti

Creato da [Louis Raillé](https://louisraille.fr) con Claude Code. Versione
Windows .NET e traduzione italiana in questo fork.
Ispirato ai concept di compagni da notch condivisi da alcuni studi di design:
il progetto è indipendente e non è affiliato a nessuno di loro.

## Licenza

- **Codice:** [MIT](../LICENSE): usalo, forkalo, impara, basta mantenere
  l'avviso di copyright.
- **Nome, personaggio Mochi, icona, suoni e media:** © Louis Raillé, tutti i
  diritti riservati; vedi [LICENSE-ASSETS.md](../LICENSE-ASSETS.md). Se
  distribuisci un tuo fork, dagli un nome e un personaggio tuoi.

<div align="center">

[Sito](https://louis-cfm.github.io/coucou/) · [Privacy](https://louis-cfm.github.io/coucou/privacy.html) · [Termini](https://louis-cfm.github.io/coucou/terms.html) · [Supporto](https://louis-cfm.github.io/coucou/support.html)

</div>
