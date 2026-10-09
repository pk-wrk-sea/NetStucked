using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetStucked.Core;
using NetStucked.Infrastructure;
using NetStucked.Desktop.Services;

namespace NetStucked.Desktop.ViewModels;

public sealed partial class WifiProfileRow : ObservableObject
{
    public WifiProfile Profile { get; private set; }
    public string Name => Profile.Name;
    public string Ssid => Profile.Ssid;
    public string Security => Profile.Security;
    public string Eap => Profile.Eap;
    public string Auto => Profile.AutoConnect ? "Auto" : "Manual";
    public int Priority => Profile.Priority;
    public string Management => Profile.PolicyManaged ? "Company policy" : "Windows profile";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _status = "Saved";
    [ObservableProperty] private string _signal = "—";
    [ObservableProperty] private string _lastUsed = "—";
    public WifiProfileRow(WifiProfile profile) => Profile = profile;
    public void Update(WifiProfile profile, WifiSnapshot snapshot, WifiProfileMetadata metadata)
    {
        Profile = profile; Description = metadata.Description; LastUsed = metadata.LastUsed?.ToString("yyyy-MM-dd HH:mm") ?? "—";
        var network = snapshot.Networks.FirstOrDefault(n => n.ProfileName == Name) ?? snapshot.Networks.FirstOrDefault(n => n.SsidHex == profile.SsidHex && n.Security == profile.Security);
        Status = snapshot.ConnectedProfile == Name ? "Connected" : network is { Connectable: true } ? "Available" : "Saved";
        Signal = snapshot.ConnectedProfile == Name && snapshot.Signal is { } quality ? $"{quality}%" : network is not null ? $"{network.Signal}%" : "—";
        foreach (string key in new[] { nameof(Security), nameof(Eap), nameof(Auto), nameof(Priority), nameof(Management) }) OnPropertyChanged(key);
    }
}

