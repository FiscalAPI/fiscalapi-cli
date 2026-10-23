using FiscalApi.Cli.Http;
using FiscalApi.Cli.Output;

namespace FiscalApi.Cli.Webhooks;

public static class WebhookView
{
    private static readonly string[] Headers = ["ID", "KIND", "STATUS", "URL", "EVENTS", "DESCRIPTION"];

    public static void WriteList(TextWriter output, PagedList<Webhook> page) =>
        PagedListView.Write(output, page, Headers, webhook =>
            [webhook.Id, webhook.Kind, webhook.Status, webhook.Url, string.Join(',', webhook.EnabledEvents), webhook.Description]);

    public static void WriteDetail(TextWriter output, Webhook webhook) =>
        LabeledValues.Write(output,
        [
            ("ID", webhook.Id),
            ("Kind", webhook.Kind),
            ("Status", webhook.Status),
            ("Disabled reason", webhook.DisabledReason),
            ("Disabled at", webhook.DisabledAt),
            ("Failing since", webhook.FailingSince),
            ("URL", webhook.Url),
            ("Description", webhook.Description),
            ("Events", string.Join(", ", webhook.EnabledEvents)),
            ("Persons", webhook.PersonIds is { Count: > 0 } personIds ? string.Join(", ", personIds) : "all"),
            ("Listen session person", webhook.ListenerPersonId),
            ("Session expires", webhook.SessionExpiresAt),
            ("Created", webhook.CreatedAt),
            ("Updated", webhook.UpdatedAt)
        ]);

    public static void WriteSigningSecret(CliConsole console, string? secret, bool json, bool rotated)
    {
        if (secret is null)
        {
            return;
        }

        if (!json)
        {
            console.Out.WriteLine();
            console.Out.WriteLine($"Signing secret: {secret}");
        }

        console.Error.WriteLine("Save this signing secret. Use it to verify that events come from FiscalAPI.");
        if (rotated)
        {
            console.Error.WriteLine("The old signing secret also signs events for 24 hours, so you can update your server.");
        }
    }
}
