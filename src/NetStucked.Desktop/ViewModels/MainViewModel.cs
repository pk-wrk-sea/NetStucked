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
    public UrlCheckerViewModel Url { get; }
    public MonitoringViewModel Monitoring { get; }
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
    public MainViewModel(LivePingViewModel ping, TracerouteWorkspaceViewModel trace, PortTestViewModel port, UpdatesViewModel updates, AppearanceViewModel appearance, DnsResolverViewModel dns, SubnetCalculatorViewModel subnet, MacCheckerViewModel mac, UrlCheckerViewModel url, MonitoringViewModel monitoring)
    {
        Ping = ping; TraceWorkspace = trace; Port = port; Updates = updates; Appearance = appearance; _currentPage = ping; _sidebarExpanded = ping.Store.Preferences.SidebarExpanded;
        Dns = dns; Subnet = subnet; Mac = mac; Url = url; Monitoring = monitoring;
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
        if (SelectedPage == page) return;
        SelectedPage = page; PageTitle = page == "Updates" ? "Updates & Recovery" : page;
        CurrentPage = page switch
        {
            "Live Ping" => Ping,
            "Traceroute" => TraceWorkspace,
            "Port Test" => Port,
            "DNS Resolver" => Dns,
            "Subnet Calculator" => Subnet,
            "MAC Checker" => Mac,
            "URL Checker" => Url,
            "Monitoring" => Monitoring,
            "Updates" => Updates,
            "Settings" => Appearance,
            "Dashboard" => new PlaceholderViewModel("Pending - Coming Soon"),
            _ => new PlaceholderViewModel("Pending - Coming Soon")
        };
        PageSubtitle = page switch { "Live Ping" => "Ping multiple hosts or subnets and monitor response in real time", "Traceroute" => "Trace route hops and monitor path response in real time", "Port Test" => "Test TCP ports across hosts and subnets", "DNS Resolver" => "Look up selected DNS records through system or custom resolvers", "Subnet Calculator" => "Calculate IPv4 subnet ranges as you type", "MAC Checker" => "Look up registered vendors through MACVendors.com", "URL Checker" => "VirusTotal reports and embedded Palo Alto Test A Site", "Monitoring" => "Device reachability, selected interfaces and local incidents", "Updates" => "Manage application versions", "Settings" => "Appearance and application preferences", _ => "" };
        Ping.SetPresentationActive(ReferenceEquals(CurrentPage, Ping)); TraceWorkspace.SetPresentationActive(ReferenceEquals(CurrentPage, TraceWorkspace));
        Port.SetPresentationActive(ReferenceEquals(CurrentPage, Port));
    }
    public async Task ShutdownAsync() { _activity.Stop(); await Task.WhenAll(Ping.DisposeAsync().AsTask(), TraceWorkspace.DisposeAsync().AsTask(), Port.DisposeAsync().AsTask(), Updates.DisposeAsync().AsTask(), Dns.DisposeAsync().AsTask(), Mac.DisposeAsync().AsTask(), Url.DisposeAsync().AsTask(), Monitoring.DisposeAsync().AsTask()); await Ping.Store.SaveAsync(); }
    public async Task PrepareForUpdateAsync()
    {
        if (TraceWorkspace.RemoveSessionCommand.ExecutionTask is { } closing) await closing;
        await Task.WhenAll(Ping.StopForUpdateAsync(), Port.StopForUpdateAsync(), Task.WhenAll(TraceWorkspace.Sessions.ToArray().Select(s => s.StopForUpdateAsync())));
        await TraceWorkspace.StopMetadataAsync();
        await Task.WhenAll(Dns.StopForUpdateAsync(), Mac.StopForUpdateAsync(), Url.StopForUpdateAsync(), Monitoring.StopForUpdateAsync());
        Ping.Store.Preferences.TraceTarget = Trace.Target;
        await Ping.Store.SaveAsync();
    }
}
