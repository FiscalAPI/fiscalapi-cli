using System.Reflection;

namespace FiscalApi.Cli;

public static class CliVersion
{
    /// <summary>
    /// Sale de <see cref="AssemblyInformationalVersionAttribute"/> y no de <c>Assembly.Location</c>,
    /// que viene vacío en un binario single-file o AOT.
    /// </summary>
    public static string Current { get; } =
        typeof(CliVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? throw new InvalidOperationException("El ensamblado no trae AssemblyInformationalVersion; el SDK lo genera desde <Version>.");
}
