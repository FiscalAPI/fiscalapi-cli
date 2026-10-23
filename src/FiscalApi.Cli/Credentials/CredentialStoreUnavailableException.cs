using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Credentials;

public sealed class CredentialStoreUnavailableException(string detail)
    : CliException($"No credential store is available. Set {CredentialResolver.ApiKeyVariable} and {CredentialResolver.TenantKeyVariable}.")
{
    public string Detail { get; } = detail;

    public override int ExitCode => ExitCodes.Usage;

    public override void WriteTo(TextWriter error)
    {
        base.WriteTo(error);
        error.WriteLine($"  Details: {Detail}");
    }
}
