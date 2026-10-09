using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

/// <summary>A fresh socket for each handshake; sends no application payload.</summary>
public sealed class TcpPortProbe : ITcpProbe
{
    public async Task<TcpProbeResult> ConnectAsync(IPAddress address, int port, int timeoutMs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMs);
        long started = Stopwatch.GetTimestamp();
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), timeout.Token).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            return new(PortOutcome.Connected, elapsed, $"TCP connection established to {new IPEndPoint(address, port)}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(PortOutcome.Timeout, null, $"TCP connection exceeded {timeoutMs} ms."); }
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
            return new(outcome, null, $"{ex.SocketErrorCode} ({ex.ErrorCode}): {ex.Message}");
        }
    }
}
