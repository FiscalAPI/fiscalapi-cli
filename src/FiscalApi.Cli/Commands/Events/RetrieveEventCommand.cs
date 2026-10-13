using System.CommandLine;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Events;

/// <summary>
/// <c>fiscalapi events retrieve &lt;id&gt;</c>: <c>GET /api/v4/events/{id}</c>. Imprime el sobre exactamente como lo entregó
/// la API, con o sin <c>--json</c>: es lo que se firmó y lo que el receptor recibió, y re-serializarlo cambiaría sus bytes.
/// </summary>
public sealed class RetrieveEventCommand : Command
{
    private readonly Argument<string> _id = new("id") { Description = "Id del evento: el webhook-id con el que se entregó." };
    private readonly ApiCommandContext _context;

    public RetrieveEventCommand(ApiCommandContext context)
        : base("retrieve", "Imprime el sobre de un evento tal como se entregó.")
    {
        _context = context;
        Arguments.Add(_id);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = _context.Settings(parseResult);
        _context.WriteRaw(await new EventsApi(_context.Client(settings)).RetrieveAsync(parseResult.GetValue(_id)!, cancellationToken));
        return ExitCodes.Success;
    }
}
