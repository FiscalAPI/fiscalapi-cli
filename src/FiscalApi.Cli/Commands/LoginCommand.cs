using System.CommandLine;
using System.Net;
using FiscalApi.Cli.Credentials;
using FiscalApi.Cli.Errors;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Input;

namespace FiscalApi.Cli.Commands;

/// <summary>
/// <c>fiscalapi login</c>: pide la API key y la tenant key, las valida con
/// <c>GET /api/v4/me/features</c> y las guarda en el almacén del SO, en la entrada del host del ambiente.
/// No usa las variables de entorno: guarda lo que la persona acaba de dar.
/// </summary>
public sealed class LoginCommand : Command
{
    private const string FeaturesPath = "api/v4/me/features";

    // El permiso que exigen listen, trigger y webhooks (Permissions.Webhooks.Manage de fapi-back).
    private const string WebhooksManagePermission = "Webhooks.Manage";

    private readonly GlobalOptions _globalOptions;
    private readonly FiscalApiClientFactory _apiClients;
    private readonly ICredentialStore _store;
    private readonly ISecretReader _input;
    private readonly CliConsole _console;

    public LoginCommand(
        GlobalOptions globalOptions,
        FiscalApiClientFactory apiClients,
        ICredentialStore store,
        ISecretReader input,
        CliConsole console)
        : base("login", "Valida la API key y la tenant key y las guarda en el almacén de credenciales del sistema.")
    {
        _globalOptions = globalOptions;
        _apiClients = apiClients;
        _store = store;
        _input = input;
        _console = console;
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = _globalOptions.Read(parseResult);
        var host = settings.Environment.Host;

        // Sin almacén no se piden las llaves ni se llama a la API: no se podrían guardar.
        _store.EnsureAvailable(host);

        // Cada valor se valida en cuanto se lee: un valor inválido es un error de uso, no un 401, y una API key
        // inválida se rechaza antes de pedir la tenant key.
        var apiKey = ApiCredentials.ValidApiKey(await _input.ReadAsync("API key: ", cancellationToken));
        var credentials = ApiCredentials.Create(apiKey, await _input.ReadAsync("Tenant key: ", cancellationToken));

        var features = await FetchFeaturesAsync(settings, credentials, cancellationToken);
        _store.Save(host, credentials);

        // Los mensajes van a stderr: login no produce datos, y con --json stdout queda vacío.
        await _console.Error.WriteLineAsync($"Listo. Credenciales guardadas para {host}.");
        if (features?.Permissions.Contains(WebhooksManagePermission, StringComparer.Ordinal) != true)
        {
            await _console.Error.WriteLineAsync(
                "Esta API key no tiene el permiso Webhooks.Manage; listen, trigger y webhooks responderán 403.");
        }

        return ExitCodes.Success;
    }

    private async Task<PersonFeatures?> FetchFeaturesAsync(
        GlobalSettings settings, ApiCredentials credentials, CancellationToken cancellationToken)
    {
        var client = _apiClients.Create(settings, credentials, FiscalApiClientFactory.DefaultTimeout);
        try
        {
            return await client.GetAsync(FeaturesPath, FiscalApiJsonContext.Default.ApiResponsePersonFeatures, cancellationToken);
        }
        // fapi-back responde 401 tanto por la API key como por la tenant key. Otro error (p. ej. el 403 de un
        // tenant inactivo) se muestra tal cual, con su mensaje y su traceIdentifier.
        catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidCredentialsException(settings.Environment.Host, exception);
        }
    }
}
