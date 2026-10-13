using Microsoft.Extensions.Time.Testing;

namespace FiscalApi.Cli.Tests.Http;

/// <summary>
/// <see cref="FakeTimeProvider"/> que avisa cuando alguien crea un timer (cada <c>Task.Delay</c> crea uno).
/// Así la prueba avanza el reloj solo cuando la espera ya existe: sin carreras con el hilo que hace el request.
/// </summary>
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

    /// <summary>
    /// Avanza sin contexto de sincronización: así lo que libera el timer (el reintento) corre en línea dentro
    /// de esta llamada y la siguiente aserción ve su efecto. Con el contexto de xUnit iría a otro hilo, tarde.
    /// </summary>
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

    /// <summary>Espera (en tiempo real, con límite) a que la operación empiece a esperar en este reloj.</summary>
    public async Task WaitForDelayAsync()
    {
        Assert.True(await _timersCreated.WaitAsync(WaitLimit), "La operación nunca empezó a esperar.");
    }
}
