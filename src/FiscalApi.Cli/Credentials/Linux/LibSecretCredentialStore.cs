using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace FiscalApi.Cli.Credentials.Linux;

[SupportedOSPlatform("linux")]
internal sealed unsafe class LibSecretCredentialStore(string service) : ICredentialStore
{
    private const string StoreName = "Secret Service";
    private const string SchemaName = "com.fiscalapi.cli";
    private const string ServiceAttribute = "service";
    private const string HostAttribute = "host";

    private delegate T SecretDecoder<out T>(ReadOnlySpan<byte> secret, string host);

    public void EnsureAvailable(string host) => ReadSecret(host, static (_, _) => true);

    public ApiCredentials? Read(string host) => ReadSecret<ApiCredentials>(host, StoredCredentialsFormat.Decode);

    public void Save(string host, ApiCredentials credentials)
    {
        var secret = StoredCredentialsFormat.Encode(credentials);
        var terminated = new byte[secret.Length + 1];
        try
        {
            secret.CopyTo(terminated, 0);
            using var query = Open(host);
            GLib.GError* error = null;
            fixed (byte* password = terminated)
            {
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
            throw new CredentialStoreUnavailableException($"{StoreName}: libsecret-1.so.0 not found. Install libsecret-1-0 or libsecret.");
        }
    }

    private static void EnsureSessionBus()
    {
        GLib.GError* error = null;
        var connection = Gio.BusGetSync(Gio.BusTypeSession, cancellable: 0, &error);
        if (connection == 0)
        {
            throw new CredentialStoreUnavailableException($"{StoreName}: no D-Bus session bus. {GLib.TakeMessage(error)}");
        }

        GObject.Unref(connection);
    }

    private static void ThrowIfFailed(GLib.GError* error)
    {
        if (error == null)
        {
            return;
        }

        var serviceMissing = error->Domain == Gio.DBusErrorQuark()
            && error->Code is Gio.DBusErrorServiceUnknown or Gio.DBusErrorNameHasNoOwner;
        var detail = $"{StoreName}: {GLib.TakeMessage(error)}";
        throw serviceMissing ? new CredentialStoreUnavailableException(detail) : CredentialStoreException.Locked(detail);
    }

    private sealed class Query : IDisposable
    {
        private readonly StringHashTable _attributes;

        public Query(string service, string host)
        {
            _attributes = new StringHashTable([new(ServiceAttribute, service), new(HostAttribute, host)]);
            try
            {
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
