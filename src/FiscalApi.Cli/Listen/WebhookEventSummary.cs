using System.Text.Json;

namespace FiscalApi.Cli.Listen;

/// <summary>
/// Lo que la terminal muestra de una entrega: el id del evento (de <c>webhook-id</c>), su tipo y el sobre (D-04). Se lee
/// sin tocar el cuerpo, que se reenvía siempre tal cual, y sin fallar: un cuerpo que no es el sobre o una entrega sin
/// <c>webhook-id</c> se muestran con <see cref="Unknown"/> y se acusan como cualquier otra.
/// </summary>
/// <param name="Envelope">El sobre completo para <c>--json</c>; nulo si el cuerpo no es JSON.</param>
public sealed record WebhookEventSummary(string Id, string Type, JsonElement? Envelope)
{
    public const string Unknown = "?";

    public static WebhookEventSummary Of(CliDelivery delivery)
    {
        var envelope = TryParse(delivery.Body);
        return new WebhookEventSummary(delivery.EventId ?? Unknown, TypeOf(envelope), envelope);
    }

    private static string TypeOf(JsonElement? envelope) =>
        envelope is { ValueKind: JsonValueKind.Object } root &&
        root.TryGetProperty("type", out var type) &&
        type.ValueKind == JsonValueKind.String
            ? type.GetString()!
            : Unknown;

    // Clone: el elemento sobrevive al documento que lo leyó.
    private static JsonElement? TryParse(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
