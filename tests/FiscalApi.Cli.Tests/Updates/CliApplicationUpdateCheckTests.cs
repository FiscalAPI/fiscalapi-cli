using System.CommandLine;
using FiscalApi.Cli.Errors;
using FiscalApi.Cli.Updates;

namespace FiscalApi.Cli.Tests.Updates;

/// <summary>
/// Cómo <see cref="CliApplication"/> usa el aviso de versión nueva: corre en paralelo al comando, se escribe en stderr al
/// final y nunca cambia stdout ni el código de salida.
/// </summary>
public sealed class CliApplicationUpdateCheckTests
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    [Fact]
    public async Task Notice_goes_to_stderr_after_the_command_without_touching_stdout_or_the_exit_code()
    {
        var check = new ScriptedUpdateCheck(Task.FromResult<UpdateNotice?>(Notice()));
        var probe = new Command("probe");
        probe.SetAction(async (_, cancellationToken) =>
        {
            await _out.WriteLineAsync("datos".AsMemory(), cancellationToken);
            await _error.WriteLineAsync("diagnóstico del comando".AsMemory(), cancellationToken);
            return ExitCodes.Success;
        });

        var exitCode = await RunAsync(["probe"], probe, check);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("datos" + Environment.NewLine, _out.ToString());
        var error = _error.ToString();
        Assert.Contains("https://github.com/FiscalAPI/fiscalapi-cli/releases/tag/v0.2.0", error);
        Assert.True(error.IndexOf("diagnóstico del comando", StringComparison.Ordinal) < error.IndexOf("0.2.0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Failed_command_keeps_its_exit_code_and_still_shows_the_notice()
    {
        var check = new ScriptedUpdateCheck(Task.FromResult<UpdateNotice?>(Notice()));
        var probe = new Command("probe");
        probe.SetAction((_, _) => Task.FromException<int>(new UsageException("Falta --url.")));

        var exitCode = await RunAsync(["probe"], probe, check);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("Falta --url.", _error.ToString());
        Assert.Contains("0.2.0", _error.ToString());
    }

    [Fact]
    public async Task Command_runs_while_the_check_is_still_pending()
    {
        var pending = new TaskCompletionSource<UpdateNotice?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var check = new ScriptedUpdateCheck(pending.Task);
        var ranBeforeTheCheckEnded = false;
        var probe = new Command("probe");
        probe.SetAction((_, _) =>
        {
            ranBeforeTheCheckEnded = !pending.Task.IsCompleted;
            pending.SetResult(null);
            return Task.FromResult(ExitCodes.Success);
        });

        var exitCode = await RunAsync(["probe"], probe, check);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(ranBeforeTheCheckEnded);
        Assert.Empty(_error.ToString());
    }

    [Fact]
    public async Task Ctrl_c_still_shows_a_notice_that_was_already_known()
    {
        // fiscalapi listen siempre termina con Ctrl+C: si el aviso ya llegó durante la escucha, se muestra.
        using var cancellation = new CancellationTokenSource();
        var check = new ScriptedUpdateCheck(Task.FromResult<UpdateNotice?>(Notice()));

        var exitCode = await RunAsync(["probe"], CancelledByCtrlC(cancellation), check, cancellation.Token);

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        Assert.Contains("https://github.com/FiscalAPI/fiscalapi-cli/releases/tag/v0.2.0", _error.ToString());
    }

    [Fact]
    public async Task Ctrl_c_exits_at_once_without_waiting_for_a_pending_check()
    {
        using var cancellation = new CancellationTokenSource();
        var neverEnds = new TaskCompletionSource<UpdateNotice?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var check = new ScriptedUpdateCheck(neverEnds.Task);

        var exitCode = await RunAsync(["probe"], CancelledByCtrlC(cancellation), check, cancellation.Token)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        Assert.Empty(_error.ToString());
    }

    [Fact]
    public async Task Ctrl_c_ignores_a_check_that_ended_without_a_notice()
    {
        using var cancellation = new CancellationTokenSource();
        var check = new ScriptedUpdateCheck(Task.FromResult<UpdateNotice?>(null));

        var exitCode = await RunAsync(["probe"], CancelledByCtrlC(cancellation), check, cancellation.Token);

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        Assert.Empty(_error.ToString());
    }

    /// <summary>Un comando que espera hasta que llega Ctrl+C, como fiscalapi listen.</summary>
    private static Command CancelledByCtrlC(CancellationTokenSource cancellation)
    {
        var probe = new Command("probe");
        probe.SetAction(async (_, cancellationToken) =>
        {
            await cancellation.CancelAsync();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return ExitCodes.Success;
        });
        return probe;
    }

    [Fact]
    public async Task The_check_receives_the_json_setting_of_the_invocation()
    {
        var check = new ScriptedUpdateCheck(Task.FromResult<UpdateNotice?>(null));
        var probe = new Command("probe");
        probe.SetAction((_, _) => Task.FromResult(ExitCodes.Success));

        await RunAsync(["probe", "--json"], probe, check);

        Assert.True(Assert.Single(check.Settings).Json);
    }

    [Fact]
    public async Task Usage_errors_from_parsing_do_not_start_the_check()
    {
        var check = new ScriptedUpdateCheck(Task.FromResult<UpdateNotice?>(Notice()));

        var exitCode = await RunAsync(["--no-existe"], new Command("probe"), check);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Empty(check.Settings);
    }

    private static UpdateNotice Notice()
    {
        Assert.True(SemanticVersion.TryParse("0.1.0", out var current));
        Assert.True(SemanticVersion.TryParse("v0.2.0", out var latest));
        return new UpdateNotice(current, latest);
    }

    private Task<int> RunAsync(string[] args, Command command, IUpdateCheck check, CancellationToken cancellationToken = default) =>
        new CliApplication(new GlobalOptions(), [command], new CliConsole(_out, _error), check).RunAsync(args, cancellationToken);

    /// <summary>Devuelve el resultado que se le da y anota con qué opciones globales se le pidió empezar.</summary>
    private sealed class ScriptedUpdateCheck(Task<UpdateNotice?> result) : IUpdateCheck
    {
        public List<GlobalSettings> Settings { get; } = [];

        public Task<UpdateNotice?> Start(GlobalSettings settings, CancellationToken cancellationToken)
        {
            Settings.Add(settings);
            return result;
        }
    }
}
