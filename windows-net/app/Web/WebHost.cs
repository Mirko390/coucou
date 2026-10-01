// One WebView2 inside one of our windows, and the message channel to its page.
//
// Wire format (the page side lives in web/src/core/bridge.ts):
//   page → host   { id, cmd, args }                 chrome.webview.postMessage
//   host → page   { id, ok: true, result }          reply
//                 { id, ok: false, error }
//   host → page   { event, payload }                push
//
// The pages are locked in: they are served from a virtual host mapped onto
// wwwroot\, they cannot navigate anywhere else, open windows, download, or ask
// for any browser permission, and only messages from our own origin are read.

using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;

namespace Coucou.Web;

/// <summary>Runs one page command; <paramref name="files"/> are paths of dropped files, if any.</summary>
delegate Task<JsonNode?> CommandHandler(string command, JsonObject args, IReadOnlyList<string> files);

sealed class WebHost
{
    /// <summary>
    /// WebView2 allows one browser environment per app, and its options are
    /// fixed by whichever webview is created first, so every window shares this one.
    /// </summary>
    static Task<CoreWebView2Environment>? environment;

    const string VirtualHost = "coucou.localhost";

    /// <summary>The same switches the Tauri build passed to WebView2.</summary>
    const string BrowserArguments =
        "--disable-features=msWebOOUI,msPdfOOUI,msSmartScreenProtection --autoplay-policy=no-user-gesture-required";

    readonly Form form;
    readonly CoreWebView2Controller controller;
    readonly CoreWebView2 core;
    readonly string origin;
    readonly CommandHandler handle;

    WebHost(Form form, CoreWebView2Controller controller, string origin, CommandHandler handle)
    {
        this.form = form;
        this.controller = controller;
        this.origin = origin;
        this.handle = handle;
        core = controller.CoreWebView2;
    }

    static Task<CoreWebView2Environment> SharedEnvironment()
    {
        if (environment is not null) return environment;
        // Transparent from the very first frame: no white flash in the island
        // before the page has painted. Each window then sets its own colour.
        Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", "00000000");
        var options = new CoreWebView2EnvironmentOptions(BrowserArguments);
        return environment = CoreWebView2Environment.CreateAsync(null, Paths.WebViewData, options);
    }

    /// <param name="devServer">The Vite dev server's origin, or null to serve wwwroot\.</param>
    /// <param name="page">"index.html" or "settings.html".</param>
    public static async Task<WebHost> Create(Form form, string? devServer, string page, Color background, CommandHandler handle)
    {
        var env = await SharedEnvironment();
        var controller = await env.CreateCoreWebView2ControllerAsync(form.Handle);
        // It has done its job; VS Code or a browser started from Coucou must not inherit it.
        Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", null);
        controller.DefaultBackgroundColor = background;
        controller.Bounds = form.ClientRectangle;

        var origin = devServer?.TrimEnd('/') ?? $"https://{VirtualHost}";
        var host = new WebHost(form, controller, origin, handle);
        host.Configure(devServer is null);
        form.Resize += (_, _) => controller.Bounds = form.ClientRectangle;
        form.Move += (_, _) => controller.NotifyParentWindowPositionChanged();
        host.core.Navigate($"{origin}/{page}");
        return host;
    }

    void Configure(bool packaged)
    {
        var settings = core.Settings;
        settings.IsWebMessageEnabled = true;
        settings.AreHostObjectsAllowed = false;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsPinchZoomEnabled = false;
        settings.IsSwipeNavigationEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
#if DEBUG
        settings.AreDevToolsEnabled = true;
#else
        settings.AreDevToolsEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        // No F5, Ctrl+R, Ctrl+P, Ctrl+F… inside the island.
        settings.AreBrowserAcceleratorKeysEnabled = false;
#endif

        if (packaged)
        {
            var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            if (!Directory.Exists(wwwroot)) Log.Line($"pages not found at {wwwroot} — build web/ first");
            core.SetVirtualHostNameToFolderMapping(VirtualHost, wwwroot, CoreWebView2HostResourceAccessKind.Deny);
        }

        core.NavigationStarting += (_, e) =>
        {
            if (!e.Uri.StartsWith(origin + "/", StringComparison.OrdinalIgnoreCase)) e.Cancel = true;
        };
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.DownloadStarting += (_, e) => e.Cancel = true;
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        core.ProcessFailed += (_, e) =>
        {
            Log.Line($"webview process failed: {e.ProcessFailedKind} ({e.Reason})");
            if (e.ProcessFailedKind is CoreWebView2ProcessFailedKind.RenderProcessExited
                or CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
            {
                core.Reload();
            }
        };
        core.WebMessageReceived += OnMessage;
    }

    void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!e.Source.StartsWith(origin + "/", StringComparison.OrdinalIgnoreCase)) return;

        JsonNode? message;
        try { message = JsonNode.Parse(e.WebMessageAsJson); }
        catch (System.Text.Json.JsonException) { return; }
        if (message.Get("id").Int() is not { } id || message.Str("cmd") is not { } command) return;
        var args = message.Get("args")?.DeepClone() as JsonObject ?? [];

        // Dropped files arrive as CoreWebView2File objects, the only way a page
        // can hand over a real path. They live on this thread: read them now.
        var files = new List<string>();
        if (e.AdditionalObjects is { } objects)
        {
            foreach (var item in objects)
            {
                if (item is CoreWebView2File file) files.Add(file.Path);
            }
        }

        _ = Respond(id, command, args, files);
    }

    /// <summary>Runs the command off the UI thread, then replies on it.</summary>
    async Task Respond(long id, string command, JsonObject args, List<string> files)
    {
        var reply = new JsonObject { ["id"] = id };
        try
        {
            reply["result"] = await Task.Run(() => handle(command, args, files));
            reply["ok"] = true;
        }
        catch (Exception e)
        {
            if (e is not UserFacingException) Log.Line($"{command} failed: {e}");
            reply["ok"] = false;
            reply["error"] = e.Message;
        }
        Post(reply.ToJsonString());
    }

    /// <summary>Pushes an event to the page. Safe from any thread.</summary>
    public void Emit(string name, JsonNode? payload) =>
        Post(new JsonObject { ["event"] = name, ["payload"] = payload }.ToJsonString());

    void Post(string json)
    {
        if (form.IsDisposed || !form.IsHandleCreated) return;
        if (form.InvokeRequired)
        {
            try { form.BeginInvoke(() => Post(json)); }
            catch (InvalidOperationException) { /* the window is going away */ }
            return;
        }
        try { core.PostWebMessageAsJson(json); }
        catch (Exception e) when (e is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // The page is reloading or the webview is shutting down.
        }
    }

    public void SetVisible(bool visible) => controller.IsVisible = visible;

    /// <summary>Hands keyboard focus to the page (the island's chat field).</summary>
    public void Focus() => controller.MoveFocus(CoreWebView2MoveFocusReason.Programmatic);

    public void Close() => controller.Close();
}
