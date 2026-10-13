using FiscalApi.Cli.Http;
using FiscalApi.Cli.Output;

namespace FiscalApi.Cli.Webhooks;

/// <summary>Cómo se ven los eventos de webhook en la terminal.</summary>
public static class WebhookEventView
{
    private static readonly string[] Headers = ["ID", "TIPO", "CREADO (UTC)", "RECURSO", "PERSONA"];

    public static void WriteList(TextWriter output, PagedList<WebhookEventEnvelope> page) =>
        PagedListView.Write(output, page, Headers, envelope =>
            [envelope.Id, envelope.Type, envelope.CreatedAt, envelope.Data?.RelatedObject?.Id, envelope.PersonId]);

    public static void WriteResend(TextWriter output, WebhookEventResend resend) =>
        output.WriteLine($"Reenvío creado: entrega {resend.DeliveryId} al webhook {resend.WebhookId}.");
}
