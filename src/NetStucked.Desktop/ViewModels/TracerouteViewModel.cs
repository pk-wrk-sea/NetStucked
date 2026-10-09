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

namespace NetStucked.Desktop.ViewModels;

public partial class TracerouteViewModel : ObservableObject, IAsyncDisposable
{
    private readonly TracerouteMonitoringService _service;
    private readonly IDesktopDialogs _dialogs;
    private readonly ILogger<TracerouteViewModel> _logger;
    private readonly DispatcherTimer _timer;
    private TraceSettings _settings;
    private bool _busy;
    private bool _resultsRefreshPending;
    private string _sessionTarget = "";
    private long _session, _revision, _lastViewRefresh;
    private bool _forceViewRefresh;
    private readonly Dictionary<int, TraceHopSnapshot> _pending = new();
    private readonly Dictionary<int, HopRow> _rowLookup = new();
    private readonly Queue<TraceEvent> _pendingEvents = new();
    public UserSettingsStore Store { get; }
    public TracerouteMonitoringService Engine => _service;
    public ObservableCollection<TraceAddressEntry> RecentAddresses { get; internal set; } = [];
    public Func<string, Task>? RecordAddressAsync { get; internal set; }
    public int SessionNumber { get; internal set; }
    public string Title => string.IsNullOrWhiteSpace(Target) ? $"Session {SessionNumber}" : Target.Trim();
    public bool IsActive => State is SessionState.Starting or SessionState.Running;
    public long ViewRefreshCount { get; private set; }
    public ObservableCollection<HopRow> Rows { get; } = [];
    public ObservableCollection<TraceEvent> EventRows { get; } = [];
    public ICollectionView Results { get; }
    public ICollectionView Events { get; }
    public string[] StatusFilters { get; } = ["All status", "Reply", "Timeout", "Unknown"];
    public string[] EventFilters { get; } = ["All events", "Info", "DNS", "Error", "Route"];
    [ObservableProperty] private string _target = "";
    [ObservableProperty] private string _statusFilter = "All status";
    [ObservableProperty] private string _eventFilter = "All events";
    [ObservableProperty] private SessionState _state;
    [ObservableProperty] private string _message = "";
    public string PauseLabel => State == SessionState.Paused ? "Ⅱ Resume" : "Ⅱ Pause";
    public bool CanEdit => !_busy && State is SessionState.Idle or SessionState.Stopped or SessionState.Error;

