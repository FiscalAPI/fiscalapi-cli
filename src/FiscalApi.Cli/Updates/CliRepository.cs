namespace FiscalApi.Cli.Updates;

internal static class CliRepository
{
    private const string Slug = "FiscalAPI/fiscalapi-cli";

    public static readonly Uri LatestReleaseApi = new($"https://api.github.com/repos/{Slug}/releases/latest");

    public static Uri ReleasePage(SemanticVersion version) => new($"https://github.com/{Slug}/releases/tag/v{version}");
}
