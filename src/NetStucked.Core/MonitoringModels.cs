using System.Net;

namespace NetStucked.Core;
public enum SnmpVersion { V1, V2c, V3 }
public enum SnmpAuth { None, MD5, SHA1, SHA256, SHA384, SHA512 }
public enum SnmpPrivacy { None, DES, AES128, AES192, AES256 }
public sealed record SnmpSettings
{
    public SnmpVersion Version { get; init; } = SnmpVersion.V2c;
    public int Port { get; init; } = 161;
    public string Username { get; init; } = "";
    public SnmpAuth Authentication { get; init; } = SnmpAuth.SHA256;
    public SnmpPrivacy Privacy { get; init; } = SnmpPrivacy.AES128;
    public bool SystemInfo { get; init; } = true;
    public bool Interfaces { get; init; } = true;
    public bool Bandwidth { get; init; } = true;
    public bool Errors { get; init; } = true;
    public int[] InterfaceIndexes { get; init; } = [];
    public string[] CustomOids { get; init; } = [];
    public void Validate()
    {
        if (!Enum.IsDefined(Version) || !Enum.IsDefined(Authentication) || !Enum.IsDefined(Privacy) || Port is < 1 or > 65535 || Username.Length > 64 || InterfaceIndexes.Length > 32 || InterfaceIndexes.Any(i => i < 1) || InterfaceIndexes.Distinct().Count() != InterfaceIndexes.Length || CustomOids.Length > 16)
            throw new ArgumentException("Invalid SNMP options: at most 32 selected interfaces and 16 custom OIDs.");
        if (Version == SnmpVersion.V3 && (string.IsNullOrWhiteSpace(Username) || Username.Any(char.IsControl) || Privacy != SnmpPrivacy.None && Authentication == SnmpAuth.None)) throw new ArgumentException("SNMP v3 requires a username; privacy requires authentication.");
        foreach (string oid in CustomOids) if (oid.Length > 160 || !oid.Trim('.').Split('.').All(p => uint.TryParse(p, out _)) || !oid.StartsWith("1.", StringComparison.Ordinal)) throw new ArgumentException("Custom OIDs must be numeric 1.x object identifiers.");
    }
}
public sealed record SnmpSecrets(string Community, string AuthenticationPassword, string PrivacyPassword)
{
    public override string ToString() => "Protected SNMP credentials";
}
public sealed record MonitorDevice
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public string Host { get; init; } = "";
    public string Group { get; init; } = "";
    public bool Started { get; init; }
    public bool Maintenance { get; init; }
    public bool Icmp { get; init; } = true;
    public bool Tcp { get; init; }
    public int TcpPort { get; init; } = 443;
    public bool Snmp { get; init; }
    public SnmpSettings SnmpSettings { get; init; } = new();
    public int IcmpIntervalSeconds { get; init; } = 10;
    public int SnmpIntervalSeconds { get; init; } = 30;
    public int TimeoutMs { get; init; } = 2000;
    public int FailureThreshold { get; init; } = 3;
    public int RecoveryThreshold { get; init; } = 2;
    public double WarningRttMs { get; init; } = 200;
    public double WarningLossPercent { get; init; } = 20;
    public double WarningUtilizationPercent { get; init; } = 80;
    public void Validate()
    {
        if (Id == Guid.Empty || Name.Length is < 1 or > 80 || Group.Length > 80 || Host.Length is < 1 or > 253 || Host.Any(char.IsWhiteSpace)) throw new ArgumentException("Enter a device name and one valid host/IP.");
        if (!IPAddress.TryParse(Host, out _)) _ = DiagnosticInput.DnsName(Host, DnsRecordType.A);
        if (!Icmp && !Tcp && !Snmp) throw new ArgumentException("Choose at least one monitoring method.");
        if (TcpPort is < 1 or > 65535 || IcmpIntervalSeconds is < 1 or > 3600 || SnmpIntervalSeconds is < 5 or > 3600 || TimeoutMs is < 500 or > 10000 || FailureThreshold is < 1 or > 20 || RecoveryThreshold is < 1 or > 20 || WarningRttMs is < 1 or > 60000 || WarningLossPercent is < 0 or > 100 || WarningUtilizationPercent is < 1 or > 100) throw new ArgumentException("Invalid intervals, timeout or health thresholds.");
        if (Snmp) SnmpSettings.Validate();
    }
}
public sealed record SnmpInterface(int Index, string Name, int? AdminStatus, int? OperStatus, ulong? SpeedBits, ulong? InOctets, ulong? OutOctets, ulong? InErrors, ulong? OutErrors, ulong? InDiscards, ulong? OutDiscards, ulong? LastChange, ulong? Discontinuity, bool Counter64);
public sealed record SnmpSnapshot(DateTimeOffset Time, string Status, string Name, string Description, ulong? UptimeTicks, IReadOnlyList<SnmpInterface> Interfaces, IReadOnlyDictionary<string, string> CustomValues, string Details = "") { public bool Success => Status == "Success"; }
public interface ISnmpClient { Task<SnmpSnapshot> ReadAsync(string host, SnmpSettings settings, SnmpSecrets secrets, int timeoutMs, bool discover, CancellationToken token); }
public sealed record MonitorObservation(DateTimeOffset Time, bool? Reachable, bool? PingReply, double? RttMs, bool? TcpConnected, string Evidence, SnmpSnapshot? Snmp);
public interface IMonitorProbe { Task<MonitorObservation> PollAsync(MonitorDevice device, bool readSnmp, CancellationToken token); }
public enum MonitorHealth { Unknown, Healthy, Suspected, Unreachable, Recovering, Warning, Maintenance, Stopped }
public sealed record InterfaceHealth(int Index, string Name, string Status, double? InMbps, double? OutMbps, double? UtilizationPercent, ulong? NewErrors, ulong? NewDiscards, int ObservedChanges, string Details);
public sealed record MonitorIncident(Guid Id, Guid DeviceId, string DeviceName, string Kind, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, string Details);
public sealed record MonitorSnapshot(Guid DeviceId, MonitorHealth Health, DateTimeOffset? LastCheck, double? RttMs, double? LossPercent, double? AvailabilityPercent, string Evidence, SnmpSnapshot? Snmp, IReadOnlyList<InterfaceHealth> Interfaces);
public sealed record MonitorData(int SchemaVersion, IReadOnlyList<MonitorDevice> Devices, IReadOnlyList<MonitorIncident> Incidents, DateTimeOffset? LastSavedAt, bool Notifications = true, bool Sound = false);
public interface IMonitorStore { MonitorData Load(); Task SaveAsync(MonitorData data, CancellationToken token); }
