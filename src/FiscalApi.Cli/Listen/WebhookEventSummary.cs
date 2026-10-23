using System.Text.Json;

namespace FiscalApi.Cli.Listen;

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
        type.ValueKind == JsonValueKind.String &&
        type.GetString() is { } value
            ? value
            : Unknown;

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
