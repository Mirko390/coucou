// A 16 ms sleep that really is 16 ms.
//
// Thread.Sleep rounds up to the system tick (15.6 ms by default), so Sleep(16)
// lands on 31 ms and the cursor poll would run at 32 Hz. A high-resolution
// waitable timer is precise without changing the machine-wide timer resolution.

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Coucou.Native;

sealed partial class PreciseSleep : IDisposable
{
    const uint CreateWaitableTimerHighResolution = 0x0000_0002;
    const uint TimerAllAccess = 0x001F_0003;
    const uint Infinite = 0xFFFF_FFFF;

    readonly SafeWaitHandle? timer;

    public PreciseSleep()
    {
        var handle = CreateWaitableTimerExW(0, 0, CreateWaitableTimerHighResolution, TimerAllAccess);
        // Older than Windows 10 1803: fall back to the coarse sleep.
        if (handle.IsInvalid) handle.Dispose();
        else timer = handle;
    }

    public void Sleep(TimeSpan duration)
    {
        if (timer is null)
        {
            Thread.Sleep(duration);
            return;
        }
        // Negative = relative, in 100 ns units.
        var due = -duration.Ticks;
        if (!SetWaitableTimer(timer, in due, 0, 0, 0, false))
        {
            Thread.Sleep(duration);
            return;
        }
        WaitForSingleObject(timer, Infinite);
    }

    public void Dispose() => timer?.Dispose();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeWaitHandle CreateWaitableTimerExW(nint attributes, nint name, uint flags, uint access);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWaitableTimer(
        SafeWaitHandle timer, in long dueTime, int period, nint completion, nint argument,
        [MarshalAs(UnmanagedType.Bool)] bool resume);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint WaitForSingleObject(SafeWaitHandle handle, uint milliseconds);
}
