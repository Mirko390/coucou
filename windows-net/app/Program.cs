// Coucou runs without a console window: Mochi and the tray icon are the whole UI.
//
//   coucou.exe          pages from wwwroot\ next to the exe
//   coucou.exe --dev    pages from the Vite dev server (npm run dev in web/)

namespace Coucou;

static class Program
{
    const string DevServer = "http://127.0.0.1:1420";

    [STAThread]
    static void Main(string[] args)
    {
        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            SingleInstance.WakeRunningInstance();
            return;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Line($"ui error: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Line($"fatal: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Line($"background error: {e.Exception.InnerException?.Message ?? e.Exception.Message}");
            e.SetObserved();
        };

        ApplicationConfiguration.Initialize();
        Application.Run(new CoucouApp(instance, args.Contains("--dev") ? DevServer : null));
    }
}
