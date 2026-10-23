namespace FiscalApi.Cli.Errors;

public sealed class ResourceStateException(string message) : CliException(message)
{
    public override int ExitCode => ExitCodes.Failure;
}
