using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using NetStucked.Core;
using Xunit;
using Xunit.Abstractions;

namespace NetStucked.Tests;

public class PerformanceRegressionTests(ITestOutputHelper output)
{
    [Fact]
    public async Task PingCadenceDoesNotAddProbeDurationToTheInterval()
    {
        var starts = new ConcurrentQueue<double>();
        var clock = Stopwatch.StartNew();
        var probe = new FakeProbe { Handler = async (address, _, token) =>
        {
            starts.Enqueue(clock.Elapsed.TotalMilliseconds);
            await Task.Delay(150, token);
            return new(IPStatus.Success, address, 150);
        } };
        await using var service = new MultiTargetPingService(probe, new FakeDns());
        await service.StartAsync([new("127.0.0.1", "adapter only")], new() { IntervalMs = 250 });
        await Eventually.Until(() => starts.Count >= 6);
        await service.StopAsync();
        double[] times = starts.ToArray();
        double mean = times.Zip(times.Skip(1), (a, b) => b - a).Average();
        output.WriteLine($"Ping start-to-start mean={mean:0.00}ms; requested=250ms; adapter delay=150ms.");
        Assert.InRange(mean, 200, 365);
        Assert.Equal(0, probe.Overlap);
    }

    [Fact]
    public async Task TraceDoesNotWaitForTimeoutsOneHopAtATime()
    {
        var probe = new FakeProbe { Handler = async (address, ttl, token) =>
        {
            await Task.Delay(120, token);
            return ttl >= 9 ? new(IPStatus.Success, address, 120) : new(IPStatus.TimedOut, null, null);
        } };
        await using var service = new TracerouteMonitoringService(probe, new FakeDns());
        var clock = Stopwatch.StartNew();
        await service.StartAsync("127.0.0.1", new() { Continuous = false, MaxHops = 12 });
        await Eventually.Until(() => service.State == SessionState.Stopped);
        output.WriteLine($"Nine-hop timeout adapter: completion={clock.Elapsed.TotalMilliseconds:0.00}ms; first destination={service.Diagnostics.FirstDestinationMs:0.00}ms; max concurrent={probe.Maximum}.");
        Assert.True(clock.ElapsedMilliseconds < 800, $"Simulated nine-hop trace took {clock.ElapsedMilliseconds}ms.");
        Assert.Equal("Destination replied", service.Outcome);
        Assert.Equal(9, service.Snapshot().Count);
        Assert.InRange(probe.Maximum, 2, 4);
        Assert.Equal(0, probe.TtlOverlap);
    }

    [Fact]
    public async Task BlockedHostnameLookupsDoNotStarveLiteralIpTargets()
    {
        var probe = new FakeProbe();
        await using var service = new MultiTargetPingService(probe, new SlowDns());
        await service.StartAsync([new("a.test", ""), new("b.test", ""), new("c.test", ""), new("127.0.0.1", "")],
            new() { IntervalMs = 250, Concurrency = 2 });
        await Eventually.Until(() => service.Snapshot().Last().Received > 0, 1500);
        await service.StopAsync();
        Assert.Equal(0, probe.Active);
        Assert.All(service.Snapshot().Take(3), row => Assert.Equal(0, row.Sent));
    }

    [Fact]
    public async Task LatestRowsAreCachedAndSessionResetDiscardsOldMeasurements()
    {
        var probe = new FakeProbe();
        await using var service = new MultiTargetPingService(probe, new FakeDns());
        await service.StartAsync([new("127.0.0.1", "")], new() { IntervalMs = 250 });
        await Eventually.Until(() => service.Snapshot()[0].Sent >= 2);
        await service.PauseAsync();
        var update = service.ReadUpdates(0, 0);
        Assert.True(update.Reset); Assert.Single(update.Rows);
        Assert.Same(service.Snapshot()[0], service.Snapshot()[0]);
        Assert.Empty(service.ReadUpdates(update.SessionId, update.Revision).Rows);
        Assert.Equal(service.Snapshot()[0].Sent, update.Totals.Sent);
        Assert.All(service.History("127.0.0.1"), sample => { Assert.Equal(update.SessionId, sample.SessionId); Assert.True(sample.ProbeId > 0); });
        await service.StopAsync();
        probe.Handler = async (_, _, token) => { await Task.Delay(Timeout.Infinite, token); throw new InvalidOperationException(); };
        await service.StartAsync([new("127.0.0.1", "new")], new());
        var restarted = service.ReadUpdates(update.SessionId, update.Revision);
        Assert.True(restarted.Reset); Assert.True(restarted.SessionId > update.SessionId);
        Assert.Equal(0, restarted.Totals.Sent); Assert.Empty(service.History("127.0.0.1"));
        await service.StopAsync();
    }

