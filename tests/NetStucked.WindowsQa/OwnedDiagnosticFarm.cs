using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetStucked.WindowsQa;

/// <summary>QA-owned loopback responders; never used by production or seeded as user targets.</summary>
internal sealed class OwnedDiagnosticFarm : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly UdpClient _dns = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly TcpListener _http = new(IPAddress.Loopback, 0);
    private readonly System.Collections.Concurrent.ConcurrentBag<Task> _clients = [];
    private readonly Task _dnsTask, _httpTask;
    public string Resolver => _dns.Client.LocalEndPoint!.ToString()!;
    public string Url { get; }
    public int DnsReplies, HttpReplies;
    public OwnedDiagnosticFarm()
    {
        _http.Start(); Url = "http://127.0.0.1:" + ((IPEndPoint)_http.LocalEndpoint).Port + "/";
        _dnsTask = DnsLoop(); _httpTask = HttpLoop();
    }
    private async Task DnsLoop()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var packet = await _dns.ReceiveAsync(_stop.Token); byte[] q = packet.Buffer; ushort type = BinaryPrimitives.ReadUInt16BigEndian(q.AsSpan(q.Length - 4));
                byte[] value = type == 28 ? IPAddress.IPv6Loopback.GetAddressBytes() : new byte[] { 127, 0, 0, 1 };
                byte[] response = q.Concat(new byte[] { 0xc0, 12, 0, (byte)type, 0, 1, 0, 0, 0, 30, 0, (byte)value.Length }).Concat(value).ToArray(); response[2] = 0x81; response[3] = 0x80; response[7] = 1;
                await _dns.SendAsync(response, packet.RemoteEndPoint, _stop.Token); Interlocked.Increment(ref DnsReplies);
            }
        }
        catch (OperationCanceledException) { }
    }
    private async Task HttpLoop()
    {
        try { while (!_stop.IsCancellationRequested) { var client = await _http.AcceptTcpClientAsync(_stop.Token); _clients.Add(Serve(client)); } } catch (OperationCanceledException) { }
    }
    private async Task Serve(TcpClient client)
    {
        using (client)
        try
        {
            using var stream = client.GetStream(); var header = new StringBuilder(); byte[] one = new byte[1];
            while (header.Length < 4096 && !header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal)) { if (await stream.ReadAsync(one, _stop.Token) == 0) return; header.Append((char)one[0]); }
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), _stop.Token); Interlocked.Increment(ref HttpReplies);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
    }
    public async ValueTask DisposeAsync() { await _stop.CancelAsync(); await Task.WhenAll(_dnsTask, _httpTask); await Task.WhenAll(_clients); _dns.Dispose(); _http.Stop(); _stop.Dispose(); }
}
