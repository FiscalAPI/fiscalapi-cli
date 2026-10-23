using System.CommandLine;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Events;

public sealed class ResendEventCommand : Command
{
    private readonly Argument<string> _eventId = new("eventId") { Description = "ID of the event to resend." };

    private readonly Option<string> _webhook = new("--webhook")
    {
        Description = "ID of the webhook that receives the event. Defaults to your listen session.",
        HelpName = "id"
    };

    private readonly ApiCommandContext _context;

    public ResendEventCommand(ApiCommandContext context)
        : base("resend", "Resend an event to a webhook or to your listen session.")
    {
        _context = context;
        Arguments.Add(_eventId);
        Options.Add(_webhook);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = _context.Settings(parseResult);
        var data = await new EventsApi(_context.Client(settings)).ResendAsync(
            parseResult.GetRequiredValue(_eventId), parseResult.GetValue(_webhook), cancellationToken);
        _context.WriteData(settings, data, FiscalApiJsonContext.Default.WebhookEventResend, WebhookEventView.WriteResend);
        return ExitCodes.Success;
    }
}
