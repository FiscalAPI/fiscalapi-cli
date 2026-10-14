using System.CommandLine;
using System.CommandLine.Parsing;
using FiscalApi.Cli.Errors;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Webhooks;

/// <summary>
/// <c>fiscalapi webhooks update &lt;id&gt; [--url] [--description] [--events] [--person…] [--enabled|--disabled]</c>. El
/// <c>PUT /api/v4/webhooks/{id}</c> reemplaza todo el webhook: primero se lee con <c>GET</c> y solo cambia lo que la persona
/// escribió, para no borrar lo demás.
/// </summary>
public sealed class UpdateWebhookCommand : Command
{
    // El texto de la API (WebhookMessages.CliListenerNotEditable).
    private const string CliListenerNotEditable =
        "Una escucha de CLI no se edita ni se prueba; usa fiscalapi listen y fiscalapi trigger.";

    private readonly Argument<string> _id = new("id") { Description = "Id del webhook." };
    private readonly WebhookOptions _webhook = new(required: false);

    private readonly Option<bool> _enabled = new("--enabled")
    {
        Description = "Habilita el webhook; limpia el motivo y la fecha de la deshabilitación."
    };

    private readonly Option<bool> _disabled = new("--disabled")
    {
        Description = "Deshabilita el webhook y cierra sus entregas pendientes."
    };

    private readonly ApiCommandContext _context;

    public UpdateWebhookCommand(ApiCommandContext context)
        : base("update", "Cambia la URL, la descripción, los tipos de evento, las personas o el estado de un webhook.")
    {
        _context = context;
        Arguments.Add(_id);
        _webhook.AddTo(this);
        Options.Add(_enabled);
        Options.Add(_disabled);
        Validators.Add(RequireOneChange);
        Validators.Add(RejectEnabledWithDisabled);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = _context.Settings(parseResult);
        var api = new WebhooksApi(_context.Client(settings));
        var current = ApiCommandContext.Read(
            await api.RetrieveAsync(parseResult.GetValue(_id)!, cancellationToken), FiscalApiJsonContext.Default.Webhook);

        // La API también lo rechaza, pero su validación de la URL (vacía en una escucha) respondería antes y confundiría.
        // Es el estado del recurso, no el uso del comando: sale con 1, como el 400 de la API.
        if (current.IsCliListener)
        {
            throw new ResourceStateException(CliListenerNotEditable);
        }

        var data = await api.ReplaceAsync(Changes(parseResult).ApplyTo(current), cancellationToken);
        _context.WriteData(settings, data, FiscalApiJsonContext.Default.Webhook, WebhookView.WriteDetail);
        return ExitCodes.Success;
    }

    private WebhookChanges Changes(ParseResult parseResult) => new(
        WebhookOptions.GivenValue(parseResult, _webhook.Url),
        WebhookOptions.GivenValue(parseResult, _webhook.Description),
        WebhookOptions.GivenValue(parseResult, _webhook.Events),
        // --all-persons es la lista vacía, que la API lee como "todas las personas".
        parseResult.GetValue(_webhook.AllPersons) ? [] : WebhookOptions.GivenValue(parseResult, _webhook.Persons),
        parseResult.GetValue(_enabled) ? WebhookStatuses.Enabled
        : parseResult.GetValue(_disabled) ? WebhookStatuses.Disabled
        : null);

    // Sin cambios, el PUT reescribiría lo mismo: casi siempre es una opción olvidada.
    private void RequireOneChange(CommandResult result)
    {
        Option[] changes =
            [_webhook.Url, _webhook.Description, _webhook.Events, _webhook.Persons, _webhook.AllPersons, _enabled, _disabled];
        if (!changes.Any(option => result.GetResult(option) is { Implicit: false }))
        {
            result.AddError(
                "Indica qué cambiar: --url, --description, --events, --person, --all-persons, --enabled o --disabled.");
        }
    }

    private void RejectEnabledWithDisabled(CommandResult result)
    {
        if (result.GetResult(_enabled) is { Implicit: false } && result.GetResult(_disabled) is { Implicit: false })
        {
            result.AddError("--enabled y --disabled no se combinan.");
        }
    }
}
