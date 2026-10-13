using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Credentials;

/// <summary>
/// El SO no tiene un almacén de credenciales utilizable: Linux sin Secret Service, WSL, un servidor o una sesión
/// sin llavero. No hay archivo plano de respaldo (D-25); la salida es usar las variables de entorno, por eso es un
/// error de uso (código 2) y no de la API.
/// </summary>
public sealed class CredentialStoreUnavailableException(string detail)
    : CliException("No hay un almacén de credenciales disponible en este sistema. Define FISCALAPI_API_KEY y FISCALAPI_TENANT_KEY.")
{
    /// <summary>Lo que respondió el SO (un código o el error de D-Bus), para diagnosticar; nunca trae el secreto.</summary>
    public string Detail { get; } = detail;

    public override int ExitCode => ExitCodes.Usage;

    public override void WriteTo(TextWriter error)
    {
        base.WriteTo(error);
        error.WriteLine($"  Detalle: {Detail}");
    }
}
