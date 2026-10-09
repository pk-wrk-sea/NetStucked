using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public sealed class BrandingScanTests
{
    [Fact]
    public void PlannerExpandsHostPortProductAndNeverDropsInvalidEntries()
    {
        var result = PortScanPlanner.Parse("192.168.0.0/22 Branch\nrouter.example HQ", "22,443,80,25,65525,50000,22", PortProtocol.UDP, true);
        Assert.True(result.IsValid); Assert.Equal(6138, result.Targets.Count);
        Assert.All(result.Targets, t => Assert.Equal(PortProtocol.UDP, t.Protocol));
        Assert.Equal("Branch", result.Targets[0].Description);
        Assert.False(PortScanPlanner.Parse("192.168.0.0/22", "443", PortProtocol.TCP, false).IsValid);
        Assert.False(PortScanPlanner.Parse("192.168.0.0/21", "443", PortProtocol.TCP, true).IsValid);
        Assert.False(PortScanPlanner.Parse("192.168.0.0/22", string.Join(',', Enumerable.Range(1, 17)), PortProtocol.TCP, true).IsValid);
        Assert.False(PortScanPlanner.Parse("127.0.0.1", "443,bad,65536", PortProtocol.TCP, false).IsValid);
    }
    [Fact]
    public void HopMappingParsesSpaceAndPreservesMultiwordUnicodeDescriptions()
    {
        var map = HopDescriptionParser.Parse("10.10.10.1 CSW-HQ\n2001:db8::1 Branch กรุงเทพ\n\n");
        Assert.Equal("CSW-HQ", map["10.10.10.1"]); Assert.Equal("Branch กรุงเทพ", map["2001:db8::1"]);
        Assert.Throws<ArgumentException>(() => HopDescriptionParser.Parse("10.10.10.1"));
        Assert.Throws<ArgumentException>(() => HopDescriptionParser.Parse("10.1 CSW"));
        Assert.Throws<ArgumentException>(() => HopDescriptionParser.Parse("10.10.10.1 A\n10.10.10.1 B"));
    }
    [Theory]
    [InlineData("10.10.10.1")][InlineData("192.168.1.1")][InlineData("172.31.255.1")][InlineData("100.64.0.1")]
    [InlineData("127.0.0.1")][InlineData("169.254.1.1")][InlineData("192.0.2.1")][InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")][InlineData("224.0.0.1")][InlineData("::1")][InlineData("fc00::1")][InlineData("fe80::1")]
    [InlineData("2001:db8::1")][InlineData("::ffff:10.10.10.1")]
    public void NonpublicAddressesAreNeverSubmittedForWanLookup(string address) => Assert.False(HopDescriptionParser.IsPublic(IPAddress.Parse(address)));
    [Theory]
    [InlineData("1.1.1.1")][InlineData("8.8.8.8")][InlineData("2606:4700:4700::1111")]
    public void PublicAddressIsEligibleForMetadata(string address) => Assert.True(HopDescriptionParser.IsPublic(IPAddress.Parse(address)));
    [Fact]
    public async Task SinglePassCompletesWithDnsFailureAndUdpSilenceWithoutRepeating()
    {
        var adapter = new TestProbe(); await using var engine = new MultiTargetPortService(adapter, new TestDns(), adapter);
        await engine.StartAsync([new("127.0.0.1", 443, ""), new("127.0.0.1", 53, "") { Protocol = PortProtocol.UDP }, new("missing.invalid", 22, "")], new() { Continuous = false, IntervalMs = 250 });
        await Eventually.Until(() => engine.State == SessionState.Stopped);
        var result = engine.ReadUpdates(0, 0);
        Assert.Equal(2, result.Totals.Attempts); Assert.Equal(1, result.Totals.Connected); Assert.Equal(1, result.Totals.NoResponse);
        Assert.Equal(1, result.Totals.Failed); Assert.Equal(0, result.Totals.Closed);
        Assert.Equal(1, adapter.TcpCalls); Assert.Equal(1, adapter.UdpCalls);
        var silent = result.Rows.Single(r => r.Protocol == PortProtocol.UDP); Assert.Null(silent.Last); Assert.Null(silent.Average);
        Assert.Equal("No response", silent.Status); Assert.Equal(0, result.Rows.Single(r => r.Host == "missing.invalid").Attempts);
    }
    [Fact]
    public async Task PausingSinglePassRetriesCancelledWorkWithoutCountingIt()
    {
        var adapter = new TestProbe { Block = true }; await using var engine = new MultiTargetPortService(adapter, new TestDns(), adapter);
        await engine.StartAsync([new("127.0.0.1", 443, "")], new() { Continuous = false, IntervalMs = 250 });
        await Eventually.Until(() => adapter.TcpCalls > 0); await engine.PauseAsync(); Assert.Equal(0, engine.Snapshot()[0].Attempts);
        adapter.Block = false; await engine.ResumeAsync(); await Eventually.Until(() => engine.State == SessionState.Stopped);
        Assert.Equal(1, engine.Snapshot()[0].Attempts); Assert.Equal(2, adapter.TcpCalls);
    }
    [Fact]
    public async Task RealUdpResponseSilenceAndCancellationRemainDistinct()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0)); int port = ((IPEndPoint)socket.LocalEndPoint!).Port;
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var responding = Task.Run(async () =>
        {
            var buffer = new byte[16]; var received = await socket.ReceiveFromAsync(buffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0), lifetime.Token);
            await socket.SendToAsync(new byte[] { 42 }, SocketFlags.None, received.RemoteEndPoint, lifetime.Token);
        });
        var probe = new UdpPortProbe(); var result = await probe.ProbeAsync(IPAddress.Loopback, port, 1000, lifetime.Token); await responding;
        Assert.Equal(PortOutcome.Responded, result.Outcome); Assert.NotNull(result.ConnectMs);
        var silent = await probe.ProbeAsync(IPAddress.Loopback, port, 500, lifetime.Token);
        Assert.Equal(PortOutcome.NoResponse, silent.Outcome); Assert.Null(silent.ConnectMs);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.ProbeAsync(IPAddress.Loopback, port, 500, cancelled.Token));
    }
    [Fact]
    public async Task SettingsKeepThemeAndSharedAliasesAcrossReloadAndOldSchemaDefaults()
    {
        string path = Path.Combine(Path.GetTempPath(), "NetStucked-branding-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new UserSettingsStore(path); store.Preferences.Theme = "Dark"; store.Preferences.SidebarExpanded = false;
            store.Preferences.HopDescriptions = HopDescriptionParser.Parse("10.10.10.1 CSW-HQ"); store.Preferences.PortProtocol = PortProtocol.UDP;
            store.Preferences.PortNumberTemplates["Custom"] = "22,65525"; await store.SaveAsync();
            store.Preferences.Port = store.Preferences.Port with { PacketSize = 128 }; await store.SaveAsync();
            var read = new UserSettingsStore(path); Assert.Null(read.LoadError); Assert.Equal("Dark", read.Preferences.Theme);
            Assert.False(read.Preferences.SidebarExpanded); Assert.Equal("CSW-HQ", read.Preferences.HopDescriptions["10.10.10.1"]);
            Assert.Equal(PortProtocol.UDP, read.Preferences.PortProtocol); Assert.Equal("22,65525", read.Preferences.PortNumberTemplates["Custom"]);
            Assert.Equal(128, read.Preferences.Port.PacketSize);
            File.WriteAllText(Path.Combine(path, "settings.json"), "{}"); read = new(path);
            Assert.Null(read.LoadError); Assert.Equal("Light", read.Preferences.Theme); Assert.False(read.Preferences.PortContinuous);
            Assert.Equal(32, read.Preferences.Port.PacketSize);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
    [Fact]
    public async Task WanAdapterUsesOnlyPublicRipeResourcesAndRealReturnedIdentity()
    {
        var handler = new WanHttp(); using var source = new RipeWanIdentitySource(new HttpClient(handler));
        Assert.Null(await source.LookupAsync(IPAddress.Parse("192.168.1.1"), CancellationToken.None)); Assert.Empty(handler.Requests);
        var result = await source.LookupAsync(IPAddress.Parse("1.1.1.1"), CancellationToken.None);
        Assert.Equal("Fixture Organization · AS64500", result?.Description); Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal("stat.ripe.net", request.Host));
        Assert.Contains("resource=1.1.1.1", handler.Requests[0].Query); Assert.Contains("1.1.1.0%2F24", handler.Requests[1].Query);
        handler.Oversized = true;
        await Assert.ThrowsAsync<IOException>(() => source.LookupAsync(IPAddress.Parse("8.8.8.8"), CancellationToken.None));
    }
    [Fact]
    public async Task WanCancellationDrainsActiveHttpAndPreventsQueuedHttpSends()
    {
        var handler = new BlockingWanHttp(); using var source = new RipeWanIdentitySource(new HttpClient(handler));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var tasks = Enumerable.Range(1, 32).Select(i => source.LookupAsync(IPAddress.Parse($"8.8.8.{i}"), cancellation.Token)).ToArray();
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.WhenAll(tasks));
        Assert.Equal(1, handler.Requests); Assert.Equal(0, handler.Active);
    }
    private sealed class BlockingWanHttp : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Requests, Active;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Requests); Interlocked.Increment(ref Active); Started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); throw new InvalidOperationException("The test response must be cancelled."); }
            finally { Interlocked.Decrement(ref Active); }
        }
    }
    private sealed class WanHttp : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = []; public bool Oversized;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests.Add(request.RequestUri!);
            string body = request.RequestUri!.AbsolutePath.Contains("network-info") ? "{\"data\":{\"prefix\":\"1.1.1.0/24\"}}" : "{\"data\":{\"asns\":[{\"asn\":64500,\"holder\":\"Fixture Organization\"}]}}";
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
            if (Oversized) response.Content.Headers.ContentLength = 262145;
            return Task.FromResult(response);
        }
    }
    private sealed class TestProbe : ITcpProbe, IUdpProbe
    {
        public bool Block; public int TcpCalls, UdpCalls;
        public async Task<TcpProbeResult> ConnectAsync(IPAddress address, int port, int timeout, int packetSize, CancellationToken token)
        { Interlocked.Increment(ref TcpCalls); if (Block) await Task.Delay(Timeout.Infinite, token); return new(PortOutcome.Connected, 1, "Test adapter"); }
        public Task<TcpProbeResult> ProbeAsync(IPAddress address, int port, int timeout, int packetSize, CancellationToken token)
        { Interlocked.Increment(ref UdpCalls); return Task.FromResult(new TcpProbeResult(PortOutcome.NoResponse, null, "Test adapter silence")); }
    }
    private sealed class TestDns : IDnsResolver
    {
        public Task<IPAddress> ResolveAsync(string host, IpFamily family, CancellationToken token) => Task.FromException<IPAddress>(new SocketException((int)SocketError.HostNotFound));
        public Task<string?> ReverseAsync(IPAddress address, CancellationToken token) => Task.FromResult<string?>(null);
    }
}
