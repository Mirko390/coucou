// The settings window — an ordinary window around settings.html. Created the
// first time it is opened, then only ever shown and hidden: closing it hides it.

using Coucou.Native;
using Coucou.Web;

namespace Coucou;

sealed class SettingsWindow : Form
{
    static readonly Color Background = Color.FromArgb(0x0b, 0x0c, 0x0e);

    readonly string? devServer;
    readonly CommandHandler handle;
    WebHost? web;

    public SettingsWindow(string? devServer, CommandHandler handle)
    {
        this.devServer = devServer;
        this.handle = handle;
        Text = Loc.T("Settings — Coucou");
        Icon = AppIcon.Large();
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Background;
    }

    public WebHost? Web => web;

    /// <summary>After a language change.</summary>
    public void Retitle() => Text = Loc.T("Settings — Coucou");

    /// <summary>Shows the window, creating it and its page the first time.</summary>
    public void ShowAndFocus()
    {
        if (!IsHandleCreated)
        {
            _ = Handle;
            // Sized in logical pixels for the display it opens on.
            var scale = DeviceDpi / 96.0;
            ClientSize = new Size((int)Math.Round(560 * scale), (int)Math.Round(680 * scale));
            MinimumSize = SizeFromClientSize(new Size((int)Math.Round(460 * scale), (int)Math.Round(480 * scale)));
            CenterToScreen();
            _ = LoadPage();
        }
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Show();
        Activate();
    }

    async Task LoadPage()
    {
        try
        {
            web = await WebHost.Create(this, devServer, "settings.html", Background, handle);
        }
        catch (Exception e)
        {
            Log.Line($"settings window failed: {e.Message}");
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // The page is dark; so is its title bar.
        Win32.DwmSetWindowAttribute(Handle, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, 1, sizeof(int));
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Closing it must only hide it, or it could never be reopened — however
        // the close arrives (the X, Alt+F4, a WM_CLOSE from elsewhere). Only
        // Windows shutting down closes it for real; quitting disposes it.
        if (e.CloseReason != CloseReason.WindowsShutDown)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }

    /// <summary>A hidden page renders nothing and runs no timers.</summary>
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        web?.SetVisible(Visible);
    }
}
