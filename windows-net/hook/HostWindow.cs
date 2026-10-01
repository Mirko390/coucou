// Which window the session lives in — Claude Desktop, Windows Terminal, VS Code,
// Visual Studio — so the island can bring it forward when Claude is waiting for
// an answer it cannot give itself.
//
// Found by walking up from this process: the first ancestor that owns a visible,
// titled, top-level window is the one the person is working in. Only done for
// the events that need it (see HookPayload.Read), never for every tool call.

using System.Runtime.InteropServices;

namespace Coucou.Hook;

static unsafe partial class HostWindow
{
    /// <summary>Ancestors that own windows but are never "where the session is".</summary>
    static readonly string[] NotAHost = ["explorer.exe", "svchost.exe", "sihost.exe", "RuntimeBroker.exe", "services.exe", "winlogon.exe"];

    const int MaxDepth = 12;

    /// <returns>The host window handle, or null when none could be found.</returns>
    public static long? Find()
    {
        try
        {
            var processes = Snapshot();
            var pid = (uint)Environment.ProcessId;
            for (var depth = 0; depth < MaxDepth && processes.TryGetValue(pid, out var self); depth++)
            {
                pid = self.Parent;
                if (pid == 0 || !processes.TryGetValue(pid, out var parent)) break;
                if (NotAHost.Contains(parent.Exe, StringComparer.OrdinalIgnoreCase)) break;
                if (MainWindowOf(pid) is { } hwnd) return hwnd;
            }
            // A classic console (conhost) belongs to no ancestor: ask for it directly.
            var console = GetConsoleWindow();
            return console != 0 && IsWindowVisible(console) ? (long)console : null;
        }
        catch
        {
            return null;
        }
    }

    static Dictionary<uint, (uint Parent, string Exe)> Snapshot()
    {
        var result = new Dictionary<uint, (uint, string)>();
        var snapshot = CreateToolhelp32Snapshot(SnapProcess, 0);
        if (snapshot == -1) return result;
        try
        {
            var entry = new ProcessEntry { Size = (uint)sizeof(ProcessEntry) };
            for (var ok = Process32FirstW(snapshot, &entry); ok; ok = Process32NextW(snapshot, &entry))
            {
                result[entry.ProcessId] = (entry.ParentProcessId, new string(entry.ExeFile));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }
        return result;
    }

    struct Search
    {
        public uint Pid;
        public nint Found;
    }

    static long? MainWindowOf(uint pid)
    {
        var search = new Search { Pid = pid };
        EnumWindows(&Visit, (nint)(&search));
        return search.Found != 0 ? (long)search.Found : null;
    }

    [UnmanagedCallersOnly]
    static int Visit(nint hwnd, nint data)
    {
        var search = (Search*)data;
        GetWindowThreadProcessId(hwnd, out var owner);
        if (owner != search->Pid || !IsWindowVisible(hwnd)) return 1;
        if (GetWindow(hwnd, GwOwner) != 0 || GetWindowTextLengthW(hwnd) == 0) return 1;
        if ((GetWindowLongPtrW(hwnd, GwlExStyle) & WsExToolWindow) != 0) return 1;
        search->Found = hwnd;
        return 0;
    }

    const uint SnapProcess = 0x2;
    const uint GwOwner = 4;
    const int GwlExStyle = -20;
    const long WsExToolWindow = 0x80;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public nint DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityBase;
        public uint Flags;
        public fixed char ExeFile[260];
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint CreateToolhelp32Snapshot(uint flags, uint processId);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Process32FirstW(nint snapshot, ProcessEntry* entry);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Process32NextW(nint snapshot, ProcessEntry* entry);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    [LibraryImport("kernel32.dll")]
    private static partial nint GetConsoleWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(delegate* unmanaged<nint, nint, int> callback, nint data);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial nint GetWindow(nint hwnd, uint command);

    [LibraryImport("user32.dll")]
    private static partial int GetWindowTextLengthW(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial nint GetWindowLongPtrW(nint hwnd, int index);
}
