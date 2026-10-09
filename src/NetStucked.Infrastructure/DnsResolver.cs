using System.Net;
using System.Net.Sockets;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed class DnsResolver : IDnsResolver
{
    public async Task<IPAddress> ResolveAsync(string host, IpFamily family, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IPAddress[] addresses;
        if (IPAddress.TryParse(host, out var literal)) addresses = [literal];
        else
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            try { addresses = await Dns.GetHostAddressesAsync(host, timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new TimeoutException("Hostname resolution exceeded 3 seconds."); }
        }
        return addresses.Where(a => family == IpFamily.Auto || a.AddressFamily == (family == IpFamily.IPv4 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6))
            .OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).FirstOrDefault()
            ?? throw new SocketException((int)SocketError.HostNotFound);
    }

    public async Task<string?> ReverseAsync(IPAddress address, CancellationToken cancellationToken)
    {
        try
        {
            // .NET provides cancellation on the hostname overload; wait on the underlying OS resolution before reuse.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            var entry = await Dns.GetHostEntryAsync(address.ToString(), AddressFamily.Unspecified, timeout.Token).ConfigureAwait(false);
            return entry.HostName != address.ToString() ? entry.HostName : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (SocketException) { return null; }
    }
}
