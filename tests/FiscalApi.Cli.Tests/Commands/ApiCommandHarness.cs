using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;
using FiscalApi.Cli.Commands;
using FiscalApi.Cli.Commands.Events;
using FiscalApi.Cli.Commands.Webhooks;
using FiscalApi.Cli.Credentials;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Input;
using FiscalApi.Cli.Tests.Credentials;
using FiscalApi.Cli.Tests.Http;
using FiscalApi.Cli.Tests.Updates;

namespace FiscalApi.Cli.Tests.Commands;

/// <summary>
/// Corre <c>fiscalapi webhooks …</c>, <c>fiscalapi events …</c> y <c>fiscalapi trigger …</c> con la misma composición que <c>Program</c>, sobre
/// la API simulada (<see cref="StubHttpMessageHandler"/>) y las credenciales de las variables de entorno.
/// </summary>
internal sealed class ApiCommandHarness
{
    public const string ApiKey = "sk_test_clave-de-comandos";
    public const string TenantKey = "tenant-de-comandos-0001";

    public StubHttpMessageHandler Api { get; } = new();

    public string Out => _out.ToString();

    public string Error => _error.ToString();

    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    /// <param name="args">El comando sin <c>--api-base</c>, que se agrega aquí.</param>
    /// <param name="confirmationInput">Lo que la persona escribe al confirmar (<c>delete</c> sin <c>--yes</c>).</param>
    /// <param name="interactive">Si hay una persona en la terminal; con stdin redirigido, no.</param>
    /// <param name="confirmation">Otra confirmación en lugar de la de consola (p. ej. una que simula Ctrl+C).</param>
    /// <param name="cancellationToken">El token que Program cancela con Ctrl+C.</param>
    public Task<int> RunAsync(
        IEnumerable<string> args,
        string confirmationInput = "",
        bool interactive = false,
        IConfirmationPrompt? confirmation = null,
        CancellationToken cancellationToken = default)
    {
        var globalOptions = new GlobalOptions();
        var console = new CliConsole(_out, _error);
        var credentials = new CredentialResolver(new InMemoryCredentialStore(), name => name switch
        {
            CredentialResolver.ApiKeyVariable => ApiKey,
            CredentialResolver.TenantKeyVariable => TenantKey,
            _ => null
        });
        var apiClients = new FiscalApiClientFactory(Api, credentials, console.Error, FiscalApiClientBuilder.NoDelayRetryPolicy);
        confirmation ??= new ConsoleConfirmationPrompt(console.Error, new StringReader(confirmationInput), interactive);
        Command[] commands =
        [
            new WebhooksCommand(globalOptions, apiClients, confirmation, console),
            new EventsCommand(globalOptions, apiClients, console),
            new TriggerCommand(globalOptions, apiClients, console)
        ];

        return new CliApplication(globalOptions, commands, console, NoUpdateCheck.Instance)
            .RunAsync([.. args, "--api-base", FiscalApiClientBuilder.BaseAddress.ToString()], cancellationToken);
    }

    public Task<int> RunAsync(params string[] args) => RunAsync((IEnumerable<string>)args);

    /// <summary>El envelope de éxito de fapi-back con <paramref name="data"/> tal cual.</summary>
    public static string Success(string data) => $$"""{"data":{{data}},"succeeded":true,"message":"","details":""}""";

    /// <summary>La única request que recibió la API.</summary>
    public HttpRequestMessage SingleRequest() => Assert.Single(Api.Requests);

    public JsonNode? Body(int index = 0) => Api.RequestBodies[index] is { } body ? JsonNode.Parse(body) : null;

    /// <summary>Stdout es exactamente <paramref name="rawData"/> más el salto de línea, y es JSON válido.</summary>
    public void AssertJsonOut(string rawData)
    {
        Assert.Equal(rawData + Environment.NewLine, Out);
        using var _ = JsonDocument.Parse(Out);
    }

    public static void AssertJsonEqual(string expected, JsonNode? actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), actual), $"Se esperaba {expected} y llegó {actual?.ToJsonString()}");

    public static int Occurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
