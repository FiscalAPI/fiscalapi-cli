using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FiscalApi.Cli.Updates;

/// <summary>
/// Aviso de versión nueva, como el <c>CheckCliVersionAsync</c> del CLI de ABP pero sin bloquear: consulta el último
/// release de GitHub como mucho una vez cada <see cref="Interval"/>, en paralelo al comando y con un tope de
/// <see cref="Timeout"/>. Un aviso nunca cambia el resultado del comando, así que cualquier falla termina sin aviso.
/// </summary>
public sealed class UpdateCheck(
    HttpMessageHandler handler,
    UpdateCheckStateFile? stateFile,
    Func<string, string?> environment,
    bool isErrorRedirected,
    TimeProvider timeProvider,
    string currentVersion) : IUpdateCheck
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    /// <summary>Apaga el aviso. <c>CI</c> lo definen GitHub Actions, GitLab CI y la mayoría de los CI.</summary>
    public const string OptOutVariable = "FISCALAPI_NO_UPDATE_CHECK";
    private const string CiVariable = "CI";

    // releases/latest ya descarta borradores y prereleases (docs.github.com/rest/releases/releases#get-the-latest-release).
    // Anónimo: el repo es público y el límite de 60 consultas por hora e IP sobra para una al día.
    private static readonly Uri LatestReleaseUri = new("https://api.github.com/repos/FiscalAPI/fiscalapi-cli/releases/latest");
    private const string GitHubApiVersion = "2022-11-28";

    // La respuesta trae las notas del release y la lista de binarios: unos KB. El tope corta algo desmedido.
    private const int MaxResponseBytes = 1024 * 1024;

    public Task<UpdateNotice?> Start(GlobalSettings settings, CancellationToken cancellationToken)
    {
        if (IsTurnedOff(settings) || stateFile is null || !SemanticVersion.TryParse(currentVersion, out var current))
        {
            return Task.FromResult<UpdateNotice?>(null);
        }

        return CheckWithinBudgetAsync(stateFile, current, cancellationToken);
    }

    // Con --json la salida es para una máquina; con stderr redirigido, el aviso ensuciaría el log de un script.
    private bool IsTurnedOff(GlobalSettings settings) =>
        settings.Json || isErrorRedirected || IsOn(CiVariable) || IsOn(OptOutVariable);

    // Cualquier valor enciende la variable, salvo vacío, "0" o "false".
    private bool IsOn(string variable) =>
        environment(variable) is { Length: > 0 } value && value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase);

    private async Task<UpdateNotice?> CheckWithinBudgetAsync(UpdateCheckStateFile state, SemanticVersion current, CancellationToken cancellationToken)
    {
        using var budget = new CancellationTokenSource(Timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(budget.Token, cancellationToken);
        try
        {
            // Task.Run: leer el archivo de estado y preparar la conexión son síncronos al principio y no deben demorar
            // el comando. WaitAsync corta a los 2 s aunque algo no respete la cancelación (un disco de red colgado).
            return await Task.Run(() => CheckAsync(state, current, linked.Token), linked.Token).WaitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private async Task<UpdateNotice?> CheckAsync(UpdateCheckStateFile state, SemanticVersion current, CancellationToken cancellationToken)
    {
        try
        {
            // La fecha se guarda antes de preguntar: una consulta que falla o se corta también cuenta, así una red caída
            // no se reintenta en cada comando. Si no se puede guardar, no se pregunta, para no consultar cada vez.
            // Una consulta cancelada (Ctrl+C o el tope) antes de empezar no cuenta como la del día.
            var now = timeProvider.GetUtcNow();
            if (!state.IsDue(now, Interval) || cancellationToken.IsCancellationRequested || !state.TryRecordCheck(now))
            {
                return null;
            }

            var latest = await FetchLatestReleaseAsync(cancellationToken);
            return latest is not null && latest.CompareTo(current) > 0 ? new UpdateNotice(current, latest) : null;
        }
        catch (Exception)
        {
            // Red, TLS, JSON, tope de tamaño, disco o cancelación: el aviso es opcional y nunca se muestra un error suyo.
            return null;
        }
    }

    private async Task<SemanticVersion?> FetchLatestReleaseAsync(CancellationToken cancellationToken)
    {
        // Un cliente propio sobre el handler compartido (mismo proxy del sistema): no lleva los headers con las
        // credenciales de FiscalAPI que agrega FiscalApiClientFactory.
        using var client = new HttpClient(handler, disposeHandler: false) { MaxResponseContentBufferSize = MaxResponseBytes };
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", GitHubApiVersion);
        // GitHub rechaza las peticiones sin User-Agent.
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("fiscalapi-cli", currentVersion));

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return null;
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync(body, UpdateCheckJsonContext.Default.GitHubRelease, cancellationToken);

        // Doble defensa: releases/latest no devuelve borradores ni prereleases, pero un tag como v1.0.0-rc.1 publicado
        // sin marcarlo como prerelease tampoco se anuncia.
        return release is { Draft: false, Prerelease: false }
               && SemanticVersion.TryParse(release.TagName, out var version)
               && !version.IsPrerelease
            ? version
            : null;
    }
}
