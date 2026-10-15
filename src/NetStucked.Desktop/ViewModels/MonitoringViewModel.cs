using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetStucked.Core;
using NetStucked.Infrastructure;
using NetStucked.Desktop.Services;

namespace NetStucked.Desktop.ViewModels;
public partial class MonitorRow(MonitorDevice device) : ObservableObject
{
    [ObservableProperty] private MonitorDevice _device = device;
    [ObservableProperty] private MonitorSnapshot? _snapshot;
    public string Status => Snapshot?.Health == MonitorHealth.Unreachable ? "Down / Unreachable" : Snapshot?.Health.ToString() ?? (Device.Started ? "Unknown" : "Stopped");
    partial void OnSnapshotChanged(MonitorSnapshot? value) => OnPropertyChanged(nameof(Status));
    partial void OnDeviceChanged(MonitorDevice value) => OnPropertyChanged(nameof(Status));
}
public partial class InterfaceSelection(SnmpInterface data, bool watched) : ObservableObject
{
    public SnmpInterface Data { get; } = data;
    [ObservableProperty] private bool _watched = watched;
}
public partial class MonitoringViewModel : ObservableObject, IAsyncDisposable
{
    public MonitoringService Engine { get; }
    public UserSettingsStore Store { get; }
    private readonly ISnmpClient _snmp;
    private readonly ISecretVault _vault;
    private readonly IDesktopDialogs _dialogs;
    private readonly DispatcherTimer _refresh;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private Guid _editingId = Guid.NewGuid();
    private CancellationTokenSource? _testCancel;
    private Task? _test;
    private bool _disposed;
    private bool _selectionInvalidated;
    private readonly Dictionary<string, DateTimeOffset> _alertTimes = [];
    public ObservableCollection<MonitorRow> Devices { get; } = [];
    public ObservableCollection<InterfaceSelection> DiscoveredInterfaces { get; } = [];
    public ObservableCollection<InterfaceHealth> InterfaceResults { get; } = [];
    public ObservableCollection<MonitorIncident> Incidents { get; } = [];
    public SnmpVersion[] SnmpVersions { get; } = Enum.GetValues<SnmpVersion>();
    public SnmpAuth[] AuthAlgorithms { get; } = Enum.GetValues<SnmpAuth>();
    public SnmpPrivacy[] PrivacyAlgorithms { get; } = Enum.GetValues<SnmpPrivacy>();
    [ObservableProperty] private MonitorRow? _selectedDevice;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _host = "";
    [ObservableProperty] private string _group = "";
    [ObservableProperty] private bool _useIcmp = true;
    [ObservableProperty] private bool _useTcp;
    [ObservableProperty] private bool _useSnmp;
    [ObservableProperty] private string _tcpPort = "443";
    [ObservableProperty] private string _icmpInterval = "10";
    [ObservableProperty] private string _snmpInterval = "30";
    [ObservableProperty] private string _timeout = "2000";
    [ObservableProperty] private string _failureThreshold = "3";
    [ObservableProperty] private string _recoveryThreshold = "2";
    [ObservableProperty] private string _warningRtt = "200";
    [ObservableProperty] private string _warningLoss = "20";
    [ObservableProperty] private string _warningUtilization = "80";
    [ObservableProperty] private SnmpVersion _snmpVersion = SnmpVersion.V2c;
    [ObservableProperty] private SnmpAuth _authentication = SnmpAuth.SHA256;
    [ObservableProperty] private SnmpPrivacy _privacy = SnmpPrivacy.AES128;
    [ObservableProperty] private string _snmpPort = "161";
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private bool _systemInfo = true;
    [ObservableProperty] private bool _interfaces = true;
    [ObservableProperty] private bool _bandwidth = true;
    [ObservableProperty] private bool _errors = true;
    [ObservableProperty] private string _customOids = "";
    [ObservableProperty] private bool _notifications;
    [ObservableProperty] private bool _sound;
    [ObservableProperty] private bool _isTesting;
    [ObservableProperty] private string _message = "Add devices, Test SNMP if selected, then Start. Only explicitly started devices auto-resume.";
    [ObservableProperty] private string _snmpTestDetails = "No test performed.";
    [ObservableProperty] private DateTimeOffset _clock = DateTimeOffset.UtcNow;
    public bool CanEdit => !IsTesting && SelectedDevice?.Device.Started != true;
    public bool IsV3 => SnmpVersion == SnmpVersion.V3;
    public string Activity => $"{Engine.ActiveCount} active / {Devices.Count} configured · local collection while the app runs";
    public string SelectedDetails => SelectedDevice?.Snapshot is { } snapshot ? $"{snapshot.Evidence}\nSNMP: {snapshot.Snmp?.Status ?? "Not sampled"}, {snapshot.Snmp?.Time.ToString("u") ?? "—"}\n{snapshot.Snmp?.Name}\n{snapshot.Snmp?.Description}\n{snapshot.Snmp?.Details}\n" + string.Join('\n', snapshot.Snmp?.CustomValues.Select(v => v.Key + ": " + v.Value) ?? []) : "No measured observation yet.";
    public event Action<MonitorIncident>? LocalNotification;
    public MonitoringViewModel(MonitoringService engine, ISnmpClient snmp, ISecretVault vault, UserSettingsStore store, IDesktopDialogs dialogs)
    {
        Engine = engine; _snmp = snmp; _vault = vault; Store = store; _dialogs = dialogs; _notifications = engine.Notifications; _sound = engine.Sound;
        Engine.IncidentChanged += OnIncident;
        _refresh = new(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Refresh(), _dispatcher); _refresh.Start(); Refresh();
    }
    private void InvalidateSelection() { _selectionInvalidated = true; DiscoveredInterfaces.Clear(); SnmpTestDetails = "Configuration changed; Test SNMP again."; }
    partial void OnHostChanged(string value) => InvalidateSelection();
    partial void OnSnmpPortChanged(string value) => InvalidateSelection();
    partial void OnUsernameChanged(string value) => InvalidateSelection();
    partial void OnSnmpVersionChanged(SnmpVersion value) { OnPropertyChanged(nameof(IsV3)); InvalidateSelection(); }
    partial void OnIsTestingChanged(bool value) => OnPropertyChanged(nameof(CanEdit));
    partial void OnNotificationsChanged(bool value) { Engine.Notifications = value; _ = SavePreferencesAsync(); }
    partial void OnSoundChanged(bool value) { Engine.Sound = value; _ = SavePreferencesAsync(); }
    private async Task SavePreferencesAsync() { try { await Engine.SaveAsync(CancellationToken.None); } catch { Message = "Unable to save monitoring notification preferences."; } }
    private void OnIncident(MonitorIncident incident)
    {
        if (!_disposed) _dispatcher.BeginInvoke(() =>
        {
            if (_disposed) return; Refresh();
            string key = incident.DeviceId + "/" + incident.Kind + "/" + (incident.EndedAt is null ? "start" : "recovery");
            if (!Notifications || _alertTimes.TryGetValue(key, out var last) && DateTimeOffset.UtcNow - last < TimeSpan.FromMinutes(5)) return;
            _alertTimes[key] = DateTimeOffset.UtcNow;
            try { LocalNotification?.Invoke(incident); } catch { Message = "Local notification could not be displayed; the incident remains recorded."; }
        });
    }
    public async Task ResumeSavedAsync() { try { await Engine.ResumeSavedAsync(); Refresh(); } catch (Exception ex) { Message = "Unable to resume saved monitors: " + ex.GetType().Name; } }
    partial void OnSelectedDeviceChanged(MonitorRow? value)
    {
        if (value is null) return;
        var d = value.Device; _editingId = d.Id; Name = d.Name; Host = d.Host; Group = d.Group; UseIcmp = d.Icmp; UseTcp = d.Tcp; UseSnmp = d.Snmp;
        TcpPort = d.TcpPort.ToString(); IcmpInterval = d.IcmpIntervalSeconds.ToString(); SnmpInterval = d.SnmpIntervalSeconds.ToString(); Timeout = d.TimeoutMs.ToString(); FailureThreshold = d.FailureThreshold.ToString(); RecoveryThreshold = d.RecoveryThreshold.ToString(); WarningRtt = d.WarningRttMs.ToString(); WarningLoss = d.WarningLossPercent.ToString(); WarningUtilization = d.WarningUtilizationPercent.ToString();
        var s = d.SnmpSettings; SnmpVersion = s.Version; SnmpPort = s.Port.ToString(); Username = s.Username; Authentication = s.Authentication; Privacy = s.Privacy; SystemInfo = s.SystemInfo; Interfaces = s.Interfaces; Bandwidth = s.Bandwidth; Errors = s.Errors; CustomOids = string.Join('\n', s.CustomOids); DiscoveredInterfaces.Clear();
        _selectionInvalidated = false; OnPropertyChanged(nameof(CanEdit)); RefreshDetails();
    }
    private static int Number(string text) => int.TryParse(text, out int value) ? value : throw new ArgumentException("Enter valid numeric intervals, ports and thresholds.");
    private MonitorDevice Configuration()
    {
        var saved = SelectedDevice?.Device.Id == _editingId ? SelectedDevice.Device : null;
        var selection = DiscoveredInterfaces.Count > 0 ? DiscoveredInterfaces.Where(i => i.Watched).Select(i => i.Data.Index).ToArray() : !_selectionInvalidated ? saved?.SnmpSettings.InterfaceIndexes ?? [] : [];
        var device = new MonitorDevice { Id = _editingId, Name = Name.Trim(), Host = Host.Trim(), Group = Group.Trim(), Maintenance = saved?.Maintenance ?? false, Icmp = UseIcmp, Tcp = UseTcp, TcpPort = Number(TcpPort), Snmp = UseSnmp, IcmpIntervalSeconds = Number(IcmpInterval), SnmpIntervalSeconds = Number(SnmpInterval), TimeoutMs = Number(Timeout), FailureThreshold = Number(FailureThreshold), RecoveryThreshold = Number(RecoveryThreshold), WarningRttMs = Number(WarningRtt), WarningLossPercent = Number(WarningLoss), WarningUtilizationPercent = Number(WarningUtilization), SnmpSettings = new() { Version = SnmpVersion, Port = Number(SnmpPort), Username = Username.Trim(), Authentication = Authentication, Privacy = Privacy, SystemInfo = SystemInfo, Interfaces = Interfaces, Bandwidth = Bandwidth, Errors = Errors, InterfaceIndexes = selection, CustomOids = CustomOids.Split(['\r','\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) } };
        device.Validate(); return device;
    }
    [RelayCommand] private void NewDevice() { if (IsTesting) return; SelectedDevice = null; _editingId = Guid.NewGuid(); Name = Host = Group = Username = CustomOids = ""; UseIcmp = true; UseTcp = UseSnmp = false; DiscoveredInterfaces.Clear(); OnPropertyChanged(nameof(CanEdit)); Message = "New device · configure checks then Save / Start."; }
    [RelayCommand] private async Task Save() { try { await Engine.SaveDeviceAsync(Configuration()); Refresh(); SelectedDevice = Devices.Single(d => d.Device.Id == _editingId); Message = "Device saved. Start explicitly to enable polling and future auto-resume."; } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.IO.IOException) { Message = ex.Message; } }
    [RelayCommand] private async Task Start() { if (IsTesting) return; try { if (SelectedDevice?.Device.Started != true) { await Engine.SaveDeviceAsync(Configuration()); Refresh(); SelectedDevice = Devices.Single(d => d.Device.Id == _editingId); } await Engine.StartAsync(_editingId); Refresh(); OnPropertyChanged(nameof(CanEdit)); Message = "Monitoring started; auto-resume intent saved."; } catch (Exception ex) { Message = "Unable to start monitor: " + (ex is ArgumentException ? ex.Message : ex.GetType().Name); } }
    [RelayCommand] private async Task Stop() { if (SelectedDevice is null) return; await Engine.StopAsync(SelectedDevice.Device.Id); Refresh(); OnPropertyChanged(nameof(CanEdit)); Message = "Stopped. This device will not auto-resume."; }
    [RelayCommand] private async Task StartAll() { foreach (var d in Engine.Devices) await Engine.StartAsync(d.Id); Refresh(); OnPropertyChanged(nameof(CanEdit)); }
    [RelayCommand] private async Task StopAll() { await Engine.StopAllAsync(false); Refresh(); OnPropertyChanged(nameof(CanEdit)); }
    [RelayCommand] private async Task Delete() { if (SelectedDevice is null || !_dialogs.Confirm("Remove monitor", "Stop and remove the selected device?")) return; Guid id = SelectedDevice.Device.Id; await Engine.RemoveAsync(id); _vault.Delete("snmp-" + id.ToString("N")); SelectedDevice = null; Refresh(); NewDevice(); }
    [RelayCommand] private async Task Maintenance() { if (SelectedDevice is null) return; await Engine.SetMaintenanceAsync(SelectedDevice.Device.Id, !SelectedDevice.Device.Maintenance); Refresh(); }
    public void SaveCredentials(string community, string auth, string privacy)
    {
        var d = Configuration(); var secret = new SnmpSecrets(community, auth, privacy); if (d.SnmpSettings.Version == Core.SnmpVersion.V3) _ = SharpSnmpClient.Privacy(d.SnmpSettings, secret); else if (string.IsNullOrEmpty(community)) throw new ArgumentException("Enter the SNMP community.");
        _vault.Write("snmp-" + d.Id.ToString("N"), JsonSerializer.Serialize(secret)); Message = "SNMP credentials saved in Windows Credential Manager.";
    }
    public Task TestSnmpAsync(string community, string auth, string privacy)
    {
        if (IsTesting || !CanEdit) return Task.CompletedTask;
        _test = TestCoreAsync(community, auth, privacy); return _test;
    }
    private async Task TestCoreAsync(string community, string auth, string privacy)
    {
        try
        {
            var d = Configuration(); d.SnmpSettings.Validate(); var secret = new SnmpSecrets(community, auth, privacy);
            if (community.Length + auth.Length + privacy.Length == 0) { string? stored = _vault.Read("snmp-" + d.Id.ToString("N")); if (stored is not null) secret = JsonSerializer.Deserialize<SnmpSecrets>(stored)!; }
            IsTesting = true; _testCancel = new(); Message = "Testing read-only SNMP and discovering interfaces…";
            var result = await _snmp.ReadAsync(d.Host, d.SnmpSettings, secret, d.TimeoutMs, true, _testCancel.Token);
            if (_editingId != d.Id || Host.Trim() != d.Host) { Message = "Selection changed; the previous device's test result was discarded."; return; }
            SnmpTestDetails = $"{result.Status}\n{result.Name}\n{result.Description}\nUptime ticks: {result.UptimeTicks}\n{result.Details}\n" + string.Join('\n', result.CustomValues.Select(v => v.Key + ": " + v.Value));
            DiscoveredInterfaces.Clear(); foreach (var row in result.Interfaces) DiscoveredInterfaces.Add(new(row, d.SnmpSettings.InterfaceIndexes.Contains(row.Index)));
            Message = result.Success ? "SNMP Test succeeded. Select up to 32 interfaces, save credentials/configuration, then Start." : "SNMP Test: " + result.Status + ". " + result.Details;
        }
        catch (OperationCanceledException) { Message = "SNMP test cancelled."; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or JsonException) { Message = "SNMP test/configuration could not proceed: " + ex.GetType().Name; }
        finally { _testCancel?.Dispose(); _testCancel = null; IsTesting = false; }
    }
    [RelayCommand] private void CancelTest() => _testCancel?.Cancel();
    public void Refresh()
    {
        Clock = DateTimeOffset.UtcNow;
        var configs = Engine.Devices; foreach (var row in Devices.Where(r => !configs.Any(d => d.Id == r.Device.Id)).ToArray()) Devices.Remove(row);
        foreach (var d in configs) { var row = Devices.FirstOrDefault(r => r.Device.Id == d.Id); if (row is null) { row = new(d); Devices.Add(row); } else if (row.Device != d) row.Device = d; row.Snapshot = Engine.Snapshots.FirstOrDefault(s => s.DeviceId == d.Id); }
        var incidents = Engine.Incidents.Take(500).ToArray(); foreach (var incident in incidents) { var old = Incidents.FirstOrDefault(i => i.Id == incident.Id); if (old == incident) continue; if (old is not null) Incidents[Incidents.IndexOf(old)] = incident; else Incidents.Insert(0, incident); } while (Incidents.Count > 500) Incidents.RemoveAt(Incidents.Count - 1);
        RefreshDetails(); OnPropertyChanged(nameof(Activity)); if (Engine.SaveError is not null) Message = Engine.SaveError;
    }
    private void RefreshDetails()
    {
        var current = SelectedDevice?.Snapshot?.Interfaces ?? []; if (!InterfaceResults.SequenceEqual(current)) { InterfaceResults.Clear(); foreach (var item in current) InterfaceResults.Add(item); }
        OnPropertyChanged(nameof(SelectedDetails));
    }
    [RelayCommand] private Task Export() => _dialogs.ExportAsync("monitoring.csv", "Device,Host,State,LastCheck,RTT,Loss,Availability,Evidence\n" + string.Join('\n', Devices.Select(d => string.Join(',', new object?[] { d.Device.Name, d.Device.Host, d.Snapshot?.Health, d.Snapshot?.LastCheck, d.Snapshot?.RttMs, d.Snapshot?.LossPercent, d.Snapshot?.AvailabilityPercent, d.Snapshot?.Evidence }.Select(CsvExporter.Cell)))));
    public async Task StopForUpdateAsync() { _testCancel?.Cancel(); if (_test is { } test) await test; await Engine.StopAllAsync(); }
    public async ValueTask DisposeAsync() { if (_disposed) return; _disposed = true; _refresh.Stop(); Engine.IncidentChanged -= OnIncident; await StopForUpdateAsync(); await Engine.DisposeAsync(); }
}
