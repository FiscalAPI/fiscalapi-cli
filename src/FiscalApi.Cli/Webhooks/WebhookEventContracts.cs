namespace FiscalApi.Cli.Webhooks;

// Los contratos de los eventos de webhook de fapi-back (WH-013, D-28), con sus mismos nombres.

/// <summary>
/// Lo que la tabla de <c>events list</c> muestra del sobre de D-04 (<c>{id, type, createdAt, personId,
/// data:{relatedObject, status, previousStatus}}</c>). <c>createdAt</c> es texto: la API lo entrega en UTC y se muestra tal
/// cual.
/// </summary>
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

/// <summary>El recurso del evento: la factura, la solicitud de descarga, la regla o el webhook.</summary>
public sealed class WebhookRelatedObject
{
    public string? Id { get; init; }
}

/// <summary>Los filtros de <c>GET /api/v4/events</c>; los instantes van en UTC.</summary>
public sealed record WebhookEventsQuery(
    string? Type,
    string? RelatedObjectId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    Http.PageRequest Page);

/// <summary>El cuerpo de <c>POST /api/v4/events/{id}/resend</c> cuando se indica el webhook (<c>ResendWebhookEventCommand</c>).</summary>
public sealed record ResendWebhookEventRequest(string WebhookId);

/// <summary>El reenvío aceptado (<c>WebhookEventResendDto</c>): con estos ids se sigue en el log de entregas.</summary>
public sealed record WebhookEventResend(string DeliveryId, string WebhookId);
