using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

/// <summary>A generic UDP probe. Silence is deliberately inconclusive.</summary>
public sealed class UdpPortProbe : IUdpProbe
{
    public async Task<TcpProbeResult> ProbeAsync(IPAddress address, int port, int timeoutMs, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeoutMs);
        using var socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        long start = Stopwatch.GetTimestamp();
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), deadline.Token).ConfigureAwait(false);
            // An empty datagram makes no assumptions about the service's application protocol.
            await socket.SendAsync(ReadOnlyMemory<byte>.Empty, SocketFlags.None, deadline.Token).ConfigureAwait(false);
            var buffer = new byte[4096];
            int received = await socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, deadline.Token).ConfigureAwait(false);
            return new(PortOutcome.Responded, Stopwatch.GetElapsedTime(start).TotalMilliseconds, $"Received {received} UDP bytes.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(PortOutcome.NoResponse, null, "No UDP response; the port may be open, filtered, or silent."); }
        catch (SocketException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return ex.SocketErrorCode switch
            {
                SocketError.ConnectionReset or SocketError.ConnectionRefused => new(PortOutcome.Closed, null, "ICMP port unreachable was reported by the network stack."),
                SocketError.HostUnreachable or SocketError.NetworkUnreachable => new(PortOutcome.Unreachable, null, ex.Message),
                _ => new(PortOutcome.Error, null, ex.Message)
            };
        }
    }
}
