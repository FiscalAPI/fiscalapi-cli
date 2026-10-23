namespace FiscalApi.Cli.Http;

public sealed class ExponentialBackoff
{
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

    public TimeSpan Delay(int attempt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        var exponent = Math.Min(attempt - 1, MaxExponent);
        var exponential = TimeSpan.FromTicks((long)Math.Min(_baseDelay.Ticks * Math.Pow(2, exponent), MaxDelay.Ticks));
        return exponential / 2 + exponential / 2 * Random.Shared.NextDouble();
    }
}
