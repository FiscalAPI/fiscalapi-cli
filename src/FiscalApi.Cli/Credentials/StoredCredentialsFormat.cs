using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Credentials;

/// <summary>
/// El secreto de una entrada del almacén: un JSON UTF-8 con las dos llaves (<c>{"apiKey":…,"tenantKey":…}</c>).
/// Van juntas en una sola entrada porque solo sirven en pareja; así una lectura nunca mezcla las de dos tenants.
/// </summary>
internal static class StoredCredentialsFormat
{
    /// <summary>Quien la recibe la borra con <see cref="CryptographicOperations.ZeroMemory"/> al terminar.</summary>
    public static byte[] Encode(ApiCredentials credentials) => JsonSerializer.SerializeToUtf8Bytes(
        new StoredCredentials { ApiKey = credentials.ApiKey, TenantKey = credentials.TenantKey },
        StoredCredentialsJsonContext.Default.StoredCredentials);

    /// <exception cref="CredentialStoreException">La entrada no es de este CLI o se modificó a mano.</exception>
    public static ApiCredentials Decode(ReadOnlySpan<byte> secret, string host)
    {
        try
        {
            var stored = JsonSerializer.Deserialize(secret, StoredCredentialsJsonContext.Default.StoredCredentials);
            return ApiCredentials.Create(stored?.ApiKey, stored?.TenantKey);
        }
        catch (Exception exception) when (exception is JsonException or UsageException)
        {
            throw new CredentialStoreException(
                $"La entrada guardada para {host} no se puede leer. Ejecuta `fiscalapi logout` y luego `fiscalapi login`.");
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
