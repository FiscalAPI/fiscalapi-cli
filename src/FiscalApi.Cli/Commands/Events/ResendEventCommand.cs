using System.CommandLine;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Events;

/// <summary>
/// <c>fiscalapi events resend &lt;eventId&gt; [--webhook &lt;id&gt;]</c>: <c>POST /api/v4/events/{id}/resend</c>.
/// Sin <c>--webhook</c>, la API lo entrega a la escucha de CLI de la persona: <c>fiscalapi listen</c> tiene
/// que estar abierto, o responde 409.
/// </summary>
public sealed class ResendEventCommand : Command
{
    private readonly Argument<string> _eventId = new("eventId") { Description = "Id del evento que se reenvía." };

    private readonly Option<string> _webhook = new("--webhook")
    {
        Description = "Id del webhook que recibe el reenvío. Por omisión, tu escucha de fiscalapi listen.",
        HelpName = "id"
    };

    private readonly ApiCommandContext _context;

    public ResendEventCommand(ApiCommandContext context)
        : base("resend", "Reenvía un evento a un webhook o a tu escucha de fiscalapi listen.")
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
            parseResult.GetValue(_eventId)!, parseResult.GetValue(_webhook), cancellationToken);
        _context.WriteData(settings, data, FiscalApiJsonContext.Default.WebhookEventResend, WebhookEventView.WriteResend);
        return ExitCodes.Success;
    }
}
