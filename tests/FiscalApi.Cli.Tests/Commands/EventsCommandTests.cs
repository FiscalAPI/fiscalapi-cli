using System.Net;
using System.Text;
using FiscalApi.Cli.Tests.Http;

namespace FiscalApi.Cli.Tests.Commands;

/// <summary>
/// <c>fiscalapi events list|retrieve|resend</c> contra las rutas de <c>EventsController</c> de la API,
/// simuladas con <see cref="StubHttpMessageHandler"/>. <c>retrieve</c> imprime el sobre exactamente como lo entregó la
/// API.
/// </summary>
public sealed class EventsCommandTests
{
    private const string EventsUrl = "https://api.example.test/api/v4/events";
    private const string EventId = "0d6f5c1e-8a2b-4c3d-9e8f-7a6b5c4d3e2f";

    // El sobre del evento como lo guarda y entrega la API (WebhookEnvelopeSerializer): sin espacios y con las nulas.
    private const string Envelope =
        $$$"""{"id":"{{{EventId}}}","type":"invoice.canceled","createdAt":"2026-10-12T15:47:46Z","personId":"persona-1","data":{"relatedObject":{"id":"factura-1","type":"invoice","url":"/api/v4/invoices/factura-1"},"status":"X","previousStatus":"C"}}""";

    private const string OtherEnvelope =
        """{"id":"evt-2","type":"webhook.test","createdAt":"2026-10-12T15:40:00Z","personId":null,"data":{"relatedObject":{"id":"we-1","type":"webhook","url":"/api/v4/webhooks/we-1"},"status":null,"previousStatus":null}}""";

    private const string Page =
        $$"""{"items":[{{Envelope}},{{OtherEnvelope}}],"pageNumber":1,"totalPages":1,"totalCount":2,"hasPreviousPage":false,"hasNextPage":false}""";

    private readonly ApiCommandHarness _cli = new();

    // ---------------------------------------------------------------- list

