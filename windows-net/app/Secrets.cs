// API keys live in the Windows Credential Manager, never on disk and never in
// the pages — the island can only ask whether a key is present.
//
// Stored exactly as the Tauri build's `keyring` crate stored them (generic
// credential, target "<key>.fr.louisraille.coucou", UTF-16 blob), so keys typed
// into the old build keep working.

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Coucou;

static unsafe partial class Secrets
{
    const string Service = "fr.louisraille.coucou";

    /// <summary>Every key Coucou may store. Anything outside this list is refused.</summary>
    public static readonly IReadOnlyList<string> KnownKeys =
    [
        "anthropic-api-key",
        "n8n-url",
        "n8n-api-key",
        "vercel-token",
        "github-token",
        "stripe-api-key",
        "resend-api-key",
        "notion-api-key",
        "calcom-api-key",
    ];

    static string? Target(string key) => KnownKeys.Contains(key) ? $"{key}.{Service}" : null;

    public static string? Get(string key)
    {
        if (Target(key) is not { } target) return null;
        if (!CredReadW(target, CredTypeGeneric, 0, out var found)) return null;
        try
        {
            var size = (int)found->CredentialBlobSize;
            if (size == 0 || size % 2 != 0) return null;
            var value = new string((char*)found->CredentialBlob, 0, size / 2);
            return value.Length == 0 ? null : value;
        }
        finally
        {
            CredFree(found);
        }
    }

    public static bool Present(string key) => Get(key) is not null;

    public static void Set(string key, string value)
    {
        var target = Target(key) ?? throw new UserFacingException($"unknown key {key}");
        if (value.Length == 0)
        {
            // An empty value means "no key"; whether one was there does not matter.
            try { Delete(target); }
            catch (UserFacingException) { }
            return;
        }

        fixed (char* targetName = target)
        fixed (char* userName = key)
        fixed (char* comment = "Coucou")
        fixed (char* blob = value)
        {
            var credential = new Credential
            {
                Type = CredTypeGeneric,
                TargetName = targetName,
                UserName = userName,
                Comment = comment,
                CredentialBlobSize = (uint)(value.Length * sizeof(char)),
                CredentialBlob = (byte*)blob,
                Persist = CredPersistEnterprise,
            };
            if (!CredWriteW(&credential, 0))
                throw new UserFacingException(new Win32Exception(Marshal.GetLastPInvokeError()).Message);
        }
    }

    public static void Clear(string key)
    {
        var target = Target(key) ?? throw new UserFacingException($"unknown key {key}");
        Delete(target);
    }

    static void Delete(string target)
    {
        if (CredDeleteW(target, CredTypeGeneric, 0)) return;
        var error = Marshal.GetLastPInvokeError();
        if (error != ErrorNotFound) throw new UserFacingException(new Win32Exception(error).Message);
    }

    const uint CredTypeGeneric = 1;
    const uint CredPersistEnterprise = 3;
    const int ErrorNotFound = 1168;

    [StructLayout(LayoutKind.Sequential)]
    struct Credential
    {
        public uint Flags;
        public uint Type;
        public char* TargetName;
        public char* Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public byte* CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public nint Attributes;
        public char* TargetAlias;
        public char* UserName;
    }

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredReadW(string target, uint type, uint flags, out Credential* credential);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredWriteW(Credential* credential, uint flags);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredDeleteW(string target, uint type, uint flags);

    [LibraryImport("advapi32.dll")]
    private static partial void CredFree(void* buffer);
}
