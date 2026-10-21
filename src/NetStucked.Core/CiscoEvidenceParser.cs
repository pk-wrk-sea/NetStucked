using System.Net;
using System.Text.RegularExpressions;

namespace NetStucked.Core;

// Conservative, line-preserving offline parser. Unrecognized formats remain unknown.
public static class CiscoEvidenceParser
{
    public const string Version = "cisco-text/2";
    private static Match Match(string s, string pattern) => Regex.Match(s, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
    public static EvidenceDocument Parse(string text, string source, string hash, string host, DateTimeOffset collected, bool complete, Guid? assetId, CancellationToken token = default)
    {
        if (text.Length > 8 * 1024 * 1024) throw new ArgumentException("Offline analysis limit is 8 MiB per transcript.");
        var facts = new List<DeviceFact>(); var neighbors = new List<NeighborFact>(); var warnings = new List<string>();
        string family = "Unknown", command = "Unknown", neighbor = "", address = "", local = "", remote = "", protocol = ""; int neighborLine = 0;
        void Add(string field, string value, int line) { if (facts.Count >= 4096) throw new ArgumentException("Fact limit exceeded."); if (value.Length > 0) facts.Add(new(field, value.Trim(), command, line, command == "Unknown" ? "Reported / command unknown" : "Observed")); }
        void Flush()
        {
            if (neighbor.Length > 0)
            {
                if (neighbors.Count >= 512) throw new ArgumentException("One-hop neighbor limit exceeded.");
                neighbors.Add(new(neighbor, address, local, remote, protocol, neighborLine));
            }
            // Even an unnamed/incomplete record must not contaminate the next command or neighbor.
            neighbor = address = local = remote = protocol = ""; neighborLine = 0;
        }
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            token.ThrowIfCancellationRequested(); string s = lines[i]; if (s.Length > 16384) { warnings.Add("Overlong line omitted: " + (i + 1)); if (warnings.Count > 100) throw new ArgumentException("Too many malformed lines."); continue; }
            var m = Match(s, @"(?:^|[>#]\s*|^--- )(show [a-z0-9 _./:-]+?)(?: ---)?\s*$");
            if (m.Success) { Flush(); command = m.Groups[1].Value.Trim().ToLowerInvariant(); }
            if (s.Contains("Cisco IOS XE Software", StringComparison.OrdinalIgnoreCase)) family = "IOS XE";
            else if (family == "Unknown" && s.Contains("Cisco IOS Software", StringComparison.OrdinalIgnoreCase)) family = "IOS";
            else if (s.Contains("Cisco Adaptive Security Appliance Software Version", StringComparison.OrdinalIgnoreCase)) family = "ASA";
            else if (s.Contains("Cisco Nexus Operating System", StringComparison.OrdinalIgnoreCase)) family = "NX-OS";
            m = Match(s, @"(?:Cisco IOS XE Software, Version|Cisco IOS Software.*?Version|Cisco Adaptive Security Appliance Software Version|NXOS: version|system:\s+version)\s+([^\s,]+)");
            if (m.Success) Add("Version", m.Groups[1].Value, i + 1);
            m = Match(s, @"^\s*hostname\s+(\S+)\s*$"); if (m.Success) Add("Hostname", m.Groups[1].Value, i + 1);
            m = Match(s, @"^\s*([^\s]+) uptime is (.+)$"); if (m.Success) { Add("Hostname", m.Groups[1].Value, i + 1); Add("Uptime", m.Groups[2].Value, i + 1); }
            m = Match(s, @"^\s*(?:Model [Nn]umber\s*:\s*|cisco\s+)([A-Za-z0-9-]+)(?:\s|$)"); if (m.Success) Add("Model", m.Groups[1].Value, i + 1);
            m = Match(s, @"^\s*(?:System serial number\s*:|Processor board ID)\s*(\S+)"); if (m.Success) Add("Serial", m.Groups[1].Value, i + 1);
            m = Match(s, @"PID:\s*([^,]+),.*SN:\s*(\S+)"); if (m.Success) { Add("Inventory PID", m.Groups[1].Value, i + 1); Add("Inventory serial", m.Groups[2].Value, i + 1); }
            m = Match(s, @"^\s*(\S+) is (up|down|administratively down), line protocol is (up|down)"); if (m.Success) Add("Interface", s.Trim(), i + 1);
            m = Match(s, @"^\s*interface (\S+)$"); if (m.Success) Add("Configured interface", m.Groups[1].Value, i + 1);
            if (Match(s, @"^\s*(ip route |ipv6 route |router (ospf|bgp)|vlan \d+|channel-group |switchport (mode|trunk|access))").Success) Add("Configuration", s.Trim(), i + 1);
            if (Match(s, @"^\s*(Gateway of last resort|[CSOBDRL* ]+\s+\d{1,3}(?:\.\d{1,3}){3}/\d+)").Success) Add("Route", s.Trim(), i + 1);
            bool cdp = command is "Unknown" or "show cdp neighbors detail", lldp = command is "Unknown" or "show lldp neighbors detail";
            if (cdp)
            {
                m = Match(s, @"^\s*Device ID:\s*(\S+)"); if (m.Success) { Flush(); neighbor = m.Groups[1].Value; protocol = "CDP"; neighborLine = i + 1; }
                m = Match(s, @"Interface:\s*([^,]+),\s*Port ID \(outgoing port\):\s*(\S+)"); if (m.Success && protocol == "CDP") { local = m.Groups[1].Value.Trim(); remote = m.Groups[2].Value; }
            }
            if (lldp)
            {
                m = Match(s, @"^\s*Local Intf:\s*(\S+)"); if (m.Success) { Flush(); protocol = "LLDP"; local = m.Groups[1].Value; }
                m = Match(s, @"^\s*System Name:\s*(\S+)"); if (m.Success) { if (protocol != "LLDP" || neighbor.Length > 0) Flush(); neighbor = m.Groups[1].Value; protocol = "LLDP"; neighborLine = i + 1; }
                m = Match(s, @"^\s*Port id:\s*(\S+)"); if (m.Success && protocol == "LLDP") remote = m.Groups[1].Value;
            }
            if (protocol.Length > 0 && (cdp || lldp))
            { m = Match(s, @"(?:IP address:|Management Address:)\s*(\S+)"); if (m.Success && IPAddress.TryParse(m.Groups[1].Value, out _)) address = m.Groups[1].Value; }
        }
        Flush();
        if (family == "Unknown") warnings.Add("OS family not recognized. No automatic product applicability.");
        if (!complete) warnings.Add("Partial capture: missing output cannot establish absence.");
        if (facts.Where(f => f.Field == "Version").Select(f => f.Value).Distinct().Count() > 1) warnings.Add("Conflicting version observations: engineer review required.");
        if (neighbors.Count == 0) warnings.Add("No supported CDP/LLDP detail records. This does not prove no neighbors.");
        return new(Guid.NewGuid(), assetId, host, source, hash, collected, Version, family, complete, facts.DistinctBy(f => (f.Field, f.Value, f.Command)).ToArray(), neighbors.ToArray(), warnings.ToArray());
    }
}

