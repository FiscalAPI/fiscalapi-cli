using System.Net;
using FiscalApi.Cli.Input;
using FiscalApi.Cli.Tests.Http;

namespace FiscalApi.Cli.Tests.Commands;

/// <summary>
/// <c>fiscalapi webhooks list|retrieve|create|update|delete|rotate-secret</c> contra las rutas de
/// <c>WebhooksController</c> de la API, simuladas con <see cref="StubHttpMessageHandler"/>. Cada
/// subcomando arma la ruta, el método, la query y el cuerpo; <c>--json</c> deja en stdout el <c>data</c> sin cambios; el
/// secreto sale una sola vez.
/// </summary>
public sealed class WebhooksCommandTests
{
    private const string WebhooksUrl = "https://api.example.test/api/v4/webhooks";
    private const string WebhookId = "6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d";
    private const string Secret = "whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw";

    // Como lo escribe fapi-back (WebhookDto): camelCase, enums en camelCase y las nulas presentes.
    private const string UrlWebhook =
        $$"""{"id":"{{WebhookId}}","kind":"url","url":"https://example.com/webhooks","description":"Producción","enabledEvents":["invoice.canceled","invoice.cancellation_requested"],"personIds":["persona-1","persona-2"],"status":"enabled","disabledReason":null,"disabledAt":null,"failingSince":null,"listenerPersonId":null,"sessionExpiresAt":null,"createdAt":"2026-10-13T09:30:00.000","updatedAt":null}""";

    private const string WebhookWithSecret =
        $$"""{"id":"{{WebhookId}}","kind":"url","url":"https://example.com/webhooks","description":"","enabledEvents":["*"],"personIds":null,"status":"enabled","disabledReason":null,"disabledAt":null,"failingSince":null,"listenerPersonId":null,"sessionExpiresAt":null,"createdAt":"2026-10-13T09:30:00.000","updatedAt":null,"secret":"{{Secret}}"}""";

    private const string CliListener =
        """{"id":"escucha-1","kind":"cliListener","url":null,"description":"","enabledEvents":["*"],"personIds":null,"status":"enabled","disabledReason":null,"disabledAt":null,"failingSince":null,"listenerPersonId":"persona-1","sessionExpiresAt":"2026-10-13T09:31:00.000","createdAt":"2026-10-13T09:00:00.000","updatedAt":null}""";

    private const string Page =
        $$"""{"items":[{{UrlWebhook}},{{CliListener}}],"pageNumber":1,"totalPages":3,"totalCount":5,"hasPreviousPage":false,"hasNextPage":true}""";

    private const string NotFound =
        """{"succeeded":false,"message":"Sorry, it's not me, it's you.","details":"No such resource for the given Id: 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d","traceIdentifier":"0HN7:00000404"}""";

    private readonly ApiCommandHarness _cli = new();

    // ---------------------------------------------------------------- list

