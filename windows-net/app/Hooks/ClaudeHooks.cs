// Claude Code hook installation.
//
// The rule from CLAUDE.md is strict and is followed to the letter:
// read %USERPROFILE%\.claude\settings.json, take a dated backup, merge without
// touching anybody else's hooks, show the diff, and write only after an explicit
// click. Uninstall removes Coucou's entries and nothing else.
//
// The command is only the quoted exe path in forward slashes plus the event name:
// on Windows Claude Code runs hook commands through Git Bash, and anything with
// PowerShell or cmd in it breaks.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Coucou.Hooks;

sealed record HookStatus(bool Installed, string SettingsPath, string HookPath, bool HookReady);

/// <param name="Fingerprint">
/// Identifies the bytes this diff was computed from; handed back to
/// <see cref="ClaudeHooks.Write"/> so we only ever apply what the user actually looked at.
/// </param>
sealed record HookPreview(string Diff, string Backup, string SettingsPath, string Fingerprint);

/// <param name="settingsPath">Claude Code's settings.json.</param>
/// <param name="hookExePath">Where coucou-hook.exe is installed.</param>
sealed class ClaudeHooks(string settingsPath, string hookExePath)
{
    /// <summary>
    /// Every event the island reacts to, with the hook timeout written to
    /// settings.json. PermissionRequest waits for a human, so it gets the
    /// decision timeout + 10 s.
    /// </summary>
    public static readonly IReadOnlyList<(string Event, int Timeout)> HookEvents =
    [
        ("SessionStart", 10),
        ("SessionEnd", 10),
        ("UserPromptSubmit", 10),
        ("PreToolUse", 10),
        ("PostToolUse", 10),
        ("PostToolUseFailure", 10),
        ("PermissionRequest", 120),
        ("Notification", 10),
        ("Stop", 10),
        ("StopFailure", 10),
        ("SubagentStart", 10),
        ("SubagentStop", 10),
    ];

    /// <summary>Marker that identifies a Coucou entry inside settings.json.</summary>
    const string Marker = "coucou-hook";

    public string SettingsPath => settingsPath;

    // ── Public API ────────────────────────────────────────────────────────────

    public HookStatus Status()
    {
        var current = ReadSettingsLossy();
        var installed = current["hooks"] is JsonObject hooks
            && hooks.Any(pair => pair.Value is JsonArray list && list.Any(EntryIsOurs));
        return new HookStatus(installed, settingsPath, hookExePath, File.Exists(hookExePath));
    }

    public HookPreview Preview(bool install)
    {
        var current = ReadSettings();
        var next = install ? Merged(current) : WithoutOurs(current);
        return new HookPreview(
            UnifiedDiff(JsonText.Pretty(current), JsonText.Pretty(next)),
            BackupPath(),
            settingsPath,
            CurrentFingerprint());
    }

    /// <summary>Writes the merged (or cleaned) settings after taking a dated backup.</summary>
    /// <remarks>
    /// <paramref name="fingerprint"/> is the one the preview was computed from. If
    /// the file changed in between — another tool, another window, the user's own
    /// editor — we stop and make them look at a fresh diff, because the only thing
    /// worse than not installing the hooks is silently reverting somebody else's edit.
    /// </remarks>
    /// <returns>The path of the backup.</returns>
    public string Write(bool install, string fingerprint)
    {
        var dir = Path.GetDirectoryName(settingsPath) ?? ".";
        Directory.CreateDirectory(dir);

        // Read before the backup: an unreadable file must abort before we touch
        // anything at all.
        var current = ReadSettings();
        if (CurrentFingerprint() != fingerprint)
            throw new UserFacingException(
                Loc.T("{0} changed since the preview. Nothing was written — review the new diff.", settingsPath));

        var backup = BackupPath();
        if (File.Exists(settingsPath))
        {
            try { File.Copy(settingsPath, backup, overwrite: true); }
            catch (Exception e) { throw new UserFacingException(Loc.T("backup failed: {0}", e.Message)); }
        }

        var next = install ? Merged(current) : WithoutOurs(current);
        var text = JsonText.Pretty(next) + "\n";

        // Write beside the target and rename over it: a crash or a full disk
        // leaves the original settings.json intact rather than half a file.
        var temp = $"{settingsPath}.coucou-{Environment.ProcessId}";
        try
        {
            File.WriteAllBytes(temp, Encoding.UTF8.GetBytes(text));
            File.Move(temp, settingsPath, overwrite: true);
        }
        catch (Exception e)
        {
            try { File.Delete(temp); } catch { /* best effort */ }
            throw new UserFacingException(Loc.T("write failed: {0}", e.Message));
        }
        return backup;
    }

