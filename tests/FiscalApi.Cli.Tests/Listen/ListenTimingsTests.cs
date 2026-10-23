using FiscalApi.Cli.Listen;

namespace FiscalApi.Cli.Tests.Listen;

public sealed class ListenTimingsTests
{
    [Fact]
    public void Defaults_follow_the_listen_contract()
    {
        var timings = ListenTimings.Default;

        Assert.Equal(25, timings.PollWaitSeconds);
        Assert.Equal(TimeSpan.FromSeconds(35), timings.ApiTimeout);
        Assert.Equal(TimeSpan.FromSeconds(15), timings.ForwardTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), timings.Reconnect.MaxDelay);
        Assert.Equal(TimeSpan.FromMinutes(2), timings.DeliveryLease);
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
