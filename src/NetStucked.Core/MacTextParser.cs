using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace NetStucked.Core;

public sealed record MacInputRow(MacAddressInfo Mac, string IpAddress, int SourceLine);
public static partial class MacTextParser
{
    [GeneratedRegex(@"(?<![a-zA-Z0-9:.-])(?:[a-fA-F0-9]{2}(?:[:-][a-fA-F0-9]{2}){5}|[a-fA-F0-9]{4}(?:\.[a-fA-F0-9]{4}){2}|[a-fA-F0-9]{12})(?![a-zA-Z0-9:.-])")]
    private static partial Regex Candidate();
    public static IReadOnlyList<MacInputRow> Parse(string text)
    {
        if (text.Length > 65536) throw new ArgumentException("Pasted text exceeds 64 KiB.");
        var rows = new List<MacInputRow>(); int lineNumber = 0;
        foreach (string line in text.Replace("\r", "").Split('\n'))
        {
            lineNumber++;
            var matches = Candidate().Matches(line);
            if (matches.Count == 0) continue;
            string withoutMacs = Candidate().Replace(line, " ");
            var ips = withoutMacs.Split([' ', '\t', ',', ';', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => StrictIp(s.Trim('[', ']', '"'))).Where(s => s is not null).Distinct().ToArray();
            string mapping = matches.Count > 1 && ips.Length > 0 ? "Ambiguous: multiple MACs in this line" : ips.Length == 1 ? ips[0]! : ips.Length > 1 ? "Multiple IPs: " + string.Join("; ", ips) : "";
            foreach (Match match in matches)
            {
                rows.Add(new(MacAddressParser.Parse(match.Value), mapping, lineNumber));
                if (rows.Count > 512) throw new ArgumentException("Maximum 512 detected MAC rows per check.");
            }
        }
        if (rows.Count == 0) throw new ArgumentException("No supported MAC address was found in the pasted text.");
        if (rows.Select(r => r.Mac.Address).Distinct().Count() > 128) throw new ArgumentException("Maximum 128 different MAC addresses per lookup.");
        return rows;
    }
    private static string? StrictIp(string text)
    {
        if (text.Contains(':')) return IPAddress.TryParse(text, out var ipv6) && ipv6.AddressFamily == AddressFamily.InterNetworkV6 ? ipv6.ToString() : null;
        string[] parts = text.Split('.');
        if (parts.Length != 4 || parts.Any(p => p.Length is < 1 or > 3 || !p.All(char.IsAsciiDigit) || !byte.TryParse(p, out _))) return null;
        return string.Join('.', parts.Select(p => byte.Parse(p)));
    }
}
