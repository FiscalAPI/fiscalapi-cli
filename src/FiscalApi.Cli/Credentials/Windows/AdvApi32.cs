using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

namespace FiscalApi.Cli.Credentials.Windows;

[SupportedOSPlatform("windows")]
internal static unsafe partial class AdvApi32
{
    private const string Library = "advapi32.dll";

    [StructLayout(LayoutKind.Sequential)]
    internal struct CREDENTIALW
    {
        public uint Flags;
        public uint Type;
        public char* TargetName;
        public char* Comment;
        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public byte* CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public void* Attributes;
        public char* TargetAlias;
        public char* UserName;
    }

    [LibraryImport(Library, EntryPoint = "CredReadW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CredReadW(string targetName, uint type, uint flags, CREDENTIALW** credential);

    [LibraryImport(Library, EntryPoint = "CredWriteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CredWriteW(CREDENTIALW* credential, uint flags);

    [LibraryImport(Library, EntryPoint = "CredDeleteW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CredDeleteW(string targetName, uint type, uint flags);

    [LibraryImport(Library, EntryPoint = "CredFree")]
    internal static partial void CredFree(void* buffer);
}
