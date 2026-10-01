// Who we are, for the relay pipe name. Compiled into both coucou.exe and
// coucou-hook.exe, so the two can never disagree on it.
//
// Named pipes share one machine-wide namespace, so the SID in the name is what
// keeps two accounts on the same machine from ever meeting on `coucou-*`. The
// relay additionally checks that the process serving the pipe really is us.

using System.Security.Principal;

namespace Coucou;

static class RelayPipeName
{
    /// <summary>The SID of the account this process runs as, as <c>S-1-5-21-…</c>.</summary>
    public static string? CurrentUserSid()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.User?.Value;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// <c>coucou-&lt;sid&gt;</c>. Falls back to the user name only if the SID cannot
    /// be read at all, which should not happen.
    /// </summary>
    public static string Name =>
        "coucou-" + (CurrentUserSid() ?? Environment.GetEnvironmentVariable("USERNAME") ?? "user");

    /// <summary><c>\\.\pipe\coucou-&lt;sid&gt;</c>.</summary>
    public static string Path => @"\\.\pipe\" + Name;
}
