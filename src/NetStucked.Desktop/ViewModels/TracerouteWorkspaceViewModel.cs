using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NetStucked.Core;
using NetStucked.Desktop.Services;
using NetStucked.Infrastructure;

namespace NetStucked.Desktop.ViewModels;

public partial class TracerouteWorkspaceViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IIcmpProbe _probe;
    private readonly IDnsResolver _dns;
    private readonly IDesktopDialogs _dialogs;
    private readonly UserSettingsStore _store;
    private readonly ILoggerFactory _logs;
    private readonly HashSet<TracerouteViewModel> _closing = [];
    private readonly SemaphoreSlim _historyGate = new(1, 1);
    private bool _visible;
    private int _nextNumber;
    public ObservableCollection<TracerouteViewModel> Sessions { get; } = [];
    public ObservableCollection<TraceAddressEntry> RecentAddresses { get; } = [];
    [ObservableProperty] private TracerouteViewModel _selectedSession = null!;
    public bool IsActive => Sessions.Any(s => s.IsActive);

    public TracerouteWorkspaceViewModel(IIcmpProbe probe, IDnsResolver dns, IDesktopDialogs dialogs, UserSettingsStore store, ILoggerFactory logs)
    {
        _probe = probe; _dns = dns; _dialogs = dialogs; _store = store; _logs = logs;
        foreach (string address in store.Preferences.TraceHistory) RecentAddresses.Add(Entry(address));
        AddSession();
    }
    private TraceAddressEntry Entry(string address) => new(address, new AsyncRelayCommand(() => ChangeHistoryAsync(address, false)));
    private async Task ChangeHistoryAsync(string address, bool add)
    {
        await _historyGate.WaitAsync();
        try
        {
            var previous = _store.Preferences.TraceHistory;
            var next = previous.Where(a => !a.Equals(address, StringComparison.OrdinalIgnoreCase)).ToList();
            if (add) next.Insert(0, address);
            next = next.Take(100).ToList();
            _store.Preferences.TraceHistory = next;
            try { await _store.SaveAsync(); }
            catch { _store.Preferences.TraceHistory = previous; throw; }
            var targets = Sessions.ToDictionary(s => s, s => s.Target);
            RecentAddresses.Clear();
            foreach (string value in next) RecentAddresses.Add(Entry(value));
            // An editable ComboBox can clear its text when its selected history item is removed.
            foreach (var pair in targets) pair.Key.Target = pair.Value;
        }
        catch (Exception ex) { _dialogs.ShowError($"Destination history: {ex.Message}"); }
        finally { _historyGate.Release(); }
    }
    private bool CanAddSession() => Sessions.Count < 8;
    [RelayCommand(CanExecute = nameof(CanAddSession))]
    private void AddSession()
    {
        var session = new TracerouteViewModel(new TracerouteMonitoringService(_probe, _dns), _dialogs, _store, _logs.CreateLogger<TracerouteViewModel>())
        { SessionNumber = ++_nextNumber, RecentAddresses = RecentAddresses, RecordAddressAsync = address => ChangeHistoryAsync(address, true) };
        if (Sessions.Count > 0) session.Target = "";
        session.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TracerouteViewModel.IsActive)) OnPropertyChanged(nameof(IsActive)); };
        Sessions.Add(session); SelectedSession = session;
        AddSessionCommand.NotifyCanExecuteChanged();
    }
    [RelayCommand]
    private async Task RemoveSessionAsync(TracerouteViewModel? session)
    {
        if (session is null || !Sessions.Contains(session) || !_closing.Add(session)) return;
        try
        {
            await session.DisposeAsync();
            if (SelectedSession == session) SelectedSession = Sessions.FirstOrDefault(s => s != session)!;
            Sessions.Remove(session);
            if (Sessions.Count == 0) AddSession();
            OnPropertyChanged(nameof(IsActive)); AddSessionCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex) { _dialogs.ShowError($"Close trace session: {ex.Message}"); }
        finally { _closing.Remove(session); }
    }
    partial void OnSelectedSessionChanged(TracerouteViewModel? oldValue, TracerouteViewModel newValue)
    {
        oldValue?.SetPresentationActive(false);
        newValue?.SetPresentationActive(_visible);
    }
    public void SetPresentationActive(bool active) { _visible = active; SelectedSession?.SetPresentationActive(active); }
    public void UpdateStates() { foreach (var session in Sessions) session.UpdateState(); }
    public async ValueTask DisposeAsync() { foreach (var session in Sessions.ToArray()) await session.DisposeAsync(); }
}

public sealed record TraceAddressEntry(string Value, IAsyncRelayCommand DeleteCommand);
