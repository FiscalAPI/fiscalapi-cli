using System.Net.Http.Headers;
using FiscalApi.Cli.Credentials;

namespace FiscalApi.Cli.Http;

public sealed class FiscalApiClientFactory(
    HttpMessageHandler handler, CredentialResolver credentials, TextWriter diagnostics, RetryPolicy retryPolicy)
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private const string ApiKeyHeader = "X-API-KEY";
    private const string TenantKeyHeader = "X-TENANT-KEY";

    public static SocketsHttpHandler CreateHandler() => new() { AllowAutoRedirect = false };

    public FiscalApiClient Create(GlobalSettings settings, TimeSpan timeout) =>
        Create(settings, credentials.Resolve(settings.Environment), timeout);

    public FiscalApiClient Create(GlobalSettings settings, ApiCredentials apiCredentials, TimeSpan timeout)
    {
        var pipeline = settings.Verbose ? new VerboseLoggingHandler(diagnostics) { InnerHandler = handler } : handler;
        var httpClient = new HttpClient(pipeline, disposeHandler: false)
        {
            BaseAddress = settings.Environment.BaseAddress,
            Timeout = timeout
        };
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("fiscalapi-cli", CliVersion.Current));
        httpClient.DefaultRequestHeaders.Add(ApiKeyHeader, apiCredentials.ApiKey);
        httpClient.DefaultRequestHeaders.Add(TenantKeyHeader, apiCredentials.TenantKey);
        return new FiscalApiClient(httpClient, retryPolicy);
    }
}
