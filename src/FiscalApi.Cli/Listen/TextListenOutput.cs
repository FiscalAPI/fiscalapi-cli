using System.Globalization;

namespace FiscalApi.Cli.Listen;

/// <summary>La salida legible: "2026-10-12 09:47:46  --> invoice.canceled [evt_…] [200]", en la hora local.</summary>
public sealed class TextListenOutput(CliConsole console, bool verbose, TimeProvider timeProvider)
    : ListenOutput(console, verbose, timeProvider)
{
    public override void WriteSecret(string secret) => WriteData(secret);

    public override void WriteDelivery(CliDelivery delivery, DeliveryOutcome outcome)
    {
        var summary = WebhookEventSummary.Of(delivery);
        var time = TimeProvider.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        WriteData($"{time}  --> {summary.Type} [{summary.Id}]{Result(outcome)}");
    }

    // Sin --forward-to no hay resultado que mostrar: la terminal es el destino.
    private static string Result(DeliveryOutcome outcome) => outcome switch
    {
        { Forwarded: false } => "",
        { StatusCode: { } statusCode } => $" [{statusCode}]",
        _ => $" [error: {outcome.Error}]"
    };
}
