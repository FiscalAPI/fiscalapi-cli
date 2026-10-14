namespace FiscalApi.Cli.Listen;

/// <summary>Una entrega que trajo el long-polling, con la sesión que la reclamó y hasta cuándo la API espera su acuse.</summary>
/// <param name="Session">La sesión del long-polling que la trajo: su acuse lleva ese <c>sessionId</c>.</param>
/// <param name="Delivery">La entrega tal como llegó.</param>
/// <param name="AckDeadline">Cuándo vence su lease: después, la API ya la cerró sin acuse.</param>
public sealed record ClaimedDelivery(CliListenerSession Session, CliDelivery Delivery, DateTimeOffset AckDeadline);
