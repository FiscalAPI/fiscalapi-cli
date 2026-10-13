namespace FiscalApi.Cli.Input;

/// <summary>
/// Stdin redirigido (<c>printf '%s\n%s\n' "$KEY" "$TENANT" | fiscalapi login</c>): un valor por línea y sin
/// prompts, porque no hay una persona que los lea.
/// </summary>
public sealed class RedirectedSecretReader(TextReader input) : ISecretReader
{
    public async Task<string?> ReadAsync(string prompt, CancellationToken cancellationToken) =>
        await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
}
