using System.CommandLine;
using System.Net;
using FiscalApi.Cli.Commands;
using FiscalApi.Cli.Tests.Http;

namespace FiscalApi.Cli.Tests.Commands;

/// <summary>
/// <c>fiscalapi trigger</c> contra <c>POST /api/v4/events/trigger</c> de la API,
/// simulada con <see cref="StubHttpMessageHandler"/>. Quien valida el tipo y el ambiente es la
/// API: el CLI solo arma la petición y muestra su respuesta.
/// </summary>
public sealed class TriggerCommandTests
{
    private const string TriggerUrl = "https://api.example.test/api/v4/events/trigger";
    private const string EventId = "0d6f5c1e-8a2b-4c3d-9e8f-7a6b5c4d3e2f";
    private const string Accepted = $$"""{"eventId":"{{EventId}}","webhookId":"escucha-1"}""";

    private readonly ApiCommandHarness _cli = new();

    [Fact]
    public async Task Trigger_posts_only_the_type_so_the_api_sends_it_to_the_cli_listener()
    {
        _cli.Api.RespondJson(HttpStatusCode.Accepted, ApiCommandHarness.Success(Accepted));

        var exitCode = await _cli.RunAsync("trigger", "invoice.canceled");

        Assert.Equal(ExitCodes.Success, exitCode);
        var request = _cli.SingleRequest();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri(TriggerUrl), request.RequestUri);
        // Sin --webhook no viaja webhookId (ni las demás opciones): la API elige la escucha de quien llama.
        ApiCommandHarness.AssertJsonEqual("""{"type":"invoice.canceled"}""", _cli.Body());
    }

    [Fact]
    public async Task Trigger_sends_every_option_with_the_api_names()
    {
        _cli.Api.RespondJson(HttpStatusCode.Accepted,
            ApiCommandHarness.Success($$"""{"eventId":"{{EventId}}","webhookId":"we-1"}"""));

        await _cli.RunAsync(
            "trigger", "download_rule.suspended",
            "--related-object-id", "regla-1",
            "--person-id", "persona-1",
            "--webhook", "we-1");

        ApiCommandHarness.AssertJsonEqual(
            """{"type":"download_rule.suspended","relatedObjectId":"regla-1","personId":"persona-1","webhookId":"we-1"}""",
            _cli.Body());
    }

    [Fact]
    public async Task Trigger_prints_the_event_id_and_its_destination()
    {
        _cli.Api.RespondJson(HttpStatusCode.Accepted, ApiCommandHarness.Success(Accepted));

        await _cli.RunAsync("trigger", "invoice.canceled");

        Assert.Equal($"Evento disparado: {EventId} al webhook escucha-1.{Environment.NewLine}", _cli.Out);
        Assert.Empty(_cli.Error);
    }

    [Fact]
    public async Task Trigger_with_json_prints_the_data_unchanged()
    {
        // Espacios y un campo que el CLI no conoce: --json no re-serializa.
        const string accepted = $$"""{ "eventId" : "{{EventId}}", "webhookId":"escucha-1", "nuevo": 1.50e2 }""";
        _cli.Api.RespondJson(HttpStatusCode.Accepted, ApiCommandHarness.Success(accepted));

        await _cli.RunAsync("trigger", "invoice.canceled", "--json");

        _cli.AssertJsonOut(accepted);
    }

    // El CLI no bloquea --live: la regla vive en la API, que responde 403 fuera de Development y Test. El harness
    // siempre agrega --api-base, que gana sobre --live, así que solo prueba que el flag no se rechaza en el cliente.
    [Fact]
    public async Task Trigger_with_live_flag_is_not_blocked_by_the_cli_and_shows_the_api_403()
    {
        _cli.Api.RespondJson(HttpStatusCode.Forbidden,
            """{"succeeded":false,"message":"Sorry, it's not me, it's you.","details":"fiscalapi trigger no está disponible en producción; usa el ambiente de pruebas.","traceIdentifier":"0HN7:00000403"}""");

        var exitCode = await _cli.RunAsync("trigger", "invoice.canceled", "--live");

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Single(_cli.Api.Requests);
        Assert.Contains("fiscalapi trigger no está disponible en producción; usa el ambiente de pruebas.", _cli.Error);
        Assert.Contains("Código HTTP: 403", _cli.Error);
        Assert.Contains("0HN7:00000403", _cli.Error);
        Assert.Empty(_cli.Out);
    }

    [Fact]
    public async Task Trigger_without_a_listen_session_exits_1_with_the_api_text()
    {
        _cli.Api.RespondJson(HttpStatusCode.Conflict,
            """{"succeeded":false,"message":"Sorry, it's not me, it's you.","details":"La escucha de CLI no está conectada; ejecuta fiscalapi listen."}""");

        var exitCode = await _cli.RunAsync("trigger", "invoice.canceled");

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("La escucha de CLI no está conectada; ejecuta fiscalapi listen.", _cli.Error);
        Assert.Contains("Código HTTP: 409", _cli.Error);
        Assert.Empty(_cli.Out);
    }

    // El tipo lo valida la API: el CLI manda incluso uno que no autocompleta (webhook.test, que trigger rechaza).
    [Fact]
    public async Task Trigger_of_a_type_the_api_rejects_exits_1_with_the_validation_text()
    {
        _cli.Api.RespondJson(HttpStatusCode.BadRequest,
            """{"data":[{"propertyName":"Type","errorMessage":"El tipo de evento 'webhook.test' no existe.","attemptedValue":"webhook.test"}],"succeeded":false,"message":"Sorry, it's not me, it's you.","details":"One or more validation failures have occurred."}""");

        var exitCode = await _cli.RunAsync("trigger", "webhook.test");

        Assert.Equal(ExitCodes.Failure, exitCode);
        ApiCommandHarness.AssertJsonEqual("""{"type":"webhook.test"}""", _cli.Body());
        Assert.Contains("El tipo de evento 'webhook.test' no existe.", _cli.Error);
        Assert.Contains("Código HTTP: 400", _cli.Error);
        Assert.Empty(_cli.Out);
    }

    [Fact]
    public async Task Trigger_is_never_retried()
    {
        _cli.Api.RespondHtml(HttpStatusCode.BadGateway, "<html>502</html>");

        var exitCode = await _cli.RunAsync("trigger", "invoice.canceled");

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Single(_cli.Api.Requests);
    }

    [Fact]
    public async Task Trigger_without_type_is_a_usage_error_without_calling_the_api()
    {
        var exitCode = await _cli.RunAsync("trigger");

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Empty(_cli.Api.Requests);
    }

    [Fact]
    public void Type_completes_with_the_six_subscribable_event_types()
    {
        // En orden ordinal, sin webhook.test (no se dispara: la API lo rechaza en trigger).
        string[] expected =
        [
            "download_request.abandoned",
            "download_request.completed",
            "download_rule.suspended",
            "invoice.canceled",
            "invoice.cancellation_rejected",
            "invoice.cancellation_requested"
        ];

        Assert.Equal(expected, ArgumentCompletions("trigger "));
    }

    [Fact]
    public void Type_completion_filters_by_what_was_typed()
    {
        Assert.Equal(["download_request.abandoned", "download_request.completed"], ArgumentCompletions("trigger download_req"));
    }

    /// <summary>
    /// Las sugerencias de valor al final de <paramref name="line"/>, sin las opciones (<c>--webhook</c>, <c>-h</c>,
    /// <c>/?</c>…).
    /// </summary>
    private static string[] ArgumentCompletions(string line)
    {
        var root = new RootCommand();
        root.Subcommands.Add(new TriggerCommand(
            new GlobalOptions(), FiscalApiClientBuilder.Factory(new StubHttpMessageHandler()), new CliConsole(TextWriter.Null, TextWriter.Null)));

        return root.Parse(line)
            .GetCompletions(line.Length)
            .Select(completion => completion.Label)
            .Where(label => !label.StartsWith('-') && !label.StartsWith('/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
