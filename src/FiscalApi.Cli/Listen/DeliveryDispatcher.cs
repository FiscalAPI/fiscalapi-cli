using System.Runtime.ExceptionServices;

namespace FiscalApi.Cli.Listen;

public sealed class DeliveryDispatcher : IAsyncDisposable
{
    private readonly SemaphoreSlim _slots;
    private readonly CancellationTokenSource _stopping;
    private readonly Lock _gate = new();

    private readonly Dictionary<string, Task> _lastByEvent = new(StringComparer.Ordinal);

    private ExceptionDispatchInfo? _fault;

    public DeliveryDispatcher(int maxInFlight, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxInFlight, 1);
        _slots = new SemaphoreSlim(maxInFlight, maxInFlight);
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    public void Enqueue(string eventId, Func<CancellationToken, Task> work)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task previous;
        lock (_gate)
        {
            previous = _lastByEvent.GetValueOrDefault(eventId) ?? Task.CompletedTask;
            _lastByEvent[eventId] = done.Task;
        }

        _ = RunAsync(eventId, previous, work, done);
    }

    public void ThrowIfFaulted() => Interlocked.Exchange(ref _fault, null)?.Throw();

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        Task[] pending;
        lock (_gate)
        {
            pending = [.. _lastByEvent.Values];
        }

        await Task.WhenAll(pending).ConfigureAwait(false);
        _stopping.Dispose();
        _slots.Dispose();
    }

    private async Task RunAsync(string eventId, Task previous, Func<CancellationToken, Task> work, TaskCompletionSource done)
    {
        try
        {
            await previous.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
            await _slots.WaitAsync(_stopping.Token).ConfigureAwait(false);
            try
            {
                await work(_stopping.Token).ConfigureAwait(false);
            }
            finally
            {
                _slots.Release();
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Interlocked.CompareExchange(ref _fault, ExceptionDispatchInfo.Capture(exception), null);
        }
        finally
        {
            lock (_gate)
            {
                if (_lastByEvent.TryGetValue(eventId, out var last) && last == done.Task)
                {
                    _lastByEvent.Remove(eventId);
                }
            }

            done.SetResult();
        }
    }
}
