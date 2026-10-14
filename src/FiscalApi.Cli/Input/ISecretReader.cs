namespace FiscalApi.Cli.Input;

/// <summary>
/// Entrada de un secreto. Nunca llega por argumento: quedaría en el historial del shell y en la lista de procesos.
/// </summary>
public interface ISecretReader
{
    /// <param name="prompt">Lo que se le pide a la persona, si hay una persona en la terminal.</param>
    /// <returns>El valor leído, o <c>null</c> si la entrada terminó antes.</returns>
    Task<string?> ReadAsync(string prompt, CancellationToken cancellationToken);
}
