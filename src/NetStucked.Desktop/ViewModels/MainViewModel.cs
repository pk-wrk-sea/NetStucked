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
    public MainViewModel(LivePingViewModel ping, TracerouteWorkspaceViewModel trace, PortTestViewModel port, UpdatesViewModel updates)
    {
        Ping = ping; TraceWorkspace = trace; Port = port; Updates = updates; _currentPage = ping;
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
            "Updates" => Updates,
            "Dashboard" => new PlaceholderViewModel("Dashboard — Coming later. Design pending."),
            "Network Info" => new PlaceholderViewModel("Network Info — Planned"),
            _ => new PlaceholderViewModel($"NetStucked {Version}\nLight theme • probe settings are available inside each diagnostic tool\nPreferences and logs: {Ping.Store.DirectoryPath}")
        };
        PageSubtitle = page switch { "Live Ping" => "Ping multiple hosts or subnets and monitor response in real time", "Traceroute" => "Trace route hops and monitor path response in real time", "Port Test" => "Test TCP connections to remote endpoints and monitor connection time", "Updates" => "Manage application versions", _ => "" };
        Ping.SetPresentationActive(ReferenceEquals(CurrentPage, Ping)); TraceWorkspace.SetPresentationActive(ReferenceEquals(CurrentPage, TraceWorkspace));
        Port.SetPresentationActive(ReferenceEquals(CurrentPage, Port));
    }
    public async Task ShutdownAsync() { _activity.Stop(); await Task.WhenAll(Ping.DisposeAsync().AsTask(), TraceWorkspace.DisposeAsync().AsTask(), Port.DisposeAsync().AsTask(), Updates.DisposeAsync().AsTask()); await Ping.Store.SaveAsync(); }
    public async Task PrepareForUpdateAsync()
    {
        if (TraceWorkspace.RemoveSessionCommand.ExecutionTask is { } closing) await closing;
        await Task.WhenAll(Ping.StopForUpdateAsync(), Port.StopForUpdateAsync(), Task.WhenAll(TraceWorkspace.Sessions.ToArray().Select(s => s.StopForUpdateAsync())));
        Ping.Store.Preferences.TraceTarget = Trace.Target;
        await Ping.Store.SaveAsync();
    }
}
