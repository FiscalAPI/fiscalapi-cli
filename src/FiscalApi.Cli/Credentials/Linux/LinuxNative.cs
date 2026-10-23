using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;

namespace FiscalApi.Cli.Credentials.Linux;

[SupportedOSPlatform("linux")]
internal static unsafe partial class GLib
{
    private const string Library = "libglib-2.0.so.0";

    [StructLayout(LayoutKind.Sequential)]
    internal struct GError
    {
        public uint Domain;
        public int Code;
        public byte* Message;
    }

    [LibraryImport(Library, EntryPoint = "g_hash_table_new")]
    internal static partial nint HashTableNew(nint hashFunction, nint keyEqualFunction);

    [LibraryImport(Library, EntryPoint = "g_hash_table_insert")]
    internal static partial int HashTableInsert(nint table, nint key, nint value);

    [LibraryImport(Library, EntryPoint = "g_hash_table_unref")]
    internal static partial void HashTableUnref(nint table);

    [LibraryImport(Library, EntryPoint = "g_error_free")]
    internal static partial void ErrorFree(GError* error);

    private static readonly Lazy<(nint Hash, nint Equal)> StringHashFunctionPointers = new(() =>
    {
        var library = NativeLibrary.Load(Library);
        return (NativeLibrary.GetExport(library, "g_str_hash"), NativeLibrary.GetExport(library, "g_str_equal"));
    });

    internal static (nint Hash, nint Equal) StringHashFunctions => StringHashFunctionPointers.Value;

    internal static string TakeMessage(GError* error)
    {
        var message = Utf8StringMarshaller.ConvertToManaged(error->Message) ?? string.Empty;
        ErrorFree(error);
        return message;
    }
}

[SupportedOSPlatform("linux")]
internal static unsafe partial class Gio
{
    private const string Library = "libgio-2.0.so.0";

    internal const int BusTypeSession = 2;

    internal const int DBusErrorServiceUnknown = 2;
    internal const int DBusErrorNameHasNoOwner = 3;

    [LibraryImport(Library, EntryPoint = "g_bus_get_sync")]
    internal static partial nint BusGetSync(int busType, nint cancellable, GLib.GError** error);

    [LibraryImport(Library, EntryPoint = "g_dbus_error_quark")]
    internal static partial uint DBusErrorQuark();
}

[SupportedOSPlatform("linux")]
internal static partial class GObject
{
    [LibraryImport("libgobject-2.0.so.0", EntryPoint = "g_object_unref")]
    internal static partial void Unref(nint instance);
}

[SupportedOSPlatform("linux")]
internal static unsafe partial class LibSecret
{
    private const string Library = "libsecret-1.so.0";

    internal const int SchemaNone = 0;

    [LibraryImport(Library, EntryPoint = "secret_schema_newv", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint SchemaNew(string name, int flags, nint attributeNamesAndTypes);

    [LibraryImport(Library, EntryPoint = "secret_schema_unref")]
    internal static partial void SchemaUnref(nint schema);

    [LibraryImport(Library, EntryPoint = "secret_password_storev_sync", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int PasswordStore(
        nint schema, nint attributes, string? collection, string label, byte* password, nint cancellable, GLib.GError** error);

    [LibraryImport(Library, EntryPoint = "secret_password_lookupv_sync")]
    internal static partial byte* PasswordLookup(nint schema, nint attributes, nint cancellable, GLib.GError** error);

    [LibraryImport(Library, EntryPoint = "secret_password_clearv_sync")]
    internal static partial int PasswordClear(nint schema, nint attributes, nint cancellable, GLib.GError** error);

    [LibraryImport(Library, EntryPoint = "secret_password_free")]
    internal static partial void PasswordFree(byte* password);
}

[SupportedOSPlatform("linux")]
internal sealed unsafe class StringHashTable : IDisposable
{
    private readonly List<nint> _strings = [];

    public StringHashTable(IEnumerable<KeyValuePair<string, string?>> entries)
    {
        var (hash, equal) = GLib.StringHashFunctions;
        Handle = GLib.HashTableNew(hash, equal);
        foreach (var (key, value) in entries)
        {
            GLib.HashTableInsert(Handle, Utf8(key), value is null ? 0 : Utf8(value));
        }
    }

    public nint Handle { get; }

    public void Dispose()
    {
        GLib.HashTableUnref(Handle);
        foreach (var pointer in _strings)
        {
            Utf8StringMarshaller.Free((byte*)pointer);
        }
    }

    private nint Utf8(string value)
    {
        var pointer = (nint)Utf8StringMarshaller.ConvertToUnmanaged(value);
        _strings.Add(pointer);
        return pointer;
    }
}
