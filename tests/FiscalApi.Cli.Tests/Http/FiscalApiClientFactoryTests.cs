using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Tests.Http;

public sealed class FiscalApiClientFactoryTests
{
    [Fact]
    public void Http_client_targets_the_resolved_environment_with_the_cli_user_agent_and_timeout()
    {
        var settings = new GlobalSettings(ApiEnvironment.Resolve(apiBase: null, live: true), Json: false, Verbose: false);

        using var httpClient = FiscalApiClientFactory.CreateHttpClient(settings, TextWriter.Null);

        Assert.Equal(ApiEnvironment.LiveBaseAddress, httpClient.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(30), httpClient.Timeout);
        Assert.Equal($"fiscalapi-cli/{CliVersion.Current}", httpClient.DefaultRequestHeaders.UserAgent.ToString());
    }
}
