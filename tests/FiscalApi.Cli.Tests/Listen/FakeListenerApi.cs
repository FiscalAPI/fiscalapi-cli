using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FiscalApi.Cli.Tests.Listen;

/// <summary>
/// La escucha de CLI de fapi-back (WH-014/WH-015) en Kestrel, en proceso: abre sesiones, responde el long-polling con lo
/// que la prueba encola y guarda cada acuse. Sin pasos encolados, el long-polling espera hasta que el CLI se va, como el
/// real cuando no hay entregas. Nunca se llama a Test ni a Live.
/// </summary>
internal sealed class FakeListenerApi : IAsyncDisposable
{
    public const string ListenerId = "wh_listener_0001";
    public const string Secret = "whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw";

    private const string Route = "/api/v4/webhook-endpoints";
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private readonly WebApplication _app;
    private readonly ConcurrentQueue<Func<HttpContext, Task>> _polls = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<(int Status, string Message)>> _ackRejections = new();
    private readonly ConcurrentQueue<JsonObject> _opens = new();
    private readonly ConcurrentQueue<RecordedPoll> _pollsReceived = new();
    private readonly ConcurrentQueue<RecordedAck> _acks = new();
    private int _sessions;

    private FakeListenerApi(WebApplication app) => _app = app;

    public Uri BaseAddress { get; private set; } = null!;

    public IReadOnlyCollection<JsonObject> Opens => _opens;

    public IReadOnlyCollection<RecordedPoll> PollsReceived => _pollsReceived;

    public IReadOnlyCollection<RecordedAck> Acks => _acks;

    /// <param name="certificate">Con certificado, escucha en https.</param>
    public static async Task<FakeListenerApi> StartAsync(X509Certificate2? certificate = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseShutdownTimeout(TimeSpan.FromSeconds(1));
        builder.WebHost.UseKestrel(kestrel => kestrel.Listen(System.Net.IPAddress.Loopback, 0, listen =>
        {
            if (certificate is not null)
            {
                listen.UseHttps(certificate);
            }
        }));
        var app = builder.Build();
        var api = new FakeListenerApi(app);
        api.MapRoutes();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();
        api.BaseAddress = new Uri(address + "/");
        return api;
    }

    /// <summary>El próximo long-polling responde estas entregas.</summary>
    public FakeListenerApi Deliver(params FakeDelivery[] deliveries)
    {
        var data = new JsonArray(deliveries.Select(delivery => (JsonNode)delivery.ToJson()).ToArray());
        _polls.Enqueue(context => WriteJsonAsync(context, StatusCodes.Status200OK,
            new JsonObject { ["succeeded"] = true, ["data"] = data }));
        return this;
    }

    /// <summary>El próximo long-polling responde un error con el envelope de fapi-back.</summary>
    public FakeListenerApi FailPoll(int status, string message)
    {
        _polls.Enqueue(context => WriteErrorAsync(context, status, message));
        return this;
    }

    /// <summary>El próximo long-polling corta la conexión sin responder: una caída de red a la mitad.</summary>
    public FakeListenerApi DropPoll(int times = 1)
    {
        for (var i = 0; i < times; i++)
        {
            _polls.Enqueue(context =>
            {
                context.Abort();
                return Task.CompletedTask;
            });
        }

        return this;
    }

    /// <summary>Los próximos <paramref name="times"/> acuses de esta entrega responden un error en lugar de 200.</summary>
    public FakeListenerApi RejectAck(string deliveryId, int status, string message, int times = 1)
    {
        var rejections = _ackRejections.GetOrAdd(deliveryId, _ => new ConcurrentQueue<(int, string)>());
        for (var i = 0; i < times; i++)
        {
            rejections.Enqueue((status, message));
        }

        return this;
    }

    public Task WaitForAcksAsync(int count) =>
        WaitUntilAsync(() => _acks.Count >= count, $"La API no recibió {count} acuses.");

    public Task WaitForPollsAsync(int count) =>
        WaitUntilAsync(() => _pollsReceived.Count >= count, $"La API no recibió {count} long-polling.");

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    internal static async Task WaitUntilAsync(Func<bool> condition, string failure)
    {
        var deadline = DateTime.UtcNow + WaitLimit;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException(failure);
            }

