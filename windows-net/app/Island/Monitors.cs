// The displays, in physical pixels, with their scale. Read fresh every time:
// monitors get plugged in, unplugged, rearranged and rescaled, and a cached list
// is how an island ends up pinned to coordinates that no longer exist.

using System.Runtime.InteropServices;
using Coucou.Native;

namespace Coucou.Island;

readonly record struct MonitorArea(Rect Bounds, double Scale, bool Primary);

static unsafe class Monitors
{
    public static List<MonitorArea> All()
    {
        var handles = new List<nint>();
        var self = GCHandle.Alloc(handles);
        try
        {
            Win32.EnumDisplayMonitors(0, 0, &Collect, GCHandle.ToIntPtr(self));
        }
        finally
        {
            self.Free();
        }

        var areas = new List<MonitorArea>(handles.Count);
        foreach (var monitor in handles)
        {
            var info = new MonitorInfo { Size = sizeof(MonitorInfo) };
            if (!Win32.GetMonitorInfo(monitor, ref info)) continue;
            var scale = Win32.GetDpiForMonitor(monitor, Win32.MDT_EFFECTIVE_DPI, out var dpi, out _) == 0 ? dpi / 96.0 : 1.0;
            areas.Add(new MonitorArea(info.Monitor, scale, (info.Flags & Win32.MONITORINFOF_PRIMARY) != 0));
        }
        return areas;
    }

    [UnmanagedCallersOnly]
    static int Collect(nint monitor, nint hdc, Rect* rect, nint data)
    {
        ((List<nint>)GCHandle.FromIntPtr(data).Target!).Add(monitor);
        return 1;
    }

    /// <summary>The display the island lives on: the primary one, or the one under the cursor.</summary>
    public static MonitorArea? Target(string pref)
    {
        var all = All();
        if (pref == "cursor" && Win32.GetCursorPos(out var cursor))
        {
            foreach (var m in all)
            {
                if (m.Bounds.Contains(cursor.X, cursor.Y)) return m;
            }
        }
        foreach (var m in all)
        {
            if (m.Primary) return m;
        }
        return all.Count > 0 ? all[0] : null;
    }

    /// <summary>Logical rect and scale of the island's display, for the page.</summary>
    public static ScreenInfo Info(string pref) => Target(pref) is { } m
        ? new ScreenInfo(
            m.Bounds.Left / m.Scale,
            m.Bounds.Top / m.Scale,
            m.Bounds.Width / m.Scale,
            m.Bounds.Height / m.Scale,
            m.Scale)
        : new ScreenInfo(0, 0, 1920, 1080, 1);

    /// <summary>
    /// Position, size and scale of the display the island lives on. Any change
    /// here means the island has to be placed again.
    /// </summary>
    public static (int X, int Y, int Width, int Height, double Scale)? Key(string pref) => Target(pref) is { } m
        ? (m.Bounds.Left, m.Bounds.Top, m.Bounds.Width, m.Bounds.Height, m.Scale)
        : null;
}
