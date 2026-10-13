using System.Net.Http.Headers;

namespace FiscalApi.Cli.Http;

/// <summary>
/// El único lugar donde se fabrica el <see cref="HttpClient"/> del CLI. Se crea uno por invocación, después de
/// parsear, porque la URL base y <c>--verbose</c> dependen de las opciones globales.
/// </summary>
public static class FiscalApiClientFactory
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    public static FiscalApiClient Create(GlobalSettings settings, TextWriter diagnostics) =>
        new(CreateHttpClient(settings, diagnostics), RetryPolicy.Default);

    public static HttpClient CreateHttpClient(GlobalSettings settings, TextWriter diagnostics)
    {
        // SocketsHttpHandler usa el proxy del sistema por omisión (UseProxy = true), como pide el contrato.
        HttpMessageHandler handler = new SocketsHttpHandler();
        if (settings.Verbose)
        {
            handler = new VerboseLoggingHandler(diagnostics) { InnerHandler = handler };
        }

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = settings.Environment.BaseAddress,
            Timeout = RequestTimeout
        };
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("fiscalapi-cli", CliVersion.Current));
        return httpClient;
    }
}
