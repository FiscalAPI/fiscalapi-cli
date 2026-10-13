using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Credentials;

/// <summary>
/// El almacén del SO existe, pero no se pudo usar: está bloqueado y no se puede pedir la contraseña, no respondió,
/// falló con un código inesperado, o la entrada guardada no se puede leer. Es distinto de "no hay almacén"
/// (<see cref="CredentialStoreUnavailableException"/>): aquí las variables de entorno no son la salida. Código 1.
/// </summary>
public sealed class CredentialStoreException : CliException
{
    private CredentialStoreException(string message, string? detail)
        : base(message) => Detail = detail;

    /// <summary>Lo que respondió el SO (un código o el error de D-Bus), para diagnosticar; nunca trae el secreto.</summary>
    public string? Detail { get; }

    public override int ExitCode => ExitCodes.Failure;

    public static CredentialStoreException Locked(string detail) =>
        new("El almacén de credenciales del sistema existe, pero está bloqueado o no respondió.", detail);

    public static CredentialStoreException Failed(string operation, string detail) =>
        new($"El almacén de credenciales del sistema falló al {operation} la entrada.", detail);

    public static CredentialStoreException Unreadable(string host) =>
        new($"La entrada guardada para {host} no se puede leer. Ejecuta `fiscalapi logout` y luego `fiscalapi login`.", detail: null);

    /// <summary>El mismo error, precedido de lo que se estaba haciendo (p. ej. "No se pudieron borrar…").</summary>
    public CredentialStoreException During(string context) => new($"{context} {Message}", Detail);

    public override void WriteTo(TextWriter error)
    {
        base.WriteTo(error);
        if (Detail is not null)
        {
            error.WriteLine($"  Detalle: {Detail}");
        }
    }
}
