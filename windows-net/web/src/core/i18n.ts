// Interface language.
//
// The English strings in the code double as keys: `t("Allow")` is "Allow" in
// English and whatever the active table says otherwise. A string missing from a
// table falls back to English, so a new string never shows up blank — it just
// shows up untranslated until the table catches up.
//
// The host (app/Loc.cs) translates its own strings — tray menu, window title,
// error messages — from the same preference.

export type Language = "en" | "it";
export type LanguagePref = "auto" | Language;

type Vars = Record<string, string | number>;

const IT: Record<string, string> = {
  // ── Header and shared ──────────────────────────────────────────────────────
  "Overview": "Panoramica",
  "Ask": "Chiedi",
  "Drop": "Trascina un file",
  "Settings": "Impostazioni",
  "Settings…": "Impostazioni…",
  "Mute": "Silenzia",
  "Open": "Apri",
  "Cancel": "Annulla",
  "Back": "Indietro",
  "Save": "Salva",
  "Remove": "Rimuovi",
  "Sound": "Suono",

  // ── Island views ───────────────────────────────────────────────────────────
  // Mochi speaks in the first person and doesn't mind a joke. Anything that
  // touches files, keys or permissions stays plain and exact.
  "Nothing running right now.": "Tutto tranquillo, per ora.",
  "Drop a file or window, or ask me anything.": "Lanciami un file o chiedimi quello che vuoi.",
  "Ask Claude": "Chiedi a Claude",
  "needs permission": "aspetta il tuo ok",
  "Deny": "Nega",
  "Allow": "Consenti",
  "Y": "S",
  "Claude Code is asking a question": "Claude Code ha una domanda per te",
  "Claude needs an answer.": "Claude aspetta una tua risposta.",
  "Answer in Claude": "Rispondi in Claude",
  "Answer in your terminal — Coucou can't reply for you yet.":
    "Rispondi in Claude Code: io da qui non so ancora farlo.",
  "Workflow stopped.": "Il workflow si è inceppato.",
  "Retry": "Riprova",
  "Open in n8n": "Apri in n8n",
  "Session stopped on an error.": "La sessione è inciampata in un errore.",
  "No detail available.": "Nessun dettaglio, purtroppo.",
  "Open project": "Apri il progetto",
  "Claude Code finished": "Claude Code ha finito",
  "Session finished": "Fatto! Sessione conclusa",
  "Too many hits at once.": "Ehi, piano con le mani!",
  "Give me a sec — back to work in three seconds.": "Mi gira la testa… torno operativo tra tre secondi.",
  "Auto-close · {s}s": "Chiusura automatica · {s} s",
  "{s}s": "{s} s",
  "Sending by email isn't in this version.": "Le email non le so ancora spedire, in questa versione.",
  "Claude is searching…": "Claude sta frugando sul web…",
  "Result": "Risultato",

  // ── Drop and upload ────────────────────────────────────────────────────────
  "Drop your files here": "Lanciami qui i tuoi file",
  "Images": "Immagini",
  "Code": "Codice",
  "Docs": "Documenti",
  "Uploading {name}": "Sto ingoiando {name}",
  "What do you want to do with it?": "Gnam! E adesso cosa ne facciamo?",
  "Ask a question": "Fai una domanda",

  // ── Chat ───────────────────────────────────────────────────────────────────
  "Ask me anything…": "Chiedimi pure qualsiasi cosa…",
  "Continue…": "Dimmi pure…",
  "Send": "Invia",

  // ── Claude Code steps ──────────────────────────────────────────────────────
  "Runs": "Esegue",
  "Reads": "Legge",
  "Writes": "Scrive",
  "Edits": "Modifica",
  "Finds": "Cerca file",
  "Searches": "Cerca",
  "Web search": "Ricerca web",
  "Fetches": "Scarica",
  "Tasks": "Attività",
  "Agent": "Agente",
  "Lists": "Elenca",
  "Tool": "Strumento",
  "Session": "Sessione",
  "⚠ failed": "⚠ non riuscito",
  "+ subagent": "+ subagente",
  "• subagent done": "• subagente finito",

  // ── Integration cards ──────────────────────────────────────────────────────
  "just now": "proprio ora",
  "{n}m": "{n} min",
  "{n}h": "{n} h",
  "{n}d": "{n} g",
  "{time} ago": "{time} fa",
  "Hooks not installed": "Hook non installati",
  "Key not configured": "Manca la chiave",
  "Connected · loading…": "Connesso · un attimo…",
  "Open n8n": "Apri n8n",
  "Open {name}": "Apri {name}",
  "Refresh": "Aggiorna",
  "Integration": "Integrazione",
  "Details": "Dettagli",
  "Deployments": "Deploy",
  "Deployment": "Deploy",
  "Ready": "Pronto",
  "Canceled": "Annullato",
  "Error": "Errore",
  "Emails": "Email",
  "Total stars": "Stelle totali",
  "Repositories": "Repository",
  "Payment": "Pagamento",
  "Payments": "Pagamenti",
  "Untitled": "Senza titolo",
  "Recent": "Recenti",
  "No calls scheduled": "Agenda libera: nessuna chiamata in vista",
  "Meeting": "Riunione",
  "Schedule": "Agenda",
  "Success": "Riuscito",
  "Failed": "Fallito",
  "Completed successfully.": "Tutto filato liscio.",
  "No error details available.": "Nessun dettaglio sull'errore, purtroppo.",

  // ── Settings window ────────────────────────────────────────────────────────
  "Settings — Coucou": "Impostazioni — Coucou",
  "Coucou is hooked into your Claude Code sessions. Tool calls, questions and permission requests show up in the island, and you can answer them there.":
    "Coucou è agganciato alle tue sessioni di Claude Code: strumenti usati, domande e richieste di permesso arrivano nell'isola, e puoi rispondere da lì.",
  "Install the hooks to see your Claude Code sessions in the island and approve permissions without leaving what you are doing.":
    "Installa gli hook: vedrai le sessioni di Claude Code nell'isola e approverai i permessi senza mollare quello che stai facendo.",
  "Relay": "Relay",
  "coucou-hook.exe is not in place yet. Restart Coucou; if it still fails, reinstall it.":
    "coucou-hook.exe non è ancora al suo posto. Riavvia Coucou; se il problema resta, reinstallalo.",
  "Reinstall hooks…": "Reinstalla gli hook…",
  "Install hooks…": "Installa gli hook…",
  "The relay isn't installed yet.": "Il relay non è ancora installato.",
  "Uninstall hooks…": "Disinstalla gli hook…",
  "This is exactly what will change in your settings.json. Your own hooks are left untouched.":
    "Ecco esattamente cosa cambierà nel tuo settings.json. I tuoi hook restano come sono.",
  "This removes Coucou's entries only. Your own hooks are left untouched.":
    "Vengono rimosse solo le voci di Coucou. I tuoi hook restano come sono.",
  "Backup → {path}": "Backup → {path}",
  "Back up and write": "Fai il backup e scrivi",
  "Back up and remove": "Fai il backup e rimuovi",
  "Done. Previous settings saved as {backup}. Open a new Claude Code session to pick the hooks up.":
    "Fatto. Le impostazioni precedenti sono salvate in {backup}. Apri una nuova sessione di Claude Code per attivare gli hook.",
  "Could not write: {error}": "Scrittura non riuscita: {error}",
  "Key saved in the Windows Credential Manager.": "Chiave salvata in Gestione credenziali di Windows.",
  "No key yet — the chat needs one.": "Ancora nessuna chiave: senza, la chat resta muta.",
  "(stored)": "(salvata)",
  "Save key": "Salva chiave",
  "Saved. It never touches disk.": "Salvata. Su disco non ci finisce mai.",
  "Could not save: {error}": "Salvataggio non riuscito: {error}",
  "Key removed.": "Chiave rimossa.",
  "Could not remove: {error}": "Rimozione non riuscita: {error}",
  "API key": "Chiave API",
  "Model": "Modello",
  "Secret key": "Chiave segreta",
  "Instance URL": "URL dell'istanza",
  "Integration token": "Token dell'integrazione",
  "Pick up to {max} pills to show next to Mochi — {used}/{max} in use. Keys are stored in the Windows Credential Manager, never on disk.":
    "Scegli fino a {max} pillole da mostrare accanto a Mochi: {used}/{max} in uso. Le chiavi sono salvate in Gestione credenziali di Windows, mai su disco.",
  "Integrations": "Integrazioni",
  "General": "Generali",
  "Main display": "Schermo principale",
  "Display under the cursor": "Schermo sotto il cursore",
  "Auto-close": "Chiusura automatica",
  "after you leave the island": "dopo che lasci l'isola",
  "Island lives on": "Mostra l'isola su",
  "Launch at startup": "Avvia con Windows",
  "Language": "Lingua",
  "Open projects in": "Apri i progetti con",
  "File Explorer": "Esplora file",
  "Automatic opens Visual Studio when the folder has a solution, VS Code otherwise.":
    "Automatica apre Visual Studio se nella cartella c'è una soluzione, altrimenti VS Code.",
  "Automatic": "Automatica",
  "No telemetry. Network requests only go to the services you configure yourself.":
    "Zero telemetria, niente spioni: Coucou parla solo con i servizi che configuri tu.",
};

const TABLES: Record<Language, Record<string, string> | null> = { en: null, it: IT };

/** "auto" follows Windows' display language, which WebView2 reports as navigator.language. */
export function resolveLanguage(pref: string | undefined): Language {
  if (pref === "en" || pref === "it") return pref;
  const ui = (navigator.languages?.[0] ?? navigator.language ?? "en").toLowerCase();
  return ui.startsWith("it") ? "it" : "en";
}

let current: Language = resolveLanguage("auto");

export function setLanguage(pref: string | undefined) {
  current = resolveLanguage(pref);
  document.documentElement.lang = current;
}

export function language(): Language {
  return current;
}

/** The string in the active language, with `{name}` placeholders filled in. */
export function t(text: string, vars?: Vars): string {
  const template = TABLES[current]?.[text] ?? text;
  if (!vars) return template;
  return template.replace(/\{(\w+)\}/g, (match, key: string) => (key in vars ? String(vars[key]) : match));
}

/** A number with the active language's decimal separator. */
export function formatNumber(n: number, fractionDigits: number): string {
  return new Intl.NumberFormat(current, {
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
    useGrouping: false,
  }).format(n);
}
