using System.CommandLine;
using FiscalApi.Cli.Errors;
using FiscalApi.Cli.Input;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Webhooks;

/// <summary>
/// <c>fiscalapi webhooks delete &lt;id&gt; [--yes]</c>: <c>DELETE /api/v4/webhooks/{id}</c>. Pide confirmación en la
/// terminal; sin una persona que responda (stdin redirigido) o con <c>--json</c> (un script), exige <c>--yes</c>.
/// </summary>
public sealed class DeleteWebhookCommand : Command
{
    private readonly Argument<string> _id = new("id") { Description = "Id del webhook (o de tu escucha de CLI)." };

    private readonly Option<bool> _yes = new("--yes")
    {
        Description = "Borra sin pedir confirmación (necesario en scripts y con --json)."
    };

    private readonly ApiCommandContext _context;
    private readonly IConfirmationPrompt _confirmation;

    public DeleteWebhookCommand(ApiCommandContext context, IConfirmationPrompt confirmation)
        : base("delete", "Da de baja un webhook y cierra sus entregas pendientes; su historial se conserva.")
    {
        _context = context;
        _confirmation = confirmation;
        Arguments.Add(_id);
        Options.Add(_yes);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = _context.Settings(parseResult);
        var id = parseResult.GetValue(_id)!;
        if (!parseResult.GetValue(_yes))
        {
            if (settings.Json || !_confirmation.IsInteractive)
            {
                throw new UsageException("Para borrar sin confirmación interactiva usa --yes.");
            }

            if (!await _confirmation.ConfirmAsync(
                    $"¿Borrar el webhook {id}? Sus entregas pendientes se cerrarán.", cancellationToken))
            {
                // La persona decidió no borrar: no es un error.
                await _context.Console.Error.WriteLineAsync($"No se borró el webhook {id}.");
                return ExitCodes.Success;
            }
        }

        var data = await new WebhooksApi(_context.Client(settings)).DeleteAsync(id, cancellationToken);
        if (settings.Json)
        {
            _context.WriteRaw(data);
        }
        else
        {
            // Como logout: el resultado es un aviso para la persona, no un dato.
            await _context.Console.Error.WriteLineAsync($"Webhook {id} borrado.");
        }

        return ExitCodes.Success;
    }
}
