// The host's own strings in the interface language: the tray menu, the settings
// window's title, and every message that reaches the pages as an error.
//
// Same scheme as web/src/core/i18n.ts: the English text is the key, a missing
// entry falls back to English. Placeholders are string.Format's {0}, {1}.

using System.Globalization;

namespace Coucou;

static class Loc
{
    static readonly Dictionary<string, string> It = new()
    {
        // ── Tray and windows ──────────────────────────────────────────────────
        ["Open Coucou"] = "Apri Coucou",
        ["Settings…"] = "Impostazioni…",
        ["Pause"] = "Pausa",
        ["Quit"] = "Esci",
        ["Settings — Coucou"] = "Impostazioni — Coucou",
        ["Coucou could not start its window.\n\n{0}\n\nThe Microsoft Edge WebView2 Runtime may need to be installed or repaired."] =
            "Coucou non è riuscito ad aprire la sua finestra.\n\n{0}\n\nProva a installare o riparare Microsoft Edge WebView2 Runtime.",

        // ── Chat ──────────────────────────────────────────────────────────────
        ["API key missing. Open settings."] = "Mi manca la chiave API: aggiungila nelle impostazioni.",
        ["Claude declined this one."] = "Su questa Claude ha preferito passare.",
        ["Unexpected API response."] = "Risposta inattesa dall'API.",
        ["No response text."] = "Claude ha risposto senza scrivere niente.",
        ["Network error: {0}"] = "Errore di rete: {0}",
        ["Claude API {0}: {1}"] = "API di Claude {0}: {1}",
        ["Bad API response: {0}"] = "Risposta dell'API non valida: {0}",

        // ── Claude Code settings.json — plain and exact on purpose ────────────
        ["Can't read {0}: {1}"] = "Impossibile leggere {0}: {1}",
        ["{0} isn't valid JSON ({1}). Fix or move it, then try again — Coucou won't overwrite it."] =
            "{0} non è un JSON valido ({1}). Correggilo o spostalo e riprova: Coucou non lo sovrascrive.",
        ["{0} isn't a JSON object — Coucou won't touch it."] = "{0} non è un oggetto JSON: Coucou non lo tocca.",
        ["{0} changed since the preview. Nothing was written — review the new diff."] =
            "{0} è cambiato dopo l'anteprima. Non è stato scritto nulla: controlla il nuovo diff.",
        ["backup failed: {0}"] = "backup non riuscito: {0}",
        ["write failed: {0}"] = "scrittura non riuscita: {0}",
        ["No change."] = "Nessuna modifica.",

        // ── Dropped files ─────────────────────────────────────────────────────
        ["Folders can't be dropped yet."] = "Le cartelle non le digerisco ancora: prova con un file.",
        ["cannot read {0}: {1}"] = "impossibile leggere {0}: {1}",
        ["cannot copy: {0}"] = "copia non riuscita: {0}",

        // ── Integrations ──────────────────────────────────────────────────────
        ["Invalid API key (401)"] = "Chiave API non valida (401)",
        ["Use a secret key (sk_live_… not pk_live_…)"] = "Usa una chiave segreta (sk_live_…, non pk_live_…)",
        ["Token lacks the needed scope"] = "Al token mancano i permessi necessari",
        ["Token lacks access"] = "Il token non ha accesso",
        ["Key lacks access"] = "La chiave non ha accesso",
        ["Integration lacks access"] = "L'integrazione non ha accesso",
        ["API error {0}"] = "Errore API {0}",
        ["No connection: {0}"] = "Nessuna connessione: {0}",
    };

    /// <summary>
    /// "en" until the app applies the preference, so code that runs without it
    /// (the tests) speaks English whatever the machine's language.
    /// </summary>
    static volatile string current = "en";

    public static string Language => current;

    /// <summary>Applies the "language" preference; "auto" follows Windows' display language.</summary>
    public static void Apply(string? pref) => current = Resolve(pref);

    public static string Resolve(string? pref) => pref switch
    {
        "en" or "it" => pref,
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "it" ? "it" : "en",
    };

    public static string T(string text) =>
        current == "it" && It.TryGetValue(text, out var translated) ? translated : text;

    public static string T(string text, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(text), args);

    /// <summary>Every English key, for the test that keeps the table honest.</summary>
    internal static IEnumerable<string> Keys => It.Keys;
}
