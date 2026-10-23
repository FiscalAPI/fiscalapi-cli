namespace FiscalApi.Cli.Http;

public sealed class RetryPolicy
{
    private readonly ExponentialBackoff _backoff;
    private readonly TimeProvider _timeProvider;

    public RetryPolicy(int maxAttempts, TimeSpan baseDelay, TimeSpan maxDelay, TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        MaxAttempts = maxAttempts;
        _backoff = new ExponentialBackoff(baseDelay, maxDelay);
        _timeProvider = timeProvider;
    }

    public static RetryPolicy Default { get; } =
        new(maxAttempts: 3, baseDelay: TimeSpan.FromMilliseconds(500), maxDelay: TimeSpan.FromSeconds(10), TimeProvider.System);

    public int MaxAttempts { get; }

    public async Task<HttpResponseMessage> SendAsync(
        HttpClient httpClient, Func<HttpRequestMessage> createRequest, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                using var request = createRequest();
                response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException exception) when (attempt < MaxAttempts && IsTransient(exception.HttpRequestError))
            {
                await Task.Delay(_backoff.Delay(attempt), _timeProvider, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (attempt == MaxAttempts || !TransientHttpStatus.Is(response.StatusCode) || DelayBeforeRetry(attempt, response) is not { } delay)
            {
                return response;
            }

            response.Dispose();
            await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsTransient(HttpRequestError error) =>
        error is HttpRequestError.ConnectionError or HttpRequestError.ResponseEnded;

    private TimeSpan? DelayBeforeRetry(int attempt, HttpResponseMessage response)
    {
        var delay = RetryAfter(response) ?? _backoff.Delay(attempt);
        return delay <= _backoff.MaxDelay ? delay : null;
    }

    private TimeSpan? RetryAfter(HttpResponseMessage response) => response.Headers.RetryAfter switch
    {
        { Delta: { } delta } => delta,
        { Date: { } date } => Max(date - _timeProvider.GetUtcNow(), TimeSpan.Zero),
        _ => null
    };

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
