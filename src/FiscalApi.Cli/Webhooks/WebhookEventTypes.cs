namespace FiscalApi.Cli.Webhooks;

/// <summary>
/// Los tipos de evento que se suscriben, con los nombres de <c>WebhookEventTypes</c> de los SDK. Son la
/// ayuda de autocompletado de <c>trigger</c>, no una validación: el catálogo puede crecer y quien valida es la API.
/// <c>webhook.test</c> no está porque no se suscribe ni se dispara: solo lo manda la prueba de un webhook.
/// </summary>
public static class WebhookEventTypes
{
    public const string InvoiceCancellationRequested = "invoice.cancellation_requested";

    public const string InvoiceCanceled = "invoice.canceled";

    public const string InvoiceCancellationRejected = "invoice.cancellation_rejected";

    public const string DownloadRequestCompleted = "download_request.completed";

    public const string DownloadRequestAbandoned = "download_request.abandoned";

    public const string DownloadRuleSuspended = "download_rule.suspended";

    public static IReadOnlyList<string> Subscribable { get; } =
    [
        InvoiceCancellationRequested,
        InvoiceCanceled,
        InvoiceCancellationRejected,
        DownloadRequestCompleted,
        DownloadRequestAbandoned,
        DownloadRuleSuspended
    ];
}
