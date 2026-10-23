using System.CommandLine;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Events;

public sealed class RetrieveEventCommand : Command
{
    private readonly Argument<string> _id = new("id") { Description = "Event ID: the webhook-id it was delivered with." };
    private readonly ApiCommandContext _context;

    public RetrieveEventCommand(ApiCommandContext context)
        : base("retrieve", "Print the event as the API returns it.")
    {
        _context = context;
        Arguments.Add(_id);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = _context.Settings(parseResult);
        _context.WriteRaw(await new EventsApi(_context.Client(settings)).RetrieveAsync(parseResult.GetRequiredValue(_id), cancellationToken));
        return ExitCodes.Success;
    }
}
