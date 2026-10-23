namespace FiscalApi.Cli.Input;

public interface ISecretReader
{
    Task<string?> ReadAsync(string prompt, CancellationToken cancellationToken);
}
