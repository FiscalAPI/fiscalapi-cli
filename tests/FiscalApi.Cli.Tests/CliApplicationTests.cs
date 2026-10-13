using System.CommandLine;
using System.Net;
using FiscalApi.Cli.Errors;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Tests.Http;

namespace FiscalApi.Cli.Tests;

/// <summary>
/// Códigos de salida del contrato común: 0 éxito, 1 API o red, 2 uso, 130 Ctrl+C.
/// </summary>
public sealed class CliApplicationTests
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    [Fact]
    public async Task Version_prints_the_informational_version_and_exits_0()
    {
        var exitCode = await RunAsync(["--version"]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(CliVersion.Current, _out.ToString().Trim());
    }

    [Fact]
    public async Task Help_lists_the_global_options_and_exits_0()
    {
        var exitCode = await RunAsync(["--help"]);

        Assert.Equal(ExitCodes.Success, exitCode);
        var help = _out.ToString();
        Assert.Contains("--live", help);
        Assert.Contains("--api-base", help);
        Assert.Contains("--json", help);
        Assert.Contains("--verbose", help);
    }

    [Fact]
    public async Task Without_arguments_prints_help_and_exits_0()
    {
        var exitCode = await RunAsync([]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("--api-base", _out.ToString());
    }

    [Fact]
    public async Task Without_subcommand_and_with_json_the_help_goes_to_stderr()
    {
        var exitCode = await RunAsync(["--json"]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Empty(_out.ToString());
        Assert.Contains("--api-base", _error.ToString());
    }

    [Fact]
    public async Task Unknown_option_is_a_usage_error()
    {
        var exitCode = await RunAsync(["--no-existe"]);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("--no-existe", _error.ToString());
        Assert.Contains("fiscalapi --help", _error.ToString());
    }

    [Fact]
    public async Task Usage_error_leaves_stdout_empty_even_with_json()
    {
        var exitCode = await RunAsync(["--json", "--no-existe"]);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Empty(_out.ToString());
    }

    [Fact]
    public async Task Invalid_api_base_is_a_usage_error()
    {
        var exitCode = await RunAsync(["--api-base", "localhost:5001"]);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("--api-base", _error.ToString());
    }

    [Fact]
    public async Task Usage_exception_from_a_command_exits_2()
    {
        var exitCode = await RunAsync(["probe"], Throwing(new UsageException("Falta --url.")));

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("Falta --url.", _error.ToString());
    }

    [Fact]
    public async Task Api_exception_from_a_command_exits_1_and_goes_to_stderr()
    {
        var error = ApiException.FromEnvelope(
            HttpStatusCode.NotFound,
            new ApiResponse { Message = "Recurso no encontrado", Details = "", TraceIdentifier = "0HN7:00000003" });

        var exitCode = await RunAsync(["probe"], Throwing(error));

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Empty(_out.ToString());
        Assert.Contains("Recurso no encontrado", _error.ToString());
        Assert.Contains("0HN7:00000003", _error.ToString());
    }

    [Fact]
    public async Task Network_exception_from_a_command_exits_1()
    {
        var error = new NetworkException("No se pudo conectar con https://test.fiscalapi.com/.", new HttpRequestException());

        var exitCode = await RunAsync(["probe"], Throwing(error));

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("No se pudo conectar", _error.ToString());
    }

    [Fact]
    public async Task Unexpected_exception_exits_1_without_crashing()
    {
        var exitCode = await RunAsync(["probe"], Throwing(new InvalidOperationException("estado imposible")));

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("estado imposible", _error.ToString());
    }

    [Fact]
    public async Task Cancellation_by_ctrl_c_exits_130()
    {
        using var cancellation = new CancellationTokenSource();
        var probe = new Command("probe");
        probe.SetAction(async (_, cancellationToken) =>
        {
            await cancellation.CancelAsync();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return ExitCodes.Success;
        });

        var exitCode = await RunAsync(["probe"], probe, cancellation.Token);

        Assert.Equal(ExitCodes.Cancelled, exitCode);
    }

    [Fact]
    public async Task Ctrl_c_during_an_api_request_exits_130_and_not_as_a_network_error()
    {
        using var cancellation = new CancellationTokenSource();
        var client = FiscalApiClientBuilder.Create(new StubHttpMessageHandler().NeverRespond());

        var exitCode = await RunAsync(["probe"], CallingApi(client, cancellation), cancellation.Token);

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        Assert.Empty(_error.ToString());
    }

    [Fact]
    public async Task Success_response_with_data_of_another_shape_exits_1_showing_the_exception()
    {
        var client = FiscalApiClientBuilder.Create(new StubHttpMessageHandler()
            .RespondJson(HttpStatusCode.OK, """{"succeeded":true,"data":"no es un objeto"}"""));

        var exitCode = await RunAsync(["probe"], CallingApi(client));

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("JsonException", _error.ToString());
        Assert.DoesNotContain("Respuesta inesperada", _error.ToString());
    }

    [Fact]
    public async Task Command_exit_code_is_returned_as_is()
    {
        var probe = new Command("probe");
        probe.SetAction((_, _) => Task.FromResult(ExitCodes.Success));

        var exitCode = await RunAsync(["probe"], probe);

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    private static Command Throwing(Exception exception)
    {
        var probe = new Command("probe");
        probe.SetAction((_, _) => Task.FromException<int>(exception));
        return probe;
    }

    /// <summary>Un comando que hace un GET; si recibe <paramref name="cancelAfterStart"/>, simula Ctrl+C en pleno request.</summary>
    private static Command CallingApi(FiscalApiClient client, CancellationTokenSource? cancelAfterStart = null)
    {
        var probe = new Command("probe");
        probe.SetAction(async (_, cancellationToken) =>
        {
            var request = client.GetAsync("api/v4/probe", TestJsonContext.Default.ApiResponseProbeData, cancellationToken);
            if (cancelAfterStart is not null)
            {
                await cancelAfterStart.CancelAsync();
            }

            await request;
            return ExitCodes.Success;
        });
        return probe;
    }

    private Task<int> RunAsync(string[] args, Command? command = null, CancellationToken cancellationToken = default)
    {
        var application = new CliApplication(
            new GlobalOptions(),
            command is null ? [] : [command],
            new CliConsole(_out, _error));
        return application.RunAsync(args, cancellationToken);
    }
}
