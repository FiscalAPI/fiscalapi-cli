using System.Text.Json.Serialization;

namespace FiscalApi.Cli.Webhooks;

public sealed class WebhookEventEnvelope
{
    public string? Id { get; init; }

    public string? Type { get; init; }

    public string? CreatedAt { get; init; }

    public string? PersonId { get; init; }

    public WebhookEventData? Data { get; init; }
}

public sealed class WebhookEventData
{
    public WebhookRelatedObject? RelatedObject { get; init; }
}

public sealed class WebhookRelatedObject
{
    public string? Id { get; init; }
}

public sealed record WebhookEventsQuery(
    string? Type,
    string? RelatedObjectId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    Http.PageRequest Page);

public sealed record ResendWebhookEventRequest(string WebhookId);

public sealed record WebhookEventResend(string DeliveryId, string WebhookId);

public sealed record TriggerWebhookEventRequest(
    string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RelatedObjectId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PersonId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WebhookId);

public sealed record WebhookEventTrigger(string EventId, string WebhookId);
