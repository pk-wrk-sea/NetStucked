using System.Text.Json;
using NetStucked.Core;
namespace NetStucked.Infrastructure;
public sealed class MonitorProbe(IIcmpProbe icmp, ITcpProbe tcp, IDnsResolver dns, ISnmpClient snmp, ISecretVault vault) : IMonitorProbe
{
    public async Task<MonitorObservation> PollAsync(MonitorDevice device, bool readSnmp, CancellationToken token)
    {
        bool? ping = null, connected = null; double? rtt = null; SnmpSnapshot? snapshot = null;
        string evidence;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(device.TimeoutMs);
            var address = await dns.ResolveAsync(device.Host, IpFamily.Auto, deadline.Token);
            if (device.Icmp) { var result = await icmp.SendAsync(address, device.TimeoutMs, 32, 128, token); ping = result.IsEchoReply; rtt = result.RttMs; }
            if (device.Tcp && ping != true) { var result = await tcp.ConnectAsync(address, device.TcpPort, device.TimeoutMs, 0, token); connected = result.Outcome == PortOutcome.Connected; }
            evidence = ping == true ? "ICMP reply" : connected == true ? "TCP connected; ICMP unavailable" : device.Icmp || device.Tcp ? "Configured ICMP/TCP checks did not respond" : "SNMP-only observation";
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { evidence = "Address resolution timed out; availability unknown"; }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or ArgumentException or InvalidOperationException) { evidence = "Address resolution/probe unavailable; device state unknown"; }
        if (readSnmp)
        {
            string? stored = vault.Read("snmp-" + device.Id.ToString("N"));
            SnmpSecrets? secret = stored is null ? null : JsonSerializer.Deserialize<SnmpSecrets>(stored);
            snapshot = secret is null ? new(DateTimeOffset.UtcNow, "Not configured", "", "", null, [], new Dictionary<string, string>(), "Protected SNMP credentials are missing") : await snmp.ReadAsync(device.Host, device.SnmpSettings, secret, device.TimeoutMs, false, token);
            evidence += "; SNMP " + snapshot.Status;
        }
        bool? reachable = ping == true || connected == true || snapshot?.Success == true ? true : ping is not null || connected is not null ? false : null;
        return new(DateTimeOffset.UtcNow, reachable, ping, rtt, connected, evidence, snapshot);
    }
}
