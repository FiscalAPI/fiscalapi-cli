using System.Text.Json;
using System.Text.Json.Serialization;
using FiscalApi.Cli.Listen;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Http;

/// <summary>
/// Contrato JSON del CLI generado en compilación: la serialización por reflexión no funciona en Native AOT.
/// Los comandos agregan aquí los tipos de sus requests y respuestas (los enums con <c>JsonStringEnumConverter&lt;TEnum&gt;</c>).
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ApiResponse<PersonFeatures>))]
[JsonSerializable(typeof(OpenCliListenerRequest))]
[JsonSerializable(typeof(ApiResponse<CliListenerSession>))]
[JsonSerializable(typeof(ApiResponse<IReadOnlyList<CliDelivery>>))]
[JsonSerializable(typeof(CliDeliveryAck))]
[JsonSerializable(typeof(ApiResponse<bool>))]
[JsonSerializable(typeof(ListenSecretRecord))]
[JsonSerializable(typeof(ListenEventRecord))]
// El data se lee como JsonElement para imprimirlo sin cambios; los modelos solo alimentan la salida de texto.
[JsonSerializable(typeof(ApiResponse<JsonElement>))]
[JsonSerializable(typeof(Webhook))]
[JsonSerializable(typeof(PagedList<Webhook>))]
[JsonSerializable(typeof(CreateWebhookRequest))]
[JsonSerializable(typeof(UpdateWebhookRequest))]
[JsonSerializable(typeof(PagedList<WebhookEventEnvelope>))]
[JsonSerializable(typeof(ResendWebhookEventRequest))]
[JsonSerializable(typeof(WebhookEventResend))]
[JsonSerializable(typeof(TriggerWebhookEventRequest))]
[JsonSerializable(typeof(WebhookEventTrigger))]
public sealed partial class FiscalApiJsonContext : JsonSerializerContext;
