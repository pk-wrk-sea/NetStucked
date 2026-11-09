using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Threading;

namespace NetStucked.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public LivePingViewModel Ping { get; }
    public TracerouteWorkspaceViewModel TraceWorkspace { get; }
    public PortTestViewModel Port { get; }
    public UpdatesViewModel Updates { get; }
    public AppearanceViewModel Appearance { get; }
    public DnsResolverViewModel Dns { get; }
    public SubnetCalculatorViewModel Subnet { get; }
    public MacCheckerViewModel Mac { get; }
    // Dormant legacy accessors support offline recovery/old regression fixtures only.
    // No production navigation, startup or timer dereferences these lazy workflows.
    private readonly Lazy<UrlCheckerViewModel> _url;
    private readonly Lazy<MonitoringViewModel> _monitoring;
    private readonly Lazy<Services.EngineeringContext> _engineering;
    private readonly Lazy<DashboardViewModel> _dashboard;
    private readonly Lazy<InventoryViewModel> _inventory;
    private readonly Lazy<TopologyViewModel> _topology;
    private readonly Lazy<AdvisoryPlannerViewModel> _planner;
    public UrlCheckerViewModel Url => _url.Value;
    public MonitoringViewModel Monitoring => _monitoring.Value;
    public ConfigCollectorViewModel Collector { get; }
    public Services.EngineeringContext Engineering => _engineering.Value;
    public DashboardViewModel Dashboard => _dashboard.Value;
    public InventoryViewModel Inventory => _inventory.Value;
    public TopologyViewModel Topology => _topology.Value;
    public AdvisoryPlannerViewModel Planner => _planner.Value;
    public IpsCoverageViewModel Coverage { get; }
    [ObservableProperty] private bool _sidebarExpanded = true;
    [RelayCommand] private void ToggleSidebar() => SidebarExpanded = !SidebarExpanded;
    partial void OnSidebarExpandedChanged(bool value) => Ping.Store.Preferences.SidebarExpanded = value;
    public TracerouteViewModel Trace => TraceWorkspace.SelectedSession;
    private readonly DispatcherTimer _activity;
    public bool IsPingActive => Ping.IsActive;
    public bool IsTraceActive => TraceWorkspace.IsActive;
    public bool IsPortActive => Port.IsActive;
    [ObservableProperty] private object _currentPage;
    [ObservableProperty] private string _pageTitle = "Live Ping";
    [ObservableProperty] private string _pageSubtitle = "Ping multiple hosts or subnets and monitor response in real time";
    [ObservableProperty] private string _selectedPage = "Live Ping";
    public string Version => Updates.CurrentVersion;
    public MainViewModel(LivePingViewModel ping, TracerouteWorkspaceViewModel trace, PortTestViewModel port, UpdatesViewModel updates, AppearanceViewModel appearance, DnsResolverViewModel dns, SubnetCalculatorViewModel subnet, MacCheckerViewModel mac, Lazy<UrlCheckerViewModel> url, Lazy<MonitoringViewModel> monitoring, ConfigCollectorViewModel collector, Lazy<Services.EngineeringContext> engineering, Lazy<DashboardViewModel> dashboard, Lazy<InventoryViewModel> inventory, Lazy<TopologyViewModel> topology, Lazy<AdvisoryPlannerViewModel> planner, IpsCoverageViewModel coverage)
    {
        Ping = ping; TraceWorkspace = trace; Port = port; Updates = updates; Appearance = appearance; _currentPage = ping; _sidebarExpanded = ping.Store.Preferences.SidebarExpanded;
        Dns = dns; Subnet = subnet; Mac = mac; _url = url; _monitoring = monitoring; Collector = collector;
        _engineering = engineering; _dashboard = dashboard; _inventory = inventory; _topology = topology; _planner = planner; Coverage = coverage;
        Updates.StopDiagnosticsAsync = PrepareForUpdateAsync;
        Ping.SetPresentationActive(true); TraceWorkspace.SetPresentationActive(false);
        Ping.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Ping.IsActive)) OnPropertyChanged(nameof(IsPingActive)); };
        TraceWorkspace.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TraceWorkspace.IsActive)) OnPropertyChanged(nameof(IsTraceActive)); };
        Port.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Port.IsActive)) OnPropertyChanged(nameof(IsPortActive)); };
        _activity = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, (_, _) => { Ping.UpdateState(); TraceWorkspace.UpdateStates(); Port.UpdateState(); }, Dispatcher.CurrentDispatcher);
        _activity.Start();
    }
    [RelayCommand]
    private void Navigate(string page)
    {
        if (SelectedPage == page || page is "Monitoring" or "Topology Explorer" or "Inventory" or "URL Checker" or "CVE & Upgrade Planner") return;
        SelectedPage = page; PageTitle = page == "Updates" ? "Updates & Recovery" : page;
        CurrentPage = page switch
        {
            "Live Ping" => Ping,
            "Traceroute" => TraceWorkspace,
            "Port Test" => Port,
            "DNS Resolver" => Dns,
            "Subnet Calculator" => Subnet,
            "MAC Checker" => Mac,
            "Config Collector" => Collector,
            "Updates" => Updates,
            "Settings" => Appearance,
            "Dashboard" => new PlaceholderViewModel("Dashboard — Coming soon"),
            "CVE Checker | IPS" => Coverage,
            _ => new PlaceholderViewModel("Pending - Coming Soon")
        };
        PageSubtitle = page switch { "Live Ping" => "Ping multiple hosts or subnets and monitor response in real time", "Traceroute" => "Trace route hops and monitor path response in real time", "Port Test" => "Test TCP ports across hosts and subnets", "DNS Resolver" => "Look up selected DNS records through system or custom resolvers", "Subnet Calculator" => "Calculate IPv4 subnet ranges as you type", "MAC Checker" => "Look up registered vendors through MACVendors.com", "Updates" => "Manage application versions", "Settings" => "Appearance and application preferences", _ => "" };
        Ping.SetPresentationActive(ReferenceEquals(CurrentPage, Ping)); TraceWorkspace.SetPresentationActive(ReferenceEquals(CurrentPage, TraceWorkspace));
        if (page == "Config Collector") PageSubtitle = "Collect read-only device configuration through verified SSH or explicitly enabled Telnet";
        if (page == "Dashboard") PageSubtitle = "Coming soon";
        if (page == "CVE Checker | IPS") PageSubtitle = "Import VA reports and compare IPS signature coverage from Palo Alto and Check Point sources";
        Port.SetPresentationActive(ReferenceEquals(CurrentPage, Port));
    }
    public async Task ShutdownAsync() { _activity.Stop(); await Task.WhenAll(Ping.DisposeAsync().AsTask(), TraceWorkspace.DisposeAsync().AsTask(), Port.DisposeAsync().AsTask(), Updates.DisposeAsync().AsTask(), Dns.DisposeAsync().AsTask(), Mac.DisposeAsync().AsTask(), Coverage.DisposeAsync().AsTask(), Collector.DisposeAsync().AsTask(), StopLegacyAsync()); await Ping.Store.SaveAsync(); }
    private async Task StopLegacyAsync()
    {
        if (_url.IsValueCreated) await _url.Value.StopForUpdateAsync();
        if (_monitoring.IsValueCreated) await _monitoring.Value.StopForUpdateAsync();
        if (_engineering.IsValueCreated) await _engineering.Value.StopAsync();
    }
    public async Task PrepareForUpdateAsync()
    {
        if (TraceWorkspace.RemoveSessionCommand.ExecutionTask is { } closing) await closing;
        await Task.WhenAll(Ping.StopForUpdateAsync(), Port.StopForUpdateAsync(), Task.WhenAll(TraceWorkspace.Sessions.ToArray().Select(s => s.StopForUpdateAsync())));
        await TraceWorkspace.StopMetadataAsync();
        await Task.WhenAll(Dns.StopForUpdateAsync(), Mac.StopForUpdateAsync(), Coverage.StopForUpdateAsync(), Collector.StopForUpdateAsync(), StopLegacyAsync());
        Ping.Store.Preferences.TraceTarget = Trace.Target;
        await Ping.Store.SaveAsync();
    }
}
