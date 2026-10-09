using System.Net;
using System.Net.NetworkInformation;
using NetStucked.Core;
using Xunit;

namespace NetStucked.Tests;

public sealed class UiRevisionCoreTests
{
    [Fact]
    public async Task PingTransitionTimestampsComeOnlyFromCompletedReplies()
    {
        ProbeResult reply = new(IPStatus.Success, IPAddress.Loopback, 1);
        var fake = new FakeProbe { Handler = (_, _, _) => Task.FromResult(reply) };
        await using var service = new MultiTargetPingService(fake, new FakeDns());
        await service.StartAsync([new("127.0.0.1", "")], new() { IntervalMs = 250 });
        await Eventually.Until(() => service.Snapshot()[0].Sent >= 2); await service.PauseAsync();
        var up = service.Snapshot()[0];
        Assert.NotNull(up.LastSuccess); Assert.NotNull(up.ReachableSince); Assert.Null(up.UnreachableSince);
        Assert.True(up.LastSuccess >= up.ReachableSince); Assert.Equal("ICMP reply from 127.0.0.1", up.ResultError);
        reply = new(IPStatus.TimedOut, null, null);
        await service.ResumeAsync(); await Eventually.Until(() => service.Snapshot()[0].Lost >= 2); await service.PauseAsync();
        var down = service.Snapshot()[0];
        Assert.Equal(up.LastSuccess, down.LastSuccess); Assert.NotNull(down.UnreachableSince); Assert.Null(down.ReachableSince);
        Assert.Equal("Unreachable", service.History("127.0.0.1")[0].Status);
        reply = new(IPStatus.Success, IPAddress.Loopback, 2);
        await service.ResumeAsync(); await Eventually.Until(() => service.Snapshot()[0].Received > up.Received); await service.StopAsync();
        var recovered = service.Snapshot()[0];
        Assert.True(recovered.ReachableSince > up.ReachableSince); Assert.Null(recovered.UnreachableSince);
        Assert.Equal("Up", service.History("127.0.0.1")[0].Status);
    }
    [Fact]
    public async Task DnsFailureDoesNotFabricatePingOrReachabilityTimestamps()
    {
        await using var service = new MultiTargetPingService(new FakeProbe(), new FakeDns(true));
        await service.StartAsync([new("failed.invalid", "")], new());
        await Eventually.Until(() => service.Snapshot()[0].Error is not null); await service.StopAsync();
        var row = service.Snapshot()[0];
        Assert.Null(row.LastPing); Assert.Null(row.LastSuccess); Assert.Null(row.ReachableSince); Assert.Null(row.UnreachableSince);
        Assert.StartsWith("DNS:", row.ResultError); Assert.Equal(0, row.Sent);
    }
    [Theory]
    [InlineData(249, 500)] [InlineData(250, 499)]
    public void ProbeSettingMinimumsAreEnforced(int interval, int timeout)
    {
        Assert.Throws<ArgumentException>(() => new ProbeSettings { IntervalMs = interval, TimeoutMs = timeout }.Validate());
        Assert.Throws<ArgumentException>(() => new TraceSettings { IntervalMs = interval, TimeoutMs = timeout }.Validate());
        new ProbeSettings { IntervalMs = 250, TimeoutMs = 500 }.Validate();
        new TraceSettings { IntervalMs = 250, TimeoutMs = 500 }.Validate();
    }
}
