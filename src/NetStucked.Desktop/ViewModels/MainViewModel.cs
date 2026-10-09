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
    public NetworkInfoViewModel Network { get; }
    public DnsTestViewModel Dns { get; }
    public HttpTestViewModel Http { get; }
    [ObservableProperty] private string _networkTransition = "";
    public bool HasNetworkTransition => NetworkTransition.Length > 0;
    partial void OnNetworkTransitionChanged(string value) => OnPropertyChanged(nameof(HasNetworkTransition));
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
    public MainViewModel(LivePingViewModel ping, TracerouteWorkspaceViewModel trace, PortTestViewModel port, UpdatesViewModel updates, AppearanceViewModel appearance, NetworkInfoViewModel network, DnsTestViewModel dns, HttpTestViewModel http)
    {
        Ping = ping; TraceWorkspace = trace; Port = port; Updates = updates; Appearance = appearance; Network = network; _currentPage = ping; _sidebarExpanded = ping.Store.Preferences.SidebarExpanded;
        Dns = dns; Http = http;
        Network.TransitionObserved += entry => { NetworkTransition = entry.Time.ToString("HH:mm:ss") + " · " + entry.Message; };
        Updates.StopDiagnosticsAsync = PrepareForUpdateAsync;
        Ping.SetPresentationActive(true); TraceWorkspace.SetPresentationActive(false);
        Ping.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Ping.IsActive)) OnPropertyChanged(nameof(IsPingActive)); };
        TraceWorkspace.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TraceWorkspace.IsActive)) OnPropertyChanged(nameof(IsTraceActive)); };
        Port.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Port.IsActive)) OnPropertyChanged(nameof(IsPortActive)); };
        _activity = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, (_, _) => { Ping.UpdateState(); TraceWorkspace.UpdateStates(); Port.UpdateState(); if (IsPingActive || IsTraceActive || IsPortActive) Network.ObserveWhileDiagnosticsActive(); }, Dispatcher.CurrentDispatcher);
        _activity.Start();
    }
    [RelayCommand]
    private void Navigate(string page)
    {
        if (SelectedPage == page) return;
        SelectedPage = page; PageTitle = page == "Updates" ? "Updates & Recovery" : page == "Network Info" ? "Wi-Fi Profile Manager" : page;
        CurrentPage = page switch
        {
            "Live Ping" => Ping,
            "Traceroute" => TraceWorkspace,
            "Port Test" => Port,
            "Diagnostics" => Port,
            "DNS Test" => Dns,
            "HTTP / HTTPS Test" => Http,
            "Updates" => Updates,
            "Settings" => Appearance,
            "Dashboard" => new PlaceholderViewModel("Pending - Coming Soon"),
            "Network Info" => Network,
            _ => new PlaceholderViewModel("Pending - Coming Soon")
        };
        PageSubtitle = page switch { "Live Ping" => "Ping multiple hosts or subnets and monitor response in real time", "Traceroute" => "Trace route hops and monitor path response in real time", "Port Test" => "Test TCP / UDP ports across hosts and subnets", "Network Info" => "Switch between client Wi-Fi environments for service testing", "Updates" => "Manage application versions", "Settings" => "Appearance and application preferences", _ => "" };
        if (page is "Diagnostics" or "DNS Test" or "HTTP / HTTPS Test") PageSubtitle = page == "DNS Test" ? "Query and compare DNS records from selected resolvers" : page == "HTTP / HTTPS Test" ? "Inspect HTTP responses, redirects and TLS certificates" : "TCP / UDP ports, DNS records and HTTP / HTTPS diagnostics";
        Ping.SetPresentationActive(ReferenceEquals(CurrentPage, Ping)); TraceWorkspace.SetPresentationActive(ReferenceEquals(CurrentPage, TraceWorkspace));
        Port.SetPresentationActive(ReferenceEquals(CurrentPage, Port));
        Network.SetPresentationActive(ReferenceEquals(CurrentPage, Network));
    }
    public async Task ShutdownAsync() { _activity.Stop(); await Task.WhenAll(Ping.DisposeAsync().AsTask(), TraceWorkspace.DisposeAsync().AsTask(), Port.DisposeAsync().AsTask(), Updates.DisposeAsync().AsTask(), Network.DisposeAsync().AsTask(), Dns.DisposeAsync().AsTask(), Http.DisposeAsync().AsTask()); await Ping.Store.SaveAsync(); }
    public async Task PrepareForUpdateAsync()
    {
        if (TraceWorkspace.RemoveSessionCommand.ExecutionTask is { } closing) await closing;
        await Task.WhenAll(Ping.StopForUpdateAsync(), Port.StopForUpdateAsync(), Task.WhenAll(TraceWorkspace.Sessions.ToArray().Select(s => s.StopForUpdateAsync())));
        await TraceWorkspace.StopMetadataAsync();
        await Network.StopForUpdateAsync();
        await Task.WhenAll(Dns.StopForUpdateAsync(), Http.StopForUpdateAsync());
        Ping.Store.Preferences.TraceTarget = Trace.Target;
        await Ping.Store.SaveAsync();
    }
}
