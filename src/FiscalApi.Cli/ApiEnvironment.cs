namespace FiscalApi.Cli;

public sealed record ApiEnvironment
{
    public static readonly Uri TestBaseAddress = new("https://test.fiscalapi.com/");
    public static readonly Uri LiveBaseAddress = new("https://live.fiscalapi.com/");

    private ApiEnvironment(Uri baseAddress) => BaseAddress = baseAddress;

    public Uri BaseAddress { get; }

    public string Host => BaseAddress.Authority;

    public static ApiEnvironment Resolve(Uri? apiBase, bool live) =>
        new(WithTrailingSlash(apiBase ?? (live ? LiveBaseAddress : TestBaseAddress)));

    private static Uri WithTrailingSlash(Uri baseAddress) =>
        baseAddress.AbsolutePath.EndsWith('/')
            ? baseAddress
            : new UriBuilder(baseAddress) { Path = baseAddress.AbsolutePath + "/" }.Uri;
}