public sealed record TopologyLink(string Device, string Neighbor, string Address, string LocalInterface, string RemoteInterface, string Protocol,
    string Classification, string Freshness, string Source, string Sha256, int Line, DateTimeOffset CollectedAt, string Detail);
public static class TopologyProjection
{
    public static TopologyLink[] Build(EvidenceDocument selected, IReadOnlyList<EvidenceDocument> all, DateTimeOffset now) => selected.Neighbors.Select(n =>
    {
        var peers = all.Where(e => e.Id != selected.Id && (e.Host.Equals(n.Name, StringComparison.OrdinalIgnoreCase) || e.Host == n.Address || e.Facts.Any(f => f.Field == "Hostname" && f.Value.Equals(n.Name, StringComparison.OrdinalIgnoreCase)))).ToArray();
        bool reciprocal = peers.Length == 1 && peers.Any(e => e.Complete && e.Neighbors.Any(r => (r.Name.Equals(selected.Host, StringComparison.OrdinalIgnoreCase) || selected.Facts.Any(f => f.Field == "Hostname" && f.Value.Equals(r.Name, StringComparison.OrdinalIgnoreCase))) && r.LocalInterface.Equals(n.RemoteInterface, StringComparison.OrdinalIgnoreCase) && r.RemoteInterface.Equals(n.LocalInterface, StringComparison.OrdinalIgnoreCase) && r.LocalInterface.Length > 0 && r.RemoteInterface.Length > 0));
        bool conflict = selected.Neighbors.Any(r => r.LocalInterface.Equals(n.LocalInterface, StringComparison.OrdinalIgnoreCase) && !r.Name.Equals(n.Name, StringComparison.OrdinalIgnoreCase) && r.LocalInterface.Length > 0);
        bool addressConflict = peers.Any(e => IPAddress.TryParse(n.Address, out var reported) && IPAddress.TryParse(e.Host, out var captured) && !reported.Equals(captured));
        bool stale = now - selected.CollectedAt > TimeSpan.FromDays(1) || peers.Any(e => now - e.CollectedAt > TimeSpan.FromDays(1));
        return new TopologyLink(selected.Host, n.Name, n.Address, n.LocalInterface, n.RemoteInterface, n.Protocol,
            conflict || addressConflict || peers.Length > 1 ? "Needs review (conflicting identity)" : reciprocal && selected.Complete && !stale ? "Verified (reciprocal evidence)" : n.LocalInterface.Length > 0 && n.RemoteInterface.Length > 0 ? "Discovered (reported adjacency)" : "Unknown endpoints",
            stale ? "Stale >24h" : "Captured ≤24h", selected.Source, selected.Sha256, n.Line, selected.CollectedAt,
            "No uplink direction inferred. " + (peers.Length > 1 ? "Multiple matching peer identities; review ambiguity. " : "") + (addressConflict ? "Reported management address differs from the captured peer address; review identity. " : "") + (!selected.Complete ? "Partial capture. " : "") + "Inspect both endpoint evidence before physical verification.");
    }).ToArray();
}
