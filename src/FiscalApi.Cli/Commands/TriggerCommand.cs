using System.CommandLine;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands;

/// <summary>
/// <c>fiscalapi trigger &lt;type&gt; [--related-object-id &lt;id&gt;] [--person-id &lt;id&gt;] [--webhook &lt;id&gt;]</c>:
/// <c>POST /api/v4/events/trigger</c>. La API genera un evento sintético del tipo y lo entrega a un solo
/// destino: sin <c>--webhook</c>, la escucha de CLI de la persona, así que <c>fiscalapi listen</c> tiene que estar
/// abierto o responde 409. El CLI no valida el tipo ni bloquea <c>--live</c>: esas reglas viven solo en la API, que
/// responde 400 o 403 con su texto.
/// </summary>
public sealed class TriggerCommand : Command
{
    // Con HelpName el uso dice <type>; sin él, System.CommandLine lo reemplaza por la lista entera de sugerencias.
    private readonly Argument<string> _type = new("type")
    {
        Description = $"Tipo de evento: {string.Join(", ", WebhookEventTypes.Subscribable)}. Quien lo valida es la API.",
        HelpName = "type"
    };

    private readonly Option<string> _relatedObjectId = new("--related-object-id")
    {
        Description = "Id del recurso del evento (data.relatedObject.id). Por omisión, un id sintético cuyo GET responde 404.",
        HelpName = "id"
    };

    private readonly Option<string> _personId = new("--person-id")
    {
        Description = "Persona del tenant dueña del recurso (personId del evento). Por omisión, el evento va sin persona.",
        HelpName = "id"
    };

    private readonly Option<string> _webhook = new("--webhook")
    {
        Description = "Id del webhook que recibe el evento. Por omisión, tu escucha de fiscalapi listen.",
        HelpName = "id"
    };

    private readonly ApiCommandContext _context;

    public TriggerCommand(GlobalOptions globalOptions, FiscalApiClientFactory apiClients, CliConsole console)
        : base("trigger", "Dispara un evento de prueba hacia tu escucha de fiscalapi listen o hacia un webhook (solo en pruebas).")
    {
        _context = new ApiCommandContext(globalOptions, apiClients, console);
        // Sugerencias, no AcceptOnlyFromAmong: un tipo nuevo del servidor se puede disparar sin actualizar el CLI.
        _type.CompletionSources.Add([.. WebhookEventTypes.Subscribable]);
        Arguments.Add(_type);
        Options.Add(_relatedObjectId);
        Options.Add(_personId);
        Options.Add(_webhook);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = _context.Settings(parseResult);
        var request = new TriggerWebhookEventRequest(
            parseResult.GetValue(_type)!,
            parseResult.GetValue(_relatedObjectId),
            parseResult.GetValue(_personId),
            parseResult.GetValue(_webhook));

        var data = await new EventsApi(_context.Client(settings)).TriggerAsync(request, cancellationToken);
        _context.WriteData(settings, data, FiscalApiJsonContext.Default.WebhookEventTrigger, WebhookEventView.WriteTrigger);
        return ExitCodes.Success;
    }
}
