using System.Net;
using System.Net.Http.Headers;
using FiscalApi.Cli.Errors;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Tests.Http;

public sealed class FiscalApiClientRetryTests
{
    private const string Success = """{"succeeded":true,"message":"","details":"","data":{"name":"ok"}}""";
    private const string Unauthorized = """{"succeeded":false,"message":"API key inválida","details":"","traceIdentifier":"0HN-401"}""";
    private static readonly int AllAttempts = FiscalApiClientBuilder.NoDelayRetryPolicy.MaxAttempts;

    private readonly SteppedTimeProvider _time = new();

    [Fact]
    public async Task Get_with_503_is_retried_until_it_succeeds()
    {
        var handler = new StubHttpMessageHandler()
            .RespondHtml(HttpStatusCode.ServiceUnavailable, "<html>503 Service Temporarily Unavailable</html>")
            .RespondJson(HttpStatusCode.OK, Success);

        var data = await GetAsync(FiscalApiClientBuilder.Create(handler));

        Assert.Equal(new ProbeData("ok"), data);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Get_with_503_on_every_attempt_fails_after_the_last_attempt()
    {
        var handler = new StubHttpMessageHandler().RespondHtml(HttpStatusCode.ServiceUnavailable, "<html>503</html>", AllAttempts);

        var error = await Assert.ThrowsAsync<ApiException>(() => GetAsync(FiscalApiClientBuilder.Create(handler)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Equal(AllAttempts, handler.Requests.Count);
    }

    [Fact]
    public async Task Get_with_408_is_retried()
    {
        var handler = new StubHttpMessageHandler()
            .RespondHtml(HttpStatusCode.RequestTimeout, "<html>408</html>")
            .RespondJson(HttpStatusCode.OK, Success);

        var data = await GetAsync(FiscalApiClientBuilder.Create(handler));

        Assert.Equal(new ProbeData("ok"), data);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData(HttpRequestError.ConnectionError)]
    [InlineData(HttpRequestError.ResponseEnded)]
    public async Task Get_after_a_transient_network_error_is_retried(HttpRequestError error)
    {
        var handler = new StubHttpMessageHandler()
            .FailWithNetworkError(error)
            .RespondJson(HttpStatusCode.OK, Success);

        var data = await GetAsync(FiscalApiClientBuilder.Create(handler));

        Assert.Equal(new ProbeData("ok"), data);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData(HttpRequestError.SecureConnectionError)]
    [InlineData(HttpRequestError.NameResolutionError)]
    public async Task Get_after_a_permanent_network_error_is_not_retried(HttpRequestError error)
    {
        var handler = new StubHttpMessageHandler()
            .FailWithNetworkError(error)
            .RespondJson(HttpStatusCode.OK, Success);

        await Assert.ThrowsAsync<NetworkException>(() => GetAsync(FiscalApiClientBuilder.Create(handler)));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Get_that_times_out_is_not_retried()
    {
        var handler = new StubHttpMessageHandler().NeverRespond();
        var client = FiscalApiClientBuilder.Create(handler, timeout: TimeSpan.FromMilliseconds(50));

        var error = await Assert.ThrowsAsync<NetworkException>(() => GetAsync(client));

        Assert.Contains("did not respond", error.Message);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Post_with_503_is_not_retried()
    {
        var handler = new StubHttpMessageHandler()
            .RespondHtml(HttpStatusCode.ServiceUnavailable, "<html>503</html>")
            .RespondJson(HttpStatusCode.OK, Success);

        var error = await Assert.ThrowsAsync<ApiException>(() => PostAsync(FiscalApiClientBuilder.Create(handler)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Post_after_a_network_error_is_not_retried()
    {
        var handler = new StubHttpMessageHandler()
            .FailWithNetworkError()
            .RespondJson(HttpStatusCode.OK, Success);

        await Assert.ThrowsAsync<NetworkException>(() => PostAsync(FiscalApiClientBuilder.Create(handler)));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Get_with_401_is_never_retried()
    {
        var handler = new StubHttpMessageHandler()
            .RespondJson(HttpStatusCode.Unauthorized, Unauthorized)
            .RespondJson(HttpStatusCode.OK, Success);

        var error = await Assert.ThrowsAsync<ApiException>(() => GetAsync(FiscalApiClientBuilder.Create(handler)));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Get_with_429_waits_the_retry_after_seconds_before_retrying()
    {
        var handler = new StubHttpMessageHandler()
            .RespondWithRetryAfter(HttpStatusCode.TooManyRequests, new RetryConditionHeaderValue(TimeSpan.FromSeconds(7)))
            .RespondJson(HttpStatusCode.OK, Success);

        var pending = GetAsync(TimedClient(handler));
        await _time.WaitForDelayAsync();

        _time.Advance(TimeSpan.FromSeconds(7) - TimeSpan.FromMilliseconds(1));
        Assert.Single(handler.Requests);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(new ProbeData("ok"), await Finished(pending));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Get_with_503_waits_until_the_retry_after_http_date()
    {
        var handler = new StubHttpMessageHandler()
            .RespondWithRetryAfter(HttpStatusCode.ServiceUnavailable, new RetryConditionHeaderValue(_time.GetUtcNow().AddSeconds(5)))
            .RespondJson(HttpStatusCode.OK, Success);

        var pending = GetAsync(TimedClient(handler));
        await _time.WaitForDelayAsync();

        _time.Advance(TimeSpan.FromSeconds(5) - TimeSpan.FromMilliseconds(1));
        Assert.Single(handler.Requests);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(new ProbeData("ok"), await Finished(pending));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Get_with_a_retry_after_longer_than_the_maximum_delay_is_not_retried()
    {
        var handler = new StubHttpMessageHandler()
            .RespondWithRetryAfter(HttpStatusCode.TooManyRequests, new RetryConditionHeaderValue(TimeSpan.FromMinutes(5)))
            .RespondJson(HttpStatusCode.OK, Success);

        var error = await Assert.ThrowsAsync<ApiException>(() => Finished(GetAsync(TimedClient(handler))));

        Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Backoff_doubles_the_wait_with_jitter_between_half_and_all_of_it()
    {
        var handler = new StubHttpMessageHandler()
            .RespondHtml(HttpStatusCode.ServiceUnavailable, "<html>503</html>", times: 2)
            .RespondJson(HttpStatusCode.OK, Success);

        var pending = GetAsync(TimedClient(handler));

        await _time.WaitForDelayAsync();
        _time.Advance(TimeSpan.FromMilliseconds(499));
        Assert.Single(handler.Requests);
        _time.Advance(TimeSpan.FromMilliseconds(501));

        await _time.WaitForDelayAsync();
        Assert.Equal(2, handler.Requests.Count);
        _time.Advance(TimeSpan.FromMilliseconds(999));
        Assert.Equal(2, handler.Requests.Count);
        _time.Advance(TimeSpan.FromMilliseconds(1001));
        Assert.Equal(new ProbeData("ok"), await Finished(pending));
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task User_cancellation_during_the_request_propagates_and_is_not_a_network_error()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHttpMessageHandler().NeverRespond();

        var pending = GetAsync(FiscalApiClientBuilder.Create(handler), cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Finished(pending));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task User_cancellation_during_the_backoff_propagates_and_is_not_a_network_error()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHttpMessageHandler()
            .RespondHtml(HttpStatusCode.ServiceUnavailable, "<html>503</html>")
            .RespondJson(HttpStatusCode.OK, Success);

        var pending = GetAsync(TimedClient(handler), cancellation.Token);
        await _time.WaitForDelayAsync();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Finished(pending));
        Assert.Single(handler.Requests);
    }

    private static Task<T> Finished<T>(Task<T> task) => task.WaitAsync(TimeSpan.FromSeconds(5));

    private FiscalApiClient TimedClient(StubHttpMessageHandler handler) => FiscalApiClientBuilder.Create(
        handler, new RetryPolicy(maxAttempts: 3, baseDelay: TimeSpan.FromSeconds(1), maxDelay: TimeSpan.FromSeconds(10), _time));

    private static Task<ProbeData?> GetAsync(FiscalApiClient client, CancellationToken cancellationToken = default) =>
        client.GetAsync("api/v4/probe", TestJsonContext.Default.ApiResponseProbeData, cancellationToken);

    private static Task<ProbeData?> PostAsync(FiscalApiClient client) =>
        client.PostAsync(
            "api/v4/probe",
            new ProbeRequest("x"),
            TestJsonContext.Default.ProbeRequest,
            TestJsonContext.Default.ApiResponseProbeData,
            CancellationToken.None);
}
