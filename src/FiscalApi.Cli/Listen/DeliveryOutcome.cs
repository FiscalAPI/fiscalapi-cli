namespace FiscalApi.Cli.Listen;

public sealed record DeliveryOutcome(int? StatusCode, string? Error, int LatencyMs, bool Forwarded)
{
    public static DeliveryOutcome Responded(int statusCode, int latencyMs) => new(statusCode, null, latencyMs, Forwarded: true);

    public static DeliveryOutcome Failed(string error, int latencyMs) => new(null, error, latencyMs, Forwarded: true);

    public CliDeliveryAck ToAck(string sessionId) => new(sessionId, StatusCode, LatencyMs, Error);
}
