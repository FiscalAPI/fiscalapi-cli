namespace FiscalApi.Cli.Listen;

/// <summary>A dónde va cada entrega que trae el long-polling.</summary>
public interface IDeliveryTarget
{
    /// <summary>Entrega y responde lo que pasó. Solo lanza si se canceló <paramref name="cancellationToken"/>.</summary>
    Task<DeliveryOutcome> DeliverAsync(CliDelivery delivery, CancellationToken cancellationToken);
}
