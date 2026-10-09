namespace NetStucked.Core;

public sealed class SampleStatistics
{
    public long Sent { get; private set; }
    public long Received { get; private set; }
    public long Lost => Sent - Received;
    public double LossPercent => Statistics.LossPercent(Sent, Received);
    public double? Last { get; private set; }
    public double? Minimum { get; private set; }
    public double? Maximum { get; private set; }
    public double? Average => Received == 0 ? null : _sum / Received;
    public double? Jitter => _deltas == 0 ? null : _deltaSum / _deltas;
    public int ConsecutiveFailures { get; private set; }
    private double _sum, _deltaSum;
    private long _deltas;
    private double? _previous;

    public void Add(double? respondingRtt)
    {
        if (respondingRtt is double invalid && (!double.IsFinite(invalid) || invalid < 0))
            throw new ArgumentOutOfRangeException(nameof(respondingRtt));
        Sent++;
        Last = respondingRtt;
        if (respondingRtt is not double rtt) { ConsecutiveFailures++; return; }
        Received++;
        ConsecutiveFailures = 0;
        _sum += rtt;
        Minimum = Minimum is null ? rtt : Math.Min(Minimum.Value, rtt);
        Maximum = Maximum is null ? rtt : Math.Max(Maximum.Value, rtt);
        if (_previous is double previous) { _deltaSum += Math.Abs(previous - rtt); _deltas++; }
        _previous = rtt;
    }
}

internal sealed class TargetAccumulator(TargetDefinition target, int number, int retention)
{
    public TargetDefinition Target { get; } = target;
    public int Number { get; } = number;
    public SampleStatistics Stats { get; } = new();
    public string? ResolvedIp { get; set; }
    public string? Error { get; set; }
    private readonly Queue<PingSample> _history = new();
    private DateTimeOffset? _lastPing;
    private int? _ttl;
    private DateTimeOffset? _lastSuccess, _reachableSince, _unreachableSince;
    private bool? _responding;
    private string? _result;

    public void Add(ProbeResult reply, long sessionId = 0, long probeId = 0)
    {
        var now = DateTimeOffset.Now;
        Stats.Add(reply.IsEchoReply ? reply.RttMs : null);
        _lastPing = now;
        _ttl = reply.IsEchoReply ? reply.ReplyTtl : null;
        if (reply.IsEchoReply)
        {
            _lastSuccess = now;
            if (_responding != true) _reachableSince = now;
            _unreachableSince = null;
            _result = $"ICMP reply from {reply.Address?.ToString() ?? ResolvedIp}";
        }
        else
        {
            if (_responding != false) _unreachableSince = now;
            _reachableSince = null;
            _result = reply.Error ?? $"No ICMP reply ({reply.Status})";
        }
        _responding = reply.IsEchoReply;
        Error = reply.Error ?? (reply.IsEchoReply ? null : reply.Status.ToString());
        _history.Enqueue(new(now, Stats.Sent, reply.IsEchoReply ? "Reply" : reply.Status == System.Net.NetworkInformation.IPStatus.TimedOut ? "Timeout" : reply.Status.ToString(), Stats.Last, _ttl, Error) { SessionId = sessionId, ProbeId = probeId });
        while (_history.Count > retention) _history.Dequeue();
    }

    public IReadOnlyList<PingSample> History(int limit) => _history.Reverse().Take(limit).ToArray();
    public TargetSnapshot Snapshot(ProbeSettings settings)
    {
        string status = Stats.Sent == 0 || Error?.StartsWith("DNS:", StringComparison.Ordinal) == true ? "Unknown" : Stats.ConsecutiveFailures >= settings.FailureThreshold ? "Unreachable" :
            Stats.ConsecutiveFailures > 0 || Stats.Last >= settings.WarningRttMs || Stats.LossPercent >= settings.WarningLossPercent && Stats.Lost > 0 ? "Warn" : "Up";
        return new(Number, Target.Host, Target.Description, ResolvedIp, status, Stats.Last, Stats.Average, Stats.Minimum,
            Stats.Maximum, Stats.Sent, Stats.Received, Stats.Lost, Stats.LossPercent, _lastPing, Error, _ttl)
        { LastSuccess = _lastSuccess, ReachableSince = _reachableSince, UnreachableSince = _unreachableSince,
            ResultError = Error?.StartsWith("DNS:", StringComparison.Ordinal) == true ? Error : _result };
    }
}

internal sealed class HopAccumulator(int hop)
{
    public int Hop { get; } = hop;
    public SampleStatistics Stats { get; } = new();
    public string? Address { get; set; }
    public string? Hostname { get; set; }
    public string Description { get; set; } = "";
    public string Status { get; set; } = "Unknown";
    public int RouteChanges { get; set; }
    public DateTimeOffset? Updated { get; set; }
    public TraceHopSnapshot Snapshot() => new(Hop, Address, Hostname, Description, Status, Stats.Last, Stats.Minimum,
        Stats.Average, Stats.Maximum, Stats.Jitter, Stats.Sent, Stats.Received, Stats.LossPercent, RouteChanges, Updated);
}
