using System.Net;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Tests.Http;

/// <summary>
/// <c>--verbose</c> muestra el detalle HTTP sin headers de credenciales.
/// </summary>
public sealed class VerboseLoggingHandlerTests
{
    [Fact]
    public async Task Writes_request_and_response_lines_without_credential_headers()
    {
        using var diagnostics = new StringWriter();
        var stub = new StubHttpMessageHandler().RespondJson(HttpStatusCode.OK, """{"succeeded":true}""");
        using var client = new HttpClient(new VerboseLoggingHandler(diagnostics) { InnerHandler = stub })
        {
            BaseAddress = FiscalApiClientBuilder.BaseAddress
        };
        client.DefaultRequestHeaders.Add("X-API-KEY", "sk_test_secreto");
        client.DefaultRequestHeaders.Add("X-TENANT-KEY", "tenant-secreto");
        client.DefaultRequestHeaders.Authorization = new("Bearer", "jwt-secreto");
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        using var _ = await client.GetAsync("api/v4/probe");

        var log = diagnostics.ToString();
        Assert.Contains("GET https://api.example.test/api/v4/probe", log);
        Assert.Contains("Accept: application/json", log);
        Assert.Contains("200", log);
        Assert.DoesNotContain("secreto", log);
        Assert.DoesNotContain("X-API-KEY", log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("X-TENANT-KEY", log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", log, StringComparison.OrdinalIgnoreCase);
    }
}
