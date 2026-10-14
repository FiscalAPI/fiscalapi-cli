using System.Text.Json;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Webhooks;

/// <summary>
/// Las rutas de los webhooks (<c>/api/v4/webhooks</c>) sobre <see cref="FiscalApiClient"/>. Cada una responde
/// el <c>data</c> tal como lo escribió la API: el comando lo imprime así con <c>--json</c> o lo lee para la tabla.
/// </summary>
public sealed class WebhooksApi(FiscalApiClient client)
{
    private const string WebhooksPath = "api/v4/webhooks";

    /// <summary><c>GET /webhooks</c>: una página de webhooks, en orden de alta y sin su secreto.</summary>
    public async Task<JsonElement> ListAsync(PageRequest page, CancellationToken cancellationToken) =>
        ApiData.Required(await client.GetAsync(
            WebhooksPath + QueryString.From(page.QueryParameters),
            FiscalApiJsonContext.Default.ApiResponseJsonElement,
            cancellationToken).ConfigureAwait(false));

    /// <summary><c>GET /webhooks/{id}</c>: el webhook sin su secreto.</summary>
    public async Task<JsonElement> RetrieveAsync(string id, CancellationToken cancellationToken) =>
        ApiData.Required(await client.GetAsync(
            WebhookPath(id), FiscalApiJsonContext.Default.ApiResponseJsonElement, cancellationToken).ConfigureAwait(false));

    /// <summary><c>POST /webhooks</c>: 201 con el webhook y su secreto.</summary>
    public async Task<JsonElement> CreateAsync(CreateWebhookRequest request, CancellationToken cancellationToken) =>
        ApiData.Required(await client.PostAsync(
            WebhooksPath,
            request,
            FiscalApiJsonContext.Default.CreateWebhookRequest,
            FiscalApiJsonContext.Default.ApiResponseJsonElement,
            cancellationToken).ConfigureAwait(false));

    /// <summary><c>PUT /webhooks/{id}</c>: reemplazo completo, con el mismo id en la ruta y en el cuerpo.</summary>
    public async Task<JsonElement> ReplaceAsync(UpdateWebhookRequest request, CancellationToken cancellationToken) =>
        ApiData.Required(await client.PutAsync(
            WebhookPath(request.Id),
            request,
            FiscalApiJsonContext.Default.UpdateWebhookRequest,
            FiscalApiJsonContext.Default.ApiResponseJsonElement,
            cancellationToken).ConfigureAwait(false));

    /// <summary><c>DELETE /webhooks/{id}</c>: da de baja el webhook y cierra sus entregas pendientes.</summary>
    public async Task<JsonElement> DeleteAsync(string id, CancellationToken cancellationToken) =>
        ApiData.Required(await client.DeleteAsync(
            WebhookPath(id), FiscalApiJsonContext.Default.ApiResponseJsonElement, cancellationToken).ConfigureAwait(false));

    /// <summary><c>POST /webhooks/{id}/rotate-secret</c>, sin cuerpo: el webhook con el secreto nuevo.</summary>
    public async Task<JsonElement> RotateSecretAsync(string id, CancellationToken cancellationToken) =>
        ApiData.Required(await client.PostAsync(
            $"{WebhookPath(id)}/rotate-secret",
            FiscalApiJsonContext.Default.ApiResponseJsonElement,
            cancellationToken).ConfigureAwait(false));

    private static string WebhookPath(string id) => $"{WebhooksPath}/{Uri.EscapeDataString(id)}";
}
