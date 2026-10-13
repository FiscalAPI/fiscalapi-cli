using System.Text.Json;

namespace FiscalApi.Cli.Listen;

/// <summary>
/// Lo que la terminal muestra de una entrega: el id, el tipo y el sobre del evento (D-04). Se lee del cuerpo sin tocarlo:
/// lo que se reenvía es siempre el texto original.
/// </summary>
/// <param name="Envelope">El sobre completo, para <c>--json</c>; no depende del documento que lo leyó.</param>
public sealed record WebhookEventSummary(string Id, string Type, JsonElement Envelope)
{
    /// <exception cref="JsonException">El cuerpo no es el sobre de D-04: un defecto del contrato con la API.</exception>
    public static WebhookEventSummary Parse(string body)
    {
        using var envelope = JsonDocument.Parse(body);
        var root = envelope.RootElement;
        return new WebhookEventSummary(RequiredString(root, "id"), RequiredString(root, "type"), root.Clone());
    }

    private static string RequiredString(JsonElement envelope, string property) =>
        envelope.GetProperty(property).GetString()
        ?? throw new JsonException($"El sobre del evento trae '{property}' nulo.");
}
