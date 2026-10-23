using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FiscalApi.Cli.Credentials.MacOS;

[SupportedOSPlatform("macos")]
internal static unsafe partial class CoreFoundation
{
    private const string Library = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    private const uint StringEncodingUtf8 = 0x08000100;

    private static readonly nint Handle = NativeLibrary.Load(Library);

    private static readonly nint TypeDictionaryKeyCallBacks = NativeLibrary.GetExport(Handle, "kCFTypeDictionaryKeyCallBacks");

    private static readonly nint TypeDictionaryValueCallBacks = NativeLibrary.GetExport(Handle, "kCFTypeDictionaryValueCallBacks");

    public static readonly nint BooleanTrue = *(nint*)NativeLibrary.GetExport(Handle, "kCFBooleanTrue");

    public static CFHandle CreateString(string value) =>
        CFHandle.Owned(CFStringCreateWithCString(allocator: 0, value, StringEncodingUtf8));

    public static CFHandle CreateData(ReadOnlySpan<byte> bytes)
    {
        fixed (byte* pointer = bytes)
        {
            return CFHandle.Owned(CFDataCreate(allocator: 0, pointer, bytes.Length));
        }
    }

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

[SupportedOSPlatform("macos")]
internal sealed class CFHandle : SafeHandle
{
    private CFHandle(nint handle)
        : base(invalidHandleValue: 0, ownsHandle: true) => SetHandle(handle);

    public override bool IsInvalid => handle == 0;

    public static CFHandle Owned(nint handle) =>
        handle != 0 ? new CFHandle(handle) : throw new InvalidOperationException("CoreFoundation returned a null object.");

    protected override bool ReleaseHandle()
    {
        CoreFoundation.CFRelease(handle);
        return true;
    }
}

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

    [LibraryImport(Library)]
    public static partial int SecItemCopyMatching(nint query, nint* result);

    [LibraryImport(Library)]
    public static partial int SecItemAdd(nint attributes, nint* result);

    [LibraryImport(Library)]
    public static partial int SecItemUpdate(nint query, nint attributesToUpdate);

    [LibraryImport(Library)]
    public static partial int SecItemDelete(nint query);

    private static nint Constant(string name) => *(nint*)NativeLibrary.GetExport(Handle, name);
}
