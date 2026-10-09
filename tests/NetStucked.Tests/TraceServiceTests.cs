using System.Net;
using System.Net.NetworkInformation;
using NetStucked.Core;
using Xunit;

namespace NetStucked.Tests;

public class TraceServiceTests
{
    [Theory]
    [InlineData(IPStatus.Success)]
    [InlineData(IPStatus.DestinationHostUnreachable)]
    public async Task ConfiguredProbesPerHopIncludesTheFinalHop(IPStatus finalStatus)
    {
        var fake = new FakeProbe { Handler = (address, ttl, _) => Task.FromResult(ttl == 1
            ? new ProbeResult(IPStatus.TtlExpired, IPAddress.Parse("192.0.2.1"), 3)
            : new ProbeResult(finalStatus, address, 8)) };
        await using var service = new TracerouteMonitoringService(fake, new FakeDns());
        await service.StartAsync("127.0.0.1", new() { Continuous = false, MaxHops = 5, ProbesPerHop = 3 });
        await Eventually.Until(() => service.State == SessionState.Stopped);
        Assert.Equal(new[] { 1, 1, 1, 2, 2, 2 }, fake.Ttls.Order().ToArray());
        Assert.All(service.Snapshot(), row => { Assert.Equal(3, row.Sent); Assert.Equal(3, row.Received); });
        Assert.Equal(finalStatus == IPStatus.Success ? "Destination replied" : "No final reply", service.Outcome);
        Assert.Equal(0, fake.TtlOverlap);
    }

    [Fact]
    public async Task ProgressesTtlThroughIntermediateAndTimeoutToVerifiedDestination()
    {
        var fake = new FakeProbe { Handler = (address, ttl, _) => Task.FromResult(ttl switch
        { 1 => new ProbeResult(IPStatus.TtlExpired, IPAddress.Parse("192.0.2.1"), 3), 2 => new(IPStatus.TimedOut, null, null), _ => new(IPStatus.Success, address, 8) }) };
        await using var service = new TracerouteMonitoringService(fake, new FakeDns());
        await service.StartAsync("127.0.0.1", new() { Continuous = false, MaxHops = 8 });
        await Eventually.Until(() => service.State == SessionState.Stopped);
        Assert.Equal(new[] { 1, 2, 3 }, fake.Ttls.ToArray()); Assert.Equal("Destination replied", service.Outcome);
        var rows = service.Snapshot(); Assert.Equal(3, rows.Count); Assert.Equal(1, rows[0].Received); Assert.Null(rows[1].Last); Assert.Null(rows[1].Address);
    }
    [Fact]
    public async Task MissingFinalReplyIsNotHostOfflineAndCyclesStopAtMaxHops()
    {
        var fake = new FakeProbe { Handler = (_, _, _) => Task.FromResult(new ProbeResult(IPStatus.TimedOut, null, null)) };
        await using var service = new TracerouteMonitoringService(fake, new FakeDns());
        await service.StartAsync("127.0.0.1", new() { Continuous = false, MaxHops = 4, ProbesPerHop = 2 });
        await Eventually.Until(() => service.State == SessionState.Stopped);
        Assert.Equal("No final reply", service.Outcome); Assert.Equal(8, fake.Ttls.Count);
        Assert.All(service.Snapshot(), row => { Assert.Equal(2, row.Sent); Assert.Equal(100, row.LossPercent); Assert.Null(row.Average); Assert.Null(row.Jitter); });
    }
    [Fact]
    public async Task AcrossCyclesMissingHopsDoNotInventChangesAndAlternatesLogOnce()
    {
        int cycle = 0;
        var fake = new FakeProbe { Handler = (address, ttl, _) =>
        {
            if (ttl == 1) Interlocked.Increment(ref cycle);
            return Task.FromResult(ttl == 2 ? new ProbeResult(IPStatus.Success, address, 10) :
                cycle == 2 ? new(IPStatus.TimedOut, null, null) : new(IPStatus.TtlExpired, IPAddress.Parse(cycle >= 4 ? "192.0.2.2" : "192.0.2.1"), cycle * 2));
        } };
        await using var service = new TracerouteMonitoringService(fake, new FakeDns());
        await service.StartAsync("127.0.0.1", new() { IntervalMs = 1000, MaxHops = 3 });
        await Eventually.Until(() => service.CompletedCycles >= 5);
        await service.StopAsync();
        var row = service.Snapshot()[0]; Assert.Equal(1, row.RouteChanges); Assert.Single(service.Events(), e => e.Type == "Route");
        Assert.Contains("192.0.2.1 → 192.0.2.2", service.Events().Single(e => e.Type == "Route").Message);
        Assert.Equal(5, row.Sent); Assert.Equal(4, row.Received); Assert.Equal(8d / 3, row.Jitter!.Value, 6);
        Assert.Equal(0, fake.TtlOverlap); Assert.InRange(fake.Maximum, 1, 4);
    }
    [Fact]
    public async Task CancelledPartialCycleRetainsSamplesButDoesNotCommitRouteChange()
    {
        int cycle = 0;
        var fake = new FakeProbe { Handler = async (address, ttl, token) =>
        {
            if (ttl == 1) { int value = Interlocked.Increment(ref cycle); return new(IPStatus.TtlExpired, IPAddress.Parse(value == 1 ? "192.0.2.1" : "192.0.2.2"), 2); }
            if (cycle == 2) await Task.Delay(Timeout.Infinite, token);
            return new(IPStatus.Success, address, 3);
        } };
        await using var service = new TracerouteMonitoringService(fake, new FakeDns());
        await service.StartAsync("127.0.0.1", new() { IntervalMs = 1000, HopConcurrency = 1, AdaptivePolling = false });
        await Eventually.Until(() => cycle == 2 && fake.Active == 1);
        await service.PauseAsync();
        Assert.Equal(1, service.CompletedCycles); Assert.Equal(0, service.Snapshot()[0].RouteChanges); Assert.Equal(0, fake.Active);
        long attempts = service.Snapshot().Sum(r => r.Sent); await Task.Delay(100); Assert.Equal(attempts, service.Snapshot().Sum(r => r.Sent));
        await service.ResumeAsync(); await Eventually.Until(() => service.CompletedCycles >= 2);
        await service.StopAsync(); Assert.Equal(1, service.Snapshot()[0].RouteChanges); Assert.Equal(0, fake.Active);
    }
    [Fact]
    public async Task ResolutionFailureEndsInErrorAndLogsActualError()
    {
        await using var service = new TracerouteMonitoringService(new FakeProbe(), new FakeDns(true));
        await service.StartAsync("missing.invalid", new()); await Eventually.Until(() => service.State == SessionState.Error);
        Assert.Empty(service.Snapshot()); Assert.Contains(service.Events(), e => e.Type == "Error");
    }
    [Fact]
    public async Task Ipv6TraceIsExplicitlyRejected()
    {
        var service = new TracerouteMonitoringService(new FakeProbe(), new FakeDns());
        await Assert.ThrowsAsync<ArgumentException>(() => service.StartAsync("::1", new()));
    }

