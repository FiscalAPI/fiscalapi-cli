namespace FiscalApi.Cli.Updates;

public sealed record UpdateNotice(SemanticVersion Current, SemanticVersion Latest)
{
    public Uri DownloadUrl => CliRepository.ReleasePage(Latest);

    public async Task WriteToAsync(TextWriter writer)
    {
        await writer.WriteLineAsync();
        await writer.WriteLineAsync($"fiscalapi {Latest} is available (you have {Current}).");
        await writer.WriteLineAsync($"Download it from {DownloadUrl}");
    }
}
