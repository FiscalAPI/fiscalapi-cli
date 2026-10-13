using System.Collections.Frozen;
using System.Net;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Errors;

/// <summary>
/// La API respondió con un error. Se arma desde el envelope <see cref="ApiResponse"/> de fapi-back y muestra
/// <c>message</c>, <c>details</c> y el <c>traceIdentifier</c> que pide soporte.
/// </summary>
public sealed class ApiException : CliException
{
    // Pistas accionables por código HTTP. Viven aquí, en un mapa, y no en condicionales repartidos por los comandos.
    private static readonly FrozenDictionary<HttpStatusCode, string> Hints = new Dictionary<HttpStatusCode, string>
    {
        // El back responde 401 tanto por la API key como por la tenant key (X-TENANT-KEY).
        [HttpStatusCode.Unauthorized] =
            "Ejecuta `fiscalapi login` o revisa las variables de entorno FISCALAPI_API_KEY y FISCALAPI_TENANT_KEY."
    }.ToFrozenDictionary();

    private ApiException(HttpStatusCode statusCode, string message, string? details, string? traceIdentifier)
        : base(message)
    {
        StatusCode = statusCode;
        Details = details;
        TraceIdentifier = traceIdentifier;
    }

    public override int ExitCode => ExitCodes.Failure;

    /// <summary>408, 429 y 5xx (incluido el 502/503 de nginx en un deploy); un 4xx distinto nunca.</summary>
    public override bool IsTransient => TransientHttpStatus.Is(StatusCode);

    /// <summary>El de la respuesta HTTP: el envelope omite <c>httpStatusCode</c> cuando vale el default.</summary>
    public HttpStatusCode StatusCode { get; }

    public string? Details { get; }

    public string? TraceIdentifier { get; }

    public string? Hint => Hints.GetValueOrDefault(StatusCode);

    public static ApiException FromEnvelope(HttpStatusCode statusCode, ApiResponse envelope) => new(
        statusCode,
        string.IsNullOrWhiteSpace(envelope.Message) ? $"La API respondió {(int)statusCode} sin mensaje." : envelope.Message,
        NullIfBlank(envelope.Details),
        NullIfBlank(envelope.TraceIdentifier));

    /// <summary>Un cuerpo que no es el envelope JSON, como el 502/503 en HTML de nginx.</summary>
    public static ApiException UnexpectedResponse(HttpStatusCode statusCode) =>
        new(statusCode, $"Respuesta inesperada ({(int)statusCode}).", details: null, traceIdentifier: null);

    public override void WriteTo(TextWriter error)
    {
        base.WriteTo(error);
        if (Details is not null && Details != Message)
        {
            error.WriteLine($"  Detalle: {Details}");
        }

        error.WriteLine($"  Código HTTP: {(int)StatusCode}");
        if (TraceIdentifier is not null)
        {
            error.WriteLine($"  traceIdentifier (para soporte): {TraceIdentifier}");
        }

        if (Hint is not null)
        {
            error.WriteLine($"  Sugerencia: {Hint}");
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
