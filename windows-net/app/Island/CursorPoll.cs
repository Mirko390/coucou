// The cursor poll: emits `cursor` (window-logical coordinates) at ~60 Hz while
// the island is visible, decides click-through, and notices display changes.
// Parked on an event the rest of the time, so a hidden island costs nothing.

using Coucou.Native;

namespace Coucou.Island;

/// <summary>
/// The island shape in window-logical coordinates, pushed by the page. The poll
/// thread owns the click-through decision so it lands in the same 16 ms tick as
/// the cursor read — a round trip to the page here loses clicks.
/// </summary>
/// <param name="Margin">
/// How far around the shape still counts as on it: generous for the open island,
/// where a click must never be swallowed, and none at rest, where the bar sits
/// over other apps' tabs and title bars.
/// </param>
readonly record struct IslandRect(double X, double Y, double Width, double Height, double Margin);

/// <summary>Wakes / parks the cursor poll thread.</summary>
sealed class PollGate
{
    readonly ManualResetEventSlim active = new(false);
    readonly Lock rectLock = new();
    IslandRect rect;

    public volatile bool Collapsed = true;

    /// <summary>Mirrors the window flag so we only touch it when it changes.</summary>
    public volatile bool Ignoring;

    public IslandRect Rect
    {
        get { lock (rectLock) return rect; }
        set { lock (rectLock) rect = value; }
    }

    /// <summary>Forces the next poll tick to re-apply the flag (after a window resize).</summary>
    public void ForgetIgnoreState() => Ignoring = false;

    public void SetActive(bool on)
    {
        if (on) active.Set();
        else active.Reset();
    }

    public bool IsActive => active.IsSet;

    public void WaitUntilActive() => active.Wait();
}

/// <param name="setIgnoreCursor">Flips click-through; marshalled to the window's thread by the caller.</param>
/// <param name="screenPref">The "screen" preference, read on every check.</param>
sealed class CursorPoll(
    IslandWindow island,
    PollGate gate,
    Func<string> screenPref,
    Action<bool> setIgnoreCursor,
    Action<double, double> emitCursor,
    Action emitScreenChanged)
{
    static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(16);

    public void Start() => new Thread(Run) { IsBackground = true, Name = "cursor poll" }.Start();

    void Run()
    {
        using var sleep = new PreciseSleep();
        // Remembered across wakes so a display change while hidden is noticed the
        // moment the island comes back.
        (int, int, int, int, double)? lastScreen = null;
        while (true)
        {
            gate.WaitUntilActive();
            var last = (X: double.MinValue, Y: double.MinValue);
            var ticks = 0;
            while (gate.IsActive)
            {
                sleep.Sleep(Tick);

                // Checked about twice a second — the poll is already running, so
                // this costs one monitor query.
                if (++ticks % 30 == 0)
                {
                    var now = Monitors.Key(screenPref());
                    if (now is not null && now != lastScreen)
                    {
                        var first = lastScreen is null;
                        lastScreen = now;
                        if (!first)
                        {
                            Log.Line("display layout changed — repositioning");
                            emitScreenChanged();
                        }
                    }
                }

                var hwnd = island.Hwnd;
                if (hwnd == 0 || !Win32.GetWindowRect(hwnd, out var window)) continue;
                if (!Win32.GetCursorPos(out var cursor)) continue;
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;
                var x = (cursor.X - window.Left) / scale;
                var y = (cursor.Y - window.Top) / scale;
                if (Math.Abs(x - last.X) < 1 && Math.Abs(y - last.Y) < 1) continue;
                last = (x, y);

                // Click-through: the window only takes the mouse over the island
                // shape. A small entry margin means the flag is already off by
                // the time a moving cursor reaches a button.
                var r = gate.Rect;
                var onIsland = r.Width > 0
                    && x >= r.X - r.Margin
                    && x <= r.X + r.Width + r.Margin
                    && y >= r.Y - r.Margin
                    && y <= r.Y + r.Height + r.Margin;

                // A file being dragged has to be able to find us. Click-through
                // hides the window from WindowFromPoint, so OLE finds no drop
                // target and shows the "no drop" cursor. So while a button is held
                // anywhere over the panel, the whole panel takes the mouse, which
                // also makes the drop zone as forgiving as the Mac's.
                var down = (Win32.GetAsyncKeyState(Win32.VK_LBUTTON) & 0x8000) != 0;
                var dragging = down
                    && x >= 0 && x <= window.Width / scale
                    && y >= 0 && y <= window.Height / scale;

                var accept = onIsland || dragging;
                if (!gate.IsActive) continue; // collapsed while we were reading the cursor
                if (gate.Ignoring == accept)
                {
                    gate.Ignoring = !accept;
                    setIgnoreCursor(!accept);
                }

                emitCursor(x, y);
            }
        }
    }
}
