namespace FiscalApi.Cli.Listen;

public sealed class DisplayOnlyTarget : IDeliveryTarget
{
    private static readonly Task<DeliveryOutcome> Displayed =
        Task.FromResult(new DeliveryOutcome(StatusCode: 200, Error: null, LatencyMs: 0, Forwarded: false));

    public static DisplayOnlyTarget Instance { get; } = new();

    private DisplayOnlyTarget()
    {
    }

    public Task<DeliveryOutcome> DeliverAsync(CliDelivery delivery, CancellationToken cancellationToken) => Displayed;
}
