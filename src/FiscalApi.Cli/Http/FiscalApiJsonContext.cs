using System.Text.Json;
using System.Text.Json.Serialization;
using FiscalApi.Cli.Listen;

namespace FiscalApi.Cli.Http;

/// <summary>
/// Contrato JSON del CLI generado en compilación: la serialización por reflexión no funciona en Native AOT.
/// Los comandos agregan aquí los tipos de sus requests y respuestas (los enums con <c>JsonStringEnumConverter&lt;TEnum&gt;</c>).
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ApiResponse))]
[JsonSerializable(typeof(ApiResponse<PersonFeatures>))]
[JsonSerializable(typeof(OpenCliListenerRequest))]
[JsonSerializable(typeof(ApiResponse<CliListenerSession>))]
[JsonSerializable(typeof(ApiResponse<IReadOnlyList<CliDelivery>>))]
[JsonSerializable(typeof(CliDeliveryAck))]
[JsonSerializable(typeof(ApiResponse<bool>))]
[JsonSerializable(typeof(ListenSecretRecord))]
[JsonSerializable(typeof(ListenEventRecord))]
public sealed partial class FiscalApiJsonContext : JsonSerializerContext;
