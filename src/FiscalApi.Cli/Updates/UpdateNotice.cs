namespace FiscalApi.Cli.Updates;

/// <summary>Hay un release más nuevo que el CLI que corre: qué versión es y de dónde se descarga.</summary>
public sealed record UpdateNotice(SemanticVersion Current, SemanticVersion Latest)
{
    /// <summary>
    /// La página del release se arma con la versión ya validada y no con el <c>html_url</c> de la respuesta: así a la
    /// terminal solo llega texto que el CLI controla.
    /// </summary>
    public Uri DownloadUrl => new($"https://github.com/FiscalAPI/fiscalapi-cli/releases/tag/v{Latest}");

    /// <summary>Va a stderr: stdout es de los datos del comando.</summary>
    public async Task WriteToAsync(TextWriter writer)
    {
        await writer.WriteLineAsync();
        await writer.WriteLineAsync($"Hay una versión nueva de fiscalapi: {Latest} (tienes {Current}).");
        await writer.WriteLineAsync($"Descárgala en {DownloadUrl}");
    }
}
