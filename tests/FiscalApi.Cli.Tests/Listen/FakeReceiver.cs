using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FiscalApi.Cli.Tests.Listen;

/// <summary>
/// El receptor local de la persona (<c>--forward-to</c>) en Kestrel, en proceso: guarda cada request con sus bytes y
/// headers exactos y responde lo que decide la prueba (200 por omisión).
/// </summary>
internal sealed class FakeReceiver : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentQueue<ReceivedRequest> _requests = new();

    private FakeReceiver(WebApplication app, Func<ReceivedRequest, CancellationToken, Task<int>> respond)
    {
        _app = app;
        _app.Run(async context =>
        {
            var request = await ReceivedRequest.ReadAsync(context.Request);
            _requests.Enqueue(request);
            context.Response.StatusCode = await respond(request, context.RequestAborted);
        });
    }

    public Uri Url { get; private set; } = null!;

    public IReadOnlyCollection<ReceivedRequest> Requests => _requests;

    /// <param name="respond">El código de cada request; puede tardar para simular un receptor lento.</param>
    /// <param name="certificate">Con certificado, escucha en https.</param>
    public static async Task<FakeReceiver> StartAsync(
        Func<ReceivedRequest, CancellationToken, Task<int>>? respond = null, X509Certificate2? certificate = null)
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
        var receiver = new FakeReceiver(builder.Build(), respond ?? ((_, _) => Task.FromResult(StatusCodes.Status200OK)));
        await receiver._app.StartAsync();
        var address = receiver._app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        receiver.Url = new Uri(address + "/webhooks/fiscalapi");
        return receiver;
    }

    public Task WaitForRequestsAsync(int count) =>
        FakeListenerApi.WaitUntilAsync(() => _requests.Count >= count, $"El receptor no recibió {count} requests.");

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

/// <summary>Lo que llegó al receptor, copiado: Kestrel recicla los headers de un request al terminarlo.</summary>
internal sealed record ReceivedRequest(string Method, string Path, IReadOnlyDictionary<string, string> Headers, byte[] Body)
{
    public static async Task<ReceivedRequest> ReadAsync(HttpRequest request)
    {
        using var body = new MemoryStream();
        await request.Body.CopyToAsync(body);
        var headers = request.Headers.ToDictionary(header => header.Key, header => header.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);
        return new ReceivedRequest(request.Method, request.Path.ToString(), headers, body.ToArray());
    }
}
