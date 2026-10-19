using System.Collections.Immutable;
using System.Net;
using System.Text.RegularExpressions;

namespace NetStucked.Core;

public enum CollectorProtocol { Ssh, Telnet }
public enum CollectorDeviceState { Queued, Connecting, Authenticating, Preparing, Collecting, Saving, Completed, Partial, Failed, Cancelled }
public enum CollectorBatchState { Idle, Validating, Running, Cancelling, Completed, Partial, Failed }
public enum CollectorFailure { AuthenticationRejected, UnsupportedAuthentication, HostKey, TransientTransport, Transport, Timeout, EnableRejected, Command, Confirmation, OutputLimit, Storage }
public sealed class CollectorException(CollectorFailure failure, string safeMessage) : Exception(safeMessage)
{ public CollectorFailure Failure { get; } = failure; }
// Deliberately not a record: generated record ToString must never expose secrets.
public sealed class CollectorCredentials(string username, string password, string enableSecret)
{
    [System.Text.Json.Serialization.JsonIgnore] public string Username { get; } = username;
    [System.Text.Json.Serialization.JsonIgnore] public string Password { get; } = password;
    [System.Text.Json.Serialization.JsonIgnore] public string EnableSecret { get; } = enableSecret;
    public override string ToString() => "[memory-only collector credentials]";
}
public sealed record CollectorTarget(int Line, string Host, int Port, string Description, int? AuthSlot)
{ public string Endpoint => Host.Contains(':') ? $"[{Host}]:{Port}" : $"{Host}:{Port}"; }
public sealed record CollectorOptions(CollectorProtocol Protocol, string DeviceProfile, string OutputFolder,
    int Concurrency = 2, int ConnectTimeoutSeconds = 15, int CommandTimeoutSeconds = 30,
    bool WriteTxt = true, bool WriteJson = true, bool AllowTelnet = false, long MaxCaptureBytes = 64 * 1024 * 1024);
public sealed class CollectorRun
{
    public Guid Id { get; } = Guid.NewGuid();
    public CollectorOptions Options { get; }
    public ImmutableArray<CollectorTarget> Targets { get; }
    public ImmutableArray<string> BasicCommands { get; }
    public ImmutableArray<string> CollectionCommands { get; }
    public ImmutableDictionary<string, string> HostPins { get; }
    [System.Text.Json.Serialization.JsonIgnore] public CollectorCredentials Auth1 { get; }
    [System.Text.Json.Serialization.JsonIgnore] public CollectorCredentials? Auth2 { get; }
    public CollectorRun(CollectorOptions options, IEnumerable<CollectorTarget> targets, IEnumerable<string> basic,
        IEnumerable<string> collection, IDictionary<string, string> pins, CollectorCredentials auth1, CollectorCredentials? auth2)
    {
        if (options.Concurrency is < 1 or > 8 || options.ConnectTimeoutSeconds is < 5 or > 120 || options.CommandTimeoutSeconds is < 5 or > 300)
            throw new ArgumentException("Concurrency must be 1–8; connect timeout 5–120s; command timeout 5–300s.");
        if (!options.WriteTxt && !options.WriteJson) throw new ArgumentException("Choose TXT and/or JSON output.");
        if (options.Protocol == CollectorProtocol.Telnet && !options.AllowTelnet) throw new ArgumentException("Acknowledge plaintext Telnet first.");
        if (options.MaxCaptureBytes is < 1024 or > 256 * 1024 * 1024) throw new ArgumentException("Capture limit must be 1 KiB–256 MiB.");
        if (!Path.IsPathFullyQualified(options.OutputFolder)) throw new ArgumentException("Choose an absolute output folder.");
        Options = options; Targets = targets.ToImmutableArray(); BasicCommands = basic.ToImmutableArray(); CollectionCommands = collection.ToImmutableArray();
        HostPins = pins.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase); Auth1 = auth1; Auth2 = auth2;
        if (Targets.Length is < 1 or > 256) throw new ArgumentException("Enter 1–256 devices.");
        ValidateCredentials(auth1); if (auth2 is not null) ValidateCredentials(auth2);
        if (Targets.Any(t => t.AuthSlot == 2) && auth2 is null) throw new ArgumentException("An auth=2 device needs AUTH2.");
        CollectorCommands.Validate(options.DeviceProfile, BasicCommands, CollectionCommands);
    }
    private static void ValidateCredentials(CollectorCredentials c)
    {
        if (c.Username.Length is < 1 or > 256 || c.Password.Length is < 1 or > 512 || c.EnableSecret.Length > 512
            || (c.Username + c.Password + c.EnableSecret).Any(char.IsControl)) throw new ArgumentException("Enter a username and password without control characters (maximum 256/512 characters).");
    }
    public override string ToString() => $"Collector run {Id} ({Targets.Length} devices; credentials excluded)";
}
public sealed record CollectorCommandEvidence(string Command, DateTimeOffset Started, DateTimeOffset Finished, bool Completed);
public sealed record CollectorResult(Guid RunId, CollectorTarget Target, CollectorDeviceState State, string Detail,
    int? AuthSlot = null, string Preview = "", string TxtPath = "", string JsonPath = "", long Bytes = 0, string Sha256 = "",
    DateTimeOffset? Started = null, DateTimeOffset? Finished = null, string DevicePrompt = "");