    [Fact]
    public async Task ThousandTargetsRemainRateLimitedAndCancelledQueueItemsAreNotLoss()
    {
        var starts = new ConcurrentQueue<double>(); var clock = Stopwatch.StartNew();
        var probe = new FakeProbe { Handler = async (address, _, token) =>
        {
            starts.Enqueue(clock.Elapsed.TotalMilliseconds);
            await Task.Delay(address.GetAddressBytes()[3] % 3 == 0 ? 400 : 20, token);
            return address.GetAddressBytes()[3] % 3 == 0 ? new(IPStatus.TimedOut, null, null) : new(IPStatus.Success, address, 20);
        } };
        var targets = Enumerable.Range(1, 1000).Select(i => new TargetDefinition($"192.0.{i / 256}.{i % 256}", "adapter only")).ToArray();
        await using var service = new MultiTargetPingService(probe, new FakeDns());
        await service.StartAsync(targets, new() { IntervalMs = 250, Concurrency = 8, MaxPacketsPerSecond = 20 });
        await Task.Delay(1700);
        await service.PauseAsync();
        int count = starts.Count; var frozen = service.Snapshot();
        Assert.InRange(count, 15, 8 + (int)Math.Ceiling(clock.Elapsed.TotalSeconds * 20));
        Assert.Equal(0, probe.Active); Assert.Equal(0, probe.Overlap); Assert.InRange(probe.Maximum, 1, 8);
        Assert.True(service.Diagnostics.SkippedIntervals > 0);
        Assert.All(frozen, row => Assert.Equal(row.Sent, row.Received + row.Lost));
        Assert.True(frozen.Sum(r => r.Sent) <= count);
        await Task.Delay(150); Assert.Equal(count, starts.Count);
        await service.ResumeAsync(); await Task.Delay(250); await service.StopAsync();
        Assert.InRange(starts.Count - count, 1, 15); Assert.Equal(0, probe.Active);
        output.WriteLine($"1000-target adapter: starts before pause={count}; peak active={probe.Maximum}; missed intervals={service.Diagnostics.SkippedIntervals}; completed={service.Diagnostics.CompletedProbes}; cancelled={service.Diagnostics.CancelledProbes}.");
    }

    [Fact]
    public async Task DnsCacheExpiresWithoutCreatingIcmpLoss()
    {
        var dns = new ChangingDns(); var probe = new FakeProbe();
        await using var service = new MultiTargetPingService(probe, dns);
        await service.StartAsync([new("changing.test", "")], new() { IntervalMs = 250, DnsCacheSeconds = 1, DnsRetrySeconds = 1 });
        await Eventually.Until(() => service.Snapshot()[0].ResolvedIp == "127.0.0.2", 4000);
        await service.StopAsync();
        Assert.Equal(2, dns.Calls); Assert.Equal(0, service.Snapshot()[0].Lost);
        Assert.True(service.Snapshot()[0].Received >= 4);
    }

    [Fact]
    public async Task FailedDnsUsesBackoffAndNeverBecomesPacketLoss()
    {
        var dns = new FailedDns();
        await using var service = new MultiTargetPingService(new FakeProbe(), dns);
        await service.StartAsync([new("failed.test", "")], new() { IntervalMs = 250, DnsRetrySeconds = 1 });
        await Eventually.Until(() => dns.Calls >= 2, 2000);
        await service.StopAsync();
        Assert.Equal(2, dns.Calls); var row = Assert.Single(service.Snapshot());
        Assert.Equal("Unknown", row.Status); Assert.Equal(0, row.Sent); Assert.Equal(0, row.Lost);
    }

