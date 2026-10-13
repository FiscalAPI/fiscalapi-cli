using System.Net;
using FiscalApi.Cli.Commands;
using FiscalApi.Cli.Credentials;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Input;
using FiscalApi.Cli.Tests.Credentials;
using FiscalApi.Cli.Tests.Http;
using FiscalApi.Cli.Tests.Input;

namespace FiscalApi.Cli.Tests.Commands;

/// <summary>
/// CLI-002: <c>fiscalapi login</c> lee la API key y la tenant key (por stdin redirigido o tecla por tecla), las valida con
/// <c>GET /api/v4/me/features</c> y las guarda por host. La API se simula con <see cref="StubHttpMessageHandler"/>.
/// </summary>
public sealed class LoginCommandTests
{
    private const string ApiKey = "sk_test_clave-de-login";
    private const string TenantKey = "tenant-de-login-0001";

    private static readonly string Host = FiscalApiClientBuilder.BaseAddress.Authority;

    private readonly InMemoryCredentialStore _store = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    [Fact]
    public async Task Valid_credentials_are_saved_for_the_host_and_reported()
    {
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.OK, PersonFeaturesJson.WithWebhooksManage);

        var exitCode = await RunAsync(handler, $"{ApiKey}\n{TenantKey}\n");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains($"Listo. Credenciales guardadas para {Host}.", _error.ToString());
        Assert.Equal(ApiCredentials.Create(ApiKey, TenantKey), _store.Read(Host));
        Assert.DoesNotContain("Webhooks.Manage", _error.ToString());
    }

    [Fact]
    public async Task Credentials_are_validated_against_me_features_with_their_headers()
    {
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.OK, PersonFeaturesJson.WithWebhooksManage);

        await RunAsync(handler, $"{ApiKey}\n{TenantKey}\n");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri("https://api.example.test/api/v4/me/features"), request.RequestUri);
        Assert.Equal([ApiKey], request.Headers.GetValues("X-API-KEY"));
        Assert.Equal([TenantKey], request.Headers.GetValues("X-TENANT-KEY"));
    }

    [Fact]
    public async Task Rejected_credentials_are_not_saved_and_exit_1()
    {
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.Unauthorized,
            """{"succeeded":false,"message":"Invalid API KEY","details":"","traceIdentifier":"0HN7:00000401"}""");

        var exitCode = await RunAsync(handler, $"{ApiKey}\n{TenantKey}\n");

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains($"Las credenciales no son válidas para {Host}.", _error.ToString());
        Assert.Empty(_store.Entries);
    }

    [Fact]
    public async Task Rejection_does_not_overwrite_the_credentials_already_saved()
    {
        var saved = ApiCredentials.Create("sk_test_ya-guardada", "tenant-ya-guardado");
        _store.Save(Host, saved);
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.Unauthorized,
            """{"succeeded":false,"message":"Invalid tenant key","details":""}""");

        await RunAsync(handler, $"{ApiKey}\n{TenantKey}\n");

        Assert.Equal(saved, _store.Read(Host));
    }

    [Fact]
    public async Task Without_webhooks_manage_it_warns_and_still_saves()
    {
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.OK, PersonFeaturesJson.WithoutWebhooksManage);

        var exitCode = await RunAsync(handler, $"{ApiKey}\n{TenantKey}\n");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains(
            "Esta API key no tiene el permiso Webhooks.Manage; listen, trigger y webhook-endpoints responderán 403.",
            _error.ToString());
        Assert.Contains($"Listo. Credenciales guardadas para {Host}.", _error.ToString());
        Assert.Equal(ApiCredentials.Create(ApiKey, TenantKey), _store.Read(Host));
    }

    [Fact]
    public async Task Without_a_store_fails_with_the_approved_text_before_asking_for_keys_or_calling_the_api()
    {
        var handler = new StubHttpMessageHandler();
        var keys = new ScriptedKeys($"{ApiKey}\r{TenantKey}\r");

        var exitCode = await RunAsync(handler, InteractiveReader(keys), new UnavailableCredentialStore());

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains(
            "No hay un almacén de credenciales disponible en este sistema. Define FISCALAPI_API_KEY y FISCALAPI_TENANT_KEY.",
            _error.ToString());
        Assert.DoesNotContain("API key: ", _error.ToString());
        Assert.Equal(0, keys.Read);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Locked_store_fails_before_asking_for_keys_or_calling_the_api()
    {
        var handler = new StubHttpMessageHandler();
        var keys = new ScriptedKeys($"{ApiKey}\r{TenantKey}\r");

        var exitCode = await RunAsync(handler, InteractiveReader(keys), new LockedCredentialStore());

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("El almacén de credenciales del sistema existe, pero está bloqueado o no respondió.", _error.ToString());
        Assert.Contains(LockedCredentialStore.Detail, _error.ToString());
        Assert.Equal(0, keys.Read);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("\r")]
    [InlineData("sk_test_con eñe\r")]
    public async Task Invalid_api_key_fails_before_asking_for_the_tenant_key(string typed)
    {
        var handler = new StubHttpMessageHandler();

        var exitCode = await RunAsync(handler, InteractiveReader(new ScriptedKeys(typed + $"{TenantKey}\r")));

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("La API key", _error.ToString());
        Assert.Contains("API key: ", _error.ToString());
        Assert.DoesNotContain("Tenant key: ", _error.ToString());
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Interactive_input_saves_the_typed_keys_without_echoing_them()
    {
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.OK, PersonFeaturesJson.WithWebhooksManage);

        var exitCode = await RunAsync(handler, InteractiveReader(new ScriptedKeys($"{ApiKey}\r{TenantKey}\r")));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(ApiCredentials.Create(ApiKey, TenantKey), _store.Read(Host));
        Assert.DoesNotContain(ApiKey, _out.ToString() + _error.ToString());
        Assert.DoesNotContain(TenantKey, _out.ToString() + _error.ToString());
    }

    [Fact]
    public async Task Ctrl_c_while_waiting_for_a_key_exits_130()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var handler = new StubHttpMessageHandler();

        var exitCode = await RunAsync(handler, InteractiveReader(new ScriptedKeys("")), cancellationToken: cancellation.Token);

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        Assert.Empty(handler.Requests);
        Assert.Empty(_store.Entries);
    }

    [Fact]
    public async Task Redirected_input_with_a_byte_order_mark_is_accepted()
    {
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.OK, PersonFeaturesJson.WithWebhooksManage);

        var exitCode = await RunAsync(handler, $"\uFEFF{ApiKey}\n{TenantKey}\n");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(ApiCredentials.Create(ApiKey, TenantKey), _store.Read(Host));
    }

    [Fact]
    public async Task Redirected_input_is_read_line_by_line_and_trimmed()
    {
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.OK, PersonFeaturesJson.WithWebhooksManage);

        var exitCode = await RunAsync(handler, $"  {ApiKey}  \r\n\t{TenantKey}\r\n");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(ApiCredentials.Create(ApiKey, TenantKey), _store.Read(Host));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\n")]
    [InlineData("sk_test_sin-tenant\n")]
    public async Task Missing_or_empty_input_is_a_usage_error_without_calling_the_api(string input)
    {
        var handler = new StubHttpMessageHandler();

        var exitCode = await RunAsync(handler, input);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Empty(handler.Requests);
        Assert.Empty(_store.Entries);
    }

    [Fact]
    public async Task Credentials_never_reach_stdout_or_stderr_even_with_verbose()
    {
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.OK, PersonFeaturesJson.WithoutWebhooksManage);

        var exitCode = await RunAsync(handler, $"{ApiKey}\n{TenantKey}\n", verbose: true);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("> GET https://api.example.test/api/v4/me/features", _error.ToString());
        var output = _out.ToString() + _error.ToString();
        Assert.DoesNotContain(ApiKey, output);
        Assert.DoesNotContain(TenantKey, output);
    }

    [Fact]
    public async Task Stdout_stays_empty_so_json_mode_is_not_polluted()
    {
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.OK, PersonFeaturesJson.WithWebhooksManage);

        await RunAsync(handler, $"{ApiKey}\n{TenantKey}\n", json: true);

        Assert.Empty(_out.ToString());
    }

    [Fact]
    public async Task Credentials_are_saved_per_environment_host()
    {
        var handler = new StubHttpMessageHandler().RespondJson(HttpStatusCode.OK, PersonFeaturesJson.WithWebhooksManage);

        await RunAsync(handler, $"{ApiKey}\n{TenantKey}\n", apiBase: "http://localhost:5000");

        Assert.Equal(["localhost:5000"], _store.Entries.Keys);
        Assert.Contains("Listo. Credenciales guardadas para localhost:5000.", _error.ToString());
    }

    private ConsoleSecretReader InteractiveReader(ScriptedKeys keys) => new(_error, keys.TryRead);

    private Task<int> RunAsync(
        StubHttpMessageHandler handler,
        string input,
        ICredentialStore? store = null,
        bool verbose = false,
        bool json = false,
        string? apiBase = null) =>
        RunAsync(handler, new RedirectedSecretReader(new StringReader(input)), store, verbose, json, apiBase);

    private Task<int> RunAsync(
        StubHttpMessageHandler handler,
        ISecretReader input,
        ICredentialStore? store = null,
        bool verbose = false,
        bool json = false,
        string? apiBase = null,
        CancellationToken cancellationToken = default)
    {
        var globalOptions = new GlobalOptions();
        var console = new CliConsole(_out, _error);
        var credentialStore = store ?? _store;
        // Las variables de entorno no intervienen en login: la persona da las credenciales que se validan y guardan.
        var apiClients = new FiscalApiClientFactory(
            handler, new CredentialResolver(credentialStore, _ => null), console.Error, FiscalApiClientBuilder.NoDelayRetryPolicy);
        var login = new LoginCommand(globalOptions, apiClients, credentialStore, input, console);

        List<string> args = ["login", "--api-base", apiBase ?? FiscalApiClientBuilder.BaseAddress.ToString()];
        if (verbose)
        {
            args.Add("--verbose");
        }

        if (json)
        {
            args.Add("--json");
        }

        return new CliApplication(globalOptions, [login], console).RunAsync(args, cancellationToken);
    }
}
