using System.CommandLine;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands;

public sealed class TriggerCommand : Command
{
    private readonly Argument<string> _type = new("type")
    {
        Description = $"Event type: {string.Join(", ", WebhookEventTypes.Subscribable)}. The API validates it.",
        HelpName = "type"
    };

    private readonly Option<string> _relatedObjectId = new("--related-object-id")
    {
        Description = "Resource ID for the event (data.relatedObject.id). Defaults to a synthetic ID.",
        HelpName = "id"
    };

    private readonly Option<string> _personId = new("--person-id")
    {
        Description = "Tenant person that owns the resource (event personId). Defaults to none.",
        HelpName = "id"
    };

    private readonly Option<string> _webhook = new("--webhook")
    {
        Description = "ID of the webhook that receives the event. Defaults to your listen session.",
        HelpName = "id"
    };

    private readonly ApiCommandContext _context;

    public TriggerCommand(GlobalOptions globalOptions, FiscalApiClientFactory apiClients, CliConsole console)
        : base("trigger", "Send a test event to your listen session or to a webhook. Test environment only.")
    {
        _context = new ApiCommandContext(globalOptions, apiClients, console);
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
            parseResult.GetRequiredValue(_type),
            parseResult.GetValue(_relatedObjectId),
            parseResult.GetValue(_personId),
            parseResult.GetValue(_webhook));

        var data = await new EventsApi(_context.Client(settings)).TriggerAsync(request, cancellationToken);
        _context.WriteData(settings, data, FiscalApiJsonContext.Default.WebhookEventTrigger, WebhookEventView.WriteTrigger);
        return ExitCodes.Success;
    }
}
