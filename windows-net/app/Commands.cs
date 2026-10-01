// The commands the pages call (see web/src/core/bridge.ts). They run on pool
// threads; anything that touches a window hops onto the UI thread.

using System.Text.Json;
using System.Text.Json.Nodes;
using Coucou.Island;

namespace Coucou;

sealed partial class CoucouApp
{
    async Task<JsonNode?> Dispatch(string command, JsonObject args, IReadOnlyList<string> files)
    {
        switch (command)
        {
            case "boot":
                return Boot();
            case "save_settings":
                SaveSettings(args);
                return null;

            // ── Island window ─────────────────────────────────────────────────
            case "set_collapsed":
                SetCollapsed(args.Get("collapsed").Bool() ?? false);
                return null;
            case "set_island_rect":
                gate.Rect = new(
                    args.Get("x").Num() ?? 0, args.Get("y").Num() ?? 0,
                    args.Get("width").Num() ?? 0, args.Get("height").Num() ?? 0,
                    args.Get("margin").Num() ?? 14);
                return null;
            case "focus_window":
                FocusWindow(args.Get("focused").Bool() ?? false);
                return null;
            case "reposition":
                OnUiThread(() => island.ApplyGeometry(CurrentSettings.Screen, gate.Collapsed));
                return null;
            case "open_settings_window":
                OnUiThread(settingsWindow.ShowAndFocus);
                return null;
            case "quit_app":
                OnUiThread(ExitThread, wait: false);
                return null;

            // ── Outside Coucou ────────────────────────────────────────────────
            case "open_url":
                Launcher.OpenUrl(args.Str("url") ?? "");
                return null;
            case "open_project":
                return Launcher.OpenProject(args.Str("path"), CurrentSettings.Editor);
            case "open_n8n":
                // The URL lives in the Credential Manager, like the key.
                if (Secrets.Get("n8n-url") is { } n8n) Launcher.OpenUrl(n8n);
                return null;
            case "log_line":
                Log.Line($"ui  {args.Str("message")}");
                return null;
            case "set_paused":
                // Paused means paused: the pollers stop talking to the network,
                // not just the island stopping showing things.
                pollers.Paused = args.Get("paused").Bool() ?? false;
                return null;

            // ── Claude Code hooks ─────────────────────────────────────────────
            case "hooks_status":
                return JsonSerializer.SerializeToNode(hooks.Status(), AppJson.Default.HookStatus);
            case "hooks_preview":
                // The diff the user has to look at before anything is written.
                return JsonSerializer.SerializeToNode(
                    hooks.Preview(args.Get("install").Bool() ?? true), AppJson.Default.HookPreview);
            case "hooks_apply":
                return HooksApply(args.Get("install").Bool() ?? true, args.Str("fingerprint") ?? "");
            case "approval_decision":
                relay.Answer(args.Str("requestId") ?? "", args.Str("decision") ?? "deny");
                return null;
            case "approval_ack":
                // The card is on screen, so the long wait for a human may begin.
                relay.Acknowledge(args.Str("requestId") ?? "");
                return null;
            case "approval_decline":
                // Nobody can act on it: Claude Code asks in the terminal right away.
                relay.Decline(args.Str("requestId") ?? "");
                return null;
            case "focus_session":
                return FocusSession(args.Str("sessionId") ?? "");

            // ── Chat, files, secrets ──────────────────────────────────────────
            case "chat_send":
            {
                var text = await chat.Send(
                    CurrentSettings.Model, args.Str("query") ?? "", ChatContext.From(args.Get("context")));
                return new JsonObject { ["text"] = text };
            }
            case "chat_reset":
                chat.Reset();
                return null;
            case "ingest_file":
                return JsonSerializer.SerializeToNode(
                    Inbox.Ingest(args.Str("path") ?? "", Paths.Inbox), AppJson.Default.DroppedFile);
            case "dropped_paths":
                return new JsonArray(files.Select(f => (JsonNode?)f).ToArray());
            case "secret_present":
                // The pages may only ask whether a key exists — never read it.
                return Secrets.Present(args.Str("key") ?? "");
            case "secret_set":
                Secrets.Set(args.Str("key") ?? "", args.Str("value") ?? "");
                return null;
            case "secret_clear":
                Secrets.Clear(args.Str("key") ?? "");
                return null;

            // ── Integrations ──────────────────────────────────────────────────
            case "refresh_integration":
                await pollers.PollOnce(args.Str("id") ?? "");
                return null;

            default:
                throw new UserFacingException($"unknown command {command}");
        }
    }

    JsonNode? Boot()
    {
        // The real state of ~/.claude/settings.json wins over whatever we stored.
        var current = CurrentSettings with { HooksInstalled = hooks.Status().Installed };
        var boot = new BootInfo(current, Monitors.Info(current.Screen), Version, Paths.HookExe);
        return JsonSerializer.SerializeToNode(boot, AppJson.Default.BootInfo);
    }

    void SaveSettings(JsonObject args)
    {
        var next = (args.Get("settings").Deserialize(AppJson.Default.Settings)
            ?? throw new UserFacingException("settings missing")).Normalized();
        Settings previous;
        lock (settingsLock)
        {
            previous = settings;
            settings = next;
        }
        try { next.Save(); }
        catch (Exception e) { Log.Line($"could not save settings: {e.Message}"); }

        if (previous.Autostart != next.Autostart) Autostart.Set(next.Autostart);
        if (previous.Language != next.Language)
        {
            Loc.Apply(next.Language);
            OnUiThread(() =>
            {
                tray.Relabel();
                settingsWindow.Retitle();
            });
        }
        if (previous.Screen != next.Screen)
            OnUiThread(() => island.ApplyGeometry(next.Screen, gate.Collapsed));
        EmitSettingsChanged(next);
    }

    /// <summary>
    /// Hidden island → shrink the window to the invisible wake strip and park the
    /// cursor poll; anything else → full panel and 60 Hz polling.
    /// </summary>
    void SetCollapsed(bool collapsed) => OnUiThread(() =>
    {
        gate.Collapsed = collapsed;
        island.ApplyGeometry(CurrentSettings.Screen, collapsed);
        // The wake strip must always take the mouse, and a resize invalidates the flag.
        island.SetIgnoreCursor(false);
        gate.ForgetIgnoreState();
        gate.SetActive(!collapsed);
    });

    void FocusWindow(bool focused) => OnUiThread(() =>
    {
        island.SetActivating(focused);
        if (!focused) return;
        Native.Win32.BringToFront(island.Hwnd);
        islandWeb?.Focus();
    });

    /// <summary>
    /// "Answer in Claude": brings forward the window the session runs in, as the
    /// relay reported it. False when it is unknown or has gone.
    /// </summary>
    bool FocusSession(string sessionId)
    {
        if (relay.HostOf(sessionId) is not { } hwnd) return false;
        var done = false;
        OnUiThread(() => done = Native.Win32.BringToFront(hwnd));
        return done;
    }

    /// <summary>Only ever called from an explicit click in the settings window.</summary>
    JsonNode HooksApply(bool install, string fingerprint)
    {
        // The fingerprint comes from the preview the user actually looked at, so
        // a settings.json that changed in between is refused rather than overwritten.
        var backup = hooks.Write(install, fingerprint);
        Settings updated;
        lock (settingsLock) updated = settings = settings with { HooksInstalled = install };
        try { updated.Save(); }
        catch (Exception e) { Log.Line($"could not save settings: {e.Message}"); }
        EmitSettingsChanged(updated);
        return backup;
    }
}
