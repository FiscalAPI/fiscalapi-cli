using System.CommandLine;
using System.CommandLine.Parsing;
using FiscalApi.Cli.Errors;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Webhooks;

public sealed class UpdateWebhookCommand : Command
{
    private const string CliListenerNotEditable =
        "A listen session cannot be updated. Use `fiscalapi listen` and `fiscalapi trigger` instead.";

    private readonly Argument<string> _id = new("id") { Description = "Webhook ID." };
    private readonly WebhookOptions _webhook = new(required: false);

    private readonly Option<bool> _enabled = new("--enabled")
    {
        Description = "Enable the webhook and clear its disabled reason and date."
    };

    private readonly Option<bool> _disabled = new("--disabled")
    {
        Description = "Disable the webhook and close its pending deliveries."
    };

    private readonly ApiCommandContext _context;

    public UpdateWebhookCommand(ApiCommandContext context)
        : base("update", "Update the URL, description, event types, persons or status of a webhook.")
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
            await api.RetrieveAsync(parseResult.GetRequiredValue(_id), cancellationToken), FiscalApiJsonContext.Default.Webhook);

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
        parseResult.GetValue(_webhook.AllPersons) ? [] : WebhookOptions.GivenValue(parseResult, _webhook.Persons),
        parseResult.GetValue(_enabled) ? WebhookStatuses.Enabled
        : parseResult.GetValue(_disabled) ? WebhookStatuses.Disabled
        : null);

    private void RequireOneChange(CommandResult result)
    {
        Option[] changes =
            [_webhook.Url, _webhook.Description, _webhook.Events, _webhook.Persons, _webhook.AllPersons, _enabled, _disabled];
        if (!changes.Any(option => result.GetResult(option) is { Implicit: false }))
        {
            result.AddError(
                "Specify what to update: --url, --description, --events, --person, --all-persons, --enabled or --disabled.");
        }
    }

    private void RejectEnabledWithDisabled(CommandResult result)
    {
        if (result.GetResult(_enabled) is { Implicit: false } && result.GetResult(_disabled) is { Implicit: false })
        {
            result.AddError("--enabled cannot be used with --disabled.");
        }
    }
}
