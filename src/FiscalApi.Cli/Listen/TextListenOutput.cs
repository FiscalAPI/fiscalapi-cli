using System.Globalization;

namespace FiscalApi.Cli.Listen;

public sealed class TextListenOutput(CliConsole console, bool verbose, TimeProvider timeProvider)
    : ListenOutput(console, verbose, timeProvider)
{
    public override void WriteSecret(string secret) => WriteData(secret);

    public override void WriteDelivery(CliDelivery delivery, DeliveryOutcome outcome)
    {
        var summary = WebhookEventSummary.Of(delivery);
        var time = TimeProvider.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        WriteData($"{time}  {summary.Type} [{summary.Id}]{Result(outcome)}");
    }

    private static string Result(DeliveryOutcome outcome) => outcome switch
    {
        { Forwarded: false } => "",
        { StatusCode: { } statusCode } => $" [{statusCode}]",
        _ => $" [error: {outcome.Error}]"
    };
}
