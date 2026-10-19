using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetStucked.Core;
using NetStucked.Desktop.Services;

namespace NetStucked.Desktop.ViewModels;

public partial class ConfigCollectorViewModel : ObservableObject, IAsyncDisposable
{
    private readonly CollectorController _controller;
    private readonly IDesktopDialogs _dialogs;
    private readonly ConcurrentDictionary<string, CollectorResult> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _presenter;
    private CancellationTokenSource? _cancel;
    private Task? _running;
    private Guid? _runId;
    private bool _disposed;
    public ObservableCollection<CollectorRowViewModel> Results { get; } = [];
    public IReadOnlyList<string> Profiles => CollectorCommands.Profiles;
    public IReadOnlyList<CollectorProtocol> Protocols { get; } = Enum.GetValues<CollectorProtocol>();
    [ObservableProperty] private string _targetText = "";
    [ObservableProperty] private CollectorProtocol _protocol;
    [ObservableProperty] private string _profile = "Cisco IOS / IOS XE";
    [ObservableProperty] private string _basicCommands = "terminal length 0";
    [ObservableProperty] private string _collectionCommands = "show running-config\nshow version";
    [ObservableProperty] private string _hostPins = "";
    [ObservableProperty] private string _outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetStucked", "Captures");
    [ObservableProperty] private int _concurrency = 2;
    [ObservableProperty] private int _connectTimeout = 15;
    [ObservableProperty] private int _commandTimeout = 30;
    [ObservableProperty] private bool _writeTxt = true;
    [ObservableProperty] private bool _writeJson = true;
    [ObservableProperty] private bool _allowTelnet;
    [ObservableProperty] private string _username1 = "";
    [ObservableProperty] private string _username2 = "";
    [ObservableProperty] private bool _useAuth2;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _message = "Paste authorized devices, enter AUTH1 and verified SSH host pins, then Start.";
    [ObservableProperty] private CollectorBatchState _state = CollectorBatchState.Idle;
    [ObservableProperty] private CollectorRowViewModel? _selectedRow;
    public bool CanEdit => !IsRunning;
    public bool IsTelnet => Protocol == CollectorProtocol.Telnet;
    public string Preview => SelectedRow?.Result.Preview ?? "Select a device to view its bounded capture preview.";
    public string SelectedFiles => SelectedRow is null ? "" : string.Join('\n', new[] { SelectedRow.Result.TxtPath, SelectedRow.Result.JsonPath }.Where(s => s.Length > 0));
    partial void OnProtocolChanged(CollectorProtocol value) => OnPropertyChanged(nameof(IsTelnet));
    partial void OnProfileChanged(string value) { BasicCommands = CollectorCommands.Basic(value); CollectionCommands = CollectorCommands.Collection(value); }
    partial void OnSelectedRowChanged(CollectorRowViewModel? value) { OnPropertyChanged(nameof(Preview)); OnPropertyChanged(nameof(SelectedFiles)); }
    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(CanEdit));
    public ConfigCollectorViewModel(CollectorController controller, IDesktopDialogs dialogs)
    {
        _controller = controller; _dialogs = dialogs;
        _presenter = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) => Drain(), Dispatcher.CurrentDispatcher);
        _presenter.Start();
    }
    public async Task StartAsync(string password1, string enable1, string password2, string enable2)
    {
        if (IsRunning || _disposed) return;
        State = CollectorBatchState.Validating;
        CollectorRun run;
        try
        {
            // No mutable UI references cross into the controller, including passwords/profile/commands.
            run = new(new(Protocol, Profile, OutputFolder, Concurrency, ConnectTimeout, CommandTimeout, WriteTxt, WriteJson, AllowTelnet),
                CollectorInputParser.Parse(TargetText, IsTelnet ? 23 : 22), CollectorCommands.Lines(BasicCommands), CollectorCommands.Lines(CollectionCommands),
                CollectorInputParser.ParsePins(HostPins), new(Username1, password1, enable1), UseAuth2 ? new(Username2, password2, enable2) : null);
        }
        catch (ArgumentException ex) { State = CollectorBatchState.Idle; Message = ex.Message; return; }
        _runId = run.Id; _pending.Clear(); Results.Clear();
        foreach (var target in run.Targets) Results.Add(new(new(run.Id, target, CollectorDeviceState.Queued, "Queued")));
        SelectedRow = Results[0]; IsRunning = true; State = CollectorBatchState.Running;
        Message = $"Run {run.Id:N} · {run.Targets.Length} devices · options frozen";
        _cancel = new CancellationTokenSource(); var token = _cancel.Token;
        _running = RunAsync(run, token);
        try { await _running; }
        finally { _running = null; _cancel.Dispose(); _cancel = null; IsRunning = false; }
    }
    private async Task RunAsync(CollectorRun run, CancellationToken token)
    {
        try
        {
            var results = await Task.Run(() => _controller.RunAsync(run, result =>
            {
                // Coalesced per endpoint: at most 256 bounded preview messages; no unbounded output queue.
                _pending[result.Target.Endpoint] = result;
            }, token), CancellationToken.None);
            Drain(true);
            int completed = results.Count(r => r.State == CollectorDeviceState.Completed), partial = results.Count(r => r.State == CollectorDeviceState.Partial), cancelled = results.Count(r => r.State == CollectorDeviceState.Cancelled);
            State = completed == results.Count ? CollectorBatchState.Completed : completed + partial > 0 || results.Any(r => r.Bytes > 0) ? CollectorBatchState.Partial : CollectorBatchState.Failed;
            Message = $"{State} · completed {completed} · partial {partial} · failed {results.Count - completed - partial - cancelled} · cancelled {cancelled}";
        }
        catch (Exception) { State = CollectorBatchState.Failed; Message = "Collector failed; credentials and device exception contents were not logged."; }
    }
    private void Drain(bool all = false)
    {
        int budget = all ? 256 : 16;
        foreach (string endpoint in _pending.Keys.Take(budget))
            if (_pending.TryRemove(endpoint, out var result) && result.RunId == _runId)
            {
                var row = Results.FirstOrDefault(r => r.Endpoint == endpoint);
                row?.Update(result);
                if (row == SelectedRow) { OnPropertyChanged(nameof(Preview)); OnPropertyChanged(nameof(SelectedFiles)); }
            }
    }
    [RelayCommand] private async Task StopAsync()
    {
        if (_cancel is null) return;
        State = CollectorBatchState.Cancelling; Message = "Cancelling; waiting for connections and capture cleanup…";
        await _cancel.CancelAsync(); if (_running is { } running) await running;
    }
    public Task StopForUpdateAsync() => StopAsync();
    [RelayCommand] private async Task LoadTargetsAsync()
    {
        if (IsRunning) return;
        try { if (await _dialogs.LoadTargetsAsync() is { } text) { if (text.Length > 65536) throw new ArgumentException("Target file exceeds 64 KiB."); TargetText = text; } }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { Message = "Cannot load target list; check file access/size."; }
    }
    [RelayCommand] private async Task ExportErrorsAsync()
    {
        static string Csv(object? value) => "\"" + (value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
        var rows = Results.Select(r => r.Result).Where(r => r.State is CollectorDeviceState.Failed or CollectorDeviceState.Partial or CollectorDeviceState.Cancelled);
        string csv = "Run ID,Host,Port,State,Detail\n" + string.Join('\n', rows.Select(r => string.Join(',', new object?[] { r.RunId, r.Target.Host, r.Target.Port, r.State, r.Detail }.Select(Csv))));
        try { await _dialogs.ExportAsync("collector-errors.csv", csv); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Message = "Error CSV could not be saved."; }
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return; _disposed = true;
        await StopAsync(); _presenter.Stop(); _pending.Clear();
    }
}
public partial class CollectorRowViewModel(CollectorResult result) : ObservableObject
{
    public CollectorResult Result { get; private set; } = result;
    public string Endpoint => Result.Target.Endpoint;
    public string Description => Result.Target.Description;
    public string State => Result.State.ToString();
    public string Auth => Result.AuthSlot is { } slot ? "AUTH" + slot : "—";
    public string Detail => Result.Detail;
    public long Bytes => Result.Bytes;
    public void Update(CollectorResult result) { Result = result; OnPropertyChanged(nameof(State)); OnPropertyChanged(nameof(Auth)); OnPropertyChanged(nameof(Detail)); OnPropertyChanged(nameof(Bytes)); }
}
