using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Listen;

/// <summary>Las rutas de la escucha de CLI sobre <see cref="FiscalApiClient"/>.</summary>
public sealed class ListenerApi(FiscalApiClient client, int pollWaitSeconds)
{
    private const string WebhooksPath = "api/v4/webhooks";

    /// <summary>Abre una sesión nueva, que reemplaza la anterior de la persona, y responde el secreto.</summary>
    public async Task<CliListenerSession> OpenAsync(IReadOnlyList<string> enabledEvents, CancellationToken cancellationToken) =>
        await client.PostAsync(
            $"{WebhooksPath}/cli-listener",
            new OpenCliListenerRequest(enabledEvents),
            FiscalApiJsonContext.Default.OpenCliListenerRequest,
            FiscalApiJsonContext.Default.ApiResponseCliListenerSession,
            cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException("La API abrió la escucha de CLI sin responder la sesión.");

    /// <summary>
    /// El long-polling: renueva la sesión y espera hasta <c>pollWaitSeconds</c> a que haya entregas. Cada entrega se
    /// responde una sola vez y espera su acuse.
    /// </summary>
    public async Task<IReadOnlyList<CliDelivery>> PollAsync(CliListenerSession session, CancellationToken cancellationToken) =>
        await client.GetAsync(
            $"{ListenerPath(session)}/cli-deliveries?sessionId={Uri.EscapeDataString(session.SessionId)}&waitSeconds={pollWaitSeconds}",
            FiscalApiJsonContext.Default.ApiResponseIReadOnlyListCliDelivery,
            cancellationToken).ConfigureAwait(false)
        ?? [];

    /// <summary>
    /// El acuse de una entrega con lo que respondió el receptor. La API responde 409 si la entrega ya no lo espera (llegó
    /// tarde o es de otra sesión).
    /// </summary>
    public Task AckAsync(CliListenerSession session, string deliveryId, CliDeliveryAck ack, CancellationToken cancellationToken) =>
        client.PostAsync(
            $"{ListenerPath(session)}/cli-deliveries/{Uri.EscapeDataString(deliveryId)}/ack",
            ack,
            FiscalApiJsonContext.Default.CliDeliveryAck,
            FiscalApiJsonContext.Default.ApiResponseBoolean,
            cancellationToken);

    private static string ListenerPath(CliListenerSession session) => $"{WebhooksPath}/{Uri.EscapeDataString(session.Id)}";
}
