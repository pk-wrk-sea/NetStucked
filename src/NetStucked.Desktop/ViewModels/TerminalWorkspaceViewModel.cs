using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetStucked.Core;
using NetStucked.Desktop.Services;

namespace NetStucked.Desktop.ViewModels;

public interface ITerminalRenderer
{
    Task BeginAsync(Guid tab, Guid generation);
    Task WriteAsync(Guid tab, TerminalFrame frame, CancellationToken token);
    Task RemoveAsync(Guid tab);
    Task StopAsync();
}

public partial class TerminalTabViewModel : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();
    internal TerminalSession Session { get; }
    internal Task Pump { get; set; } = Task.CompletedTask;
    internal CancellationTokenSource Rendering { get; set; } = new();
    internal long Revision { get; set; }
    internal bool Closing { get; set; }
    internal Task Connecting { get; set; } = Task.CompletedTask;
    [ObservableProperty] private string _name = "New session";
    [ObservableProperty] private string _host = "";
    [ObservableProperty] private int _port = 22;
    [ObservableProperty] private TerminalProtocol _protocol;
    [ObservableProperty] private TerminalAuthentication _authentication;
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _hostPin = "";
    [ObservableProperty] private string _keyPath = "";
    [ObservableProperty] private bool _allowTelnet;
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private TerminalState _state;
    [ObservableProperty] private string _message = "Enter an authorized host and Connect. New SSH host keys ask for confirmation.";
    public bool CanEdit => !Busy && State is not (TerminalState.Connected or TerminalState.Connecting or TerminalState.Disconnecting);
    public bool IsSsh => Protocol == TerminalProtocol.Ssh;
    public bool IsTelnet => !IsSsh;
    public bool IsPrivateKey => Authentication == TerminalAuthentication.PrivateKey;
    public bool IsPassword => !IsPrivateKey;
    partial void OnBusyChanged(bool value) => OnPropertyChanged(nameof(CanEdit));
    partial void OnStateChanged(TerminalState value) => OnPropertyChanged(nameof(CanEdit));
    partial void OnProtocolChanged(TerminalProtocol value) { if (Port is 22 or 23) Port = value == TerminalProtocol.Ssh ? 22 : 23; OnPropertyChanged(nameof(IsSsh)); OnPropertyChanged(nameof(IsTelnet)); }
    partial void OnAuthenticationChanged(TerminalAuthentication value) { OnPropertyChanged(nameof(IsPrivateKey)); OnPropertyChanged(nameof(IsPassword)); }
    internal TerminalTabViewModel(ITerminalTransport transport) => Session = new(transport);
    public TerminalProfile Profile(Guid? id = null) => new(id ?? Id, string.IsNullOrWhiteSpace(Name) ? Host.Trim() : Name.Trim(), Host.Trim().Trim('[', ']'), Port, Protocol, Username, HostPin.Trim(), Authentication, KeyPath, AllowTelnet);
    internal void Load(TerminalProfile p) { Name = p.Name; Host = p.Host; Protocol = p.Protocol; Port = p.Port; Username = p.Username; HostPin = p.HostPin; Authentication = p.Authentication; KeyPath = p.KeyPath; AllowTelnet = p.AllowTelnet; }
}

