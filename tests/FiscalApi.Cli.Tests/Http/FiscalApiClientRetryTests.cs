using System.Net;
using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Tests.Http;

/// <summary>
/// Contrato común del CLI: solo un GET se reintenta y solo ante errores transitorios.
/// </summary>
public sealed class FiscalApiClientRetryTests
{
    private const string Success = """{"succeeded":true,"message":"","details":"","data":{"name":"ok"}}""";
    private const string Unauthorized = """{"succeeded":false,"message":"API key inválida","details":"","traceIdentifier":"0HN-401"}""";

    [Fact]
    public async Task Get_with_503_is_retried_until_it_succeeds()
    {
        var handler = new StubHttpMessageHandler()
            .RespondHtml(HttpStatusCode.ServiceUnavailable, "<html>503 Service Temporarily Unavailable</html>")
            .RespondJson(HttpStatusCode.OK, Success);

        var data = await FiscalApiClientBuilder.Create(handler)
            .GetAsync("api/v4/probe", TestJsonContext.Default.ApiResponseProbeData, CancellationToken.None);

        Assert.Equal(new ProbeData("ok"), data);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Get_with_503_on_every_attempt_fails_after_the_last_attempt()
    {
        var handler = new StubHttpMessageHandler().RespondHtml(
            HttpStatusCode.ServiceUnavailable, "<html>503</html>", times: FiscalApiClientBuilder.NoDelayRetryPolicy.MaxAttempts);

        var error = await Assert.ThrowsAsync<ApiException>(() => FiscalApiClientBuilder.Create(handler)
            .GetAsync("api/v4/probe", TestJsonContext.Default.ApiResponseProbeData, CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Equal(FiscalApiClientBuilder.NoDelayRetryPolicy.MaxAttempts, handler.Requests.Count);
    }

    [Fact]
    public async Task Get_after_a_network_error_is_retried()
    {
        var handler = new StubHttpMessageHandler()
            .FailWithNetworkError()
            .RespondJson(HttpStatusCode.OK, Success);

        var data = await FiscalApiClientBuilder.Create(handler)
            .GetAsync("api/v4/probe", TestJsonContext.Default.ApiResponseProbeData, CancellationToken.None);

        Assert.Equal(new ProbeData("ok"), data);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Post_with_503_is_not_retried()
    {
        var handler = new StubHttpMessageHandler()
            .RespondHtml(HttpStatusCode.ServiceUnavailable, "<html>503</html>")
            .RespondJson(HttpStatusCode.OK, Success);

        var error = await Assert.ThrowsAsync<ApiException>(() => FiscalApiClientBuilder.Create(handler)
            .PostAsync(
                "api/v4/probe",
                new ProbeRequest("x"),
                TestJsonContext.Default.ProbeRequest,
                TestJsonContext.Default.ApiResponseProbeData,
                CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Post_after_a_network_error_is_not_retried()
    {
        var handler = new StubHttpMessageHandler()
            .FailWithNetworkError()
            .RespondJson(HttpStatusCode.OK, Success);

        await Assert.ThrowsAsync<NetworkException>(() => FiscalApiClientBuilder.Create(handler)
            .PostAsync(
                "api/v4/probe",
                new ProbeRequest("x"),
                TestJsonContext.Default.ProbeRequest,
                TestJsonContext.Default.ApiResponseProbeData,
                CancellationToken.None));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Get_with_401_is_never_retried()
    {
        var handler = new StubHttpMessageHandler()
            .RespondJson(HttpStatusCode.Unauthorized, Unauthorized)
            .RespondJson(HttpStatusCode.OK, Success);

        var error = await Assert.ThrowsAsync<ApiException>(() => FiscalApiClientBuilder.Create(handler)
            .GetAsync("api/v4/probe", TestJsonContext.Default.ApiResponseProbeData, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Get_with_429_is_retried_after_the_retry_after_delay()
    {
        var handler = new StubHttpMessageHandler()
            .RespondTooManyRequests(retryAfter: TimeSpan.Zero)
            .RespondJson(HttpStatusCode.OK, Success);

        var data = await FiscalApiClientBuilder.Create(handler)
            .GetAsync("api/v4/probe", TestJsonContext.Default.ApiResponseProbeData, CancellationToken.None);

        Assert.Equal(new ProbeData("ok"), data);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Get_with_429_and_a_retry_after_longer_than_the_maximum_delay_is_not_retried()
    {
        var handler = new StubHttpMessageHandler()
            .RespondTooManyRequests(retryAfter: TimeSpan.FromMinutes(5))
            .RespondJson(HttpStatusCode.OK, Success);

        var error = await Assert.ThrowsAsync<ApiException>(() => FiscalApiClientBuilder.Create(handler)
            .GetAsync("api/v4/probe", TestJsonContext.Default.ApiResponseProbeData, CancellationToken.None));

        Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
        Assert.Single(handler.Requests);
    }
}
