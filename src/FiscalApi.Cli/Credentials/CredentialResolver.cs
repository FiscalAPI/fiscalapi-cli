using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Credentials;

/// <summary>
/// De dónde salen las credenciales de una invocación: las variables de entorno ganan sobre lo guardado en
/// el almacén del SO, para que el CI no dependa de un llavero.
/// </summary>
public sealed class CredentialResolver(ICredentialStore store, Func<string, string?> readEnvironmentVariable)
{
    public const string ApiKeyVariable = "FISCALAPI_API_KEY";
    public const string TenantKeyVariable = "FISCALAPI_TENANT_KEY";

    /// <exception cref="UsageException">Sin credenciales, con solo una de las dos variables o con un valor inválido.</exception>
    /// <exception cref="CredentialStoreUnavailableException">Sin variables y sin almacén en el SO.</exception>
    /// <exception cref="CredentialStoreException">Sin variables, y el almacén está bloqueado o falló.</exception>
    public ApiCredentials Resolve(ApiEnvironment environment)
    {
        var apiKey = ReadVariable(ApiKeyVariable);
        var tenantKey = ReadVariable(TenantKeyVariable);

        return (apiKey, tenantKey) switch
        {
            (not null, not null) => ApiCredentials.Create(apiKey, tenantKey),
            (null, null) => store.Read(environment.Host) ?? throw new UsageException(
                $"No hay credenciales para {environment.Host}. Ejecuta `fiscalapi login` o define {ApiKeyVariable} y {TenantKeyVariable}."),
            // Completar con lo guardado mezclaría la key de un tenant con la tenant key de otro.
            _ => throw new UsageException(
                $"Define {ApiKeyVariable} y {TenantKeyVariable} juntas; solo está definida {(apiKey is null ? TenantKeyVariable : ApiKeyVariable)}.")
        };
    }

    // Una variable vacía cuenta como no definida: es lo que deja `export FISCALAPI_API_KEY=` en un script.
    private string? ReadVariable(string name) =>
        readEnvironmentVariable(name) is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;
}
