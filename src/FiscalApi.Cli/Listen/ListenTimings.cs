using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Listen;

/// <summary>
/// Los plazos de <c>fiscalapi listen</c>. <see cref="Default"/> son los del contrato (CLI-004, WH-015); las pruebas los
/// acortan para no esperar 15 s a un receptor ni 30 s a una reconexión.
/// </summary>
/// <param name="ApiTimeout">Timeout del cliente de la API: cubre el long-polling de <paramref name="PollWaitSeconds"/>.</param>
/// <param name="PollWaitSeconds">La espera que se pide a cada long-polling; la API la acota a 25 s.</param>
/// <param name="ForwardTimeout">Timeout de cada reenvío a <c>--forward-to</c>.</param>
/// <param name="Reconnect">Espera entre reintentos tras un error de red o un 5xx, con jitter.</param>
/// <param name="MaxInFlightDeliveries">Entregas que se reenvían (o esperan su turno) a la vez.</param>
/// <param name="DeliveryLease">
/// Cuánto espera la API el acuse de una entrega que respondió el long-polling (<c>WebhookConsts.DeliveryLease</c>):
/// después la cierra sin acuse, así que reintentar un acuse más allá no sirve.
/// </param>
public sealed record ListenTimings(
    TimeSpan ApiTimeout,
    int PollWaitSeconds,
    TimeSpan ForwardTimeout,
    ExponentialBackoff Reconnect,
    int MaxInFlightDeliveries,
    TimeSpan DeliveryLease)
{
    public static ListenTimings Default { get; } = new(
        ApiTimeout: TimeSpan.FromSeconds(35),
        PollWaitSeconds: 25,
        ForwardTimeout: TimeSpan.FromSeconds(15),
        Reconnect: new ExponentialBackoff(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30)),
        // Un lote del long-polling trae hasta 25 entregas (WebhookConsts.ListenerBatchSize). Con 16 a la vez, aun si
        // todas agotan los 15 s del reenvío, el lote se despacha en dos rondas y el siguiente long-polling sale mucho
        // antes de que la sesión caduque (60 s sin long-polling).
        MaxInFlightDeliveries: 16,
        DeliveryLease: TimeSpan.FromMinutes(2));
}
