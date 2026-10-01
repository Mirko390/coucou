// The pure half of the relay: turning Claude Code's hook JSON into what Coucou
// receives, and Coucou's one-word answer into what Claude Code expects. No I/O,
// so the tests can drive it directly.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Coucou.Hook;

/// <summary>One line to send down the pipe, plus the event it carries.</summary>
public sealed record HookEvent(string Line, string Event);

public static class HookPayload
{
    /// <summary>
    /// Fields that are pointless to forward and can be enormous (a whole file
    /// read, a full command output). The island never shows them.
    /// </summary>
    static readonly string[] DroppedFields = ["tool_response", "transcript_path"];

    /// <summary>
    /// Longest string forwarded for any single field, in UTF-8 bytes; the island
    /// truncates to far less than this anyway.
    /// </summary>
    public const int MaxFieldLength = 2_000;

    /// <summary>
    /// Which terminal the session runs in. Unlike macOS, Coucou on Windows
    /// accepts events from every terminal, so this is context only — never a filter.
    /// </summary>
    static readonly (string Key, string Variable)[] TerminalContext =
    [
        ("term_program", "TERM_PROGRAM"),
        ("wt_session", "WT_SESSION"),
        ("term_session_id", "TERM_SESSION_ID"),
        ("vscode_pid", "VSCODE_PID"),
        ("session_pid", "CLAUDE_CODE_SSE_PORT"),
    ];

    /// <summary>
    /// Reads the raw stdin bytes and returns the payload to forward plus the
    /// event name, or null when there is nothing usable to forward.
    /// </summary>
    /// <param name="argEvent">argv[1]; trusted when the JSON lacks the event name.</param>
    /// <param name="hostWindow">
    /// Finds the window the session runs in. Only called for the events that wait
    /// on the person (see <see cref="NeedsHost"/>), never for every tool call.
    /// </param>
    public static HookEvent? Read(
        ReadOnlySpan<byte> raw,
        string argEvent,
        Func<string, string?> environment,
        string currentDirectory,
        Func<long?>? hostWindow = null)
    {
        // Some shells hand us a UTF-8 BOM; the JSON reader would choke on it.
        if (raw.StartsWith("﻿"u8)) raw = raw[3..];
        if (raw.IsEmpty) return null;

        JsonObject map;
        try
        {
            if (JsonNode.Parse(raw) is not JsonObject parsed) return null;
            map = parsed;
        }
        catch (JsonException)
        {
            return null;
        }

        var fromJson = (map["hook_event_name"] as JsonValue)?.TryGetValue<string>(out var name) == true
            ? name
            : null;
        var hookEvent = string.IsNullOrEmpty(fromJson) ? argEvent : fromJson;
        map["hook_event_name"] = hookEvent;

        foreach (var field in DroppedFields) map.Remove(field);

        var cwd = (map["cwd"] as JsonValue)?.TryGetValue<string>(out var c) == true ? c : null;
        if (string.IsNullOrEmpty(cwd)) map["cwd"] = currentDirectory;

        foreach (var (key, variable) in TerminalContext)
        {
            if (!map.ContainsKey(key)) map[key] = environment(variable) ?? "";
        }

        // Where to send the person when Claude asks something only they can answer.
        if (hostWindow is not null && NeedsHost(hookEvent) && hostWindow() is { } hwnd)
            map["host_window"] = hwnd;

        TruncateStrings(map);
        return new HookEvent(map.ToJsonString() + "\n", hookEvent);
    }

    /// <summary>The events after which the island may offer to bring the session's window forward.</summary>
    public static bool NeedsHost(string hookEvent) => hookEvent is "PermissionRequest" or "Notification";

    /// <summary>Caps every string in the payload. A single Write can carry a whole file.</summary>
    public static void TruncateStrings(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    var value = obj[key];
                    if (Cut(value) is { } cut) obj[key] = cut;
                    else TruncateStrings(value);
                }
                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    if (Cut(array[i]) is { } cut) array[i] = cut;
                    else TruncateStrings(array[i]);
                }
                break;
        }
    }

    /// <summary>The shortened string, or null when the node is not an over-long string.</summary>
    static string? Cut(JsonNode? node)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var s)) return null;
        if (Encoding.UTF8.GetByteCount(s) <= MaxFieldLength) return null;

        // Cut on a character boundary; a lone byte index can split UTF-8.
        var bytes = 0;
        var end = 0;
        while (end < s.Length)
        {
            Rune.DecodeFromUtf16(s.AsSpan(end), out var rune, out var consumed);
            if (bytes + rune.Utf8SequenceLength > MaxFieldLength) break;
            bytes += rune.Utf8SequenceLength;
            end += consumed;
        }
        return string.Concat(s.AsSpan(0, end), "…");
    }

    /// <summary>
    /// The documented PermissionRequest output. Anything we do not recognise
    /// prints nothing at all rather than guessing — silence is the safe answer.
    /// See https://code.claude.com/docs/en/hooks
    /// </summary>
    public static string? DecisionJson(string decision)
    {
        var behavior = decision.Trim() switch
        {
            // "always" still answers a plain allow; remembering it is the
            // island's business, not Claude Code's.
            "allow" or "always" => """{"behavior":"allow"}""",
            "deny" => """{"behavior":"deny","message":"Denied from Coucou"}""",
            _ => null,
        };
        return behavior is null
            ? null
            : """{"hookSpecificOutput":{"hookEventName":"PermissionRequest","decision":""" + behavior + "}}";
    }
}
