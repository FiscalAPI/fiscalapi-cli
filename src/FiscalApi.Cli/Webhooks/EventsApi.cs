using System.Globalization;
using System.Text.Json;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Webhooks;

public sealed class EventsApi(FiscalApiClient client)
{
    private const string EventsPath = "api/v4/events";

    private const string UtcInstantFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    public async Task<JsonElement> ListAsync(WebhookEventsQuery query, CancellationToken cancellationToken)
    {
        KeyValuePair<string, string?>[] filters =
        [
            KeyValuePair.Create("type", query.Type),
            KeyValuePair.Create("relatedObjectId", query.RelatedObjectId),
            KeyValuePair.Create("from", Utc(query.From)),
            KeyValuePair.Create("to", Utc(query.To))
        ];
        return ApiData.Required(await client.GetAsync(
            EventsPath + QueryString.From(filters.Concat(query.Page.QueryParameters)),
            FiscalApiJsonContext.Default.ApiResponseJsonElement,
            cancellationToken).ConfigureAwait(false));
    }

    public async Task<JsonElement> RetrieveAsync(string id, CancellationToken cancellationToken) =>
        ApiData.Required(await client.GetAsync(
            EventPath(id), FiscalApiJsonContext.Default.ApiResponseJsonElement, cancellationToken).ConfigureAwait(false));

    public async Task<JsonElement> ResendAsync(string id, string? webhookId, CancellationToken cancellationToken)
    {
        var path = $"{EventPath(id)}/resend";
        var data = webhookId is null
            ? await client.PostAsync(path, FiscalApiJsonContext.Default.ApiResponseJsonElement, cancellationToken)
                .ConfigureAwait(false)
            : await client.PostAsync(
                path,
                new ResendWebhookEventRequest(webhookId),
                FiscalApiJsonContext.Default.ResendWebhookEventRequest,
                FiscalApiJsonContext.Default.ApiResponseJsonElement,
                cancellationToken).ConfigureAwait(false);
        return ApiData.Required(data);
    }

    public async Task<JsonElement> TriggerAsync(TriggerWebhookEventRequest request, CancellationToken cancellationToken) =>
        ApiData.Required(await client.PostAsync(
            $"{EventsPath}/trigger",
            request,
            FiscalApiJsonContext.Default.TriggerWebhookEventRequest,
            FiscalApiJsonContext.Default.ApiResponseJsonElement,
            cancellationToken).ConfigureAwait(false));

    private static string EventPath(string id) => $"{EventsPath}/{Uri.EscapeDataString(id)}";

    private static string? Utc(DateTimeOffset? instant) =>
        instant?.UtcDateTime.ToString(UtcInstantFormat, CultureInfo.InvariantCulture);
}
