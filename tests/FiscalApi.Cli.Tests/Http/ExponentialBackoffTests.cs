using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Tests.Http;

public sealed class ExponentialBackoffTests
{
    private static readonly ExponentialBackoff Backoff = new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(5, 16)]
    public void Each_attempt_doubles_the_wait_with_jitter_in_its_upper_half(int attempt, int exponentialSeconds)
    {
        var exponential = TimeSpan.FromSeconds(exponentialSeconds);

        for (var sample = 0; sample < 200; sample++)
        {
            var delay = Backoff.Delay(attempt);

            Assert.InRange(delay, exponential / 2, exponential);
        }
    }

    [Theory]
    [InlineData(6)]
    [InlineData(31)]
    [InlineData(int.MaxValue)]
    public void The_wait_never_exceeds_the_cap(int attempt)
    {
        for (var sample = 0; sample < 200; sample++)
        {
            Assert.InRange(Backoff.Delay(attempt), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30));
        }
    }

    [Fact]
    public void Jitter_spreads_the_waits_of_the_same_attempt()
    {
        var delays = Enumerable.Range(0, 50).Select(_ => Backoff.Delay(4)).Distinct().Count();

        Assert.True(delays > 1, "Todas las esperas salieron iguales: sin jitter, varios CLI reintentarían al unísono.");
    }

    [Fact]
    public void Rejects_a_cap_below_the_base_or_an_attempt_below_one()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialBackoff(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Backoff.Delay(0));
    }
}
