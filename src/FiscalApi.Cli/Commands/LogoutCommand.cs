using System.CommandLine;
using FiscalApi.Cli.Credentials;

namespace FiscalApi.Cli.Commands;

public sealed class LogoutCommand : Command
{
    private readonly GlobalOptions _globalOptions;
    private readonly ICredentialStore _store;
    private readonly CliConsole _console;

    public LogoutCommand(GlobalOptions globalOptions, ICredentialStore store, CliConsole console)
        : base("logout", "Delete the saved credentials for the environment.")
    {
        _globalOptions = globalOptions;
        _store = store;
        _console = console;
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var host = _globalOptions.Read(parseResult).Environment.Host;

        await _console.Error.WriteLineAsync(DeleteSavedCredentials(host)
            ? $"Credentials deleted for {host}."
            : $"No saved credentials for {host}.");
        return ExitCodes.Success;
    }

    private bool DeleteSavedCredentials(string host)
    {
        try
        {
            return _store.Delete(host);
        }
        catch (CredentialStoreUnavailableException)
        {
            return false;
        }
        catch (CredentialStoreException exception)
        {
            throw exception.During($"Could not delete the saved credentials for {host}.");
        }
    }
}
