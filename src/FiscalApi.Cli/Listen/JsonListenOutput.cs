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
        var summary = WebhookEventSummary.Of(delivery);
        // Sin --forward-to no hay resultado del receptor que mostrar.
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

/// <summary>La línea de <c>--print-secret --json</c>.</summary>
public sealed record ListenSecretRecord(string Secret);

/// <summary>
/// La línea de una entrega con <c>--json</c>. Sin <c>--forward-to</c> no lleva statusCode, latencyMs ni error; con él,
/// statusCode o error, como su acuse.
/// </summary>
/// <param name="Time">Cuando terminó el reenvío, en la hora local con su desfase.</param>
/// <param name="EventId">El de <c>webhook-id</c>, o "?" si no vino.</param>
/// <param name="Type">El del sobre, o "?" si el cuerpo no es el sobre de D-04.</param>
/// <param name="Event">El sobre del evento tal como llegó (D-04); se omite si el cuerpo no es JSON.</param>
public sealed record ListenEventRecord(
    DateTimeOffset Time,
    string DeliveryId,
    string EventId,
    string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? StatusCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? LatencyMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Error,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? Event);
