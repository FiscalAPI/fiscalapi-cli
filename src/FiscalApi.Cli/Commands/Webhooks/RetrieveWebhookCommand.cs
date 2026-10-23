using System.CommandLine;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Webhooks;

public sealed class RetrieveWebhookCommand : Command
{
    private readonly Argument<string> _id = new("id") { Description = "Webhook ID." };
    private readonly ApiCommandContext _context;

    public RetrieveWebhookCommand(ApiCommandContext context)
        : base("retrieve", "Retrieve a webhook, without its signing secret.")
    {
        _context = context;
        Arguments.Add(_id);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = _context.Settings(parseResult);
        var data = await new WebhooksApi(_context.Client(settings)).RetrieveAsync(parseResult.GetRequiredValue(_id), cancellationToken);
        _context.WriteData(settings, data, FiscalApiJsonContext.Default.Webhook, WebhookView.WriteDetail);
        return ExitCodes.Success;
    }
}
