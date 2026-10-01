// Where Coucou keeps things. The same places as the Tauri build, so preferences,
// the relay already referenced by ~/.claude/settings.json and the log carry over.

namespace Coucou;

static class Paths
{
    /// <summary>%APPDATA%\Coucou — preferences (settings.json). Never a secret.</summary>
    public static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Coucou");

    /// <summary>%LOCALAPPDATA%\Coucou — the relay, the inbox, the log, WebView2's data.</summary>
    public static string LocalDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Coucou");

    public static string SettingsFile => Path.Combine(ConfigDir, "settings.json");

    public static string HookExe => Path.Combine(LocalDir, "bin", "coucou-hook.exe");

    public static string Inbox => Path.Combine(LocalDir, "inbox");

    public static string LogFile => Path.Combine(LocalDir, "coucou.log");

    public static string WebViewData => Path.Combine(LocalDir, "WebView2");

    /// <summary>%USERPROFILE%\.claude\settings.json — Claude Code's, not ours.</summary>
    public static string ClaudeSettings =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");
}
