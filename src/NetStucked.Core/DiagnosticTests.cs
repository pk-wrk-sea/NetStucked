using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace NetStucked.Core;

public enum DnsRecordType : ushort { A = 1, CNAME = 5, PTR = 12, MX = 15, TXT = 16, AAAA = 28, SRV = 33 }
public sealed record DnsRecord(string Name, string Type, uint Ttl, string Value, string Section);
public sealed record DnsTestRequest(string Name, DnsRecordType Type, IPEndPoint Server, int TimeoutMs);
public sealed record DnsTestResult(DateTimeOffset Time, string Name, string Type, string Resolver, string Status, double? ElapsedMs, string Transport, IReadOnlyList<DnsRecord> Records, string Error)
{
    public string Answers => string.Join(" | ", Records.Where(r => r.Section == "Answer").Select(r => r.Value)).Truncate(2048);
    public int AnswerCount => Records.Count(r => r.Section == "Answer");
    public string Details => string.Join(Environment.NewLine, Records.Select(r => $"{r.Section}: {r.Name}  {r.Type}  TTL {r.Ttl}  {r.Value}")) + (Error.Length == 0 ? "" : Environment.NewLine + Error);
}
public interface IDnsTestClient { Task<DnsTestResult> QueryAsync(DnsTestRequest request, CancellationToken token); }
public sealed record HttpTestRequest(Uri Url, string Method, int TimeoutMs, bool FollowRedirects, int ExpectedStatus);
public sealed record HttpTestResult(DateTimeOffset Time, string Url, string Status, int? StatusCode, double? ElapsedMs, string RemoteAddress, string Redirect, string Tls, string CertificateExpiry, string Details, string Error);
public interface IHttpTestClient { Task<HttpTestResult> TestAsync(HttpTestRequest request, CancellationToken token); }

public static class DiagnosticInput
{
    public const int MaxDnsQueries = 512;
    public static string DnsName(string text, DnsRecordType type)
    {
        text = text.Trim();
        if (type == DnsRecordType.PTR && IPAddress.TryParse(text, out var ip))
            return ip.AddressFamily == AddressFamily.InterNetwork ? string.Join('.', ip.GetAddressBytes().Reverse()) + ".in-addr.arpa" : string.Join('.', Convert.ToHexString(ip.GetAddressBytes()).ToLowerInvariant().Reverse()) + ".ip6.arpa";
        string name = new IdnMapping().GetAscii(text.TrimEnd('.'));
        if (name.Length is < 1 or > 253 || name.Split('.').Any(l => l.Length is < 1 or > 63 || l.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))) throw new ArgumentException("Enter a valid DNS name (or an IP for PTR).");
        return name;
    }
    public static IPEndPoint DnsServer(string text)
    {
        text = text.Trim();
        if (text.StartsWith('[') && text.Contains("]:"))
        {
            if (IPEndPoint.TryParse(text, out var bracketed) && bracketed.Port is > 0 and <= 65535) return bracketed;
            throw new ArgumentException("Invalid bracketed DNS server endpoint.");
        }
        if (IPAddress.TryParse(text, out var address)) return new(address, 53);
        if (IPEndPoint.TryParse(text, out var endpoint) && endpoint.Port is > 0 and <= 65535) return endpoint;
        throw new ArgumentException("DNS servers must be IP addresses; an optional port is allowed (IP:port or [IPv6]:port).");
    }
    public static Uri HttpUrl(string text)
    {
        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.Host.Length == 0 || uri.UserInfo.Length > 0) throw new ArgumentException("Enter an HTTP or HTTPS URL without embedded credentials.");
        return uri;
    }
    public static string SafeUrl(Uri uri) => uri.GetLeftPart(UriPartial.Path) + (uri.Query.Length == 0 ? "" : "?[query omitted]");
    public static string[] Lines(string text, int maximum, bool caseSensitive = false)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase).ToArray();
        if (lines.Length == 0 || lines.Length > maximum) throw new ArgumentException($"Enter between 1 and {maximum} unique targets.");
        return lines;
    }
    public static string Truncate(this string value, int maximum) => value.Length <= maximum ? value : value[..maximum] + "…";
}
