namespace FiscalApi.Cli.Listen;

/// <summary>
/// Lo que pasó con una entrega, decidido por su destino (<see cref="IDeliveryTarget"/>): es lo que lleva su acuse y lo que
/// muestra la terminal.
/// </summary>
/// <param name="StatusCode">El código que se acusa; nulo si el receptor no respondió.</param>
/// <param name="Error">Por qué el receptor no respondió, con los nombres de <c>WebhookAttemptError</c> de fapi-back.</param>
/// <param name="LatencyMs">Lo que tardó la entrega.</param>
/// <param name="Forwarded">Si hubo un reenvío cuyo resultado se muestra; sin <c>--forward-to</c> no lo hay.</param>
public sealed record DeliveryOutcome(int? StatusCode, string? Error, int LatencyMs, bool Forwarded)
{
    public static DeliveryOutcome Responded(int statusCode, int latencyMs) => new(statusCode, null, latencyMs, Forwarded: true);

    public static DeliveryOutcome Failed(string error, int latencyMs) => new(null, error, latencyMs, Forwarded: true);

    public CliDeliveryAck ToAck(string sessionId) => new(sessionId, StatusCode, LatencyMs, Error);
}
