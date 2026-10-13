using System.Globalization;
using System.Text.Json;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Webhooks;

/// <summary>
/// Las rutas de los eventos de webhook (<c>/api/v4/events</c>, D-28) sobre <see cref="FiscalApiClient"/>. Cada una responde
/// el <c>data</c> tal como lo escribió la API.
/// </summary>
public sealed class EventsApi(FiscalApiClient client)
{
    private const string EventsPath = "api/v4/events";

    // El formato de los instantes que espera fapi-back (ISO-8601 en UTC); sin fracción si no la hay.
    private const string UtcInstantFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    /// <summary><c>GET /events</c>: una página de sobres de los últimos 30 días, del más nuevo al más viejo.</summary>
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

    /// <summary><c>GET /events/{id}</c>: el sobre exacto que se firmó y se entregó.</summary>
    public async Task<JsonElement> RetrieveAsync(string id, CancellationToken cancellationToken) =>
        ApiData.Required(await client.GetAsync(
            EventPath(id), FiscalApiJsonContext.Default.ApiResponseJsonElement, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// <c>POST /events/{id}/resend</c>: 202 con la entrega nueva. Sin <paramref name="webhookId"/> va sin cuerpo y la
    /// API lo entrega a la escucha de CLI de la persona (D-28), que tiene que tener un <c>listen</c> abierto.
    /// </summary>
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

    private static string EventPath(string id) => $"{EventsPath}/{Uri.EscapeDataString(id)}";

    private static string? Utc(DateTimeOffset? instant) =>
        instant?.UtcDateTime.ToString(UtcInstantFormat, CultureInfo.InvariantCulture);
}
