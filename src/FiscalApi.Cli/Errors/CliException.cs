namespace FiscalApi.Cli.Errors;

/// <summary>
/// Error esperado del CLI. Cada tipo sabe su código de salida y cómo mostrarse, así el único punto que atrapa
/// errores (<see cref="CliApplication"/>) no necesita condicionales por tipo.
/// </summary>
public abstract class CliException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public abstract int ExitCode { get; }

    public virtual void WriteTo(TextWriter error) => error.WriteLine($"Error: {Message}");
}
