using FiscalApi.Cli.Credentials;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Tests.Credentials;

namespace FiscalApi.Cli.Tests.Http;

internal static class FiscalApiClientBuilder
{
    public static readonly Uri BaseAddress = new("https://api.example.test/");

    public static readonly ApiCredentials Credentials = ApiCredentials.Create("sk_test_clave-de-prueba", "tenant-de-prueba-0001");

    public static readonly RetryPolicy NoDelayRetryPolicy =
        new(maxAttempts: 3, baseDelay: TimeSpan.Zero, maxDelay: TimeSpan.FromSeconds(1), TimeProvider.System);

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
