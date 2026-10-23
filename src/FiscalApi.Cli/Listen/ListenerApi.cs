using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Listen;

public sealed class ListenerApi(FiscalApiClient client, int pollWaitSeconds)
{
    private const string WebhooksPath = "api/v4/webhooks";

    public async Task<CliListenerSession> OpenAsync(IReadOnlyList<string> enabledEvents, CancellationToken cancellationToken) =>
        await client.PostAsync(
            $"{WebhooksPath}/cli-listener",
            new OpenCliListenerRequest(enabledEvents),
            FiscalApiJsonContext.Default.OpenCliListenerRequest,
            FiscalApiJsonContext.Default.ApiResponseCliListenerSession,
            cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException("The API opened the listen session but returned no session data.");

    public async Task<IReadOnlyList<CliDelivery>> PollAsync(CliListenerSession session, CancellationToken cancellationToken) =>
        await client.GetAsync(
            $"{ListenerPath(session)}/cli-deliveries?sessionId={Uri.EscapeDataString(session.SessionId)}&waitSeconds={pollWaitSeconds}",
            FiscalApiJsonContext.Default.ApiResponseIReadOnlyListCliDelivery,
            cancellationToken).ConfigureAwait(false)
        ?? [];

    public Task AckAsync(CliListenerSession session, string deliveryId, CliDeliveryAck ack, CancellationToken cancellationToken) =>
        client.PostAsync(
            $"{ListenerPath(session)}/cli-deliveries/{Uri.EscapeDataString(deliveryId)}/ack",
            ack,
            FiscalApiJsonContext.Default.CliDeliveryAck,
            FiscalApiJsonContext.Default.ApiResponseBoolean,
            cancellationToken);

    private static string ListenerPath(CliListenerSession session) => $"{WebhooksPath}/{Uri.EscapeDataString(session.Id)}";
}
