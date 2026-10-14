using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FiscalApi.Cli.Credentials.MacOS;

/// <summary>
/// CoreFoundation (CFBase.h, CFString.h, CFData.h, CFDictionary.h) con <see cref="LibraryImportAttribute"/>.
/// <c>CFIndex</c> es <c>signed long</c>: 64 bits en macOS, por eso <see cref="nint"/>.
/// </summary>
[SupportedOSPlatform("macos")]
internal static unsafe partial class CoreFoundation
{
    private const string Library = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    private const uint StringEncodingUtf8 = 0x08000100;

    private static readonly nint Handle = NativeLibrary.Load(Library);

    /// <summary>Retiene y libera llaves y valores como objetos CF: el diccionario no depende de nuestras referencias.</summary>
    private static readonly nint TypeDictionaryKeyCallBacks = NativeLibrary.GetExport(Handle, "kCFTypeDictionaryKeyCallBacks");

    private static readonly nint TypeDictionaryValueCallBacks = NativeLibrary.GetExport(Handle, "kCFTypeDictionaryValueCallBacks");

    public static readonly nint BooleanTrue = *(nint*)NativeLibrary.GetExport(Handle, "kCFBooleanTrue");

    public static CFHandle CreateString(string value) =>
        CFHandle.Owned(CFStringCreateWithCString(allocator: 0, value, StringEncodingUtf8));

    /// <summary>CoreFoundation copia los bytes: quien llama puede borrar los suyos en cuanto vuelve.</summary>
    public static CFHandle CreateData(ReadOnlySpan<byte> bytes)
    {
        fixed (byte* pointer = bytes)
        {
            return CFHandle.Owned(CFDataCreate(allocator: 0, pointer, bytes.Length));
        }
    }

    /// <summary>Los bytes viven mientras viva <paramref name="data"/>.</summary>
    public static ReadOnlySpan<byte> GetBytes(CFHandle data) =>
        new(CFDataGetBytePtr(data.DangerousGetHandle()), checked((int)CFDataGetLength(data.DangerousGetHandle())));

    public static CFHandle CreateDictionary(ReadOnlySpan<nint> keys, ReadOnlySpan<nint> values)
    {
        fixed (nint* keyPointers = keys)
        fixed (nint* valuePointers = values)
        {
            return CFHandle.Owned(CFDictionaryCreate(
                allocator: 0, keyPointers, valuePointers, keys.Length, TypeDictionaryKeyCallBacks, TypeDictionaryValueCallBacks));
        }
    }

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint CFStringCreateWithCString(nint allocator, string value, uint encoding);

    [LibraryImport(Library)]
    private static partial nint CFDataCreate(nint allocator, byte* bytes, nint length);

    [LibraryImport(Library)]
    private static partial byte* CFDataGetBytePtr(nint data);

    [LibraryImport(Library)]
    private static partial nint CFDataGetLength(nint data);

    [LibraryImport(Library)]
    private static partial nint CFDictionaryCreate(
        nint allocator, nint* keys, nint* values, nint count, nint keyCallBacks, nint valueCallBacks);

    [LibraryImport(Library)]
    internal static partial void CFRelease(nint value);
}

/// <summary>
/// Un objeto de CoreFoundation que nos pertenece (regla "Create/Copy"): se libera una sola vez con <c>CFRelease</c>,
/// aun si hay una excepción.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class CFHandle : SafeHandle
{
    private CFHandle(nint handle)
        : base(invalidHandleValue: 0, ownsHandle: true) => SetHandle(handle);

    public override bool IsInvalid => handle == 0;

    /// <exception cref="InvalidOperationException">CoreFoundation devolvió NULL (sin memoria o sin resultado).</exception>
    public static CFHandle Owned(nint handle) =>
        handle != 0 ? new CFHandle(handle) : throw new InvalidOperationException("CoreFoundation devolvió un objeto nulo.");

    protected override bool ReleaseHandle()
    {
        CoreFoundation.CFRelease(handle);
        return true;
    }
}

/// <summary>Keychain Services de Security.framework (SecItem.h y SecBase.h).</summary>
[SupportedOSPlatform("macos")]
internal static unsafe partial class SecurityFramework
{
    private const string Library = "/System/Library/Frameworks/Security.framework/Security";

    public const int Success = 0;
    public const int NotAvailable = -25291;
    public const int NoSuchKeychain = -25294;
    public const int ItemNotFound = -25300;
    public const int NoDefaultKeychain = -25307;
    public const int InteractionNotAllowed = -25308;

    private static readonly nint Handle = NativeLibrary.Load(Library);

    public static readonly nint Class = Constant("kSecClass");
    public static readonly nint ClassGenericPassword = Constant("kSecClassGenericPassword");
    public static readonly nint AttrService = Constant("kSecAttrService");
    public static readonly nint AttrAccount = Constant("kSecAttrAccount");
    public static readonly nint AttrLabel = Constant("kSecAttrLabel");
    public static readonly nint ValueData = Constant("kSecValueData");
    public static readonly nint ReturnData = Constant("kSecReturnData");
    public static readonly nint MatchLimit = Constant("kSecMatchLimit");
    public static readonly nint MatchLimitOne = Constant("kSecMatchLimitOne");

    /// <param name="result">Retenido (CF_RETURNS_RETAINED): se libera con <c>CFRelease</c>.</param>
    [LibraryImport(Library)]
    public static partial int SecItemCopyMatching(nint query, nint* result);

    [LibraryImport(Library)]
    public static partial int SecItemAdd(nint attributes, nint* result);

    [LibraryImport(Library)]
    public static partial int SecItemUpdate(nint query, nint attributesToUpdate);

    [LibraryImport(Library)]
    public static partial int SecItemDelete(nint query);

    // Las constantes de Security.framework son variables CFStringRef exportadas: GetExport da su dirección.
    private static nint Constant(string name) => *(nint*)NativeLibrary.GetExport(Handle, name);
}
