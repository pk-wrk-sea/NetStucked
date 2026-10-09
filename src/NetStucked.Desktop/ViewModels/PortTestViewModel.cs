using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NetStucked.Core;
using NetStucked.Desktop.Services;
using NetStucked.Infrastructure;
using NetStucked.Desktop.Behaviors;

namespace NetStucked.Desktop.ViewModels;

public partial class PortTestViewModel : ObservableObject, IAsyncDisposable
{
    private readonly MultiTargetPortService _service;
    private readonly IDesktopDialogs _dialogs;
    private readonly ILogger<PortTestViewModel> _logger;
    private readonly DispatcherTimer _timer;
    private PortProbeSettings _settings;
    private bool _busy;
    private PortRow? _historyRow;
    private string? _rememberedHost;
    private long _historySequence = -1;
    private int _previousHistoryLimit;
    private long _session, _revision;
    private readonly Dictionary<int, PortSnapshot> _pending = new();
    private readonly Dictionary<int, PortRow> _rowLookup = new();
    private readonly Dictionary<int, string> _dnsErrors = new();
    private bool _viewDirty;
    private long _lastViewRefresh;
    private bool _inputValid;
    private PortProtocol? _resultProtocol;
    public UserSettingsStore Store { get; }
    public ObservableCollection<PortRow> Rows { get; } = [];
    public StableObservableCollection<PortSample> History { get; } = [];
    public ObservableCollection<PortTemplateEntry> Templates { get; } = [];
    [ObservableProperty] private PortTemplateEntry? _selectedTemplate;
    public bool IsActive => State is SessionState.Starting or SessionState.Running;
    public long ViewRefreshCount { get; private set; }
    public ICollectionView Results { get; }
    public string[] StatusFilters { get; } = ["All status", "Connected", "Responded", "Refused", "Closed", "No response", "Timeout", "DNS error", "Unreachable", "Error", "Unknown"];
    public PortProtocol[] Protocols { get; } = [PortProtocol.TCP, PortProtocol.UDP];
    public ObservableCollection<string> PortTemplateNames { get; } = [];
    [ObservableProperty] private string? _selectedPortTemplate;
    [ObservableProperty] private string _portNumbers = "443";
    [ObservableProperty] private PortProtocol _protocol;
    public bool MultipleTargets => true;
    public bool Continuous => true;
    [ObservableProperty] private bool _scopeAcknowledged;
    [ObservableProperty] private bool _requiresScopeAcknowledgement;
    [ObservableProperty] private bool _addressesExpanded = true;
    [ObservableProperty] private bool _historyExpanded = true;
    [ObservableProperty] private int _noResponse;
    public string SuccessLabel => (_resultProtocol ?? Protocol) == PortProtocol.UDP ? "Responded" : "Connected";
    public string FailureLabel => (_resultProtocol ?? Protocol) == PortProtocol.UDP ? "No success %" : "Failure %";
    public string ProtocolGuide => Protocol == PortProtocol.UDP ? "UDP payload probe · silence is inconclusive." : "TCP connect + payload send · a successful send does not validate the application protocol.";
    [RelayCommand] private void ToggleAddresses() => AddressesExpanded = !AddressesExpanded;
    [RelayCommand] private void ToggleHistory() => HistoryExpanded = !HistoryExpanded;
    public int[] HistoryLimits { get; } = [100, 500];
    [ObservableProperty] private string _targetText = "";
    [ObservableProperty] private string _targetPreview = "0 unique TCP endpoints";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private string _statusFilter = "All status";
    [ObservableProperty] private PortRow? _selectedRow;
    [ObservableProperty] private int _historyLimit = 100;
    [ObservableProperty] private SessionState _state;
    [ObservableProperty] private int _targetCount;
    [ObservableProperty] private int _reachable;
    [ObservableProperty] private int _unreachable;
    [ObservableProperty] private long _sent;
    [ObservableProperty] private long _received;
    [ObservableProperty] private double _loss;
    public string PauseLabel => State == SessionState.Paused ? "Ⅱ  Resume" : "Ⅱ  Pause";
    public string HistoryTitle => SelectedRow is null ? "Connection History — select a target" : $"Connection History — {SelectedRow.Data.Endpoint}  {SelectedRow.Data.Description}";
    public bool CanEdit => !_busy && State is SessionState.Idle or SessionState.Stopped or SessionState.Error;

