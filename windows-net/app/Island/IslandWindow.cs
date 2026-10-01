// Island window: placement on the chosen display, the two window sizes
// (full panel / invisible wake strip), transparency and click-through.
//
// There is no notch on a PC, so the island is a black shape drawn at the top
// centre of the main display inside a borderless, transparent, always-on-top
// window that never takes focus. The shape itself is the web page; this window
// only has to be see-through everywhere the page is, and let clicks through
// everywhere the island is not.

using Coucou.Native;
using static Coucou.Native.Win32;

namespace Coucou.Island;

sealed class IslandWindow : Form
{
    /// <summary>Logical size of the full window — the largest island view, like the macOS panel.</summary>
    public const double PanelWidth = 720;
    public const double PanelHeight = 320;

    /// <summary>Logical size of the invisible strip that wakes the island when it is hidden.</summary>
    public const double StripWidth = 240;
    public const double StripHeight = 6;

    /// <summary>The window handle, readable from the cursor poll thread.</summary>
    public nint Hwnd { get; private set; }

    public IslandWindow()
    {
        Text = "Coucou";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ControlBox = false;
        MinimizeBox = false;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Black;
    }

    protected override bool ShowWithoutActivation => true;

    /// <summary>
    /// WS_EX_NOACTIVATE keeps clicks from stealing focus; WS_EX_TOOLWINDOW keeps
    /// the island out of Alt-Tab.
    /// </summary>
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= (int)(WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST);
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Hwnd = Handle;

        // Blur-behind with an empty region blurs nothing, but makes DWM honour
        // the window's alpha channel: everything painted with alpha 0 — GDI
        // black, and every transparent pixel of the page — is see-through.
        var region = CreateRectRgn(0, 0, -1, -1);
        var blur = new DwmBlurBehind { Flags = DWM_BB_ENABLE | DWM_BB_BLURREGION, Enable = 1, RegionBlur = region };
        DwmEnableBlurBehindWindow(Handle, in blur);
        DeleteObject(region);
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            // GDI writes alpha 0, so plain black is exactly "nothing here".
            case WM_ERASEBKGND:
                GetClientRect(m.HWnd, out var client);
                FillRect(m.WParam, in client, GetStockObject(BLACK_BRUSH));
                m.Result = 1;
                return;
            case WM_PAINT:
                var hdc = BeginPaint(m.HWnd, out var paint);
                FillRect(hdc, in paint.Paint, GetStockObject(BLACK_BRUSH));
                EndPaint(m.HWnd, in paint);
                m.Result = 0;
                return;
            // The island is placed and sized by ApplyGeometry, in physical pixels;
            // WinForms' own rescaling on a DPI change would only fight it.
            case WM_DPICHANGED:
                m.Result = 0;
                return;
        }
        base.WndProc(ref m);
    }

    /// <summary>Places and sizes the window. <paramref name="collapsed"/> picks the wake strip.</summary>
    public void ApplyGeometry(string pref, bool collapsed)
    {
        if (Monitors.Target(pref) is not { } m) return;
        var (lw, lh) = collapsed ? (StripWidth, StripHeight) : (PanelWidth, PanelHeight);
        var pw = Math.Max(1, (int)Math.Round(lw * m.Scale));
        var ph = Math.Max(1, (int)Math.Round(lh * m.Scale));
        var x = m.Bounds.Left + (m.Bounds.Width - pw) / 2;
        var y = m.Bounds.Top;

        SetWindowPos(Hwnd, HWND_TOPMOST, x, y, pw, ph, SWP_NOACTIVATE);
        // Moving across displays can rescale the window: re-assert the physical size.
        SetWindowPos(Hwnd, HWND_TOPMOST, 0, 0, pw, ph, SWP_NOACTIVATE | SWP_NOMOVE);
    }

    /// <summary>Temporarily allow activation so a text field inside the island can be typed in.</summary>
    public void SetActivating(bool activating) =>
        SetExStyle(WS_EX_NOACTIVATE, on: !activating);

    /// <summary>
    /// Click-through. WS_EX_TRANSPARENT only lets clicks through a layered
    /// window, hence the pair — the same two flags the Tauri build set.
    /// </summary>
    public void SetIgnoreCursor(bool ignore) =>
        SetExStyle(WS_EX_TRANSPARENT | WS_EX_LAYERED, on: ignore);

    void SetExStyle(long flags, bool on)
    {
        var ex = (long)GetWindowLongPtr(Hwnd, GWL_EXSTYLE);
        var want = on ? ex | flags : ex & ~flags;
        if (want != ex) SetWindowLongPtr(Hwnd, GWL_EXSTYLE, (nint)want);
    }
}
