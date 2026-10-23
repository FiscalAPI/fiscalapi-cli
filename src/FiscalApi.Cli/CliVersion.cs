using System.Reflection;

namespace FiscalApi.Cli;

public static class CliVersion
{
    public static string Current { get; } =
        typeof(CliVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? throw new InvalidOperationException("The assembly has no AssemblyInformationalVersion. The SDK generates it from <Version>.");
}
