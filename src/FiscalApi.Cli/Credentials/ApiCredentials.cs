using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Credentials;

public sealed record ApiCredentials
{
    public const int MaxLength = 200;

    private ApiCredentials(string apiKey, string tenantKey)
    {
        ApiKey = apiKey;
        TenantKey = tenantKey;
    }

    public string ApiKey { get; }

    public string TenantKey { get; }

    public static ApiCredentials Create(string? apiKey, string? tenantKey) => new(ValidApiKey(apiKey), ValidTenantKey(tenantKey));

    public static string ValidApiKey(string? apiKey) => Validated(apiKey, "The API key");

    private static string ValidTenantKey(string? tenantKey) => Validated(tenantKey, "The tenant key");

    public override string ToString() => "ApiCredentials { ApiKey = ***, TenantKey = *** }";

    private static string Validated(string? value, string name)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > MaxLength || !trimmed.All(IsVisibleAscii))
        {
            throw new UsageException($"{name} is empty, too long, or has invalid characters.");
        }

        return trimmed;
    }

    private static bool IsVisibleAscii(char character) => character is > ' ' and <= '~';
}
