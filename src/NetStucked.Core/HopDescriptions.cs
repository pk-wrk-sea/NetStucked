using System.Net;
using System.Net.Sockets;

namespace NetStucked.Core;

public static class HopDescriptionParser
{
    public static Dictionary<string, string> Parse(string text)
    {
        if (text.Length > 1_000_000) throw new ArgumentException("Description list exceeds 1 MB.");
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int lineNumber = 0;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } raw)
        {
            if (++lineNumber > 1024) throw new ArgumentException("Maximum 1,024 description lines.");
            var parts = raw.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var ip) || parts[0].Contains('%') ||
                ip.AddressFamily == AddressFamily.InterNetwork && !TargetInputParser.IsStrictIpv4(parts[0]) || parts[1].Length > 512)
                throw new ArgumentException($"Line {lineNumber}: use IP <space> Description (maximum 512 characters).");
            if (!result.TryAdd(ip.ToString(), parts[1])) throw new ArgumentException($"Line {lineNumber}: duplicate IP {ip}.");
        }
        return result;
    }
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var b = address.GetAddressBytes();
        if (b.Length == 4)
            return !(b[0] is 0 or 10 or 127 || b[0] >= 224 || b[0] == 100 && b[1] is >= 64 and <= 127 ||
                b[0] == 169 && b[1] == 254 || b[0] == 172 && b[1] is >= 16 and <= 31 ||
                b[0] == 192 && (b[1] == 168 || b[1] == 0 && (b[2] == 0 || b[2] == 2) || b[1] == 88 && b[2] == 99) ||
                b[0] == 198 && (b[1] is 18 or 19 || b[1] == 51 && b[2] == 100) || b[0] == 203 && b[1] == 0 && b[2] == 113);
        return (b[0] & 0xE0) == 0x20 && !(b[0] == 0x20 && b[1] == 1 && (b[2] == 0x0d && b[3] == 0xb8 || b[2] == 0));
    }
}

public sealed record WanIdentity(string Description, DateTimeOffset RetrievedAt);
public interface IWanIdentitySource { Task<WanIdentity?> LookupAsync(IPAddress address, CancellationToken token); }
