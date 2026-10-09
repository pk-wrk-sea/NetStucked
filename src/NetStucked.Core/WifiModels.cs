using System.Net;
using System.Net.Sockets;

namespace NetStucked.Core;

public sealed record WifiAdapter(Guid Id, string Name, string State)
{
    public override string ToString() => Name;
}
public sealed record WifiProfile(string Name, string Ssid, string SsidHex, string Security, string Eap,
    bool AutoConnect, bool PolicyManaged, int Priority, [property: System.Text.Json.Serialization.JsonIgnore] string Xml)
{
    // Profile XML may contain an OS-encrypted key. Never include it in diagnostics.
    public override string ToString() => Name;
    public bool PerUser { get; init; } = true;
}
public sealed record WifiNetwork(string Ssid, string SsidHex, string ProfileName, string Security, int Signal, bool Connectable);
public sealed record AdapterIpConfiguration(int IPv4Index, int IPv6Index, IReadOnlyList<IPAddress> Addresses,
    IReadOnlyList<IPAddress> Gateways, IReadOnlyList<IPAddress> DnsServers)
{
    public bool HasUsableAddress => Addresses.Any(a => !a.Equals(IPAddress.Any) && !a.Equals(IPAddress.IPv6Any) &&
        !(a.AddressFamily == AddressFamily.InterNetwork && a.GetAddressBytes()[0] == 169 && a.GetAddressBytes()[1] == 254) && !a.IsIPv6LinkLocal);
    public string Identity => $"{IPv4Index}/{IPv6Index}/" + Join(Addresses) + "/" + Join(Gateways) + "/" + Join(DnsServers);
    private static string Join(IReadOnlyList<IPAddress> values) => string.Join(',', values.Select(a => a.ToString()).Order(StringComparer.Ordinal));
    public static AdapterIpConfiguration Empty { get; } = new(0, 0, [], [], []);
}
public sealed record WifiSnapshot(DateTimeOffset ObservedAt, WifiAdapter Adapter, IReadOnlyList<WifiProfile> Profiles,
    IReadOnlyList<WifiNetwork> Networks, string? ConnectedProfile, string? ConnectedSsid, int? Signal,
    AdapterIpConfiguration Ip, string? Notice = null)
{
    public string Identity => $"{Adapter.Id}/{ConnectedProfile}/{ConnectedSsid}/{Ip.Identity}";
}
public sealed record WifiEvent(DateTimeOffset Time, string Adapter, string Profile, string Event, string Result, string Message);
public sealed record WifiProfileMetadata(string Description = "", DateTimeOffset? LastUsed = null, Guid? TestProfileId = null);
public sealed record ServiceTestProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "Default";
    public bool TestGateway { get; init; } = true;
    public string DnsName { get; init; } = "";
    public string TcpHost { get; init; } = "";
    public int TcpPort { get; init; } = 443;
    public string HttpUrl { get; init; } = "";
    public int ExpectedHttpStatus { get; init; } = 200;
    public int TimeoutMs { get; init; } = 3000;
    public void Validate()
    {
        if (Name is null || DnsName is null || TcpHost is null || HttpUrl is null || Id == Guid.Empty || Name.Length is < 1 or > 80 || TimeoutMs is < 500 or > 10000 || TcpPort is < 1 or > 65535 || ExpectedHttpStatus is < 100 or > 599 ||
            (DnsName.Length > 0 && (!TargetInputParserHost(DnsName) || IPAddress.TryParse(DnsName, out _))) ||
            (TcpHost.Length > 0 && !TargetInputParserHost(TcpHost)) ||
            (HttpUrl.Length > 0 && (!Uri.TryCreate(HttpUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0 || HttpUrl.Length > 2048)))
            throw new ArgumentException("Use a profile name, valid DNS/TCP/HTTP targets without credentials, a valid port/status, and timeout 500–10000 ms.");
    }
    private static bool TargetInputParserHost(string value) => TargetInputParser.IsValidHost(value);
}
public sealed record ServiceTestResult(DateTimeOffset Time, string Service, string Target, string Status, double? LatencyMs, string Message);
public sealed record WifiTestRun(DateTimeOffset Time, string Adapter, string Ssid, string Profile, string TestProfile, string Status, IReadOnlyList<ServiceTestResult> Results);
public sealed record WifiProfileInput(string Name, string Ssid, string Security, bool AutoConnect, string Description, [property: System.Text.Json.Serialization.JsonIgnore] string Password)
{
    public override string ToString() => "Wi-Fi profile input (secret redacted)";
}
public sealed record EnterpriseCredentials([property: System.Text.Json.Serialization.JsonIgnore] string Username, [property: System.Text.Json.Serialization.JsonIgnore] string Password, [property: System.Text.Json.Serialization.JsonIgnore] string Domain, bool Remember)
{
    public override string ToString() => "Enterprise credentials (redacted)";
}
public interface IWifiService
{
    Task<IReadOnlyList<WifiAdapter>> GetAdaptersAsync(CancellationToken token);
    Task<WifiSnapshot> ReadAsync(Guid adapter, CancellationToken token);
    Task ScanAsync(Guid adapter, CancellationToken token);
    Task ConnectAsync(Guid adapter, string profile, CancellationToken token);
    Task DisconnectAsync(Guid adapter, CancellationToken token);
    Task SaveProfileAsync(Guid adapter, string xml, bool overwrite, CancellationToken token);
    Task DeleteProfileAsync(Guid adapter, string profile, CancellationToken token);
    Task SetEnterpriseCredentialsAsync(Guid adapter, string profile, EnterpriseCredentials credentials, CancellationToken token);
}
public interface IWifiCredentialVault
{
    EnterpriseCredentials? Read(Guid adapter, string profile);
    void Write(Guid adapter, string profile, EnterpriseCredentials credentials);
    void Delete(Guid adapter, string profile);
}
public interface IWifiServiceTests
{
    Task<IReadOnlyList<ServiceTestResult>> RunAsync(WifiSnapshot context, ServiceTestProfile profile, CancellationToken token);
}
