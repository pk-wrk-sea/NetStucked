using System.Text;
using System.Threading.Channels;
using System.Net;
using System.Net.Sockets;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public sealed class CollectorLineEndingTests
{
    [Fact]
    public async Task SshCollectorCompletesAgainstCarriageReturnTerminatedConsole()
    {
        var transport = new StrictCarriageReturnTransport();
        var run = new CollectorRun(new(CollectorProtocol.Ssh, "Cisco IOS / IOS XE", Path.GetTempPath(), CommandTimeoutSeconds: 5),
            [new(1, "owned-strict-cr.test", 22, "Owned console fixture; no real device", null)], ["terminal length 0"], ["show version"],
            new Dictionary<string, string>(), new("owned-fixture", "owned-not-a-real-secret", ""), null);
        var result = Assert.Single(await new CollectorController(transport, new MemoryCaptureFactory()).RunAsync(run, _ => { }, default).WaitAsync(TimeSpan.FromSeconds(8)));
        Assert.True(result.State == CollectorDeviceState.Completed, $"Expected the CR-delimited console to complete; actual {result.State}: {result.Detail}");
        Assert.Equal(new[] { "terminal length 0", "show version" }, transport.Connection.Commands);
        Assert.All(transport.Connection.Writes, bytes => { Assert.Equal((byte)'\r', bytes[^1]); Assert.DoesNotContain((byte)'\n', bytes); });
        Assert.True(transport.Connection.Disposed);
    }

    [Fact]
    public async Task TelnetCollectorKeepsExactlyOneCrLfPerLoginAndCommandOnActualLoopbackWire()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var wireLines = new List<byte[]>();
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(deadline.Token); var stream = peer.GetStream();
            async Task ReadLine()
            {
                var line = new List<byte>(); byte[] one = new byte[1];
                do { Assert.Equal(1, await stream.ReadAsync(one, deadline.Token)); line.Add(one[0]); Assert.InRange(line.Count, 1, 2048); } while (line[^1] != '\n');
                Assert.True(line.Count >= 2 && line[^2] == '\r', "Every actual Telnet line must end with exactly CRLF.");
                Assert.DoesNotContain((byte)'\r', line.Take(line.Count - 2)); wireLines.Add(line.ToArray());
            }
            await stream.WriteAsync(Encoding.UTF8.GetBytes("Username:"), deadline.Token); await ReadLine();
            await stream.WriteAsync(Encoding.UTF8.GetBytes("Password:"), deadline.Token); await ReadLine();
            await stream.WriteAsync(Encoding.UTF8.GetBytes("\r\nowned#"), deadline.Token);
            for (int i = 0; i < 2; i++) { await ReadLine(); await stream.WriteAsync(Encoding.UTF8.GetBytes("\r\nowned output\r\nowned#"), deadline.Token); }
        }, deadline.Token);
        var run = new CollectorRun(new(CollectorProtocol.Telnet, "Cisco IOS / IOS XE", Path.GetTempPath(), CommandTimeoutSeconds: 5, AllowTelnet: true),
            [new(1, "127.0.0.1", port, "Owned loopback CRLF fixture", null)], ["terminal length 0"], ["show version"], new Dictionary<string, string>(), new("owned-fixture", "owned-not-a-real-secret", ""), null);
        var result = Assert.Single(await new CollectorController(new CollectorTransport(), new MemoryCaptureFactory()).RunAsync(run, _ => { }, deadline.Token));
        await server;
        Assert.Equal(CollectorDeviceState.Completed, result.State);
        Assert.Equal(new[] { "owned-fixture\r\n", "owned-not-a-real-secret\r\n", "terminal length 0\r\n", "show version\r\n" }, wireLines.Select(Encoding.UTF8.GetString));
    }

    [Fact]
    public async Task OwnedConsoleFixtureAcceptsCarriageReturnAndWaitsOnBareLineFeed()
    {
        await using var console = new StrictCarriageReturnConnection();
        byte[] buffer = new byte[1024];
        Assert.Equal("owned#", Encoding.UTF8.GetString(buffer, 0, await console.ReadAsync(buffer, default)));
        await console.WriteAsync(Encoding.UTF8.GetBytes("show version\n"), default);
        Assert.Empty(console.Commands);
        await console.WriteAsync(Encoding.UTF8.GetBytes("\r"), default);
        Assert.Equal("show version", Assert.Single(console.Commands));
        Assert.Contains("owned output", Encoding.UTF8.GetString(buffer, 0, await console.ReadAsync(buffer, default)));
    }

    // This owned byte-stream fixture tests only the console terminator contract.
    // It does not establish the observed Cisco incident's PTY or authentication cause.
    private sealed class StrictCarriageReturnTransport : ICollectorTransport
    {
        public StrictCarriageReturnConnection Connection { get; } = new();
        public Task<ICollectorConnection> ConnectAsync(CollectorTarget target, CollectorOptions options, CollectorCredentials credentials, string? hostPin, CancellationToken token)
            => Task.FromResult<ICollectorConnection>(Connection);
    }
    private sealed class StrictCarriageReturnConnection : ICollectorConnection
    {
        private readonly Channel<byte[]> _received = Channel.CreateUnbounded<byte[]>();
        private string _pending = "";
        public List<string> Commands { get; } = [];
        public List<byte[]> Writes { get; } = [];
        public bool Disposed { get; private set; }
        public StrictCarriageReturnConnection() => _received.Writer.TryWrite(Encoding.UTF8.GetBytes("owned#"));
        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
        {
            byte[] response = await _received.Reader.ReadAsync(token);
            response.CopyTo(buffer); return response.Length;
        }
        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Writes.Add(bytes.ToArray()); _pending += Encoding.UTF8.GetString(bytes.Span);
            while (_pending.IndexOf('\r') is int end && end >= 0)
            {
                string command = _pending[..end].Trim('\n'); _pending = _pending[(end + 1)..];
                Commands.Add(command); _received.Writer.TryWrite(Encoding.UTF8.GetBytes(command + "\r\nowned output\r\nowned#"));
            }
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() { Disposed = true; _received.Writer.TryComplete(); return ValueTask.CompletedTask; }
    }
    private sealed class MemoryCaptureFactory : ICollectorCaptureFactory
    { public Task<ICollectorCapture> CreateAsync(CollectorRun run, CollectorTarget target, CancellationToken token) => Task.FromResult<ICollectorCapture>(new MemoryCapture()); }
    private sealed class MemoryCapture : ICollectorCapture
    {
        public string Preview { get; private set; } = "";
        public Task AppendAsync(string text, CancellationToken token) { Preview += text; return Task.CompletedTask; }
        public Task<CollectorResult> CompleteAsync(CollectorResult result, IReadOnlyList<CollectorCommandEvidence> commands, CancellationToken token) => Task.FromResult(result with { Preview = Preview, Bytes = Encoding.UTF8.GetByteCount(Preview) });
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
