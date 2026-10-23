using System.Text.Json;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Webhooks;

namespace FiscalApi.Cli.Commands.Webhooks;

public static class WebhookWithSecretOutput
{
    public static void Write(ApiCommandContext context, GlobalSettings settings, JsonElement data, bool rotated)
    {
        var secret = SecretIn(data);
        try
        {
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
