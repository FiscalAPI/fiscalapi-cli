namespace FiscalApi.Cli.Listen;

/// <summary>Los headers de Standard Webhooks (D-02) que la API firma y el CLI reenvía sin cambios.</summary>
public static class WebhookHeaderNames
{
    /// <summary>El id del evento.</summary>
    public const string Id = "webhook-id";

    public const string Timestamp = "webhook-timestamp";

    public const string Signature = "webhook-signature";

    public static bool IsSigned(string name) =>
        name.Equals(Id, StringComparison.OrdinalIgnoreCase) ||
        name.Equals(Timestamp, StringComparison.OrdinalIgnoreCase) ||
        name.Equals(Signature, StringComparison.OrdinalIgnoreCase);
}
