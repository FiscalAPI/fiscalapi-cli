using FiscalApi.Cli.Credentials;

namespace FiscalApi.Cli.Tests.Credentials;

/// <summary>
/// Ida y vuelta contra el almacén real del SO local (Credential Manager, llavero de macOS o Secret Service).
/// Usa un servicio de prueba propio y hosts únicos por prueba, y borra lo que escribe: nunca toca las entradas
/// de producción (<see cref="CredentialStores.ProductionService"/>).
/// </summary>
public sealed class CredentialStoreTests : IDisposable
{
    private const string TestService = "fiscalapi-cli-test";

    private readonly ICredentialStore _store = CredentialStores.ForCurrentOs(TestService);
    private readonly string _host = UniqueHost();
    private readonly string _otherHost = UniqueHost() + ":5001";

    private static readonly ApiCredentials Credentials = ApiCredentials.Create("sk_test_ida-y-vuelta", "tenant-ida-y-vuelta");
    private static readonly ApiCredentials OtherCredentials = ApiCredentials.Create("sk_test_otro-host", "tenant-otro-host");

    [CredentialStoreFact]
    public void Saved_credentials_are_read_back_and_gone_after_delete()
    {
        _store.Save(_host, Credentials);

        Assert.Equal(Credentials, _store.Read(_host));
        Assert.True(_store.Delete(_host));
        Assert.Null(_store.Read(_host));
    }

    [CredentialStoreFact]
    public void Saving_again_replaces_the_entry_of_the_host()
    {
        _store.Save(_host, OtherCredentials);
        _store.Save(_host, Credentials);

        Assert.Equal(Credentials, _store.Read(_host));
    }

    [CredentialStoreFact]
    public void Credentials_of_another_host_do_not_cross()
    {
        _store.Save(_host, Credentials);
        _store.Save(_otherHost, OtherCredentials);

        Assert.Equal(Credentials, _store.Read(_host));
        Assert.Equal(OtherCredentials, _store.Read(_otherHost));

        Assert.True(_store.Delete(_otherHost));
        Assert.Equal(Credentials, _store.Read(_host));
    }

    [CredentialStoreFact]
    public void Host_without_entry_reads_as_null()
    {
        Assert.Null(_store.Read(_host));
    }

    [CredentialStoreFact]
    public void Deleting_a_missing_entry_does_not_fail()
    {
        Assert.False(_store.Delete(_host));
    }

    [CredentialStoreFact]
    public void Store_of_this_system_is_available_with_or_without_an_entry()
    {
        _store.EnsureAvailable(_host);
        _store.Save(_host, Credentials);

        _store.EnsureAvailable(_host);
    }

    // Cada borrado por separado: si el primero falla, el segundo igual limpia su entrada.
    public void Dispose()
    {
        try
        {
            _store.Delete(_host);
        }
        finally
        {
            _store.Delete(_otherHost);
        }
    }

    private static string UniqueHost() => $"{Guid.NewGuid():N}.example.test";
}
