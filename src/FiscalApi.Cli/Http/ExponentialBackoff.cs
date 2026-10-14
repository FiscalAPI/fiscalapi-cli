namespace FiscalApi.Cli.Http;

/// <summary>
/// Espera exponencial con "equal jitter": la mitad fija y la otra mitad aleatoria, para que varios CLI no reintenten al
/// unísono, y nunca más que <c>maxDelay</c>. Inmutable: cada quien lleva su propio número de intento.
/// </summary>
public sealed class ExponentialBackoff
{
    // Con más de 2^30 veces la base cualquier espera ya superó el máximo; el tope evita desbordar el cálculo.
    private const int MaxExponent = 30;

    private readonly TimeSpan _baseDelay;

    public ExponentialBackoff(TimeSpan baseDelay, TimeSpan maxDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(baseDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDelay, baseDelay);
        _baseDelay = baseDelay;
        MaxDelay = maxDelay;
    }

    public TimeSpan MaxDelay { get; }

    /// <param name="attempt">El intento que falló, desde 1: la espera del primero ronda la base.</param>
    public TimeSpan Delay(int attempt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        var exponent = Math.Min(attempt - 1, MaxExponent);
        var exponential = TimeSpan.FromTicks((long)Math.Min(_baseDelay.Ticks * Math.Pow(2, exponent), MaxDelay.Ticks));
        return exponential / 2 + exponential / 2 * Random.Shared.NextDouble();
    }
}