public sealed partial class NetworkInfoViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IWifiService _wifi;
    private readonly IWifiCredentialVault _vault;
    private readonly IWifiServiceTests _tests;
    private readonly IDesktopDialogs _dialogs;
    private readonly UserSettingsStore _store;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _wake = new(0, 1);
    private Task? _monitor;
    private CancellationTokenSource? _monitorCancellation;
    private Task _operationTask = Task.CompletedTask;
    private CancellationTokenSource? _operation;
    private bool _disposed, _visible, _testing;
    private bool _savePending;
    private bool _readUnavailable;
    private WifiSnapshot? _snapshot;
    public UserSettingsStore Store => _store;
    private string? _identity;
    public event Action<WifiEvent>? TransitionObserved;
    public ObservableCollection<WifiAdapter> Adapters { get; } = [];
    public ObservableCollection<WifiProfileRow> Rows { get; } = [];
    public ObservableCollection<WifiNetwork> AvailableNetworks { get; } = [];
    public ObservableCollection<ServiceTestProfile> TestProfiles { get; } = [];
    public ObservableCollection<ServiceTestResult> TestResults { get; } = [];
    public ObservableCollection<WifiEvent> Events { get; } = [];
    public ICollectionView Profiles { get; }
    public ICollectionView ConnectionHistory { get; }
    public string ProfileCount => $"Saved Wi-Fi Profiles ({Rows.Count})";
    [ObservableProperty] private WifiAdapter? _selectedAdapter;
    [ObservableProperty] private WifiProfileRow? _selectedProfile;
    [ObservableProperty] private WifiNetwork? _selectedNetwork;
    [ObservableProperty] private ServiceTestProfile? _selectedTestProfile;
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _message = "Select a wireless adapter. Profiles and credentials are managed by Windows.";
    [ObservableProperty] private string _connectionStatus = "Not observed";
    [ObservableProperty] private string _ipAddresses = "—";
    [ObservableProperty] private string _gateway = "—";
    [ObservableProperty] private string _dnsServers = "—";
    [ObservableProperty] private string _connectedSsid = "—";
    [ObservableProperty] private string _signal = "—";
    [ObservableProperty] private string _observedAt = "—";
    [ObservableProperty] private string _testSummary = "Configure company endpoints before testing.";
    [ObservableProperty] private WifiEvent? _selectedHistory;
    [ObservableProperty] private bool _isBusy;
    public bool CanUseAdapter => !IsBusy && SelectedAdapter is not null && !_disposed && !_readUnavailable;
    public bool CanUseProfile => CanUseAdapter && SelectedProfile is not null;
    public bool CanModifyProfile => CanUseProfile && !SelectedProfile!.Profile.PolicyManaged;
    public bool CanTest => CanUseProfile && SelectedTestProfile is not null;
    public bool CanDisconnect => CanUseAdapter && _snapshot?.ConnectedProfile is not null;
    public string CredentialGuide => SelectedProfile?.Profile.Eap switch
    {
        "PEAP / MSCHAPv2" => "Windows EAP credentials • optional Windows Credential Manager storage • server trust policy preserved",
        "EAP-TLS (certificate)" => "Windows EAP-TLS profile and Certificate Store • valid client certificate/private key required • managed by IT",
        "—" => "Personal key stays in the Windows WLAN profile. Export excludes passwords.",
        _ => "Authentication is managed by Windows / your organization. Import the IT-provisioned profile."
    };
    public NetworkInfoViewModel(IWifiService wifi, IWifiCredentialVault vault, IWifiServiceTests tests, IDesktopDialogs dialogs, UserSettingsStore store)
    {
        _wifi = wifi; _vault = vault; _tests = tests; _dialogs = dialogs; _store = store;
        Profiles = CollectionViewSource.GetDefaultView(Rows);
        Profiles.Filter = item => item is WifiProfileRow row && (Search.Length == 0 || (row.Name + " " + row.Ssid + " " + row.Description).Contains(Search, StringComparison.OrdinalIgnoreCase));
        ConnectionHistory = new ListCollectionView(Events) { Filter = item => item is WifiEvent e && e.Event is "Connect" or "Disconnect" or "Network transition" or "Connect & Test" };
        foreach (var item in store.Preferences.WifiTestProfiles) TestProfiles.Add(item);
        SelectedTestProfile = TestProfiles.FirstOrDefault();
        foreach (var item in store.Preferences.WifiHistory) Events.Add(item);
        NetworkChange.NetworkAddressChanged += AddressChanged;
    }
    public void SetPresentationActive(bool visible)
    {
        _visible = visible;
        if (visible) EnsureMonitor();
        if (visible) Wake();
    }
    public void ObserveWhileDiagnosticsActive() => EnsureMonitor();
    private void EnsureMonitor() { if (!_disposed && _monitor is null) { _monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); _monitor = Task.Run(() => MonitorAsync(_monitorCancellation.Token)); } }
    private void AddressChanged(object? sender, EventArgs args) => Wake();
    private void Wake() { try { if (!_disposed && _wake.CurrentCount == 0) _wake.Release(); } catch (SemaphoreFullException) { } catch (ObjectDisposedException) { } }
    private async Task MonitorAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                Guid? selected = await _dispatcher.InvokeAsync(() => SelectedAdapter?.Id);
                bool busy = await _dispatcher.InvokeAsync(() => IsBusy && !_testing);
                if (!busy)
                {
                    var adapters = await _wifi.GetAdaptersAsync(token).ConfigureAwait(false);
                    await _dispatcher.InvokeAsync(() => ApplyAdapters(adapters));
                    selected = await _dispatcher.InvokeAsync(() => SelectedAdapter?.Id);
                    if (selected is { } id)
                    {
                        var snapshot = await _wifi.ReadAsync(id, token).ConfigureAwait(false);
                        await _dispatcher.InvokeAsync(() => ApplySnapshot(snapshot));
                    }
                    var save = await _dispatcher.InvokeAsync(() => _savePending && !IsBusy ? SaveObservedEventsAsync() : Task.CompletedTask);
                    try { await save.ConfigureAwait(false); }
                    catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.IO.InvalidDataException)
                    { await _dispatcher.InvokeAsync(() => Message = "Observed network context is current; local history was not saved: " + ex.Message); }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    _readUnavailable = true; _snapshot = null; _identity = null;
                    ConnectionStatus = "Unavailable"; IpAddresses = Gateway = DnsServers = ConnectedSsid = Signal = ObservedAt = "—";
                    foreach (var row in Rows) { row.Status = "Unknown"; row.Signal = "—"; }
                    if (_testing) { TestResults.Clear(); TestSummary = "Network context unavailable. Results invalidated; run again."; _operation?.Cancel(); }
                    if (!IsBusy) Message = ex.Message;
                    NotifyActions();
                });
            }
            try { await _wake.WaitAsync(TimeSpan.FromSeconds(_visible || _testing ? 2 : 5), token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }
    private void ApplyAdapters(IReadOnlyList<WifiAdapter> adapters)
    {
        if (_disposed) return;
        Guid? id = SelectedAdapter?.Id ?? _store.Preferences.WifiAdapter;
        if (!Adapters.Select(a => (a.Id, a.Name)).SequenceEqual(adapters.Select(a => (a.Id, a.Name))))
        {
            Adapters.Clear(); foreach (var adapter in adapters) Adapters.Add(adapter);
            SelectedAdapter = Adapters.FirstOrDefault(a => a.Id == id) ?? Adapters.FirstOrDefault();
        }
        if (adapters.Count == 0) { SelectedAdapter = null; Message = "No wireless adapter found. Check WLAN AutoConfig, adapter/driver and Windows Wi-Fi settings."; }
    }
    partial void OnSelectedAdapterChanged(WifiAdapter? value)
    {
        _identity = null; _snapshot = null; Rows.Clear(); AvailableNetworks.Clear(); SelectedProfile = null;
        _store.Preferences.WifiAdapter = value?.Id; _operation?.Cancel();
        ConnectionStatus = "Not observed"; IpAddresses = Gateway = DnsServers = ConnectedSsid = Signal = "—";
        NotifyActions(); OnPropertyChanged(nameof(ProfileCount)); Wake();
    }
    partial void OnSelectedProfileChanged(WifiProfileRow? value)
    {
        if (value is not null && _store.Preferences.WifiProfiles.TryGetValue(Key(value.Name), out var metadata) && metadata.TestProfileId is { } id) SelectedTestProfile = TestProfiles.FirstOrDefault(p => p.Id == id) ?? SelectedTestProfile;
        NotifyActions(); OnPropertyChanged(nameof(CredentialGuide));
    }
    partial void OnSelectedTestProfileChanged(ServiceTestProfile? value)
    {
        if (SelectedProfile is not null) { var key = Key(SelectedProfile.Name); var old = _store.Preferences.WifiProfiles.GetValueOrDefault(key) ?? new(); RememberMetadata(key, old with { TestProfileId = value?.Id }); }
        NotifyActions();
    }
    partial void OnSearchChanged(string value) => Profiles.Refresh();
    partial void OnSelectedHistoryChanged(WifiEvent? value)
    {
        if (IsBusy || value?.Event != "Connect & Test") return;
        var run = _store.Preferences.WifiTestRuns.LastOrDefault(r => r.Time == value.Time);
        if (run is null) return;
        TestResults.Clear(); foreach (var result in run.Results) TestResults.Add(result);
        TestSummary = $"Recorded {run.Time:MM-dd HH:mm:ss} · {run.Ssid} · {run.TestProfile} · {run.Status}";
    }
    partial void OnIsBusyChanged(bool value) => NotifyActions();
    private void NotifyActions()
    {
        foreach (string name in new[] { nameof(CanUseAdapter), nameof(CanUseProfile), nameof(CanModifyProfile), nameof(CanTest), nameof(CanDisconnect) }) OnPropertyChanged(name);
        ScanCommand.NotifyCanExecuteChanged(); ConnectCommand.NotifyCanExecuteChanged(); DisconnectCommand.NotifyCanExecuteChanged(); AddProfileCommand.NotifyCanExecuteChanged(); EditProfileCommand.NotifyCanExecuteChanged(); DeleteProfileCommand.NotifyCanExecuteChanged(); ImportCommand.NotifyCanExecuteChanged(); ExportCommand.NotifyCanExecuteChanged(); CredentialsCommand.NotifyCanExecuteChanged(); ForgetCredentialsCommand.NotifyCanExecuteChanged(); ConnectAndTestCommand.NotifyCanExecuteChanged(); RunTestsCommand.NotifyCanExecuteChanged(); EditTestProfileCommand.NotifyCanExecuteChanged(); AddTestProfileCommand.NotifyCanExecuteChanged(); DeleteTestProfileCommand.NotifyCanExecuteChanged();
    }
    private string Key(string profile) => SelectedAdapter!.Id.ToString("N") + "/" + profile;
    private void RememberMetadata(string key, WifiProfileMetadata value)
    {
        if (!_store.Preferences.WifiProfiles.ContainsKey(key) && _store.Preferences.WifiProfiles.Count >= 1024) { Message = "Nonsecret Wi-Fi metadata limit reached (1024 profiles). Windows profiles remain available."; return; }
        _store.Preferences.WifiProfiles[key] = value;
    }
    private void ApplySnapshot(WifiSnapshot snapshot)
    {
        if (_disposed || SelectedAdapter?.Id != snapshot.Adapter.Id) return;
        _readUnavailable = false;
        if (_identity is not null && _identity != snapshot.Identity)
        {
            var transition = new WifiEvent(DateTimeOffset.Now, snapshot.Adapter.Name, snapshot.ConnectedProfile ?? "—", "Network transition", "Observed", $"{_snapshot?.ConnectedSsid ?? "Disconnected"} → {snapshot.ConnectedSsid ?? "Disconnected"}; IP {snapshot.Ip.Identity}. Diagnostics may time out while routes change.");
            AddEvent(transition); TransitionObserved?.Invoke(transition);
            if (_testing) { TestSummary = "Network changed during testing. Results invalidated; run again."; TestResults.Clear(); _operation?.Cancel(); }
        }
        _identity = snapshot.Identity; _snapshot = snapshot;
        string? selected = SelectedProfile?.Name;
        foreach (var removed in Rows.Where(r => !snapshot.Profiles.Any(p => p.Name == r.Name)).ToArray()) Rows.Remove(removed);
        foreach (var profile in snapshot.Profiles)
        {
            var row = Rows.FirstOrDefault(r => r.Name == profile.Name);
            if (row is null) { row = new(profile); Rows.Add(row); }
            row.Update(profile, snapshot, _store.Preferences.WifiProfiles.GetValueOrDefault(Key(profile.Name)) ?? new());
        }
        SelectedProfile = Rows.FirstOrDefault(r => r.Name == selected) ?? Rows.FirstOrDefault(r => r.Name == snapshot.ConnectedProfile);
        if (!AvailableNetworks.SequenceEqual(snapshot.Networks)) { AvailableNetworks.Clear(); foreach (var network in snapshot.Networks.OrderByDescending(n => n.Signal)) AvailableNetworks.Add(network); }
        ConnectionStatus = snapshot.Adapter.State; ConnectedSsid = snapshot.ConnectedSsid ?? "—";
        IpAddresses = snapshot.Ip.Addresses.Count == 0 ? "—" : string.Join("\n", snapshot.Ip.Addresses);
        Gateway = snapshot.Ip.Gateways.Count == 0 ? "—" : string.Join(", ", snapshot.Ip.Gateways);
        DnsServers = snapshot.Ip.DnsServers.Count == 0 ? "—" : string.Join(", ", snapshot.Ip.DnsServers);
        Signal = snapshot.Signal is { } signal ? $"{signal}%" : "—"; ObservedAt = snapshot.ObservedAt.ToString("HH:mm:ss");
        if (!IsBusy) Message = snapshot.Notice ?? "Windows WLAN profiles • signal is measured quality (%) • scans run only when requested";
        OnPropertyChanged(nameof(ProfileCount)); OnPropertyChanged(nameof(CredentialGuide)); Profiles.Refresh(); NotifyActions();
    }
    private void AddEvent(WifiEvent entry)
    {
        entry = entry with { Message = entry.Message[..Math.Min(entry.Message.Length, 1024)] };
        Events.Add(entry); while (Events.Count > 500) Events.RemoveAt(0);
        _store.Preferences.WifiHistory = Events.ToList();
        _savePending = true;
    }
    private async Task SaveObservedEventsAsync() { _savePending = false; try { await _store.SaveAsync(); } catch { _savePending = true; throw; } }
    private Task Execute(Func<CancellationToken, Task> work, string operation)
    {
        if (IsBusy || _disposed) return Task.CompletedTask;
        return _operationTask = ExecuteCoreAsync(work, operation);
    }
    private async Task ExecuteCoreAsync(Func<CancellationToken, Task> work, string name)
    {
        IsBusy = true; _operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); Message = name + "…";
        try
        {
            await work(_operation.Token); _operation.Token.ThrowIfCancellationRequested();
            try { await _store.SaveAsync(_operation.Token); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.IO.InvalidDataException)
            { Message = "Windows operation completed; local preferences/history were not saved: " + ex.Message; AddEvent(new(DateTimeOffset.Now, SelectedAdapter?.Name ?? "—", SelectedProfile?.Name ?? "—", "Save preferences", "Failed", ex.Message)); }
        }
        catch (OperationCanceledException) { Message = name + " cancelled. Completed network operations are not rolled back."; }
        catch (Exception ex) { Message = name + ": " + ex.Message; AddEvent(new(DateTimeOffset.Now, SelectedAdapter?.Name ?? "—", SelectedProfile?.Name ?? "—", name, "Failed", ex.Message)); }
        finally { _testing = false; _operation.Dispose(); _operation = null; IsBusy = false; Wake(); }
    }
    [RelayCommand] private void Cancel() => _operation?.Cancel();
    [RelayCommand(CanExecute = nameof(CanUseAdapter))] private Task ScanAsync() => Execute(async t => { await _wifi.ScanAsync(SelectedAdapter!.Id, t); ApplySnapshot(await _wifi.ReadAsync(SelectedAdapter.Id, t)); Message = "Scan completed."; }, "Scan SSIDs");
    [RelayCommand(CanExecute = nameof(CanUseProfile))] private Task ConnectAsync() => Execute(t => ConnectSelectedAsync(t), "Connect");
    private async Task ConnectSelectedAsync(CancellationToken token)
    {
        Guid adapter = SelectedAdapter!.Id; string profile = SelectedProfile!.Name;
        var credential = SelectedProfile.Eap == "PEAP / MSCHAPv2" && !SelectedProfile.Profile.PolicyManaged ? await Task.Run(() => _vault.Read(adapter, profile), token) : null;
        if (credential is not null) await _wifi.SetEnterpriseCredentialsAsync(adapter, profile, credential, token);
        await _wifi.ConnectAsync(adapter, profile, token);
        var snapshot = await _wifi.ReadAsync(adapter, token); ApplySnapshot(snapshot);
        if (snapshot.ConnectedProfile != profile) throw new InvalidOperationException("Windows connection no longer matches the chosen profile.");
        var key = Key(profile); RememberMetadata(key, (_store.Preferences.WifiProfiles.GetValueOrDefault(key) ?? new()) with { LastUsed = DateTimeOffset.Now });
        AddEvent(new(DateTimeOffset.Now, SelectedAdapter.Name, profile, "Connect", "Success", "Windows confirmed the selected profile. IP configuration may still be pending."));
        Message = "Connected to " + profile;
    }
    [RelayCommand(CanExecute = nameof(CanDisconnect))] private Task DisconnectAsync() => Execute(async t => { string profile = _snapshot!.ConnectedProfile!; await _wifi.DisconnectAsync(SelectedAdapter!.Id, t); ApplySnapshot(await _wifi.ReadAsync(SelectedAdapter.Id, t)); AddEvent(new(DateTimeOffset.Now, SelectedAdapter.Name, profile, "Disconnect", "Success", "Disconnected by user.")); }, "Disconnect");
    [RelayCommand(CanExecute = nameof(CanUseAdapter))] private Task AddProfileAsync()
    {
        if (SelectedNetwork is not null && SelectedNetwork.Security is not ("WPA2-Personal" or "WPA3-Personal" or "Open"))
        {
            Message = "Import your IT-provisioned Windows WLAN XML for Enterprise/other authentication. NetStucked does not generate company trust policy.";
            return Task.CompletedTask;
        }
        var input = _dialogs.EditWifiProfile(new("", SelectedNetwork?.Ssid ?? "", SelectedNetwork?.Security is "WPA3-Personal" or "Open" ? SelectedNetwork.Security : "WPA2-Personal", false, "", ""), true);
        if (input is null) return Task.CompletedTask;
        return Execute(async t => { await _wifi.SaveProfileAsync(SelectedAdapter!.Id, WifiProfileXml.Personal(input), false, t); RememberMetadata(Key(input.Name), new(input.Description)); ApplySnapshot(await _wifi.ReadAsync(SelectedAdapter.Id, t)); SelectedProfile = Rows.FirstOrDefault(r => r.Name == input.Name); }, "Add profile");
    }
    [RelayCommand(CanExecute = nameof(CanModifyProfile))] private Task EditProfileAsync()
    {
        var row = SelectedProfile!; var metadata = _dialogs.EditWifiMetadata(row.Description, row.Profile.AutoConnect);
        if (metadata is null) return Task.CompletedTask;
        return Execute(async t => { if (metadata.AutoConnect != row.Profile.AutoConnect) await _wifi.SaveProfileAsync(SelectedAdapter!.Id, WifiProfileXml.SetAutoConnect(row.Profile.Xml, metadata.AutoConnect), true, t); var key = Key(row.Name); RememberMetadata(key, (_store.Preferences.WifiProfiles.GetValueOrDefault(key) ?? new()) with { Description = metadata.Description }); ApplySnapshot(await _wifi.ReadAsync(SelectedAdapter!.Id, t)); }, "Edit profile");
    }
    [RelayCommand(CanExecute = nameof(CanModifyProfile))] private Task DeleteProfileAsync()
    {
        string name = SelectedProfile!.Name;
        if (!_dialogs.ConfirmWifiDeletion(name)) return Task.CompletedTask;
        return Execute(async t => { var adapter = SelectedAdapter!.Id; await _wifi.DeleteProfileAsync(adapter, name, t); await Task.Run(() => _vault.Delete(adapter, name), t); _store.Preferences.WifiProfiles.Remove(Key(name)); ApplySnapshot(await _wifi.ReadAsync(adapter, t)); }, "Delete profile");
    }
    [RelayCommand(CanExecute = nameof(CanUseAdapter))] private Task ImportAsync() => Execute(async t =>
    {
        string? xml = await _dialogs.LoadWifiProfileAsync(); if (xml is null) return;
        WifiProfile profile = WifiProfileXml.Describe(xml, 2, 1);
        if (Rows.Any(r => r.Name == profile.Name)) throw new ArgumentException("A profile with this name exists. Import does not overwrite existing profiles.");
        if (!_dialogs.ConfirmWifiImport(profile)) return;
        if (profile.Security.EndsWith("Personal") && !WifiProfileXml.Parse(xml).Descendants().Any(e => e.Name.LocalName == "keyMaterial"))
        {
            var input = _dialogs.EditWifiProfile(new(profile.Name, profile.Ssid, profile.Security, profile.AutoConnect, "", ""), false);
            if (input is null) return; xml = WifiProfileXml.ReplacePersonalKey(xml, input.Password);
        }
        await _wifi.SaveProfileAsync(SelectedAdapter!.Id, xml, false, t); ApplySnapshot(await _wifi.ReadAsync(SelectedAdapter.Id, t)); SelectedProfile = Rows.FirstOrDefault(r => r.Name == profile.Name);
    }, "Import profile");
    [RelayCommand(CanExecute = nameof(CanUseProfile))] private Task ExportAsync() => Execute(_ => _dialogs.SaveWifiProfileAsync(SelectedProfile!.Name, WifiProfileXml.ExportSafe(SelectedProfile.Profile.Xml)), "Export profile");
    [RelayCommand(CanExecute = nameof(CanModifyProfile))] private Task CredentialsAsync()
    {
        if (SelectedProfile!.Security.EndsWith("Personal", StringComparison.Ordinal))
        {
            var row = SelectedProfile; var input = _dialogs.EditWifiProfile(new(row.Name, row.Ssid, row.Security, row.Profile.AutoConnect, row.Description, ""), false);
            if (input is null) return Task.CompletedTask;
            return Execute(async t => { await _wifi.SaveProfileAsync(SelectedAdapter!.Id, WifiProfileXml.ReplacePersonalKey(row.Profile.Xml, input.Password), true, t); ApplySnapshot(await _wifi.ReadAsync(SelectedAdapter.Id, t)); Message = "Personal key updated in Windows WLAN. Disconnect and Connect to re-authenticate."; }, "Update Personal key");
        }
        if (SelectedProfile.Eap != "PEAP / MSCHAPv2") { _dialogs.ShowError(CredentialGuide + "\nUse Windows/IT provisioning for this method."); return Task.CompletedTask; }
        var credential = _dialogs.EditEnterpriseCredentials(); if (credential is null) return Task.CompletedTask;
        return Execute(async t => { Guid adapter = SelectedAdapter!.Id; string name = SelectedProfile.Name; await _wifi.SetEnterpriseCredentialsAsync(adapter, name, credential, t); await Task.Run(() => { if (credential.Remember) _vault.Write(adapter, name, credential); else _vault.Delete(adapter, name); }, t); Message = "Credentials supplied to Windows EAP."; }, "Set credentials");
    }
    [RelayCommand(CanExecute = nameof(CanModifyProfile))] private Task ForgetCredentialsAsync() => Execute(t => Task.Run(() => _vault.Delete(SelectedAdapter!.Id, SelectedProfile!.Name), t), "Forget app credential");
    [RelayCommand(CanExecute = nameof(CanUseAdapter))] private void AddTestProfile() => EditTest(null);
    [RelayCommand(CanExecute = nameof(CanTest))] private void EditTestProfile() => EditTest(SelectedTestProfile);
    private void EditTest(ServiceTestProfile? original)
    {
        if (TestProfiles.Count >= 100 && original is null) { Message = "Maximum 100 service test profiles."; return; }
        var edited = _dialogs.EditServiceTestProfile(original ?? new()); if (edited is null) return;
        if (original is not null) TestProfiles.Remove(original); TestProfiles.Add(edited); SelectedTestProfile = edited; _store.Preferences.WifiTestProfiles = TestProfiles.ToList();
    }
    [RelayCommand(CanExecute = nameof(CanTest))] private void DeleteTestProfile()
    {
        var selected = SelectedTestProfile!; TestProfiles.Remove(selected); SelectedTestProfile = TestProfiles.FirstOrDefault();
        foreach (string key in _store.Preferences.WifiProfiles.Where(p => p.Value.TestProfileId == selected.Id).Select(p => p.Key).ToArray()) _store.Preferences.WifiProfiles[key] = _store.Preferences.WifiProfiles[key] with { TestProfileId = null };
        _store.Preferences.WifiTestProfiles = TestProfiles.ToList();
    }
    [RelayCommand(CanExecute = nameof(CanTest))] private Task ConnectAndTestAsync() => Execute(async t => { await ConnectSelectedAsync(t); await TestAsync(t); }, "Connect & Test");
    [RelayCommand(CanExecute = nameof(CanTest))] private Task RunTestsAsync() => Execute(t => TestAsync(t), "Connect & Test");
    private async Task TestAsync(CancellationToken token)
    {
        Guid adapter = SelectedAdapter!.Id; string name = SelectedProfile!.Name; var profile = SelectedTestProfile!;
        TestResults.Clear(); TestSummary = "Waiting for IP, gateway and DNS…";
        using var ready = CancellationTokenSource.CreateLinkedTokenSource(token); ready.CancelAfter(TimeSpan.FromSeconds(20));
        WifiSnapshot snapshot;
        try
        {
            do
            {
                snapshot = await _wifi.ReadAsync(adapter, ready.Token);
                if (snapshot.ConnectedProfile != name) throw new InvalidOperationException("Select the connected Wi-Fi profile or use Connect & Test.");
                if (snapshot.Ip.HasUsableAddress && (!profile.TestGateway || snapshot.Ip.Gateways.Count > 0) && (profile.DnsName.Length == 0 || snapshot.Ip.DnsServers.Count > 0)) break;
                await Task.Delay(250, ready.Token);
            } while (true);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("The selected adapter did not receive the IP/gateway/DNS required by this test within 20 seconds."); }
        ApplySnapshot(snapshot); _testing = true; TestSummary = "Testing through " + snapshot.Adapter.Name + "…";
        var results = await _tests.RunAsync(snapshot, profile, token);
        var after = await _wifi.ReadAsync(adapter, token); token.ThrowIfCancellationRequested();
        if (after.Identity != snapshot.Identity) { ApplySnapshot(after); TestSummary = "Network changed. Results invalidated; run again."; return; }
        foreach (var result in results) TestResults.Add(result);
        TestSummary = results.Any(r => r.Status == "FAIL") ? "FAIL — one or more checks failed" : results.All(r => r.Status == "PASS") ? "PASS — all configured checks passed" : "INCOMPLETE — some checks skipped";
        var time = DateTimeOffset.Now; string status = TestSummary.Split(' ')[0];
        _store.Preferences.WifiTestRuns.Add(new(time, snapshot.Adapter.Name, snapshot.ConnectedSsid ?? "—", name, profile.Name, status, results));
        while (_store.Preferences.WifiTestRuns.Count > 100) _store.Preferences.WifiTestRuns.RemoveAt(0);
        AddEvent(new(time, snapshot.Adapter.Name, name, "Connect & Test", status, profile.Name + ": " + string.Join("; ", results.Select(r => $"{r.Service} {r.Target}: {r.Status}, {r.LatencyMs?.ToString("0.0") ?? "—"} ms"))));
    }
    [RelayCommand] private Task ExportHistoryAsync() => Execute(_ => _dialogs.ExportAsync("wifi-history.csv", "Time,Adapter,Profile,Event,Result,Message\r\n" + string.Join("\r\n", Events.Select(e => string.Join(',', new[] { e.Time.ToString("O"), e.Adapter, e.Profile, e.Event, e.Result, e.Message }.Select(Quote))))), "Export history");
    [RelayCommand] private Task ExportTestsAsync() => Execute(_ => _dialogs.ExportAsync("wifi-services.csv", "Time,Service,Target,Status,LatencyMs,Message\r\n" + string.Join("\r\n", TestResults.Select(e => string.Join(',', new[] { e.Time.ToString("O"), e.Service, e.Target, e.Status, e.LatencyMs?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "", e.Message }.Select(Quote))))), "Export results");
    private static string Quote(string text) { string first = text.TrimStart(); bool formula = first.StartsWith('=') || first.StartsWith('+') || first.StartsWith('-') || first.StartsWith('@') || text.StartsWith('\t') || text.StartsWith('\r'); return "\"" + (formula ? "'" : "") + text.Replace("\"", "\"\"") + "\""; }
    public async Task StopForUpdateAsync()
    {
        _operation?.Cancel(); await _operationTask;
        _monitorCancellation?.Cancel();
        if (_monitor is not null) await _monitor;
        _monitorCancellation?.Dispose(); _monitorCancellation = null; _monitor = null;
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return; _disposed = true; NetworkChange.NetworkAddressChanged -= AddressChanged;
        _lifetime.Cancel(); _operation?.Cancel(); await _operationTask;
        if (_monitor is not null) await _monitor;
        _monitorCancellation?.Dispose();
        _lifetime.Dispose(); _wake.Dispose();
    }
}
