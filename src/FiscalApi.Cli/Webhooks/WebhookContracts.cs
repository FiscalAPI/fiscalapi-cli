using System.Text.Json.Serialization;

namespace FiscalApi.Cli.Webhooks;

public static class WebhookKinds
{
    public const string CliListener = "cliListener";
}

public static class WebhookStatuses
{
    public const string Enabled = "enabled";
    public const string Disabled = "disabled";
}

public sealed class Webhook
{
    public required string Id { get; init; }

    public string? Kind { get; init; }

    public string? Url { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<string> EnabledEvents { get; init; } = [];

    public IReadOnlyList<string>? PersonIds { get; init; }

    public string? Status { get; init; }

    public string? DisabledReason { get; init; }

    public string? DisabledAt { get; init; }

    public string? FailingSince { get; init; }

    public string? ListenerPersonId { get; init; }

    public string? SessionExpiresAt { get; init; }

    public string? CreatedAt { get; init; }

    public string? UpdatedAt { get; init; }

    [JsonIgnore]
    public bool IsCliListener => Kind == WebhookKinds.CliListener;
}

public sealed record CreateWebhookRequest(
    string Url,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description,
    IReadOnlyList<string> EnabledEvents,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? PersonIds);

public sealed record UpdateWebhookRequest(
    string Id,
    string? Url,
    string Description,
    IReadOnlyList<string> EnabledEvents,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? PersonIds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Status);

public sealed record WebhookChanges(
    string? Url,
    string? Description,
    IReadOnlyList<string>? EnabledEvents,
    IReadOnlyList<string>? PersonIds,
    string? Status)
{
    public UpdateWebhookRequest ApplyTo(Webhook current) => new(
        current.Id,
        Url ?? current.Url,
        Description ?? current.Description ?? "",
        EnabledEvents ?? current.EnabledEvents,
        PersonIds ?? current.PersonIds,
        Status);
}
