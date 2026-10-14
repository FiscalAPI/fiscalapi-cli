using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Listen;

/// <summary>
/// Los plazos de <c>fiscalapi listen</c>. <see cref="Default"/> son los del contrato con la API; las pruebas los
/// acortan para no esperar 15 s a un receptor ni 30 s a una reconexión.
/// </summary>
/// <param name="ApiTimeout">Timeout del cliente de la API: cubre el long-polling de <paramref name="PollWaitSeconds"/>.</param>
/// <param name="PollWaitSeconds">La espera que se pide a cada long-polling; la API la acota a 25 s.</param>
/// <param name="ForwardTimeout">Timeout de cada reenvío a <c>--forward-to</c>.</param>
/// <param name="Reconnect">
/// Espera entre reintentos tras un error de red o un 5xx, tras un 409 al reabrir o entre dos 410 seguidos, con jitter.
/// </param>
/// <param name="MaxInFlightDeliveries">Entregas que se reenvían a la vez; las que esperan su turno no cuentan.</param>
/// <param name="DeliveryLease">
/// Cuánto espera la API el acuse de una entrega que respondió el long-polling (<c>WebhookConsts.DeliveryLease</c>):
/// después la cierra sin acuse, así que ni se reenvía ni se reintenta su acuse más allá.
/// </param>
/// <param name="ShutdownAckTimeout">
/// Al salir con Ctrl+C, el plazo del único intento de acusar lo que ya tenía resultado.
/// </param>
public sealed record ListenTimings(
    TimeSpan ApiTimeout,
    int PollWaitSeconds,
    TimeSpan ForwardTimeout,
    ExponentialBackoff Reconnect,
    int MaxInFlightDeliveries,
    TimeSpan DeliveryLease,
    TimeSpan ShutdownAckTimeout)
{
    public static ListenTimings Default { get; } = new(
        ApiTimeout: TimeSpan.FromSeconds(35),
        PollWaitSeconds: 25,
        ForwardTimeout: TimeSpan.FromSeconds(15),
        Reconnect: new ExponentialBackoff(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30)),
        // El long-polling nunca espera a los reenvíos (DeliveryDispatcher), así que este cupo solo acota cuántos receptores
        // se atienden a la vez; no influye en que la sesión siga viva (60 s sin long-polling).
        MaxInFlightDeliveries: 16,
        DeliveryLease: TimeSpan.FromMinutes(2),
        // Corto: la persona pidió salir. Un acuse que no llega en este plazo lo cierra la API por AckTimeout.
        ShutdownAckTimeout: TimeSpan.FromSeconds(2));
}
