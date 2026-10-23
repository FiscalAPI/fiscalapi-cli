using Microsoft.Extensions.Time.Testing;

namespace FiscalApi.Cli.Tests.Http;

internal sealed class SteppedTimeProvider : TimeProvider
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
    private readonly SemaphoreSlim _timersCreated = new(0);

    public override DateTimeOffset GetUtcNow() => _clock.GetUtcNow();

    public override long GetTimestamp() => _clock.GetTimestamp();

    public override long TimestampFrequency => _clock.TimestampFrequency;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = _clock.CreateTimer(callback, state, dueTime, period);
        _timersCreated.Release();
        return timer;
    }

    public void Advance(TimeSpan delta)
    {
        var context = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            _clock.Advance(delta);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(context);
        }
    }

    public async Task WaitForDelayAsync()
    {
        Assert.True(await _timersCreated.WaitAsync(WaitLimit), "La operación nunca empezó a esperar.");
    }
}
