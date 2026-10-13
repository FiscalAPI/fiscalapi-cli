using FiscalApi.Cli.Credentials;

namespace FiscalApi.Cli.Tests.Credentials;

/// <summary>Almacén en memoria para las pruebas de comandos: no toca el almacén real del SO.</summary>
internal sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, ApiCredentials> _entries = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, ApiCredentials> Entries => _entries;

    public ApiCredentials? Read(string host) => _entries.GetValueOrDefault(host);

    public void Save(string host, ApiCredentials credentials) => _entries[host] = credentials;

    public bool Delete(string host) => _entries.Remove(host);
}

/// <summary>Como Linux sin Secret Service, WSL o un servidor: cada operación reporta que no hay almacén.</summary>
internal sealed class UnavailableCredentialStore : ICredentialStore
{
    public const string Detail = "org.freedesktop.secrets no está disponible (prueba)";

    public ApiCredentials Read(string host) => throw new CredentialStoreUnavailableException(Detail);

    public void Save(string host, ApiCredentials credentials) => throw new CredentialStoreUnavailableException(Detail);

    public bool Delete(string host) => throw new CredentialStoreUnavailableException(Detail);
}
