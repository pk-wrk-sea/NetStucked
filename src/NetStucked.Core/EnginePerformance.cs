using System.Diagnostics;

namespace NetStucked.Core;

public sealed record LatencySummary(long Count, double MeanMs, double P95Ms, double MaximumMs);
public sealed record EngineDiagnostics(long SessionId, long CompletedProbes, long CancelledProbes, long SkippedIntervals,
    int MaximumNetworkOperations, LatencySummary SchedulerLag, LatencySummary ProbeQueueWait,
    LatencySummary DnsDuration, LatencySummary ProbeDuration, LatencySummary UiLag, double? FirstDestinationMs);

/// <summary>Bounded internal timing samples. These never replace measured network RTTs.</summary>
internal sealed class EnginePerformance
{
    private readonly object _sync = new();
    private readonly Samples _scheduler = new(), _queue = new(), _dns = new(), _probe = new(), _ui = new();
    private long _session, _completed, _cancelled, _skipped;
    private int _active, _maximum;
    private double? _firstDestination;
    public void Reset(long session) { lock (_sync) { _session = session; _completed = _cancelled = _skipped = 0; _active = _maximum = 0; _firstDestination = null; foreach (var item in new[] { _scheduler, _queue, _dns, _probe, _ui }) item.Clear(); } }
    public void Schedule(double ms) { lock (_sync) _scheduler.Add(ms); }
    public void Queue(double ms) { lock (_sync) _queue.Add(ms); }
    public void Dns(double ms) { lock (_sync) _dns.Add(ms); }
    public void Probe(double ms, bool cancelled) { lock (_sync) { _probe.Add(ms); if (cancelled) _cancelled++; else _completed++; } }
    public void Ui(long published) { if (published > 0) { lock (_sync) _ui.Add(Stopwatch.GetElapsedTime(published).TotalMilliseconds); } }
    public void Skipped(long count) { lock (_sync) _skipped += count; }
    public void Enter() { lock (_sync) _maximum = Math.Max(_maximum, ++_active); }
    public void Leave() { lock (_sync) _active--; }
    public void Destination(double elapsedMs) { lock (_sync) _firstDestination ??= elapsedMs; }
    public EngineDiagnostics Snapshot() { lock (_sync) return new(_session, _completed, _cancelled, _skipped, _maximum, _scheduler.Summary(), _queue.Summary(), _dns.Summary(), _probe.Summary(), _ui.Summary(), _firstDestination); }
    private sealed class Samples
    {
        private readonly double[] _recent = new double[256];
        private long _count;
        private double _sum, _maximum;
        public void Clear() { _count = 0; _sum = _maximum = 0; }
        public void Add(double ms) { ms = Math.Max(0, ms); _recent[_count % _recent.Length] = ms; _count++; _sum += ms; _maximum = Math.Max(_maximum, ms); }
        public LatencySummary Summary()
        {
            int size = (int)Math.Min(_count, _recent.Length);
            var copy = _recent.AsSpan(0, size).ToArray(); Array.Sort(copy);
            return new(_count, _count == 0 ? 0 : _sum / _count, size == 0 ? 0 : copy[(int)Math.Ceiling(size * .95) - 1], _maximum);
        }
    }
}

/// <summary>Cancellable token bucket. Waiting does not reserve future tokens.</summary>
internal sealed class PacketRateBudget(int rate, int burst) : IDisposable
{
    private readonly SemaphoreSlim _turn = new(1, 1);
    private double _tokens = burst;
    private long _updated = Stopwatch.GetTimestamp();
    public async ValueTask WaitAsync(CancellationToken token)
    {
        await _turn.WaitAsync(token).ConfigureAwait(false);
        try
        {
            while (true)
            {
                long now = Stopwatch.GetTimestamp();
                _tokens = Math.Min(burst, _tokens + Stopwatch.GetElapsedTime(_updated, now).TotalSeconds * rate); _updated = now;
                if (_tokens >= 1) { _tokens--; return; }
                await Task.Delay(TimeSpan.FromSeconds((1 - _tokens) / rate), token).ConfigureAwait(false);
            }
        }
        finally { _turn.Release(); }
    }
    public void Dispose() => _turn.Dispose();
}

public sealed record PingTotals(int Targets, int Reachable, int Unreachable, long Sent, long Received)
{
    public double LossPercent => Statistics.LossPercent(Sent, Received);
}
public sealed record PingUpdate(long SessionId, long Revision, bool Reset, IReadOnlyList<TargetSnapshot> Rows, PingTotals Totals);
public sealed record TraceUpdate(long SessionId, long Revision, bool Reset, IReadOnlyList<TraceHopSnapshot> Rows,
    IReadOnlyList<int> ActiveHops, IReadOnlyList<TraceEvent> Events);
