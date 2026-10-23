using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace FiscalApi.Cli.Credentials.MacOS;

[SupportedOSPlatform("macos")]
internal sealed unsafe class KeychainCredentialStore(string service) : ICredentialStore
{
    private const string StoreName = "macOS Keychain";

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
            ThrowIfFailed(status, "check");
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

        ThrowIfFailed(status, "read");
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

        var status = SecurityFramework.SecItemUpdate(Raw(query), Raw(update));
        if (status == SecurityFramework.ItemNotFound)
        {
            using var label = CoreFoundation.CreateString($"FiscalAPI CLI ({host})");
            using var item = CoreFoundation.CreateDictionary(
                [SecurityFramework.Class, SecurityFramework.AttrService, SecurityFramework.AttrAccount, SecurityFramework.AttrLabel, SecurityFramework.ValueData],
                [SecurityFramework.ClassGenericPassword, Raw(serviceName), Raw(account), Raw(label), Raw(data)]);
            status = SecurityFramework.SecItemAdd(Raw(item), result: null);
        }

        ThrowIfFailed(status, "save");
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

        ThrowIfFailed(status, "delete");
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

    private static nint Raw(CFHandle value) => value.DangerousGetHandle();

    private static void ThrowIfFailed(int status, string operation)
    {
        if (status == SecurityFramework.Success)
        {
            return;
        }

        var detail = $"{StoreName}: OSStatus {status}";
        throw status switch
        {
            SecurityFramework.NotAvailable or SecurityFramework.NoSuchKeychain or SecurityFramework.NoDefaultKeychain =>
                new CredentialStoreUnavailableException(detail),
            SecurityFramework.InteractionNotAllowed => CredentialStoreException.Locked(detail),
            _ => CredentialStoreException.Failed(operation, detail)
        };
    }
}
