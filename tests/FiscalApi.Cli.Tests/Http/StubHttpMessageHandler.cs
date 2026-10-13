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
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public StubHttpMessageHandler RespondJson(HttpStatusCode status, string json, int times = 1) =>
        Enqueue(times, _ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

    /// <summary>Como el 502/503 de nginx: un cuerpo que no es JSON.</summary>
    public StubHttpMessageHandler RespondHtml(HttpStatusCode status, string html, int times = 1) =>
        Enqueue(times, _ => new HttpResponseMessage(status)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        });

    public StubHttpMessageHandler RespondTooManyRequests(TimeSpan retryAfter) =>
        Enqueue(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("""{"succeeded":false,"message":"Demasiadas solicitudes"}""", Encoding.UTF8, "application/json")
            };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter);
            return response;
        });

    public StubHttpMessageHandler FailWithNetworkError(int times = 1) =>
        Enqueue(times, _ => throw new HttpRequestException("Conexión rechazada."));

    private StubHttpMessageHandler Enqueue(Func<HttpRequestMessage, HttpResponseMessage> response) => Enqueue(1, response);

    private StubHttpMessageHandler Enqueue(int times, Func<HttpRequestMessage, HttpResponseMessage> response)
    {
        for (var i = 0; i < times; i++)
        {
            _responses.Enqueue(response);
        }

        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (!_responses.TryDequeue(out var respond))
        {
            throw new InvalidOperationException($"Request no esperado: {request.Method} {request.RequestUri}.");
        }

        return Task.FromResult(respond(request));
    }
}
