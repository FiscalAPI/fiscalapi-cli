using System.CommandLine;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Input;

namespace FiscalApi.Cli.Commands.Webhooks;

/// <summary>
/// <c>fiscalapi webhooks list|retrieve|create|update|delete|rotate-secret</c>: los webhooks del
/// tenant sobre <c>/api/v4/webhooks</c>. Sin subcomando muestra la ayuda.
/// </summary>
public sealed class WebhooksCommand : Command
{
    public WebhooksCommand(
        GlobalOptions globalOptions, FiscalApiClientFactory apiClients, IConfirmationPrompt confirmation, CliConsole console)
        : base("webhooks", "Administra los webhooks que reciben los eventos del tenant.")
    {
        var context = new ApiCommandContext(globalOptions, apiClients, console);
        Subcommands.Add(new ListWebhooksCommand(context));
        Subcommands.Add(new RetrieveWebhookCommand(context));
        Subcommands.Add(new CreateWebhookCommand(context));
        Subcommands.Add(new UpdateWebhookCommand(context));
        Subcommands.Add(new DeleteWebhookCommand(context, confirmation));
        Subcommands.Add(new RotateWebhookSecretCommand(context));
    }
}
