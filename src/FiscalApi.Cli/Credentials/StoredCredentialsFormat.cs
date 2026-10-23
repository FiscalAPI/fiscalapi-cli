using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Credentials;

internal static class StoredCredentialsFormat
{
    public static byte[] Encode(ApiCredentials credentials) => JsonSerializer.SerializeToUtf8Bytes(
        new StoredCredentials { ApiKey = credentials.ApiKey, TenantKey = credentials.TenantKey },
        StoredCredentialsJsonContext.Default.StoredCredentials);

    public static ApiCredentials Decode(ReadOnlySpan<byte> secret, string host)
    {
        try
        {
            var stored = JsonSerializer.Deserialize(secret, StoredCredentialsJsonContext.Default.StoredCredentials);
            return ApiCredentials.Create(stored?.ApiKey, stored?.TenantKey);
        }
        catch (Exception exception) when (exception is JsonException or UsageException)
        {
            throw CredentialStoreException.Unreadable(host);
        }
    }
}

internal sealed class StoredCredentials
{
    public string? ApiKey { get; init; }

    public string? TenantKey { get; init; }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(StoredCredentials))]
internal sealed partial class StoredCredentialsJsonContext : JsonSerializerContext;
