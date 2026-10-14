using System.Net.Http.Headers;
using FiscalApi.Cli.Credentials;

namespace FiscalApi.Cli.Http;

/// <summary>
/// El único lugar donde se fabrican los clientes HTTP del CLI. Comparte un solo handler (pool de conexiones)
/// y crea un <see cref="HttpClient"/> por invocación, porque la URL base, <c>--verbose</c>, el timeout y las
/// credenciales dependen de las opciones globales y del comando. Llega a los comandos por constructor para que
/// sus pruebas inyecten un handler falso.
/// </summary>
public sealed class FiscalApiClientFactory(
    HttpMessageHandler handler, CredentialResolver credentials, TextWriter diagnostics, RetryPolicy retryPolicy)
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    // Los de fapi-back (ApiConstants.ApiKeyHeader y TenantHeader).
    private const string ApiKeyHeader = "X-API-KEY";
    private const string TenantKeyHeader = "X-TENANT-KEY";

    /// <summary>
    /// El handler de producción, uno por proceso. Usa el proxy del sistema por omisión (<c>UseProxy = true</c>).
    /// No sigue redirecciones: un 3xx termina como <see cref="Errors.ApiException"/>, así X-API-KEY y
    /// X-TENANT-KEY nunca viajan a otro host y un POST no se convierte en GET.
    /// </summary>
    public static SocketsHttpHandler CreateHandler() => new() { AllowAutoRedirect = false };

    /// <summary>
    /// El cliente de los comandos de la API, con las credenciales del ambiente: las variables de entorno o, si no
    /// están, las que guardó <c>fiscalapi login</c> (<see cref="CredentialResolver"/>).
    /// </summary>
    public FiscalApiClient Create(GlobalSettings settings, TimeSpan timeout) =>
        Create(settings, credentials.Resolve(settings.Environment), timeout);

    /// <summary>Con credenciales dadas: <c>fiscalapi login</c> valida las que la persona acaba de escribir.</summary>
    public FiscalApiClient Create(GlobalSettings settings, ApiCredentials apiCredentials, TimeSpan timeout)
    {
        // Cada HttpClient envuelve el handler compartido sin adueñarse de él: lo libera Program al salir.
        var pipeline = settings.Verbose ? new VerboseLoggingHandler(diagnostics) { InnerHandler = handler } : handler;
        var httpClient = new HttpClient(pipeline, disposeHandler: false)
        {
            BaseAddress = settings.Environment.BaseAddress,
            Timeout = timeout
        };
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("fiscalapi-cli", CliVersion.Current));
        // ApiCredentials ya garantiza ASCII visible: Add no puede fallar con un mensaje que repita el valor.
        httpClient.DefaultRequestHeaders.Add(ApiKeyHeader, apiCredentials.ApiKey);
        httpClient.DefaultRequestHeaders.Add(TenantKeyHeader, apiCredentials.TenantKey);
        return new FiscalApiClient(httpClient, retryPolicy);
    }
}
