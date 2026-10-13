namespace FiscalApi.Cli;

/// <summary>
/// Ambiente de la API contra el que trabaja una invocación (D-25). Se resuelve una sola vez por invocación
/// y viaja como valor inmutable, sin estáticos mutables.
/// </summary>
public sealed record ApiEnvironment
{
    public static readonly Uri TestBaseAddress = new("https://test.fiscalapi.com/");
    public static readonly Uri LiveBaseAddress = new("https://live.fiscalapi.com/");

    private ApiEnvironment(Uri baseAddress) => BaseAddress = baseAddress;

    /// <summary>Siempre termina en <c>/</c>, para que las rutas relativas conserven su prefijo.</summary>
    public Uri BaseAddress { get; }

    /// <summary>Precedencia: <c>--api-base</c> gana sobre <c>--live</c>, y este sobre test.</summary>
    public static ApiEnvironment Resolve(Uri? apiBase, bool live) =>
        new(WithTrailingSlash(apiBase ?? (live ? LiveBaseAddress : TestBaseAddress)));

    // Sin la "/" final, Uri resuelve "api/v4/..." contra el padre y descarta el último segmento
    // (https://host/fiscalapi + api/v4 = https://host/api/v4).
    private static Uri WithTrailingSlash(Uri baseAddress) =>
        baseAddress.AbsolutePath.EndsWith('/')
            ? baseAddress
            : new UriBuilder(baseAddress) { Path = baseAddress.AbsolutePath + "/" }.Uri;
}
