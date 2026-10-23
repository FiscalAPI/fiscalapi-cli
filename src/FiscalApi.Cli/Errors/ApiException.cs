using System.Collections.Frozen;
using System.Net;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Errors;

public sealed class ApiException : CliException
{
    private static readonly FrozenDictionary<HttpStatusCode, string> Hints = new Dictionary<HttpStatusCode, string>
    {
        [HttpStatusCode.Unauthorized] =
            "Run `fiscalapi login` or set FISCALAPI_API_KEY and FISCALAPI_TENANT_KEY."
    }.ToFrozenDictionary();

    private ApiException(
        HttpStatusCode statusCode,
        string message,
        string? details,
        string? traceIdentifier,
        IReadOnlyList<string> validationErrors)
        : base(message)
    {
        StatusCode = statusCode;
        Details = details;
        TraceIdentifier = traceIdentifier;
        ValidationErrors = validationErrors;
    }

    public override int ExitCode => ExitCodes.Failure;

    public override bool IsTransient => TransientHttpStatus.Is(StatusCode);

    public HttpStatusCode StatusCode { get; }

    public string? Details { get; }

    public string? TraceIdentifier { get; }

    public string? Hint => Hints.GetValueOrDefault(StatusCode);

    public IReadOnlyList<string> ValidationErrors { get; }

    public static ApiException FromEnvelope(
        HttpStatusCode statusCode, ApiResponse envelope, IReadOnlyList<string>? validationErrors = null) => new(
        statusCode,
        string.IsNullOrWhiteSpace(envelope.Message) ? $"The API returned {(int)statusCode} without a message." : envelope.Message,
        NullIfBlank(envelope.Details),
        NullIfBlank(envelope.TraceIdentifier),
        validationErrors ?? []);

    public static ApiException UnexpectedResponse(HttpStatusCode statusCode) =>
        new(statusCode, $"Unexpected response ({(int)statusCode}).", details: null, traceIdentifier: null, validationErrors: []);

    public override void WriteTo(TextWriter error)
    {
        base.WriteTo(error);
        if (Details is not null && Details != Message)
        {
            error.WriteLine($"  Details: {Details}");
        }

        foreach (var validationError in ValidationErrors)
        {
            error.WriteLine($"  - {validationError}");
        }

        error.WriteLine($"  HTTP status: {(int)StatusCode}");
        if (TraceIdentifier is not null)
        {
            error.WriteLine($"  traceIdentifier (for support): {TraceIdentifier}");
        }

        if (Hint is not null)
        {
            error.WriteLine($"  Hint: {Hint}");
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
