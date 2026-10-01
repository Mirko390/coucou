// Copies coucou-hook.exe into %LOCALAPPDATA%\Coucou\bin on launch.
//
// It ships next to coucou.exe (the build puts the AOT-published relay there,
// and the installer installs it there). The copy in bin\ is the one
// ~/.claude/settings.json points at, so it survives the app being moved,
// reinstalled or updated — and it is the same path the Tauri build used, so
// hooks installed by it keep working.

namespace Coucou.Hooks;

static class HookExe
{
    public static void Ensure()
    {
        var dest = Paths.HookExe;
        var source = Path.Combine(AppContext.BaseDirectory, "coucou-hook.exe");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        }
        catch (Exception)
        {
            return;
        }

        // Getting this wrong is silent and fatal for the hooks, so say so loudly.
        if (!File.Exists(source))
        {
            Log.Line($"coucou-hook.exe not found — Claude Code hooks cannot work. Looked in: {source}");
            return;
        }

        var src = new FileInfo(source);
        var dst = new FileInfo(dest);
        if (dst.Exists && src.Length == dst.Length && src.LastWriteTimeUtc == dst.LastWriteTimeUtc) return;

        try
        {
            // File.Copy keeps the timestamp, which is what the check above relies on.
            File.Copy(source, dest, overwrite: true);
        }
        catch (Exception e)
        {
            // A hook may be running right now and hold the file open; keeping
            // the old copy is fine, it speaks the same protocol.
            if (!File.Exists(dest)) Log.Line($"could not install coucou-hook.exe: {e.Message}");
        }
    }
}
