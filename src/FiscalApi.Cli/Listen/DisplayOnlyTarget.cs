namespace FiscalApi.Cli.Listen;

/// <summary>Sin <c>--forward-to</c>: la entrega solo se muestra en la terminal.</summary>
public sealed class DisplayOnlyTarget : IDeliveryTarget
{
    public static DisplayOnlyTarget Instance { get; } = new();

    private DisplayOnlyTarget()
    {
    }

    public Task<DeliveryOutcome> DeliverAsync(CliDelivery delivery, CancellationToken cancellationToken) =>
        Task.FromResult(DeliveryOutcome.NotForwarded);
}
