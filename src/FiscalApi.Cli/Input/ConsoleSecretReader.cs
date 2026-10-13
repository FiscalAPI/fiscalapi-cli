using System.Text;

namespace FiscalApi.Cli.Input;

/// <summary>
/// Una persona en la terminal: muestra el prompt en stderr y lee las teclas sin eco, así el secreto no queda en
/// pantalla ni en el scrollback.
/// </summary>
public sealed class ConsoleSecretReader(TextWriter prompts) : ISecretReader
{
    // Console.ReadKey bloquea sin token: se espera a que haya tecla en intervalos cortos para que Ctrl+C (que
    // Program convierte en la cancelación del token) termine la lectura con el código 130.
    private static readonly TimeSpan KeyPollInterval = TimeSpan.FromMilliseconds(50);

    public async Task<string?> ReadAsync(string prompt, CancellationToken cancellationToken)
    {
        await prompts.WriteAsync(prompt);
        var value = new StringBuilder();
        while (true)
        {
            while (!Console.KeyAvailable)
            {
                await Task.Delay(KeyPollInterval, cancellationToken);
            }

            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                await prompts.WriteLineAsync();
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
}
