using System.Net;
using System.Net.Sockets;

namespace NetStucked.Core;

public static class DnsServerSelection
{
    public static string[] Filter(IEnumerable<IPAddress> addresses, bool includeIpv6) => addresses
        .Where(a => a.AddressFamily == AddressFamily.InterNetwork || includeIpv6 && a.AddressFamily == AddressFamily.InterNetworkV6)
        .Distinct().Take(4).Select(a => a.ToString()).ToArray();
}
