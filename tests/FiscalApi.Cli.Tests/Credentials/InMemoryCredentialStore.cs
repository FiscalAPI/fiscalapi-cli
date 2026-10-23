using FiscalApi.Cli.Credentials;

namespace FiscalApi.Cli.Tests.Credentials;

internal sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, ApiCredentials> _entries = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, ApiCredentials> Entries => _entries;

    public void EnsureAvailable(string host)
    {
    }

    public ApiCredentials? Read(string host) => _entries.GetValueOrDefault(host);

    public void Save(string host, ApiCredentials credentials) => _entries[host] = credentials;

    public bool Delete(string host) => _entries.Remove(host);
}

internal sealed class UnavailableCredentialStore : ICredentialStore
{
    public const string Detail = "org.freedesktop.secrets no está disponible (prueba)";

    public void EnsureAvailable(string host) => throw new CredentialStoreUnavailableException(Detail);

    public ApiCredentials Read(string host) => throw new CredentialStoreUnavailableException(Detail);

    public void Save(string host, ApiCredentials credentials) => throw new CredentialStoreUnavailableException(Detail);

    public bool Delete(string host) => throw new CredentialStoreUnavailableException(Detail);
}

internal sealed class LockedCredentialStore : ICredentialStore
{
    public const string Detail = "Secret Service: Cannot create an item in a locked collection (prueba)";

    public void EnsureAvailable(string host) => throw CredentialStoreException.Locked(Detail);

    public ApiCredentials Read(string host) => throw CredentialStoreException.Locked(Detail);

    public void Save(string host, ApiCredentials credentials) => throw CredentialStoreException.Locked(Detail);

    public bool Delete(string host) => throw CredentialStoreException.Locked(Detail);
}
