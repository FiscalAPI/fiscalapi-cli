namespace FiscalApi.Cli.Http;

/// <summary>
/// Reintentos con backoff exponencial y jitter, solo ante errores transitorios: conexión fallida o cortada,
/// 408, 429 y 5xx. Un 4xx nunca se reintenta, y un timeout tampoco: el plazo que ve la persona es el del
/// mensaje. Quien la usa decide qué requests son seguros de repetir (solo GET).
/// </summary>
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

    // Solo fallas que otro intento puede resolver. Un error de TLS o de DNS (casi siempre un --api-base mal
    // escrito) se repetiría igual, y solo retrasaría el mensaje.
    private static bool IsTransient(HttpRequestError error) =>
        error is HttpRequestError.ConnectionError or HttpRequestError.ResponseEnded;

    /// <summary>
    /// Respeta <c>Retry-After</c> cuando viene. Si pide esperar más que el máximo, no se reintenta y el error
    /// llega a la persona en lugar de dejar la terminal colgada.
    /// </summary>
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
