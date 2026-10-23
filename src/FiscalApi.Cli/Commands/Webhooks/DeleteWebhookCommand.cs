using System.CommandLine;
using FiscalApi.Cli.Errors;
using FiscalApi.Cli.Input;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Webhooks;

public sealed class DeleteWebhookCommand : Command
{
    private readonly Argument<string> _id = new("id") { Description = "ID of the webhook or listen session." };

    private readonly Option<bool> _yes = new("--yes")
    {
        Description = "Delete without confirmation. Required in scripts and with --json."
    };

    private readonly ApiCommandContext _context;
    private readonly IConfirmationPrompt _confirmation;

    public DeleteWebhookCommand(ApiCommandContext context, IConfirmationPrompt confirmation)
        : base("delete", "Delete a webhook and close its pending deliveries. Its history is kept.")
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
        var id = parseResult.GetRequiredValue(_id);
        if (!parseResult.GetValue(_yes))
        {
            if (settings.Json || !_confirmation.IsInteractive)
            {
                throw new UsageException("Use --yes to delete without an interactive confirmation.");
            }

            var confirmed = await _confirmation.ConfirmAsync(
                $"Delete webhook {id}? Its pending deliveries will be closed.", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!confirmed)
            {
                await _context.Console.Error.WriteLineAsync($"Webhook {id} was not deleted.");
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
            await _context.Console.Error.WriteLineAsync($"Webhook {id} deleted.");
        }

        return ExitCodes.Success;
    }
}
