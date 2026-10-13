using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Tests.Http;

/// <summary>Arma el cliente con la misma fábrica que usa producción, sobre un handler falso.</summary>
internal static class FiscalApiClientBuilder
{
    public static readonly Uri BaseAddress = new("https://api.example.test/");

    /// <summary>Política real (3 intentos) sin espera entre ellos, para las pruebas que no miden tiempos.</summary>
    public static readonly RetryPolicy NoDelayRetryPolicy =
        new(maxAttempts: 3, baseDelay: TimeSpan.Zero, maxDelay: TimeSpan.FromSeconds(1), TimeProvider.System);

    public static FiscalApiClient Create(
        StubHttpMessageHandler handler, RetryPolicy? retryPolicy = null, TimeSpan? timeout = null) =>
        new FiscalApiClientFactory(handler, TextWriter.Null, retryPolicy ?? NoDelayRetryPolicy)
            .Create(
                new GlobalSettings(ApiEnvironment.Resolve(BaseAddress, live: false), Json: false, Verbose: false),
                timeout ?? FiscalApiClientFactory.DefaultTimeout);
}
