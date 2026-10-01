// One Coucou per user session. Launching it again (Start menu, a second click)
// opens the island of the one already running instead of starting a second.

namespace Coucou;

sealed class SingleInstance : IDisposable
{
    static string Suffix => RelayPipeName.CurrentUserSid() ?? Environment.UserName;

    readonly Mutex mutex;
    readonly EventWaitHandle wake;
    RegisteredWaitHandle? registration;

    SingleInstance(Mutex mutex, EventWaitHandle wake)
    {
        this.mutex = mutex;
        this.wake = wake;
    }

    /// <summary>The instance lock, or null when another Coucou already holds it.</summary>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, $@"Local\Coucou-{Suffix}", out var created);
        if (!created)
        {
            mutex.Dispose();
            return null;
        }
        var wake = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\Coucou-open-{Suffix}");
        return new SingleInstance(mutex, wake);
    }

    /// <summary>Asks the running instance to open its island.</summary>
    public static void WakeRunningInstance()
    {
        try
        {
            using var wake = EventWaitHandle.OpenExisting($@"Local\Coucou-open-{Suffix}");
            wake.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // It is still starting up, or just quit.
        }
    }

    /// <summary>Calls <paramref name="onWake"/> (on a pool thread) whenever a second launch asks.</summary>
    public void OnWake(Action onWake) =>
        registration = ThreadPool.RegisterWaitForSingleObject(wake, (_, _) => onWake(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        registration?.Unregister(null);
        wake.Dispose();
        mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
