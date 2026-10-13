using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace FiscalApi.Cli.Credentials.Linux;

/// <summary>
/// Secret Service de freedesktop (gnome-keyring, KWallet) por medio de libsecret: un secreto por host en la
/// colección por omisión, con los atributos <c>service</c> y <c>host</c> del esquema <c>com.fiscalapi.cli</c>.
/// Se ve en Seahorse ("Contraseñas y claves"). Sin libsecret, sin sesión de D-Bus o sin el servicio
/// <c>org.freedesktop.secrets</c> no hay almacén; con ellos, cualquier otro error es un almacén bloqueado o que
/// no respondió.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed unsafe class LibSecretCredentialStore(string service) : ICredentialStore
{
    private const string SchemaName = "com.fiscalapi.cli";
    private const string ServiceAttribute = "service";
    private const string HostAttribute = "host";

    private delegate T SecretDecoder<out T>(ReadOnlySpan<byte> secret, string host);

    // libsecret no tiene una consulta sin secreto: se busca la entrada y se libera sin decodificarla, así una
    // entrada ilegible no impide que login la reemplace.
    public void EnsureAvailable(string host) => ReadSecret(host, static (_, _) => true);

    public ApiCredentials? Read(string host) => ReadSecret<ApiCredentials>(host, StoredCredentialsFormat.Decode);

    public void Save(string host, ApiCredentials credentials)
    {
        var secret = StoredCredentialsFormat.Encode(credentials);
        // libsecret recibe el secreto como texto UTF-8 terminado en NUL.
        var terminated = new byte[secret.Length + 1];
        try
        {
            secret.CopyTo(terminated, 0);
            using var query = Open(host);
            GLib.GError* error = null;
            fixed (byte* password = terminated)
            {
                // Colección NULL: la de omisión, que persiste entre sesiones (la de sesión se pierde al salir).
                LibSecret.PasswordStore(
                    query.Schema, query.Attributes, collection: null, $"FiscalAPI CLI ({host})", password, cancellable: 0, &error);
            }

            ThrowIfFailed(error);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
            CryptographicOperations.ZeroMemory(terminated);
        }
    }

    public bool Delete(string host)
    {
        using var query = Open(host);
        GLib.GError* error = null;
        var removed = LibSecret.PasswordClear(query.Schema, query.Attributes, cancellable: 0, &error);
        ThrowIfFailed(error);
        return removed != 0;
    }

    /// <returns>Lo que devuelve <paramref name="decode"/>, o el valor por omisión si no hay entrada.</returns>
    private T? ReadSecret<T>(string host, SecretDecoder<T> decode)
    {
        using var query = Open(host);
        GLib.GError* error = null;
        var password = LibSecret.PasswordLookup(query.Schema, query.Attributes, cancellable: 0, &error);
        ThrowIfFailed(error);
        if (password == null)
        {
            return default;
        }

        try
        {
            return decode(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(password), host);
        }
        finally
        {
            // Borra la memoria del secreto antes de liberarla.
            LibSecret.PasswordFree(password);
        }
    }

    private Query Open(string host)
    {
        try
        {
            EnsureSessionBus();
            return new Query(service, host);
        }
        catch (DllNotFoundException)
        {
            throw new CredentialStoreUnavailableException("No se encontró libsecret-1.so.0 (paquete libsecret-1-0 o libsecret).");
        }
    }

    /// <summary>
    /// Sin sesión de D-Bus (WSL, SSH, un contenedor) no hay Secret Service posible. Se comprueba aparte porque el
    /// error que deja libsecret en ese caso es genérico (G_IO_ERROR_FAILED) y no se distingue de otras fallas.
    /// </summary>
    private static void EnsureSessionBus()
    {
        GLib.GError* error = null;
        var connection = Gio.BusGetSync(Gio.BusTypeSession, cancellable: 0, &error);
        if (connection == 0)
        {
            throw new CredentialStoreUnavailableException($"D-Bus: {GLib.TakeMessage(error)}");
        }

        GObject.Unref(connection);
    }

    /// <summary>
    /// Con la sesión de D-Bus ya comprobada, "el servicio no existe" (ServiceUnknown, NameHasNoOwner) es que no hay
    /// almacén. Cualquier otro error (colección bloqueada, desbloqueo cancelado, sin respuesta) es un almacén que
    /// existe y no se pudo usar. El mensaje es de D-Bus o del servicio; nunca trae el secreto.
    /// </summary>
    private static void ThrowIfFailed(GLib.GError* error)
    {
        if (error == null)
        {
            return;
        }

        var serviceMissing = error->Domain == Gio.DBusErrorQuark()
            && error->Code is Gio.DBusErrorServiceUnknown or Gio.DBusErrorNameHasNoOwner;
        var detail = $"Secret Service: {GLib.TakeMessage(error)}";
        throw serviceMissing ? new CredentialStoreUnavailableException(detail) : CredentialStoreException.Locked(detail);
    }

    /// <summary>El esquema y los atributos de una entrada; libera lo nativo de ambos al terminar la operación.</summary>
    private sealed class Query : IDisposable
    {
        private readonly StringHashTable _attributes;

        public Query(string service, string host)
        {
            _attributes = new StringHashTable([new(ServiceAttribute, service), new(HostAttribute, host)]);
            try
            {
                // Los valores NULL son GINT_TO_POINTER(SECRET_SCHEMA_ATTRIBUTE_STRING): los dos atributos son texto.
                using var attributeTypes = new StringHashTable([new(ServiceAttribute, null), new(HostAttribute, null)]);
                Schema = LibSecret.SchemaNew(SchemaName, LibSecret.SchemaNone, attributeTypes.Handle);
            }
            catch
            {
                _attributes.Dispose();
                throw;
            }
        }

        public nint Schema { get; }

        public nint Attributes => _attributes.Handle;

        public void Dispose()
        {
            LibSecret.SchemaUnref(Schema);
            _attributes.Dispose();
        }
    }
}
