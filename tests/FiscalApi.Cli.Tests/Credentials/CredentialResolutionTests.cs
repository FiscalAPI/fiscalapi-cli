using System.CommandLine;
using System.Net;
using FiscalApi.Cli.Credentials;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Tests.Http;
using FiscalApi.Cli.Tests.Updates;

namespace FiscalApi.Cli.Tests.Credentials;

/// <summary>
/// <c>FISCALAPI_API_KEY</c>/<c>FISCALAPI_TENANT_KEY</c> ganan sobre lo guardado, y las credenciales nunca
/// aparecen en <c>--verbose</c> ni en un error.
/// </summary>
public sealed class CredentialResolutionTests
{
    private const string EnvironmentApiKey = "sk_test_clave-del-entorno";
    private const string EnvironmentTenantKey = "tenant-del-entorno-0001";
    private const string StoredApiKey = "sk_test_clave-guardada";
    private const string StoredTenantKey = "tenant-guardado-0002";

    private static readonly string Host = FiscalApiClientBuilder.BaseAddress.Authority;

    private readonly InMemoryCredentialStore _store = new();
    private readonly Dictionary<string, string> _variables = [];
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    [Fact]
    public void Environment_variables_win_over_the_stored_credentials()
    {
        _store.Save(Host, ApiCredentials.Create(StoredApiKey, StoredTenantKey));
        DefineEnvironmentCredentials();

        var credentials = Resolver().Resolve(Environment());

        Assert.Equal(ApiCredentials.Create(EnvironmentApiKey, EnvironmentTenantKey), credentials);
    }

    [Fact]
    public void Without_environment_variables_the_stored_credentials_of_the_host_are_used()
    {
        _store.Save(Host, ApiCredentials.Create(StoredApiKey, StoredTenantKey));
        _store.Save("otro-host.example.test", ApiCredentials.Create("sk_test_de-otro-host", "tenant-de-otro-host"));

        var credentials = Resolver().Resolve(Environment());

        Assert.Equal(ApiCredentials.Create(StoredApiKey, StoredTenantKey), credentials);
    }

    [Fact]
    public void Blank_environment_variables_count_as_undefined()
    {
        _store.Save(Host, ApiCredentials.Create(StoredApiKey, StoredTenantKey));
        _variables[CredentialResolver.ApiKeyVariable] = "  ";
        _variables[CredentialResolver.TenantKeyVariable] = "";

        var credentials = Resolver().Resolve(Environment());

        Assert.Equal(ApiCredentials.Create(StoredApiKey, StoredTenantKey), credentials);
    }

    [Theory]
    [InlineData(CredentialResolver.ApiKeyVariable)]
    [InlineData(CredentialResolver.TenantKeyVariable)]
    public async Task Only_one_environment_variable_is_a_usage_error_and_is_not_mixed_with_the_stored_entry(string variable)
    {
        _store.Save(Host, ApiCredentials.Create(StoredApiKey, StoredTenantKey));
        _variables[variable] = variable == CredentialResolver.ApiKeyVariable ? EnvironmentApiKey : EnvironmentTenantKey;

        var exitCode = await RunProbeAsync();

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains(CredentialResolver.ApiKeyVariable, _error.ToString());
        Assert.Contains(CredentialResolver.TenantKeyVariable, _error.ToString());
        AssertNoCredentialIn(_out.ToString() + _error.ToString());
    }

    [Fact]
    public async Task Without_credentials_is_a_usage_error_that_points_to_login()
    {
        var exitCode = await RunProbeAsync();

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains($"No hay credenciales para {Host}.", _error.ToString());
        Assert.Contains("fiscalapi login", _error.ToString());
        Assert.Contains(CredentialResolver.ApiKeyVariable, _error.ToString());
    }

    [Fact]
    public async Task Without_store_and_without_environment_variables_shows_the_approved_text()
    {
        var exitCode = await RunProbeAsync(store: new UnavailableCredentialStore());

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains(
            "No hay un almacén de credenciales disponible en este sistema. Define FISCALAPI_API_KEY y FISCALAPI_TENANT_KEY.",
            _error.ToString());
        Assert.Contains(UnavailableCredentialStore.Detail, _error.ToString());
    }

    [Fact]
    public async Task Locked_store_without_environment_variables_is_a_store_error_with_its_detail()
    {
        var exitCode = await RunProbeAsync(store: new LockedCredentialStore());

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("El almacén de credenciales del sistema existe, pero está bloqueado o no respondió.", _error.ToString());
        Assert.Contains(LockedCredentialStore.Detail, _error.ToString());
        Assert.DoesNotContain("No hay un almacén", _error.ToString());
    }

    [Fact]
    public async Task Environment_variables_do_not_touch_a_locked_store()
    {
        DefineEnvironmentCredentials();
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.OK, PersonFeaturesJson.WithWebhooksManage);

        var exitCode = await RunProbeAsync(handler, new LockedCredentialStore());

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    [Fact]
    public async Task Environment_variables_work_without_a_store()
    {
        DefineEnvironmentCredentials();
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.OK, PersonFeaturesJson.WithWebhooksManage);

