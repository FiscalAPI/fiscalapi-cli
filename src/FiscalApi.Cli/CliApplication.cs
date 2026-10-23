using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using FiscalApi.Cli.Errors;
using FiscalApi.Cli.Updates;

namespace FiscalApi.Cli;

public sealed class CliApplication
{
    private readonly RootCommand _rootCommand;
    private readonly GlobalOptions _globalOptions;
    private readonly CliConsole _console;
    private readonly InvocationConfiguration _invocation;
    private readonly InvocationConfiguration _jsonInvocation;
    private readonly IUpdateCheck _updateCheck;

    public CliApplication(GlobalOptions globalOptions, IReadOnlyList<Command> commands, CliConsole console, IUpdateCheck updateCheck)
    {
        _globalOptions = globalOptions;
        _console = console;
        _updateCheck = updateCheck;
        _rootCommand = new RootCommand("FiscalAPI CLI. Manage webhooks and events from your terminal.");
        globalOptions.AddTo(_rootCommand);
        foreach (var command in commands)
        {
            _rootCommand.Subcommands.Add(command);
        }

        var versionOption = _rootCommand.Options.OfType<VersionOption>().Single();
        versionOption.Action = new PrintVersionAction();
        versionOption.Description = "Show the CLI version.";
        _rootCommand.Options.OfType<HelpOption>().Single().Description = "Show help and usage.";

        _rootCommand.SetAction((parseResult, _) => Task.FromResult(new HelpAction().Invoke(parseResult)));

        _invocation = CreateInvocation(output: console.Out, console.Error);
        _jsonInvocation = CreateInvocation(output: console.Error, console.Error);
    }

    public async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var parseResult = _rootCommand.Parse(args);
        if (parseResult.Action is ParseErrorAction)
        {
            return await ReportUsageErrorsAsync(parseResult);
        }

        var settings = _globalOptions.Read(parseResult);
        var updateNotice = _updateCheck.Start(settings, cancellationToken);
        var exitCode = await InvokeAsync(parseResult, settings, cancellationToken);
        if (await NoticeToShowAsync(updateNotice, exitCode) is { } notice)
        {
            await notice.WriteToAsync(_console.Error);
        }

        return exitCode;
    }

    private static async Task<UpdateNotice?> NoticeToShowAsync(Task<UpdateNotice?> updateNotice, int exitCode)
    {
        if (exitCode != ExitCodes.Cancelled)
        {
            return await updateNotice;
        }

        return updateNotice.IsCompletedSuccessfully ? updateNotice.Result : null;
    }

    private async Task<int> InvokeAsync(ParseResult parseResult, GlobalSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var invocation = settings.Json ? _jsonInvocation : _invocation;
            return await parseResult.InvokeAsync(invocation, cancellationToken);
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
            await _console.Error.WriteLineAsync($"Unexpected CLI error: {exception}");
            return ExitCodes.Failure;
        }
    }

    private async Task<int> ReportUsageErrorsAsync(ParseResult parseResult)
    {
        foreach (var error in parseResult.Errors)
        {
            await _console.Error.WriteLineAsync($"Error: {error.Message}");
        }

        await _console.Error.WriteLineAsync("Run `fiscalapi --help` to see commands and options.");
        return ExitCodes.Usage;
    }

    private static InvocationConfiguration CreateInvocation(TextWriter output, TextWriter error) => new()
    {
        Output = output,
        Error = error,
        EnableDefaultExceptionHandler = false,
        ProcessTerminationTimeout = null
    };

    private sealed class PrintVersionAction : AsynchronousCommandLineAction
    {
        public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken)
        {
            await parseResult.InvocationConfiguration.Output.WriteLineAsync(CliVersion.Current.AsMemory(), cancellationToken);
            return ExitCodes.Success;
        }
    }
}
