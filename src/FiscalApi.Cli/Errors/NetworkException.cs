namespace FiscalApi.Cli.Errors;

/// <summary>La API no respondió: conexión rechazada, DNS, TLS o tiempo de espera agotado.</summary>
public sealed class NetworkException(string message, Exception innerException) : CliException(message, innerException)
{
    public override int ExitCode => ExitCodes.Failure;
}
