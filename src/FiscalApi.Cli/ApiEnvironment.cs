namespace FiscalApi.Cli;

/// <summary>
/// Ambiente de la API contra el que trabaja una invocación (D-25). Se resuelve una sola vez por invocación
/// y viaja como valor inmutable, sin estáticos mutables.
/// </summary>
public sealed record ApiEnvironment(Uri BaseAddress)
{
    public static readonly Uri TestBaseAddress = new("https://test.fiscalapi.com");
    public static readonly Uri LiveBaseAddress = new("https://live.fiscalapi.com");

    /// <summary>Precedencia: <c>--api-base</c> gana sobre <c>--live</c>, y este sobre test.</summary>
    public static ApiEnvironment Resolve(Uri? apiBase, bool live) =>
        new(apiBase ?? (live ? LiveBaseAddress : TestBaseAddress));
}
