using System.CommandLine;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Webhooks;

public sealed class ListWebhooksCommand : Command
{
    private readonly PageOptions _page = new();
    private readonly ApiCommandContext _context;

    public ListWebhooksCommand(ApiCommandContext context)
        : base("list", "List the tenant's webhooks, without their signing secrets.")
    {
        _context = context;
        _page.AddTo(this);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = _context.Settings(parseResult);
        var data = await new WebhooksApi(_context.Client(settings)).ListAsync(_page.Read(parseResult), cancellationToken);
        _context.WriteData(settings, data, FiscalApiJsonContext.Default.PagedListWebhook, WebhookView.WriteList);
        return ExitCodes.Success;
    }
}
