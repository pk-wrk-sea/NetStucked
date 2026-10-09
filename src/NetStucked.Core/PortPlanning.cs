using System.Globalization;

namespace NetStucked.Core;

public static class PortScanPlanner
{
    public const int MaxHosts = 1024, MaxPorts = 64, DefaultChecks = 4096, MaxChecks = 16384;
    public static readonly IReadOnlyDictionary<string, string> StandardTemplates = new Dictionary<string, string>
    {
        ["Standard TCP ports"] = "21,22,25,53,80,110,143,443,445,3389",
        ["Standard UDP ports"] = "53,123,161", ["Web ports"] = "80,443", ["Remote access"] = "22,3389"
    };
    public static PortParseResult Parse(string addresses, string numbers, PortProtocol protocol, bool multiple)
    {
        var errors = new List<string>();
        if (!Enum.IsDefined(protocol)) return new([], ["Select TCP or UDP."]);
        if (numbers.Length > 2048) return new([], ["Port list is too long."]);
        var ports = new List<int>();
        foreach (string value in numbers.Split([',',' ','\t','\r','\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string[] range = value.Split('-');
            if (range.Length is < 1 or > 2 || !TryPort(range[0], out int first) || !TryPort(range[^1], out int last) || last < first)
            { errors.Add($"Invalid port '{value}'. Use 1–65535 or an ascending range such as 1000-1005."); continue; }
            if (last - first + 1 > MaxPorts) { errors.Add($"Select at most {MaxPorts} ports after expanding ranges."); continue; }
            for (int port = first; port <= last; port++)
            {
                if (ports.Contains(port)) continue;
                if (ports.Count == MaxPorts) { errors.Add($"Select at most {MaxPorts} ports after expanding ranges."); break; }
                ports.Add(port);
            }
        }
        if (ports.Count == 0) errors.Add("Enter at least one port.");
        if (ports.Count > MaxPorts) errors.Add($"Select at most {MaxPorts} ports.");
        var hosts = TargetInputParser.Parse(addresses, MaxHosts);
        errors.AddRange(hosts.Errors.Select(e => $"Line {e.Line}: {e.Message}"));
        if (!multiple && (hosts.Targets.Count > 1 || hosts.Targets.Any(t => t.SourceCidr is not null))) errors.Add("Choose Multiple IP Scan for more than one host or a CIDR.");
        long checks = (long)hosts.Targets.Count * ports.Count;
        if (checks > MaxChecks) errors.Add($"Scope has {checks:N0} checks; maximum {MaxChecks:N0}.");
        if (errors.Count > 0) return new([], errors);
        return new(hosts.Targets.SelectMany(host => ports.Select(port => new PortTarget(host.Host, port, host.Description) { Protocol = protocol })).ToArray(), []);
    }
    private static bool TryPort(string text, out int port) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is >= 1 and <= 65535;
}
