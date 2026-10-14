using FiscalApi.Cli.Http;
using FiscalApi.Cli.Output;

namespace FiscalApi.Cli.Webhooks;

/// <summary>
/// Cómo se ven los webhooks en la terminal. Ninguna vista escribe el secreto: solo
/// <see cref="WriteSigningSecret"/>, una vez, en el alta y en la rotación.
/// </summary>
public static class WebhookView
{
    private static readonly string[] Headers = ["ID", "TIPO", "ESTADO", "URL", "EVENTOS", "DESCRIPCIÓN"];

    public static void WriteList(TextWriter output, PagedList<Webhook> page) =>
        PagedListView.Write(output, page, Headers, webhook =>
            [webhook.Id, webhook.Kind, webhook.Status, webhook.Url, string.Join(',', webhook.EnabledEvents), webhook.Description]);

    public static void WriteDetail(TextWriter output, Webhook webhook) =>
        LabeledValues.Write(output,
        [
            ("ID", webhook.Id),
            ("Tipo", webhook.Kind),
            ("Estado", webhook.Status),
            ("Motivo", webhook.DisabledReason),
            ("Deshabilitado", webhook.DisabledAt),
            ("Fallando desde", webhook.FailingSince),
            ("URL", webhook.Url),
            ("Descripción", webhook.Description),
            ("Eventos", string.Join(", ", webhook.EnabledEvents)),
            // Sin personas, la API le entrega los eventos de todas.
            ("Personas", webhook.PersonIds is { Count: > 0 } personIds ? string.Join(", ", personIds) : "todas"),
            ("Persona de la escucha", webhook.ListenerPersonId),
            ("Sesión vence", webhook.SessionExpiresAt),
            ("Creado", webhook.CreatedAt),
            ("Actualizado", webhook.UpdatedAt)
        ]);

    /// <summary>
    /// El secreto va a stdout, con el webhook (con <c>--json</c> ya va dentro del <c>data</c>); el aviso de guardarlo
    /// (el mismo texto del dashboard) es para la persona y va a stderr, también con <c>--json</c>. Durante una
    /// suplantación la API no lo responde y no hay nada que guardar.
    /// </summary>
    /// <param name="secret">El de la respuesta, o <c>null</c> si no vino.</param>
    /// <param name="rotated">Si viene de <c>rotate-secret</c>: el anterior sigue firmando 24 horas.</param>
    public static void WriteSigningSecret(CliConsole console, string? secret, bool json, bool rotated)
    {
        if (secret is null)
        {
            return;
        }

        if (!json)
        {
            console.Out.WriteLine();
            console.Out.WriteLine($"Secreto de firma: {secret}");
        }

        console.Error.WriteLine("Guarda este secreto de firma. Lo usarás para verificar que los eventos vienen de FiscalAPI.");
        if (rotated)
        {
            console.Error.WriteLine(
                "El secreto anterior seguirá firmando durante 24 horas para que actualices tu receptor sin perder eventos.");
        }
    }
}
