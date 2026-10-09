using System.Net;
using System.Net.Sockets;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public sealed class PortDefectTests
{
    [Fact]
    public void RangesIncludeBothEndpointsDeduplicateAndKeepOriginalOrder()
    {
        var scan = PortScanPlanner.Parse("127.0.0.1\nlocalhost", "443,1000-1005,1003,65534-65535", PortProtocol.TCP, true);
        Assert.True(scan.IsValid);
        Assert.Equal(new[] { 443,1000,1001,1002,1003,1004,1005,65534,65535 }, scan.Targets.Where(t => t.Host == "127.0.0.1").Select(t => t.Port));
        Assert.Equal(18, scan.Targets.Count);
        Assert.True(PortScanPlanner.Parse("127.0.0.1", "1-64,1-64", PortProtocol.UDP, true).IsValid);
    }
    [Theory]
    [InlineData("1005-1000")][InlineData("0-3")][InlineData("65535-65536")]
    [InlineData("1000--1005")][InlineData("1-2-3")][InlineData("+1-3")]
    [InlineData("1-65535")][InlineData("1-64,65")][InlineData("22,443,bad")]
    public void InvalidOrOversizedRangeCannotAuthorizeAnyProbe(string ports)
    {
        var scan = PortScanPlanner.Parse("127.0.0.1", ports, PortProtocol.TCP, true);
        Assert.False(scan.IsValid); Assert.Empty(scan.Targets); Assert.NotEmpty(scan.Errors);
    }
    [Theory]
    [InlineData(-1)][InlineData(1401)]
    public void PayloadSizeHasABoundInSettingsAndAdapters(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PortProbeSettings { PacketSize = size }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => PortProbeSettings.ValidatePacketSize(size));
    }
    [Theory]
    [InlineData("127.0.0.1",0)][InlineData("127.0.0.1",32)][InlineData("127.0.0.1",1400)][InlineData("::1",32)]
    public async Task RealTcpPeerReceivesExactConfiguredPayload(string literal, int size)
    {
        var address = IPAddress.Parse(literal); var listener = new TcpListener(address, 0); listener.Start();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var receive = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync(lifetime.Token);
                using var data = new MemoryStream(); await client.GetStream().CopyToAsync(data, lifetime.Token);
                return data.ToArray();
            });
            var result = await new TcpPortProbe().ConnectAsync(address, ((IPEndPoint)listener.LocalEndpoint).Port, 2000, size, lifetime.Token);
            byte[] actual = await receive;
            Assert.Equal(PortOutcome.Connected, result.Outcome); Assert.NotNull(result.ConnectMs);
            Assert.Equal(size, result.BytesSent); Assert.Equal(size, actual.Length); Assert.All(actual, b => Assert.Equal((byte)0, b));
        }
        finally { listener.Stop(); }
    }
    [Theory]
    [InlineData(0)][InlineData(32)][InlineData(1400)]
    public async Task RealUdpPeerReceivesExactConfiguredDatagramAndReplyIsMeasured(int size)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0)); using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var receive = Task.Run(async () =>
        {
            var buffer = new byte[PortProbeSettings.MaxPacketSize + 1];
            var received = await socket.ReceiveFromAsync(buffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0), lifetime.Token);
            Assert.Equal(size, received.ReceivedBytes); Assert.All(buffer.Take(size), b => Assert.Equal((byte)0, b));
            await socket.SendToAsync(new byte[] { 42 }, SocketFlags.None, received.RemoteEndPoint, lifetime.Token);
        });
        var result = await new UdpPortProbe().ProbeAsync(IPAddress.Loopback, ((IPEndPoint)socket.LocalEndPoint!).Port, 2000, size, lifetime.Token);
        await receive; Assert.Equal(PortOutcome.Responded, result.Outcome); Assert.Equal(size, result.BytesSent); Assert.NotNull(result.ConnectMs);
    }
    [Fact]
    public async Task SchedulerPassesConfiguredSizeToBothProtocols()
    {
        var probe = new PayloadFixture(); await using var service = new MultiTargetPortService(probe, new LiteralDns(), probe);
        await service.StartAsync([new("127.0.0.1",1,""), new("127.0.0.1",2,"") { Protocol = PortProtocol.UDP }], new() { PacketSize = 128, Continuous = false });
        await Eventually.Until(() => service.State == SessionState.Stopped);
        Assert.Equal(128, probe.TcpSize); Assert.Equal(128, probe.UdpSize);
    }
    private sealed class PayloadFixture : ITcpProbe, IUdpProbe
    {
        public int TcpSize, UdpSize;
        public Task<TcpProbeResult> ConnectAsync(IPAddress address, int port, int timeout, int size, CancellationToken token)
        { TcpSize = size; return Task.FromResult(new TcpProbeResult(PortOutcome.Connected, 1, "Test TCP payload")); }
        public Task<TcpProbeResult> ProbeAsync(IPAddress address, int port, int timeout, int size, CancellationToken token)
        { UdpSize = size; return Task.FromResult(new TcpProbeResult(PortOutcome.Responded, 1, "Test UDP payload")); }
    }
    private sealed class LiteralDns : IDnsResolver
    {
        public Task<IPAddress> ResolveAsync(string host, IpFamily family, CancellationToken token) => Task.FromResult(IPAddress.Parse(host));
        public Task<string?> ReverseAsync(IPAddress address, CancellationToken token) => Task.FromResult<string?>(null);
    }
}
