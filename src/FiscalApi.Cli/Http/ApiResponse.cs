namespace FiscalApi.Cli.Http;

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
