using System.CommandLine;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Webhooks;

/// <summary><c>fiscalapi webhooks retrieve &lt;id&gt;</c>: <c>GET /api/v4/webhooks/{id}</c>.</summary>
public sealed class RetrieveWebhookCommand : Command
{
    private readonly Argument<string> _id = new("id") { Description = "Id del webhook." };
    private readonly ApiCommandContext _context;

    public RetrieveWebhookCommand(ApiCommandContext context)
        : base("retrieve", "Muestra un webhook, sin su secreto.")
    {
        _context = context;
        Arguments.Add(_id);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = _context.Settings(parseResult);
        var data = await new WebhooksApi(_context.Client(settings)).RetrieveAsync(parseResult.GetValue(_id)!, cancellationToken);
        _context.WriteData(settings, data, FiscalApiJsonContext.Default.Webhook, WebhookView.WriteDetail);
        return ExitCodes.Success;
    }
}
