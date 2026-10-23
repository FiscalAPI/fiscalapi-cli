using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Globalization;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Events;

public sealed class ListEventsCommand : Command
{
    private const string DateOnlyFormat = "yyyy-MM-dd";

    private static readonly string[] DateTimeFormats =
    [
        "yyyy-MM-dd'T'HH:mm",
        "yyyy-MM-dd'T'HH:mmK",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"
    ];

    private readonly Option<string> _type = new("--type")
    {
        Description = "Only events of this type, e.g. invoice.canceled or webhook.test.",
        HelpName = "type"
    };

    private readonly Option<string> _relatedObjectId = new("--related-object-id")
    {
        Description = "Only events for this resource (data.relatedObject.id).",
        HelpName = "id"
    };

    private readonly Option<DateTimeOffset?> _from = new("--from")
    {
        Description = "Start time, inclusive, in ISO 8601, e.g. 2026-10-01 or 2026-10-01T08:00:00-06:00. UTC if no offset.",
        HelpName = "date",
        CustomParser = result => ParseInstant(result, dateIncludesWholeDay: false)
    };

    private readonly Option<DateTimeOffset?> _to = new("--to")
    {
        Description = "End time, inclusive, in ISO 8601. UTC if no offset. A date alone includes the whole day. Defaults to now.",
        HelpName = "date",
        CustomParser = result => ParseInstant(result, dateIncludesWholeDay: true)
    };

    private readonly PageOptions _page = new();
    private readonly ApiCommandContext _context;

    public ListEventsCommand(ApiCommandContext context)
        : base("list", "List events from the last 30 days, newest first.")
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

    private static DateTimeOffset? ParseInstant(ArgumentResult result, bool dateIncludesWholeDay)
    {
        var value = result.Tokens.Single().Value;
        if (TryParseUtc(value, [DateOnlyFormat], out var date))
        {
            return dateIncludesWholeDay ? date.AddDays(1).AddTicks(-1) : date;
        }

        if (TryParseUtc(value, DateTimeFormats, out var instant))
        {
            return instant;
        }

        if (result.Parent is not OptionResult option)
        {
            throw new UnreachableException("Dates are parsed only as option values.");
        }

        result.AddError(
            $"{option.Option.Name} must be an ISO 8601 date, e.g. 2026-10-01 or 2026-10-01T08:00:00-06:00. Got '{value}'.");
        return null;
    }

    private static bool TryParseUtc(string value, string[] formats, out DateTimeOffset instant) =>
        DateTimeOffset.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out instant);
}
