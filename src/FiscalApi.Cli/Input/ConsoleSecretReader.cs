using System.Text;

namespace FiscalApi.Cli.Input;

public sealed class ConsoleSecretReader(TextWriter prompts, Func<ConsoleKeyInfo?> tryReadKey) : ISecretReader
{
    private static readonly TimeSpan KeyPollInterval = TimeSpan.FromMilliseconds(50);

    public async Task<string?> ReadAsync(string prompt, CancellationToken cancellationToken)
    {
        await prompts.WriteAsync(prompt).ConfigureAwait(false);
        var value = new StringBuilder();
        while (true)
        {
            var key = await NextKeyAsync(cancellationToken).ConfigureAwait(false);
            if (key.Key == ConsoleKey.Enter)
            {
                await prompts.WriteLineAsync().ConfigureAwait(false);
                return value.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                value.Length = Math.Max(0, value.Length - 1);
            }
            else if (!char.IsControl(key.KeyChar))
            {
                value.Append(key.KeyChar);
            }
        }
    }

    private async Task<ConsoleKeyInfo> NextKeyAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (tryReadKey() is { } key)
            {
                return key;
            }

            await Task.Delay(KeyPollInterval, cancellationToken).ConfigureAwait(false);
        }
    }
}
