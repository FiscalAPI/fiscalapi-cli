using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Listen;

namespace FiscalApi.Cli.Commands;

public sealed class ListenCommand : Command
{
    private readonly Option<Uri?> _forwardTo = new("--forward-to")
    {
        Description = "URL that receives each event, e.g. http://localhost:3000/webhooks.",
        HelpName = "url",
        CustomParser = ParseForwardTo
    };

    private readonly Option<IReadOnlyList<string>> _events = new("--events")
    {
        Description = "Comma-separated event types, e.g. invoice.canceled,download_request.completed. Defaults to all.",
        HelpName = "types",
        Arity = ArgumentArity.ExactlyOne,
        CustomParser = CommaSeparatedValues.Parse,
        DefaultValueFactory = _ => ["*"]
    };

    private readonly Option<IReadOnlyList<KeyValuePair<string, string>>> _headers = new("--headers")
    {
        Description = "Extra headers for each forwarded request, as \"Name:Value,Name:Value\".",
        HelpName = "headers",
        Arity = ArgumentArity.ExactlyOne,
        CustomParser = ParseHeaders,
        DefaultValueFactory = _ => []
    };

    private readonly Option<bool> _skipVerify = new("--skip-verify")
    {
        Description = "Skip the TLS certificate check for --forward-to, e.g. self-signed. Never applies to the API."
    };

    private readonly Option<bool> _printSecret = new("--print-secret")
    {
        Description = "Print the signing secret of a new listen session and exit. Ends any running listen with the same API key."
    };

    private readonly GlobalOptions _globalOptions;
    private readonly FiscalApiClientFactory _apiClients;
    private readonly CliConsole _console;
    private readonly TimeProvider _timeProvider;
    private readonly ListenTimings _timings;

    public ListenCommand(
        GlobalOptions globalOptions,
        FiscalApiClientFactory apiClients,
        CliConsole console,
        TimeProvider timeProvider,
        ListenTimings timings)
        : base("listen", "Receive webhook events in your terminal and forward them to your local server.")
    {
        _globalOptions = globalOptions;
        _apiClients = apiClients;
        _console = console;
        _timeProvider = timeProvider;
        _timings = timings;
        Options.Add(_forwardTo);
        Options.Add(_events);
        Options.Add(_headers);
        Options.Add(_skipVerify);
        Options.Add(_printSecret);
        Validators.Add(RequireForwardToFor);
        Validators.Add(RejectForwardingWithPrintSecret);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = _globalOptions.Read(parseResult);
        var api = new ListenerApi(_apiClients.Create(settings, _timings.ApiTimeout), _timings.PollWaitSeconds);
        var enabledEvents = parseResult.GetRequiredValue(_events);
        var output = ListenOutput.For(settings, _console, _timeProvider);

        if (parseResult.GetValue(_printSecret))
        {
            var session = await api.OpenAsync(enabledEvents, cancellationToken);
            output.WriteSecret(session.Secret);
            return ExitCodes.Success;
        }

        using var forwarder = CreateForwarder(parseResult);
        var target = forwarder ?? (IDeliveryTarget)DisplayOnlyTarget.Instance;
        var processor = new DeliveryProcessor(target, api, output, _timings, _timeProvider);
        await new ListenLoop(api, enabledEvents, processor, output, _timings, _timeProvider).RunAsync(cancellationToken);
        throw new UnreachableException("The listen loop ends only with an exception.");
    }

    private HttpForwardTarget? CreateForwarder(ParseResult parseResult) =>
        parseResult.GetValue(_forwardTo) is { } forwardTo
            ? HttpForwardTarget.Create(
                forwardTo, parseResult.GetRequiredValue(_headers), parseResult.GetValue(_skipVerify), _timings.ForwardTimeout, _timeProvider)
            : null;

    private void RequireForwardToFor(CommandResult result)
    {
        if (result.GetResult(_forwardTo) is not null)
        {
            return;
        }

        foreach (var option in new Option[] { _headers, _skipVerify })
        {
            if (result.GetResult(option) is { Implicit: false })
            {
                result.AddError($"{option.Name} requires --forward-to.");
            }
        }
    }

    private void RejectForwardingWithPrintSecret(CommandResult result)
    {
        if (result.GetResult(_printSecret) is not { Implicit: false })
        {
            return;
        }

        foreach (var option in new Option[] { _forwardTo, _headers, _skipVerify })
        {
            if (result.GetResult(option) is { Implicit: false })
            {
                result.AddError($"--print-secret cannot be used with {option.Name}.");
            }
        }
    }

    private static IReadOnlyList<KeyValuePair<string, string>> ParseHeaders(ArgumentResult result)
    {
        var headers = new List<KeyValuePair<string, string>>();
        foreach (var entry in result.Tokens.Single().Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = entry.IndexOf(':', StringComparison.Ordinal);
            var name = separator > 0 ? entry[..separator].Trim() : "";
            var value = separator > 0 ? entry[(separator + 1)..].Trim() : "";
            if (name.Length == 0)
            {
                result.AddError($"--headers expects comma-separated Name:Value pairs. Got '{entry}'.");
            }
            else if (WebhookHeaderNames.IsSigned(name))
            {
                result.AddError($"--headers cannot set {name}. FiscalAPI signs it.");
            }
            else if (!IsValidRequestHeader(name, value))
            {
                result.AddError($"--headers has a header that HTTP does not allow: '{name}'.");
            }
            else
            {
                headers.Add(KeyValuePair.Create(name, value));
            }
        }

        return headers;
    }

    private static bool IsValidRequestHeader(string name, string value)
    {
        using var probe = new HttpRequestMessage();
        try
        {
            probe.Headers.Add(name, value);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException)
        {
            return false;
        }
    }

    private static Uri? ParseForwardTo(ArgumentResult result)
    {
        var value = result.Tokens.Single().Value;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            return uri;
        }

        result.AddError($"--forward-to must be an absolute http or https URL. Got '{value}'.");
        return null;
    }
}
