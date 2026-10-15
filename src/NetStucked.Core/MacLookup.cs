using System.Globalization;
using System.Text.RegularExpressions;

namespace NetStucked.Core;

public sealed record MacAddressInfo(string Address, string Oui, string AddressType, bool CanIdentifyVendor);
public sealed record MacVendorResult(DateTimeOffset Time, string Address, string Oui, string? Vendor, string AddressType, string Status, string Details);
public interface IMacVendorLookup
{
    Task<MacVendorResult> LookupAsync(MacAddressInfo address, int timeoutMs, CancellationToken token);
}
public static partial class MacAddressParser
{
    [GeneratedRegex(@"^(?:[0-9a-fA-F]{12}|(?:[0-9a-fA-F]{2}:){5}[0-9a-fA-F]{2}|(?:[0-9a-fA-F]{2}-){5}[0-9a-fA-F]{2}|(?:[0-9a-fA-F]{4}\.){2}[0-9a-fA-F]{4})$")]
    private static partial Regex Format();
    public static MacAddressInfo Parse(string input)
    {
        input = input.Trim();
        if (input.Length > 17 || !Format().IsMatch(input)) throw new ArgumentException("Enter a 48-bit MAC address in colon, dash, Cisco dotted or plain hexadecimal format.");
        string hex = new(input.Where(char.IsAsciiHexDigit).Select(char.ToUpperInvariant).ToArray());
        byte first = byte.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        bool multicast = (first & 1) != 0, local = (first & 2) != 0;
        string kind = hex == "000000000000" ? "Unspecified" : hex == "FFFFFFFFFFFF" ? "Broadcast" : multicast ? "Multicast" : local ? "Locally administered" : "Universal unicast";
        string normalized = string.Join(':', Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
        return new(normalized, normalized[..8], kind, kind == "Universal unicast");
    }
}