    private sealed class FailedDns : IDnsResolver
    {
        public int Calls;
        public Task<IPAddress> ResolveAsync(string host, IpFamily family, CancellationToken token) { Interlocked.Increment(ref Calls); throw new InvalidOperationException("adapter DNS failure"); }
        public Task<string?> ReverseAsync(IPAddress address, CancellationToken token) => Task.FromResult<string?>(null);
    }

    [Fact]
    public async Task IndependentHopPollingKeepsFastHopsMovingWhileAnotherHopWaits()
    {
        var probe = new FakeProbe { Handler = async (address, ttl, token) =>
        {
            await Task.Delay(ttl == 2 ? 800 : 15, token);
            return ttl == 3 ? new(IPStatus.Success, address, 15) : new(IPStatus.TtlExpired, IPAddress.Parse($"192.0.2.{ttl}"), ttl == 2 ? 800 : 15);
        } };
        await using var service = new TracerouteMonitoringService(probe, new FakeDns());
        await service.StartAsync("127.0.0.1", new() { IntervalMs = 250, MaxHops = 4 });
        await Eventually.Until(() => service.Snapshot().FirstOrDefault(h => h.Hop == 1)?.Sent >= 4, 4000);
        await service.PauseAsync();
        var rows = service.Snapshot();
        Assert.True(rows.Single(h => h.Hop == 1).Sent > rows.Single(h => h.Hop == 2).Sent);
        Assert.All(rows, row => { Assert.Equal(0, row.LossPercent); Assert.Equal(row.Sent, row.Received); });
        Assert.Equal(0, probe.Active); Assert.Equal(0, probe.TtlOverlap);
        Assert.DoesNotContain(service.Events(), e => e.Type == "Route" || e.Message.StartsWith("Cycle"));
        Assert.InRange(service.Events().Count, 2, 4);
        output.WriteLine($"Independent fast-hop sent={rows[0].Sent}; slow-hop sent={rows[1].Sent}; events={service.Events().Count}.");
        await service.ResumeAsync();
        await Eventually.Until(() => service.CompletedCycles >= 2);
        await service.StopAsync(); Assert.Equal(0, probe.Active);
    }

    [Fact]
    public async Task IndependentTraceSessionsPauseAndStopWithoutBlockingEachOther()
    {
        var firstProbe = new FakeProbe(); var secondProbe = new FakeProbe();
        await using var first = new TracerouteMonitoringService(firstProbe, new FakeDns());
        await using var second = new TracerouteMonitoringService(secondProbe, new FakeDns());
        await first.StartAsync("127.0.0.1", new() { IntervalMs = 250 });
        await second.StartAsync("127.0.0.2", new() { IntervalMs = 250 });
        await Eventually.Until(() => first.CompletedCycles >= 2 && second.CompletedCycles >= 2);
        await first.PauseAsync(); long paused = first.Snapshot()[0].Sent, progressing = second.Snapshot()[0].Sent;
        await Eventually.Until(() => second.Snapshot()[0].Sent >= progressing + 2);
        Assert.Equal(paused, first.Snapshot()[0].Sent); Assert.Equal(0, firstProbe.Active);
        await first.StopAsync(); await second.StopAsync();
        Assert.Equal(0, secondProbe.Active); Assert.Equal(0, firstProbe.TtlOverlap); Assert.Equal(0, secondProbe.TtlOverlap);
    }

    [Fact]
    public async Task DestinationIsPublishedBeforeSlowEarlierHopsFinishAndHigherTtlsAreCancelled()
    {
        var probe = new FakeProbe { Handler = async (address, ttl, token) =>
        {
            await Task.Delay(ttl == 3 ? 20 : 500, token);
            return ttl == 3 ? new(IPStatus.Success, address, 20) : new(IPStatus.TimedOut, null, null);
        } };
        await using var service = new TracerouteMonitoringService(probe, new FakeDns());
        var clock = Stopwatch.StartNew();
        await service.StartAsync("127.0.0.1", new() { Continuous = false, MaxHops = 12 });
        await Eventually.Until(() => service.Snapshot().Any(h => h.Hop == 3 && h.Received > 0));
        Assert.True(clock.ElapsedMilliseconds < 350); Assert.Equal(0, service.CompletedCycles);
        await Eventually.Until(() => service.State == SessionState.Stopped);
        Assert.Equal(3, service.Snapshot().Count); Assert.DoesNotContain(service.Snapshot(), h => h.Hop > 3);
        Assert.True(service.Diagnostics.CancelledProbes >= 1); Assert.Equal(0, probe.Active); Assert.Equal(0, probe.TtlOverlap);
    }

