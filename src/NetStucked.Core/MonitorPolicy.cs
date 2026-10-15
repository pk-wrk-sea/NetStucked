namespace NetStucked.Core;

public sealed class MonitorPolicy
{
    private int _failures, _successes, _warningCount, _warningRecovery;
    private long _available, _observed;
    private readonly Queue<bool> _ping = new();
    private DateTimeOffset? _firstFailure;
    private MonitorIncident? _down, _warning;
    private SnmpSnapshot? _previous;
    private SnmpSnapshot? _latestSnmp;
    private IReadOnlyList<InterfaceHealth> _lastInterfaces = [];
    private bool _warningFromSnmp;
    private readonly Dictionary<int, int> _changes = [];
    public MonitorPolicy(IEnumerable<MonitorIncident>? previous = null)
    {
        _down = previous?.FirstOrDefault(i => i.Kind == "Reachability" && i.EndedAt is null);
        _warning = previous?.FirstOrDefault(i => i.Kind == "Health" && i.EndedAt is null);
        _warningFromSnmp = _warning?.Details.StartsWith("[SNMP]", StringComparison.Ordinal) == true;
    }
    public (MonitorSnapshot Snapshot, IReadOnlyList<MonitorIncident> Changes) Apply(MonitorDevice device, MonitorObservation observation)
    {
        var incidents = new List<MonitorIncident>();
        var interfaces = new List<InterfaceHealth>();
        if (observation.Snmp is not null) _latestSnmp = observation.Snmp;
        if (observation.PingReply is bool reply) { _ping.Enqueue(reply); while (_ping.Count > 20) _ping.Dequeue(); }
        if (observation.Reachable is bool reachable) { _observed++; if (reachable) _available++; }
        if (observation.Snmp?.Success == true)
        {
            foreach (var row in observation.Snmp.Interfaces)
            {
                var previous = _previous?.Interfaces.FirstOrDefault(i => i.Index == row.Index);
                bool baseline = previous is not null && _previous!.UptimeTicks <= observation.Snmp.UptimeTicks && previous.Discontinuity == row.Discontinuity && previous.Counter64 == row.Counter64;
                double seconds = _previous is null ? 0 : (observation.Snmp.Time - _previous.Time).TotalSeconds;
                bool rateSafe = row.Counter64 || row.SpeedBits is > 0 && row.SpeedBits.Value * seconds / 8 < (1UL << 32);
                double? Rate(ulong? now, ulong? old) => baseline && rateSafe && seconds > 0 && now is not null && old is not null && (!row.Counter64 || now >= old) ? Delta(now.Value, old.Value, row.Counter64) * 8 / seconds / 1_000_000d : null;
                double? incoming = Rate(row.InOctets, previous?.InOctets), outgoing = Rate(row.OutOctets, previous?.OutOctets);
                double? utilization = row.SpeedBits is > 0 && incoming is not null && outgoing is not null ? Math.Max(incoming.Value, outgoing.Value) * 1_000_000 / row.SpeedBits * 100 : null;
                ulong? Count(ulong? now, ulong? old) => baseline && now is not null && old is not null ? Delta(now.Value, old.Value, false) : null;
                ulong? errors = Sum(Count(row.InErrors, previous?.InErrors), Count(row.OutErrors, previous?.OutErrors));
                ulong? discards = Sum(Count(row.InDiscards, previous?.InDiscards), Count(row.OutDiscards, previous?.OutDiscards));
                bool changed = baseline && previous?.OperStatus is not null && row.OperStatus is not null && (previous.OperStatus != row.OperStatus || previous.LastChange is not null && row.LastChange is not null && previous.LastChange != row.LastChange);
                if (changed) _changes[row.Index] = _changes.GetValueOrDefault(row.Index) + 1;
                string status = row.AdminStatus != 1 ? "Admin down / excluded" : row.OperStatus is 2 or 6 or 7 ? "Link down" : row.OperStatus != 1 ? "Unknown / inactive" : utilization >= device.WarningUtilizationPercent || errors > 0 || discards > 0 || changed ? "Warning" : "Up";
                interfaces.Add(new(row.Index, row.Name, status, incoming, outgoing, utilization, errors, discards, _changes.GetValueOrDefault(row.Index), !baseline ? "Waiting for two compatible counter samples" : !rateSafe ? "Counter32 window may wrap multiple times; reduce interval or use v2c/v3 high-capacity counters" : "Observed counters; polling can miss changes between samples"));
            }
            _previous = observation.Snmp;
            _lastInterfaces = interfaces;
        }
        double? loss = _ping.Count == 0 ? null : 100d * _ping.Count(p => !p) / _ping.Count;
        MonitorHealth health = MonitorHealth.Unknown;
        if (device.Maintenance) health = MonitorHealth.Maintenance;
        else if (observation.Reachable == false)
        {
            _successes = 0; _firstFailure ??= observation.Time; _failures++;
            health = _failures >= device.FailureThreshold || _down is not null ? MonitorHealth.Unreachable : MonitorHealth.Suspected;
            if (_failures >= device.FailureThreshold && _down is null) { _down = new(Guid.NewGuid(), device.Id, device.Name, "Reachability", _firstFailure.Value, null, observation.Evidence); incidents.Add(_down); }
        }
        else if (observation.Reachable == true)
        {
            _failures = 0; _firstFailure = null; _successes++;
            if (_down is not null && _successes < device.RecoveryThreshold) health = MonitorHealth.Recovering;
            else
            {
                if (_down is not null) { incidents.Add(_down with { EndedAt = observation.Time, Details = "Recovery confirmed by consecutive successful checks" }); _down = null; }
                bool pingWarning = observation.RttMs > device.WarningRttMs || _ping.Count >= 5 && loss > device.WarningLossPercent;
                bool snmpWarning = _lastInterfaces.Any(i => i.Status is "Link down" or "Warning") || _latestSnmp is { Success: false };
                bool warn = pingWarning || snmpWarning;
                if (warn) { if (pingWarning && observation.PingReply is not null || snmpWarning && observation.Snmp is not null) _warningCount++; _warningRecovery = 0; _warningFromSnmp |= snmpWarning; }
                else { _warningCount = 0; if (_warningFromSnmp ? observation.Snmp is not null : observation.PingReply is not null) _warningRecovery++; }
                health = warn || _warning is not null ? MonitorHealth.Warning : MonitorHealth.Healthy;
                if (warn && _warningCount >= device.FailureThreshold && _warning is null) { _warning = new(Guid.NewGuid(), device.Id, device.Name, "Health", observation.Time, null, (snmpWarning ? "[SNMP] " : "[ICMP] ") + _lastInterfaces.FirstOrDefault(i => i.Status is "Link down" or "Warning")?.Name + " " + observation.Evidence); incidents.Add(_warning); }
                if (!warn && _warning is not null && _warningRecovery >= device.RecoveryThreshold) { incidents.Add(_warning with { EndedAt = observation.Time, Details = "Health thresholds recovered" }); _warning = null; _warningFromSnmp = false; health = MonitorHealth.Healthy; }
            }
        }
        else { _failures = _successes = 0; _firstFailure = null; }
        if (device.Maintenance) { _failures = _successes = _warningCount = _warningRecovery = 0; _firstFailure = null; }
        var visibleInterfaces = _latestSnmp is { Success: false } ? _lastInterfaces.Select(i => i with { Status = "Stale", Details = "Latest SNMP request failed; last successful counters shown" }).ToArray() : _lastInterfaces;
        return (new(device.Id, health, observation.Time, observation.RttMs, loss, _observed == 0 ? null : 100d * _available / _observed, observation.Evidence, _latestSnmp, visibleInterfaces), incidents);
    }
    public void ResetCounterBaseline() { _previous = _latestSnmp = null; _lastInterfaces = []; _failures = _successes = _warningCount = _warningRecovery = 0; _firstFailure = null; }
    public static ulong Delta(ulong current, ulong previous, bool counter64) => current >= previous ? current - previous : counter64 ? 0 : (1UL << 32) - previous + current;
    private static ulong? Sum(ulong? a, ulong? b) => a is null || b is null ? null : a + b;
}