    public TracerouteViewModel(TracerouteMonitoringService service, IDesktopDialogs dialogs, UserSettingsStore store, ILogger<TracerouteViewModel> logger)
    {
        _service = service; _dialogs = dialogs; Store = store; _logger = logger;
        _settings = Normalize(store.Preferences.Trace); Target = store.Preferences.TraceTarget;
        Results = CollectionViewSource.GetDefaultView(Rows);
        Events = CollectionViewSource.GetDefaultView(EventRows);
        Results.Filter = item => item is HopRow row && (StatusFilter == "All status" || row.Data.Status == StatusFilter);
        Events.Filter = item => item is TraceEvent entry && (EventFilter == "All events" || entry.Type == EventFilter);
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Background, (_, _) => Refresh(), Dispatcher.CurrentDispatcher);
        _timer.Start();
    }
    partial void OnTargetChanged(string value) { StartCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(Title)); }
    partial void OnStatusFilterChanged(string value) { _resultsRefreshPending = _forceViewRefresh = true; RefreshResults(); }
    partial void OnEventFilterChanged(string value) => Events.Refresh();
    partial void OnStateChanged(SessionState value) { OnPropertyChanged(nameof(PauseLabel)); OnPropertyChanged(nameof(IsActive)); NotifyCommands(); }
    public void UpdateState() => State = _service.State;
    private static TraceSettings Normalize(TraceSettings settings) => new() { MaxHops = settings.MaxHops, IntervalMs = settings.IntervalMs, TimeoutMs = settings.TimeoutMs, PacketSize = settings.PacketSize, Continuous = true };
    private bool CanStart() => CanEdit && TargetInputParser.IsValidHost(Target.Trim());
    private bool CanPause() => !_busy && State is SessionState.Running or SessionState.Paused;
    private bool CanStop() => !_busy && State is SessionState.Starting or SessionState.Running or SessionState.Pausing or SessionState.Paused;
    private void NotifyCommands() { OnPropertyChanged(nameof(CanEdit)); StartCommand.NotifyCanExecuteChanged(); PauseCommand.NotifyCanExecuteChanged(); StopCommand.NotifyCanExecuteChanged(); SettingsCommand.NotifyCanExecuteChanged(); }
    private async Task ExecuteAsync(Func<Task> action)
    {
        _busy = true; NotifyCommands();
        try { await action(); }
        catch (Exception ex) { Message = ex.Message; _logger.LogError(ex, "Traceroute operation failed"); _dialogs.ShowError(ex.Message); }
        finally { _busy = false; Refresh(); NotifyCommands(); }
    }
    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task StartAsync() => ExecuteAsync(async () =>
    {
        _sessionTarget = Target.Trim();
        await _service.StartAsync(_sessionTarget, _settings);
        Store.Preferences.TraceTarget = Target;
        if (RecordAddressAsync is not null) await RecordAddressAsync(_sessionTarget);
    });
    [RelayCommand(CanExecute = nameof(CanPause))]
    private Task PauseAsync() => ExecuteAsync(() => State == SessionState.Paused ? _service.ResumeAsync() : _service.PauseAsync());
    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task StopAsync() => ExecuteAsync(async () => { await _service.StopAsync(); LogDiagnostics(); });
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Settings() { var edited = _dialogs.EditTraceSettings(_settings); if (edited is not null) { _settings = Normalize(edited); Store.Preferences.Trace = _settings; } }
    [RelayCommand]
    private Task ExportAsync() => ExecuteAsync(() => { Refresh(true); return _dialogs.ExportAsync("NetStucked-traceroute.csv",
        CsvExporter.Trace(Results.Cast<HopRow>().Select(r => r.Data with { Description = r.Description }))); });
    public void SetPresentationActive(bool active) { if (active) { UpdateState(); _timer.Start(); } else _timer.Stop(); }
    public void Refresh(bool flush = false)
    {
        State = _service.State;
        var update = _service.ReadUpdates(_session, _revision);
        if (update.Reset) { _pending.Clear(); _pendingEvents.Clear(); _rowLookup.Clear(); Rows.Clear(); EventRows.Clear(); }
        _session = update.SessionId; _revision = update.Revision;
        var active = update.ActiveHops.ToHashSet();
        foreach (int key in _rowLookup.Keys.Where(k => !active.Contains(k)).ToArray()) { Rows.Remove(_rowLookup[key]); _rowLookup.Remove(key); }
        foreach (int key in _pending.Keys.Where(k => !active.Contains(k)).ToArray()) _pending.Remove(key);
        foreach (var data in update.Rows) _pending[data.Hop] = data;
        var budget = Stopwatch.StartNew();
        foreach (int key in _pending.Keys.Order().ToArray())
        {
            var data = _pending[key]; _pending.Remove(key);
            if (!_rowLookup.TryGetValue(key, out var row))
            {
                row = new(data, (hop, description) =>
                {
                    _service.SetDescription(hop, description);
                    Store.Preferences.TraceDescriptions[$"{_sessionTarget}:{hop}"] = description;
                });
                if (Store.Preferences.TraceDescriptions.TryGetValue($"{_sessionTarget}:{data.Hop}", out var description)) row.Description = description;
                _rowLookup[key] = row;
                int index = 0; while (index < Rows.Count && Rows[index].Data.Hop < data.Hop) index++;
                Rows.Insert(index, row); _resultsRefreshPending = true;
            }
            else
            {
                if (row.Data.Status != data.Status && StatusFilter != "All status" || HopRow.SortChanged(Results, row.Data, data)) _resultsRefreshPending = true;
                row.Data = data;
            }
            _service.ReportUiApplied(data);
            if (!flush && budget.Elapsed.TotalMilliseconds >= 4) break;
        }
        if (flush) _forceViewRefresh = true;
        RefreshResults();
        foreach (var entry in update.Events) _pendingEvents.Enqueue(entry);
        while (_pendingEvents.Count > 1000) _pendingEvents.Dequeue();
        while (_pendingEvents.Count > 0 && (flush || budget.Elapsed.TotalMilliseconds < 4)) EventRows.Add(_pendingEvents.Dequeue());
        while (EventRows.Count > 1000) EventRows.RemoveAt(0);
        Message = State == SessionState.Error ? _service.LastError ?? "Error" : $"{State} • {_service.Outcome}";
    }
    private void RefreshResults()
    {
        // WPF rejects Refresh during an edit transaction. Continue updating row data,
        // then apply sorting/filtering on the next timer tick after the user commits/cancels.
        if (!_resultsRefreshPending) return;
        if (Results is IEditableCollectionView { IsEditingItem: true } or IEditableCollectionView { IsAddingNew: true }) return;
        if (!_forceViewRefresh && _lastViewRefresh != 0 && Stopwatch.GetElapsedTime(_lastViewRefresh).TotalMilliseconds < 500) return;
        Results.Refresh(); ViewRefreshCount++;
        _resultsRefreshPending = _forceViewRefresh = false; _lastViewRefresh = Stopwatch.GetTimestamp();
    }
    private void LogDiagnostics() => _logger.LogInformation("Traceroute timing {Diagnostics}", System.Text.Json.JsonSerializer.Serialize(_service.Diagnostics));
    public async ValueTask DisposeAsync() { _timer.Stop(); await _service.DisposeAsync(); LogDiagnostics(); }
}