public interface ICollectorConnection : IAsyncDisposable
{
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token);
    ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token);
}
public interface ICollectorTransport
{
    Task<ICollectorConnection> ConnectAsync(CollectorTarget target, CollectorOptions options, CollectorCredentials credentials,
        string? hostPin, CancellationToken token);
}
public interface ICollectorCapture : IAsyncDisposable
{
    Task AppendAsync(string text, CancellationToken token);
    string Preview { get; }
    Task<CollectorResult> CompleteAsync(CollectorResult result, IReadOnlyList<CollectorCommandEvidence> commands, CancellationToken token);
}
public interface ICollectorCaptureFactory
{ Task<ICollectorCapture> CreateAsync(CollectorRun run, CollectorTarget target, CancellationToken token); }

public static class CollectorInputParser
{
    public static IReadOnlyList<CollectorTarget> Parse(string text, int defaultPort)
    {
        if (text.Length > 65536) throw new ArgumentException("Device input exceeds 64 KiB.");
        var result = new List<CollectorTarget>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int line = 0;
        foreach (string raw in text.Split('\n'))
        {
            line++; string value = raw.Trim(); if (value.Length == 0 || value.StartsWith('#')) continue;
            var tokens = Regex.Split(value, @"\s+"); string endpoint = tokens[0], host = endpoint; int port = defaultPort;
            if (endpoint.StartsWith('['))
            {
                int close = endpoint.IndexOf(']');
                if (close < 0) throw new ArgumentException($"Line {line}: unclosed IPv6 brackets.");
                host = endpoint[1..close]; string suffix = endpoint[(close + 1)..];
                if (suffix.Length > 0 && (!suffix.StartsWith(':') || !int.TryParse(suffix[1..], out port))) throw new ArgumentException($"Line {line}: invalid port.");
                if (!IPAddress.TryParse(host, out _)) throw new ArgumentException($"Line {line}: invalid IP address.");
            }
            else if (endpoint.Count(c => c == ':') == 1)
            {
                int split = endpoint.LastIndexOf(':'); host = endpoint[..split];
                if (!int.TryParse(endpoint[(split + 1)..], out port)) throw new ArgumentException($"Line {line}: invalid port.");
            }
            if (port is < 1 or > 65535 || host.Length is < 1 or > 253 || host.Contains('%') || host.Any(char.IsControl)
                || (Uri.CheckHostName(host) == UriHostNameType.Unknown)) throw new ArgumentException($"Line {line}: invalid host or port.");
            host = IPAddress.TryParse(host, out var ip) ? ip.ToString() : host.TrimEnd('.').ToLowerInvariant();
            int? auth = null; var description = new List<string>();
            foreach (string part in tokens.Skip(1))
            {
                if (part.StartsWith("auth=", StringComparison.OrdinalIgnoreCase))
                {
                    if (auth is not null || part.ToLowerInvariant() is not ("auth=1" or "auth=2")) throw new ArgumentException($"Line {line}: use one auth=1 or auth=2.");
                    auth = part[^1] - '0';
                }
                else description.Add(part);
            }
            var target = new CollectorTarget(line, host, port, string.Join(' ', description), auth);
            if (seen.Add(target.Endpoint)) result.Add(target);
            if (result.Count > 256) throw new ArgumentException("Maximum 256 unique devices.");
        }
        if (result.Count == 0) throw new ArgumentException("Enter at least one device.");
        return result;
    }
    public static Dictionary<string, string> ParsePins(string text)
    {
        var pins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in text.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0 && !s.StartsWith('#')))
        {
            var parts = Regex.Split(line, @"\s+");
            if (parts.Length != 2 || !parts[1].StartsWith("SHA256:") || !TryFingerprint(parts[1]))
                throw new ArgumentException("Host pins: use endpoint SHA256:base64 (one per line).");
            var target = Parse(parts[0], 22).Single(); pins[target.Endpoint] = parts[1].TrimEnd('=');
        }
        return pins;
    }
    private static bool TryFingerprint(string value) { try { return Convert.FromBase64String(value[7..].PadRight(44, '=')).Length == 32; } catch (FormatException) { return false; } }
}

public static class CollectorCommands
{
    public static IReadOnlyList<string> Profiles { get; } = ["Cisco IOS / IOS XE", "Cisco NX-OS", "Cisco ASA", "Huawei VRP", "Aruba / ProCurve"];
    public static string Basic(string profile) => profile switch { "Cisco ASA" => "terminal pager 0", "Huawei VRP" => "screen-length 0 temporary", "Aruba / ProCurve" => "no page", _ => "terminal length 0" };
    public static string Collection(string profile) => profile == "Huawei VRP" ? "display current-configuration\ndisplay version" : "show running-config\nshow version";
    public static string[] Lines(string text) => text.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
    public static void Validate(string profile, IEnumerable<string> basic, IEnumerable<string> collection)
    {
        if (!Profiles.Contains(profile)) throw new ArgumentException("Select a supported device profile.");
        var b = basic.ToArray(); var c = collection.ToArray();
        if (b.Length > 4 || c.Length is < 1 or > 32) throw new ArgumentException("Use up to 4 basic and 1–32 collection commands.");
        if (b.Any(s => s != Basic(profile))) throw new ArgumentException("Basic commands must match the selected profile's pagination command.");
        // An intentionally narrow grammar, not a general remote shell. No pipes, separators, redirects or confirmations.
        if (c.Any(s => s.Length > 160 || !Regex.IsMatch(s, @"^(show|display) [A-Za-z0-9 _./:-]+$", RegexOptions.CultureInvariant)
            || Regex.IsMatch(s, @"\b(tech|tech-support|password|secret|crypto|redirect|copy|delete|erase|reload|configure)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
            throw new ArgumentException("Collection accepts bounded read-only show/display commands; shell modifiers and interactive commands are blocked.");
    }
}
