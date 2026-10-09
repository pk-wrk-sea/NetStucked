using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

/// <summary>A fresh socket for each handshake and optional zero-filled payload send.</summary>
public sealed class TcpPortProbe : ITcpProbe
{
    public Task<TcpProbeResult> ConnectAsync(IPAddress address, int port, int timeoutMs, CancellationToken cancellationToken)
        => ConnectAsync(address, port, timeoutMs, 0, cancellationToken);
    public async Task<TcpProbeResult> ConnectAsync(IPAddress address, int port, int timeoutMs, int packetSize, CancellationToken cancellationToken)
    {
        PortProbeSettings.ValidatePacketSize(packetSize);
        cancellationToken.ThrowIfCancellationRequested();
        using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMs);
        long started = Stopwatch.GetTimestamp();
        bool connected = false; int sent = 0;
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), timeout.Token).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            connected = true;
            var payload = new byte[packetSize];
            while (sent < payload.Length)
            {
                int count = await socket.SendAsync(payload.AsMemory(sent), SocketFlags.None, timeout.Token).ConfigureAwait(false);
                if (count == 0) throw new SocketException((int)SocketError.ConnectionReset);
                sent += count;
            }
            return new(PortOutcome.Connected, elapsed, $"TCP connected; sent {sent} payload bytes to {new IPEndPoint(address, port)}. Send completion does not confirm an application response.") { BytesSent = sent };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(PortOutcome.Timeout, null, connected ? $"TCP connected; payload send exceeded {timeoutMs} ms after {sent} bytes." : $"TCP connection exceeded {timeoutMs} ms.") { BytesSent = sent }; }
        catch (SocketException ex)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = ex.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => PortOutcome.Refused,
                SocketError.TimedOut => PortOutcome.Timeout,
                SocketError.HostUnreachable or SocketError.NetworkUnreachable or SocketError.NetworkDown => PortOutcome.Unreachable,
                _ => PortOutcome.Error
            };
            return new(connected ? PortOutcome.Error : outcome, null, connected
                ? $"TCP connected; payload send failed after {sent} bytes: {ex.SocketErrorCode} ({ex.ErrorCode}): {ex.Message}"
                : $"{ex.SocketErrorCode} ({ex.ErrorCode}): {ex.Message}") { BytesSent = sent };
        }
    }
}
