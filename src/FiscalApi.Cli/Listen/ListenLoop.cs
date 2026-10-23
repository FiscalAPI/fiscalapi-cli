using System.Net;
using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Listen;

public sealed class ListenLoop(
    ListenerApi api,
    IReadOnlyList<string> enabledEvents,
    DeliveryProcessor processor,
    ListenOutput output,
    ListenTimings timings,
    TimeProvider timeProvider)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        CliListenerSession? session = await api.OpenAsync(enabledEvents, cancellationToken).ConfigureAwait(false);
        output.WriteReady(session);

        await using var dispatcher = new DeliveryDispatcher(timings.MaxInFlightDeliveries, cancellationToken);
        var connectionFailures = 0;
        var busyReopens = 0;
        var expirationsInARow = 0;
        while (true)
        {
            IReadOnlyList<CliDelivery> deliveries;
            try
            {
                session ??= await api.OpenAsync(enabledEvents, cancellationToken).ConfigureAwait(false);
                deliveries = await api.PollAsync(session, cancellationToken).ConfigureAwait(false);
            }
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
            catch (ApiException busy) when (session is null && busy.StatusCode == HttpStatusCode.Conflict)
            {
                var retryIn = timings.Reconnect.Delay(++busyReopens);
                output.WriteReopenRetry(busy, retryIn);
                await WaitAsync(retryIn, cancellationToken).ConfigureAwait(false);
                continue;
            }
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
        var ackDeadline = timeProvider.GetUtcNow() + timings.DeliveryLease;
        foreach (var delivery in deliveries)
        {
            var claimed = new ClaimedDelivery(session, delivery, ackDeadline);
            dispatcher.Enqueue(delivery.OrderingKey, token => processor.ProcessAsync(claimed, token));
        }
    }
}
