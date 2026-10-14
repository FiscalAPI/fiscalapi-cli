namespace FiscalApi.Cli;

/// <summary>
/// Códigos de salida del contrato común del CLI. Los scripts y el CI de quien integra dependen de ellos.
/// </summary>
public static class ExitCodes
{
    public const int Success = 0;

    /// <summary>Error de la API o de la red.</summary>
    public const int Failure = 1;

    /// <summary>Error de uso: argumentos inválidos (System.CommandLine) o <see cref="Errors.UsageException"/>.</summary>
    public const int Usage = 2;

    /// <summary>Ctrl+C. 128 + SIGINT (2), la convención de los shells POSIX.</summary>
    public const int Cancelled = 130;
}