            await Task.Delay(10);
        }
    }

    private void MapRoutes()
    {
        _app.MapPost($"{Route}/cli-listener", OpenAsync);
        _app.MapGet($"{Route}/{{id}}/cli-deliveries", PollAsync);
        _app.MapPost($"{Route}/{{id}}/cli-deliveries/{{deliveryId}}/ack", AckAsync);
    }

    private async Task OpenAsync(HttpContext context)
    {
        _opens.Enqueue((await JsonNode.ParseAsync(context.Request.Body))!.AsObject());
        var session = Interlocked.Increment(ref _sessions);
        await WriteJsonAsync(context, StatusCodes.Status200OK, new JsonObject
        {
            ["succeeded"] = true,
            ["data"] = new JsonObject
            {
                ["id"] = ListenerId,
                ["sessionId"] = SessionId(session),
                ["sessionExpiresAt"] = "2026-10-13T12:01:00Z",
                ["secret"] = Secret
            }
        });
    }

    public static string SessionId(int number) => $"ses_{number:0000}";

    private async Task PollAsync(HttpContext context, string id)
    {
        _pollsReceived.Enqueue(new RecordedPoll(id, context.Request.Query["sessionId"].ToString(),
            context.Request.Query["waitSeconds"].ToString()));
        if (_polls.TryDequeue(out var respond))
        {
            await respond(context);
            return;
        }

        // Sin entregas: espera hasta que el CLI se va (Ctrl+C o fin de la prueba) y responde la lista vacía.
        try
        {
            await Task.Delay(Timeout.Infinite, context.RequestAborted);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task AckAsync(HttpContext context, string id, string deliveryId)
    {
        var body = (await JsonNode.ParseAsync(context.Request.Body))!.AsObject();
        _acks.Enqueue(new RecordedAck(id, deliveryId, body));
        if (_ackRejections.TryGetValue(deliveryId, out var rejections) && rejections.TryDequeue(out var rejection))
        {
            await WriteErrorAsync(context, rejection.Status, rejection.Message);
            return;
        }

        await WriteJsonAsync(context, StatusCodes.Status200OK, new JsonObject { ["succeeded"] = true, ["data"] = true });
    }

    private static Task WriteErrorAsync(HttpContext context, int status, string message) =>
        WriteJsonAsync(context, status, new JsonObject
        {
            ["succeeded"] = false,
            ["message"] = message,
            ["details"] = message,
            ["httpStatusCode"] = status,
            ["traceIdentifier"] = "0HN7:00000001"
        });

    private static async Task WriteJsonAsync(HttpContext context, int status, JsonObject body)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(body.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    public sealed record RecordedPoll(string ListenerId, string SessionId, string WaitSeconds);

    public sealed record RecordedAck(string ListenerId, string DeliveryId, JsonObject Body)
    {
        public string? SessionId => Body["sessionId"]?.GetValue<string>();

        public int? StatusCode => Body["statusCode"]?.GetValue<int>();

        public string? Error => Body["error"]?.GetValue<string>();

        public int? LatencyMs => Body["latencyMs"]?.GetValue<int>();
    }
}

/// <summary>Una entrega como la responde el long-polling: deliveryId, los headers firmados y el cuerpo tal cual.</summary>
internal sealed record FakeDelivery(
    string DeliveryId, string EventId, string Type, string? Body = null, string? SignatureHeader = null)
{
    public const string Timestamp = "1791900000";

    /// <summary>La firma real de Standard Webhooks (D-02) con el secreto de la escucha: el receptor la puede verificar.</summary>
    public string Signature =>
        SignatureHeader ?? StandardWebhooksSignature.Sign(EventId, Timestamp, Encoding.UTF8.GetBytes(EnvelopeBody));

    /// <summary>El sobre de D-04 con un formato que un re-serializador cambiaría (sangría y espacios).</summary>
    public string EnvelopeBody => Body ?? $$$"""
        {"id":"{{{EventId}}}","type":"{{{Type}}}","createdAt":"2026-10-12T18:47:53Z","personId":null,
         "data":{"relatedObject":{"id":"inv_1","type":"invoice","url":"/api/v4/invoices/inv_1"},"status":"X","previousStatus":"V"}}
        """;

    public IReadOnlyDictionary<string, string> Headers => new Dictionary<string, string>
    {
        ["webhook-id"] = EventId,
        ["webhook-timestamp"] = Timestamp,
        ["webhook-signature"] = Signature
    };

    public JsonObject ToJson() => new()
    {
        ["deliveryId"] = DeliveryId,
        ["headers"] = new JsonObject(Headers.Select(header =>
            KeyValuePair.Create(header.Key, (JsonNode?)JsonValue.Create(header.Value)))),
        ["body"] = EnvelopeBody
    };
}
