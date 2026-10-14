using FiscalApi.Cli.Listen;

namespace FiscalApi.Cli.Tests.Listen;

/// <summary>Los plazos de <c>listen</c> son los del contrato con la API.</summary>
public sealed class ListenTimingsTests
{
    [Fact]
    public void Defaults_follow_the_listen_contract()
    {
        var timings = ListenTimings.Default;

        // El long-polling espera hasta 25 s; el cliente de la API le da 35 s.
        Assert.Equal(25, timings.PollWaitSeconds);
        Assert.Equal(TimeSpan.FromSeconds(35), timings.ApiTimeout);
        Assert.Equal(TimeSpan.FromSeconds(15), timings.ForwardTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), timings.Reconnect.MaxDelay);
        // WebhookConsts.DeliveryLease de fapi-back: después, la API cierra la entrega sin acuse.
        Assert.Equal(TimeSpan.FromMinutes(2), timings.DeliveryLease);
        // Al salir con Ctrl+C, el único intento de acusar lo que ya tenía resultado.
        Assert.Equal(TimeSpan.FromSeconds(2), timings.ShutdownAckTimeout);
    }

    [Fact]
    public void Reconnection_waits_never_exceed_30_seconds()
    {
        for (var attempt = 1; attempt <= 64; attempt++)
        {
            Assert.InRange(ListenTimings.Default.Reconnect.Delay(attempt), TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }
    }
}
