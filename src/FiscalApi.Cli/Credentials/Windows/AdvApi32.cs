using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

namespace FiscalApi.Cli.Credentials.Windows;

/// <summary>
/// Credential Manager de <c>advapi32.dll</c> (wincred.h), con <see cref="LibraryImportAttribute"/>: el marshalling
/// se genera al compilar y funciona en Native AOT. Siempre las variantes <c>W</c> (UTF-16).
/// </summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class AdvApi32
{
    private const string Library = "advapi32.dll";

    /// <summary><c>CREDENTIALW</c> de wincred.h, campo por campo y en el mismo orden (blittable).</summary>
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

    /// <summary>El resultado es un solo bloque que se libera con <see cref="CredFree"/>.</summary>
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
