namespace FiscalApi.Cli.Input;

/// <summary>Una pregunta de sí o no antes de una acción que no se deshace (p. ej. <c>webhooks delete</c>).</summary>
public interface IConfirmationPrompt
{
    /// <summary>Si hay una persona en la terminal que pueda responder; con stdin redirigido, no.</summary>
    bool IsInteractive { get; }

    /// <returns><c>true</c> solo si la persona respondió que sí.</returns>
    Task<bool> ConfirmAsync(string question, CancellationToken cancellationToken);
}
