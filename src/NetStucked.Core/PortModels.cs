using System.Globalization;
using System.Net;

namespace NetStucked.Core;

public sealed record PortProbeSettings
{
    public const int MaxPacketSize = 1400;
    public bool Continuous { get; init; } = true;
    public int PacketSize { get; init; } = 32;
    public int IntervalMs { get; init; } = 1000;
    public int TimeoutMs { get; init; } = 2000;
    internal int Concurrency => 32;
    internal int MaxTargets => PortScanPlanner.MaxChecks;
    internal int DnsConcurrency => 4;
    internal int MaxPacketsPerSecond => 128;
    internal int DnsCacheSeconds => 30;
    internal int DnsRetrySeconds => 5;
    internal IpFamily AddressFamily => IpFamily.Auto;
    public void Validate()
    {
        if (IntervalMs is < 250 or > 60000 || TimeoutMs is < 500 or > 10000)
            throw new ArgumentException("Interval must be 250–60000 ms; timeout must be 500–10000 ms.");
        ValidatePacketSize(PacketSize);
    }
    public static void ValidatePacketSize(int size)
    {
        if (size is < 0 or > MaxPacketSize) throw new ArgumentOutOfRangeException(nameof(size), $"Packet size must be 0–{MaxPacketSize} payload bytes.");
    }
}

public enum PortProtocol { TCP, UDP }
public sealed record PortTarget(string Host, int Port, string Description)
{
    public PortProtocol Protocol { get; init; }
    public string Key => (Protocol == PortProtocol.UDP ? "udp:" : "") + $"{Host.ToLowerInvariant()}:{Port}";
    public string Endpoint => Host.Contains(':') ? $"[{Host}]:{Port}" : $"{Host}:{Port}";
}
public sealed record PortParseResult(IReadOnlyList<PortTarget> Targets, IReadOnlyList<string> Errors)
{
    public bool IsValid => Targets.Count > 0 && Errors.Count == 0;
}
public static class PortInputParser
{
    public static PortParseResult Parse(string text, int cap = 1024)
    {
        var targets = new List<PortTarget>(); var errors = new List<string>(); var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (text.Length > 1000000) return new([], ["Address list exceeds 1 MB."]);
        if (cap is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(cap));
        int lineNumber = 0;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } raw)
        {
            if (++lineNumber > 10000) { errors.Add("Address list exceeds 10000 lines."); break; }
            string line = raw.Trim(); if (line.Length == 0 || line.StartsWith('#')) continue;
            string[] parts = line.Split((char[]?)null, 3, StringSplitOptions.RemoveEmptyEntries);
            string host = parts[0], portText = "", description = "";
            int colon = host.LastIndexOf(':');
            bool combined = host.StartsWith('[') ? host.Contains("]:", StringComparison.Ordinal) : colon > 0 && host.IndexOf(':') == colon;
            if (combined)
            {
                portText = host[(colon + 1)..]; host = host[..colon];
                if (host.StartsWith('[') && host.EndsWith(']')) host = host[1..^1];
                description = string.Join(' ', parts.Skip(1));
            }
            else if (parts.Length >= 2) { portText = parts[1]; description = parts.Length == 3 ? parts[2] : ""; }
            if (!TargetInputParser.IsValidHost(host) || !int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out int port) || port is < 1 or > 65535 || description.Length > 512)
            { errors.Add($"Line {lineNumber}: use host:port or host port, followed by an optional description (port 1–65535). IPv6: [address]:port or address port."); continue; }
            host = IPAddress.TryParse(host, out var ip) ? ip.ToString() : host.TrimEnd('.').ToLowerInvariant();
            var target = new PortTarget(host, port, description);
            if (!keys.Add(target.Key)) continue;
            if (targets.Count >= cap) { errors.Add($"Maximum {cap} unique endpoints."); break; }
            targets.Add(target);
        }
        return new(targets, errors);
    }
}

public enum PortOutcome { Connected, Refused, Timeout, Unreachable, Error, Responded, Closed, NoResponse }
public sealed record TcpProbeResult(PortOutcome Outcome, double? ConnectMs, string Details)
{
    public int BytesSent { get; init; }
}
public interface ITcpProbe
{
    Task<TcpProbeResult> ConnectAsync(IPAddress address, int port, int timeoutMs, int packetSize, CancellationToken cancellationToken);
}
public interface IUdpProbe { Task<TcpProbeResult> ProbeAsync(IPAddress address, int port, int timeoutMs, int packetSize, CancellationToken cancellationToken); }
public sealed record PortSample(DateTimeOffset Time, long Sequence, string Status, double? ConnectMs, string Details);
public sealed record PortSnapshot(int Number, string Host, int Port, string Description, string? ResolvedIp, string Status,
    double? Last, double? Average, double? Minimum, double? Maximum, long Attempts, long Connected, double FailurePercent,
    DateTimeOffset? LastTest, DateTimeOffset? LastSuccess, string Details, long HistoryRevision)
{
    public PortProtocol Protocol { get; init; }
    public string Key => (Protocol == PortProtocol.UDP ? "udp:" : "") + $"{Host.ToLowerInvariant()}:{Port}";
    public string Endpoint => Host.Contains(':') ? $"[{Host}]:{Port}" : $"{Host}:{Port}";
    public long SessionId { get; init; }
    public long Revision { get; init; }
    public long PublishedTimestamp { get; init; }
}
public sealed record PortTotals(int Targets, int Connected, int Failed, long Attempts, long Successful)
{
    public int Closed { get; init; }
    public int NoResponse { get; init; }
    public double FailurePercent => Statistics.LossPercent(Attempts, Successful);
}
public sealed record PortUpdate(long SessionId, long Revision, bool Reset, IReadOnlyList<PortSnapshot> Rows, PortTotals Totals);

internal sealed class PortAccumulator(PortTarget target, int number, int retention)
{
    public PortTarget Target { get; } = target;
    public string? ResolvedIp { get; set; }
    private readonly SampleStatistics _stats = new();
    private readonly Queue<PortSample> _history = new();
    private string _status = "Unknown", _details = "";
    private DateTimeOffset? _lastTest, _lastSuccess;
    private double? _last;
    private long _sequence;
    public void Add(TcpProbeResult result)
    {
        _lastTest = DateTimeOffset.Now; _status = result.Outcome == PortOutcome.NoResponse ? "No response" : result.Outcome.ToString(); _details = result.Details;
        _last = result.Outcome is PortOutcome.Connected or PortOutcome.Responded ? result.ConnectMs : null;
        _stats.Add(_last);
        if (result.Outcome is PortOutcome.Connected or PortOutcome.Responded) _lastSuccess = _lastTest;
        Record();
    }
    public void DnsFailure(string error)
    {
        ResolvedIp = null; _status = "DNS error"; _details = error; _last = null; _lastTest = DateTimeOffset.Now;
        // A failed name lookup is not a TCP connection attempt.
        Record();
    }
    private void Record()
    {
        _history.Enqueue(new(_lastTest!.Value, ++_sequence, _status, _last, _details));
        while (_history.Count > retention) _history.Dequeue();
    }
    public IReadOnlyList<PortSample> History(int limit) => _history.Reverse().Take(limit).ToArray();
    public PortSnapshot Snapshot() => new(number, Target.Host, Target.Port, Target.Description, ResolvedIp, _status, _last,
        _stats.Average, _stats.Minimum, _stats.Maximum, _stats.Sent, _stats.Received, _stats.LossPercent, _lastTest, _lastSuccess, _details, _sequence) { Protocol = Target.Protocol };
}
