// The client end of the relay pipe, and the little bit of Win32 it needs.
//
// Named pipes live in a machine-wide namespace, so `\\.\pipe\coucou-<name>` can
// be created by *any* account that gets there first. Two defences, both cheap:
// the pipe name carries our SID, and once connected we check the server process
// really belongs to us before sending anything.

using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Coucou.Hook;

static partial class RelayClient
{
    /// <summary>Budget for getting a pipe connection. Beyond this Claude Code wins, always.</summary>
    static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Every instance is serving someone else right now. This is the one error
    /// worth retrying: the server exists and a slot will free up.
    /// </summary>
    const int ErrorPipeBusy = 231;

    /// <summary>
    /// Opens the pipe. Retries only while the server is busy: any other error
    /// means there is nothing to talk to, and waiting would only delay Claude Code.
    /// </summary>
    /// <remarks>
    /// Not <see cref="NamedPipeClientStream.Connect(int)"/>: it keeps polling for
    /// a pipe that does not exist, which would cost every hook event the full
    /// timeout whenever Coucou is closed.
    /// </remarks>
    public static NamedPipeClientStream? Connect()
    {
        var path = RelayPipeName.Path;
        var clock = Stopwatch.StartNew();
        while (true)
        {
            // SECURITY_IDENTIFICATION: the server may learn who we are, never act as us.
            var handle = CreateFileW(
                path,
                GenericRead | GenericWrite,
                0,
                0,
                OpenExisting,
                SecuritySqosPresent | SecurityIdentification,
                0);
            if (!handle.IsInvalid)
            {
                var pipe = new SafePipeHandle(handle.DangerousGetHandle(), ownsHandle: true);
                handle.SetHandleAsInvalid();
                // Somebody else's server on our pipe name gets nothing from us.
                if (ServerIsSameUser(pipe))
                    return new NamedPipeClientStream(PipeDirection.InOut, isAsync: false, isConnected: true, pipe);
                pipe.Dispose();
                return null;
            }
            handle.Dispose();
            if (Marshal.GetLastPInvokeError() != ErrorPipeBusy || clock.Elapsed >= ConnectTimeout)
                return null;
            Thread.Sleep(15);
        }
    }

    /// <summary>
    /// True when the process serving <paramref name="pipe"/> runs as the same user we do.
    /// </summary>
    /// <remarks>
    /// A failure to answer is treated as "not ours": refusing to talk to a pipe
    /// we cannot vouch for costs one hook event, while trusting it could hand
    /// another account on this machine the contents of every tool call.
    /// </remarks>
    static bool ServerIsSameUser(SafePipeHandle pipe)
    {
        var mine = RelayPipeName.CurrentUserSid();
        if (mine is null) return false;
        if (!GetNamedPipeServerProcessId(pipe, out var pid) || pid == 0) return false;

        using var process = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (process.IsInvalid) return false;
        if (!OpenProcessToken(process, TokenQuery, out var token)) return false;
        using (token)
        {
            try
            {
                using var identity = new WindowsIdentity(token.DangerousGetHandle());
                return identity.User?.Value == mine;
            }
            catch
            {
                return false;
            }
        }
    }

    const uint GenericRead = 0x8000_0000;
    const uint GenericWrite = 0x4000_0000;
    const uint OpenExisting = 3;
    const uint SecuritySqosPresent = 0x0010_0000;
    const uint SecurityIdentification = 0x0001_0000;
    const uint ProcessQueryLimitedInformation = 0x1000;
    const uint TokenQuery = 0x0008;

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(
        string fileName, uint access, uint share, nint security, uint creation, uint flags, nint template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(
        uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);
}
