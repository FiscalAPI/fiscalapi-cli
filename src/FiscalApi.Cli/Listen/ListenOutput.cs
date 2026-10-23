using System.Globalization;
using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Listen;

public abstract class ListenOutput(CliConsole console, bool verbose, TimeProvider timeProvider)
{
    private readonly Lock _gate = new();

    protected TimeProvider TimeProvider => timeProvider;

    public static ListenOutput For(GlobalSettings settings, CliConsole console, TimeProvider timeProvider) =>
        settings.Json
            ? new JsonListenOutput(console, settings.Verbose, timeProvider)
            : new TextListenOutput(console, settings.Verbose, timeProvider);

    public abstract void WriteSecret(string secret);

    public abstract void WriteDelivery(CliDelivery delivery, DeliveryOutcome outcome);

    public void WriteReady(CliListenerSession session) =>
        WriteDiagnostic($"Ready. Your signing secret is {session.Secret} (listen session {session.Id}).");

    public void WriteConnectionLost(CliException failure, TimeSpan retryIn) =>
        WriteDiagnostic($"Lost connection to FiscalAPI ({Detail(failure)}). Retrying in {Seconds(retryIn)} s.");

    public void WriteReconnected() => WriteDiagnostic("Reconnected to FiscalAPI.");

    public void WriteSessionExpired() => WriteDiagnostic("The listen session expired. Opening a new one.");

    public void WriteReopenRetry(CliException failure, TimeSpan retryIn) =>
        WriteDiagnostic($"Could not reopen the listen session ({Detail(failure)}). Retrying in {Seconds(retryIn)} s.");

    public void WriteExpired(CliDelivery delivery) =>
        WriteDiagnostic(
            $"Delivery {delivery.DeliveryId} ({delivery.EventId ?? WebhookEventSummary.Unknown}) expired before " +
            "forwarding. The API closed it unacknowledged.");

    public void WriteAckNotRecorded(string deliveryId, CliException failure, bool verboseOnly)
    {
        if (verbose || !verboseOnly)
        {
            WriteDiagnostic($"Could not acknowledge delivery {deliveryId}: {failure.Message}");
        }
    }

    protected void WriteData(string line) => WriteLine(console.Out, line);

    private static string Detail(CliException failure) => failure.Message.TrimEnd('.');

    private static string Seconds(TimeSpan delay) => delay.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture);

    private void WriteDiagnostic(string line) => WriteLine(console.Error, line);

    private void WriteLine(TextWriter writer, string line)
    {
        lock (_gate)
        {
            writer.WriteLine(line);
        }
    }
}
