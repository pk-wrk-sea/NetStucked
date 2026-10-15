using System.Globalization;
using System.Numerics;

namespace NetStucked.Core;

public enum SubnetInputState { Incomplete, Invalid, Valid }
public sealed record SubnetEvaluation(SubnetInputState State, string Message, Ipv4Subnet? Subnet = null);
public sealed record Ipv4Subnet(string Address, int Prefix, string Network, string Mask, string Wildcard,
    string? Broadcast, string FirstUsable, string LastUsable, ulong TotalAddresses, ulong UsableAddresses)
{
    public string NetworkCidr => $"{Network}/{Prefix}";
}

public static class SubnetCalculator
{
    public static SubnetEvaluation Evaluate(string? input)
    {
        string text = (input ?? "").Trim();
        if (text.Length == 0) return Incomplete("Enter an IPv4 address and /prefix or subnet mask.");
        if (text.Length > 128) return Invalid("Input is limited to 128 characters.");
        string address, suffix;
        bool cidr = text.Contains('/');
        if (cidr)
        {
            string[] pieces = text.Split('/');
            if (pieces.Length != 2) return Invalid("Use one slash followed by a prefix from 0 to 32.");
            address = pieces[0].Trim(); suffix = pieces[1].Trim();
        }
        else
        {
            string[] pieces = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length > 2) return Invalid("Use IPv4 /prefix or IPv4 subnet-mask.");
            address = pieces[0]; suffix = pieces.Length == 2 ? pieces[1] : "";
        }
        // A period between the complete IP and mask is accepted as a pasted separator.
        if (address.EndsWith('.') && TargetInputParser.IsStrictIpv4(address[..^1])) address = address[..^1];
        var addressState = AddressState(address);
        if (addressState == SubnetInputState.Invalid) return Invalid("IPv4 octets must be 0–255, without leading zeros.");
        int prefix;
        if (suffix.Length == 0) return Incomplete(addressState == SubnetInputState.Valid
            ? "Add /prefix (0–32) or a dotted subnet mask." : "IPv4 address is not complete.");
        if (cidr)
        {
            // Also tolerate one terminal sentence period after a complete prefix.
            if (suffix.EndsWith('.') && suffix[..^1].All(char.IsAsciiDigit)) suffix = suffix[..^1];
            if (suffix.Length == 0) return Incomplete("Enter the CIDR prefix.");
            if (!suffix.All(char.IsAsciiDigit) || (suffix.Length > 1 && suffix[0] == '0') ||
                !int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out prefix) || prefix > 32)
                return Invalid("CIDR prefix must be a whole number from 0 to 32.");
        }
        else
        {
            var maskState = AddressState(suffix);
            if (maskState == SubnetInputState.Invalid) return Invalid("Subnet mask must contain four octets from 0 to 255.");
            if (maskState == SubnetInputState.Incomplete) return Incomplete("Subnet mask is not complete.");
            uint maskValue = Number(suffix);
            ulong inverse = ~maskValue;
            if ((inverse & (inverse + 1)) != 0) return Invalid("Subnet mask must have contiguous network bits (for example 255.255.254.0).");
            prefix = BitOperations.LeadingZeroCount(~maskValue);
        }
        if (addressState == SubnetInputState.Incomplete) return Incomplete("IPv4 address is not complete.");
        uint value = Number(address);
        uint mask = prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);
        uint wildcard = ~mask, network = value & mask, last = network | wildcard;
        ulong total = 1UL << (32 - prefix), usable = prefix >= 31 ? total : total - 2;
        var subnet = new Ipv4Subnet(Text(value), prefix, Text(network), Text(mask), Text(wildcard),
            prefix >= 31 ? null : Text(last), Text(prefix >= 31 ? network : network + 1),
            Text(prefix >= 31 ? last : last - 1), total, usable);
        return new(SubnetInputState.Valid, $"Valid syntax · {subnet.NetworkCidr}", subnet);
    }
    private static SubnetEvaluation Incomplete(string message) => new(SubnetInputState.Incomplete, "Input incomplete: " + message);
    private static SubnetEvaluation Invalid(string message) => new(SubnetInputState.Invalid, "Syntax error: " + message);
    private static SubnetInputState AddressState(string address)
    {
        string[] parts = address.Split('.');
        if (parts.Length > 4) return SubnetInputState.Invalid;
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (part.Length == 0) return i == parts.Length - 1 ? SubnetInputState.Incomplete : SubnetInputState.Invalid;
            if (part.Length > 3 || !part.All(char.IsAsciiDigit) || (part.Length > 1 && part[0] == '0') ||
                !byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _)) return SubnetInputState.Invalid;
        }
        return parts.Length == 4 ? SubnetInputState.Valid : SubnetInputState.Incomplete;
    }
    private static uint Number(string address) => address.Split('.').Aggregate(0u, (value, octet) => (value << 8) | byte.Parse(octet, CultureInfo.InvariantCulture));
    private static string Text(uint value) => $"{value >> 24}.{(value >> 16) & 255}.{(value >> 8) & 255}.{value & 255}";
}
