namespace FiscalApi.Cli.Errors;

public sealed class InvalidCredentialsException(string host, ApiException rejection)
    : CliException($"Invalid credentials for {host}.", rejection)
{
    public override int ExitCode => ExitCodes.Failure;
}
