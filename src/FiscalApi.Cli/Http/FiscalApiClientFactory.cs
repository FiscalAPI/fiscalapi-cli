using System.Net.Http.Headers;

namespace FiscalApi.Cli.Http;

/// <summary>
/// El único lugar donde se fabrican los clientes HTTP del CLI. Comparte un solo handler (pool de conexiones)
/// y crea un <see cref="HttpClient"/> por invocación, porque la URL base, <c>--verbose</c> y el timeout
/// dependen de las opciones globales y del comando. Llega a los comandos por constructor para que sus
/// pruebas inyecten un handler falso.
/// </summary>
public sealed class FiscalApiClientFactory(HttpMessageHandler handler, TextWriter diagnostics, RetryPolicy retryPolicy)
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// El handler de producción, uno por proceso. Usa el proxy del sistema por omisión (<c>UseProxy = true</c>).
    /// No sigue redirecciones: un 3xx termina como <see cref="Errors.ApiException"/>, así X-API-KEY y
    /// X-TENANT-KEY nunca viajan a otro host y un POST no se convierte en GET.
    /// </summary>
    public static SocketsHttpHandler CreateHandler() => new() { AllowAutoRedirect = false };

    public FiscalApiClient Create(GlobalSettings settings, TimeSpan timeout)
    {
        // Cada HttpClient envuelve el handler compartido sin adueñarse de él: lo libera Program al salir.
        var pipeline = settings.Verbose ? new VerboseLoggingHandler(diagnostics) { InnerHandler = handler } : handler;
        var httpClient = new HttpClient(pipeline, disposeHandler: false)
        {
            BaseAddress = settings.Environment.BaseAddress,
            Timeout = timeout
        };
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("fiscalapi-cli", CliVersion.Current));
        return new FiscalApiClient(httpClient, retryPolicy);
    }
}
