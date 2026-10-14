namespace FiscalApi.Cli.Input;

/// <summary>
/// Stdin redirigido (<c>printf '%s\n%s\n' "$KEY" "$TENANT" | fiscalapi login</c>): un valor por línea y sin
/// prompts, porque no hay una persona que los lea.
/// </summary>
public sealed class RedirectedSecretReader(TextReader input) : ISecretReader
{
    private const char ByteOrderMark = '\uFEFF';

    private bool _firstLine = true;

    public async Task<string?> ReadAsync(string prompt, CancellationToken cancellationToken)
    {
        var line = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        // Un archivo guardado como "UTF-8 con BOM" y redirigido trae U+FEFF al inicio, y Trim no lo quita.
        if (_firstLine && line is not null)
        {
            line = line.TrimStart(ByteOrderMark);
        }

        _firstLine = false;
        return line;
    }
}
