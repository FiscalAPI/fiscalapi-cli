namespace FiscalApi.Cli.Errors;

public abstract class CliException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public abstract int ExitCode { get; }

    public virtual bool IsTransient => false;

    public virtual void WriteTo(TextWriter error) => error.WriteLine($"Error: {Message}");
}
