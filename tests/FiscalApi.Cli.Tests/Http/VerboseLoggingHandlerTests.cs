using System.Net;
using System.Text;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Tests.Http;

/// <summary>
/// <c>--verbose</c> muestra el detalle HTTP sin headers de credenciales ni cuerpos.
/// </summary>
public sealed class VerboseLoggingHandlerTests
{
    private readonly StringWriter _diagnostics = new();

    [Fact]
    public async Task Writes_request_and_response_lines_without_credential_headers()
    {
        var stub = new StubHttpMessageHandler().Respond(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"succeeded":true,"data":"cuerpo-secreto"}""", Encoding.UTF8, "application/json")
            };
            response.Headers.Add("X-Request-ID", "req-123");
            response.Headers.Add("Set-Cookie", "session=cookie-secreta");
            return response;
        });
        using var client = CreateClient(stub);
        client.DefaultRequestHeaders.Add("X-API-KEY", "sk_test_secreto");
        client.DefaultRequestHeaders.Add("X-TENANT-KEY", "tenant-secreto");
        client.DefaultRequestHeaders.Authorization = new("Bearer", "jwt-secreto");
        client.DefaultRequestHeaders.ProxyAuthorization = new("Basic", "proxy-secreto");
        client.DefaultRequestHeaders.Add("Cookie", "session=cookie-secreta");
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        using var content = new StringContent("""{"name":"cuerpo-secreto"}""", Encoding.UTF8, "application/json");
        using var _ = await client.PostAsync("api/v4/probe", content);

        var log = _diagnostics.ToString();
        Assert.Contains("> POST https://api.example.test/api/v4/probe", log);
        Assert.Contains("> Accept: application/json", log);
        Assert.Contains("> Content-Type: application/json; charset=utf-8", log);
        Assert.Contains("< 200 OK", log);
        Assert.Contains("< X-Request-ID: req-123", log);
        Assert.Contains("< Content-Type: application/json; charset=utf-8", log);
        Assert.DoesNotContain("secret", log);
        foreach (var header in new[] { "X-API-KEY", "X-TENANT-KEY", "Authorization", "Proxy-Authorization", "Cookie", "Set-Cookie" })
        {
            Assert.DoesNotContain($" {header}:", log, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Writes_a_line_when_the_request_fails_on_the_network()
    {
        var stub = new StubHttpMessageHandler().FailWithNetworkError(HttpRequestError.ConnectionError);
        using var client = CreateClient(stub);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("api/v4/probe"));

        Assert.Contains("! GET https://api.example.test/api/v4/probe falló (ConnectionError)", _diagnostics.ToString());
    }

    [Fact]
    public async Task Writes_a_line_when_the_request_gets_no_response()
    {
        var stub = new StubHttpMessageHandler().NeverRespond();
        using var client = CreateClient(stub);
        client.Timeout = TimeSpan.FromMilliseconds(50);

        await Assert.ThrowsAsync<TaskCanceledException>(() => client.GetAsync("api/v4/probe"));

        Assert.Contains("! GET https://api.example.test/api/v4/probe sin respuesta", _diagnostics.ToString());
    }

    private HttpClient CreateClient(StubHttpMessageHandler stub) =>
        new(new VerboseLoggingHandler(_diagnostics) { InnerHandler = stub })
        {
            BaseAddress = FiscalApiClientBuilder.BaseAddress
        };
}
