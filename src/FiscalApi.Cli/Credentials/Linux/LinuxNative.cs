using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;

namespace FiscalApi.Cli.Credentials.Linux;

/// <summary>
/// GLib (ghash.h, gerror.h) con <see cref="LibraryImportAttribute"/>. libsecret depende de ella, así que está
/// instalada siempre que lo esté libsecret.
/// </summary>
[SupportedOSPlatform("linux")]
internal static unsafe partial class GLib
{
    private const string Library = "libglib-2.0.so.0";

    /// <summary><c>GError</c>: <c>GQuark domain</c> (guint32), <c>gint code</c> y <c>gchar *message</c>.</summary>
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

    // Lazy y no un campo estático: si GLib falta, el inicializador de tipo envolvería el DllNotFoundException en
    // un TypeInitializationException. Lazy guarda la excepción y la vuelve a lanzar tal cual en cada intento.
    private static readonly Lazy<(nint Hash, nint Equal)> StringHashFunctionPointers = new(() =>
    {
        var library = NativeLibrary.Load(Library);
        return (NativeLibrary.GetExport(library, "g_str_hash"), NativeLibrary.GetExport(library, "g_str_equal"));
    });

    /// <summary>
    /// <c>g_str_hash</c> y <c>g_str_equal</c> como punteros a función para <c>g_hash_table_new</c>: llaves de texto
    /// que se comparan por contenido, que es lo que libsecret espera de una tabla de atributos. Se cargan una vez.
    /// </summary>
    internal static (nint Hash, nint Equal) StringHashFunctions => StringHashFunctionPointers.Value;

    /// <summary>Copia el mensaje del error y lo libera.</summary>
    internal static string TakeMessage(GError* error)
    {
        var message = Utf8StringMarshaller.ConvertToManaged(error->Message) ?? string.Empty;
        ErrorFree(error);
        return message;
    }
}

/// <summary>GIO (gdbusconnection.h, gdbuserror.h): la sesión de D-Bus que usa libsecret.</summary>
[SupportedOSPlatform("linux")]
internal static unsafe partial class Gio
{
    private const string Library = "libgio-2.0.so.0";

    internal const int BusTypeSession = 2;

    // GDBusError: el nombre pedido (org.freedesktop.secrets) no existe en el bus ni se puede activar.
    internal const int DBusErrorServiceUnknown = 2;
    internal const int DBusErrorNameHasNoOwner = 3;

    /// <returns>La conexión (se libera con <see cref="GObject.Unref"/>), o 0 con <paramref name="error"/>.</returns>
    [LibraryImport(Library, EntryPoint = "g_bus_get_sync")]
    internal static partial nint BusGetSync(int busType, nint cancellable, GLib.GError** error);

    [LibraryImport(Library, EntryPoint = "g_dbus_error_quark")]
    internal static partial uint DBusErrorQuark();
}

/// <summary>GObject: libera la referencia a la conexión de D-Bus.</summary>
[SupportedOSPlatform("linux")]
internal static partial class GObject
{
    [LibraryImport("libgobject-2.0.so.0", EntryPoint = "g_object_unref")]
    internal static partial void Unref(nint instance);
}

/// <summary>
/// libsecret (secret-password.h, secret-schema.h): las variantes <c>v</c>, que reciben los atributos en un
/// <c>GHashTable</c> en lugar de argumentos variádicos, que el interop de .NET no puede llamar.
/// </summary>
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

    /// <returns>Un secreto nuevo que se libera con <see cref="PasswordFree"/>, o NULL si no hay.</returns>
    [LibraryImport(Library, EntryPoint = "secret_password_lookupv_sync")]
    internal static partial byte* PasswordLookup(nint schema, nint attributes, nint cancellable, GLib.GError** error);

    /// <returns>Distinto de 0 si borró algo.</returns>
    [LibraryImport(Library, EntryPoint = "secret_password_clearv_sync")]
    internal static partial int PasswordClear(nint schema, nint attributes, nint cancellable, GLib.GError** error);

    /// <summary>Borra la memoria del secreto (<c>egg_secure_strfree</c>) y luego la libera.</summary>
    [LibraryImport(Library, EntryPoint = "secret_password_free")]
    internal static partial void PasswordFree(byte* password);
}

/// <summary>
/// Un <c>GHashTable</c> de texto a texto con las cadenas UTF-8 que le pertenecen. La tabla no copia ni libera sus
/// llaves y valores (sin funciones de destrucción), así que viven aquí hasta <see cref="Dispose"/>.
/// </summary>
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
