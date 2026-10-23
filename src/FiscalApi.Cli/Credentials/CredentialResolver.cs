using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Credentials;

public sealed class CredentialResolver(ICredentialStore store, Func<string, string?> readEnvironmentVariable)
{
    public const string ApiKeyVariable = "FISCALAPI_API_KEY";
    public const string TenantKeyVariable = "FISCALAPI_TENANT_KEY";

    public ApiCredentials Resolve(ApiEnvironment environment)
    {
        var apiKey = ReadVariable(ApiKeyVariable);
        var tenantKey = ReadVariable(TenantKeyVariable);

        return (apiKey, tenantKey) switch
        {
            (not null, not null) => ApiCredentials.Create(apiKey, tenantKey),
            (null, null) => store.Read(environment.Host) ?? throw new UsageException(
                $"No credentials for {environment.Host}. Run `fiscalapi login` or set {ApiKeyVariable} and {TenantKeyVariable}."),
            _ => throw new UsageException(
                $"Set both {ApiKeyVariable} and {TenantKeyVariable}; only {(apiKey is null ? TenantKeyVariable : ApiKeyVariable)} is set.")
        };
    }

    private string? ReadVariable(string name) =>
        readEnvironmentVariable(name) is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;
}
