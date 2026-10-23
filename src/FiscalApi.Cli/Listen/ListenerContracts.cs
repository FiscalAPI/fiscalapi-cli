using System.Text.Json.Serialization;

namespace FiscalApi.Cli.Listen;

public sealed record OpenCliListenerRequest(IReadOnlyList<string> EnabledEvents);

public sealed class CliListenerSession
{
    public required string Id { get; init; }

    public required string SessionId { get; init; }

    public required string Secret { get; init; }
}

public sealed record CliDelivery(string DeliveryId, IReadOnlyDictionary<string, string> Headers, string Body)
{
    public string? EventId => Headers.GetValueOrDefault(WebhookHeaderNames.Id);

    public string OrderingKey => EventId ?? DeliveryId;
}

public sealed record CliDeliveryAck(
    string SessionId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? StatusCode,
    int LatencyMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Error);
