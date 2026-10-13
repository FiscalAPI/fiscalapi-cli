using System.Text.Json;
using System.Text.Json.Serialization;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Listen;

/// <summary><c>--json</c>: un objeto JSON por línea en stdout (JSON Lines), para procesarlo con otra herramienta.</summary>
public sealed class JsonListenOutput(CliConsole console, bool verbose, TimeProvider timeProvider)
    : ListenOutput(console, verbose, timeProvider)
{
    public override void WriteSecret(string secret) =>
        WriteData(JsonSerializer.Serialize(new ListenSecretRecord(secret), FiscalApiJsonContext.Default.ListenSecretRecord));

    public override void WriteDelivery(CliDelivery delivery, DeliveryOutcome outcome)
    {
        var summary = WebhookEventSummary.Parse(delivery.Body);
        var record = new ListenEventRecord(
            TimeProvider.GetLocalNow(),
            delivery.DeliveryId,
            summary.Id,
            summary.Type,
            outcome.StatusCode,
            outcome.LatencyMs,
            outcome.Error,
            summary.Envelope);
        WriteData(JsonSerializer.Serialize(record, FiscalApiJsonContext.Default.ListenEventRecord));
    }
}

/// <summary>La línea de <c>--print-secret --json</c>.</summary>
public sealed record ListenSecretRecord(string Secret);

/// <summary>
/// La línea de una entrega con <c>--json</c>. Sin <c>--forward-to</c> no lleva statusCode, latencyMs ni error; con él,
/// statusCode o error, como su acuse.
/// </summary>
/// <param name="Time">Cuando terminó el reenvío, en la hora local con su desfase.</param>
/// <param name="Event">El sobre del evento tal como llegó (D-04).</param>
public sealed record ListenEventRecord(
    DateTimeOffset Time,
    string DeliveryId,
    string EventId,
    string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? StatusCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? LatencyMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Error,
    JsonElement Event);
