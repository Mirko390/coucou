// Coucou for Windows — app wiring. The commands the pages call are in Commands.cs.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Coucou.Hooks;
using Coucou.Integrations;
using Coucou.Island;
using Coucou.Web;

namespace Coucou;

sealed partial class CoucouApp : ApplicationContext
{
    public static readonly string Version =
        typeof(CoucouApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0]
        ?? "0.0.0";

    readonly Lock settingsLock = new();
    Settings settings;

    readonly string? devServer;
    readonly PollGate gate = new();
    readonly IslandWindow island = new();
    readonly SettingsWindow settingsWindow;
    readonly Tray tray;
    readonly HookRelay relay;
    readonly ClaudeHooks hooks = new(Paths.ClaudeSettings, Paths.HookExe);
    readonly ClaudeChat chat = new();
    readonly Pollers pollers;
    WebHost? islandWeb;

    Settings CurrentSettings
    {
        get { lock (settingsLock) return settings; }
    }

    /// <param name="devServer">The Vite dev server to load pages from, or null for wwwroot\.</param>
    public CoucouApp(SingleInstance instance, string? devServer)
    {
        this.devServer = devServer;
        settings = Settings.Load();
        Loc.Apply(settings.Language);
        relay = new HookRelay(payload => EmitToIsland("hook", payload));
        pollers = new Pollers(
            update => EmitToIsland("integration", update),
            id => CurrentSettings.ActiveIntegrations.Contains(id));
        settingsWindow = new SettingsWindow(devServer, Dispatch);
        tray = new Tray(OnTray);

        // Nothing closes the island but quitting, so a WM_CLOSE — the installer
        // making way for an upgrade, a task killer — means "quit", gracefully.
        island.FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.WindowsShutDown) return;
            e.Cancel = true;
            island.BeginInvoke(ExitThread);
        };

        // The window starts at full size so the launch greeting has room.
        _ = island.Handle;
        island.ApplyGeometry(settings.Screen, collapsed: false);
        island.Show();
        gate.Collapsed = false;
        gate.SetActive(true);
        new CursorPoll(
            island,
            gate,
            () => CurrentSettings.Screen,
            ignore => OnUiThread(() =>
            {
                // A poll tick that raced the collapse lands after it: it must not
                // make the wake strip click-through, or nothing could wake the island.
                if (ignore && gate.Collapsed) return;
                island.SetIgnoreCursor(ignore);
            }, wait: false),
            (x, y) => EmitToIsland("cursor", new JsonObject { ["x"] = x, ["y"] = y }),
            () => EmitToIsland("screen-changed", null)).Start();

        Log.Line($"--- Coucou {Version} started ---");
        HookExe.Ensure();
        relay.Start();
        pollers.Start();
        instance.OnWake(() => EmitToIsland("tray", "open"));
        _ = LoadIsland();
    }

    async Task LoadIsland()
    {
        try
        {
            islandWeb = await WebHost.Create(island, devServer, "index.html", Color.Transparent, Dispatch);
        }
        catch (Exception e)
        {
            // Most likely the WebView2 runtime is missing or broken.
            Log.Line($"island webview failed: {e.Message}");
            MessageBox.Show(
                Loc.T("Coucou could not start its window.\n\n{0}\n\nThe Microsoft Edge WebView2 Runtime may need to be installed or repaired.", e.Message),
                "Coucou", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ExitThread();
        }
    }

    // ── Events to the pages ───────────────────────────────────────────────────

    void EmitToIsland(string name, JsonNode? payload) => islandWeb?.Emit(name, payload);

    /// <summary>Keeps both windows in step (island ⇄ settings window).</summary>
    void EmitSettingsChanged(Settings current)
    {
        EmitToIsland("settings-changed", ToNode(current));
        settingsWindow.Web?.Emit("settings-changed", ToNode(current));
    }

    static JsonNode? ToNode(Settings s) => JsonSerializer.SerializeToNode(s, AppJson.Default.Settings);

    // ── Tray ──────────────────────────────────────────────────────────────────

    void OnTray(string item)
    {
        switch (item)
        {
            case "quit":
                ExitThread();
                break;
            case "settings":
                settingsWindow.ShowAndFocus();
                break;
            default:
                EmitToIsland("tray", item);
                break;
        }
    }

    // ── Threads ───────────────────────────────────────────────────────────────

    /// <summary>Runs on the UI thread, which owns both windows.</summary>
    void OnUiThread(Action action, bool wait = true)
    {
        if (island.IsDisposed) return;
        if (!island.InvokeRequired)
        {
            action();
            return;
        }
        try
        {
            if (wait) island.Invoke(action);
            else island.BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // Shutting down.
        }
    }

    protected override void ExitThreadCore()
    {
        Log.Line("--- Coucou quitting ---");
        tray.Dispose();
        islandWeb?.Close();
        settingsWindow.Web?.Close();
        settingsWindow.Dispose();
        island.Dispose();
        base.ExitThreadCore();
    }
}
