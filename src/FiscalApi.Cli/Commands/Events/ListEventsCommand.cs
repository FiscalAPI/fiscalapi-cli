using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Events;

/// <summary>
/// <c>fiscalapi events list [--type] [--related-object-id] [--from] [--to] [--page] [--page-size]</c>:
/// <c>GET /api/v4/events</c>. Los filtros los valida la API (tipo que existe, rango de 30 días); el CLI solo convierte las
/// fechas a UTC.
/// </summary>
public sealed class ListEventsCommand : Command
{
    // ISO 8601: fecha, o fecha y hora con o sin segundos y fracción, con o sin zona (Z o ±hh:mm).
    private static readonly string[] Iso8601Formats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-dd'T'HH:mm",
        "yyyy-MM-dd'T'HH:mmK",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"
    ];

    private readonly Option<string> _type = new("--type")
    {
        Description = "Solo los eventos de este tipo (p. ej. invoice.canceled o webhook.test).",
        HelpName = "tipo"
    };

    private readonly Option<string> _relatedObjectId = new("--related-object-id")
    {
        Description = "Solo los eventos de este recurso (data.relatedObject.id).",
        HelpName = "id"
    };

    private readonly Option<DateTimeOffset?> _from = new("--from")
    {
        Description = "Desde este instante, incluido, en ISO 8601 (p. ej. 2026-10-01 o 2026-10-01T08:00:00-06:00); sin zona, UTC.",
        HelpName = "fecha",
        CustomParser = ParseInstant
    };

    private readonly Option<DateTimeOffset?> _to = new("--to")
    {
        Description = "Hasta este instante, incluido, en ISO 8601; sin zona, UTC. Por omisión, ahora.",
        HelpName = "fecha",
        CustomParser = ParseInstant
    };

    private readonly PageOptions _page = new();
    private readonly ApiCommandContext _context;

    public ListEventsCommand(ApiCommandContext context)
        : base("list", "Lista los eventos de los últimos 30 días, del más nuevo al más viejo.")
    {
        _context = context;
        Options.Add(_type);
        Options.Add(_relatedObjectId);
        Options.Add(_from);
        Options.Add(_to);
        _page.AddTo(this);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = _context.Settings(parseResult);
        var query = new WebhookEventsQuery(
            parseResult.GetValue(_type),
            parseResult.GetValue(_relatedObjectId),
            parseResult.GetValue(_from),
            parseResult.GetValue(_to),
            _page.Read(parseResult));

        var data = await new EventsApi(_context.Client(settings)).ListAsync(query, cancellationToken);
        _context.WriteData(settings, data, FiscalApiJsonContext.Default.PagedListWebhookEventEnvelope, WebhookEventView.WriteList);
        return ExitCodes.Success;
    }

    // La API compara instantes UTC: una fecha sin zona se toma como UTC y una con zona se convierte.
    private static DateTimeOffset? ParseInstant(ArgumentResult result)
    {
        var value = result.Tokens.Single().Value;
        if (DateTimeOffset.TryParseExact(
                value,
                Iso8601Formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var instant))
        {
            return instant;
        }

        var option = ((OptionResult)result.Parent!).Option.Name;
        result.AddError(
            $"{option} debe ser una fecha ISO 8601 (p. ej. 2026-10-01 o 2026-10-01T08:00:00-06:00); se recibió '{value}'.");
        return null;
    }
}
