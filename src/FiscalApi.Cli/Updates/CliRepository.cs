namespace FiscalApi.Cli.Updates;

/// <summary>
/// El repositorio público del CLI en GitHub, en un solo lugar: de él salen la consulta del último release y la liga de
/// descarga del aviso. Si el repo cambia de nombre, solo cambia <see cref="Slug"/>.
/// </summary>
internal static class CliRepository
{
    private const string Slug = "FiscalAPI/fiscalapi-cli";

    /// <summary>
    /// <c>releases/latest</c> ya descarta borradores y prereleases
    /// (docs.github.com/rest/releases/releases#get-the-latest-release).
    /// </summary>
    public static readonly Uri LatestReleaseApi = new($"https://api.github.com/repos/{Slug}/releases/latest");

    public static Uri ReleasePage(SemanticVersion version) => new($"https://github.com/{Slug}/releases/tag/v{version}");
}
