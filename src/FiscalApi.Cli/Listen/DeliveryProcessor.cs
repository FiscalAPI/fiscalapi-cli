using System.Net;
using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Listen;

public sealed class DeliveryProcessor(
    IDeliveryTarget target,
    ListenerApi api,
    ListenOutput output,
    ListenTimings timings,
    TimeProvider timeProvider)
{
    public async Task ProcessAsync(ClaimedDelivery claimed, CancellationToken cancellationToken)
    {
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
        }
    }

    private TimeSpan? RetryIn(int attempt, DateTimeOffset ackDeadline)
    {
        var retryIn = timings.Reconnect.Delay(attempt);
        return timeProvider.GetUtcNow() + retryIn < ackDeadline ? retryIn : null;
    }
}
