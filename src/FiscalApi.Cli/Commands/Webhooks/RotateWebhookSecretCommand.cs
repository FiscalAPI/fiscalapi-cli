using System.CommandLine;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Webhooks;

/// <summary>
/// <c>fiscalapi webhooks rotate-secret &lt;id&gt;</c>: <c>POST /api/v4/webhooks/{id}/rotate-secret</c>. Imprime el secreto
/// nuevo una sola vez; el anterior sigue firmando 24 horas.
/// </summary>
public sealed class RotateWebhookSecretCommand : Command
{
    private readonly Argument<string> _id = new("id") { Description = "Id del webhook (o de tu escucha de CLI)." };
    private readonly ApiCommandContext _context;

    public RotateWebhookSecretCommand(ApiCommandContext context)
        : base("rotate-secret", "Genera un secreto de firma nuevo y lo imprime una sola vez; el anterior sigue firmando 24 horas.")
    {
        _context = context;
        Arguments.Add(_id);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = _context.Settings(parseResult);
        var data = await new WebhooksApi(_context.Client(settings)).RotateSecretAsync(parseResult.GetValue(_id)!, cancellationToken);
        var webhook = ApiCommandContext.Read(data, FiscalApiJsonContext.Default.Webhook);
        _context.WriteData(settings, data, webhook, WebhookView.WriteDetail);
        WebhookView.WriteSigningSecret(_context.Console, webhook, settings.Json, rotated: true);
        return ExitCodes.Success;
    }
}