    [Fact]
    public async Task ReverseDnsFromAnOlderRouteCannotRenameTheCurrentHop()
    {
        var dns = new DelayedReverseDns(); int cycle = 0;
        var probe = new FakeProbe { Handler = (address, ttl, _) =>
        {
            if (ttl == 1) Interlocked.Increment(ref cycle);
            return Task.FromResult(ttl == 1 ? new ProbeResult(IPStatus.TtlExpired, IPAddress.Parse(cycle == 1 ? "192.0.2.1" : "192.0.2.2"), 3) : new(IPStatus.Success, address, 5));
        } };
        await using var service = new TracerouteMonitoringService(probe, dns);
        await service.StartAsync("127.0.0.1", new() { MaxHops = 2, IntervalMs = 1000, ReverseDns = true });
        await Eventually.Until(() => service.CompletedCycles >= 2);
        dns.Release.TrySetResult(); await Task.Delay(100);
        var row = service.Snapshot()[0];
        Assert.Equal("192.0.2.2", row.Address); Assert.NotEqual("old-route.test", row.Hostname);
        await service.StopAsync(); Assert.Equal(0, probe.Active);
    }

    private sealed class ChangingDns : IDnsResolver
    {
        public int Calls;
        public Task<IPAddress> ResolveAsync(string host, IpFamily family, CancellationToken token) => Task.FromResult(IPAddress.Parse(Interlocked.Increment(ref Calls) == 1 ? "127.0.0.1" : "127.0.0.2"));
        public Task<string?> ReverseAsync(IPAddress address, CancellationToken token) => Task.FromResult<string?>(null);
    }
    private sealed class DelayedReverseDns : IDnsResolver
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IPAddress> ResolveAsync(string host, IpFamily family, CancellationToken token) => Task.FromResult(IPAddress.Loopback);
        public async Task<string?> ReverseAsync(IPAddress address, CancellationToken token)
        { if (address.ToString() == "192.0.2.1") { await Release.Task.WaitAsync(token); return "old-route.test"; } return "current-route.test"; }
    }

    [Fact]
    public async Task SpeculativeCancellationCallbacksCanReadSnapshotsWithoutBlocking()
    {
        TracerouteMonitoringService? monitor = null; int blockedCallbacks = 0;
        var probe = new FakeProbe { Handler = async (address, ttl, token) =>
        {
            using var registration = token.Register(() =>
            {
                if (!Task.Run(() => monitor!.Snapshot()).Wait(TimeSpan.FromMilliseconds(300))) Interlocked.Increment(ref blockedCallbacks);
            });
            await Task.Delay(ttl == 3 ? 20 : 500, token);
            return ttl == 3 ? new(IPStatus.Success, address, 20) : new(IPStatus.TimedOut, null, null);
        } };
        await using var service = new TracerouteMonitoringService(probe, new FakeDns()); monitor = service;
        await service.StartAsync("127.0.0.1", new() { Continuous = false, MaxHops = 4 });
        await Eventually.Until(() => service.State == SessionState.Stopped);
        Assert.Equal(0, blockedCallbacks); Assert.Equal(0, probe.Active); Assert.Equal(0, probe.TtlOverlap);
    }

    private sealed class SlowDns : IDnsResolver
    {
        public async Task<IPAddress> ResolveAsync(string host, IpFamily family, CancellationToken token)
        {
            if (IPAddress.TryParse(host, out var address)) return address;
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException();
        }
        public Task<string?> ReverseAsync(IPAddress address, CancellationToken token) => Task.FromResult<string?>(null);
    }
}
