using System.Text.Json.Serialization;

namespace FiscalApi.Cli.Listen;

// Los contratos de la escucha de CLI de fapi-back (WebhookCliListenerController, D-28), con sus mismos nombres.

/// <summary>El cuerpo de <c>POST /api/v4/webhooks/cli-listener</c> (<c>OpenCliListenerCommand</c>).</summary>
public sealed record OpenCliListenerRequest(IReadOnlyList<string> EnabledEvents);

/// <summary>
/// La sesión que abre la API (<c>CliListenerSessionDto</c>). Clase y no record: su <c>ToString</c> no debe escribir el
/// secreto si algún día termina en un mensaje.
/// </summary>
public sealed class CliListenerSession
{
    /// <summary>El id de la escucha: el mismo en cada apertura de la persona.</summary>
    public required string Id { get; init; }

    /// <summary>La sesión abierta: la llevan el long-polling y cada acuse; otra apertura la reemplaza.</summary>
    public required string SessionId { get; init; }

    /// <summary>El secreto <c>whsec_…</c> con el que la API firma las entregas; estable entre sesiones.</summary>
    public required string Secret { get; init; }
}

/// <summary>Una entrega del long-polling (<c>WebhookCliDeliveryDto</c>): se reenvía tal cual.</summary>
/// <param name="DeliveryId">El id que lleva su acuse.</param>
/// <param name="Headers"><c>webhook-id</c>, <c>webhook-timestamp</c> y <c>webhook-signature</c>, ya firmados.</param>
/// <param name="Body">El sobre del evento: los mismos caracteres que se firmaron.</param>
public sealed record CliDelivery(string DeliveryId, IReadOnlyDictionary<string, string> Headers, string Body)
{
    /// <summary>El id del evento que la API manda en <c>webhook-id</c> (D-02); nulo si no vino.</summary>
    public string? EventId => Headers.GetValueOrDefault(WebhookHeaderNames.Id);

    /// <summary>
    /// Ordena las entregas del mismo evento. Sin <c>webhook-id</c> (un defecto del contrato) la entrega no espera a ninguna
    /// otra, pero igual se muestra y se acusa.
    /// </summary>
    public string OrderingKey => EventId ?? DeliveryId;
}

/// <summary>
/// El cuerpo del acuse (<c>AckWebhookCliDeliveryCommand</c>): lleva <see cref="StatusCode"/> o <see cref="Error"/>, uno de
/// los dos; el que no aplica no se escribe.
/// </summary>
public sealed record CliDeliveryAck(
    string SessionId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? StatusCode,
    int LatencyMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Error);
