using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetStucked.Core;
using NetStucked.Desktop.Services;

namespace NetStucked.Desktop.ViewModels;

public sealed record OperationSummary(string Menu, string State, string Detail);
public partial class DashboardViewModel : ObservableObject
{
    private readonly LivePingViewModel _ping;
    private readonly TracerouteWorkspaceViewModel _trace;
    private readonly PortTestViewModel _port;
    private readonly MonitoringViewModel _monitor;
    private readonly ConfigCollectorViewModel _collector;
    private readonly DnsResolverViewModel _dns;
    private readonly MacCheckerViewModel _mac;
    private readonly UrlCheckerViewModel _url;
    public EngineeringContext Workspace { get; }
    public ObservableCollection<OperationSummary> Tools { get; } = [];
    public ObservableCollection<MonitorIncident> Incidents { get; } = [];
    [ObservableProperty] private string _summary = "No observed data yet.";
    [ObservableProperty] private string _updated = "";
    private DateTimeOffset _last;
    public event Action<string>? NavigateRequested;
    public DashboardViewModel(EngineeringContext w, LivePingViewModel ping, TracerouteWorkspaceViewModel trace, PortTestViewModel port, MonitoringViewModel monitor, ConfigCollectorViewModel collector, DnsResolverViewModel dns, MacCheckerViewModel mac, UrlCheckerViewModel url)
    { Workspace = w; _ping = ping; _trace = trace; _port = port; _monitor = monitor; _collector = collector; _dns = dns; _mac = mac; _url = url; }
    [RelayCommand] private void Open(string menu) => NavigateRequested?.Invoke(menu);
    [RelayCommand] private void Refresh() => Update(true);
    public void Update(bool force = false)
    {
        var now = DateTimeOffset.UtcNow; if (!force && now - _last < TimeSpan.FromSeconds(1)) return; _last = now;
        var samples = _monitor.Engine.Snapshots; int unknown = _monitor.Engine.Devices.Count(d => d.Started && !samples.Any(s => s.DeviceId == d.Id && s.LastCheck is not null && now - s.LastCheck <= TimeSpan.FromMinutes(2)));
        Summary = $"Monitoring: {_monitor.Engine.ActiveCount} active / {_monitor.Devices.Count} configured · {samples.Count(s => s.Health == MonitorHealth.Unreachable)} unreachable · {samples.Count(s => s.Health == MonitorHealth.Warning)} warnings · {unknown} unknown/stale\nInventory: {Workspace.Data.Assets.Length} assets · {Workspace.Data.Assets.Count(a => a.MaStatus == "Expired")} expired MA · {Workspace.Data.Evidence.Length} retained observations · {Workspace.Data.Plans.Length} device change plans";
        Tools.Clear();
        foreach (var row in new[] { new OperationSummary("Live Ping", _ping.IsActive ? "Running" : "Idle", "ICMP responses reflect reachability only"), new OperationSummary("Traceroute", _trace.IsActive ? "Running" : "Idle", $"{_trace.Sessions.Count} trace sessions"), new OperationSummary("Port Test", _port.IsActive ? "Running" : "Idle", "Explicit TCP checks"), new OperationSummary("Subnet Calculator", "Local calculation", "No network requests"), new OperationSummary("DNS Resolver", _dns.IsRunning ? "Running" : "Idle", "Explicit selected resolver/record lookups"), new OperationSummary("MAC Checker", _mac.IsRunning ? "Running" : "Idle", "Explicit MACVendors lookup"), new OperationSummary("URL Checker", _url.IsChecking ? "Running" : "Idle", "VirusTotal requires configured personal key; Palo Alto provider session"), new OperationSummary("Monitoring", _monitor.Engine.ActiveCount > 0 ? "Running" : "Idle", _monitor.Activity), new OperationSummary("Config Collector", _collector.State.ToString(), _collector.Message), new OperationSummary("Inventory", "Local records", $"{Workspace.Data.Assets.Length} assets"), new OperationSummary("Topology Explorer", "Offline evidence", $"{Workspace.Data.Evidence.Length} observations"), new OperationSummary("CVE & Upgrade Planner", "Engineer review", $"{Workspace.Data.Advisories.Length} cached advisories; no automatic safe-version claim") }) Tools.Add(row);
        Incidents.Clear(); foreach (var item in _monitor.Incidents.OrderByDescending(i => i.StartedAt).Take(50)) Incidents.Add(item);
        Updated = "Snapshot UTC " + now.ToString("yyyy-MM-dd HH:mm:ss") + " · local application state; no extra probes";
    }
}
