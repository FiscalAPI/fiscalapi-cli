using System.CommandLine;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Commands.Events;

/// <summary>
/// <c>fiscalapi events list|retrieve|resend</c>: los eventos de webhook del tenant de los últimos 30 días sobre
/// <c>/api/v4/events</c>. Sin subcomando muestra la ayuda.
/// </summary>
public sealed class EventsCommand : Command
{
    public EventsCommand(GlobalOptions globalOptions, FiscalApiClientFactory apiClients, CliConsole console)
        : base("events", "Consulta y reenvía los eventos de webhook de los últimos 30 días.")
    {
        var context = new ApiCommandContext(globalOptions, apiClients, console);
        Subcommands.Add(new ListEventsCommand(context));
        Subcommands.Add(new RetrieveEventCommand(context));
        Subcommands.Add(new ResendEventCommand(context));
    }
}
