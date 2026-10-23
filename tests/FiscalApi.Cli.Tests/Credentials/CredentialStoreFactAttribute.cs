namespace FiscalApi.Cli.Tests.Credentials;

public sealed class CredentialStoreFactAttribute : FactAttribute
{
    public const string LinuxOptInVariable = "FISCALAPI_CLI_SECRET_SERVICE_TESTS";

    public CredentialStoreFactAttribute()
    {
        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable(LinuxOptInVariable) != "1")
        {
            Skip = $"Requiere un Secret Service de prueba; define {LinuxOptInVariable}=1 dentro de una sesión de D-Bus con gnome-keyring.";
        }
    }
}
