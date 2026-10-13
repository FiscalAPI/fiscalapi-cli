using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Tests.Http;

internal static class FiscalApiClientBuilder
{
    public static readonly Uri BaseAddress = new("https://api.example.test");

    /// <summary>Política real (3 intentos) sin espera entre ellos, para que las pruebas no duerman.</summary>
    public static readonly RetryPolicy NoDelayRetryPolicy =
        new(maxAttempts: 3, baseDelay: TimeSpan.Zero, maxDelay: TimeSpan.FromSeconds(1), TimeProvider.System);

    public static FiscalApiClient Create(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = BaseAddress }, NoDelayRetryPolicy);
}
