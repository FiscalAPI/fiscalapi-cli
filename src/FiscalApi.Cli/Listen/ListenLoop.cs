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
        // Cada cuenta se reinicia con el primer long-polling exitoso.
        var connectionFailures = 0;
        var busyReopens = 0;
        var expirationsInARow = 0;
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
            // pasó mientras tanto no llega (D-26) y se recupera con events resend. El primero reabre de inmediato; otro
            // sin un long-polling exitoso de por medio espera el backoff, para no quedar girando.
            catch (ApiException expired) when (expired.StatusCode == HttpStatusCode.Gone)
            {
                output.WriteSessionExpired();
                session = null;
                if (++expirationsInARow > 1)
                {
                    await WaitAsync(timings.Reconnect.Delay(expirationsInARow - 1), cancellationToken).ConfigureAwait(false);
                }

                continue;
            }
            // Con la sesión nula falló la reapertura, no el long-polling: su 409 dice que otra apertura de la misma persona
            // tiene tomado el candado de escritura de sus webhooks en el back, y otro intento lo resuelve. El 409 del long-polling
            // (otra sesión reemplazó a esta) no entra aquí y termina la escucha con el texto de la API.
            catch (ApiException busy) when (session is null && busy.StatusCode == HttpStatusCode.Conflict)
            {
                var retryIn = timings.Reconnect.Delay(++busyReopens);
                output.WriteReopenRetry(busy, retryIn);
                await WaitAsync(retryIn, cancellationToken).ConfigureAwait(false);
                continue;
            }
            // Una red caída o un 5xx (un deploy de la API) no terminan la escucha: se reintenta con backoff y jitter. Si
            // el corte pasa de 60 s, la sesión caduca y el siguiente long-polling lo dice con un 410.
            catch (CliException failure) when (failure.IsTransient)
            {
                var retryIn = timings.Reconnect.Delay(++connectionFailures);
                output.WriteConnectionLost(failure, retryIn);
                await WaitAsync(retryIn, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (connectionFailures > 0)
            {
                output.WriteReconnected();
            }

            connectionFailures = busyReopens = expirationsInARow = 0;
            Dispatch(dispatcher, session, deliveries);
        }
    }

    private Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, timeProvider, cancellationToken);

    private void Dispatch(DeliveryDispatcher dispatcher, CliListenerSession session, IReadOnlyList<CliDelivery> deliveries)
    {
        dispatcher.ThrowIfFaulted();
        // El lease corre desde que la API reclamó las entregas, justo antes de responder: se cuenta desde la respuesta.
        var ackDeadline = timeProvider.GetUtcNow() + timings.DeliveryLease;
        foreach (var delivery in deliveries)
        {
            var claimed = new ClaimedDelivery(session, delivery, ackDeadline);
            dispatcher.Enqueue(delivery.OrderingKey, token => processor.ProcessAsync(claimed, token));
        }
    }
}
