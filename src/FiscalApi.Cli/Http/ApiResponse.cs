namespace FiscalApi.Cli.Http;

/// <summary>
/// Envelope de todas las respuestas de fapi-back (<c>WebApi.Libs.Domain.Common.ApiResponse&lt;T&gt;</c>), en camelCase.
/// <c>httpStatusCode</c> no se lee: el back lo omite cuando vale el default y el código real sale de la respuesta HTTP.
/// </summary>
public class ApiResponse
{
    public bool Succeeded { get; init; }

    public string? Message { get; init; }

    public string? Details { get; init; }

    public string? TraceIdentifier { get; init; }
}

public sealed class ApiResponse<T> : ApiResponse
{
    public T? Data { get; init; }
}
