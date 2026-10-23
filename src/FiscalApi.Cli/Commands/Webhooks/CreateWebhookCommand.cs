using System.CommandLine;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Webhooks;

public sealed class CreateWebhookCommand : Command
{
    private readonly WebhookOptions _webhook = new(required: true);
    private readonly ApiCommandContext _context;

    public CreateWebhookCommand(ApiCommandContext context)
        : base("create", "Create a webhook and print its signing secret once.")
    {
        _context = context;
        _webhook.AddTo(this);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = _context.Settings(parseResult);
        var request = new CreateWebhookRequest(
            parseResult.GetRequiredValue(_webhook.Url),
            WebhookOptions.GivenValue(parseResult, _webhook.Description),
            parseResult.GetRequiredValue(_webhook.Events),
            WebhookOptions.GivenValue(parseResult, _webhook.Persons));

        var data = await new WebhooksApi(_context.Client(settings)).CreateAsync(request, cancellationToken);
        WebhookWithSecretOutput.Write(_context, settings, data, rotated: false);
        return ExitCodes.Success;
    }
}