        var exitCode = await RunProbeAsync(handler, new UnavailableCredentialStore());

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    [Fact]
    public async Task Requests_carry_the_resolved_credentials_in_their_headers()
    {
        DefineEnvironmentCredentials();
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.OK, PersonFeaturesJson.WithWebhooksManage);

        await RunProbeAsync(handler);

        var request = Assert.Single(handler.Requests);
        Assert.Equal([EnvironmentApiKey], request.Headers.GetValues("X-API-KEY"));
        Assert.Equal([EnvironmentTenantKey], request.Headers.GetValues("X-TENANT-KEY"));
    }

    [Fact]
    public async Task Verbose_and_a_rejected_request_never_show_the_environment_credentials()
    {
        DefineEnvironmentCredentials();
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.Unauthorized,
            """{"succeeded":false,"message":"Invalid API KEY","details":"","traceIdentifier":"0HN7:00000401"}""");

        var exitCode = await RunProbeAsync(handler, verbose: true);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("> GET https://api.example.test/api/v4/me/features", _error.ToString());
        Assert.Contains("Invalid API KEY", _error.ToString());
        AssertNoCredentialIn(_out.ToString() + _error.ToString());
    }

    [Fact]
    public async Task Verbose_and_a_network_failure_never_show_the_stored_credentials()
    {
        _store.Save(Host, ApiCredentials.Create(StoredApiKey, StoredTenantKey));
        var handler = new StubHttpMessageHandler()
            .FailWithNetworkError(HttpRequestError.NameResolutionError);

        var exitCode = await RunProbeAsync(handler, verbose: true);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("No se pudo conectar", _error.ToString());
        AssertNoCredentialIn(_out.ToString() + _error.ToString());
    }

    [Theory]
    [InlineData("sk_test_con\r\nsalto", EnvironmentTenantKey)]
    [InlineData("sk_test_con espacio", EnvironmentTenantKey)]
    [InlineData(EnvironmentApiKey, "tenant-con-eñe")]
    public async Task Invalid_credential_value_is_a_usage_error_that_does_not_echo_the_value(string apiKey, string tenantKey)
    {
        _variables[CredentialResolver.ApiKeyVariable] = apiKey;
        _variables[CredentialResolver.TenantKeyVariable] = tenantKey;

        var exitCode = await RunProbeAsync();

        Assert.Equal(ExitCodes.Usage, exitCode);
        var output = _out.ToString() + _error.ToString();
        Assert.DoesNotContain(apiKey, output);
        Assert.DoesNotContain(tenantKey, output);
        Assert.DoesNotContain("sk_test_con", output);
        Assert.DoesNotContain("tenant-con", output);
    }

    [Fact]
    public void Text_representation_of_the_credentials_hides_their_values()
    {
        var text = ApiCredentials.Create(StoredApiKey, StoredTenantKey).ToString();

        Assert.DoesNotContain(StoredApiKey, text);
        Assert.DoesNotContain(StoredTenantKey, text);
    }

    private void DefineEnvironmentCredentials()
    {
        _variables[CredentialResolver.ApiKeyVariable] = EnvironmentApiKey;
        _variables[CredentialResolver.TenantKeyVariable] = EnvironmentTenantKey;
    }

    private CredentialResolver Resolver(ICredentialStore? store = null) =>
        new(store ?? _store, name => _variables.GetValueOrDefault(name));

    private static ApiEnvironment Environment() => ApiEnvironment.Resolve(FiscalApiClientBuilder.BaseAddress, live: false);

    private static void AssertNoCredentialIn(string output)
    {
        foreach (var secret in new[] { EnvironmentApiKey, EnvironmentTenantKey, StoredApiKey, StoredTenantKey })
        {
            Assert.DoesNotContain(secret, output);
        }
    }

    /// <summary>
    /// Un comando de prueba que pide su cliente como lo harán los comandos de la API: la fábrica resuelve las
    /// credenciales. Corre en <see cref="CliApplication"/> para ver todo lo que llega a stdout y stderr.
    /// </summary>
    private Task<int> RunProbeAsync(StubHttpMessageHandler? handler = null, ICredentialStore? store = null, bool verbose = false)
    {
        var globalOptions = new GlobalOptions();
        var console = new CliConsole(_out, _error);
        var apiClients = new FiscalApiClientFactory(
            handler ?? new StubHttpMessageHandler(), Resolver(store), console.Error, FiscalApiClientBuilder.NoDelayRetryPolicy);

        var probe = new Command("probe");
        probe.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = apiClients.Create(globalOptions.Read(parseResult), FiscalApiClientFactory.DefaultTimeout);
            await client.GetAsync("api/v4/me/features", FiscalApiJsonContext.Default.ApiResponsePersonFeatures, cancellationToken);
            return ExitCodes.Success;
        });

        string[] args = ["probe", "--api-base", FiscalApiClientBuilder.BaseAddress.ToString(), .. verbose ? new[] { "--verbose" } : []];
        return new CliApplication(globalOptions, [probe], console, NoUpdateCheck.Instance).RunAsync(args, CancellationToken.None);
    }
}
