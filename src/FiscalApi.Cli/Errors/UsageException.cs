namespace FiscalApi.Cli.Errors;

/// <summary>
/// Uso incorrecto que System.CommandLine no puede detectar al parsear (p. ej. una combinación de opciones inválida).
/// </summary>
public sealed class UsageException(string message) : CliException(message)
{
    public override int ExitCode => ExitCodes.Usage;
}
