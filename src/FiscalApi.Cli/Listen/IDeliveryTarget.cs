namespace FiscalApi.Cli.Listen;

public interface IDeliveryTarget
{
    Task<DeliveryOutcome> DeliverAsync(CliDelivery delivery, CancellationToken cancellationToken);
}
