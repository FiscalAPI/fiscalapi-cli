using System.Runtime.ExceptionServices;

namespace FiscalApi.Cli.Listen;

/// <summary>
/// Corre el trabajo de cada entrega de <c>listen</c> fuera del long-polling, con dos reglas:
/// <list type="bullet">
/// <item><b>Orden por evento:</b> las entregas del mismo evento (un reenvío con <c>events resend</c>) corren una tras otra,
/// en el orden en que llegaron; las de eventos distintos, en paralelo.</item>
/// <item><b>Concurrencia acotada:</b> como mucho <c>maxInFlight</c> entregas corren a la vez. El cupo se toma cuando le
/// toca su turno a la entrega, así una cadena larga del mismo evento no ocupa cupo mientras espera.</item>
/// </list>
/// <see cref="Enqueue"/> nunca espera: el long-polling sigue a tiempo y la sesión no caduca aunque el receptor sea lento.
/// Lo que espera su turno ocupa solo memoria (cada lote trae como mucho 25 entregas), y la que llega a su turno con el lease
/// vencido se descarta sin reenviarse (<see cref="DeliveryProcessor"/>).
/// El trabajo no debe lanzar: una excepción es un defecto del CLI, se guarda y sale en <see cref="ThrowIfFaulted"/>, que
/// el long-polling revisa en cada vuelta para terminar por el único punto que atrapa errores.
/// </summary>
public sealed class DeliveryDispatcher : IAsyncDisposable
{
    private readonly SemaphoreSlim _slots;
    private readonly CancellationTokenSource _stopping;
    private readonly Lock _gate = new();

    // La última entrega aceptada de cada evento con trabajo pendiente; la siguiente del mismo evento la espera. La entrada
    // se borra cuando termina la última, así el diccionario no crece durante una escucha larga. Como cada entrega espera a
    // la anterior de su evento, que terminen las últimas significa que terminó todo.
    private readonly Dictionary<string, Task> _lastByEvent = new(StringComparer.Ordinal);

    private ExceptionDispatchInfo? _fault;

    /// <param name="maxInFlight">Entregas que pueden correr a la vez.</param>
    /// <param name="cancellationToken">Ctrl+C: cancela el trabajo en curso.</param>
    public DeliveryDispatcher(int maxInFlight, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxInFlight, 1);
        _slots = new SemaphoreSlim(maxInFlight, maxInFlight);
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    /// <summary>Acepta la entrega y vuelve de inmediato; su trabajo corre cuando le toca su turno y hay cupo.</summary>
    /// <param name="eventId">El evento de la entrega: ordena las del mismo evento.</param>
    /// <param name="work">El trabajo de la entrega; recibe el token que cancela <see cref="DisposeAsync"/>.</param>
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

    /// <summary>Relanza, una vez, la primera excepción que lanzó el trabajo de una entrega.</summary>
    public void ThrowIfFaulted() => Interlocked.Exchange(ref _fault, null)?.Throw();

    /// <summary>
    /// Cancela el trabajo en curso, descarta lo que esperaba su turno y espera a que todo termine: después no queda nada
    /// escribiendo en la consola.
    /// </summary>
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
            // ForceYielding: el trabajo nunca corre dentro de Enqueue, que así vuelve de inmediato al long-polling.
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
            // La escucha terminó: lo que esperaba turno o cupo no corre; la API lo cierra al vencer su lease.
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
