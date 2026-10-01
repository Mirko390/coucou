// Opening things outside Coucou: web pages, and a Claude Code project in the
// user's editor.

using System.Diagnostics;

namespace Coucou;

static class Launcher
{
    /// <summary>Opens an http(s) URL in the default browser. Anything else is ignored.</summary>
    public static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception e)
        {
            Log.Line($"open url failed: {e.Message}");
        }
    }

    /// <summary>
    /// "Open project": the session's working folder in the editor the user picked
    /// ("visualstudio", "vscode", "explorer"), or with "auto" in Visual Studio
    /// when the folder holds a solution, VS Code otherwise, Visual Studio when
    /// there is no VS Code. Explorer when none of them is installed.
    /// </summary>
    /// <returns>True when an editor opened, false for Explorer or nothing.</returns>
    /// <remarks>
    /// No <c>cmd /C</c> anywhere near this, and never a .cmd file with the path as
    /// an argument. The path is a project folder chosen by whoever is using
    /// Claude Code, and cmd would happily read <c>&amp;</c>, <c>^</c> and <c>%</c>
    /// in a folder name as syntax — .NET does not escape arguments for batch
    /// files. Every editor is started from its own exe, with the path as a
    /// separate argument.
    /// </remarks>
    public static bool OpenProject(string? path, string editor)
    {
        var folder = string.IsNullOrEmpty(path) ? null : path;
        var solution = folder is null ? null : SolutionIn(folder);

        string[] order = editor switch
        {
            "visualstudio" => ["visualstudio"],
            "vscode" => ["vscode"],
            "explorer" => [],
            _ when solution is not null => ["visualstudio", "vscode"],
            _ => ["vscode", "visualstudio"],
        };

        foreach (var candidate in order)
        {
            var (exe, target) = candidate == "visualstudio"
                ? (VisualStudio.Value, solution ?? folder)
                : (FindVSCode(), folder);
            if (exe is not null && Start(exe, target)) return true;
        }

        if (folder is not null) Start("explorer.exe", folder);
        return false;
    }

    static bool Start(string exe, string? argument)
    {
        try
        {
            var start = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true };
            if (argument is not null) start.ArgumentList.Add(argument);
            Process.Start(start)?.Dispose();
            return true;
        }
        catch (Exception e)
        {
            Log.Line($"{Path.GetFileName(exe)} failed to start: {e.Message}");
            return false;
        }
    }

    /// <summary>The folder's one .slnx or .sln, so Visual Studio opens the solution rather than a bare folder.</summary>
    internal static string? SolutionIn(string folder)
    {
        try
        {
            var solutions = Directory.GetFiles(folder, "*.slnx").Concat(Directory.GetFiles(folder, "*.sln")).ToList();
            if (solutions.Count == 1) return solutions[0];
            // An .slnx next to the .sln it was migrated from: prefer the new one.
            var slnx = solutions.Where(s => s.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)).ToList();
            return slnx.Count == 1 ? slnx[0] : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// devenv.exe of the newest Visual Studio IDE, from vswhere. Products are
    /// named on purpose: Build Tools has no IDE, and SQL Server Management Studio
    /// is built on the same shell and would otherwise match.
    /// </summary>
    static readonly Lazy<string?> VisualStudio = new(() =>
    {
        var vswhere = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(vswhere)) return null;
        try
        {
            var start = new ProcessStartInfo(vswhere)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            foreach (var arg in new[]
            {
                "-latest", "-prerelease", "-products",
                "Microsoft.VisualStudio.Product.Enterprise",
                "Microsoft.VisualStudio.Product.Professional",
                "Microsoft.VisualStudio.Product.Community",
                "-property", "productPath",
            })
            {
                start.ArgumentList.Add(arg);
            }
            using var process = Process.Start(start);
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            var devenv = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(line => line.EndsWith("devenv.exe", StringComparison.OrdinalIgnoreCase));
            return devenv is not null && File.Exists(devenv) ? devenv : null;
        }
        catch (Exception e)
        {
            Log.Line($"vswhere failed: {e.Message}");
            return null;
        }
    });

    /// <summary>Our own <c>where code</c>: walks %PATH% against %PATHEXT%, no shell involved.</summary>
    static string? FindVSCode()
    {
        var exts = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in dirs)
        {
            foreach (var ext in exts)
            {
                string candidate;
                try { candidate = Path.Combine(dir.Trim('"'), "code" + ext.ToLowerInvariant()); }
                catch (ArgumentException) { continue; }
                if (!File.Exists(candidate)) continue;

                if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) || ext.Equals(".com", StringComparison.OrdinalIgnoreCase))
                    return candidate;

                // <install>\bin\code.cmd → <install>\Code.exe
                var exe = Path.GetFullPath(Path.Combine(dir.Trim('"'), "..", "Code.exe"));
                if (File.Exists(exe)) return exe;
            }
        }
        return null;
    }
}
