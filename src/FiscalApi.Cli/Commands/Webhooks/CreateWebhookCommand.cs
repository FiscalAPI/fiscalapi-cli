using System.CommandLine;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Webhooks;

/// <summary>
/// <c>fiscalapi webhooks create --url &lt;url&gt; --events &lt;tipos&gt; [--description &lt;texto&gt;] [--person &lt;id&gt;…]</c>:
/// <c>POST /api/v4/webhooks</c>. Imprime el secreto de firma una sola vez, con el aviso de guardarlo.
/// </summary>
public sealed class CreateWebhookCommand : Command
{
    private readonly WebhookOptions _webhook = new(required: true);
    private readonly ApiCommandContext _context;

    public CreateWebhookCommand(ApiCommandContext context)
        : base("create", "Da de alta un webhook e imprime su secreto de firma una sola vez.")
    {
        _context = context;
        _webhook.AddTo(this);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = _context.Settings(parseResult);
        var request = new CreateWebhookRequest(
            parseResult.GetValue(_webhook.Url)!,
            WebhookOptions.GivenValue(parseResult, _webhook.Description),
            parseResult.GetValue(_webhook.Events)!,
            // Sin --person (o con --all-persons, que no se combina con él) no viaja: recibe los eventos de todas.
            WebhookOptions.GivenValue(parseResult, _webhook.Persons));

        var data = await new WebhooksApi(_context.Client(settings)).CreateAsync(request, cancellationToken);
        WebhookWithSecretOutput.Write(_context, settings, data, rotated: false);
        return ExitCodes.Success;
    }
}
