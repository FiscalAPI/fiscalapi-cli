using System.CommandLine;
using System.CommandLine.Parsing;

namespace FiscalApi.Cli;

public sealed class GlobalOptions
{
    private readonly Option<bool> _live = new("--live")
    {
        Description = "Use the live environment (https://live.fiscalapi.com) instead of test.",
        Recursive = true
    };

    private readonly Option<Uri?> _apiBase = new("--api-base")
    {
        Description = "API base URL, e.g. a local API. Overrides --live.",
        HelpName = "url",
        Recursive = true,
        CustomParser = ParseApiBase
    };

    private readonly Option<bool> _json = new("--json")
    {
        Description = "Write only JSON to stdout.",
        Recursive = true
    };

    private readonly Option<bool> _verbose = new("--verbose")
    {
        Description = "Show HTTP details on stderr, without credential headers.",
        Recursive = true
    };

    public void AddTo(Command command)
    {
        command.Options.Add(_live);
        command.Options.Add(_apiBase);
        command.Options.Add(_json);
        command.Options.Add(_verbose);
    }

    public GlobalSettings Read(ParseResult parseResult) => new(
        ApiEnvironment.Resolve(parseResult.GetValue(_apiBase), parseResult.GetValue(_live)),
        parseResult.GetValue(_json),
        parseResult.GetValue(_verbose));

    private static Uri? ParseApiBase(ArgumentResult result)
    {
        var value = result.Tokens.Single().Value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            result.AddError($"--api-base must be an absolute http or https URL. Got '{value}'.");
            return null;
        }

        if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
        {
            result.AddError($"--api-base allows http only for localhost, 127.0.0.1 or ::1. Use https for '{value}'.");
            return null;
        }

        return uri;
    }
}
