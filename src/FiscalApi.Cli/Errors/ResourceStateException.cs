namespace FiscalApi.Cli.Errors;

/// <summary>
/// El recurso no admite la operación en su estado (p. ej. editar una escucha de CLI). El CLI lo detecta con lo que ya leyó
/// de la API y lo informa con el texto que respondería la API; sale con 1, como un error de la API, porque el comando
/// estaba bien escrito.
/// </summary>
public sealed class ResourceStateException(string message) : CliException(message)
{
    public override int ExitCode => ExitCodes.Failure;
}
