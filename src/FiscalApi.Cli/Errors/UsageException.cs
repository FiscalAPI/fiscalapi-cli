namespace FiscalApi.Cli.Errors;

public sealed class UsageException(string message) : CliException(message)
{
    public override int ExitCode => ExitCodes.Usage;
}