    [Fact]
    public async Task CancellationDuringReverseDnsRetainsCompletedIcmpSample()
    {
        var dns = new BlockingReverseDns();
        await using var service = new TracerouteMonitoringService(new FakeProbe(), dns);
        await service.StartAsync("127.0.0.1", new() { ReverseDns = true });
        await dns.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Eventually.Until(() => service.CompletedCycles == 1);
        await service.PauseAsync();
        Assert.Equal(1, Assert.Single(service.Snapshot()).Sent);
        Assert.Equal(1, service.CompletedCycles);
        Assert.Equal(SessionState.Paused, service.State);
    }

    [Fact]
    public async Task TerminalUnreachableRetainsActualResponderAndStopsProgression()
    {
        var responder = IPAddress.Parse("192.0.2.1");
        var fake = new FakeProbe { Handler = (_, _, _) => Task.FromResult(new ProbeResult(IPStatus.DestinationHostUnreachable, responder, 4)) };
        await using var service = new TracerouteMonitoringService(fake, new FakeDns());
        await service.StartAsync("127.0.0.1", new() { Continuous = false });
        await Eventually.Until(() => service.State == SessionState.Stopped);
        var row = Assert.Single(service.Snapshot());
        Assert.Equal("192.0.2.1", row.Address); Assert.Equal("DestinationHostUnreachable", row.Status);
        Assert.Equal(1, row.Received); Assert.Equal(4, row.Last); Assert.Single(fake.Ttls);
        Assert.Equal("No final reply", service.Outcome);
    }

    [Fact]
    public async Task NoOpLifecycleCallsDoNotInventTraceEvents()
    {
        await using var service = new TracerouteMonitoringService(new FakeProbe(), new FakeDns());
        await service.PauseAsync(); await service.ResumeAsync(); await service.StopAsync();
        Assert.Equal(SessionState.Idle, service.State); Assert.Empty(service.Events());
    }
}
