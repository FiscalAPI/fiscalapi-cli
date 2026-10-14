using System.CommandLine;
using FiscalApi.Cli.Credentials;

namespace FiscalApi.Cli.Commands;

/// <summary>
/// <c>fiscalapi logout</c>: borra la entrada del host del ambiente en el almacén del SO. Es local (no
/// llama a la API). Termina con éxito si borró o si no había nada guardado; si el almacén existe pero falla
/// (bloqueado, sin respuesta), lo dice y sale con 1.
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

    private bool DeleteSavedCredentials(string host)
    {
        try
        {
            return _store.Delete(host);
        }
        // Sin almacén en el SO, login nunca pudo guardar nada: no hay nada que borrar y no es un error.
        catch (CredentialStoreUnavailableException)
        {
            return false;
        }
        // El almacén existe: la entrada puede seguir ahí, así que nunca se dice que no había nada.
        catch (CredentialStoreException exception)
        {
            throw exception.During($"No se pudieron borrar las credenciales guardadas para {host}.");
        }
    }
}
