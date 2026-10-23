using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FiscalApi.Cli.Updates;

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

    public const string OptOutVariable = "FISCALAPI_NO_UPDATE_CHECK";
    private const string CiVariable = "CI";

    private const string GitHubApiVersion = "2022-11-28";

    private const int MaxResponseBytes = 1024 * 1024;

    public Task<UpdateNotice?> Start(GlobalSettings settings, CancellationToken cancellationToken)
    {
        if (IsTurnedOff(settings) || stateFile is null || !SemanticVersion.TryParse(currentVersion, out var current))
        {
            return Task.FromResult<UpdateNotice?>(null);
        }

        return CheckWithinBudgetAsync(stateFile, current, cancellationToken);
    }

    private bool IsTurnedOff(GlobalSettings settings) =>
        settings.Json || isErrorRedirected || IsOn(CiVariable) || IsOn(OptOutVariable);

    private bool IsOn(string variable) =>
        environment(variable) is { Length: > 0 } value && value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase);

    private async Task<UpdateNotice?> CheckWithinBudgetAsync(UpdateCheckStateFile state, SemanticVersion current, CancellationToken cancellationToken)
    {
        using var budget = new CancellationTokenSource(Timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(budget.Token, cancellationToken);
        try
        {
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
            var now = timeProvider.GetUtcNow();
            if (!state.IsDue(now, Interval) || cancellationToken.IsCancellationRequested || !state.TryRecordCheck(now))
            {
                return null;
            }

            var latest = await FetchLatestReleaseAsync(cancellationToken);
            return latest is not null && latest.CompareTo(current) > 0 ? new UpdateNotice(current, latest) : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    private async Task<SemanticVersion?> FetchLatestReleaseAsync(CancellationToken cancellationToken)
    {
        using var client = new HttpClient(handler, disposeHandler: false) { MaxResponseContentBufferSize = MaxResponseBytes };
        using var request = new HttpRequestMessage(HttpMethod.Get, CliRepository.LatestReleaseApi);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", GitHubApiVersion);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("fiscalapi-cli", currentVersion));

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return null;
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync(body, UpdateCheckJsonContext.Default.GitHubRelease, cancellationToken);

        return release is { Draft: false, Prerelease: false }
               && SemanticVersion.TryParse(release.TagName, out var version)
               && !version.IsPrerelease
            ? version
            : null;
    }
}