    public PortTestViewModel(MultiTargetPortService service, IDesktopDialogs dialogs, UserSettingsStore store, ILogger<PortTestViewModel> logger)
    {
        _service = service; _dialogs = dialogs; Store = store; _logger = logger;
        _settings = store.Preferences.Port with { Continuous = true };
        store.Preferences.Port = _settings;
        store.Preferences.PortMultipleTargets = store.Preferences.PortContinuous = true;
        _portNumbers = store.Preferences.PortNumbers; _protocol = store.Preferences.PortProtocol;
        foreach (string name in PortScanPlanner.StandardTemplates.Keys.Concat(store.Preferences.PortNumberTemplates.Keys).Distinct()) PortTemplateNames.Add(name);
        foreach (var template in store.Preferences.PortTemplates) Templates.Add(CreateEntry(template));
        TargetText = store.Preferences.PortTargetText;
        MigrateLegacyInput();
        Results = CollectionViewSource.GetDefaultView(Rows);
        Results.Filter = item => item is PortRow row && Matches(row.Data);
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Background, (_, _) => Refresh(), Dispatcher.CurrentDispatcher);
        ValidateInput();
    }

    private bool Matches(PortSnapshot row) => StatusFilter switch
    {
        "All status" => true,
        _ => row.Status == StatusFilter
    };
    private void InputChanged() { ScopeAcknowledged = false; if (_settings is not null) ValidateInput(); }
    partial void OnTargetTextChanged(string value) => InputChanged();
    partial void OnPortNumbersChanged(string value) => InputChanged();
    partial void OnProtocolChanged(PortProtocol value) { InputChanged(); OnPropertyChanged(nameof(SuccessLabel)); OnPropertyChanged(nameof(FailureLabel)); OnPropertyChanged(nameof(ProtocolGuide)); }
    partial void OnScopeAcknowledgedChanged(bool value) => StartCommand.NotifyCanExecuteChanged();
    partial void OnSelectedPortTemplateChanged(string? value)
    {
        if (value is null || !CanEdit) return;
        if (value == "Standard UDP ports") Protocol = PortProtocol.UDP;
        else if (PortScanPlanner.StandardTemplates.ContainsKey(value)) Protocol = PortProtocol.TCP;
        PortNumbers = Store.Preferences.PortNumberTemplates.GetValueOrDefault(value, PortScanPlanner.StandardTemplates.GetValueOrDefault(value, "443"));
    }
    partial void OnStatusFilterChanged(string value) { _viewDirty = true; RefreshViewKeepingSelection(true); }
    partial void OnSelectedRowChanged(PortRow? value) { if (value is not null) _rememberedHost = value.Data.Key; OnPropertyChanged(nameof(HistoryTitle)); UpdateHistory(); }
    partial void OnHistoryLimitChanged(int value) => UpdateHistory();
    partial void OnStateChanged(SessionState value) { OnPropertyChanged(nameof(PauseLabel)); OnPropertyChanged(nameof(IsActive)); NotifyCommands(); }
    partial void OnSelectedTemplateChanged(PortTemplateEntry? value) { if (value is not null && CanEdit) { TargetText = value.Template.Addresses; MigrateLegacyInput(); } }
    private void MigrateLegacyInput()
    {
        if (string.IsNullOrWhiteSpace(TargetText) || TargetInputParser.Parse(TargetText, 1024).IsValid) return;
        var old = PortInputParser.Parse(TargetText);
        if (!old.IsValid || old.Targets.Select(t => t.Port).Distinct().Count() != 1) return;
        PortNumbers = old.Targets[0].Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        TargetText = string.Join(Environment.NewLine, old.Targets.Select(t => $"{t.Host} {t.Description}".TrimEnd()));
    }
    public void UpdateState() => State = _service.State;
    private PortTemplateEntry CreateEntry(PingTemplate template) => new(template, new AsyncRelayCommand(() => DeleteTemplateAsync(template.Id)));
    private Task DeleteTemplateAsync(Guid id) => ExecuteAsync(async () =>
    {
        var entry = Templates.FirstOrDefault(t => t.Template.Id == id);
        if (entry is null || !_dialogs.ConfirmTemplateDeletion(entry.Name)) return;
        var next = Templates.Where(t => t.Template.Id != id).Select(t => t.Template).ToList();
        await PersistTemplatesAsync(next);
        if (SelectedTemplate == entry) SelectedTemplate = null;
        Templates.Remove(entry);
    });
    private async Task PersistTemplatesAsync(List<PingTemplate> next)
    {
        var previous = Store.Preferences.PortTemplates;
        Store.Preferences.PortTemplates = next;
        try { await Store.SaveAsync(); }
        catch { Store.Preferences.PortTemplates = previous; throw; }
    }
    [RelayCommand] private void Unselect() { _rememberedHost = null; SelectedRow = null; }

    private PortParseResult ValidateInput()
    {
        var parsed = PortScanPlanner.Parse(TargetText, PortNumbers, Protocol, MultipleTargets);
        _inputValid = parsed.IsValid;
        RequiresScopeAcknowledgement = parsed.Targets.Count > PortScanPlanner.DefaultChecks;
        TargetPreview = $"{parsed.Targets.Select(t => t.Host).Distinct().Count():N0} hosts · {parsed.Targets.Count:N0} {Protocol} checks • authorized targets only";
        Message = string.Join(Environment.NewLine, parsed.Errors.Take(8));
        if (parsed.Errors.Count > 8) Message += $"\n…and {parsed.Errors.Count - 8} more errors.";
        StartCommand.NotifyCanExecuteChanged();
        return parsed;
    }

    private bool CanStart() => CanEdit && _inputValid && (!RequiresScopeAcknowledgement || ScopeAcknowledged);
    private bool CanPause() => !_busy && State is SessionState.Running or SessionState.Paused;
    private bool CanStop() => !_busy && State is SessionState.Starting or SessionState.Running or SessionState.Pausing or SessionState.Paused;
    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(CanEdit)); StartCommand.NotifyCanExecuteChanged(); PauseCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged(); SettingsCommand.NotifyCanExecuteChanged();
        EditPortTemplateCommand.NotifyCanExecuteChanged(); SavePortNumbersCommand.NotifyCanExecuteChanged(); RestorePortTemplateCommand.NotifyCanExecuteChanged();
    }
    private async Task ExecuteAsync(Func<Task> action)
    {
        _busy = true; NotifyCommands();
        try { await action(); }
        catch (Exception ex) { Message = ex.Message; _logger.LogError(ex, "Port Test operation failed"); _dialogs.ShowError(ex.Message); }
        finally { _busy = false; Refresh(); NotifyCommands(); }
    }
    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task StartAsync() => ExecuteAsync(async () =>
    {
        var parsed = ValidateInput();
        if (!parsed.IsValid || RequiresScopeAcknowledgement && !ScopeAcknowledged) return;
        await _service.StartAsync(parsed.Targets, _settings with { Continuous = Continuous });
        _resultProtocol = Protocol; OnPropertyChanged(nameof(SuccessLabel)); OnPropertyChanged(nameof(FailureLabel));
        PersistInput();
        SelectedRow = null; _rememberedHost = null; _historySequence = -1;
    });
    [RelayCommand(CanExecute = nameof(CanPause))]
    private Task PauseAsync() => ExecuteAsync(() => State == SessionState.Paused ? _service.ResumeAsync() : _service.PauseAsync());
    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task StopAsync() => ExecuteAsync(async () => { await _service.StopAsync(); LogDiagnostics(); });
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Settings()
    {
        var edited = _dialogs.EditPortSettings(_settings);
        if (edited is null) return;
        _settings = edited with { Continuous = true }; Store.Preferences.Port = _settings; InputChanged();
    }
    [RelayCommand]
    private Task SaveAsync() => ExecuteAsync(async () =>
    {
        var parsed = ValidateInput();
        if (!parsed.IsValid) throw new ArgumentException("Enter a valid address list before saving a template.");
        string? name = _dialogs.AskTemplateName(SelectedTemplate?.Name ?? "");
        if (name is null) return;
        name = name.Trim();
        if (name.Length is < 1 or > 80) throw new ArgumentException("Template names must contain 1–80 characters.");
        var existing = Templates.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is null && Templates.Count >= 100) throw new ArgumentException("Remove an old template first (maximum 100).");
        var template = new PingTemplate(existing?.Template.Id ?? Guid.NewGuid(), name, TargetText);
        var next = Templates.Where(t => t != existing).Select(t => t.Template).Append(template).ToList();
        await PersistTemplatesAsync(next);
        if (existing is not null) Templates.Remove(existing);
        var entry = CreateEntry(template); Templates.Add(entry); SelectedTemplate = entry;
    });
    [RelayCommand]
    private Task ExportAsync() => ExecuteAsync(() => { Refresh(true); return _dialogs.ExportAsync("NetStucked-ports.csv", CsvExporter.Port(Results.Cast<PortRow>().Select(r => r.Data))); });
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private Task EditPortTemplateAsync() => ExecuteAsync(async () =>
    {
        string name = SelectedPortTemplate ?? "Standard TCP ports";
        string? numbers = _dialogs.EditPortTemplate(name, PortNumbers);
        if (numbers is not null) await SavePortTemplateAsync(name, numbers);
    });
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private Task SavePortNumbersAsync() => ExecuteAsync(async () =>
    {
        var parsed = PortScanPlanner.Parse("127.0.0.1", PortNumbers, Protocol, false);
        if (!parsed.IsValid) throw new ArgumentException(string.Join(Environment.NewLine, parsed.Errors));
        string? name = _dialogs.AskPortTemplateName(SelectedPortTemplate ?? "");
        if (name is not null) await SavePortTemplateAsync(name.Trim(), PortNumbers);
    });
    private async Task SavePortTemplateAsync(string name, string numbers)
    {
        if (name.Length is < 1 or > 80) throw new ArgumentException("Template names must contain 1–80 characters.");
        var previous = Store.Preferences.PortNumberTemplates;
        if (!previous.ContainsKey(name) && previous.Count >= 100) throw new ArgumentException("Maximum 100 port templates.");
        var next = new Dictionary<string, string>(previous) { [name] = numbers }; Store.Preferences.PortNumberTemplates = next;
        try { await Store.SaveAsync(); } catch { Store.Preferences.PortNumberTemplates = previous; throw; }
        if (!PortTemplateNames.Contains(name)) PortTemplateNames.Add(name);
        SelectedPortTemplate = name; PortNumbers = numbers;
    }
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private Task RestorePortTemplateAsync() => ExecuteAsync(async () =>
    {
        string name = SelectedPortTemplate ?? "Standard TCP ports";
        if (!PortScanPlanner.StandardTemplates.TryGetValue(name, out var original)) throw new ArgumentException("Select a built-in port template to restore its default ports.");
        var previous = Store.Preferences.PortNumberTemplates; var next = new Dictionary<string, string>(previous); next.Remove(name); Store.Preferences.PortNumberTemplates = next;
        try { await Store.SaveAsync(); } catch { Store.Preferences.PortNumberTemplates = previous; throw; }
        SelectedPortTemplate = name; PortNumbers = original;
    });
    private void PersistInput()
    {
        Store.Preferences.PortTargetText = TargetText; Store.Preferences.PortNumbers = PortNumbers; Store.Preferences.PortProtocol = Protocol;
        Store.Preferences.PortMultipleTargets = MultipleTargets; Store.Preferences.PortContinuous = Continuous;
    }

    public void SetPresentationActive(bool active) { if (active) { UpdateState(); _timer.Start(); } else _timer.Stop(); }
    public void Refresh(bool flush = false)
    {
        State = _service.State;
        var update = _service.ReadUpdates(_session, _revision);
        if (update.Reset)
        {
            _pending.Clear(); _rowLookup.Clear(); _dnsErrors.Clear(); Rows.Clear(); SelectedRow = null; History.Clear(); _historyRow = null; _historySequence = -1;
        }
        _session = update.SessionId; _revision = update.Revision;
        foreach (var data in update.Rows)
        {
            _pending[data.Number] = data;
            if (data.Status == "DNS error") _dnsErrors[data.Number] = $"{data.Host}: {data.Details}";
            else _dnsErrors.Remove(data.Number);
        }
        var budget = Stopwatch.StartNew();
        foreach (int key in _pending.Keys.ToArray())
        {
            var data = _pending[key]; _pending.Remove(key);
            if (!_rowLookup.TryGetValue(key, out var row)) { row = new(data); _rowLookup[key] = row; Rows.Add(row); _viewDirty = true; }
            else
            {
                if (Matches(row.Data) != Matches(data) || PortRow.SortChanged(Results, row.Data, data)) _viewDirty = true;
                row.Data = data;
            }
            _service.ReportUiApplied(data);
            if (!flush && budget.Elapsed.TotalMilliseconds >= 4) break;
        }
        TargetCount = update.Totals.Targets; Reachable = update.Totals.Connected; Unreachable = update.Totals.Failed;
        NoResponse = update.Totals.NoResponse;
        Sent = update.Totals.Attempts; Received = update.Totals.Successful; Loss = update.Totals.FailurePercent;
        RefreshViewKeepingSelection(flush);
        UpdateHistory();
        if (State == SessionState.Error && _service.LastError is not null) Message = _service.LastError;
        else if (!CanEdit)
            Message = string.Join(Environment.NewLine, _dnsErrors.Values.Take(5));
    }
    private void RefreshViewKeepingSelection(bool force = false)
    {
        if (!_viewDirty || !force && _lastViewRefresh != 0 && Stopwatch.GetElapsedTime(_lastViewRefresh).TotalMilliseconds < 500) return;
        string? host = SelectedRow?.Data.Key ?? _rememberedHost;
        Results.Refresh(); ViewRefreshCount++;
        SelectedRow = host is null ? null : Rows.FirstOrDefault(r => r.Data.Key == host && Results.Contains(r));
        _viewDirty = false; _lastViewRefresh = Stopwatch.GetTimestamp();
    }
    private void UpdateHistory()
    {
        long sequence = SelectedRow?.Data.HistoryRevision ?? 0;
        if (_historyRow == SelectedRow && _historySequence == sequence && _previousHistoryLimit == HistoryLimit) return;
        bool reset = _historyRow != SelectedRow || _previousHistoryLimit != HistoryLimit || sequence < _historySequence;
        _historyRow = SelectedRow; _historySequence = sequence; _previousHistoryLimit = HistoryLimit;
        var samples = _service.History(SelectedRow?.Data.Key, HistoryLimit);
        History.Update(() =>
        {
            if (reset) { History.Clear(); foreach (var sample in samples) History.Add(sample); return; }
            long previous = History.FirstOrDefault()?.Sequence ?? 0;
            foreach (var sample in samples.Where(s => s.Sequence > previous).Reverse()) History.Insert(0, sample);
            while (History.Count > samples.Count) History.RemoveAt(History.Count - 1);
        });
    }
    private void LogDiagnostics() => _logger.LogInformation("Port Test timing {Diagnostics}", System.Text.Json.JsonSerializer.Serialize(_service.Diagnostics));
    public async Task StopForUpdateAsync()
    {
        foreach (var command in new[] { StartCommand, PauseCommand, StopCommand }) if (command.ExecutionTask is { } task) await task;
        await _service.StopAsync(); PersistInput(); UpdateState();
    }
    public async ValueTask DisposeAsync() { _timer.Stop(); await _service.StopAsync(); LogDiagnostics(); PersistInput(); }
}

public sealed record PortTemplateEntry(PingTemplate Template, IAsyncRelayCommand DeleteCommand)
{
    public string Name => Template.Name;
}
