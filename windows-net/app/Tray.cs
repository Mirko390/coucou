// Notification-area icon: Open, Settings, Pause, Quit.

namespace Coucou;

static class AppIcon
{
    static Stream Resource() =>
        typeof(AppIcon).Assembly.GetManifestResourceStream("Coucou.icon.ico")
        ?? throw new InvalidOperationException("icon resource missing");

    /// <summary>The icon at the notification area's size.</summary>
    public static Icon Small()
    {
        using var stream = Resource();
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    public static Icon Large()
    {
        using var stream = Resource();
        return new Icon(stream, SystemInformation.IconSize);
    }
}

sealed class Tray : IDisposable
{
    readonly NotifyIcon icon;
    readonly ContextMenuStrip menu;

    /// <summary>Each item with the English label it is translated from.</summary>
    readonly List<(ToolStripItem Item, string Label)> labelled = [];

    /// <param name="onItem">"open", "settings", "pause" or "quit".</param>
    public Tray(Action<string> onItem)
    {
        menu = new ContextMenuStrip();
        Add("Open Coucou", () => onItem("open"));
        menu.Items.Add(new ToolStripSeparator());
        Add("Settings…", () => onItem("settings"));
        Add("Pause", () => onItem("pause"));
        menu.Items.Add(new ToolStripSeparator());
        Add("Quit", () => onItem("quit"));

        icon = new NotifyIcon
        {
            Icon = AppIcon.Small(),
            Text = "Coucou",
            ContextMenuStrip = menu,
            Visible = true,
        };
        // A left click opens the island; the menu is on the right button.
        icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) onItem("open");
        };
    }

    void Add(string label, Action onClick) =>
        labelled.Add((menu.Items.Add(Loc.T(label), null, (_, _) => onClick()), label));

    /// <summary>After a language change.</summary>
    public void Relabel()
    {
        foreach (var (item, label) in labelled) item.Text = Loc.T(label);
    }

    public void Dispose()
    {
        // Hidden first, or a ghost icon lingers in the tray until hovered.
        icon.Visible = false;
        icon.Dispose();
        menu.Dispose();
    }
}
