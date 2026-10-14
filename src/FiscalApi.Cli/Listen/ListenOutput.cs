using System.Globalization;
using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Listen;

/// <summary>
/// Lo que <c>listen</c> escribe en la terminal. Los datos (el secreto de <c>--print-secret</c> y una línea por entrega) van
/// a stdout con el formato de cada subclase; los avisos para la persona van a stderr como texto, también con
/// <c>--json</c> (contrato común). Las entregas terminan en paralelo, así que cada línea se escribe completa bajo un
/// candado.
/// </summary>
public abstract class ListenOutput(CliConsole console, bool verbose, TimeProvider timeProvider)
{
    private readonly Lock _gate = new();

    protected TimeProvider TimeProvider => timeProvider;

    public static ListenOutput For(GlobalSettings settings, CliConsole console, TimeProvider timeProvider) =>
        settings.Json
            ? new JsonListenOutput(console, settings.Verbose, timeProvider)
            : new TextListenOutput(console, settings.Verbose, timeProvider);

    /// <summary><c>--print-secret</c>: el secreto solo, para usarlo en un script.</summary>
    public abstract void WriteSecret(string secret);

    /// <summary>Una entrega con lo que pasó al reenviarla.</summary>
    public abstract void WriteDelivery(CliDelivery delivery, DeliveryOutcome outcome);

    /// <summary>
    /// El texto aprobado con el secreto. Es el único lugar, junto con <c>--print-secret</c>, donde se escribe el
    /// secreto; va a stderr porque es para la persona, no un dato del flujo de eventos.
    /// </summary>
    public void WriteReady(CliListenerSession session) =>
        WriteDiagnostic($"Listo. Tu secreto de firma es {session.Secret} (escucha {session.Id})");

    public void WriteConnectionLost(CliException failure, TimeSpan retryIn) =>
        WriteDiagnostic($"Se perdió la conexión con FiscalAPI ({Detail(failure)}); reintentando en {Seconds(retryIn)} s.");

    public void WriteReconnected() => WriteDiagnostic("Conexión con FiscalAPI restablecida.");

    public void WriteSessionExpired() => WriteDiagnostic("La sesión de fiscalapi listen caducó; se abre una nueva.");

    public void WriteReopenRetry(CliException failure, TimeSpan retryIn) =>
        WriteDiagnostic($"No se pudo reabrir la sesión de escucha ({Detail(failure)}); reintentando en {Seconds(retryIn)} s.");

    /// <summary>Una entrega que llegó a su turno con el lease vencido: la API ya la cerró y no se reenvía.</summary>
    public void WriteExpired(CliDelivery delivery) =>
        WriteDiagnostic(
            $"La entrega {delivery.DeliveryId} ({delivery.EventId ?? WebhookEventSummary.Unknown}) venció antes de " +
            "reenviarse; la API la cerró sin acuse.");

    /// <param name="deliveryId">La entrega cuyo acuse no quedó registrado.</param>
    /// <param name="failure">Lo que respondió la API o por qué no respondió.</param>
    /// <param name="verboseOnly">Si solo se escribe con <c>--verbose</c>.</param>
    public void WriteAckNotRecorded(string deliveryId, CliException failure, bool verboseOnly)
    {
        if (verbose || !verboseOnly)
        {
            WriteDiagnostic($"No se registró el acuse de la entrega {deliveryId}: {failure.Message}");
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
