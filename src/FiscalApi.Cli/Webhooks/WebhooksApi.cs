using System.Text.Json;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Webhooks;

public sealed class WebhooksApi(FiscalApiClient client)
{
    private const string WebhooksPath = "api/v4/webhooks";

    public async Task<JsonElement> ListAsync(PageRequest page, CancellationToken cancellationToken) =>
        ApiData.Required(await client.GetAsync(
            WebhooksPath + QueryString.From(page.QueryParameters),
            FiscalApiJsonContext.Default.ApiResponseJsonElement,
            cancellationToken).ConfigureAwait(false));

    public async Task<JsonElement> RetrieveAsync(string id, CancellationToken cancellationToken) =>
        ApiData.Required(await client.GetAsync(
            WebhookPath(id), FiscalApiJsonContext.Default.ApiResponseJsonElement, cancellationToken).ConfigureAwait(false));

    public async Task<JsonElement> CreateAsync(CreateWebhookRequest request, CancellationToken cancellationToken) =>
        ApiData.Required(await client.PostAsync(
            WebhooksPath,
            request,
            FiscalApiJsonContext.Default.CreateWebhookRequest,
            FiscalApiJsonContext.Default.ApiResponseJsonElement,
            cancellationToken).ConfigureAwait(false));

    public async Task<JsonElement> ReplaceAsync(UpdateWebhookRequest request, CancellationToken cancellationToken) =>
        ApiData.Required(await client.PutAsync(
            WebhookPath(request.Id),
            request,
            FiscalApiJsonContext.Default.UpdateWebhookRequest,
            FiscalApiJsonContext.Default.ApiResponseJsonElement,
            cancellationToken).ConfigureAwait(false));

    public async Task<JsonElement> DeleteAsync(string id, CancellationToken cancellationToken) =>
        ApiData.Required(await client.DeleteAsync(
            WebhookPath(id), FiscalApiJsonContext.Default.ApiResponseJsonElement, cancellationToken).ConfigureAwait(false));

    public async Task<JsonElement> RotateSecretAsync(string id, CancellationToken cancellationToken) =>
        ApiData.Required(await client.PostAsync(
            $"{WebhookPath(id)}/rotate-secret",
            FiscalApiJsonContext.Default.ApiResponseJsonElement,
            cancellationToken).ConfigureAwait(false));

    private static string WebhookPath(string id) => $"{WebhooksPath}/{Uri.EscapeDataString(id)}";
}
