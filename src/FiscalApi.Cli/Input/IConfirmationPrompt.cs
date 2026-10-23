namespace FiscalApi.Cli.Input;

public interface IConfirmationPrompt
{
    bool IsInteractive { get; }

    Task<bool> ConfirmAsync(string question, CancellationToken cancellationToken);
}
