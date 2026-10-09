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
    public UserSettingsStore Store { get; }
    public ObservableCollection<PortRow> Rows { get; } = [];
    public StableObservableCollection<PortSample> History { get; } = [];
    public ObservableCollection<PortTemplateEntry> Templates { get; } = [];
    [ObservableProperty] private PortTemplateEntry? _selectedTemplate;
    public bool IsActive => State is SessionState.Starting or SessionState.Running;
    public long ViewRefreshCount { get; private set; }
    public ICollectionView Results { get; }
    public string[] StatusFilters { get; } = ["All status", "Connected", "Refused", "Timeout", "DNS error", "Unreachable", "Error", "Unknown"];
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
        _settings = store.Preferences.Port;
        foreach (var template in store.Preferences.PortTemplates) Templates.Add(CreateEntry(template));
        TargetText = store.Preferences.PortTargetText;
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
    partial void OnTargetTextChanged(string value) { if (_settings is not null) ValidateInput(); }
    partial void OnStatusFilterChanged(string value) { _viewDirty = true; RefreshViewKeepingSelection(true); }
    partial void OnSelectedRowChanged(PortRow? value) { if (value is not null) _rememberedHost = value.Data.Key; OnPropertyChanged(nameof(HistoryTitle)); UpdateHistory(); }
    partial void OnHistoryLimitChanged(int value) => UpdateHistory();
    partial void OnStateChanged(SessionState value) { OnPropertyChanged(nameof(PauseLabel)); OnPropertyChanged(nameof(IsActive)); NotifyCommands(); }
    partial void OnSelectedTemplateChanged(PortTemplateEntry? value) { if (value is not null && CanEdit) TargetText = value.Template.Addresses; }
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
        var parsed = PortInputParser.Parse(TargetText, 1024);
        TargetPreview = $"{parsed.Targets.Count:N0} unique TCP endpoints • authorized targets only";
        Message = string.Join(Environment.NewLine, parsed.Errors.Take(8));
        if (parsed.Errors.Count > 8) Message += $"\n…and {parsed.Errors.Count - 8} more errors.";
        StartCommand.NotifyCanExecuteChanged();
        return parsed;
    }

    private bool CanStart() => CanEdit && PortInputParser.Parse(TargetText, 1024).IsValid;
    private bool CanPause() => !_busy && State is SessionState.Running or SessionState.Paused;
    private bool CanStop() => !_busy && State is SessionState.Starting or SessionState.Running or SessionState.Pausing or SessionState.Paused;
    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(CanEdit)); StartCommand.NotifyCanExecuteChanged(); PauseCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged(); SettingsCommand.NotifyCanExecuteChanged();
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
        if (!parsed.IsValid) return;
        await _service.StartAsync(parsed.Targets, _settings);
        Store.Preferences.PortTargetText = TargetText;
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
        _settings = edited; Store.Preferences.Port = _settings; ValidateInput();
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
        await _service.StopAsync(); Store.Preferences.PortTargetText = TargetText; UpdateState();
    }
    public async ValueTask DisposeAsync() { _timer.Stop(); await _service.StopAsync(); LogDiagnostics(); Store.Preferences.PortTargetText = TargetText; }
}

public sealed record PortTemplateEntry(PingTemplate Template, IAsyncRelayCommand DeleteCommand)
{
    public string Name => Template.Name;
}
