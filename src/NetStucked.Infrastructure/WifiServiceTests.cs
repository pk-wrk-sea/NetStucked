using System.Diagnostics;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed class WifiServiceTests : IWifiServiceTests
{
    public async Task<IReadOnlyList<ServiceTestResult>> RunAsync(WifiSnapshot context, ServiceTestProfile profile, CancellationToken token)
    {
        profile.Validate();
        if (context.ConnectedProfile is null || !context.Ip.HasUsableAddress) throw new InvalidOperationException("The selected Wi-Fi adapter must be connected with a usable IP address.");
        var work = new List<Task<ServiceTestResult>>();
        Task<ServiceTestResult> Run(string service, string target, Func<CancellationToken, Task<(double?, string)>> operation) => Check(service, target, profile.TimeoutMs, operation, token);
        if (profile.TestGateway)
        {
            var gateway = context.Ip.Gateways.FirstOrDefault(g => g.AddressFamily == AddressFamily.InterNetwork) ?? context.Ip.Gateways.FirstOrDefault();
            if (gateway is null) work.Add(Task.FromResult(new ServiceTestResult(DateTimeOffset.Now, "Gateway ICMP", "—", "SKIPPED", null, "No gateway was reported for the selected adapter.")));
            else work.Add(Run("Gateway ICMP", gateway.ToString(), async t =>
            {
                var source = gateway.IsIPv6LinkLocal ? context.Ip.Addresses.FirstOrDefault(a => a.IsIPv6LinkLocal) ?? throw new InvalidOperationException("No link-local IPv6 source on the selected adapter.") : Source(context.Ip, gateway.AddressFamily);
                if (gateway.IsIPv6LinkLocal && gateway.ScopeId == 0) gateway = new IPAddress(gateway.GetAddressBytes(), context.Ip.IPv6Index);
                double rtt = await SourcePingAsync(source, gateway, profile.TimeoutMs, t).ConfigureAwait(false);
                return (rtt, "ICMP echo reply; source " + source + "; Windows ICMP does not expose the actual egress interface");
            }));
        }
        if (profile.DnsName.Length > 0) work.Add(Run("DNS resolution", profile.DnsName, async t =>
        {
            var watch = Stopwatch.StartNew(); var addresses = await ResolveAsync(profile.DnsName, context.Ip, t).ConfigureAwait(false);
            return (watch.Elapsed.TotalMilliseconds, string.Join(", ", addresses) + "; selected adapter DNS");
        }));
        if (profile.TcpHost.Length > 0) work.Add(Run("TCP connect", $"{profile.TcpHost}:{profile.TcpPort}", async t =>
        {
            var addresses = await ResolveAsync(profile.TcpHost, context.Ip, t).ConfigureAwait(false);
            var watch = Stopwatch.StartNew(); using var socket = await ConnectSocketAsync(addresses, profile.TcpPort, context.Ip, t).ConfigureAwait(false);
            return (watch.Elapsed.TotalMilliseconds, "Connected from " + socket.LocalEndPoint + " to " + socket.RemoteEndPoint);
        }));
        if (profile.HttpUrl.Length > 0) work.Add(Run("HTTP response", profile.HttpUrl, async t =>
        {
            using var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false, UseProxy = false, MaxResponseHeadersLength = 32,
                ConnectCallback = async (connection, cancel) =>
                {
                    var addresses = await ResolveAsync(connection.DnsEndPoint.Host, context.Ip, cancel).ConfigureAwait(false);
                    var socket = await ConnectSocketAsync(addresses, connection.DnsEndPoint.Port, context.Ip, cancel).ConfigureAwait(false);
                    return new NetworkStream(socket, true);
                }
            };
            using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            var watch = Stopwatch.StartNew(); using var response = await client.GetAsync(profile.HttpUrl, HttpCompletionOption.ResponseHeadersRead, t).ConfigureAwait(false);
            double elapsed = watch.Elapsed.TotalMilliseconds;
            if ((int)response.StatusCode != profile.ExpectedHttpStatus) throw new MeasuredFailure(elapsed, $"HTTP {(int)response.StatusCode}; expected {profile.ExpectedHttpStatus}. Redirects are not followed (possible sign-in/portal). Direct adapter connection; no proxy.");
            return (elapsed, $"HTTP {(int)response.StatusCode}; {(new Uri(profile.HttpUrl).Scheme == "https" ? "TLS validation enabled" : "plain HTTP")}; direct selected-adapter connection (no proxy)");
        }));
        if (work.Count == 0) throw new ArgumentException("Configure at least one service check.");
        return await Task.WhenAll(work).ConfigureAwait(false); // At most four concurrent checks, one run per ViewModel.
    }
    private sealed class MeasuredFailure(double elapsed, string message) : IOException(message) { public double Elapsed { get; } = elapsed; }
    private static async Task<ServiceTestResult> Check(string service, string target, int timeout, Func<CancellationToken, Task<(double? Latency, string Message)>> action, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(timeout);
        try { var result = await action(deadline.Token).ConfigureAwait(false); token.ThrowIfCancellationRequested(); return new(DateTimeOffset.Now, service, target, "PASS", result.Latency, result.Message); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return new(DateTimeOffset.Now, service, target, "FAIL", null, "No completed response before timeout; this does not establish a service outage."); }
        catch (MeasuredFailure ex) { return new(DateTimeOffset.Now, service, target, "FAIL", ex.Elapsed, ex.Message); }
        catch (Exception ex) when (ex is SocketException or IOException or HttpRequestException or InvalidOperationException or Win32Exception) { return new(DateTimeOffset.Now, service, target, "FAIL", null, ex.Message); }
    }
    public static IPAddress Source(AdapterIpConfiguration ip, AddressFamily family) => ip.Addresses.FirstOrDefault(a => a.AddressFamily == family && !a.IsIPv6LinkLocal && !(family == AddressFamily.InterNetwork && a.GetAddressBytes()[0] == 169 && a.GetAddressBytes()[1] == 254)) ??
        ip.Addresses.FirstOrDefault(a => family == AddressFamily.InterNetworkV6 && a.IsIPv6LinkLocal) ?? throw new InvalidOperationException("No usable source address on the selected adapter for this destination family.");
    public static Socket BoundSocket(AdapterIpConfiguration ip, AddressFamily family, SocketType type, ProtocolType protocol)
    {
        var socket = new Socket(family, type, protocol);
        try
        {
            var source = Source(ip, family); int index = family == AddressFamily.InterNetwork ? ip.IPv4Index : ip.IPv6Index;
            if (index <= 0) throw new InvalidOperationException("Selected adapter interface index is unavailable.");
            // Winsock IP_UNICAST_IF / IPV6_UNICAST_IF = 31; .NET has no named enum member.
            if (OperatingSystem.IsWindows()) socket.SetSocketOption(family == AddressFamily.InterNetwork ? SocketOptionLevel.IP : SocketOptionLevel.IPv6, (SocketOptionName)31, family == AddressFamily.InterNetwork ? IPAddress.HostToNetworkOrder(index) : index);
            socket.Bind(new IPEndPoint(source, 0)); return socket;
        }
        catch { socket.Dispose(); throw; }
    }
    private static async Task<Socket> ConnectSocketAsync(IReadOnlyList<IPAddress> addresses, int port, AdapterIpConfiguration ip, CancellationToken token)
    {
        Exception? error = null;
        foreach (var address in addresses.Take(8))
        {
            token.ThrowIfCancellationRequested(); Socket? socket = null;
            try { socket = BoundSocket(ip, address.AddressFamily, SocketType.Stream, ProtocolType.Tcp); await socket.ConnectAsync(new IPEndPoint(address, port), token).ConfigureAwait(false); return socket; }
            catch (Exception ex) when (ex is SocketException or InvalidOperationException) { socket?.Dispose(); error = ex; }
            catch { socket?.Dispose(); throw; }
        }
        throw new IOException("No reachable resolved address through the selected adapter.", error);
    }
    private static async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, AdapterIpConfiguration ip, CancellationToken token)
    {
        if (IPAddress.TryParse(host, out var direct)) return [direct];
        Exception? error = null;
        foreach (var server in ip.DnsServers.Take(3))
        {
            var addresses = new List<IPAddress>();
            foreach (ushort type in new ushort[] { 1, 28 })
            {
                if (!ip.Addresses.Any(a => a.AddressFamily == (type == 1 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6) && !a.IsIPv6LinkLocal)) continue;
                token.ThrowIfCancellationRequested();
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(token); attempt.CancelAfter(750);
                try { addresses.AddRange(await WifiDnsQuery.QueryAsync(host, new IPEndPoint(server, 53), (f, s, p) => BoundSocket(ip, f, s, p), type, attempt.Token).ConfigureAwait(false)); }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { error = new IOException("Selected adapter DNS attempt timed out."); }
                catch (Exception ex) when (ex is SocketException or IOException or InvalidOperationException) { error = ex; }
            }
            if (addresses.Count > 0) return addresses.Distinct().ToArray();
        }
        throw new IOException("Selected adapter DNS resolution failed; global DNS fallback is disabled.", error);
    }
    private static Task<double> SourcePingAsync(IPAddress source, IPAddress destination, int timeout, CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        bool ipv6 = destination.AddressFamily == AddressFamily.InterNetworkV6;
        IntPtr handle = ipv6 ? Icmp6CreateFile() : IcmpCreateFile(); if (handle == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr reply = Marshal.AllocHGlobal(1024);
        try
        {
            byte[] payload = new byte[32]; var options = new IpOptions { Ttl = 64 };
            uint count;
            if (ipv6)
            {
                var src = new SockAddr6 { Family = 23, Address = source.GetAddressBytes(), Scope = (uint)source.ScopeId };
                var dst = new SockAddr6 { Family = 23, Address = destination.GetAddressBytes(), Scope = (uint)destination.ScopeId };
                count = Icmp6SendEcho2(handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref src, ref dst, payload, (ushort)payload.Length, IntPtr.Zero, reply, 1024, (uint)timeout);
            }
            else count = IcmpSendEcho2Ex(handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, BitConverter.ToUInt32(source.GetAddressBytes()), BitConverter.ToUInt32(destination.GetAddressBytes()), payload, (ushort)payload.Length, ref options, reply, 1024, (uint)timeout);
            token.ThrowIfCancellationRequested();
            if (count == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Gateway ICMP received no echo reply.");
            uint status, rtt;
            if (ipv6) { var result = Marshal.PtrToStructure<EchoReply6>(reply); status = result.Status; rtt = result.Rtt; }
            else { var result = Marshal.PtrToStructure<EchoReply>(reply); status = result.Status; rtt = result.Rtt; }
            if (status != 0) throw new IOException("Gateway ICMP: " + (IPStatus)status);
            return (double)rtt;
        }
        finally { Marshal.FreeHGlobal(reply); IcmpCloseHandle(handle); }
    }, token); // Native bounded call is awaited even after cancellation; no abandoned probe.
    [StructLayout(LayoutKind.Sequential)] private struct IpOptions { public byte Ttl, Tos, Flags, Size; public IntPtr Data; }
    [StructLayout(LayoutKind.Sequential)] private struct EchoReply { public uint Address, Status, Rtt; public ushort Size, Reserved; public IntPtr Data; public IpOptions Options; }
    [StructLayout(LayoutKind.Sequential)] private struct SockAddr6 { public ushort Family, Port; public uint Flow; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] Address; public uint Scope; }
    [StructLayout(LayoutKind.Sequential)] private struct Address6Ex { public ushort Port; public uint Flow; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] Address; public uint Scope; }
    [StructLayout(LayoutKind.Sequential)] private struct EchoReply6 { public Address6Ex Address; public uint Status, Rtt; }
    [DllImport("iphlpapi.dll", SetLastError = true)] private static extern IntPtr IcmpCreateFile();
    [DllImport("iphlpapi.dll", SetLastError = true)] private static extern IntPtr Icmp6CreateFile();
    [DllImport("iphlpapi.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IcmpCloseHandle(IntPtr handle);
    [DllImport("iphlpapi.dll", SetLastError = true)] private static extern uint IcmpSendEcho2Ex(IntPtr handle, IntPtr evt, IntPtr callback, IntPtr context, uint source, uint destination, byte[] data, ushort size, ref IpOptions options, IntPtr reply, uint replySize, uint timeout);
    [DllImport("iphlpapi.dll", SetLastError = true)] private static extern uint Icmp6SendEcho2(IntPtr handle, IntPtr evt, IntPtr callback, IntPtr context, ref SockAddr6 source, ref SockAddr6 destination, byte[] data, ushort size, IntPtr options, IntPtr reply, uint replySize, uint timeout);
}