    [Fact]
    public async Task List_gets_the_events_without_query_by_default()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(Page));

        var exitCode = await _cli.RunAsync("events", "list");

        Assert.Equal(ExitCodes.Success, exitCode);
        var request = _cli.SingleRequest();
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri(EventsUrl), request.RequestUri);
    }

    [Fact]
    public async Task List_sends_every_filter_with_the_dates_in_utc()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(Page));

        await _cli.RunAsync(
            "events", "list",
            "--type", "invoice.canceled",
            "--related-object-id", "factura 1",
            "--from", "2026-10-01T08:00:00-06:00",
            "--to", "2026-10-13T23:59:59.5Z",
            "--page", "3",
            "--page-size", "20");

        Assert.Equal(
            $"{EventsUrl}?type=invoice.canceled&relatedObjectId=factura%201&from=2026-10-01T14%3A00%3A00Z" +
            "&to=2026-10-13T23%3A59%3A59.5Z&pageNumber=3&pageSize=20",
            _cli.SingleRequest().RequestUri!.AbsoluteUri);
    }

    // La API compara to incluido (createdAt <= to): con solo la fecha, el último instante de ese día en UTC.
    [Theory]
    [InlineData("2026-10-13", "2026-10-13T23%3A59%3A59.9999999Z")]
    [InlineData("2026-12-31", "2026-12-31T23%3A59%3A59.9999999Z")]
    [InlineData("2026-10-13T00:00", "2026-10-13T00%3A00%3A00Z")]
    [InlineData("2026-10-13T08:30:15-06:00", "2026-10-13T14%3A30%3A15Z")]
    public async Task To_with_only_a_date_includes_that_whole_day(string to, string sent)
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(Page));

        await _cli.RunAsync("events", "list", "--to", to);

        Assert.Equal($"{EventsUrl}?to={sent}", _cli.SingleRequest().RequestUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("2026-10-01", "2026-10-01T00%3A00%3A00Z")]
    [InlineData("2026-10-01T08:30", "2026-10-01T08%3A30%3A00Z")]
    [InlineData("2026-10-01T08:30:15", "2026-10-01T08%3A30%3A15Z")]
    [InlineData("2026-10-01T08:30:15+02:00", "2026-10-01T06%3A30%3A15Z")]
    public async Task A_date_without_zone_is_utc(string from, string sent)
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(Page));

        await _cli.RunAsync("events", "list", "--from", from);

        Assert.Equal($"{EventsUrl}?from={sent}", _cli.SingleRequest().RequestUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("--from", "10/01/2026")]
    [InlineData("--to", "ayer")]
    [InlineData("--to", "2026-13-01")]
    public async Task A_date_that_is_not_iso_8601_is_a_usage_error_without_calling_the_api(string option, string value)
    {
        var exitCode = await _cli.RunAsync("events", "list", option, value);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains($"{option} debe ser una fecha ISO 8601", _cli.Error);
        Assert.Empty(_cli.Api.Requests);
    }

    [Fact]
    public async Task List_prints_a_table_with_one_row_per_event()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(Page));

        await _cli.RunAsync("events", "list");

        var lines = _cli.Out.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Matches(@"^ID\s+TIPO\s+CREADO \(UTC\)\s+RECURSO\s+PERSONA$", lines[0]);
        Assert.Matches($@"^{EventId}\s+invoice\.canceled\s+2026-10-12T15:47:46Z\s+factura-1\s+persona-1$", lines[1]);
        Assert.Matches(@"^evt-2\s+webhook.test\s+2026-10-12T15:40:00Z\s+we-1\s+-$", lines[2]);
        Assert.Equal("Página 1 de 1 (2 en total).", lines[3]);
    }

    [Fact]
    public async Task List_with_json_prints_the_data_unchanged()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(Page));

        await _cli.RunAsync("events", "list", "--json");

        _cli.AssertJsonOut(Page);
    }

    [Fact]
    public async Task List_rejected_by_validation_exits_1_with_the_failure_text()
    {
        _cli.Api.RespondJson(HttpStatusCode.BadRequest,
            """{"data":[{"propertyName":"From","errorMessage":"El rango de fechas no puede exceder 30 días.","attemptedValue":"2026-08-01T00:00:00Z"}],"succeeded":false,"message":"Sorry, it's not me, it's you.","details":"One or more validation failures have occurred."}""");

        var exitCode = await _cli.RunAsync("events", "list", "--from", "2026-08-01");

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("El rango de fechas no puede exceder 30 días.", _cli.Error);
    }

    // ---------------------------------------------------------------- retrieve

    [Fact]
    public async Task Retrieve_gets_the_event_by_its_id()
    {
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(Envelope));

        var exitCode = await _cli.RunAsync("events", "retrieve", EventId);

        Assert.Equal(ExitCodes.Success, exitCode);
        var request = _cli.SingleRequest();
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri($"{EventsUrl}/{EventId}"), request.RequestUri);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retrieve_prints_the_envelope_byte_for_byte(bool json)
    {
        // Espacios, escapes y caracteres fuera de ASCII que una re-serialización cambiaría.
        const string envelope =
            "{ \"id\" : \"evt-1\",\n  \"type\":\"invoice.canceled\", \"nota\":\"Facturación de ñandú: 🚀\", " +
            "\"escape\":\"\\u00e9\\\"\\/\", \"monto\": 1.50e2 }";
        _cli.Api.RespondJson(HttpStatusCode.OK, ApiCommandHarness.Success(envelope));

        await _cli.RunAsync(json ? ["events", "retrieve", "evt-1", "--json"] : ["events", "retrieve", "evt-1"]);

        Assert.Equal(Encoding.UTF8.GetBytes(envelope + Environment.NewLine), Encoding.UTF8.GetBytes(_cli.Out));
    }

    [Fact]
    public async Task Retrieve_of_a_missing_event_exits_1_with_the_api_text()
    {
        _cli.Api.RespondJson(HttpStatusCode.NotFound,
            $$"""{"succeeded":false,"message":"Sorry, it's not me, it's you.","details":"No such resource for the given Id: {{EventId}}"}""");

        var exitCode = await _cli.RunAsync("events", "retrieve", EventId);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains($"No such resource for the given Id: {EventId}", _cli.Error);
        Assert.Empty(_cli.Out);
    }

    // ---------------------------------------------------------------- resend

    [Fact]
    public async Task Resend_without_webhook_posts_without_body_so_it_goes_to_the_cli_listener()
    {
        _cli.Api.RespondJson(HttpStatusCode.Accepted,
            ApiCommandHarness.Success("""{"deliveryId":"entrega-1","webhookId":"escucha-1"}"""));

        var exitCode = await _cli.RunAsync("events", "resend", EventId);

        Assert.Equal(ExitCodes.Success, exitCode);
        var request = _cli.SingleRequest();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri($"{EventsUrl}/{EventId}/resend"), request.RequestUri);
        Assert.Null(_cli.Api.RequestBodies[0]);
        Assert.Contains("entrega-1", _cli.Out);
        Assert.Contains("escucha-1", _cli.Out);
    }

    [Fact]
    public async Task Resend_with_webhook_sends_its_id()
    {
        _cli.Api.RespondJson(HttpStatusCode.Accepted,
            ApiCommandHarness.Success("""{"deliveryId":"entrega-1","webhookId":"we-1"}"""));

        await _cli.RunAsync("events", "resend", EventId, "--webhook", "we-1");

        ApiCommandHarness.AssertJsonEqual("""{"webhookId":"we-1"}""", _cli.Body());
    }

    [Fact]
    public async Task Resend_with_json_prints_the_data_unchanged()
    {
        const string accepted = """{"deliveryId":"entrega-1","webhookId":"we-1"}""";
        _cli.Api.RespondJson(HttpStatusCode.Accepted, ApiCommandHarness.Success(accepted));

        await _cli.RunAsync("events", "resend", EventId, "--webhook", "we-1", "--json");

        _cli.AssertJsonOut(accepted);
    }

    [Fact]
    public async Task Resend_without_a_listen_session_exits_1_with_the_api_text()
    {
        _cli.Api.RespondJson(HttpStatusCode.Conflict,
            """{"succeeded":false,"message":"Sorry, it's not me, it's you.","details":"La escucha de CLI no está conectada; ejecuta fiscalapi listen.","traceIdentifier":"0HN7:00000409"}""");

        var exitCode = await _cli.RunAsync("events", "resend", EventId);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("La escucha de CLI no está conectada; ejecuta fiscalapi listen.", _cli.Error);
        Assert.Contains("Código HTTP: 409", _cli.Error);
        Assert.Contains("0HN7:00000409", _cli.Error);
        Assert.Single(_cli.Api.Requests);
    }

    [Fact]
    public async Task Resend_is_never_retried()
    {
        _cli.Api.RespondHtml(HttpStatusCode.BadGateway, "<html>502</html>");

        var exitCode = await _cli.RunAsync("events", "resend", EventId);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Single(_cli.Api.Requests);
    }
}
