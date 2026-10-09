using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public sealed class DiagnosticsStabilityTests
{
    [Fact]
    public void HistoryRecordsActualReplyAddressAndHostWithoutSubstitutingDns()
    {
        var target = new TargetAccumulator(new("router.example", "HQ"), 1, 100) { ResolvedIp = "192.0.2.10" };
        target.Add(new(IPStatus.DestinationHostUnreachable, IPAddress.Parse("192.0.2.1"), null));
        Assert.Equal("192.0.2.1", target.Snapshot(new()).ReplyIpAddress);
        Assert.Equal("router.example", target.History(100)[0].Host);
        Assert.Equal("192.0.2.1", target.History(100)[0].ReplyIpAddress);
        target.Add(new(IPStatus.TimedOut, null, null));
        Assert.Null(target.Snapshot(new()).ReplyIpAddress); Assert.Null(target.History(100)[0].ReplyIpAddress);
        Assert.Contains("Reply IP Address", CsvExporter.Ping([target.Snapshot(new())]));
    }

    [Fact]
    public void WarningRecoversFromOldLossButKeepsCumulativeCounters()
    {
        var target = new TargetAccumulator(new("localhost", ""), 1, 100);
        target.Add(new(IPStatus.TimedOut, null, null));
        Assert.Contains("consecutive", target.Snapshot(new()).WarningReason);
        target.Add(new(IPStatus.Success, IPAddress.Loopback, 2));
        Assert.Contains("last 2 completed attempts", target.Snapshot(new()).WarningReason);
        for (int i = 0; i < 19; i++) target.Add(new(IPStatus.Success, IPAddress.Loopback, 2));
        var row = target.Snapshot(new());
        Assert.Equal("Up", row.Status); Assert.Null(row.WarningReason); Assert.Equal(1, row.Lost); Assert.True(row.LossPercent > 0);
        target.Add(new(IPStatus.Success, IPAddress.Loopback, 123));
        Assert.Contains("RTT 123", target.Snapshot(new()).ResultError);
    }

    [Fact]
    public async Task TraceCapacityRemainsAvailableWhenPingSlotsAreFull()
    {
        using var gate = new IcmpAdmissionController(2, 1, 512, 128);
        using var first = await gate.EnterAsync(false, CancellationToken.None);
        using var second = await gate.EnterAsync(false, CancellationToken.None);
        using var cancel = new CancellationTokenSource();
        var waiting = gate.EnterAsync(false, cancel.Token).AsTask();
        using var trace = await gate.EnterAsync(true, CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(waiting.IsCompleted);
        await cancel.CancelAsync(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    [Fact]
    public async Task AdmissionPacesConcurrentSendsAndCancellationReturnsSlots()
    {
        using var gate = new IcmpAdmissionController(4, 1, 20, 20);
        var times = new ConcurrentBag<double>(); var clock = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ => { using var lease = await gate.EnterAsync(false, CancellationToken.None); times.Add(clock.Elapsed.TotalMilliseconds); }));
        var ordered = times.Order().ToArray();
        for (int i = 1; i < ordered.Length; i++) Assert.True(ordered[i] - ordered[i - 1] >= 48, "Concurrent sends must be spaced, without catch-up bursts.");
        using (var cancel = new CancellationTokenSource(5)) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gate.EnterAsync(false, cancel.Token).AsTask());
        using var next = await gate.EnterAsync(false, CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("22,443,8088", 3)]
    [InlineData("1000-1005,1003", 6)]
    [InlineData("65535", 1)]
    public void OptionalHopTcpPortsParseAndDeduplicate(string text, int count)
    {
        var settings = new TraceSettings { CheckHopTcp = true, HopTcpPorts = text };
        settings.Validate(); Assert.Equal(count, settings.ParseTcpPorts().Length);
    }

    [Theory]
    [InlineData("0")][InlineData("65536")][InlineData("1-17")][InlineData("443,bad")]
    public void InvalidOrOversizedHopPortScopeIsRejectedOnlyWhenEnabled(string text)
    {
        Assert.Throws<ArgumentException>(() => new TraceSettings { CheckHopTcp = true, HopTcpPorts = text }.Validate());
        new TraceSettings { HopTcpPorts = text }.Validate();
    }

    [Fact]
    public async Task HopTcpDisabledDoesNotConnectAndEnabledReportsRealListeningPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var adapter = new CountingTcp(new TcpPortProbe());
        await using var service = new TracerouteMonitoringService(new FakeProbe(), new FakeDns(), adapter);
        await service.StartAsync("127.0.0.1", new() { IntervalMs = 250, TimeoutMs = 500 });
        await Eventually.Until(() => service.CompletedCycles >= 2); await service.StopAsync();
        Assert.Equal(0, adapter.Calls); Assert.Equal("Disabled", Assert.Single(service.Snapshot()).TcpCheckStatus);
        await service.StartAsync("127.0.0.1", new() { IntervalMs = 250, TimeoutMs = 500, CheckHopTcp = true, HopTcpPorts = port.ToString() });
        using var socket = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await Eventually.Until(() => service.Snapshot().Any(r => r.TcpOpenPorts == port.ToString()));
        await service.StopAsync(); var row = Assert.Single(service.Snapshot());
        Assert.Equal(1, adapter.Calls); Assert.NotNull(row.TcpCheckedAt); Assert.Equal(0, adapter.Payload);
        Assert.Contains(port.ToString(), CsvExporter.Trace([row]));
    }

    [Fact]
    public async Task SlowTcpDoesNotHoldIcmpAndPauseStopAwaitTcpCancellation()
    {
        var adapter = new BlockingTcp(); await using var service = new TracerouteMonitoringService(new FakeProbe(), new FakeDns(), adapter);
        await service.StartAsync("127.0.0.1", new() { IntervalMs = 250, TimeoutMs = 500, CheckHopTcp = true, HopTcpPorts = "22,443" });
        await Eventually.Until(() => adapter.Active == 1 && service.CompletedCycles >= 3);
        await service.PauseAsync(); Assert.Equal(0, adapter.Active);
        long sent = service.Snapshot().Sum(r => r.Sent); await Task.Delay(300); Assert.Equal(sent, service.Snapshot().Sum(r => r.Sent));
        await service.ResumeAsync(); await Eventually.Until(() => adapter.Active == 1);
        await service.StopAsync(); Assert.Equal(0, adapter.Active); Assert.DoesNotContain(service.Snapshot(), r => r.TcpOpenPorts.Length > 0);
    }

    [Fact]
    public async Task DuplicateHopIpIsCheckedOnceAndClosedPortsAreNotDisplayed()
    {
        var probe = new FakeProbe { Handler = (address, ttl, _) => Task.FromResult(ttl <= 2 ? new ProbeResult(IPStatus.TtlExpired, IPAddress.Parse("127.0.0.2"), 1) : new(IPStatus.Success, address, 2)) };
        var adapter = new CountingTcp(null);
        await using var service = new TracerouteMonitoringService(probe, new FakeDns(), adapter);
        await service.StartAsync("127.0.0.1", new() { MaxHops = 3, IntervalMs = 250, CheckHopTcp = true, HopTcpPorts = "22,443" });
        await Eventually.Until(() => service.Snapshot().Count == 3 && service.Snapshot().All(r => r.TcpCheckedAt is not null));
        await service.StopAsync(); Assert.Equal(4, adapter.Calls); Assert.All(service.Snapshot(), r => Assert.Equal("", r.TcpOpenPorts));
    }

    [Fact]
    public async Task HopCheckSettingsRoundTripAndLegacyDefaultsRemainOff()
    {
        string dir = Path.Combine(Path.GetTempPath(), "NetStucked-hop-tcp-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new UserSettingsStore(dir); Assert.False(store.Preferences.Trace.CheckHopTcp);
            store.Preferences.Trace = new() { CheckHopTcp = true, HopTcpPorts = "22,443,8088" }; await store.SaveAsync();
            var loaded = new UserSettingsStore(dir); Assert.Null(loaded.LoadError); Assert.True(loaded.Preferences.Trace.CheckHopTcp); Assert.Equal("22,443,8088", loaded.Preferences.Trace.HopTcpPorts);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task OneShotTraceRetainsTcpResultsBeforeEnding()
    {
        var adapter = new CountingTcp(null);
        await using var service = new TracerouteMonitoringService(new FakeProbe(), new FakeDns(), adapter);
        await service.StartAsync("127.0.0.1", new() { Continuous = false, CheckHopTcp = true, HopTcpPorts = "22,443" });
        await Eventually.Until(() => service.State == SessionState.Stopped);
        Assert.Equal(2, adapter.Calls); Assert.NotNull(Assert.Single(service.Snapshot()).TcpCheckedAt);
    }

    [Fact]
    public async Task LateTcpCompletionForAnOldRouteCannotPopulateTheNewHop()
    {
        bool change = false;
        var old = IPAddress.Parse("127.0.0.2"); var next = IPAddress.Parse("127.0.0.3");
        var probe = new FakeProbe { Handler = (address, ttl, _) => Task.FromResult(ttl == 1 ? new ProbeResult(IPStatus.TtlExpired, change ? next : old, 1) : new(IPStatus.Success, address, 2)) };
        var adapter = new DelayedOldTcp(old);
        await using var service = new TracerouteMonitoringService(probe, new FakeDns(), adapter);
        await service.StartAsync("127.0.0.1", new() { MaxHops = 2, IntervalMs = 250, CheckHopTcp = true, HopTcpPorts = "443" });
        await adapter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); change = true;
        await Eventually.Until(() => service.Snapshot().Any(r => r.Hop == 1 && r.Address == next.ToString() && r.TcpCheckedAt is not null));
        adapter.Complete.TrySetResult(); await Eventually.Until(() => adapter.Finished);
        await service.StopAsync(); var hop = service.Snapshot().Single(r => r.Hop == 1);
        Assert.Equal(next.ToString(), hop.Address); Assert.Equal("", hop.TcpOpenPorts);
    }

    private sealed class CountingTcp(ITcpProbe? real) : ITcpProbe
    {
        public int Calls, Payload;
        public Task<TcpProbeResult> ConnectAsync(IPAddress address, int port, int timeoutMs, int packetSize, CancellationToken token)
        {
            Interlocked.Increment(ref Calls); Payload = packetSize;
            return real?.ConnectAsync(address, port, timeoutMs, packetSize, token) ?? Task.FromResult(new TcpProbeResult(PortOutcome.Refused, null, "Refused by test adapter"));
        }
    }
    [Fact]
    public void MalformedSavedHopPortTextCannotReachTheDialog()
    {
        Assert.Throws<ArgumentException>(() => new TraceSettings { HopTcpPorts = null! }.Validate());
        Assert.Throws<ArgumentException>(() => new TraceSettings { HopTcpPorts = new string('1', 2049) }.Validate());
    }
    private sealed class BlockingTcp : ITcpProbe
    {
        public int Active;
        public async Task<TcpProbeResult> ConnectAsync(IPAddress address, int port, int timeoutMs, int packetSize, CancellationToken token)
        {
            Interlocked.Increment(ref Active);
            try { await Task.Delay(Timeout.Infinite, token); throw new InvalidOperationException(); }
            finally { Interlocked.Decrement(ref Active); }
        }
    }
    private sealed class DelayedOldTcp(IPAddress old) : ITcpProbe
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Complete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public volatile bool Finished;
        public async Task<TcpProbeResult> ConnectAsync(IPAddress address, int port, int timeoutMs, int packetSize, CancellationToken token)
        {
            if (!address.Equals(old)) return new(PortOutcome.Refused, null, "Refused by test adapter");
            Entered.TrySetResult(); await Complete.Task.WaitAsync(token); Finished = true;
            return new(PortOutcome.Connected, 2, "Connected by test adapter");
        }
    }
}
