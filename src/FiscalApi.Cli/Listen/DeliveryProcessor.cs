using System.Net;
using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Listen;

/// <summary>
/// El trabajo de una entrega: reenviarla (o solo mostrarla), escribir su línea y acusarla. Todo lo que tiene resultado se
/// acusa, también lo que el receptor no pudo recibir: una entrega sin acuse queda abierta hasta que la API la cierra al
/// vencer su lease, sin reintentos.
/// </summary>
public sealed class DeliveryProcessor(
    IDeliveryTarget target,
    ListenerApi api,
    ListenOutput output,
    ListenTimings timings,
    TimeProvider timeProvider)
{
    /// <param name="claimed">La entrega, su sesión y el plazo de su acuse.</param>
    /// <param name="cancellationToken">Ctrl+C o el fin de la escucha: cancela el reenvío en curso.</param>
    public async Task ProcessAsync(ClaimedDelivery claimed, CancellationToken cancellationToken)
    {
        // Con el lease vencido la API ya la cerró por AckTimeout: reenviarla tarde no se registraría y su firma podría
        // salir de la tolerancia de 5 min del receptor.
        if (timeProvider.GetUtcNow() >= claimed.AckDeadline)
        {
            output.WriteExpired(claimed.Delivery);
            return;
        }

        var outcome = await target.DeliverAsync(claimed.Delivery, cancellationToken).ConfigureAwait(false);
        output.WriteDelivery(claimed.Delivery, outcome);
        var ack = outcome.ToAck(claimed.Session.SessionId);
        try
        {
            await AcknowledgeAsync(claimed, ack, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await AcknowledgeOnShutdownAsync(claimed, ack).ConfigureAwait(false);
        }
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

    /// <summary>
    /// Ctrl+C con el resultado ya en la mano: un solo intento, con un plazo propio y corto, para que la entrega no quede
    /// como AckTimeout. Si tampoco llega, la API la cierra así; la persona ya pidió salir.
    /// </summary>
    private async Task AcknowledgeOnShutdownAsync(ClaimedDelivery claimed, CliDeliveryAck ack)
    {
        using var deadline = new CancellationTokenSource(timings.ShutdownAckTimeout, timeProvider);
        try
        {
            await api.AckAsync(claimed.Session, claimed.Delivery.DeliveryId, ack, deadline.Token).ConfigureAwait(false);
        }
        catch (CliException failure)
        {
            output.WriteAckNotRecorded(claimed.Delivery.DeliveryId, failure, verboseOnly: true);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            // Sin respuesta en el plazo: la API la cierra por AckTimeout.
        }
    }

    // Solo si el reintento sale antes de que venza el lease: después la API responde 409 de todos modos.
    private TimeSpan? RetryIn(int attempt, DateTimeOffset ackDeadline)
    {
        var retryIn = timings.Reconnect.Delay(attempt);
        return timeProvider.GetUtcNow() + retryIn < ackDeadline ? retryIn : null;
    }
}
