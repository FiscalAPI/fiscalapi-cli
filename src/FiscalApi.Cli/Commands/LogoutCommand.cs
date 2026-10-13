using System.CommandLine;
using FiscalApi.Cli.Credentials;

namespace FiscalApi.Cli.Commands;

/// <summary>
/// <c>fiscalapi logout</c> (CLI-002): borra la entrada del host del ambiente en el almacén del SO. Es local (no
/// llama a la API) y siempre termina con éxito, aunque no hubiera nada guardado.
/// </summary>
public sealed class LogoutCommand : Command
{
    private readonly GlobalOptions _globalOptions;
    private readonly ICredentialStore _store;
    private readonly CliConsole _console;

    public LogoutCommand(GlobalOptions globalOptions, ICredentialStore store, CliConsole console)
        : base("logout", "Borra las credenciales guardadas para el ambiente.")
    {
        _globalOptions = globalOptions;
        _store = store;
        _console = console;
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var host = _globalOptions.Read(parseResult).Environment.Host;

        // Como login, escribe en stderr: no produce datos.
        await _console.Error.WriteLineAsync(DeleteSavedCredentials(host)
            ? $"Credenciales borradas para {host}."
            : $"No había credenciales guardadas para {host}.");
        return ExitCodes.Success;
    }

    // Sin almacén en el SO, login nunca pudo guardar nada: no hay nada que borrar y no es un error.
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
    }
}
