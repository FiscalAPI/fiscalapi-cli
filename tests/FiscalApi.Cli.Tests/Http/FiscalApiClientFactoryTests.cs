using System.Net;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Tests.Http;

public sealed class FiscalApiClientFactoryTests
{
    private const string Success = """{"succeeded":true,"data":{"name":"ok"}}""";

    [Fact]
    public async Task Client_targets_the_resolved_environment_with_the_cli_user_agent()
    {
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.OK, Success);
        var factory = new FiscalApiClientFactory(handler, TextWriter.Null, FiscalApiClientBuilder.NoDelayRetryPolicy);

        await factory.Create(Settings(live: true), FiscalApiClientFactory.DefaultTimeout)
            .GetAsync("api/v4/probe", TestJsonContext.Default.ApiResponseProbeData, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("https://live.fiscalapi.com/api/v4/probe"), request.RequestUri);
        Assert.Equal($"fiscalapi-cli/{CliVersion.Current}", request.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task Clients_share_the_handler_without_disposing_it()
    {
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.OK, Success, times: 2);
        var factory = new FiscalApiClientFactory(handler, TextWriter.Null, FiscalApiClientBuilder.NoDelayRetryPolicy);

        await factory.Create(Settings(live: false), FiscalApiClientFactory.DefaultTimeout)
            .GetAsync("api/v4/probe", TestJsonContext.Default.ApiResponseProbeData, CancellationToken.None);
        await factory.Create(Settings(live: true), FiscalApiClientFactory.DefaultTimeout)
            .GetAsync("api/v4/probe", TestJsonContext.Default.ApiResponseProbeData, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Verbose_wraps_the_shared_handler_with_the_http_log()
    {
        using var diagnostics = new StringWriter();
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.OK, Success);
        var factory = new FiscalApiClientFactory(handler, diagnostics, FiscalApiClientBuilder.NoDelayRetryPolicy);

        await factory.Create(Settings(live: false, verbose: true), FiscalApiClientFactory.DefaultTimeout)
            .GetAsync("api/v4/probe", TestJsonContext.Default.ApiResponseProbeData, CancellationToken.None);

        Assert.Contains("> GET https://test.fiscalapi.com/api/v4/probe", diagnostics.ToString());
    }

    private static GlobalSettings Settings(bool live, bool verbose = false) =>
        new(ApiEnvironment.Resolve(apiBase: null, live), Json: false, verbose);
}
