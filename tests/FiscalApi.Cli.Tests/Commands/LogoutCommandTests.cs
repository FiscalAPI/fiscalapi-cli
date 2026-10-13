using FiscalApi.Cli.Commands;
using FiscalApi.Cli.Credentials;
using FiscalApi.Cli.Tests.Credentials;
using FiscalApi.Cli.Tests.Updates;

namespace FiscalApi.Cli.Tests.Commands;

/// <summary>
/// CLI-002: <c>fiscalapi logout</c> es local (no recibe cliente HTTP). Termina con éxito si borró o si no había
/// nada guardado; un almacén que existe pero falla (bloqueado, sin respuesta) es un error con código 1.
/// </summary>
public sealed class LogoutCommandTests
{
    private readonly InMemoryCredentialStore _store = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    [Fact]
    public async Task Deletes_only_the_credentials_of_the_selected_environment()
    {
        var live = ApiCredentials.Create("sk_live_clave", "tenant-live");
        _store.Save("test.fiscalapi.com", ApiCredentials.Create("sk_test_clave", "tenant-test"));
        _store.Save("live.fiscalapi.com", live);

        var exitCode = await RunAsync(["logout"]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Null(_store.Read("test.fiscalapi.com"));
        Assert.Equal(live, _store.Read("live.fiscalapi.com"));
        Assert.Contains("Credenciales borradas para test.fiscalapi.com.", _error.ToString());
        Assert.Empty(_out.ToString());
    }

    [Fact]
    public async Task Live_selects_the_live_entry()
    {
        _store.Save("live.fiscalapi.com", ApiCredentials.Create("sk_live_clave", "tenant-live"));

        await RunAsync(["logout", "--live"]);

        Assert.Empty(_store.Entries);
    }

    [Fact]
    public async Task Without_saved_credentials_still_succeeds()
    {
        var exitCode = await RunAsync(["logout"]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("No había credenciales guardadas para test.fiscalapi.com.", _error.ToString());
    }

    [Fact]
    public async Task Without_a_store_still_succeeds_because_nothing_could_be_saved()
    {
        var exitCode = await RunAsync(["logout"], new UnavailableCredentialStore());

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("No había credenciales guardadas para test.fiscalapi.com.", _error.ToString());
    }

    [Fact]
    public async Task Locked_store_reports_that_it_could_not_delete_and_exits_1()
    {
        var exitCode = await RunAsync(["logout"], new LockedCredentialStore());

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("No se pudieron borrar las credenciales guardadas para test.fiscalapi.com.", _error.ToString());
        Assert.Contains("El almacén de credenciales del sistema existe, pero está bloqueado o no respondió.", _error.ToString());
        Assert.Contains(LockedCredentialStore.Detail, _error.ToString());
        Assert.DoesNotContain("No había credenciales", _error.ToString());
    }

    private Task<int> RunAsync(string[] args, ICredentialStore? store = null)
    {
        var globalOptions = new GlobalOptions();
        var console = new CliConsole(_out, _error);
        var logout = new LogoutCommand(globalOptions, store ?? _store, console);
        return new CliApplication(globalOptions, [logout], console, NoUpdateCheck.Instance).RunAsync(args, CancellationToken.None);
    }
}
