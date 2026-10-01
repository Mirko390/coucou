// Preferences, stored as plain JSON in %APPDATA%\Coucou\settings.json — the same
// file and the same shape as the Tauri build. No secret ever lands here: API
// keys live in the Windows Credential Manager.

using System.Text.Json;
using System.Text.Json.Serialization;
using Coucou.Hooks;

namespace Coucou;

sealed record Settings
{
    public bool SoundEnabled { get; init; } = true;
    public double SoundVolume { get; init; } = 0.12;
    /// <summary>Seconds the open island waits once the mouse leaves: one of <see cref="AutoCloseChoices"/>.</summary>
    public double AutoCloseInterval { get; init; } = 5;
    public double AbsenceInterval { get; init; } = 180;

    public IReadOnlyList<string> ActiveIntegrations { get; init; } =
        ["integration_resend", "integration_n8n", "integration_vercel", "integration_github"];

    /// <summary>"primary" = the main display, "cursor" = whichever display the mouse is on.</summary>
    public string Screen { get; init; } = "primary";

    public bool Autostart { get; init; }
    public bool HooksInstalled { get; init; }

    /// <summary>Claude model used by the chat. Changeable in the settings window.</summary>
    public string Model { get; init; } = ClaudeChat.DefaultModel;

    /// <summary>"auto" (Windows' display language), "en" or "it".</summary>
    public string Language { get; init; } = "auto";

    /// <summary>Where "Open project" opens a session: "auto", "visualstudio", "vscode" or "explorer".</summary>
    public string Editor { get; init; } = "auto";

    public static Settings Load()
    {
        try
        {
            var bytes = File.ReadAllBytes(Paths.SettingsFile);
            return (JsonSerializer.Deserialize(bytes, AppJson.Default.Settings) ?? new Settings()).Normalized();
        }
        catch
        {
            return new Settings();
        }
    }

    /// <summary>The auto-close choices offered by the pages (web/src/core/state.ts).</summary>
    public static readonly double[] AutoCloseChoices = [3, 5, 10];

    /// <summary>
    /// Brings values the pages no longer offer — the 10/15/30 of earlier versions,
    /// or anything typed into the old number field — to the nearest choice.
    /// </summary>
    public Settings Normalized() =>
        AutoCloseChoices.Contains(AutoCloseInterval)
            ? this
            : this with { AutoCloseInterval = AutoCloseChoices.MinBy(c => Math.Abs(c - AutoCloseInterval)) };

    public void Save()
    {
        Directory.CreateDirectory(Paths.ConfigDir);
        File.WriteAllBytes(Paths.SettingsFile, JsonSerializer.SerializeToUtf8Bytes(this, AppJson.Default.Settings));
    }
}

/// <summary>Source-generated JSON for everything that crosses to the pages or the disk.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(BootInfo))]
[JsonSerializable(typeof(ScreenInfo))]
[JsonSerializable(typeof(HookStatus))]
[JsonSerializable(typeof(HookPreview))]
[JsonSerializable(typeof(DroppedFile))]
partial class AppJson : JsonSerializerContext;

sealed record ScreenInfo(double X, double Y, double Width, double Height, double Scale);

sealed record BootInfo(Settings Settings, ScreenInfo Screen, string Version, string HookPath);
