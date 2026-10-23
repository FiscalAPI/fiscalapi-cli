using FiscalApi.Cli.Credentials.Linux;
using FiscalApi.Cli.Credentials.MacOS;
using FiscalApi.Cli.Credentials.Windows;

namespace FiscalApi.Cli.Credentials;

public static class CredentialStores
{
    public const string ProductionService = "fiscalapi-cli";

    public static ICredentialStore ForCurrentOs(string service) =>
        OperatingSystem.IsWindows() ? new WindowsCredentialStore(service)
        : OperatingSystem.IsMacOS() ? new KeychainCredentialStore(service)
        : OperatingSystem.IsLinux() ? new LibSecretCredentialStore(service)
        : throw new PlatformNotSupportedException("The FiscalAPI CLI runs only on Windows, macOS, and Linux.");
}
