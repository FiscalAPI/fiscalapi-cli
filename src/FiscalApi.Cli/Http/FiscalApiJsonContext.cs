using System.Text.Json;
using System.Text.Json.Serialization;

namespace FiscalApi.Cli.Http;

/// <summary>
/// Contrato JSON del CLI generado en compilación: la serialización por reflexión no funciona en Native AOT.
/// Los comandos agregan aquí los tipos de sus requests y respuestas (los enums con <c>JsonStringEnumConverter&lt;TEnum&gt;</c>).
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ApiResponse))]
[JsonSerializable(typeof(ApiResponse<PersonFeatures>))]
public sealed partial class FiscalApiJsonContext : JsonSerializerContext;
