using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace FiscalApi.Cli.Credentials.MacOS;

/// <summary>
/// Llavero de macOS: una contraseña genérica (<c>kSecClassGenericPassword</c>) por host, con servicio
/// <c>fiscalapi-cli</c> y cuenta igual al host. Se ve en "Acceso a Llaveros".
/// Usa el llavero de sesión del usuario y no el de protección de datos (<c>kSecUseDataProtectionKeychain</c>),
/// que exige firmar el binario con el entitlement <c>keychain-access-groups</c>.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed unsafe class KeychainCredentialStore(string service) : ICredentialStore
{
    // Sin kSecReturnData la consulta solo dice si la entrada existe: no lee el secreto ni pide permiso para leerlo.
    public void EnsureAvailable(string host)
    {
        using var serviceName = CoreFoundation.CreateString(service);
        using var account = CoreFoundation.CreateString(host);
        using var query = CoreFoundation.CreateDictionary(
            [SecurityFramework.Class, SecurityFramework.AttrService, SecurityFramework.AttrAccount],
            [SecurityFramework.ClassGenericPassword, Raw(serviceName), Raw(account)]);

        var status = SecurityFramework.SecItemCopyMatching(Raw(query), result: null);
        if (status != SecurityFramework.ItemNotFound)
        {
            ThrowIfFailed(status, "consultar");
        }
    }

    public ApiCredentials? Read(string host)
    {
        using var serviceName = CoreFoundation.CreateString(service);
        using var account = CoreFoundation.CreateString(host);
        using var query = CoreFoundation.CreateDictionary(
            [SecurityFramework.Class, SecurityFramework.AttrService, SecurityFramework.AttrAccount, SecurityFramework.ReturnData, SecurityFramework.MatchLimit],
            [SecurityFramework.ClassGenericPassword, Raw(serviceName), Raw(account), CoreFoundation.BooleanTrue, SecurityFramework.MatchLimitOne]);

        nint result = 0;
        var status = SecurityFramework.SecItemCopyMatching(Raw(query), &result);
        if (status == SecurityFramework.ItemNotFound)
        {
            return null;
        }

        ThrowIfFailed(status, "leer");
        // Un CFData inmutable no se puede borrar en su lugar: se libera en cuanto se decodifica.
        using var data = CFHandle.Owned(result);
        return StoredCredentialsFormat.Decode(CoreFoundation.GetBytes(data), host);
    }

    public void Save(string host, ApiCredentials credentials)
    {
        using var serviceName = CoreFoundation.CreateString(service);
        using var account = CoreFoundation.CreateString(host);
        using var data = CreateSecretData(credentials);
        using var query = CoreFoundation.CreateDictionary(
            [SecurityFramework.Class, SecurityFramework.AttrService, SecurityFramework.AttrAccount],
            [SecurityFramework.ClassGenericPassword, Raw(serviceName), Raw(account)]);
        using var update = CoreFoundation.CreateDictionary([SecurityFramework.ValueData], [Raw(data)]);

        // Reemplazar primero conserva el permiso de acceso que la persona ya dio a la entrada.
        var status = SecurityFramework.SecItemUpdate(Raw(query), Raw(update));
        if (status == SecurityFramework.ItemNotFound)
        {
            using var label = CoreFoundation.CreateString($"FiscalAPI CLI ({host})");
            using var item = CoreFoundation.CreateDictionary(
                [SecurityFramework.Class, SecurityFramework.AttrService, SecurityFramework.AttrAccount, SecurityFramework.AttrLabel, SecurityFramework.ValueData],
                [SecurityFramework.ClassGenericPassword, Raw(serviceName), Raw(account), Raw(label), Raw(data)]);
            status = SecurityFramework.SecItemAdd(Raw(item), result: null);
        }

        ThrowIfFailed(status, "guardar");
    }

    public bool Delete(string host)
    {
        using var serviceName = CoreFoundation.CreateString(service);
        using var account = CoreFoundation.CreateString(host);
        using var query = CoreFoundation.CreateDictionary(
            [SecurityFramework.Class, SecurityFramework.AttrService, SecurityFramework.AttrAccount],
            [SecurityFramework.ClassGenericPassword, Raw(serviceName), Raw(account)]);

        var status = SecurityFramework.SecItemDelete(Raw(query));
        if (status == SecurityFramework.ItemNotFound)
        {
            return false;
        }

        ThrowIfFailed(status, "borrar");
        return true;
    }

    private static CFHandle CreateSecretData(ApiCredentials credentials)
    {
        var secret = StoredCredentialsFormat.Encode(credentials);
        try
        {
            return CoreFoundation.CreateData(secret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    // Los diccionarios retienen sus llaves y valores; los handles siguen vivos por los using de cada operación.
    private static nint Raw(CFHandle value) => value.DangerousGetHandle();

    private static void ThrowIfFailed(int status, string operation)
    {
        if (status == SecurityFramework.Success)
        {
            return;
        }

        var detail = $"llavero de macOS, OSStatus {status}";
        throw status switch
        {
            // Sin llavero de sesión: no hay almacén y la salida son las variables de entorno.
            SecurityFramework.NotAvailable or SecurityFramework.NoSuchKeychain or SecurityFramework.NoDefaultKeychain =>
                new CredentialStoreUnavailableException(detail),
            // El llavero existe pero está bloqueado y no se puede pedir la contraseña (p. ej. por SSH).
            SecurityFramework.InteractionNotAllowed => CredentialStoreException.Locked(detail),
            _ => CredentialStoreException.Failed(operation, detail)
        };
    }
}
