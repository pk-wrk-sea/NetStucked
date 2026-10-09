using System.Net;
using System.Net.Sockets;

namespace NetStucked.Core;

public sealed record TargetDefinition(string Host, string Description, string? SourceCidr = null);
public sealed record InputError(int Line, string Message);
public sealed record TargetParseResult(IReadOnlyList<TargetDefinition> Targets, IReadOnlyList<InputError> Errors)
{
    public bool IsValid => Errors.Count == 0 && Targets.Count > 0;
}

public static class Ipv4CidrExpander
{
    public static (uint Network, ulong Count, int Prefix) Parse(string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2 || !TargetInputParser.IsStrictIpv4(parts[0]) || parts[1].Length == 0 || !parts[1].All(char.IsAsciiDigit) ||
            !int.TryParse(parts[1], out var prefix) || prefix is < 0 or > 32)
            throw new FormatException("Expected IPv4 CIDR with prefix 0–32.");
        var bytes = IPAddress.Parse(parts[0]).GetAddressBytes();
        uint value = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        uint mask = prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);
        ulong count = 1UL << (32 - prefix);
        return (value & mask, prefix <= 30 ? count - 2 : count, prefix);
    }

    public static IEnumerable<string> Expand(string cidr, int maxTargets)
    {
        var (network, count, prefix) = Parse(cidr);
        if (count > (ulong)maxTargets) throw new ArgumentException($"CIDR expands to {count:N0} hosts; limit is {maxTargets:N0}.");
        ulong first = (ulong)network + (prefix <= 30 ? 1UL : 0UL);
        for (ulong offset = 0; offset < count; offset++)
        {
            ulong value = first + offset;
            yield return $"{(value >> 24) & 255}.{(value >> 16) & 255}.{(value >> 8) & 255}.{value & 255}";
        }
    }
}

public static class TargetInputParser
{
    public static bool IsStrictIpv4(string value) => value.Split('.') is { Length: 4 } parts &&
        parts.All(p => p.Length is >= 1 and <= 3 && (p.Length == 1 || p[0] != '0') && p.All(char.IsAsciiDigit) && byte.TryParse(p, out _)) &&
        IPAddress.TryParse(value, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork;

    public static bool IsValidHost(string host)
    {
        if (host.Length is 0 or > 253 || host.Contains('/') || host.Contains('%') || host.EndsWith("..", StringComparison.Ordinal)) return false;
        if (IPAddress.TryParse(host, out var address))
            return address.AddressFamily == AddressFamily.InterNetworkV6 || IsStrictIpv4(host);
        if (host.All(c => char.IsAsciiDigit(c) || c == '.')) return false;
        return host.TrimEnd('.').Split('.').All(label => label.Length is >= 1 and <= 63 &&
            char.IsAsciiLetterOrDigit(label[0]) && char.IsAsciiLetterOrDigit(label[^1]) &&
            label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'));
    }

    public static TargetParseResult Parse(string text, int maxTargets = 1024)
    {
        if (maxTargets is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(maxTargets));
        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        if (normalized.Length > 1_000_000 || normalized.Count(c => c == '\n') >= 10000)
            return new([], [new(0, "Target text is limited to 1 MB and 10,000 lines.")]);
        var result = new List<TargetDefinition>();
        var errors = new List<InputError>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = normalized.Split('\n');
        for (var line = 0; line < lines.Length; line++)
        {
            string value = lines[line].Trim();
            if (value.Length == 0) continue;
            int split = 0;
            while (split < value.Length && !char.IsWhiteSpace(value[split])) split++;
            string host = value[..split];
            string description = value[split..].Trim();
            try
            {
                if (description.Length > 512) throw new ArgumentException("Descriptions are limited to 512 characters per line.");
                IEnumerable<string> hosts;
                if (host.Contains('/')) hosts = Ipv4CidrExpander.Expand(host, maxTargets);
                else if (!IsValidHost(host)) throw new FormatException($"Invalid IP address or hostname '{host}'.");
                else hosts = [IPAddress.TryParse(host, out var ip) ? ip.ToString() : host.TrimEnd('.')];
                foreach (string expanded in hosts)
                {
                    if (seen.Contains(expanded)) continue;
                    if (result.Count >= maxTargets) throw new ArgumentException($"Unique expanded target limit {maxTargets:N0} exceeded.");
                    seen.Add(expanded);
                    result.Add(new(expanded, description, host.Contains('/') ? host : null));
                }
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                errors.Add(new(line + 1, ex.Message));
            }
        }
        if (result.Count == 0 && errors.Count == 0) errors.Add(new(0, "Enter at least one target. Use only targets you are authorized to diagnose."));
        return new(result, errors);
    }
}
