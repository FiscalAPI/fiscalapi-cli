using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Listen;

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
        MaxInFlightDeliveries: 16,
        DeliveryLease: TimeSpan.FromMinutes(2),
        ShutdownAckTimeout: TimeSpan.FromSeconds(2));
}
