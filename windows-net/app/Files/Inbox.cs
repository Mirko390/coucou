// Dropped files are copied into %LOCALAPPDATA%\Coucou\inbox so the original is
// never touched and the copy survives the drag source going away.
// The inbox is swept of anything older than a week, as on macOS.

namespace Coucou;

sealed record DroppedFile(string Name, string Path, long Size);

static class Inbox
{
    static readonly TimeSpan KeepFor = TimeSpan.FromDays(7);

    public static DroppedFile Ingest(string source, string inboxDir)
    {
        if (Directory.Exists(source)) throw new UserFacingException(Loc.T("Folders can't be dropped yet."));
        FileInfo src;
        try
        {
            src = new FileInfo(source);
            if (!src.Exists) throw new FileNotFoundException("file not found", source);
        }
        catch (Exception e) when (e is not UserFacingException)
        {
            throw new UserFacingException(Loc.T("cannot read {0}: {1}", source, e.Message));
        }

        Directory.CreateDirectory(inboxDir);
        var name = src.Name.Length > 0 ? src.Name : "file";

        var dest = Path.Combine(inboxDir, name);
        if (File.Exists(dest))
        {
            var stem = Path.GetFileNameWithoutExtension(name);
            var ext = Path.GetExtension(name);
            for (var i = 2; i < 1000; i++)
            {
                var candidate = Path.Combine(inboxDir, $"{stem} ({i}){ext}");
                if (!File.Exists(candidate))
                {
                    dest = candidate;
                    break;
                }
            }
        }

        try
        {
            File.Copy(source, dest, overwrite: true);
        }
        catch (Exception e)
        {
            throw new UserFacingException(Loc.T("cannot copy: {0}", e.Message));
        }
        // File.Copy carries the source's timestamps across, so a file last edited
        // three years ago would arrive already older than the sweep window and be
        // deleted on the spot. The inbox ages from when *we* copied it.
        try { File.SetLastWriteTimeUtc(dest, DateTime.UtcNow); }
        catch (IOException) { }
        Sweep(inboxDir);

        return new DroppedFile(name, dest, src.Length);
    }

    /// <summary>
    /// Drops anything copied here more than a week ago. <see cref="Ingest"/>
    /// stamps every copy with the time it landed, so this really is the age of
    /// the copy and not the age of whatever the user happened to drag in.
    /// </summary>
    static void Sweep(string dir)
    {
        try
        {
            var now = DateTime.UtcNow;
            foreach (var file in new DirectoryInfo(dir).EnumerateFiles())
            {
                if (now - file.LastWriteTimeUtc > KeepFor)
                {
                    try { file.Delete(); }
                    catch (IOException) { }
                }
            }
        }
        catch (IOException) { }
    }
}
