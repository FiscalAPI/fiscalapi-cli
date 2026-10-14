namespace FiscalApi.Cli.Listen;

/// <summary>Sin <c>--forward-to</c>: la entrega solo se muestra en la terminal.</summary>
public sealed class DisplayOnlyTarget : IDeliveryTarget
{
    // Asumido (CLI-004): la terminal es el destino y la entrega llegó. Se acusa como un 200 inmediato para que el log de
    // entregas no la muestre fallida.
    private static readonly Task<DeliveryOutcome> Displayed =
        Task.FromResult(new DeliveryOutcome(StatusCode: 200, Error: null, LatencyMs: 0, Forwarded: false));

    public static DisplayOnlyTarget Instance { get; } = new();

    private DisplayOnlyTarget()
    {
    }

    public Task<DeliveryOutcome> DeliverAsync(CliDelivery delivery, CancellationToken cancellationToken) => Displayed;
}
