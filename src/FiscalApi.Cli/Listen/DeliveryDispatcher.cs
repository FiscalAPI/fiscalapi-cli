using System.Runtime.ExceptionServices;

namespace FiscalApi.Cli.Listen;

/// <summary>
/// Corre el trabajo de cada entrega de <c>listen</c> fuera del long-polling, con dos reglas:
/// <list type="bullet">
/// <item><b>Concurrencia acotada:</b> como mucho <c>maxInFlight</c> entregas a la vez (en espera de su turno o corriendo).
/// Con el cupo lleno, <see cref="EnqueueAsync"/> espera a que se libere uno: el long-polling deja de reclamar entregas
/// que no podría reenviar antes de que venza su lease, en lugar de acumularlas en memoria.</item>
/// <item><b>Orden por evento:</b> las entregas del mismo evento (un reenvío con <c>events resend</c>) corren una tras otra,
/// en el orden en que llegaron; las de eventos distintos, en paralelo. Una entrega lenta no frena a las demás.</item>
/// </list>
/// El trabajo no debe lanzar: una excepción es un defecto del CLI, se guarda y sale en <see cref="ThrowIfFaulted"/>, que
/// el long-polling revisa en cada vuelta para terminar por el único punto que atrapa errores.
/// </summary>
public sealed class DeliveryDispatcher : IAsyncDisposable
{
    private readonly int _maxInFlight;
    private readonly SemaphoreSlim _slots;
    private readonly CancellationTokenSource _stopping;
    private readonly Lock _gate = new();

    // La última entrega aceptada de cada evento con trabajo pendiente; la siguiente del mismo evento la espera. La entrada
    // se borra cuando termina la última, así el diccionario no crece durante una escucha larga.
    private readonly Dictionary<string, Task> _lastByEvent = new(StringComparer.Ordinal);

    private ExceptionDispatchInfo? _fault;

    /// <param name="maxInFlight">Entregas que pueden estar a la vez en el despachador.</param>
    /// <param name="cancellationToken">Ctrl+C: cancela el trabajo en curso.</param>
    public DeliveryDispatcher(int maxInFlight, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxInFlight, 1);
        _maxInFlight = maxInFlight;
        _slots = new SemaphoreSlim(maxInFlight, maxInFlight);
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    /// <summary>Acepta la entrega en cuanto hay cupo y vuelve sin esperar a que su trabajo termine.</summary>
    /// <param name="eventId">El evento de la entrega: ordena las del mismo evento.</param>
    /// <param name="work">El trabajo de la entrega; recibe el token que cancela <see cref="DisposeAsync"/>.</param>
    /// <param name="cancellationToken">Cancela la espera de cupo.</param>
    public async Task EnqueueAsync(string eventId, Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task previous;
        lock (_gate)
        {
            previous = _lastByEvent.GetValueOrDefault(eventId) ?? Task.CompletedTask;
            _lastByEvent[eventId] = done.Task;
        }

        _ = RunAsync(eventId, previous, work, done);
    }

    /// <summary>Relanza, una vez, la primera excepción que lanzó el trabajo de una entrega.</summary>
    public void ThrowIfFaulted() => Interlocked.Exchange(ref _fault, null)?.Throw();

    /// <summary>Cancela el trabajo en curso y espera a que termine: después no queda nada escribiendo en la consola.</summary>
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        // Con todo el cupo de vuelta no queda ninguna entrega en curso: cada una devuelve el suyo al final.
        for (var slot = 0; slot < _maxInFlight; slot++)
        {
            await _slots.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        _stopping.Dispose();
        _slots.Dispose();
    }

    private async Task RunAsync(string eventId, Task previous, Func<CancellationToken, Task> work, TaskCompletionSource done)
    {
        try
        {
            // ForceYielding: el trabajo nunca corre dentro de EnqueueAsync, que así vuelve de inmediato al long-polling.
            await previous.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
            await work(_stopping.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            // La escucha terminó: la entrega se queda sin acuse y la API la cierra al vencer su lease.
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
            _slots.Release();
        }
    }
}
