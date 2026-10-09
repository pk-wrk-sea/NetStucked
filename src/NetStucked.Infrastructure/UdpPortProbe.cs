using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

/// <summary>A generic UDP probe. Silence is deliberately inconclusive.</summary>
public sealed class UdpPortProbe : IUdpProbe
{
    public Task<TcpProbeResult> ProbeAsync(IPAddress address, int port, int timeoutMs, CancellationToken cancellationToken)
        => ProbeAsync(address, port, timeoutMs, 0, cancellationToken);
    public async Task<TcpProbeResult> ProbeAsync(IPAddress address, int port, int timeoutMs, int packetSize, CancellationToken cancellationToken)
    {
        PortProbeSettings.ValidatePacketSize(packetSize);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeoutMs);
        using var socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        long start = Stopwatch.GetTimestamp();
        int sent = 0;
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), deadline.Token).ConfigureAwait(false);
            sent = await socket.SendAsync(new byte[packetSize].AsMemory(), SocketFlags.None, deadline.Token).ConfigureAwait(false);
            var buffer = new byte[4096];
            int received = await socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, deadline.Token).ConfigureAwait(false);
            return new(PortOutcome.Responded, Stopwatch.GetElapsedTime(start).TotalMilliseconds, $"Sent {sent} UDP payload bytes; received {received} UDP bytes.") { BytesSent = sent };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(PortOutcome.NoResponse, null, $"Sent {sent} UDP payload bytes; no UDP response. The port may be open, filtered, or silent.") { BytesSent = sent }; }
        catch (SocketException ex) when (!cancellationToken.IsCancellationRequested)
        {
            TcpProbeResult result = ex.SocketErrorCode switch
            {
                SocketError.ConnectionReset or SocketError.ConnectionRefused => new(PortOutcome.Closed, null, "ICMP port unreachable was reported by the network stack."),
                SocketError.HostUnreachable or SocketError.NetworkUnreachable => new(PortOutcome.Unreachable, null, ex.Message),
                _ => new(PortOutcome.Error, null, ex.Message)
            };
            return result with { BytesSent = sent, Details = $"Sent {sent} UDP payload bytes; {result.Details}" };
        }
    }
}
