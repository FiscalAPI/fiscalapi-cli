using FiscalApi.Cli.Http;
using FiscalApi.Cli.Output;

namespace FiscalApi.Cli.Webhooks;

public static class WebhookEventView
{
    private static readonly string[] Headers = ["ID", "TYPE", "CREATED (UTC)", "RESOURCE", "PERSON"];

    public static void WriteList(TextWriter output, PagedList<WebhookEventEnvelope> page) =>
        PagedListView.Write(output, page, Headers, envelope =>
            [envelope.Id, envelope.Type, envelope.CreatedAt, envelope.Data?.RelatedObject?.Id, envelope.PersonId]);

    public static void WriteResend(TextWriter output, WebhookEventResend resend) =>
        output.WriteLine($"Resend created: delivery {resend.DeliveryId} to webhook {resend.WebhookId}.");

    public static void WriteTrigger(TextWriter output, WebhookEventTrigger trigger) =>
        output.WriteLine($"Event triggered: {trigger.EventId} to webhook {trigger.WebhookId}.");
}
