using System.Net;
using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Listen;

/// <summary>
/// El bucle de <c>fiscalapi listen</c>: abre la sesión y hace long-polling mientras nadie lo detenga. Cada entrega pasa al
/// <see cref="DeliveryDispatcher"/>, así un receptor lento no frena el siguiente long-polling.
/// </summary>
public sealed class ListenLoop(
    ListenerApi api,
    IReadOnlyList<string> enabledEvents,
    DeliveryProcessor processor,
    ListenOutput output,
    ListenTimings timings,
    TimeProvider timeProvider)
{
    /// <summary>
    /// No termina por sí solo: sale con Ctrl+C (<see cref="OperationCanceledException"/>, código 130) o con un error que no
    /// se resuelve reintentando (un 409 de sesión reemplazada, un 401, un 404), que atrapa <see cref="CliApplication"/>.
    /// Al salir, el despachador cancela los reenvíos en curso y espera a que terminen.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // La primera apertura no se reintenta: un --api-base equivocado o una key sin permiso se informan de inmediato.
        CliListenerSession? session = await api.OpenAsync(enabledEvents, cancellationToken).ConfigureAwait(false);
        output.WriteReady(session);

        await using var dispatcher = new DeliveryDispatcher(timings.MaxInFlightDeliveries, cancellationToken);
        var failedAttempts = 0;
        while (true)
        {
            IReadOnlyList<CliDelivery> deliveries;
            try
            {
                // Misma escucha y mismo secreto: la sesión nueva solo cambia su sessionId.
                session ??= await api.OpenAsync(enabledEvents, cancellationToken).ConfigureAwait(false);
                deliveries = await api.PollAsync(session, cancellationToken).ConfigureAwait(false);
            }
            // 410: la sesión caducó (más de 60 s sin long-polling, p. ej. tras un corte). Se reabre sin terminar; lo que
            // pasó mientras tanto no llega (D-26) y se recupera con events resend. Un 409 (otra sesión la reemplazó) no es
            // transitorio y termina la escucha con el texto de la API.
            catch (ApiException expired) when (expired.StatusCode == HttpStatusCode.Gone)
            {
                output.WriteSessionExpired();
                session = null;
                continue;
            }
            // Una red caída o un 5xx (un deploy de la API) no terminan la escucha: se reintenta con backoff y jitter. Si
            // el corte pasa de 60 s, la sesión caduca y el siguiente long-polling lo dice con un 410.
            catch (CliException failure) when (failure.IsTransient)
            {
                var retryIn = timings.Reconnect.Delay(++failedAttempts);
                output.WriteConnectionLost(failure, retryIn);
                await Task.Delay(retryIn, timeProvider, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (failedAttempts > 0)
            {
                output.WriteReconnected();
                failedAttempts = 0;
            }

            await DispatchAsync(dispatcher, session, deliveries, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DispatchAsync(
        DeliveryDispatcher dispatcher,
        CliListenerSession session,
        IReadOnlyList<CliDelivery> deliveries,
        CancellationToken cancellationToken)
    {
        dispatcher.ThrowIfFaulted();
        // El lease corre desde que la API reclamó las entregas, justo antes de responder: se cuenta desde la respuesta.
        var ackDeadline = timeProvider.GetUtcNow() + timings.DeliveryLease;
        foreach (var delivery in deliveries)
        {
            var claimed = new ClaimedDelivery(session, delivery, ackDeadline);
            await dispatcher.EnqueueAsync(
                delivery.EventId,
                token => processor.ProcessAsync(claimed, token),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
