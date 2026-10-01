// Small append-only log at %LOCALAPPDATA%\Coucou\coucou.log — the Windows
// equivalent of nbLog() in HookServer.swift. Nothing leaves the machine.

using System.Globalization;

namespace Coucou;

static class Log
{
    static readonly Lock Gate = new();

    public static void Line(string message)
    {
        var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Paths.LocalDir);
                var file = new FileInfo(Paths.LogFile);
                // Keep it from growing forever: start fresh past ~1 MB.
                if (file.Exists && file.Length > 1_000_000) file.Delete();
                File.AppendAllText(file.FullName, $"{stamp} {message}\n");
            }
            catch
            {
                // A log that cannot be written must never take the app down.
            }
        }
    }
}
