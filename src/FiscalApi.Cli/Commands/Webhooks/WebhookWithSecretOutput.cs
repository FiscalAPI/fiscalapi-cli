using System.Text.Json;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Webhooks;

/// <summary>
/// La salida de <c>create</c> y <c>rotate-secret</c>: el webhook y su secreto. El secreto ya se generó en el servidor y es
/// la única vez que la API lo entrega, así que no depende del modelo de texto: se toma del JSON crudo y se muestra aunque
/// la vista del webhook falle con una respuesta de forma inesperada.
/// </summary>
public static class WebhookWithSecretOutput
{
    public static void Write(ApiCommandContext context, GlobalSettings settings, JsonElement data, bool rotated)
    {
        var secret = SecretIn(data);
        try
        {
            // Con --json imprime el data crudo sin leer el modelo.
            context.WriteData(settings, data, FiscalApiJsonContext.Default.Webhook, WebhookView.WriteDetail);
        }
        finally
        {
            WebhookView.WriteSigningSecret(context.Console, secret, settings.Json, rotated);
        }
    }

    private static string? SecretIn(JsonElement data) =>
        data.ValueKind == JsonValueKind.Object &&
        data.TryGetProperty("secret", out var secret) &&
        secret.ValueKind == JsonValueKind.String
            ? secret.GetString()
            : null;
}
