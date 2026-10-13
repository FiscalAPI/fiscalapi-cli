using System.Net;
using FiscalApi.Cli.Errors;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Listen;

/// <summary>
/// El trabajo de una entrega: reenviarla (o solo mostrarla), escribir su línea y acusarla. Todo lo que llega se acusa,
/// también lo que el receptor no pudo recibir: una entrega sin acuse queda abierta hasta que la API la cierra al vencer su
/// lease, sin reintentos (D-26).
/// </summary>
public sealed class DeliveryProcessor(
    IDeliveryTarget target,
    ListenerApi api,
    ListenOutput output,
    ExponentialBackoff ackBackoff,
    TimeProvider timeProvider)
{
    /// <param name="claimed">La entrega, su sesión y el plazo de su acuse.</param>
    /// <param name="cancellationToken">Ctrl+C o el fin de la escucha.</param>
    public async Task ProcessAsync(ClaimedDelivery claimed, CancellationToken cancellationToken)
    {
        var outcome = await target.DeliverAsync(claimed.Delivery, cancellationToken).ConfigureAwait(false);
        output.WriteDelivery(claimed.Delivery, outcome);
        await AcknowledgeAsync(claimed, outcome.ToAck(claimed.Session.SessionId), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Un acuse que falla por la red o con un 5xx se reintenta con backoff mientras la API lo siga esperando; repetirlo es
    /// seguro, porque uno que ya quedó registrado responde 409. Cualquier otro rechazo se informa y la escucha sigue.
    /// </summary>
    private async Task AcknowledgeAsync(ClaimedDelivery claimed, CliDeliveryAck ack, CancellationToken cancellationToken)
    {
        var deliveryId = claimed.Delivery.DeliveryId;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await api.AckAsync(claimed.Session, deliveryId, ack, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (CliException failure) when (failure.IsTransient && RetryIn(attempt, claimed.AckDeadline) is { } retryIn)
            {
                await Task.Delay(retryIn, timeProvider, cancellationToken).ConfigureAwait(false);
            }
            // 409: el acuse llegó tarde (la API ya cerró la entrega) o es de una sesión anterior. No hay nada que la persona
            // pueda corregir, así que solo se informa con --verbose.
            catch (ApiException notAwaiting) when (notAwaiting.StatusCode == HttpStatusCode.Conflict)
            {
                output.WriteAckNotRecorded(deliveryId, notAwaiting, verboseOnly: true);
                return;
            }
            catch (CliException failure)
            {
                output.WriteAckNotRecorded(deliveryId, failure, verboseOnly: false);
                return;
            }
        }
    }

    // Solo si el reintento sale antes de que venza el lease: después la API responde 409 de todos modos.
    private TimeSpan? RetryIn(int attempt, DateTimeOffset ackDeadline)
    {
        var retryIn = ackBackoff.Delay(attempt);
        return timeProvider.GetUtcNow() + retryIn < ackDeadline ? retryIn : null;
    }
}
