using System.Text;

namespace FiscalApi.Cli.Input;

/// <summary>
/// Una persona en la terminal: muestra el prompt en stderr y lee las teclas sin eco, así el secreto no queda en
/// pantalla ni en el scrollback.
/// </summary>
/// <param name="prompts">Donde se escriben el prompt y el salto de línea final; nunca una tecla.</param>
/// <param name="tryReadKey">
/// La siguiente tecla sin eco, o <c>null</c> si todavía no hay ninguna. En producción,
/// <c>Console.KeyAvailable</c> y <c>Console.ReadKey(intercept: true)</c>; en las pruebas, teclas simuladas.
/// </param>
public sealed class ConsoleSecretReader(TextWriter prompts, Func<ConsoleKeyInfo?> tryReadKey) : ISecretReader
{
    // Console.ReadKey bloquea sin token: se espera a que haya tecla en intervalos cortos para que Ctrl+C (que
    // Program convierte en la cancelación del token) termine la lectura con el código 130.
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