public partial class TerminalWorkspaceViewModel : ObservableObject, IAsyncDisposable
{
    private const string DefaultMessage = "Up to 5 independent sessions · credentials stay in memory · no automatic connections";
    private const string TabLimitMessage = "Maximum 5 terminal tabs. Close one before adding another.";
    private readonly ITerminalTransport _transport;
    private readonly ITerminalProfileStore _store;
    private readonly IDesktopDialogs _dialogs;
    private readonly ITerminalKnownHostStore _knownHosts;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private bool _loaded, _disposed, _stopping;
    public ITerminalRenderer? Renderer { get; set; }
    public ObservableCollection<TerminalTabViewModel> Tabs { get; } = [];
    public ObservableCollection<TerminalProfile> Profiles { get; } = [];
    public ObservableCollection<TerminalKnownHost> KnownHosts { get; } = [];
    [ObservableProperty] private TerminalKnownHost? _selectedKnownHost;
    [ObservableProperty] private string _hostKeysMessage = "";
    public IReadOnlyList<TerminalProtocol> Protocols { get; } = Enum.GetValues<TerminalProtocol>();
    public IReadOnlyList<TerminalAuthentication> Authentications { get; } = Enum.GetValues<TerminalAuthentication>();
    [ObservableProperty] private TerminalTabViewModel _selectedTab;
    [ObservableProperty] private TerminalProfile? _selectedProfile;
    [ObservableProperty] private string _message = DefaultMessage;
    partial void OnSelectedProfileChanged(TerminalProfile? value) { if (value is not null && SelectedTab.CanEdit) SelectedTab.Load(value); }
    partial void OnSelectedTabChanged(TerminalTabViewModel value) => SelectedProfile = null;
    public TerminalWorkspaceViewModel(ITerminalTransport transport, ITerminalProfileStore store, IDesktopDialogs dialogs, ITerminalKnownHostStore knownHosts)
    { _transport = transport; _store = store; _dialogs = dialogs; _knownHosts = knownHosts; _selectedTab = CreateTab(); Tabs.Add(_selectedTab); }
    private TerminalTabViewModel CreateTab()
    {
        var tab = new TerminalTabViewModel(_transport);
        tab.Session.Changed += status => _dispatcher.BeginInvoke(new Action(() => { if (status.Generation != tab.Session.Generation) return; tab.State = status.State; tab.Message = status.Message; }));
        return tab;
    }
    public async Task LoadAsync()
    {
        if (_loaded) return; _loaded = true;
        try { foreach (var p in await _store.LoadAsync(CancellationToken.None)) Profiles.Add(p); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { Message = "Saved profiles could not be read; no connections were started."; }
    }
    [RelayCommand] private void AddTab() { if (_disposed || _stopping) return; if (Tabs.Count >= 5) { Message = TabLimitMessage; return; } var tab = CreateTab(); Tabs.Add(tab); SelectedTab = tab; ClearTabLimitMessage(); }
    private void ClearTabLimitMessage() { if (Message == TabLimitMessage && Tabs.Count < 5) Message = DefaultMessage; }
    [RelayCommand] private async Task CloseTabAsync()
    {
        var tab = SelectedTab;
        if (tab.State is TerminalState.Connected or TerminalState.Connecting && !_dialogs.Confirm("Close terminal", "Disconnect this session and close its tab?")) return;
        tab.Closing = true; await StopTabAsync(tab); await tab.Connecting; await tab.Session.DisposeAsync(); tab.Rendering.Dispose();
        if (Renderer is not null) await Renderer.RemoveAsync(tab.Id);
        if (Tabs.Count == 1) Tabs.Add(CreateTab()); SelectedTab = Tabs.First(t => t != tab); Tabs.Remove(tab);
        ClearTabLimitMessage();
    }
    public Task ConnectAsync(string password, string passphrase)
    {
        var tab = SelectedTab;
        if (_disposed || _stopping || tab.Closing || !tab.CanEdit || Renderer is null) return Task.CompletedTask;
        return tab.Connecting = ConnectTabAsync(tab, password, passphrase);
    }
    private async Task ConnectTabAsync(TerminalTabViewModel tab, string password, string passphrase)
    {
        tab.Busy = true; long revision = ++tab.Revision;
        try
        {
            // Legacy per-profile pins remain round-trippable for older builds; the host-key database governs this UI.
            var request = new TerminalRequest(tab.Profile() with { HostPin = "" }, new(password, passphrase)); request.Validate();
            await DrainTabAsync(tab);
            // Renderer is ready before any network connection or credentials are sent.
            await Renderer!.BeginAsync(tab.Id, Guid.Empty);
            if (_disposed || tab.Closing || tab.Revision != revision) return;
            await tab.Session.ConnectAsync(request, CancellationToken.None);
            if (_disposed || tab.Closing || tab.Revision != revision) { await tab.Session.DisconnectAsync(); return; }
            Guid generation = tab.Session.Generation;
            await Renderer.BeginAsync(tab.Id, generation);
            tab.Rendering.Dispose(); tab.Rendering = new();
            if (tab.Session.Output is { } output) tab.Pump = PumpAsync(tab, output, tab.Rendering.Token);
        }
        catch (ArgumentException ex) { tab.Message = ex.Message; }
        catch (Exception) { await tab.Session.DisconnectAsync(); tab.Message = "Terminal renderer unavailable. Check Microsoft Edge WebView2 Runtime; no connection remains active."; }
        finally { tab.Busy = false; }
    }
    private async Task PumpAsync(TerminalTabViewModel tab, System.Threading.Channels.ChannelReader<TerminalFrame> output, CancellationToken token)
    {
        try { await foreach (var frame in output.ReadAllAsync(token)) if (Renderer is not null) await Renderer.WriteAsync(tab.Id, frame, token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) { await tab.Session.DisconnectAsync(); tab.Message = "Terminal rendering stopped; connection closed to prevent loss of output."; }
    }
    public async Task ReceiveAsync(Guid id, Guid generation, byte[] bytes)
    {
        var tab = Tabs.FirstOrDefault(t => t.Id == id);
        if (tab is null || tab.Session.Generation != generation || tab.Session.State != TerminalState.Connected) return;
        if (!tab.Session.TrySend(generation, bytes)) { await tab.Session.DisconnectAsync(); tab.Message = "Input queue limit reached; session closed. Check the device before reconnecting."; }
    }
    public async Task ResizeAsync(Guid id, Guid generation, int cols, int rows)
    {
        var tab = Tabs.FirstOrDefault(t => t.Id == id);
        if (tab is null || tab.Session.Generation != generation) return;
        try { await tab.Session.ResizeAsync(cols, rows, CancellationToken.None); }
        catch (Exception) { await tab.Session.DisconnectAsync(); tab.Message = "Terminal resize failed; connection closed."; }
    }
    [RelayCommand] private Task DisconnectAsync() => StopTabAsync(SelectedTab);
    private static async Task StopTabAsync(TerminalTabViewModel tab)
    {
        tab.Revision++; await DrainTabAsync(tab);
    }
    private static async Task DrainTabAsync(TerminalTabViewModel tab)
    {
        await tab.Rendering.CancelAsync(); await tab.Session.DisconnectAsync(); await tab.Pump;
    }
    [RelayCommand] private async Task SaveProfileAsync()
    {
        if (!SelectedTab.CanEdit) return;
        try
        {
            var p = SelectedTab.Profile(SelectedProfile?.Id);
            var list = Profiles.Where(x => x.Id != p.Id).Append(p).ToArray();
            await _store.SaveAsync(list, CancellationToken.None);
            Profiles.Clear(); foreach (var item in list) Profiles.Add(item); SelectedProfile = p; Message = "Profile saved. Passwords and passphrases are excluded.";
        }
        catch (ArgumentException ex) { Message = ex.Message; }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { Message = "Unable to save profiles. Check folder access."; }
    }
    [RelayCommand] private async Task DeleteProfileAsync()
    {
        if (SelectedProfile is not { } profile || !_dialogs.Confirm("Delete profile", "Delete saved profile " + profile.Name + "?")) return;
        try { await _store.SaveAsync(Profiles.Where(p => p.Id != profile.Id).ToArray(), CancellationToken.None); Profiles.Remove(profile); SelectedProfile = null; }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { Message = "Unable to delete profile. Check folder access."; }
    }
    public bool ApprovePaste(TerminalTabViewModel tab, string text)
    {
        if (tab.State != TerminalState.Connected || SelectedTab != tab) return false;
        if (!TerminalInputPolicy.ValidPaste(text)) { tab.Message = "Paste blocked: maximum 65,536 characters; no control characters except newline and tab."; return false; }
        return !TerminalInputPolicy.RequiresConfirmation(text) || _dialogs.Confirm("Confirm multiline paste", "This paste contains line breaks and may execute multiple commands on " + tab.Host + ". Send it?");
    }
    [RelayCommand] private async Task RefreshHostKeysAsync()
    {
        try { var hosts = await _knownHosts.LoadAsync(CancellationToken.None); KnownHosts.Clear(); foreach (var host in hosts.OrderBy(h => h.Host).ThenBy(h => h.Port)) KnownHosts.Add(host); HostKeysMessage = KnownHosts.Count + " saved host key(s)."; }
        catch (Exception ex) when (ex is System.IO.IOException or System.IO.InvalidDataException or UnauthorizedAccessException) { HostKeysMessage = "Saved SSH host keys could not be read. Check terminal-known-hosts.json and folder access."; }
    }
    [RelayCommand] private async Task RemoveHostKeyAsync()
    {
        if (SelectedKnownHost is not { } host || !_dialogs.Confirm("Remove saved host key", "Forget the saved SSH identity for " + host.Endpoint + "? You will be asked again next connection.")) return;
        try { bool removed = await _knownHosts.RemoveAsync(host, CancellationToken.None); await RefreshHostKeysAsync(); if (!removed) HostKeysMessage = "This key changed since the list was loaded. Review the refreshed list."; }
        catch (Exception ex) when (ex is System.IO.IOException or System.IO.InvalidDataException or UnauthorizedAccessException) { HostKeysMessage = "Unable to remove saved key. Check folder access; no key was approved."; }
    }
    public async Task StopForUpdateAsync() { _stopping = true; try { foreach (var tab in Tabs.ToArray()) { await StopTabAsync(tab); await tab.Connecting; } } finally { _stopping = false; } }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return; _disposed = true;
        await StopForUpdateAsync(); foreach (var tab in Tabs) { await tab.Session.DisposeAsync(); tab.Rendering.Dispose(); }
        if (Renderer is not null) await Renderer.StopAsync(); Renderer = null;
    }
}
