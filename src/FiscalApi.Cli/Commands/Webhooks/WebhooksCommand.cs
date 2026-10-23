using System.CommandLine;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Input;

namespace FiscalApi.Cli.Commands.Webhooks;

public sealed class WebhooksCommand : Command
{
    public WebhooksCommand(
        GlobalOptions globalOptions, FiscalApiClientFactory apiClients, IConfirmationPrompt confirmation, CliConsole console)
        : base("webhooks", "Manage the webhooks that receive the tenant's events.")
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
