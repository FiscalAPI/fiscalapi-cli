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
    /// El texto aprobado (D-21) con el secreto. Es el único lugar, junto con <c>--print-secret</c>, donde se escribe el
    /// secreto; va a stderr porque es para la persona, no un dato del flujo de eventos.
    /// </summary>
    public void WriteReady(CliListenerSession session) =>
        WriteDiagnostic($"Listo. Tu secreto de firma es {session.Secret} (escucha {session.Id})");

    public void WriteConnectionLost(CliException failure, TimeSpan retryIn) =>
        WriteDiagnostic(
            $"Se perdió la conexión con FiscalAPI ({failure.Message.TrimEnd('.')}); reintentando en " +
            $"{retryIn.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s.");

    public void WriteReconnected() => WriteDiagnostic("Conexión con FiscalAPI restablecida.");

    public void WriteSessionExpired() => WriteDiagnostic("La sesión de fiscalapi listen caducó; se abre una nueva.");

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

    private void WriteDiagnostic(string line) => WriteLine(console.Error, line);

    private void WriteLine(TextWriter writer, string line)
    {
        lock (_gate)
        {
            writer.WriteLine(line);
        }
    }
}
