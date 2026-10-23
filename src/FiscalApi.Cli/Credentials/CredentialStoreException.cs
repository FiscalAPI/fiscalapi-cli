using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Credentials;

public sealed class CredentialStoreException : CliException
{
    private CredentialStoreException(string message, string? detail)
        : base(message) => Detail = detail;

    public string? Detail { get; }

    public override int ExitCode => ExitCodes.Failure;

    public static CredentialStoreException Locked(string detail) =>
        new("The credential store is locked or not responding. Unlock it and try again.", detail);

    public static CredentialStoreException Failed(string operation, string detail) =>
        new($"The credential store failed to {operation} the entry.", detail);

    public static CredentialStoreException Unreadable(string host) =>
        new($"Saved credentials for {host} are unreadable. Run `fiscalapi logout`, then `fiscalapi login`.", detail: null);

    public CredentialStoreException During(string context) => new($"{context} {Message}", Detail);

    public override void WriteTo(TextWriter error)
    {
        base.WriteTo(error);
        if (Detail is not null)
        {
            error.WriteLine($"  Details: {Detail}");
        }
    }
}
