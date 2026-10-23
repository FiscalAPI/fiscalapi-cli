using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FiscalApi.Cli.Commands;
using FiscalApi.Cli.Credentials;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Listen;
using FiscalApi.Cli.Tests.Credentials;
using FiscalApi.Cli.Tests.Http;
using FiscalApi.Cli.Tests.Updates;
using Microsoft.AspNetCore.Http;

namespace FiscalApi.Cli.Tests.Listen;

public sealed class ListenCommandTests : IAsyncLifetime
{
    private const string ApiKey = "sk_test_clave-de-listen";
    private const string TenantKey = "tenant-de-listen-0001";
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private static readonly ListenTimings FastTimings = ListenTimings.Default with
    {
        ForwardTimeout = TimeSpan.FromSeconds(5),
        Reconnect = new ExponentialBackoff(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(50))
    };

    private readonly StringWriter _outBuffer = new();
    private readonly StringWriter _errorBuffer = new();
    private readonly CancellationTokenSource _ctrlC = new();
    private FakeListenerApi _api = null!;
    private FakeReceiver? _receiver;
    private Task<int>? _run;
    private ListenTimings _timings = FastTimings;

    private string Out => _outBuffer.ToString();

    private string Error => _errorBuffer.ToString();

    public async Task InitializeAsync() => _api = await FakeListenerApi.StartAsync();

    public async Task DisposeAsync()
    {
        await _ctrlC.CancelAsync();
        if (_run is not null)
        {
            await _run.WaitAsync(WaitLimit);
        }

        if (_receiver is not null)
        {
            await _receiver.DisposeAsync();
        }

        await _api.DisposeAsync();
        _ctrlC.Dispose();
    }

    [Fact]
    public async Task Receiver_gets_the_body_and_signed_headers_byte_for_byte()
    {
        const string body = "{\"id\":\"evt_0001\",\"type\":\"invoice.canceled\",\n  \"data\":{\"nota\":\"Facturación de ñandú\"," +
                            "\"espacios\":\"  a   b  \", \"escape\":\"\\u00e9\\\"\"}  }\n";
        var delivery = new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled", body);
        _api.Deliver(delivery);
        _receiver = await FakeReceiver.StartAsync();

        StartListen("--forward-to", _receiver.Url.ToString());
        await ExpectAsync(_receiver.WaitForRequestsAsync(1));

        var request = Assert.Single(_receiver.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("/webhooks/fiscalapi", request.Path);
        Assert.Equal(Encoding.UTF8.GetBytes(body), request.Body);
        Assert.Equal("evt_0001", request.Headers["webhook-id"]);
        Assert.Equal(FakeDelivery.Timestamp, request.Headers["webhook-timestamp"]);
        Assert.Equal(delivery.Signature, request.Headers["webhook-signature"]);
        Assert.Equal(StandardWebhooksSignature.Sign("evt_0001", FakeDelivery.Timestamp, request.Body),
            request.Headers["webhook-signature"]);
        Assert.Equal("application/json; charset=utf-8", request.Headers["Content-Type"]);
    }

    [Fact]
    public async Task During_a_secret_rotation_both_signatures_reach_the_receiver_unchanged()
    {
        const string rotation = "v1,K5oZfzN95Z9UVu1EsfQmfVNQhnkZ2pj9o9NDN/H/pI4= v1,bm9uY2UtZGUtbGEtZmlybWEtYW50ZXJpb3I=";
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled", SignatureHeader: rotation));
        _receiver = await FakeReceiver.StartAsync();

        StartListen("--forward-to", _receiver.Url.ToString());
        await ExpectAsync(_receiver.WaitForRequestsAsync(1));

        Assert.Equal(rotation, Assert.Single(_receiver.Requests).Headers["webhook-signature"]);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(422)]
    public async Task Ack_carries_the_receiver_status_code_and_the_session(int receiverStatus)
    {
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));
        _receiver = await FakeReceiver.StartAsync((_, _) => Task.FromResult(receiverStatus));

        StartListen("--forward-to", _receiver.Url.ToString());
        await ExpectAsync(_api.WaitForAcksAsync(1));

