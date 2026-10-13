using FiscalApi.Cli.Listen;

namespace FiscalApi.Cli.Tests.Listen;

/// <summary>
/// Los reenvíos de <c>listen</c> corren con concurrencia acotada y ordenados por evento: las entregas de eventos distintos
/// van en paralelo, las del mismo evento en el orden en que llegaron, y el long-polling solo espera cuando todo el cupo
/// está ocupado.
/// </summary>
public sealed class DeliveryDispatcherTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Deliveries_of_different_events_run_concurrently()
    {
        await using var dispatcher = new DeliveryDispatcher(maxInFlight: 4, CancellationToken.None);
        var first = new BlockingWork();
        var second = new BlockingWork();

        await dispatcher.EnqueueAsync("evt_1", first.RunAsync, CancellationToken.None);
        await dispatcher.EnqueueAsync("evt_2", second.RunAsync, CancellationToken.None);

        await first.Started.WaitAsync(WaitLimit);
        await second.Started.WaitAsync(WaitLimit);
        first.Release();
        second.Release();
    }

    [Fact]
    public async Task Deliveries_of_the_same_event_run_in_arrival_order()
    {
        await using var dispatcher = new DeliveryDispatcher(maxInFlight: 4, CancellationToken.None);
        var first = new BlockingWork();
        var second = new BlockingWork();

        await dispatcher.EnqueueAsync("evt_1", first.RunAsync, CancellationToken.None);
        await dispatcher.EnqueueAsync("evt_1", second.RunAsync, CancellationToken.None);
        await first.Started.WaitAsync(WaitLimit);
        await Task.Delay(100);

        Assert.False(second.Started.IsCompleted, "La segunda entrega del evento empezó antes de que terminara la primera.");
        first.Release();
        await second.Started.WaitAsync(WaitLimit);
        second.Release();
    }

    [Fact]
    public async Task A_failed_delivery_does_not_block_the_next_one_of_its_event()
    {
        await using var dispatcher = new DeliveryDispatcher(maxInFlight: 4, CancellationToken.None);
        var next = new BlockingWork();

        await dispatcher.EnqueueAsync("evt_1", _ => Task.FromException(new InvalidOperationException("defecto")),
            CancellationToken.None);
        await dispatcher.EnqueueAsync("evt_1", next.RunAsync, CancellationToken.None);

        await next.Started.WaitAsync(WaitLimit);
        next.Release();
    }

    [Fact]
    public async Task Enqueue_waits_only_while_every_slot_is_busy()
    {
        await using var dispatcher = new DeliveryDispatcher(maxInFlight: 2, CancellationToken.None);
        var first = new BlockingWork();
        var second = new BlockingWork();
        var third = new BlockingWork();
        await dispatcher.EnqueueAsync("evt_1", first.RunAsync, CancellationToken.None);
        await dispatcher.EnqueueAsync("evt_2", second.RunAsync, CancellationToken.None);

        var enqueueThird = dispatcher.EnqueueAsync("evt_3", third.RunAsync, CancellationToken.None);
        await Task.Delay(100);

        Assert.False(enqueueThird.IsCompleted, "Se aceptó una entrega con el cupo lleno.");
        first.Release();
        await enqueueThird.WaitAsync(WaitLimit);
        await third.Started.WaitAsync(WaitLimit);
        second.Release();
        third.Release();
    }

    [Fact]
    public async Task Dispose_cancels_the_work_in_flight_and_waits_for_it()
    {
        var dispatcher = new DeliveryDispatcher(maxInFlight: 2, CancellationToken.None);
        var observedCancellation = new TaskCompletionSource();
        await dispatcher.EnqueueAsync("evt_1", async cancellationToken =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            finally
            {
                observedCancellation.SetResult();
            }
        }, CancellationToken.None);

        await dispatcher.DisposeAsync().AsTask().WaitAsync(WaitLimit);

        Assert.True(observedCancellation.Task.IsCompleted, "Dispose terminó con trabajo en curso.");
    }

    [Fact]
    public async Task An_unexpected_failure_surfaces_on_the_next_check()
    {
        await using var dispatcher = new DeliveryDispatcher(maxInFlight: 2, CancellationToken.None);
        var failed = new TaskCompletionSource();
        await dispatcher.EnqueueAsync("evt_1", _ =>
        {
            failed.SetResult();
            throw new InvalidOperationException("defecto del CLI");
        }, CancellationToken.None);
        await failed.Task.WaitAsync(WaitLimit);

        var error = await WaitForFaultAsync(dispatcher);

        Assert.Equal("defecto del CLI", error.Message);
    }

    private static async Task<InvalidOperationException> WaitForFaultAsync(DeliveryDispatcher dispatcher)
    {
        var deadline = DateTime.UtcNow + WaitLimit;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                dispatcher.ThrowIfFaulted();
            }
            catch (InvalidOperationException exception)
            {
                return exception;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("El despachador nunca informó la falla.");
    }

    /// <summary>Trabajo que avisa cuando empieza y no termina hasta que la prueba lo suelta.</summary>
    private sealed class BlockingWork
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public void Release() => _release.TrySetResult();

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
        }
    }
}
