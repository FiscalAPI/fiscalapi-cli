namespace FiscalApi.Cli.Credentials;

public interface ICredentialStore
{
    void EnsureAvailable(string host);

    ApiCredentials? Read(string host);

    void Save(string host, ApiCredentials credentials);

    bool Delete(string host);
}
