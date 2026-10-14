namespace FiscalApi.Cli.Input;

/// <summary>
/// La pregunta va a stderr (stdout es para datos) y la respuesta es una línea de la terminal. Solo "s", "si", "sí", "y" o
/// "yes" confirman; cualquier otra cosa, una línea vacía o el fin de la entrada, no.
/// </summary>
/// <param name="prompts">Donde se escribe la pregunta.</param>
/// <param name="input">La terminal (<c>Console.In</c>) o, en las pruebas, un texto.</param>
/// <param name="isInteractive">Si hay una persona en la terminal (<c>!Console.IsInputRedirected</c>).</param>
public sealed class ConsoleConfirmationPrompt(TextWriter prompts, TextReader input, bool isInteractive) : IConfirmationPrompt
{
    private static readonly HashSet<string> Yes = new(StringComparer.OrdinalIgnoreCase) { "s", "si", "sí", "y", "yes" };

    public bool IsInteractive => isInteractive;

    public async Task<bool> ConfirmAsync(string question, CancellationToken cancellationToken)
    {
        await prompts.WriteAsync($"{question} [s/N] ").ConfigureAwait(false);
        // Console.In.ReadLine bloquea sin token: se espera aparte para que Ctrl+C (la cancelación del token que hace
        // Program) termine con 130 aunque la persona no haya respondido.
        var answer = await Task.Run(input.ReadLine, CancellationToken.None).WaitAsync(cancellationToken).ConfigureAwait(false);
        return answer is not null && Yes.Contains(answer.Trim());
    }
}
