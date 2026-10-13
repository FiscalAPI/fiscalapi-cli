using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace FiscalApi.Cli.Tests.Http;

/// <summary>
/// Simula la API: responde en orden lo que se le encola y guarda cada request recibido.
/// Así las pruebas nunca salen a Test ni a Live.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>
    /// El cuerpo de cada request, en el orden de <see cref="Requests"/> (<c>null</c> si no llevó). Se lee al recibirlo: el
    /// cliente libera el request (y su contenido) en cuanto termina.
    /// </summary>
    public List<string?> RequestBodies { get; } = [];

    public StubHttpMessageHandler RespondJson(HttpStatusCode status, string json, int times = 1) =>
        Respond(() => Json(status, json), times);

    /// <summary>Como el 502/503 de nginx: un cuerpo que no es JSON.</summary>
    public StubHttpMessageHandler RespondHtml(HttpStatusCode status, string html, int times = 1) =>
        Respond(() => new HttpResponseMessage(status)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        }, times);

    public StubHttpMessageHandler RespondWithRetryAfter(HttpStatusCode status, RetryConditionHeaderValue retryAfter) =>
        Respond(() =>
        {
            var response = Json(status, """{"succeeded":false,"message":"Intenta más tarde"}""");
            response.Headers.RetryAfter = retryAfter;
            return response;
        });

    public StubHttpMessageHandler Respond(Func<HttpResponseMessage> createResponse, int times = 1) =>
        Enqueue(times, _ => Task.FromResult(createResponse()));

    public StubHttpMessageHandler FailWithNetworkError(
        HttpRequestError error = HttpRequestError.ConnectionError, int times = 1) =>
        Enqueue(times, _ => Task.FromException<HttpResponseMessage>(
            new HttpRequestException(error, $"Falla de red simulada ({error}).")));

    /// <summary>Una API que no responde: el request termina solo cuando alguien cancela su token.</summary>
    public StubHttpMessageHandler NeverRespond() =>
        Enqueue(1, async cancellationToken =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new UnreachableException();
        });

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private StubHttpMessageHandler Enqueue(int times, Func<CancellationToken, Task<HttpResponseMessage>> response)
    {
        for (var i = 0; i < times; i++)
        {
            _responses.Enqueue(response);
        }

        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
        return _responses.TryDequeue(out var respond)
            ? await respond(cancellationToken)
            : throw new InvalidOperationException($"Request no esperado: {request.Method} {request.RequestUri}.");
    }
}
