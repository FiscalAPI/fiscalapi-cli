using System.CommandLine;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Commands.Events;

public sealed class EventsCommand : Command
{
    public EventsCommand(GlobalOptions globalOptions, FiscalApiClientFactory apiClients, CliConsole console)
        : base("events", "List, retrieve and resend webhook events from the last 30 days.")
    {
        var context = new ApiCommandContext(globalOptions, apiClients, console);
        Subcommands.Add(new ListEventsCommand(context));
        Subcommands.Add(new RetrieveEventCommand(context));
        Subcommands.Add(new ResendEventCommand(context));
    }
}
