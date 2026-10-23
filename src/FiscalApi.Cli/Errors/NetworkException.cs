namespace FiscalApi.Cli.Errors;

public sealed class NetworkException(string message, Exception innerException) : CliException(message, innerException)
{
    public override int ExitCode => ExitCodes.Failure;

    public override bool IsTransient => true;
}
