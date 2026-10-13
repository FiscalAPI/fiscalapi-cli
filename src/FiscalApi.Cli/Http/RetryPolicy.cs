using System.Net;

namespace FiscalApi.Cli.Http;

/// <summary>
/// Reintentos con backoff exponencial y jitter, solo ante errores transitorios: red, 408, 429 y 5xx.
/// Un 4xx nunca se reintenta. Quien la usa decide qué requests son seguros de repetir (solo GET).
/// </summary>
public sealed class RetryPolicy
{
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private readonly TimeProvider _timeProvider;

    public RetryPolicy(int maxAttempts, TimeSpan baseDelay, TimeSpan maxDelay, TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(baseDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDelay, baseDelay);
        MaxAttempts = maxAttempts;
        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
        _timeProvider = timeProvider;
    }

    /// <summary>3 intentos, a partir de 0.5 s, sin esperar más de 10 s entre intentos: es una herramienta interactiva.</summary>
    public static RetryPolicy Default { get; } =
        new(maxAttempts: 3, baseDelay: TimeSpan.FromMilliseconds(500), maxDelay: TimeSpan.FromSeconds(10), TimeProvider.System);

    public int MaxAttempts { get; }

    /// <param name="createRequest">Fábrica: un <see cref="HttpRequestMessage"/> no puede enviarse dos veces.</param>
    public async Task<HttpResponseMessage> SendAsync(
        HttpClient httpClient, Func<HttpRequestMessage> createRequest, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                using var request = createRequest();
                response = await httpClient.SendAsync(request, cancellationToken);
            }
            catch (Exception exception) when (attempt < MaxAttempts && IsTransientFailure(exception, cancellationToken))
            {
                await Task.Delay(Backoff(attempt), _timeProvider, cancellationToken);
                continue;
            }

            if (attempt == MaxAttempts || !IsTransient(response.StatusCode) || DelayBeforeRetry(attempt, response) is not { } delay)
            {
                return response;
            }

            response.Dispose();
            await Task.Delay(delay, _timeProvider, cancellationToken);
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    // HttpClient reporta su timeout como TaskCanceledException sin que el token de quien llama se haya cancelado.
    private static bool IsTransientFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException
        || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    /// <summary>
    /// Respeta <c>Retry-After</c> cuando viene. Si pide esperar más que el máximo, no se reintenta y el error
    /// llega a la persona en lugar de dejar la terminal colgada.
    /// </summary>
    private TimeSpan? DelayBeforeRetry(int attempt, HttpResponseMessage response)
    {
        var delay = RetryAfter(response) ?? Backoff(attempt);
        return delay <= _maxDelay ? delay : null;
    }

    private TimeSpan? RetryAfter(HttpResponseMessage response) => response.Headers.RetryAfter switch
    {
        { Delta: { } delta } => delta,
        { Date: { } date } => Max(date - _timeProvider.GetUtcNow(), TimeSpan.Zero),
        _ => null
    };

    // "Equal jitter": la mitad fija y la otra mitad aleatoria, para que varios CLI no reintenten al unísono.
    private TimeSpan Backoff(int attempt)
    {
        var exponential = Min(_baseDelay * Math.Pow(2, attempt - 1), _maxDelay);
        return exponential / 2 + exponential / 2 * Random.Shared.NextDouble();
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
