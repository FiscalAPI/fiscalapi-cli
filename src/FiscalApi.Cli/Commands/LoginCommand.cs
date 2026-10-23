using System.CommandLine;
using System.Net;
using FiscalApi.Cli.Credentials;
using FiscalApi.Cli.Errors;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Input;

namespace FiscalApi.Cli.Commands;

public sealed class LoginCommand : Command
{
    private const string FeaturesPath = "api/v4/me/features";

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
        : base("login", "Validate your API key and tenant key and save them in the system credential store.")
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

        _store.EnsureAvailable(host);

        var apiKey = ApiCredentials.ValidApiKey(await _input.ReadAsync("API key: ", cancellationToken));
        var credentials = ApiCredentials.Create(apiKey, await _input.ReadAsync("Tenant key: ", cancellationToken));

        var features = await FetchFeaturesAsync(settings, credentials, cancellationToken);
        _store.Save(host, credentials);

        await _console.Error.WriteLineAsync($"Done. Credentials saved for {host}.");
        if (features?.Permissions.Contains(WebhooksManagePermission, StringComparer.Ordinal) != true)
        {
            await _console.Error.WriteLineAsync(
                "This API key lacks the Webhooks.Manage permission. listen, trigger and webhooks will return 403.");
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
        catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidCredentialsException(settings.Environment.Host, exception);
        }
    }
}
