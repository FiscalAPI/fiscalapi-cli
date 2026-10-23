namespace FiscalApi.Cli.Webhooks;

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
