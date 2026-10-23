namespace FiscalApi.Cli.Input;

public sealed class ConsoleConfirmationPrompt(TextWriter prompts, TextReader input, bool isInteractive) : IConfirmationPrompt
{
    private static readonly HashSet<string> Yes = new(StringComparer.OrdinalIgnoreCase) { "y", "yes" };

    public bool IsInteractive => isInteractive;

    public async Task<bool> ConfirmAsync(string question, CancellationToken cancellationToken)
    {
        await prompts.WriteAsync($"{question} [y/N] ").ConfigureAwait(false);
        var answer = await Task.Run(input.ReadLine, CancellationToken.None).WaitAsync(cancellationToken).ConfigureAwait(false);
        return answer is not null && Yes.Contains(answer.Trim());
    }
}
