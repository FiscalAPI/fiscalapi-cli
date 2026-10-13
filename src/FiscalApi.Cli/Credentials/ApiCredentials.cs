using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Credentials;

/// <summary>
/// La API key y la tenant key con las que el CLI llama a la API (headers <c>X-API-KEY</c> y <c>X-TENANT-KEY</c>).
/// Solo se crean validadas, sin importar si vienen del teclado, de stdin, de las variables de entorno o del
/// almacén del SO: así un valor inválido nunca llega a un header ni a un mensaje de error.
/// </summary>
public sealed record ApiCredentials
{
    // Holgado frente a lo que emite fapi-back (API key de 51 caracteres, tenant key GUID) y muy por debajo del
    // máximo de un secreto en Credential Manager (CRED_MAX_CREDENTIAL_BLOB_SIZE, 2560 bytes).
    private const int MaxLength = 256;

    private ApiCredentials(string apiKey, string tenantKey)
    {
        ApiKey = apiKey;
        TenantKey = tenantKey;
    }

    public string ApiKey { get; }

    public string TenantKey { get; }

    /// <summary>
    /// Quita los espacios de los extremos y exige ASCII visible. Un salto de línea en un header haría que
    /// <see cref="HttpClient"/> lanzara una excepción que repite el valor; por eso el mensaje nunca lo incluye.
    /// </summary>
    /// <exception cref="UsageException">Un valor vacío, demasiado largo o con caracteres no válidos.</exception>
    public static ApiCredentials Create(string? apiKey, string? tenantKey) => new(
        Validated(apiKey, "La API key"),
        Validated(tenantKey, "La tenant key"));

    /// <summary>Sin los valores: un registro o un volcado de excepción nunca los muestra.</summary>
    public override string ToString() => "ApiCredentials { ApiKey = ***, TenantKey = *** }";

    private static string Validated(string? value, string name)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > MaxLength || !trimmed.All(IsVisibleAscii))
        {
            throw new UsageException($"{name} está vacía, es demasiado larga o tiene caracteres no válidos.");
        }

        return trimmed;
    }

    private static bool IsVisibleAscii(char character) => character is > ' ' and <= '~';
}