        var ack = Assert.Single(_api.Acks);
        Assert.Equal(FakeListenerApi.ListenerId, ack.ListenerId);
        Assert.Equal("dlv_0001", ack.DeliveryId);
        Assert.Equal(FakeListenerApi.SessionId(1), ack.SessionId);
        Assert.Equal(receiverStatus, ack.StatusCode);
        Assert.InRange(ack.LatencyMs!.Value, 0, 10_000);
        Assert.False(ack.Body.ContainsKey("error"), "Un acuse con statusCode no lleva error: la API pide uno de los dos.");
    }

    [Fact]
    public async Task A_refused_connection_is_acked_with_an_error()
    {
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));
        using var closed = new ClosedPort();

        StartListen("--forward-to", closed.Url("webhooks").ToString());
        await ExpectAsync(_api.WaitForAcksAsync(1));

        var ack = Assert.Single(_api.Acks);
        Assert.Equal("ConnectionFailed", ack.Error);
        Assert.Null(ack.StatusCode);
        Assert.Equal(FakeListenerApi.SessionId(1), ack.SessionId);
    }

    [Fact]
    public async Task A_receiver_that_does_not_answer_in_time_is_acked_with_a_timeout()
    {
        _timings = FastTimings with { ForwardTimeout = TimeSpan.FromMilliseconds(500) };
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));
        _receiver = await FakeReceiver.StartAsync(async (_, aborted) =>
        {
            await Task.Delay(Timeout.Infinite, aborted);
            return StatusCodes.Status200OK;
        });

        StartListen("--forward-to", _receiver.Url.ToString());
        await ExpectAsync(_api.WaitForAcksAsync(1));

        var ack = Assert.Single(_api.Acks);
        Assert.Equal("Timeout", ack.Error);
        Assert.Null(ack.StatusCode);
        Assert.InRange(ack.LatencyMs!.Value, 400, 10_000);
    }

    [Fact]
    public async Task Without_forward_to_events_are_only_shown_and_acked()
    {
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"),
            new FakeDelivery("dlv_0002", "evt_0002", "invoice.created"));

        StartListen();
        await ExpectAsync(_api.WaitForAcksAsync(2));

        Assert.Equal(["dlv_0001", "dlv_0002"], _api.Acks.Select(ack => ack.DeliveryId).Order());
        Assert.All(_api.Acks, ack =>
        {
            Assert.Equal(200, ack.StatusCode);
            Assert.Equal(0, ack.LatencyMs);
            Assert.Null(ack.Error);
            Assert.Equal(FakeListenerApi.SessionId(1), ack.SessionId);
        });
        Assert.Matches(EventLine("invoice.canceled", "evt_0001", suffix: ""), Out);
        Assert.Matches(EventLine("invoice.created", "evt_0002", suffix: ""), Out);
    }

    [Fact]
    public async Task Each_forwarded_event_prints_a_line_with_the_receiver_result()
    {
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));
        _receiver = await FakeReceiver.StartAsync((_, _) => Task.FromResult(StatusCodes.Status500InternalServerError));

        StartListen("--forward-to", _receiver.Url.ToString());
        await ExpectAsync(_api.WaitForAcksAsync(1));

        Assert.Matches(EventLine("invoice.canceled", "evt_0001", suffix: " [500]"), Out);
    }

    [Fact]
    public async Task A_forward_without_response_prints_the_error_on_its_line()
    {
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));
        using var closed = new ClosedPort();

        StartListen("--forward-to", closed.Url("webhooks").ToString());
        await ExpectAsync(_api.WaitForAcksAsync(1));

        Assert.Matches(EventLine("invoice.canceled", "evt_0001", suffix: " [error: ConnectionFailed]"), Out);
    }

    [Theory]
    [InlineData(null, new[] { "*" })]
    [InlineData("invoice.canceled", new[] { "invoice.canceled" })]
    [InlineData("invoice.canceled,invoice.created", new[] { "invoice.canceled", "invoice.created" })]
    [InlineData(" invoice.canceled , invoice.created ", new[] { "invoice.canceled", "invoice.created" })]
    public async Task Events_option_reaches_the_session_opening(string? events, string[] enabledEvents)
    {
        StartListen(events is null ? [] : ["--events", events]);
        await ExpectAsync(_api.WaitForPollsAsync(1));

        var open = Assert.Single(_api.Opens);
        Assert.Equal(enabledEvents, open["enabledEvents"]!.AsArray().Select(type => type!.GetValue<string>()));
    }

    [Fact]
    public async Task Headers_option_is_added_to_each_forward()
    {
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));
        _receiver = await FakeReceiver.StartAsync();

        StartListen("--forward-to", _receiver.Url.ToString(), "--headers", "X-Origen:fiscalapi-cli, X-Traza: abc:123");
        await ExpectAsync(_receiver.WaitForRequestsAsync(1));

        var request = Assert.Single(_receiver.Requests);
        Assert.Equal("fiscalapi-cli", request.Headers["X-Origen"]);
        Assert.Equal("abc:123", request.Headers["X-Traza"]);
        Assert.Equal("evt_0001", request.Headers["webhook-id"]);
    }

    [Fact]
    public async Task A_slow_delivery_does_not_hold_the_long_polling()
    {
        var releaseSlow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _api.Deliver(new FakeDelivery("dlv_lenta", "evt_0001", "invoice.canceled"))
            .Deliver(new FakeDelivery("dlv_rapida", "evt_0002", "invoice.created"));
        _receiver = await FakeReceiver.StartAsync(async (request, aborted) =>
        {
            if (request.Headers["webhook-id"] == "evt_0001")
            {
                await releaseSlow.Task.WaitAsync(aborted);
            }

            return StatusCodes.Status200OK;
        });

        StartListen("--forward-to", _receiver.Url.ToString());
        await ExpectAsync(_api.WaitForAcksAsync(1));

        Assert.Equal("dlv_rapida", Assert.Single(_api.Acks).DeliveryId);
        releaseSlow.SetResult();
        await ExpectAsync(_api.WaitForAcksAsync(2));
        Assert.Equal(200, _api.Acks.Single(ack => ack.DeliveryId == "dlv_lenta").StatusCode);
    }

    [Fact]
    public async Task A_network_drop_reconnects_with_backoff_without_losing_deliveries()
    {
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"))
            .DropPoll(times: 3)
            .FailPoll(StatusCodes.Status503ServiceUnavailable, "Servicio no disponible")
            .FailPoll(StatusCodes.Status503ServiceUnavailable, "Servicio no disponible")
            .FailPoll(StatusCodes.Status503ServiceUnavailable, "Servicio no disponible")
            .Deliver(new FakeDelivery("dlv_0002", "evt_0002", "invoice.created"));
        _receiver = await FakeReceiver.StartAsync();

        StartListen("--forward-to", _receiver.Url.ToString());
        await ExpectAsync(_api.WaitForAcksAsync(2));

        Assert.Equal(["dlv_0001", "dlv_0002"], _api.Acks.Select(ack => ack.DeliveryId).Order());
        Assert.Equal(["evt_0001", "evt_0002"], _receiver.Requests.Select(request => request.Headers["webhook-id"]).Order());
        Assert.All(_api.PollsReceived, poll => Assert.Equal(FakeListenerApi.SessionId(1), poll.SessionId));
        Assert.Single(_api.Opens);
        Assert.Matches(@"Lost connection to FiscalAPI \(.+\)\. Retrying in \d+(\.\d)? s\.", Error);
        Assert.Contains("Reconnected to FiscalAPI.", Error);
        Assert.False(_run!.IsCompleted, "listen terminó por una caída de red.");
    }

    [Fact]
    public async Task An_expired_session_is_reopened_without_exiting()
    {
        _api.FailPoll(StatusCodes.Status410Gone, "La sesión de fiscalapi listen caducó; vuelve a abrirla.")
            .Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));

        StartListen("--events", "invoice.canceled");
        await ExpectAsync(_api.WaitForAcksAsync(1));

        Assert.Equal(2, _api.Opens.Count);
        Assert.All(_api.Opens, open => Assert.Equal("invoice.canceled", open["enabledEvents"]![0]!.GetValue<string>()));
        Assert.Equal(
            [FakeListenerApi.SessionId(1), FakeListenerApi.SessionId(2)],
            _api.PollsReceived.Take(2).Select(poll => poll.SessionId));
        Assert.Equal(FakeListenerApi.SessionId(2), Assert.Single(_api.Acks).SessionId);
        Assert.Contains("The listen session expired. Opening a new one.", Error);
        Assert.False(_run!.IsCompleted, "listen terminó por una sesión caducada.");
    }

    [Fact]
    public async Task A_replaced_session_exits_1_with_the_api_text()
    {
        _api.FailPoll(StatusCodes.Status409Conflict, "Otra sesión de fiscalapi listen reemplazó a esta.");

        StartListen();

        Assert.Equal(ExitCodes.Failure, await ExitCodeAsync());
        Assert.Contains("Error: Otra sesión de fiscalapi listen reemplazó a esta.", Error);
        Assert.Single(_api.Opens);
    }

    [Fact]
    public async Task Ctrl_c_exits_130()
    {
        StartListen();
        await ExpectAsync(_api.WaitForPollsAsync(1));

        Assert.Equal(ExitCodes.Cancelled, await PressCtrlCAsync());
    }

    [Fact]
    public async Task Ctrl_c_during_a_forward_exits_130_without_waiting_for_the_receiver()
    {
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));
        _receiver = await FakeReceiver.StartAsync(async (_, aborted) =>
        {
            await Task.Delay(Timeout.Infinite, aborted);
            return StatusCodes.Status200OK;
        });
        StartListen("--forward-to", _receiver.Url.ToString());
        await ExpectAsync(_receiver.WaitForRequestsAsync(1));

        Assert.Equal(ExitCodes.Cancelled, await PressCtrlCAsync());
        Assert.Empty(_api.Acks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_409_on_the_ack_is_reported_only_with_verbose(bool verbose)
    {
        const string notAwaiting = "La entrega ya no espera un acuse.";
        _api.RejectAck("dlv_0001", StatusCodes.Status409Conflict, notAwaiting)
            .Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"))
            .Deliver(new FakeDelivery("dlv_0002", "evt_0001", "invoice.canceled"));

        StartListen(verbose ? ["--verbose"] : []);
        await ExpectAsync(_api.WaitForAcksAsync(2));
        var diagnostic = $"Could not acknowledge delivery dlv_0001: {notAwaiting}";
        if (verbose)
        {
            await WaitForErrorAsync(diagnostic);
        }

        Assert.Equal(2, _api.Acks.Count);
        Assert.False(_run!.IsCompleted, "listen terminó por un acuse tardío.");
        Assert.Equal(ExitCodes.Cancelled, await PressCtrlCAsync());
        Assert.Equal(verbose, Error.Contains(diagnostic));
    }

    [Fact]
    public async Task Another_rejected_ack_is_reported_and_listen_goes_on()
    {
        _api.RejectAck("dlv_0001", StatusCodes.Status404NotFound, "No se encontró el recurso dlv_0001.")
            .Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"))
            .Deliver(new FakeDelivery("dlv_0002", "evt_0002", "invoice.created"));

        StartListen();
        await ExpectAsync(_api.WaitForAcksAsync(2));
        await ExpectAsync(_api.WaitForPollsAsync(3));

        await WaitForErrorAsync("Could not acknowledge delivery dlv_0001: No se encontró el recurso dlv_0001.");
        Assert.False(_run!.IsCompleted, "listen terminó por un acuse rechazado.");
    }

    [Fact]
    public async Task An_ack_that_fails_on_the_network_or_with_a_5xx_is_retried()
    {
        _api.RejectAck("dlv_0001", StatusCodes.Status503ServiceUnavailable, "Servicio no disponible", times: 2)
            .Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));

        StartListen();
        await ExpectAsync(_api.WaitForAcksAsync(3));

        Assert.All(_api.Acks, ack => Assert.Equal(("dlv_0001", 200), (ack.DeliveryId, ack.StatusCode)));
        Assert.DoesNotContain("Could not acknowledge", Error);
    }

    [Fact]
    public async Task Print_secret_prints_only_the_secret_and_exits_0()
    {
        StartListen("--print-secret", "--events", "invoice.canceled");

        Assert.Equal(ExitCodes.Success, await ExitCodeAsync());
        Assert.Equal(FakeListenerApi.Secret + Environment.NewLine, Out);
        Assert.Empty(Error);
        Assert.Equal("invoice.canceled", Assert.Single(_api.Opens)["enabledEvents"]![0]!.GetValue<string>());
        Assert.Empty(_api.PollsReceived);
    }

    [Fact]
    public async Task Print_secret_with_json_prints_a_json_object()
    {
        StartListen("--print-secret", "--json");

        Assert.Equal(ExitCodes.Success, await ExitCodeAsync());
        var line = Assert.Single(Out.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(FakeListenerApi.Secret, JsonNode.Parse(line)!["secret"]!.GetValue<string>());
    }

    [Fact]
    public async Task Json_mode_writes_one_json_object_per_event_and_nothing_else_to_stdout()
    {
        var forwarded = new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled");
        var failed = new FakeDelivery("dlv_0002", "evt_0002", "invoice.created");
        _api.Deliver(forwarded, failed);
        _receiver = await FakeReceiver.StartAsync((request, _) => Task.FromResult(
            request.Headers["webhook-id"] == "evt_0001" ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest));

        StartListen("--forward-to", _receiver.Url.ToString(), "--json");
        await ExpectAsync(_api.WaitForAcksAsync(2));

        var lines = Out.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonNode.Parse(line)!.AsObject())
            .OrderBy(line => line["deliveryId"]!.GetValue<string>())
            .ToList();
        Assert.Equal(2, lines.Count);
        AssertEventRecord(lines[0], forwarded, statusCode: 200);
        AssertEventRecord(lines[1], failed, statusCode: 400);
        Assert.Contains($"Ready. Your signing secret is {FakeListenerApi.Secret}", Error);
    }

    private static void AssertEventRecord(JsonObject record, FakeDelivery delivery, int statusCode)
    {
        Assert.True(DateTimeOffset.TryParse(record["time"]!.GetValue<string>(), out _), "time no es una fecha ISO 8601.");
        Assert.Equal(delivery.DeliveryId, record["deliveryId"]!.GetValue<string>());
        Assert.Equal(delivery.EventId, record["eventId"]!.GetValue<string>());
        Assert.Equal(delivery.Type, record["type"]!.GetValue<string>());
        Assert.Equal(statusCode, record["statusCode"]!.GetValue<int>());
        Assert.True(record["latencyMs"]!.GetValue<int>() >= 0);
        Assert.False(record.ContainsKey("error"), "Con statusCode no va error.");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(delivery.EnvelopeBody), record["event"]), "event no es el sobre recibido.");
    }

    [Fact]
    public async Task Json_mode_reports_a_forward_without_response_with_its_error()
    {
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));
        using var closed = new ClosedPort();

        StartListen("--forward-to", closed.Url("webhooks").ToString(), "--json");
        await ExpectAsync(_api.WaitForAcksAsync(1));

        var record = JsonNode.Parse(Out)!.AsObject();
        Assert.Equal("ConnectionFailed", record["error"]!.GetValue<string>());
        Assert.False(record.ContainsKey("statusCode"), "Sin respuesta no va statusCode.");
    }

    [Fact]
    public async Task Json_mode_without_forward_to_writes_the_event_without_a_result()
    {
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));

        StartListen("--json");
        await ExpectAsync(_api.WaitForAcksAsync(1));

        var record = JsonNode.Parse(Out)!.AsObject();
        Assert.Equal("evt_0001", record["eventId"]!.GetValue<string>());
        Assert.Equal(["time", "deliveryId", "eventId", "type", "event"], record.Select(property => property.Key));
    }

    [Fact]
    public async Task Skip_verify_accepts_a_self_signed_receiver()
    {
        using var certificate = SelfSignedCertificate.ForLoopback();
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));
        _receiver = await FakeReceiver.StartAsync(certificate: certificate);

        StartListen("--forward-to", _receiver.Url.ToString(), "--skip-verify");
        await ExpectAsync(_api.WaitForAcksAsync(1));

        Assert.Equal(Uri.UriSchemeHttps, _receiver.Url.Scheme);
        Assert.Single(_receiver.Requests);
        Assert.Equal(200, Assert.Single(_api.Acks).StatusCode);
    }

    [Fact]
    public async Task Without_skip_verify_a_self_signed_receiver_is_acked_as_a_tls_failure()
    {
        using var certificate = SelfSignedCertificate.ForLoopback();
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));
        _receiver = await FakeReceiver.StartAsync(certificate: certificate);

        StartListen("--forward-to", _receiver.Url.ToString());
        await ExpectAsync(_api.WaitForAcksAsync(1));

        Assert.Empty(_receiver.Requests);
        Assert.Equal("TlsFailed", Assert.Single(_api.Acks).Error);
    }

    [Fact]
    public async Task Skip_verify_never_relaxes_tls_toward_the_api()
    {
        using var certificate = SelfSignedCertificate.ForLoopback();
        await _api.DisposeAsync();
        _api = await FakeListenerApi.StartAsync(certificate);
        _receiver = await FakeReceiver.StartAsync();

        StartListen("--forward-to", _receiver.Url.ToString(), "--skip-verify");

        Assert.Equal(ExitCodes.Failure, await ExitCodeAsync());
        Assert.Contains($"Could not connect to {_api.BaseAddress}", Error);
        Assert.Empty(_api.Opens);
    }

    [Fact]
    public async Task An_unreachable_api_at_start_exits_1_without_retrying()
    {
        using var closed = new ClosedPort();
        var unreachable = closed.Url();

        StartListen(apiBase: unreachable);

        Assert.Equal(ExitCodes.Failure, await ExitCodeAsync());
        Assert.Contains($"Could not connect to {unreachable}", Error);
        Assert.DoesNotContain("Retrying", Error);
    }

    [Fact]
    public async Task Api_credentials_never_reach_the_forward_target()
    {
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));
        _receiver = await FakeReceiver.StartAsync();

        StartListen("--forward-to", _receiver.Url.ToString(), "--verbose");
        await ExpectAsync(_receiver.WaitForRequestsAsync(1));

        var request = Assert.Single(_receiver.Requests);
        Assert.DoesNotContain(request.Headers, header =>
            header.Key.Equals("X-API-KEY", StringComparison.OrdinalIgnoreCase) ||
            header.Key.Equals("X-TENANT-KEY", StringComparison.OrdinalIgnoreCase) ||
            header.Value.Contains(ApiKey, StringComparison.Ordinal) ||
            header.Value.Contains(TenantKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Opening_prints_the_approved_ready_line_with_the_secret()
    {
        StartListen();
        await ExpectAsync(_api.WaitForPollsAsync(1));

        Assert.Contains($"Ready. Your signing secret is {FakeListenerApi.Secret} (listen session {FakeListenerApi.ListenerId}).", Error);
        var poll = Assert.Single(_api.PollsReceived);
        Assert.Equal(new FakeListenerApi.RecordedPoll(FakeListenerApi.ListenerId, FakeListenerApi.SessionId(1), "25"), poll);
    }

    [Fact]
    public async Task The_secret_appears_only_on_the_ready_line_even_with_verbose()
    {
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));

        StartListen("--verbose");
        await ExpectAsync(_api.WaitForAcksAsync(1));

        var output = Out + Error;
        Assert.Equal(1, Regex.Count(output, Regex.Escape(FakeListenerApi.Secret)));
        Assert.Contains("> POST", Error);
        Assert.DoesNotContain(ApiKey, output);
        Assert.DoesNotContain(TenantKey, output);
    }

    [Fact]
    public async Task Print_secret_help_warns_that_it_replaces_a_running_listen()
    {
        StartListen("--help");

        Assert.Equal(ExitCodes.Success, await ExitCodeAsync());
        Assert.Contains("Ends any running listen with the same API key.", Out);
    }

    [Fact]
    public async Task A_chain_of_one_event_neither_holds_the_long_polling_nor_forwards_expired_deliveries()
    {
        _timings = FastTimings with { DeliveryLease = TimeSpan.FromSeconds(1), MaxInFlightDeliveries = 2 };
        var chain = Enumerable.Range(1, 25)
            .Select(n => new FakeDelivery($"dlv_c{n:00}", "evt_0001", "invoice.canceled"))
            .ToArray();
        _api.Deliver(chain).Deliver(new FakeDelivery("dlv_otra", "evt_0002", "invoice.created"));
        _receiver = await FakeReceiver.StartAsync(async (request, aborted) =>
        {
            if (request.Headers["webhook-id"] == "evt_0001")
            {
                await Task.Delay(300, aborted);
            }

            return StatusCodes.Status200OK;
        });
        var started = Stopwatch.StartNew();

        StartListen("--forward-to", _receiver.Url.ToString());
        await ExpectAsync(FakeListenerApi.WaitUntilAsync(() => _api.Acks.Any(ack => ack.DeliveryId == "dlv_otra"),
            "La entrega del otro evento nunca se acusó."));

        Assert.True(started.Elapsed < TimeSpan.FromSeconds(2),
            $"El long-polling esperó a la cadena del mismo evento ({started.Elapsed.TotalSeconds:0.0} s).");
        await ExpectAsync(FakeListenerApi.WaitUntilAsync(() => Error.Contains("dlv_c25"), "dlv_c25 nunca se descartó."));
        var forwarded = _receiver.Requests.Count(request => request.Headers["webhook-id"] == "evt_0001");
        Assert.InRange(forwarded, 1, 6);
        Assert.Equal(forwarded, _api.Acks.Count(ack => ack.DeliveryId.StartsWith("dlv_c", StringComparison.Ordinal)));
        Assert.Contains("Delivery dlv_c25 (evt_0001) expired before forwarding. The API closed it unacknowledged.", Error);
        Assert.False(_run!.IsCompleted, "listen terminó.");
    }

    [Fact]
    public async Task Deliveries_of_the_same_event_reach_the_receiver_in_arrival_order()
    {
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _api.Deliver(
            new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled", Body: """{"id":"evt_0001","type":"invoice.canceled","n":1}"""),
            new FakeDelivery("dlv_0002", "evt_0001", "invoice.canceled", Body: """{"id":"evt_0001","type":"invoice.canceled","n":2}"""));
        _receiver = await FakeReceiver.StartAsync(async (request, aborted) =>
        {
            if (Encoding.UTF8.GetString(request.Body).Contains("\"n\":1", StringComparison.Ordinal))
            {
                await releaseFirst.Task.WaitAsync(aborted);
            }

            return StatusCodes.Status200OK;
        });

        StartListen("--forward-to", _receiver.Url.ToString());
        await ExpectAsync(_receiver.WaitForRequestsAsync(1));
        await Task.Delay(300);

        Assert.Single(_receiver.Requests);
        releaseFirst.SetResult();
        await ExpectAsync(_receiver.WaitForRequestsAsync(2));
        Assert.Equal(["\"n\":1", "\"n\":2"], _receiver.Requests.Select(request => Encoding.UTF8.GetString(request.Body)[^6..^1]));
    }

    [Fact]
    public async Task A_409_while_reopening_is_retried_with_backoff_instead_of_exiting()
    {
        _api.FailPoll(StatusCodes.Status410Gone, "La sesión de fiscalapi listen caducó; vuelve a abrirla.")
            .FailOpen(StatusCodes.Status409Conflict, "Otro cambio a los webhooks está en curso; intenta de nuevo.")
            .Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));

        StartListen();
        await ExpectAsync(_api.WaitForAcksAsync(1));

        Assert.Equal(3, _api.Opens.Count);
        Assert.Equal(FakeListenerApi.SessionId(2), Assert.Single(_api.Acks).SessionId);
        Assert.Matches(@"Could not reopen the listen session \(Otro cambio a los webhooks está en curso; " +
                       @"intenta de nuevo\)\. Retrying in \d+(\.\d)? s\.", Error);
        Assert.False(_run!.IsCompleted, "listen terminó por el candado de la apertura.");
    }

    [Fact]
    public async Task A_410_after_another_without_a_successful_poll_waits_before_reopening()
    {
        _timings = FastTimings with
        {
            Reconnect = new ExponentialBackoff(TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(400))
        };
        const string expired = "La sesión de fiscalapi listen caducó; vuelve a abrirla.";
        _api.FailPoll(StatusCodes.Status410Gone, expired)
            .FailPoll(StatusCodes.Status410Gone, expired)
            .Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));

        StartListen();
        await ExpectAsync(_api.WaitForAcksAsync(1));

        var opens = _api.OpenTimes.ToArray();
        Assert.Equal(3, opens.Length);
        Assert.True(opens[2] - opens[1] >= TimeSpan.FromMilliseconds(190),
            $"Reabrió a los {(opens[2] - opens[1]).TotalMilliseconds:0} ms.");
    }

    [Theory]
    [InlineData("HTTP/1.1 700 Fuera de rango")]
    [InlineData("HTTP/1.1 999 Fuera de rango")]
    public async Task A_receiver_status_outside_100_599_is_acked_as_an_invalid_response(string statusLine)
    {
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));
        using var receiver = new RawHttpReceiver(statusLine);

        StartListen("--forward-to", receiver.Url.ToString());
        await ExpectAsync(_api.WaitForAcksAsync(1));

        var ack = Assert.Single(_api.Acks);
        Assert.Equal("InvalidResponse", ack.Error);
        Assert.Null(ack.StatusCode);
        Assert.Equal(1, receiver.Requests);
        Assert.Matches(EventLine("invoice.canceled", "evt_0001", suffix: " [error: InvalidResponse]"), Out);
    }

    [Fact]
    public async Task A_body_that_is_not_the_envelope_without_webhook_id_is_still_shown_and_acked()
    {
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled", Body: "esto no es el sobre", WithoutEventIdHeader: true))
            .Deliver(new FakeDelivery("dlv_0002", "evt_0002", "invoice.created"));

        StartListen();
        await ExpectAsync(_api.WaitForAcksAsync(2));

        Assert.Matches(EventLine("?", "?", suffix: ""), Out);
        Assert.Matches(EventLine("invoice.created", "evt_0002", suffix: ""), Out);
        Assert.Equal(200, _api.Acks.Single(ack => ack.DeliveryId == "dlv_0001").StatusCode);
        Assert.False(_run!.IsCompleted, "listen terminó por un cuerpo inesperado.");
    }

    [Fact]
    public async Task A_body_that_is_not_json_keeps_the_header_id_and_json_mode_omits_the_event()
    {
        _api.Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled", Body: "esto no es el sobre"));

        StartListen("--json");
        await ExpectAsync(_api.WaitForAcksAsync(1));

        var record = JsonNode.Parse(Out)!.AsObject();
        Assert.Equal("evt_0001", record["eventId"]!.GetValue<string>());
        Assert.Equal("?", record["type"]!.GetValue<string>());
        Assert.False(record.ContainsKey("event"), "Un cuerpo que no es JSON no va como event.");
    }

    [Fact]
    public async Task Ctrl_c_sends_the_pending_acks_of_results_already_obtained_and_exits_130()
    {
        _api.HoldAck("dlv_0001").Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));
        StartListen();
        await ExpectAsync(_api.WaitForAcksAsync(1));

        Assert.Equal(ExitCodes.Cancelled, await PressCtrlCAsync());

        Assert.Equal(2, _api.Acks.Count);
        Assert.All(_api.Acks, ack =>
            Assert.Equal(("dlv_0001", 200, FakeListenerApi.SessionId(1)), (ack.DeliveryId, ack.StatusCode, ack.SessionId)));
    }

    [Fact]
    public async Task Ctrl_c_gives_the_pending_acks_only_a_short_deadline()
    {
        _timings = FastTimings with { ShutdownAckTimeout = TimeSpan.FromMilliseconds(300) };
        _api.HoldAck("dlv_0001", times: 2).Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));
        StartListen();
        await ExpectAsync(_api.WaitForAcksAsync(1));
        var pressed = Stopwatch.StartNew();

        Assert.Equal(ExitCodes.Cancelled, await PressCtrlCAsync());

        Assert.True(pressed.Elapsed < TimeSpan.FromSeconds(3), $"Ctrl+C tardó {pressed.Elapsed.TotalSeconds:0.0} s.");
        Assert.Equal(2, _api.Acks.Count);
    }

    [Fact]
    public async Task Ack_retries_stop_when_the_lease_expires()
    {
        _timings = FastTimings with { DeliveryLease = TimeSpan.FromMilliseconds(500) };
        _api.RejectAck("dlv_0001", StatusCodes.Status503ServiceUnavailable, "Servicio no disponible", times: 1000)
            .Deliver(new FakeDelivery("dlv_0001", "evt_0001", "invoice.canceled"));

        StartListen();
        await ExpectAsync(FakeListenerApi.WaitUntilAsync(
            () => Error.Contains("Could not acknowledge delivery dlv_0001: Servicio no disponible"),
            "El acuse nunca dejó de reintentarse."));

        var attempts = _api.Acks.Count;
        Assert.InRange(attempts, 2, 60);
        await Task.Delay(200);
        Assert.Equal(attempts, _api.Acks.Count);
    }

    [Theory]
    [InlineData(StatusCodes.Status401Unauthorized, "Invalid API KEY")]
    [InlineData(StatusCodes.Status403Forbidden, "No tienes permiso para esta operación.")]
    [InlineData(StatusCodes.Status404NotFound, "No se encontró el recurso wh_listener_0001.")]
    public async Task A_4xx_on_the_long_polling_exits_1(int status, string message)
    {
        _api.FailPoll(status, message);

        StartListen();

        Assert.Equal(ExitCodes.Failure, await ExitCodeAsync());
        Assert.Contains($"Error: {message}", Error);
        Assert.Single(_api.Opens);
    }

    [Theory]
    [InlineData("--forward-to|localhost:3000/webhooks", "--forward-to must be an absolute http or https URL")]
    [InlineData("--forward-to|ftp://localhost/webhooks", "--forward-to must be an absolute http or https URL")]
    [InlineData("--forward-to|http://localhost:3000|--headers|X-Origen", "--headers expects comma-separated Name:Value pairs")]
    [InlineData("--forward-to|http://localhost:3000|--headers|:valor", "--headers expects comma-separated Name:Value pairs")]
    [InlineData("--forward-to|http://localhost:3000|--headers|Webhook-Signature:v1,falsa", "--headers cannot set Webhook-Signature")]
    [InlineData("--forward-to|http://localhost:3000|--headers|Content-Type:text/plain", "--headers has a header that HTTP does not allow: 'Content-Type'")]
    [InlineData("--forward-to|http://localhost:3000|--headers|X Malo:1", "--headers has a header that HTTP does not allow: 'X Malo'")]
    [InlineData("--headers|X-Origen:cli", "--headers requires --forward-to.")]
    [InlineData("--skip-verify", "--skip-verify requires --forward-to.")]
    [InlineData("--events|invoice.canceled|--events|invoice.created", "'--events'")]
    [InlineData("--forward-to|http://localhost:3000|--headers|X-A:1|--headers|X-B:2", "'--headers'")]
    [InlineData("--forward-to|http://localhost:3000|--forward-to|http://localhost:3001", "'--forward-to'")]
    [InlineData("--print-secret|--forward-to|http://localhost:3000", "--print-secret cannot be used with --forward-to.")]
    [InlineData("--print-secret|--forward-to|http://localhost:3000|--headers|X-A:1", "--print-secret cannot be used with --headers.")]
    [InlineData("--print-secret|--forward-to|http://localhost:3000|--skip-verify", "--print-secret cannot be used with --skip-verify.")]
    public async Task Invalid_options_are_usage_errors_without_calling_the_api(string options, string error)
    {
        StartListen(options.Split('|'));

        Assert.Equal(ExitCodes.Usage, await ExitCodeAsync());
        Assert.Contains(error, Error);
        Assert.Empty(_api.Opens);
        Assert.Empty(Out);
    }

    private static string EventLine(string type, string eventId, string suffix) =>
        $@"(?m)^\d{{4}}-\d{{2}}-\d{{2}} \d{{2}}:\d{{2}}:\d{{2}}  {Regex.Escape(type)} \[{Regex.Escape(eventId)}\]{Regex.Escape(suffix)}\r?$";

    private Task WaitForErrorAsync(string line) =>
        ExpectAsync(FakeListenerApi.WaitUntilAsync(() => Error.Contains(line, StringComparison.Ordinal), $"No llegó a stderr: {line}"));

    private async Task ExpectAsync(Task condition)
    {
        try
        {
            await condition;
        }
        catch (TimeoutException exception)
        {
            var exit = _run is { IsCompleted: true } ? $"salió con {await _run}" : "sigue corriendo";
            throw new TimeoutException(
                $"{exception.Message} El CLI {exit}.{Environment.NewLine}stdout:{Environment.NewLine}{Out}" +
                $"{Environment.NewLine}stderr:{Environment.NewLine}{Error}", exception);
        }
    }

    private Task<int> ExitCodeAsync() => _run!.WaitAsync(WaitLimit);

    private async Task<int> PressCtrlCAsync()
    {
        await _ctrlC.CancelAsync();
        return await ExitCodeAsync();
    }

    private void StartListen(params string[] options) => StartListen(_api.BaseAddress, options);

    private void StartListen(Uri apiBase, params string[] options)
    {
        var globalOptions = new GlobalOptions();
        var console = new CliConsole(TextWriter.Synchronized(_outBuffer), TextWriter.Synchronized(_errorBuffer));
        var credentials = new CredentialResolver(new InMemoryCredentialStore(), name => name switch
        {
            CredentialResolver.ApiKeyVariable => ApiKey,
            CredentialResolver.TenantKeyVariable => TenantKey,
            _ => null
        });
        var apiClients = new FiscalApiClientFactory(
            FiscalApiClientFactory.CreateHandler(), credentials, console.Error, FiscalApiClientBuilder.NoDelayRetryPolicy);
        var listen = new ListenCommand(globalOptions, apiClients, console, TimeProvider.System, _timings);
        var application = new CliApplication(globalOptions, [listen], console, NoUpdateCheck.Instance);

        string[] args = ["listen", "--api-base", apiBase.ToString(), .. options];
        _run = Task.Run(() => application.RunAsync(args, _ctrlC.Token));
    }
}
