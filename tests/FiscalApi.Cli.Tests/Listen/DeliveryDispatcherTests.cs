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

        dispatcher.Enqueue("evt_1", first.RunAsync);
        dispatcher.Enqueue("evt_2", second.RunAsync);

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

        dispatcher.Enqueue("evt_1", first.RunAsync);
        dispatcher.Enqueue("evt_1", second.RunAsync);
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

        dispatcher.Enqueue("evt_1", _ => Task.FromException(new InvalidOperationException("defecto")));
        dispatcher.Enqueue("evt_1", next.RunAsync);

        await next.Started.WaitAsync(WaitLimit);
        next.Release();
    }

    [Fact]
    public async Task At_most_max_in_flight_deliveries_run_and_enqueue_never_waits()
    {
        await using var dispatcher = new DeliveryDispatcher(maxInFlight: 2, CancellationToken.None);
        BlockingWork[] works = [new(), new(), new()];

        for (var i = 0; i < works.Length; i++)
        {
            dispatcher.Enqueue($"evt_{i}", works[i].RunAsync);
        }

        // Cuáles dos toman el cupo primero no está definido; lo que importa es que sean dos.
        await FakeListenerApi.WaitUntilAsync(() => works.Count(work => work.Started.IsCompleted) == 2, "No corrieron dos.");
        await Task.Delay(100);
        Assert.Equal(2, works.Count(work => work.Started.IsCompleted));
        var waiting = works.Single(work => !work.Started.IsCompleted);
        works.First(work => work.Started.IsCompleted).Release();
        await waiting.Started.WaitAsync(WaitLimit);
        foreach (var work in works)
        {
            work.Release();
        }
    }

    [Fact]
    public async Task A_delivery_waiting_for_its_event_does_not_take_a_slot()
    {
        await using var dispatcher = new DeliveryDispatcher(maxInFlight: 2, CancellationToken.None);
        var running = new BlockingWork();
        var waitingTurn = new BlockingWork();
        var otherEvent = new BlockingWork();

        dispatcher.Enqueue("evt_1", running.RunAsync);
        dispatcher.Enqueue("evt_1", waitingTurn.RunAsync);
        dispatcher.Enqueue("evt_2", otherEvent.RunAsync);

        await otherEvent.Started.WaitAsync(WaitLimit);
        Assert.False(waitingTurn.Started.IsCompleted, "La segunda entrega del evento empezó antes de su turno.");
        running.Release();
        await waitingTurn.Started.WaitAsync(WaitLimit);
        waitingTurn.Release();
        otherEvent.Release();
    }

    [Fact]
    public async Task Dispose_skips_the_deliveries_still_waiting_for_their_turn()
    {
        var dispatcher = new DeliveryDispatcher(maxInFlight: 2, CancellationToken.None);
        var running = new BlockingWork();
        var waitingTurn = new BlockingWork();
        dispatcher.Enqueue("evt_1", running.RunAsync);
        dispatcher.Enqueue("evt_1", waitingTurn.RunAsync);
        await running.Started.WaitAsync(WaitLimit);

        await dispatcher.DisposeAsync().AsTask().WaitAsync(WaitLimit);

        Assert.False(waitingTurn.Started.IsCompleted, "Una entrega en espera de turno corrió después de detener la escucha.");
    }

    [Fact]
    public async Task Dispose_cancels_the_work_in_flight_and_waits_for_it()
    {
        var dispatcher = new DeliveryDispatcher(maxInFlight: 2, CancellationToken.None);
        var started = new TaskCompletionSource();
        var observedCancellation = new TaskCompletionSource();
        dispatcher.Enqueue("evt_1", async cancellationToken =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            finally
            {
                observedCancellation.SetResult();
            }
        });
        await started.Task.WaitAsync(WaitLimit);

        await dispatcher.DisposeAsync().AsTask().WaitAsync(WaitLimit);

        Assert.True(observedCancellation.Task.IsCompleted, "Dispose terminó con trabajo en curso.");
    }

    [Fact]
    public async Task An_unexpected_failure_surfaces_on_the_next_check()
    {
        await using var dispatcher = new DeliveryDispatcher(maxInFlight: 2, CancellationToken.None);
        var failed = new TaskCompletionSource();
        dispatcher.Enqueue("evt_1", _ =>
        {
            failed.SetResult();
            throw new InvalidOperationException("defecto del CLI");
        });
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
