namespace FiscalApi.Cli.Listen;

public sealed record ClaimedDelivery(CliListenerSession Session, CliDelivery Delivery, DateTimeOffset AckDeadline);
