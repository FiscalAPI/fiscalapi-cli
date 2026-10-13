namespace FiscalApi.Cli.Tests.Credentials;

/// <summary>
/// Prueba contra el almacén real del SO. En Windows (Credential Manager) y macOS (llavero de sesión) siempre corre.
/// En Linux solo corre si <see cref="LinuxOptInVariable"/> vale <c>1</c>, porque exige un Secret Service en la
/// sesión de D-Bus, que no hay en WSL, en un servidor ni en un contenedor sin preparar (el CI lo levanta).
/// </summary>
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