    [Fact]
    public async Task List_gets_the_webhooks_without_query_by_default()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(Page));

        var exitCode = await _cli.RunAsync("webhooks", "list");

        Assert.Equal(ExitCodes.Success, exitCode);
        var request = _cli.SingleRequest();
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri(WebhooksUrl), request.RequestUri);
        Assert.Equal([ApiCommandHarness.ApiKey], request.Headers.GetValues("X-API-KEY"));
    }

    [Fact]
    public async Task List_sends_the_page_options_as_page_number_and_page_size()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(Page));

        await _cli.RunAsync("webhooks", "list", "--page", "2", "--page-size", "50");

        Assert.Equal(new Uri($"{WebhooksUrl}?pageNumber=2&pageSize=50"), _cli.SingleRequest().RequestUri);
    }

    [Fact]
    public async Task List_prints_a_table_with_one_row_per_webhook_and_the_page()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(Page));

        await _cli.RunAsync("webhooks", "list");

        var lines = _cli.Out.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Matches(@"^ID\s+TIPO\s+ESTADO\s+URL\s+EVENTOS\s+DESCRIPCIÓN$", lines[0]);
        Assert.Matches($@"^{WebhookId}\s+url\s+enabled\s+https://example\.com/webhooks\s+invoice\.canceled,invoice\.cancellation_requested\s+Producción$", lines[1]);
        // Lo que no hay (la URL de una escucha, una descripción vacía) se muestra como "-".
        Assert.Matches(@"^escucha-1\s+cliListener\s+enabled\s+-\s+\*\s+-$", lines[2]);
        Assert.Equal("Página 1 de 3 (5 en total). Siguiente: --page 2", lines[3]);
        Assert.Empty(_cli.Error);
    }

    [Fact]
    public async Task List_without_webhooks_says_so()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(
            """{"items":[],"pageNumber":1,"totalPages":0,"totalCount":0,"hasPreviousPage":false,"hasNextPage":false}"""));

        var exitCode = await _cli.RunAsync("webhooks", "list");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.StartsWith("Sin datos.", _cli.Out);
    }

    [Fact]
    public async Task List_with_json_prints_the_data_unchanged()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(Page));

        await _cli.RunAsync("webhooks", "list", "--json");

        _cli.AssertJsonOut(Page);
    }

    // ---------------------------------------------------------------- retrieve

    [Fact]
    public async Task Retrieve_gets_the_webhook_by_its_escaped_id()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(UrlWebhook));

        var exitCode = await _cli.RunAsync("webhooks", "retrieve", "we/1 2");

        Assert.Equal(ExitCodes.Success, exitCode);
        var request = _cli.SingleRequest();
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"{WebhooksUrl}/we%2F1%202", request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Retrieve_prints_the_webhook_detail()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(UrlWebhook));

        await _cli.RunAsync("webhooks", "retrieve", WebhookId);

        Assert.Matches($@"(?m)^ID\s+{WebhookId}\r?$", _cli.Out);
        Assert.Matches(@"(?m)^Estado\s+enabled\r?$", _cli.Out);
        Assert.Matches(@"(?m)^URL\s+https://example\.com/webhooks\r?$", _cli.Out);
        Assert.Matches(@"(?m)^Eventos\s+invoice\.canceled, invoice\.cancellation_requested\r?$", _cli.Out);
        Assert.Matches(@"(?m)^Personas\s+persona-1, persona-2\r?$", _cli.Out);
        Assert.Matches(@"(?m)^Creado\s+2026-10-13T09:30:00\.000\r?$", _cli.Out);
        // Lo que no aplica no se muestra.
        Assert.DoesNotContain("Motivo", _cli.Out);
        Assert.DoesNotContain("Secreto", _cli.Out);
    }

    [Fact]
    public async Task Retrieve_with_json_prints_the_data_unchanged()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(UrlWebhook));

        await _cli.RunAsync("webhooks", "retrieve", WebhookId, "--json");

        _cli.AssertJsonOut(UrlWebhook);
    }

    [Fact]
    public async Task Retrieve_of_a_missing_webhook_exits_1_with_the_api_text()
    {
        _cli.Api.RespondJson(HttpStatusCode.NotFound, NotFound);

        var exitCode = await _cli.RunAsync("webhooks", "retrieve", WebhookId);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains($"No such resource for the given Id: {WebhookId}", _cli.Error);
        Assert.Contains("Código HTTP: 404", _cli.Error);
        Assert.Empty(_cli.Out);
    }

    // ---------------------------------------------------------------- create

    [Fact]
    public async Task Create_posts_the_webhook_with_its_events_and_persons()
    {
        _cli.Api.RespondJson(HttpStatusCode.Created, ApiCommandHarness.Success(WebhookWithSecret));

        var exitCode = await _cli.RunAsync(
            "webhooks", "create",
            "--url", "https://example.com/webhooks",
            "--description", "Producción",
            "--events", "invoice.canceled, invoice.cancellation_requested",
            "--person", "persona-1,persona-2",
            "--person", "persona-3");

        Assert.Equal(ExitCodes.Success, exitCode);
        var request = _cli.SingleRequest();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri(WebhooksUrl), request.RequestUri);
        ApiCommandHarness.AssertJsonEqual(
            """{"url":"https://example.com/webhooks","description":"Producción","enabledEvents":["invoice.canceled","invoice.cancellation_requested"],"personIds":["persona-1","persona-2","persona-3"]}""",
            _cli.Body());
    }

    [Fact]
    public async Task Create_without_description_or_persons_omits_them()
    {
        _cli.Api.RespondJson(HttpStatusCode.Created, ApiCommandHarness.Success(WebhookWithSecret));

        await _cli.RunAsync("webhooks", "create", "--url", "https://example.com/webhooks", "--events", "*");

        ApiCommandHarness.AssertJsonEqual("""{"url":"https://example.com/webhooks","enabledEvents":["*"]}""", _cli.Body());
    }

    [Fact]
    public async Task Create_sends_every_person_and_lets_the_api_apply_its_limit()
    {
        var persons = string.Join(',', Enumerable.Range(1, 101).Select(i => $"persona-{i}"));
        _cli.Api.RespondJson(HttpStatusCode.BadRequest, ValidationFailure("PersonIds", "Un webhook filtra como máximo 100 personas."));

        var exitCode = await _cli.RunAsync("webhooks", "create", "--url", "https://example.com/webhooks", "--events", "*", "--person", persons);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Equal(101, _cli.Body()!["personIds"]!.AsArray().Count);
        Assert.Contains("Un webhook filtra como máximo 100 personas.", _cli.Error);
    }

    [Fact]
    public async Task Create_prints_the_secret_once_with_the_warning_to_keep_it()
    {
        _cli.Api.RespondJson(HttpStatusCode.Created, ApiCommandHarness.Success(WebhookWithSecret));

        await _cli.RunAsync("webhooks", "create", "--url", "https://example.com/webhooks", "--events", "*");

        Assert.Matches($@"(?m)^ID\s+{WebhookId}\r?$", _cli.Out);
        Assert.Contains($"Secreto de firma: {Secret}", _cli.Out);
        Assert.Equal(1, ApiCommandHarness.Occurrences(_cli.Out + _cli.Error, Secret));
        Assert.Contains("Guarda este secreto de firma. Lo usarás para verificar que los eventos vienen de FiscalAPI.", _cli.Error);
    }

    [Fact]
    public async Task Create_with_json_prints_the_data_unchanged_with_the_secret_once()
    {
        _cli.Api.RespondJson(HttpStatusCode.Created, ApiCommandHarness.Success(WebhookWithSecret));

        await _cli.RunAsync("webhooks", "create", "--url", "https://example.com/webhooks", "--events", "*", "--json");

        _cli.AssertJsonOut(WebhookWithSecret);
        Assert.Equal(1, ApiCommandHarness.Occurrences(_cli.Out + _cli.Error, Secret));
    }

    [Fact]
    public async Task Create_never_writes_the_secret_in_verbose()
    {
        _cli.Api.RespondJson(HttpStatusCode.Created, ApiCommandHarness.Success(WebhookWithSecret));

        await _cli.RunAsync("webhooks", "create", "--url", "https://example.com/webhooks", "--events", "*", "--verbose");

        Assert.Contains($"> POST {WebhooksUrl}", _cli.Error);
        Assert.DoesNotContain(Secret, _cli.Error);
        Assert.Equal(1, ApiCommandHarness.Occurrences(_cli.Out, Secret));
    }

    [Theory]
    [InlineData("--url")]
    [InlineData("--events")]
    public async Task Create_requires_url_and_events_without_calling_the_api(string missing)
    {
        List<string> args = ["webhooks", "create"];
        if (missing != "--url")
        {
            args.AddRange(["--url", "https://example.com/webhooks"]);
        }

        if (missing != "--events")
        {
            args.AddRange(["--events", "*"]);
        }

        var exitCode = await _cli.RunAsync(args);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains(missing, _cli.Error);
        Assert.Empty(_cli.Api.Requests);
    }

    [Fact]
    public async Task Create_rejected_by_validation_exits_1_with_each_failure_text()
    {
        _cli.Api.RespondJson(HttpStatusCode.BadRequest, ValidationFailure("EnabledEvents[0]", "El tipo de evento 'invoice.paid' no existe."));

        var exitCode = await _cli.RunAsync("webhooks", "create", "--url", "https://example.com/webhooks", "--events", "invoice.paid");

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("El tipo de evento 'invoice.paid' no existe.", _cli.Error);
        Assert.Contains("Código HTTP: 400", _cli.Error);
        Assert.Empty(_cli.Out);
    }

    [Fact]
    public async Task Create_is_never_retried()
    {
        _cli.Api.RespondHtml(HttpStatusCode.ServiceUnavailable, "<html>503</html>");

        var exitCode = await _cli.RunAsync("webhooks", "create", "--url", "https://example.com/webhooks", "--events", "*");

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Single(_cli.Api.Requests);
    }

    // ---------------------------------------------------------------- update

    [Fact]
    public async Task Update_reads_the_webhook_and_replaces_it_changing_only_what_was_given()
    {
        _cli.Api
            .RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(UrlWebhook))
            .RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(UrlWebhook));

        var exitCode = await _cli.RunAsync("webhooks", "update", WebhookId, "--url", "https://example.com/v2/webhooks");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(2, _cli.Api.Requests.Count);
        Assert.Equal(HttpMethod.Get, _cli.Api.Requests[0].Method);
        Assert.Equal(new Uri($"{WebhooksUrl}/{WebhookId}"), _cli.Api.Requests[0].RequestUri);
        Assert.Equal(HttpMethod.Put, _cli.Api.Requests[1].Method);
        Assert.Equal(new Uri($"{WebhooksUrl}/{WebhookId}"), _cli.Api.Requests[1].RequestUri);
        // Sin status: el PUT conserva el estado.
        ApiCommandHarness.AssertJsonEqual(
            $$"""{"id":"{{WebhookId}}","url":"https://example.com/v2/webhooks","description":"Producción","enabledEvents":["invoice.canceled","invoice.cancellation_requested"],"personIds":["persona-1","persona-2"]}""",
            _cli.Body(1));
    }

    [Fact]
    public async Task Update_applies_every_option_given()
    {
        _cli.Api
            .RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(UrlWebhook))
            .RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(UrlWebhook));

        await _cli.RunAsync(
            "webhooks", "update", WebhookId,
            "--description", "",
            "--events", "*",
            "--person", "persona-9",
            "--disabled");

        ApiCommandHarness.AssertJsonEqual(
            $$"""{"id":"{{WebhookId}}","url":"https://example.com/webhooks","description":"","enabledEvents":["*"],"personIds":["persona-9"],"status":"disabled"}""",
            _cli.Body(1));
    }

    [Fact]
    public async Task Update_with_all_persons_removes_the_person_filter_explicitly()
    {
        _cli.Api
            .RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(UrlWebhook))
            .RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(UrlWebhook));

        var exitCode = await _cli.RunAsync("webhooks", "update", WebhookId, "--all-persons", "--enabled");

        Assert.Equal(ExitCodes.Success, exitCode);
        var body = _cli.Body(1)!;
        Assert.Empty(body["personIds"]!.AsArray());
        Assert.Equal("enabled", (string?)body["status"]);
    }

    [Fact]
    public async Task Update_of_a_webhook_for_every_person_keeps_omitting_person_ids()
    {
        const string forEveryPerson =
            $$"""{"id":"{{WebhookId}}","kind":"url","url":"https://example.com/webhooks","description":"","enabledEvents":["*"],"personIds":null,"status":"enabled"}""";
        _cli.Api
            .RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(forEveryPerson))
            .RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(forEveryPerson));

        await _cli.RunAsync("webhooks", "update", WebhookId, "--description", "Sin filtro");

        ApiCommandHarness.AssertJsonEqual(
            $$"""{"id":"{{WebhookId}}","url":"https://example.com/webhooks","description":"Sin filtro","enabledEvents":["*"]}""",
            _cli.Body(1));
    }

    // Un script con la variable vacía (--person "$PERSONAS") no debe ampliar el webhook a todas las personas sin avisar.
    [Theory]
    [InlineData("update", "")]
    [InlineData("update", ",")]
    [InlineData("update", " ")]
    [InlineData("create", "")]
    [InlineData("create", " , ")]
    public async Task An_empty_person_list_is_a_usage_error_without_calling_the_api(string subcommand, string persons)
    {
        List<string> args = subcommand == "update"
            ? ["webhooks", "update", WebhookId, "--person", persons]
            : ["webhooks", "create", "--url", "https://example.com/webhooks", "--events", "*", "--person", persons];

        var exitCode = await _cli.RunAsync(args);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("--person necesita al menos un id de persona; para recibir los eventos de todas usa --all-persons.", _cli.Error);
        Assert.Empty(_cli.Api.Requests);
    }

    [Fact]
    public async Task One_empty_person_among_others_is_also_a_usage_error()
    {
        var exitCode = await _cli.RunAsync("webhooks", "update", WebhookId, "--person", "persona-1", "--person", "");

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Empty(_cli.Api.Requests);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("create")]
    public async Task All_persons_with_person_is_a_usage_error(string subcommand)
    {
        List<string> args = subcommand == "update"
            ? ["webhooks", "update", WebhookId]
            : ["webhooks", "create", "--url", "https://example.com/webhooks", "--events", "*"];
        args.AddRange(["--person", "persona-1", "--all-persons"]);

        var exitCode = await _cli.RunAsync(args);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("--all-persons no se combina con --person.", _cli.Error);
        Assert.Empty(_cli.Api.Requests);
    }

    [Fact]
    public async Task Create_with_all_persons_omits_person_ids()
    {
        _cli.Api.RespondJson(HttpStatusCode.Created, ApiCommandHarness.Success(WebhookWithSecret));

        var exitCode = await _cli.RunAsync("webhooks", "create", "--url", "https://example.com/webhooks", "--events", "*", "--all-persons");

        Assert.Equal(ExitCodes.Success, exitCode);
        ApiCommandHarness.AssertJsonEqual("""{"url":"https://example.com/webhooks","enabledEvents":["*"]}""", _cli.Body());
    }

    [Fact]
    public async Task Update_with_json_prints_the_data_of_the_replacement_unchanged()
    {
        const string replaced = $$"""{"id":"{{WebhookId}}","kind":"url","url":"https://example.com/v2/webhooks","status":"disabled"}""";
        _cli.Api
            .RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(UrlWebhook))
            .RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(replaced));

        await _cli.RunAsync("webhooks", "update", WebhookId, "--disabled", "--json");

        _cli.AssertJsonOut(replaced);
    }

    [Fact]
    public async Task Update_without_changes_is_a_usage_error_without_calling_the_api()
    {
        var exitCode = await _cli.RunAsync("webhooks", "update", WebhookId);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("Indica qué cambiar", _cli.Error);
        Assert.Empty(_cli.Api.Requests);
    }

    [Fact]
    public async Task Update_with_enabled_and_disabled_is_a_usage_error()
    {
        var exitCode = await _cli.RunAsync("webhooks", "update", WebhookId, "--enabled", "--disabled");

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("--enabled y --disabled no se combinan.", _cli.Error);
        Assert.Empty(_cli.Api.Requests);
    }

    [Fact]
    public async Task Update_of_a_cli_listener_stops_before_replacing_it()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(CliListener));

        var exitCode = await _cli.RunAsync("webhooks", "update", "escucha-1", "--events", "invoice.canceled");

        // Es el estado del recurso, no el uso del comando: 1, como el 400 que respondería la API.
        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("Una escucha de CLI no se edita ni se prueba; usa fiscalapi listen y fiscalapi trigger.", _cli.Error);
        Assert.Single(_cli.Api.Requests);
    }

    [Fact]
    public async Task Update_of_a_missing_webhook_exits_1_without_replacing()
    {
        _cli.Api.RespondJson(HttpStatusCode.NotFound, NotFound);

        var exitCode = await _cli.RunAsync("webhooks", "update", WebhookId, "--disabled");

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains($"No such resource for the given Id: {WebhookId}", _cli.Error);
        Assert.Single(_cli.Api.Requests);
    }

    [Fact]
    public async Task Update_conflict_exits_1_with_the_api_text()
    {
        _cli.Api
            .RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(UrlWebhook))
            .RespondJson(HttpStatusCode.Conflict,
                """{"succeeded":false,"message":"Sorry, it's not me, it's you.","details":"Otro cambio a los webhooks está en curso; reintenta."}""");

        var exitCode = await _cli.RunAsync("webhooks", "update", WebhookId, "--disabled");

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("Otro cambio a los webhooks está en curso; reintenta.", _cli.Error);
        Assert.Contains("Código HTTP: 409", _cli.Error);
    }

    // ---------------------------------------------------------------- delete

    [Fact]
    public async Task Delete_with_yes_deletes_without_asking()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success("true"));

        var exitCode = await _cli.RunAsync("webhooks", "delete", WebhookId, "--yes");

        Assert.Equal(ExitCodes.Success, exitCode);
        var request = _cli.SingleRequest();
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal(new Uri($"{WebhooksUrl}/{WebhookId}"), request.RequestUri);
        Assert.Contains($"Webhook {WebhookId} borrado.", _cli.Error);
        Assert.DoesNotContain("¿", _cli.Error);
    }

    [Fact]
    public async Task Delete_with_json_and_yes_prints_the_data_unchanged()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success("true"));

        await _cli.RunAsync("webhooks", "delete", WebhookId, "--yes", "--json");

        _cli.AssertJsonOut("true");
    }

    [Fact]
    public async Task Delete_without_yes_and_redirected_input_is_a_usage_error()
    {
        var exitCode = await _cli.RunAsync(["webhooks", "delete", WebhookId], confirmationInput: "s\n", interactive: false);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("--yes", _cli.Error);
        Assert.Empty(_cli.Api.Requests);
    }

    [Fact]
    public async Task Delete_without_yes_and_json_is_a_usage_error_even_in_a_terminal()
    {
        var exitCode = await _cli.RunAsync(["webhooks", "delete", WebhookId, "--json"], confirmationInput: "s\n", interactive: true);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("Para borrar sin confirmación interactiva usa --yes.", _cli.Error);
        Assert.DoesNotContain("¿", _cli.Error);
        Assert.Empty(_cli.Api.Requests);
        Assert.Empty(_cli.Out);
    }

    [Theory]
    [InlineData("s\n")]
    [InlineData("Sí\n")]
    [InlineData(" y \n")]
    public async Task Delete_confirmed_in_the_terminal_deletes(string answer)
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success("true"));

        var exitCode = await _cli.RunAsync(["webhooks", "delete", WebhookId], confirmationInput: answer, interactive: true);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains($"¿Borrar el webhook {WebhookId}?", _cli.Error);
        Assert.Equal(HttpMethod.Delete, _cli.SingleRequest().Method);
    }

    [Theory]
    [InlineData("n\n")]
    [InlineData("\n")]
    [InlineData("")]
    public async Task Delete_not_confirmed_in_the_terminal_deletes_nothing(string answer)
    {
        var exitCode = await _cli.RunAsync(["webhooks", "delete", WebhookId], confirmationInput: answer, interactive: true);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains($"No se borró el webhook {WebhookId}.", _cli.Error);
        Assert.Empty(_cli.Api.Requests);
    }

    [Fact]
    public async Task Delete_of_a_missing_webhook_exits_1()
    {
        _cli.Api.RespondJson(HttpStatusCode.NotFound, NotFound);

        var exitCode = await _cli.RunAsync("webhooks", "delete", WebhookId, "--yes");

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains($"No such resource for the given Id: {WebhookId}", _cli.Error);
        Assert.DoesNotContain("borrado", _cli.Error);
    }

    // ---------------------------------------------------------------- rotate-secret

    [Fact]
    public async Task Rotate_secret_posts_without_body_and_prints_the_new_secret_once()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(WebhookWithSecret));

        var exitCode = await _cli.RunAsync("webhooks", "rotate-secret", WebhookId);

        Assert.Equal(ExitCodes.Success, exitCode);
        var request = _cli.SingleRequest();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri($"{WebhooksUrl}/{WebhookId}/rotate-secret"), request.RequestUri);
        Assert.Null(_cli.Api.RequestBodies[0]);
        Assert.Contains($"Secreto de firma: {Secret}", _cli.Out);
        Assert.Equal(1, ApiCommandHarness.Occurrences(_cli.Out + _cli.Error, Secret));
        Assert.Contains("Guarda este secreto de firma.", _cli.Error);
        Assert.Contains("El secreto anterior seguirá firmando durante 24 horas", _cli.Error);
    }

    [Fact]
    public async Task Rotate_secret_with_json_prints_the_data_unchanged()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(WebhookWithSecret));

        await _cli.RunAsync("webhooks", "rotate-secret", WebhookId, "--json");

        _cli.AssertJsonOut(WebhookWithSecret);
        Assert.Equal(1, ApiCommandHarness.Occurrences(_cli.Out + _cli.Error, Secret));
    }

    [Fact]
    public async Task Ctrl_c_while_confirming_the_delete_exits_130_without_deleting()
    {
        using var ctrlC = new CancellationTokenSource();

        var exitCode = await _cli.RunAsync(
            ["webhooks", "delete", WebhookId], confirmation: new CtrlCAtPrompt(ctrlC), cancellationToken: ctrlC.Token);

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        Assert.Empty(_cli.Api.Requests);
        Assert.DoesNotContain("No se borró", _cli.Error);
    }

    // ---------------------------------------------------------------- secreto con una respuesta inesperada

    // Un id numérico rompe el modelo tipado: el secreto ya se generó en el servidor y no se puede perder por eso.
    private const string UnexpectedWithSecret = $$"""{"id":123,"enabledEvents":"*","secret":"{{Secret}}"}""";

    [Theory]
    [InlineData("create")]
    [InlineData("rotate-secret")]
    public async Task The_secret_is_shown_even_if_the_response_does_not_match_the_model(string subcommand)
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(UnexpectedWithSecret));
        string[] args = subcommand == "create"
            ? ["webhooks", "create", "--url", "https://example.com/webhooks", "--events", "*"]
            : ["webhooks", "rotate-secret", WebhookId];

        var exitCode = await _cli.RunAsync(args);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains($"Secreto de firma: {Secret}", _cli.Out);
        Assert.Equal(1, ApiCommandHarness.Occurrences(_cli.Out + _cli.Error, Secret));
        Assert.Contains("Guarda este secreto de firma.", _cli.Error);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("rotate-secret")]
    public async Task With_json_the_raw_data_is_printed_even_if_it_does_not_match_the_model(string subcommand)
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(UnexpectedWithSecret));
        string[] args = subcommand == "create"
            ? ["webhooks", "create", "--url", "https://example.com/webhooks", "--events", "*", "--json"]
            : ["webhooks", "rotate-secret", WebhookId, "--json"];

        var exitCode = await _cli.RunAsync(args);

        Assert.Equal(ExitCodes.Success, exitCode);
        _cli.AssertJsonOut(UnexpectedWithSecret);
        Assert.Contains("Guarda este secreto de firma.", _cli.Error);
    }

    [Fact]
    public async Task A_null_data_is_reported_as_an_unexpected_response()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, """{"data":null,"succeeded":true,"message":"","details":""}""");

        var exitCode = await _cli.RunAsync("webhooks", "retrieve", WebhookId, "--json");

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("La API respondió con éxito pero sin data.", _cli.Error);
        Assert.Empty(_cli.Out);
    }

    [Fact]
    public async Task Unauthorized_shows_the_login_hint()
    {
        _cli.Api.RespondJson(HttpStatusCode.Unauthorized, """{"succeeded":false,"message":"Invalid API KEY","details":""}""");

        var exitCode = await _cli.RunAsync("webhooks", "list");

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("fiscalapi login", _cli.Error);
    }

    /// <summary>
    /// Ctrl+C con la pregunta en pantalla, como en Windows: <c>Console.ReadLine</c> devuelve <c>null</c> (no confirma) en
    /// lugar de cancelar, y Program ya canceló el token.
    /// </summary>
    private sealed class CtrlCAtPrompt(CancellationTokenSource ctrlC) : IConfirmationPrompt
    {
        public bool IsInteractive => true;

        public async Task<bool> ConfirmAsync(string question, CancellationToken cancellationToken)
        {
            await ctrlC.CancelAsync();
            return false;
        }
    }

    // El 400 de validación de fapi-back (GlobalExceptionMiddleware): el texto de cada falla va en data.
    private static string ValidationFailure(string propertyName, string errorMessage) =>
        $$$"""{"data":[{"propertyName":"{{{propertyName}}}","errorMessage":"{{{errorMessage}}}","attemptedValue":null,"customState":null,"severity":0,"errorCode":"PredicateValidator","formattedMessagePlaceholderValues":{}}],"succeeded":false,"message":"Sorry, it's not me, it's you.","details":"One or more validation failures have occurred.","traceIdentifier":"0HN7:00000400"}""";
}
