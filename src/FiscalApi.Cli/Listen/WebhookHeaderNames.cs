namespace FiscalApi.Cli.Listen;

public static class WebhookHeaderNames
{
    public const string Id = "webhook-id";

    public const string Timestamp = "webhook-timestamp";

    public const string Signature = "webhook-signature";

    public static bool IsSigned(string name) =>
        name.Equals(Id, StringComparison.OrdinalIgnoreCase) ||
        name.Equals(Timestamp, StringComparison.OrdinalIgnoreCase) ||
        name.Equals(Signature, StringComparison.OrdinalIgnoreCase);
}
