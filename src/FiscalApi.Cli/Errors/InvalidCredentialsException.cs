namespace FiscalApi.Cli.Errors;

/// <summary>
/// La API rechazó (401) la API key o la tenant key que se quería guardar con <c>fiscalapi login</c>. Es un
/// error de la API (código 1). La respuesta original queda como <see cref="Exception.InnerException"/>.
/// </summary>
public sealed class InvalidCredentialsException(string host, ApiException rejection)
    : CliException($"Las credenciales no son válidas para {host}.", rejection)
{
    public override int ExitCode => ExitCodes.Failure;
}
