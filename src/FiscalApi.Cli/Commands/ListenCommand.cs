using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Listen;

namespace FiscalApi.Cli.Commands;

/// <summary>
/// <c>fiscalapi listen</c> (CLI-004, D-26): abre la escucha de CLI, hace long-polling y reenvía cada entrega a
/// <c>--forward-to</c> con el cuerpo y los headers firmados intactos.
/// </summary>
public sealed class ListenCommand : Command
{
    private readonly Option<Uri?> _forwardTo = new("--forward-to")
    {
        Description = "URL a la que se reenvía cada evento (p. ej. http://localhost:3000/webhooks).",
        HelpName = "url",
        CustomParser = ParseForwardTo
    };

    // Sin --events, todos los tipos: el comodín de fapi-back (WebhookEventTypes.Wildcard).
    private readonly Option<IReadOnlyList<string>> _events = new("--events")
    {
        Description = "Tipos de evento separados por coma (p. ej. invoice.canceled,invoice.created). Por omisión, todos.",
        HelpName = "tipos",
        Arity = ArgumentArity.ExactlyOne,
        CustomParser = ParseEvents,
        DefaultValueFactory = _ => ["*"]
    };

    private readonly Option<IReadOnlyList<KeyValuePair<string, string>>> _headers = new("--headers")
    {
        Description = "Headers extra para cada reenvío, como \"Nombre:Valor,Nombre:Valor\".",
        HelpName = "headers",
        Arity = ArgumentArity.ExactlyOne,
        CustomParser = ParseHeaders,
        DefaultValueFactory = _ => []
    };

    private readonly Option<bool> _skipVerify = new("--skip-verify")
    {
        Description = "No verifica el certificado TLS de --forward-to (p. ej. uno autofirmado). Nunca afecta a la API."
    };

    private readonly Option<bool> _printSecret = new("--print-secret")
    {
        Description = "Imprime solo el secreto de firma de la escucha y termina. Abre una sesión: un listen en curso " +
                      "con la misma API key termina."
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
        : base("listen", "Recibe los eventos de webhooks en la terminal y los reenvía a tu servidor local.")
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
        var enabledEvents = parseResult.GetValue(_events)!;
        var output = ListenOutput.For(settings, _console, _timeProvider);

        if (parseResult.GetValue(_printSecret))
        {
            // El secreto solo llega al abrir una sesión (D-29), y abrirla reemplaza la de otro listen en curso (D-26).
            var session = await api.OpenAsync(enabledEvents, cancellationToken);
            output.WriteSecret(session.Secret);
            return ExitCodes.Success;
        }

        using var forwarder = CreateForwarder(parseResult);
        var target = forwarder ?? (IDeliveryTarget)DisplayOnlyTarget.Instance;
        var processor = new DeliveryProcessor(target, api, output, _timings, _timeProvider);
        await new ListenLoop(api, enabledEvents, processor, output, _timings, _timeProvider).RunAsync(cancellationToken);
        throw new UnreachableException("El bucle de listen solo termina con una excepción.");
    }

    private HttpForwardTarget? CreateForwarder(ParseResult parseResult) =>
        parseResult.GetValue(_forwardTo) is { } forwardTo
            ? HttpForwardTarget.Create(
                forwardTo, parseResult.GetValue(_headers)!, parseResult.GetValue(_skipVerify), _timings.ForwardTimeout, _timeProvider)
            : null;

    // Sin destino no hay reenvío al que agregar headers ni TLS que relajar: la opción sobrante es un error de uso y no un
    // silencio que haga creer a la persona que surtió efecto.
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
                result.AddError($"{option.Name} solo aplica con --forward-to.");
            }
        }
    }

    // --print-secret abre la sesión, imprime el secreto y termina: nunca reenvía, así que las opciones del reenvío no
    // tendrían efecto.
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
                result.AddError($"--print-secret no se combina con {option.Name}.");
            }
        }
    }

    // Los tipos los valida la API, con sus textos (un tipo que no existe, '*' junto a otros o ninguno).
    private static IReadOnlyList<string> ParseEvents(ArgumentResult result) =>
        result.Tokens.Single().Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    // "Nombre:Valor,Nombre:Valor": el valor llega hasta la coma siguiente y puede llevar ':'. Cada header se prueba aquí
    // con la misma validación de HttpClient que usará el reenvío, así un header inválido es un error de uso y no una
    // falla en cada entrega.
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
                result.AddError($"--headers espera pares Nombre:Valor separados por coma; se recibió '{entry}'.");
            }
            else if (WebhookHeaderNames.IsSigned(name))
            {
                result.AddError($"--headers no puede cambiar {name}: lo firma FiscalAPI.");
            }
            else if (!IsValidRequestHeader(name, value))
            {
                result.AddError($"--headers trae un header que HTTP no admite: '{name}'.");
            }
            else
            {
                headers.Add(KeyValuePair.Create(name, value));
            }
        }

        return headers;
    }

    // Content-Type y los demás headers de contenido no van en los del request: los fija el reenvío.
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

    // Una URL relativa o de otro esquema terminaría en un error de red confuso en cada entrega.
    private static Uri? ParseForwardTo(ArgumentResult result)
    {
        var value = result.Tokens.Single().Value;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            return uri;
        }

        result.AddError($"--forward-to debe ser una URL absoluta http o https; se recibió '{value}'.");
        return null;
    }
}
