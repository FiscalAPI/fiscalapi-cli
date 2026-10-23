namespace FiscalApi.Cli.Input;

public sealed class RedirectedSecretReader(TextReader input) : ISecretReader
{
    private const char ByteOrderMark = '\uFEFF';

    private bool _firstLine = true;

    public async Task<string?> ReadAsync(string prompt, CancellationToken cancellationToken)
    {
        var line = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (_firstLine && line is not null)
        {
            line = line.TrimStart(ByteOrderMark);
        }

        _firstLine = false;
        return line;
    }
}
