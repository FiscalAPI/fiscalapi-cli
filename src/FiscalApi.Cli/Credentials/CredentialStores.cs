using FiscalApi.Cli.Credentials.Linux;
using FiscalApi.Cli.Credentials.MacOS;
using FiscalApi.Cli.Credentials.Windows;

namespace FiscalApi.Cli.Credentials;

/// <summary>Elige el almacén de credenciales del SO en que corre el binario.</summary>
public static class CredentialStores
{
    /// <summary>Nombre de servicio de las entradas del CLI; las pruebas usan otro para no tocarlas.</summary>
    public const string ProductionService = "fiscalapi-cli";

    /// <exception cref="PlatformNotSupportedException">Un SO para el que no se publica el CLI.</exception>
    public static ICredentialStore ForCurrentOs(string service) =>
        OperatingSystem.IsWindows() ? new WindowsCredentialStore(service)
        : OperatingSystem.IsMacOS() ? new KeychainCredentialStore(service)
        : OperatingSystem.IsLinux() ? new LibSecretCredentialStore(service)
        : throw new PlatformNotSupportedException("El CLI de FiscalAPI solo corre en Windows, macOS y Linux.");
}
