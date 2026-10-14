using System.Text.Json.Serialization;

namespace FiscalApi.Cli.Webhooks;

// Los contratos de los webhooks de la API, con sus mismos nombres. Los enums de la API (kind,
// status, disabledReason) se leen como texto: el CLI solo los muestra, y un valor nuevo no debe romper la lectura.

/// <summary>Los valores de <c>kind</c> (<c>WebhookKind</c> en camelCase) que el CLI distingue.</summary>
public static class WebhookKinds
{
    /// <summary>La escucha de <c>fiscalapi listen</c>: sin URL y no se edita con <c>PUT</c>.</summary>
    public const string CliListener = "cliListener";
}

/// <summary>Los valores de <c>status</c> (<c>WebhookStatus</c> en camelCase).</summary>
public static class WebhookStatuses
{
    public const string Enabled = "enabled";
    public const string Disabled = "disabled";
}

/// <summary>
/// Un webhook como lo responde la API (<c>WebhookDto</c>), solo para la salida de texto. Las fechas se guardan como texto:
/// la API las escribe en la zona de la petición y el CLI las muestra tal cual. El secreto del alta y de la rotación no
/// pasa por aquí: se lee del JSON crudo para no depender de que la respuesta encaje en el modelo.
/// </summary>
public sealed class Webhook
{
    public required string Id { get; init; }

    public string? Kind { get; init; }

    /// <summary><c>null</c> en una escucha de CLI.</summary>
    public string? Url { get; init; }

    public string? Description { get; init; }

    /// <summary>Los tipos suscritos, o solo <c>*</c> para todos.</summary>
    public IReadOnlyList<string> EnabledEvents { get; init; } = [];

    /// <summary>Las personas cuyos eventos recibe; <c>null</c> recibe los de todas.</summary>
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

/// <summary>El cuerpo del alta (<c>CreateWebhookCommand</c>); lo que la persona no dio no se escribe.</summary>
public sealed record CreateWebhookRequest(
    string Url,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description,
    IReadOnlyList<string> EnabledEvents,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? PersonIds);

/// <summary>
/// El cuerpo del reemplazo (<c>UpdateWebhookCommand</c>): <c>PUT</c> escribe todo tal como llega. Sin
/// <see cref="PersonIds"/> recibe los eventos de todas las personas, y sin <see cref="Status"/> conserva el estado.
/// </summary>
public sealed record UpdateWebhookRequest(
    string Id,
    string? Url,
    string Description,
    IReadOnlyList<string> EnabledEvents,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? PersonIds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Status);

/// <summary>
/// Lo que la persona pidió cambiar con <c>webhooks update</c>; <c>null</c> es "no lo cambies". Como el <c>PUT</c> reemplaza
/// todo, los cambios se aplican sobre el webhook que se acaba de leer para no borrar lo demás.
/// </summary>
/// <param name="PersonIds">
/// Una lista vacía (<c>--all-persons</c>) recibe los eventos de todas las personas, igual que en la API.
/// </param>
/// <param name="Status">Un valor de <see cref="WebhookStatuses"/>.</param>
public sealed record WebhookChanges(
    string? Url,
    string? Description,
    IReadOnlyList<string>? EnabledEvents,
    IReadOnlyList<string>? PersonIds,
    string? Status)
{
    /// <summary>
    /// El reemplazo completo: lo pedido y, de lo demás, lo que <paramref name="current"/> ya tiene. El estado solo viaja si
    /// se pidió, así el <c>PUT</c> no deshace un cambio de estado que otra persona hizo entre la lectura y la escritura.
    /// </summary>
    public UpdateWebhookRequest ApplyTo(Webhook current) => new(
        current.Id,
        Url ?? current.Url,
        Description ?? current.Description ?? "",
        EnabledEvents ?? current.EnabledEvents,
        PersonIds ?? current.PersonIds,
        Status);
}
