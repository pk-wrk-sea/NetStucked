using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetStucked.Core;
using NetStucked.Infrastructure;
using NetStucked.Desktop.Services;

namespace NetStucked.Desktop.ViewModels;

public abstract partial class DiagnosticToolViewModel<T> : ObservableObject, IAsyncDisposable where T : class
{
    protected readonly IDesktopDialogs Dialogs;
    public UserSettingsStore Store { get; }
    public ObservableCollection<T> Results { get; } = [];
    public int[] ConcurrencyChoices { get; } = [1, 4, 8, 16];
    [ObservableProperty] private int _concurrency = 4;
    [ObservableProperty] private string _timeoutText = "5000";
    [ObservableProperty] private string _targetText = "";
    [ObservableProperty] private string _message = "Ready";
    [ObservableProperty] private bool _addressesExpanded = true;
    [ObservableProperty] private bool _detailsExpanded = true;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(StartCommand))] private bool _isRunning;
    [ObservableProperty] private T? _selectedRow;
    public bool CanEdit => !IsRunning;
    public string Details => SelectedRow switch { DnsTestResult dns => dns.Details, MacVendorResult mac => mac.Details, _ => "Select a result to inspect details." };
    private CancellationTokenSource? _cancel;
    private Task? _run;
    protected DiagnosticToolViewModel(UserSettingsStore store, IDesktopDialogs dialogs) { Store = store; Dialogs = dialogs; }
    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(CanEdit));
    partial void OnSelectedRowChanged(T? value) => OnPropertyChanged(nameof(Details));
    [RelayCommand] private void ToggleAddresses() => AddressesExpanded = !AddressesExpanded;
    [RelayCommand] private void ToggleDetails() => DetailsExpanded = !DetailsExpanded;
    protected int Timeout()
    {
        if (!int.TryParse(TimeoutText, out int timeout) || timeout is < 250 or > 60000) throw new ArgumentException("Timeout must be 250–60,000 ms.");
        if (!ConcurrencyChoices.Contains(Concurrency)) throw new ArgumentException("Choose a supported concurrency limit.");
        return timeout;
    }
    protected abstract Task<IReadOnlyList<Func<CancellationToken, Task<T>>>> PlanAsync(CancellationToken token);
    protected abstract string Csv();
    private bool CanStart() => !IsRunning;
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task Start()
    {
        if (IsRunning) return;
        IsRunning = true; _cancel = new CancellationTokenSource();
        try
        {
            Results.Clear(); SelectedRow = default;
            var jobs = await PlanAsync(_cancel.Token);
            Message = $"Running · 0 / {jobs.Count}";
            _run = Parallel.ForEachAsync(jobs, new ParallelOptions { MaxDegreeOfParallelism = Concurrency, CancellationToken = _cancel.Token }, async (job, token) =>
            {
                var row = await job(token).ConfigureAwait(false);
                await _dispatcher.InvokeAsync(() => { Results.Add(row); SelectedRow ??= row; Message = $"Running · {Results.Count} / {jobs.Count}"; });
            });
            await _run;
            Message = $"Completed · {Results.Count} results";
        }
        catch (OperationCanceledException) { Message = $"Stopped · {Results.Count} completed results"; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.IO.IOException or System.Net.Sockets.SocketException) { Message = ex.Message; Dialogs.ShowError(ex.Message); }
        finally { _run = null; _cancel.Dispose(); _cancel = null; IsRunning = false; }
    }
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    [RelayCommand] private Task Stop() => StopForUpdateAsync();
    public async Task StopForUpdateAsync()
    {
        if (_cancel is null) return;
        await _cancel.CancelAsync();
        if (_run is not null) { try { await _run; } catch (OperationCanceledException) { } }
        if (StartCommand.ExecutionTask is { } execution) await execution;
    }
    [RelayCommand] private Task Export() => Dialogs.ExportAsync(typeof(T) == typeof(DnsTestResult) ? "dns-results.csv" : "mac-vendors.csv", Csv());
    public async ValueTask DisposeAsync() => await StopForUpdateAsync();
    protected static string Row(params object?[] values) => string.Join(',', values.Select(CsvExporter.Cell));
}

public partial class DnsResolverViewModel : DiagnosticToolViewModel<DnsTestResult>
{
    private readonly IDnsTestClient _client;
    [ObservableProperty] private string _resolverText = "";
    [ObservableProperty] private bool _useSystemResolvers = true;
    [ObservableProperty] private bool _includeIpv6Dns;
    public bool CustomResolvers => !UseSystemResolvers;
    partial void OnUseSystemResolversChanged(bool value) => OnPropertyChanged(nameof(CustomResolvers));
    [ObservableProperty] private bool _queryA = true;
    [ObservableProperty] private bool _queryAaaa;
    [ObservableProperty] private bool _queryCname;
    [ObservableProperty] private bool _queryMx;
    [ObservableProperty] private bool _queryTxt;
    [ObservableProperty] private bool _queryPtr;
    [ObservableProperty] private bool _querySrv;
    [ObservableProperty] private bool _queryNs;
    [ObservableProperty] private bool _querySoa;
    public DnsResolverViewModel(IDnsTestClient client, UserSettingsStore store, IDesktopDialogs dialogs) : base(store, dialogs) => _client = client;
    protected override async Task<IReadOnlyList<Func<CancellationToken, Task<DnsTestResult>>>> PlanAsync(CancellationToken token)
    {
        int timeout = Timeout(); var names = DiagnosticInput.Lines(TargetText, 128);
        var types = new[] { (QueryA, DnsRecordType.A), (QueryAaaa, DnsRecordType.AAAA), (QueryCname, DnsRecordType.CNAME), (QueryMx, DnsRecordType.MX), (QueryTxt, DnsRecordType.TXT), (QueryPtr, DnsRecordType.PTR), (QuerySrv, DnsRecordType.SRV), (QueryNs, DnsRecordType.NS), (QuerySoa, DnsRecordType.SOA) }.Where(x => x.Item1).Select(x => x.Item2).ToArray();
        if (types.Length == 0) throw new ArgumentException("Select at least one DNS record type.");
        if (UseSystemResolvers)
        {
            var servers = await Task.Run(() => DnsServerSelection.Filter(NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up).SelectMany(n => n.GetIPProperties().DnsAddresses), IncludeIpv6Dns), token);
            ResolverText = string.Join(Environment.NewLine, servers);
            if (servers.Length == 0) throw new ArgumentException("No active adapter DNS servers were found; enter resolver IPs.");
        }
        var resolvers = DiagnosticInput.Lines(ResolverText, 4).Select(DiagnosticInput.DnsServer).ToArray();
        if (names.Length * types.Length * resolvers.Length > DiagnosticInput.MaxDnsQueries) throw new ArgumentException("Maximum 512 DNS queries per run; reduce targets, resolvers or record types.");
        var jobs = new List<Func<CancellationToken, Task<DnsTestResult>>>();
        foreach (string name in names) foreach (var type in types) foreach (var resolver in resolvers) { var request = new DnsTestRequest(DiagnosticInput.DnsName(name, type), type, resolver, timeout); jobs.Add(t => _client.QueryAsync(request, t)); }
        return jobs;
    }
    protected override string Csv() => "Time,Query,Type,Resolver,Status,ElapsedMs,Transport,Answers,Details,Error\n" + string.Join('\n', Results.Select(r => Row(r.Time.ToString("O"), r.Name, r.Type, r.Resolver, r.Status, r.ElapsedMs, r.Transport, r.Answers, r.Details, r.Error)));
}
