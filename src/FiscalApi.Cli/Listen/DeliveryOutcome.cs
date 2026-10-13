namespace FiscalApi.Cli.Listen;

/// <summary>
/// Lo que pasó con una entrega: el receptor de <c>--forward-to</c> respondió un código, o no respondió y queda el motivo,
/// o no hubo reenvío porque <c>listen</c> corre sin <c>--forward-to</c>. Es lo que lleva su acuse.
/// </summary>
/// <param name="StatusCode">El código que respondió el receptor.</param>
/// <param name="Error">Por qué el receptor no respondió, con los nombres de <c>WebhookAttemptError</c> de fapi-back.</param>
/// <param name="LatencyMs">Lo que tardó el reenvío; nulo si no hubo.</param>
public sealed record DeliveryOutcome(int? StatusCode, string? Error, int? LatencyMs)
{
    // Asumido (CLI-004): sin --forward-to la terminal es el destino y la entrega llegó, como en stripe listen. Se acusa
    // como un 200 inmediato para que el log de entregas no la muestre fallida.
    private const int DisplayedStatusCode = 200;

    public static DeliveryOutcome NotForwarded { get; } = new(null, null, null);

    public bool Forwarded => LatencyMs is not null;

    public static DeliveryOutcome Responded(int statusCode, int latencyMs) => new(statusCode, null, latencyMs);

    public static DeliveryOutcome Failed(string error, int latencyMs) => new(null, error, latencyMs);

    public CliDeliveryAck ToAck(string sessionId) => Forwarded
        ? new CliDeliveryAck(sessionId, StatusCode, LatencyMs!.Value, Error)
        : new CliDeliveryAck(sessionId, DisplayedStatusCode, LatencyMs: 0, Error: null);
}
