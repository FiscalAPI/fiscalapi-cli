using System.CommandLine;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Webhooks;

public sealed class RotateWebhookSecretCommand : Command
{
    private readonly Argument<string> _id = new("id") { Description = "ID of the webhook or listen session." };
    private readonly ApiCommandContext _context;

    public RotateWebhookSecretCommand(ApiCommandContext context)
        : base("rotate-secret", "Rotate the signing secret and print the new one once. The old one also signs events for 24 hours.")
    {
        _context = context;
        Arguments.Add(_id);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = _context.Settings(parseResult);
        var data = await new WebhooksApi(_context.Client(settings)).RotateSecretAsync(parseResult.GetRequiredValue(_id), cancellationToken);
        WebhookWithSecretOutput.Write(_context, settings, data, rotated: true);
        return ExitCodes.Success;
    }
}
