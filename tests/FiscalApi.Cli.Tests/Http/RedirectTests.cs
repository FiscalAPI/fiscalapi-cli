using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using FiscalApi.Cli.Errors;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Tests.Http;

/// <summary>
/// El handler de producción no sigue redirecciones: así un 3xx no lleva X-API-KEY ni X-TENANT-KEY a otro host
/// y un POST no se convierte en GET. Usa un servidor HTTP local en proceso, nunca Test ni Live.
/// </summary>
public sealed class RedirectTests : IDisposable
{
    private readonly HttpListener _server = new();
    private readonly ConcurrentQueue<string> _hits = new();
    private readonly Uri _baseAddress;

    public RedirectTests()
    {
        _baseAddress = new Uri($"http://localhost:{FreePort()}/");
        _server.Prefixes.Add(_baseAddress.ToString());
        _server.Start();
        _ = ServeAsync();
    }

    [Fact]
    public void Production_handler_does_not_follow_redirects()
    {
        using var handler = FiscalApiClientFactory.CreateHandler();

        Assert.False(handler.AllowAutoRedirect);
    }

    [Fact]
    public async Task Redirect_from_the_api_ends_as_an_api_exception_without_a_second_request()
    {
        using var handler = FiscalApiClientFactory.CreateHandler();
        var settings = new GlobalSettings(ApiEnvironment.Resolve(_baseAddress, live: false), Json: false, Verbose: false);
        var client = new FiscalApiClientFactory(handler, TextWriter.Null, FiscalApiClientBuilder.NoDelayRetryPolicy)
            .Create(settings, TimeSpan.FromSeconds(10));

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            client.GetAsync("origen", TestJsonContext.Default.ApiResponseProbeData, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Found, error.StatusCode);
        Assert.Equal(["/origen"], _hits);
    }

    public void Dispose() => _server.Close();

    private async Task ServeAsync()
    {
        while (_server.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _server.GetContextAsync();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            _hits.Enqueue(context.Request.Url!.AbsolutePath);
            context.Response.StatusCode = (int)HttpStatusCode.Found;
            context.Response.RedirectLocation = new Uri(_baseAddress, "destino").ToString();
            context.Response.Close();
        }
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
