using System.Net;
using System.Net.NetworkInformation;

namespace NetStucked.Core;

public enum SessionState { Idle, Starting, Running, Pausing, Paused, Stopping, Stopped, Error }
public enum IpFamily { Auto, IPv4, IPv6 }

public sealed record ProbeSettings
{
    public int IntervalMs { get; init; } = 1000;
    public int TimeoutMs { get; init; } = 2000;
    public int PacketSize { get; init; } = 32;
    public int Concurrency { get; init; } = 32;
    public int MaxTargets { get; init; } = 1024;
    public int FailureThreshold { get; init; } = 3;
    public double WarningRttMs { get; init; } = 100;
    public double WarningLossPercent { get; init; } = 5;
    public IpFamily AddressFamily { get; init; } = IpFamily.Auto;
    public int DnsConcurrency { get; init; } = 4;
    public int MaxPacketsPerSecond { get; init; } = 128;
    public int DnsCacheSeconds { get; init; } = 30;
    public int DnsRetrySeconds { get; init; } = 5;

    public void Validate()
    {
        if (IntervalMs is < 250 or > 60000 || TimeoutMs is < 500 or > 10000 ||
            PacketSize is < 1 or > 1400 || Concurrency is < 1 or > 64 || MaxTargets is < 1 or > 4096 ||
            FailureThreshold is < 1 or > 20 || WarningRttMs is < 1 or > 10000 ||
            WarningLossPercent is < 0 or > 100 || !Enum.IsDefined(AddressFamily) ||
            !double.IsFinite(WarningRttMs) || !double.IsFinite(WarningLossPercent) ||
            DnsConcurrency is < 1 or > 16 || MaxPacketsPerSecond is < 1 or > 2048 ||
            DnsCacheSeconds is < 1 or > 3600 || DnsRetrySeconds is < 1 or > 60)
            throw new ArgumentException("Invalid probe settings. Interval 250–60000ms; timeout 500–10000ms; payload 1–1400; concurrency 1–64; target cap 1–4096; failures 1–20; warning RTT 1–10000ms; loss 0–100%; ICMP rate 1–2048/s; DNS workers 1–16; cache 1–3600s; DNS retry 1–60s.");
    }
}

public sealed record TraceSettings
{
    public int MaxHops { get; init; } = 30;
    public int TimeoutMs { get; init; } = 1500;
    public int IntervalMs { get; init; } = 1000;
    public int ProbesPerHop { get; init; } = 1;
    public int PacketSize { get; init; } = 32;
    public bool Continuous { get; init; } = true;
    public bool ReverseDns { get; init; }
    public int HopConcurrency { get; init; } = 32;
    public int MaxPacketsPerSecond { get; init; } = 128;
    public bool CheckHopTcp { get; init; }
    public string HopTcpPorts { get; init; } = "22,23,80,443";
    public bool AdaptivePolling { get; init; }
    public int FullSweepIntervalMs { get; init; } = 30000;

    public int[] ParseTcpPorts()
    {
        var parsed = PortScanPlanner.Parse("127.0.0.1", HopTcpPorts, PortProtocol.TCP, false);
        if (!parsed.IsValid || parsed.Targets.Count > 16) throw new ArgumentException("TCP hop checks require 1–16 unique ports (1–65535); comma-separated ports or inclusive ranges.");
        return parsed.Targets.Select(t => t.Port).ToArray();
    }
    public void Validate()
    {
        if (HopTcpPorts is null || HopTcpPorts.Length > 2048) throw new ArgumentException("TCP hop port text must be present and at most 2048 characters.");
        if (CheckHopTcp) _ = ParseTcpPorts();
        if (MaxHops is < 1 or > 64 || TimeoutMs is < 500 or > 10000 ||
            IntervalMs is < 250 or > 300000 || ProbesPerHop is < 1 or > 3 || PacketSize is < 1 or > 1400 ||
            HopConcurrency is < 1 or > 32 || MaxPacketsPerSecond is < 1 or > 512 || FullSweepIntervalMs is < 1000 or > 300000)
            throw new ArgumentException("Invalid trace settings. Hops 1–64; timeout 500–10000ms; hop interval 250–300000ms; probes 1–3; payload 1–1400; TTL concurrency 1–32; ICMP rate 1–512/s; full sweep 1000–300000ms. Protocol is ICMP, IPv4 only.");
    }
}

public sealed record ProbeResult(IPStatus Status, IPAddress? Address, double? RttMs, int? ReplyTtl = null, string? Error = null)
{
    public bool IsEchoReply => Status == IPStatus.Success;
    public bool IsHopReply => Address is not null && (Status is IPStatus.Success or IPStatus.TtlExpired or IPStatus.TtlReassemblyTimeExceeded || IsTerminal);
    public bool IsTerminal => Status is IPStatus.DestinationHostUnreachable or IPStatus.DestinationNetworkUnreachable or
        IPStatus.DestinationUnreachable or IPStatus.DestinationPortUnreachable or IPStatus.DestinationProtocolUnreachable;
}

public interface IIcmpProbe
{
    Task<ProbeResult> SendAsync(IPAddress address, int timeoutMs, int packetSize, int ttl, CancellationToken cancellationToken);
    Task<ProbeResult> SendTraceAsync(IPAddress address, int timeoutMs, int packetSize, int ttl, CancellationToken cancellationToken)
        => SendAsync(address, timeoutMs, packetSize, ttl, cancellationToken);
}

public interface IDnsResolver
{
    Task<IPAddress> ResolveAsync(string host, IpFamily family, CancellationToken cancellationToken);
    Task<string?> ReverseAsync(IPAddress address, CancellationToken cancellationToken);
}

public sealed record PingSample(DateTimeOffset Time, long Sequence, string Result, double? LatencyMs, int? Ttl, string? Details)
{
    public string Host { get; init; } = "";
    public string? ReplyIpAddress { get; init; }
    public string Status => Result == "Reply" ? "Up" : "Unreachable";
    public long SessionId { get; init; }
    public long ProbeId { get; init; }
}
public sealed record TargetSnapshot(int Number, string Host, string Description, string? ResolvedIp, string Status,
    double? Last, double? Average, double? Minimum, double? Maximum, long Sent, long Received, long Lost,
    double LossPercent, DateTimeOffset? LastPing, string? Error, int? Ttl)
{
    public string? ReplyIpAddress { get; init; }
    public string? WarningReason { get; init; }
    public DateTimeOffset? LastSuccess { get; init; }
    public DateTimeOffset? ReachableSince { get; init; }
    public DateTimeOffset? UnreachableSince { get; init; }
    public string? ResultError { get; init; }
    public long SessionId { get; init; }
    public long Revision { get; init; }
    public long PublishedTimestamp { get; init; }
}
public sealed record TraceHopSnapshot(int Hop, string? Address, string? Hostname, string Description, string Status,
    double? Last, double? Best, double? Average, double? Worst, double? Jitter, long Sent, long Received,
    double LossPercent, int RouteChanges, DateTimeOffset? Updated)
{
    public string TcpOpenPorts { get; init; } = "";
    public string TcpCheckStatus { get; init; } = "Disabled";
    public DateTimeOffset? TcpCheckedAt { get; init; }
    public long SessionId { get; init; }
    public long CycleId { get; init; }
    public long ProbeId { get; init; }
    public long Revision { get; init; }
    public long PublishedTimestamp { get; init; }
}
public sealed record TraceEvent(DateTimeOffset Time, string Type, int? Hop, string Message)
{
    public long SessionId { get; init; }
    public long Sequence { get; init; }
    public long CycleId { get; init; }
}
