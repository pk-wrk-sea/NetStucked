using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed class IcmpPingProbe : IIcmpProbe, IDisposable
{
    private readonly SemaphoreSlim _applicationSlots = new(128, 128);
    public async Task<ProbeResult> SendAsync(IPAddress address, int timeoutMs, int packetSize, int ttl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _applicationSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await SendCoreAsync(address, timeoutMs, packetSize, ttl, cancellationToken).ConfigureAwait(false); }
        finally { _applicationSlots.Release(); }
    }
    private static async Task<ProbeResult> SendCoreAsync(IPAddress address, int timeoutMs, int packetSize, int ttl, CancellationToken cancellationToken)
    {
        using var ping = new Ping();
        var timer = Stopwatch.StartNew();
        try
        {
            PingReply reply = await ping.SendPingAsync(address, TimeSpan.FromMilliseconds(timeoutMs),
                new byte[packetSize], new PingOptions(ttl, false), cancellationToken).ConfigureAwait(false);
            timer.Stop();
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = new ProbeResult(reply.Status, reply.Address, null);
            double? rtt = reply.Status == IPStatus.Success ? reply.RoundtripTime :
                outcome.IsHopReply ? timer.Elapsed.TotalMilliseconds : null;
            return new(reply.Status, reply.Address, rtt, reply.Status == IPStatus.Success ? reply.Options?.Ttl : null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is PingException or System.Net.Sockets.SocketException or InvalidOperationException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(IPStatus.Unknown, null, null, Error: ex.InnerException?.Message ?? ex.Message);
        }
    }
    public void Dispose() => _applicationSlots.Dispose();
}
