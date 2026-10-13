using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli;

/// <summary>
/// Comando raíz <c>fiscalapi</c> y el único punto que atrapa errores y los traduce a códigos de salida.
/// Los comandos llegan ya construidos por constructor: sin contenedor DI ni registro por <see cref="Type"/>,
/// que se resuelven por reflexión y chocan con Native AOT.
/// </summary>
public sealed class CliApplication
{
    private readonly RootCommand _rootCommand;
    private readonly CliConsole _console;
    private readonly InvocationConfiguration _invocation;

    public CliApplication(GlobalOptions globalOptions, IReadOnlyList<Command> commands, CliConsole console)
    {
        _console = console;
        _rootCommand = new RootCommand("CLI de FiscalAPI: webhooks y eventos desde la terminal.");
        globalOptions.AddTo(_rootCommand);
        foreach (var command in commands)
        {
            _rootCommand.Subcommands.Add(command);
        }

        // La versión sale de la misma fuente que el User-Agent y no del ensamblado de entrada del proceso.
        var versionOption = _rootCommand.Options.OfType<VersionOption>().Single();
        versionOption.Action = new PrintVersionAction();
        versionOption.Description = "Muestra la versión del CLI.";
        _rootCommand.Options.OfType<HelpOption>().Single().Description = "Muestra la ayuda y el uso.";

        // Sin subcomando, la ayuda: es lo que la persona necesita y no es un error.
        _rootCommand.SetAction((parseResult, _) => Task.FromResult(new HelpAction().Invoke(parseResult)));

        _invocation = new InvocationConfiguration
        {
            Output = console.Out,
            Error = console.Error,
            // Los errores los atrapa RunAsync, con los códigos de salida del contrato.
            EnableDefaultExceptionHandler = false,
            // Ctrl+C lo maneja Program y llega aquí como el token de RunAsync.
            ProcessTerminationTimeout = null
        };
    }

    public async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var parseResult = _rootCommand.Parse(args);
        if (parseResult.Action is ParseErrorAction)
        {
            return await ReportUsageErrorsAsync(parseResult);
        }

        try
        {
            return await parseResult.InvokeAsync(_invocation, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ExitCodes.Cancelled;
        }
        catch (CliException exception)
        {
            exception.WriteTo(_console.Error);
            return exception.ExitCode;
        }
        catch (Exception exception)
        {
            // Un defecto del CLI: se muestra completo para poder reportarlo, sin el volcado del runtime.
            await _console.Error.WriteLineAsync($"Error inesperado del CLI: {exception}");
            return ExitCodes.Failure;
        }
    }

    /// <summary>
    /// Los errores de parseo se escriben aquí y solo en stderr: la acción de System.CommandLine también escribe
    /// en stdout (la ayuda y sugerencias), y con <c>--json</c> stdout debe quedar limpio. Sale con 2 y no con
    /// el 1 que devuelve System.CommandLine, porque el contrato reserva el 1 para la API y la red.
    /// </summary>
    private async Task<int> ReportUsageErrorsAsync(ParseResult parseResult)
    {
        foreach (var error in parseResult.Errors)
        {
            await _console.Error.WriteLineAsync($"Error: {error.Message}");
        }

        await _console.Error.WriteLineAsync("Usa `fiscalapi --help` para ver los comandos y opciones.");
        return ExitCodes.Usage;
    }

    // Síncrona como la acción integrada de --version que reemplaza.
    private sealed class PrintVersionAction : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            parseResult.InvocationConfiguration.Output.WriteLine(CliVersion.Current);
            return ExitCodes.Success;
        }
    }
}
