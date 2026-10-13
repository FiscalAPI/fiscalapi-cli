using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Credentials;

/// <summary>
/// El almacén del SO existe pero falló de una forma inesperada (un código que no es "no existe" ni "no hay
/// almacén"), o la entrada guardada no se pudo leer. El mensaje lleva el código del SO, nunca el secreto.
/// </summary>
public sealed class CredentialStoreException(string message) : CliException(message)
{
    public override int ExitCode => ExitCodes.Failure;
}
