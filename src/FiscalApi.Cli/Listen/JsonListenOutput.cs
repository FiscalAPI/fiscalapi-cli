using System.Text.Json;
using System.Text.Json.Serialization;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Listen;

public sealed class JsonListenOutput(CliConsole console, bool verbose, TimeProvider timeProvider)
    : ListenOutput(console, verbose, timeProvider)
{
    public override void WriteSecret(string secret) =>
        WriteData(JsonSerializer.Serialize(new ListenSecretRecord(secret), FiscalApiJsonContext.Default.ListenSecretRecord));

    public override void WriteDelivery(CliDelivery delivery, DeliveryOutcome outcome)
    {
        var summary = WebhookEventSummary.Of(delivery);
        var forwarded = outcome.Forwarded ? outcome : null;
        var record = new ListenEventRecord(
            TimeProvider.GetLocalNow(),
            delivery.DeliveryId,
            summary.Id,
            summary.Type,
            forwarded?.StatusCode,
            forwarded?.LatencyMs,
            forwarded?.Error,
            summary.Envelope);
        WriteData(JsonSerializer.Serialize(record, FiscalApiJsonContext.Default.ListenEventRecord));
    }
}

public sealed record ListenSecretRecord(string Secret);

public sealed record ListenEventRecord(
    DateTimeOffset Time,
    string DeliveryId,
    string EventId,
    string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? StatusCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? LatencyMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Error,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? Event);
