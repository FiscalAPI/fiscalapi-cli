using FiscalApi.Cli.Credentials;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Tests.Credentials;

namespace FiscalApi.Cli.Tests.Http;

/// <summary>Arma el cliente con la misma fábrica que usa producción, sobre un handler falso.</summary>
internal static class FiscalApiClientBuilder
{
    public static readonly Uri BaseAddress = new("https://api.example.test/");

    public static readonly ApiCredentials Credentials = ApiCredentials.Create("sk_test_clave-de-prueba", "tenant-de-prueba-0001");

    /// <summary>Política real (3 intentos) sin espera entre ellos, para las pruebas que no miden tiempos.</summary>
    public static readonly RetryPolicy NoDelayRetryPolicy =
        new(maxAttempts: 3, baseDelay: TimeSpan.Zero, maxDelay: TimeSpan.FromSeconds(1), TimeProvider.System);

    /// <summary>Fábrica sin credenciales guardadas ni variables de entorno: las pruebas pasan las suyas.</summary>
    public static FiscalApiClientFactory Factory(
        HttpMessageHandler handler, TextWriter? diagnostics = null, RetryPolicy? retryPolicy = null) =>
        new(
            handler,
            new CredentialResolver(new InMemoryCredentialStore(), _ => null),
            diagnostics ?? TextWriter.Null,
            retryPolicy ?? NoDelayRetryPolicy);

    public static FiscalApiClient Create(
        StubHttpMessageHandler handler, RetryPolicy? retryPolicy = null, TimeSpan? timeout = null) =>
        Factory(handler, retryPolicy: retryPolicy).Create(
            new GlobalSettings(ApiEnvironment.Resolve(BaseAddress, live: false), Json: false, Verbose: false),
            Credentials,
            timeout ?? FiscalApiClientFactory.DefaultTimeout);
}