    // ── Reading ───────────────────────────────────────────────────────────────

    /// <summary>Reads <c>~/.claude/settings.json</c>.</summary>
    /// <remarks>
    /// The only error that means "start from nothing" is the file not being there.
    /// Everything else — a lock held by another process, a permission problem,
    /// JSON we cannot parse — is reported, because the alternative is treating
    /// somebody's unreadable settings as an empty object and then writing that
    /// back over them.
    /// </remarks>
    JsonObject ReadSettings()
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(settingsPath);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return [];
        }
        catch (Exception e)
        {
            // A lock, a permission problem, a bad drive: all of them mean we do
            // not know what is in there, and not knowing is not the same as empty.
            throw new UserFacingException(Loc.T("Can't read {0}: {1}", settingsPath, e.Message));
        }
        return ParseSettings(bytes, settingsPath);
    }

    /// <summary>The parsing half of <see cref="ReadSettings"/>, split out so it can be tested.</summary>
    internal static JsonObject ParseSettings(ReadOnlySpan<byte> bytes, string path)
    {
        // PowerShell writes a UTF-8 BOM with `Set-Content -Encoding utf8`.
        // Stripping it is safe and well defined; guessing at anything else is not.
        if (bytes.StartsWith("﻿"u8)) bytes = bytes[3..];
        if (bytes.Trim(" \t\r\n\f"u8).IsEmpty) return [];

        JsonNode? parsed;
        try
        {
            // Duplicate keys are refused up front: keeping only one of them would
            // quietly drop part of somebody's file on the next write.
            parsed = JsonNode.Parse(bytes, documentOptions: new JsonDocumentOptions { AllowDuplicateProperties = false });
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new UserFacingException(
                Loc.T("{0} isn't valid JSON ({1}). Fix or move it, then try again — Coucou won't overwrite it.", path, e.Message));
        }
        return parsed as JsonObject
            ?? throw new UserFacingException(Loc.T("{0} isn't a JSON object — Coucou won't touch it.", path));
    }

    /// <summary>
    /// The settings as they are, or an empty object when we cannot tell. Only for
    /// read-only paths like <see cref="Status"/>, which must never fail loudly;
    /// anything that writes uses <see cref="ReadSettings"/> and surfaces the error.
    /// </summary>
    JsonObject ReadSettingsLossy()
    {
        try { return ReadSettings(); }
        catch (UserFacingException) { return []; }
    }

    // ── Merging ───────────────────────────────────────────────────────────────

    string HookCommand(string hookEvent) => $"\"{hookExePath.Replace('\\', '/')}\" {hookEvent}";

    internal static bool EntryIsOurs(JsonNode? entry) =>
        entry is JsonObject obj
        && obj["hooks"] is JsonArray hooks
        && hooks.Any(h => h is JsonObject hook
            && hook["command"] is JsonValue command
            && command.TryGetValue<string>(out var text)
            && text.Contains(Marker, StringComparison.Ordinal));

    /// <summary>Settings with Coucou's hooks added; everything else is left untouched.</summary>
    internal JsonObject Merged(JsonObject existing)
    {
        var root = existing.DeepClone().AsObject();
        if (root["hooks"] is not JsonObject hooks)
        {
            hooks = [];
            root["hooks"] = hooks;
        }

        foreach (var (hookEvent, timeout) in HookEvents)
        {
            if (hooks[hookEvent] is not JsonArray list)
            {
                list = [];
                hooks[hookEvent] = list;
            }
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (EntryIsOurs(list[i])) list.RemoveAt(i);
            }
            list.Add(new JsonObject
            {
                ["hooks"] = new JsonArray(new JsonObject
                {
                    ["type"] = "command",
                    ["command"] = HookCommand(hookEvent),
                    ["timeout"] = timeout,
                }),
            });
        }
        return root;
    }

    /// <summary>Settings with every Coucou entry removed, and nothing else changed.</summary>
    internal static JsonObject WithoutOurs(JsonObject existing)
    {
        var root = existing.DeepClone().AsObject();
        if (root["hooks"] is not JsonObject hooks) return root;

        var kept = new JsonObject();
        foreach (var (hookEvent, value) in hooks)
        {
            if (value is JsonArray list)
            {
                var others = list.Where(entry => !EntryIsOurs(entry)).Select(entry => entry?.DeepClone()).ToArray();
                if (others.Length > 0) kept[hookEvent] = new JsonArray(others);
            }
            else
            {
                kept[hookEvent] = value?.DeepClone();
            }
        }
        if (kept.Count == 0) root.Remove("hooks");
        else root["hooks"] = kept;
        return root;
    }

    // ── Backup and fingerprint ────────────────────────────────────────────────

    /// <summary>
    /// Down to the second: installing then uninstalling in the same minute must
    /// not quietly overwrite the first backup.
    /// </summary>
    string BackupPath() =>
        Path.Combine(
            Path.GetDirectoryName(settingsPath) ?? ".",
            "settings.json.bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));

    /// <summary>
    /// Identifies the exact bytes a preview was computed from. FNV-1a is plenty:
    /// the question is only "is this still the file I showed the user?".
    /// </summary>
    internal static string Fingerprint(ReadOnlySpan<byte> bytes)
    {
        var hash = 0xcbf2_9ce4_8422_2325UL;
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= 0x0000_0100_0000_01b3UL;
        }
        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    string CurrentFingerprint()
    {
        try { return Fingerprint(File.ReadAllBytes(settingsPath)); }
        catch { return Fingerprint([]); }
    }

    // ── Minimal unified diff (LCS) ────────────────────────────────────────────

    /// <summary>settings.json is short, so a plain O(n·m) LCS is the simplest honest diff.</summary>
    internal static string UnifiedDiff(string before, string after)
    {
        var a = Lines(before);
        var b = Lines(after);
        int n = a.Count, m = b.Count;

        var lcs = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                lcs[i, j] = a[i] == b[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var lines = new List<string>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y])
            {
                lines.Add("  " + a[x]);
                x++;
                y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                lines.Add("- " + a[x++]);
            }
            else
            {
                lines.Add("+ " + b[y++]);
            }
        }
        while (x < n) lines.Add("- " + a[x++]);
        while (y < m) lines.Add("+ " + b[y++]);

        // Keep three lines of context around each change so the panel stays readable.
        var changed = lines.Select((line, i) => (line, i))
            .Where(p => p.line.StartsWith('+') || p.line.StartsWith('-'))
            .Select(p => p.i)
            .ToList();
        if (changed.Count == 0) return Loc.T("No change.");

        var keep = new bool[lines.Count];
        foreach (var idx in changed)
        {
            for (var k = Math.Max(0, idx - 3); k < Math.Min(idx + 4, lines.Count); k++) keep[k] = true;
        }

        var result = new StringBuilder();
        var gap = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (keep[i])
            {
                result.Append(lines[i]).Append('\n');
                gap = false;
            }
            else if (!gap)
            {
                result.Append("  …\n");
                gap = true;
            }
        }
        return result.ToString();
    }

    /// <summary>Lines the way Rust's <c>str::lines</c> splits them: no trailing empty line, no <c>\r</c>.</summary>
    static List<string> Lines(string text)
    {
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return lines;
    }
}
