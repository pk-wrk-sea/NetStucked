using System.Collections.Immutable;
using System.Data;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetStucked.Core;
using NetStucked.Infrastructure;
using NetStucked.Desktop.Services;

namespace NetStucked.Desktop.ViewModels;

public sealed record CoverageColumn(int Index, string Label);
public sealed record CoverageResultRow(string Cve, IpsCoverageResult PaloAlto, IpsCoverageResult CheckPoint, string SourceRows)
{
    public string PaloStatus => PaloAlto.Status;
    public string CheckPointStatus => CheckPoint.Status;
    public string Freshness => $"PA: {Compact(PaloAlto)} · CP: {Compact(CheckPoint)}";
    private static string Compact(IpsCoverageResult r) => r.CheckedAt is null ? "not checked" : r.Previous is not null ? "refresh failed / old cache" : r.State == IpsCoverageState.Unknown ? "Unknown" : r.Stale ? "STALE" : r.FromCache ? "cached" : "fresh result";
    public static string Fresh(IpsCoverageResult r) => r.CheckedAt is null ? "not checked" : $"{(r.FromCache ? "cache" : "checked")} {r.CheckedAt.Value.ToLocalTime():yyyy-MM-dd HH:mm}; source {(r.SourceDate is { } sd ? sd.ToString("yyyy-MM-dd") : "date unknown")}{(r.Stale ? " · STALE" : "")}";
}
public sealed record CoverageGroupRow(string Name, string SourceRows, int CveCount, string PaloAlto, string CheckPoint);
public sealed record CoverageSignatureRow(string Vendor, string Evidence, string Id, string Name, string Type, string Status, string Release, string ContentVersion, string Limitations, string SnapshotAction);

public partial class IpsCoverageViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IEngineeringFiles _files;
    private readonly ISecretVault _vault;
    private readonly IIpsProviderFactory _factory;
    private readonly IpsCoverageCoordinator _coordinator;
    private CancellationTokenSource? _cancel;
    private Task? _active;
    private int _generation;
    private bool _disposed;
    private CoverageFile? _file, _snapshotFile;
    private CoverageResultRow[] _allRows = [];
    private CheckPointSnapshot? _snapshot;
    private Guid? _sourceInputId;
    private string? _sourceCve;
    public IpsCoverageViewModel(IEngineeringFiles files, ISecretVault vault, IIpsProviderFactory factory, IIpsCoverageCache cache)
    { _files = files; _vault = vault; _factory = factory; _coordinator = new(cache); UpdateKeyStatus(); }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEdit))] private bool _isBusy;
    public bool CanEdit => !IsBusy;
    [ObservableProperty] private string _message = "Import a VA report, confirm mapping, choose sources, then Check coverage. No automatic requests.";
    [ObservableProperty] private int _selectedTab;
    [ObservableProperty] private string _sourceName = "No VA report imported · drop .xlsx / .csv or select a file";
    [ObservableProperty] private string[] _sheets = [];
    [ObservableProperty] private string _selectedSheet = "";
    [ObservableProperty] private string _headerRow = "1";
    [ObservableProperty] private CoverageColumn[] _columns = [];
    [ObservableProperty] private int _cveColumn = -1;
    [ObservableProperty] private int _addressColumn = -1;
    [ObservableProperty] private int _hostnameColumn = -1;
    [ObservableProperty] private int _pluginColumn = -1;
    [ObservableProperty] private int _pluginNameColumn = -1;
    [ObservableProperty] private int _severityColumn = -1;
    [ObservableProperty] private DataView? _preview;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(InputSummary))] private CoverageInput? _input;
    [ObservableProperty] private CoverageIssue[] _importIssues = [];
    [ObservableProperty] private bool _usePaloAlto = true;
    [ObservableProperty] private bool _useCheckPoint = true;
    [ObservableProperty] private string _paloAltoStatus = "API key not configured";
    [ObservableProperty] private string _snapshotStatus = "No Check Point CSV snapshot imported";
    [ObservableProperty] private string _snapshotHeaderRow = "1";
    [ObservableProperty] private CoverageColumn[] _snapshotColumns = [];
    [ObservableProperty] private int _snapshotCveColumn = -1;
    [ObservableProperty] private int _snapshotNameColumn = -1;
    [ObservableProperty] private int _snapshotIdColumn = -1;
    [ObservableProperty] private int _snapshotActionColumn = -1;
    [ObservableProperty] private string _snapshotPackage = "";
    [ObservableProperty] private string _snapshotDate = "";
    [ObservableProperty] private string _filter = "All CVE";
    public string[] Filters { get; } = ["All CVE", "Found · both", "Found · one vendor", "Not found in source", "Unknown / not checked", "Pending", "Vendor states cannot cover"];
    [ObservableProperty] private string _groupBy = "By CVE";
    public string[] Groupings { get; } = ["By CVE", "By plugin", "By asset"];
    public bool IsCveView => GroupBy == "By CVE";
    public bool IsGroupView => !IsCveView;
    [ObservableProperty] private CoverageResultRow[] _resultRows = [];
    [ObservableProperty] private CoverageGroupRow[] _groupRows = [];
    [ObservableProperty] private CoverageResultRow? _selectedResult;
    [ObservableProperty] private CoverageGroupRow? _selectedGroup;
    [ObservableProperty] private CoverageSignatureRow[] _signatures = [];
    [ObservableProperty] private string _evidence = "Select a CVE to inspect signatures, source and original rows.";
    [ObservableProperty] private DataView? _selectedSourceRows;
    [ObservableProperty] private string _paloSummary = "Palo Alto · no run";
    [ObservableProperty] private string _checkPointSummary = "Check Point · no run";
    [ObservableProperty] private string _runSummary = "No coverage run";
    [ObservableProperty] private int _completedJobs;
    [ObservableProperty] private int _totalJobs = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ExportSummary))] private CoverageRun? _run;
    [ObservableProperty] private bool _acknowledgePartial;
    public string InputSummary => Input is { } i ? $"{i.Rows.Length} source rows · {i.AssetCount} assets · {i.PluginCount} plugins · {i.Cves.Length} unique CVE · {i.Associations.Length} associations · {i.Issues.Length} import issues" : "Import and confirm column mapping first.";
    public string ExportSummary => Run is { } r ? $"{(r.Partial ? "PARTIAL — unchecked jobs remain" : "Completed run — Unknown still requires review")}\nRun {r.Id}\n{InputSummary}\n{r.CompletedJobs}/{r.TotalJobs} provider-CVE outcomes\nNew workbook: Summary, CVE Details, Findings, Signatures, Row CVE Links, Import Issues.\nOriginal Remarks / Status tracking and all source columns retained as Original.*; derived fields use IPS.*." : "Run Check coverage first. No report is ready.";

    partial void OnIsBusyChanged(bool value) { CheckCommand.NotifyCanExecuteChanged(); RefreshCommand.NotifyCanExecuteChanged(); RetryFailedCommand.NotifyCanExecuteChanged(); ConfirmMappingCommand.NotifyCanExecuteChanged(); PreviewMappingCommand.NotifyCanExecuteChanged(); ApplySnapshotCommand.NotifyCanExecuteChanged(); PreviewSnapshotMappingCommand.NotifyCanExecuteChanged(); TestConnectionCommand.NotifyCanExecuteChanged(); ExportCommand.NotifyCanExecuteChanged(); RemoveKeyCommand.NotifyCanExecuteChanged(); }
    partial void OnSelectedSheetChanged(string value) { if (_file is not null && !IsBusy) { Input = null; Run = null; try { LoadMapping(); } catch (ArgumentException e) { Message = e.Message; } } }
    private void InvalidateMapping() { if (!IsBusy) { Input = null; Run = null; } }
    partial void OnHeaderRowChanged(string value) => InvalidateMapping();
    // Bind selections to objects from the current header list. SelectedValue coercion
    // otherwise leaves an apparently empty WPF selector after reimport/tab recreation.
    public CoverageColumn? CveSelection { get => Columns.FirstOrDefault(c => c.Index == CveColumn); set { if (value is not null) CveColumn = value.Index; } }
    public CoverageColumn? AddressSelection { get => Columns.FirstOrDefault(c => c.Index == AddressColumn); set { if (value is not null) AddressColumn = value.Index; } }
    public CoverageColumn? HostnameSelection { get => Columns.FirstOrDefault(c => c.Index == HostnameColumn); set { if (value is not null) HostnameColumn = value.Index; } }
    public CoverageColumn? PluginSelection { get => Columns.FirstOrDefault(c => c.Index == PluginColumn); set { if (value is not null) PluginColumn = value.Index; } }
    public CoverageColumn? PluginNameSelection { get => Columns.FirstOrDefault(c => c.Index == PluginNameColumn); set { if (value is not null) PluginNameColumn = value.Index; } }
    public CoverageColumn? SeveritySelection { get => Columns.FirstOrDefault(c => c.Index == SeverityColumn); set { if (value is not null) SeverityColumn = value.Index; } }
    partial void OnColumnsChanged(CoverageColumn[] value) { foreach (string name in new[] { nameof(CveSelection), nameof(AddressSelection), nameof(HostnameSelection), nameof(PluginSelection), nameof(PluginNameSelection), nameof(SeveritySelection) }) OnPropertyChanged(name); }
    partial void OnCveColumnChanged(int value) { OnPropertyChanged(nameof(CveSelection)); InvalidateMapping(); }
    partial void OnAddressColumnChanged(int value) { OnPropertyChanged(nameof(AddressSelection)); InvalidateMapping(); }
    partial void OnHostnameColumnChanged(int value) { OnPropertyChanged(nameof(HostnameSelection)); InvalidateMapping(); }
    partial void OnPluginColumnChanged(int value) { OnPropertyChanged(nameof(PluginSelection)); InvalidateMapping(); }
    partial void OnPluginNameColumnChanged(int value) { OnPropertyChanged(nameof(PluginNameSelection)); InvalidateMapping(); }
    partial void OnSeverityColumnChanged(int value) { OnPropertyChanged(nameof(SeveritySelection)); InvalidateMapping(); }
    private void InvalidateSnapshot() { if (!IsBusy && _snapshot is not null) { _snapshot = null; Run = null; SnapshotStatus = "Snapshot mapping/metadata changed. Click Apply snapshot to use it."; } }
    partial void OnSnapshotHeaderRowChanged(string value) => InvalidateSnapshot();
    public CoverageColumn? SnapshotCveSelection { get => SnapshotColumns.FirstOrDefault(c => c.Index == SnapshotCveColumn); set { if (value is not null) SnapshotCveColumn = value.Index; } }
    public CoverageColumn? SnapshotNameSelection { get => SnapshotColumns.FirstOrDefault(c => c.Index == SnapshotNameColumn); set { if (value is not null) SnapshotNameColumn = value.Index; } }
    public CoverageColumn? SnapshotIdSelection { get => SnapshotColumns.FirstOrDefault(c => c.Index == SnapshotIdColumn); set { if (value is not null) SnapshotIdColumn = value.Index; } }
    public CoverageColumn? SnapshotActionSelection { get => SnapshotColumns.FirstOrDefault(c => c.Index == SnapshotActionColumn); set { if (value is not null) SnapshotActionColumn = value.Index; } }
    partial void OnSnapshotColumnsChanged(CoverageColumn[] value) { foreach (string name in new[] { nameof(SnapshotCveSelection), nameof(SnapshotNameSelection), nameof(SnapshotIdSelection), nameof(SnapshotActionSelection) }) OnPropertyChanged(name); }
    partial void OnSnapshotCveColumnChanged(int value) { OnPropertyChanged(nameof(SnapshotCveSelection)); InvalidateSnapshot(); }
    partial void OnSnapshotNameColumnChanged(int value) { OnPropertyChanged(nameof(SnapshotNameSelection)); InvalidateSnapshot(); }
    partial void OnSnapshotIdColumnChanged(int value) { OnPropertyChanged(nameof(SnapshotIdSelection)); InvalidateSnapshot(); }
    partial void OnSnapshotActionColumnChanged(int value) { OnPropertyChanged(nameof(SnapshotActionSelection)); InvalidateSnapshot(); }
    partial void OnSnapshotPackageChanged(string value) => InvalidateSnapshot();
    partial void OnSnapshotDateChanged(string value) => InvalidateSnapshot();
    partial void OnUsePaloAltoChanged(bool value) { if (!IsBusy) Run = null; }
    partial void OnUseCheckPointChanged(bool value) { if (!IsBusy) Run = null; }
    partial void OnFilterChanged(string value) => RebuildFiltered();
    partial void OnGroupByChanged(string value) { SelectedGroup = null; OnPropertyChanged(nameof(IsCveView)); OnPropertyChanged(nameof(IsGroupView)); RebuildFiltered(); }
    partial void OnSelectedGroupChanged(CoverageGroupRow? value) { RebuildFiltered(); if (ResultRows.FirstOrDefault() is { } first) SelectedResult = first; }
    partial void OnSelectedResultChanged(CoverageResultRow? value) => ShowEvidence(value);
    private bool Idle() => !IsBusy && !_disposed;
    private bool CanCheck() => Idle() && Input?.Cves.Length > 0;
    private bool CanExport() => Idle() && Run is not null;
    partial void OnInputChanged(CoverageInput? value) { CheckCommand.NotifyCanExecuteChanged(); RefreshCommand.NotifyCanExecuteChanged(); RetryFailedCommand.NotifyCanExecuteChanged(); }
    partial void OnRunChanged(CoverageRun? value) { ExportCommand.NotifyCanExecuteChanged(); if (value is null) { _allRows = []; ResultRows = []; GroupRows = []; Signatures = []; SelectedResult = null; PaloSummary = "Palo Alto · no run"; CheckPointSummary = "Check Point · no run"; RunSummary = "No coverage run"; } }
    [RelayCommand] private async Task Import() { string? path = _files.Open("coverage"); if (path is not null) await ImportAsync(path); }
    public Task ImportAsync(string path) => Begin(async token =>
    {
        var loaded = await CoverageImport.ReadAsync(path, token); _file = loaded; Input = null; Run = null; ImportIssues = []; _allRows = []; ResultRows = []; GroupRows = []; Sheets = loaded.Sheets.Select(s => s.Name).ToArray();
        SelectedSheet = Sheets[0]; HeaderRow = loaded.Sheets[0].Rows.FirstOrDefault(r => r.Values.Any(v => !string.IsNullOrWhiteSpace(v)))?.Number.ToString(CultureInfo.InvariantCulture) ?? "1";
        LoadMapping(); SourceName = Path.GetFileName(path) + " · read-only · SHA-256 " + loaded.Sha256[..12]; SelectedTab = 0; Message = "Preview loaded. Confirm sheet, header and CVE mapping; no provider request sent.";
    });
    private static int Header(string text) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n is > 0 and <= 1000 ? n : throw new ArgumentException("Header row must be 1–1000.");
    private void LoadMapping()
    {
        var sheet = _file?.Sheets.Single(s => s.Name == SelectedSheet) ?? throw new ArgumentException("Import a report first.");
        var header = sheet.Rows.SingleOrDefault(r => r.Number == Header(HeaderRow)) ?? throw new ArgumentException("Header row not found.");
        Columns = MakeColumns(header.Values); var mapping = CoverageText.AutoMap(header.Values); CveColumn = mapping.Cve; AddressColumn = mapping.Address; HostnameColumn = mapping.Hostname; PluginColumn = mapping.Plugin; PluginNameColumn = mapping.PluginName; SeverityColumn = mapping.Severity;
        Preview = Table(sheet, header.Number, header.Values);
    }
    private static CoverageColumn[] MakeColumns(ImmutableArray<string> headers) => new[] { new CoverageColumn(-1, "— Not mapped —") }.Concat(headers.Select((h, i) => new CoverageColumn(i, $"{i + 1}: {(h.Length > 0 ? h : "(empty header)")}"))).ToArray();
    private static DataView Table(CoverageSheet sheet, int headerRow, ImmutableArray<string> headers)
    {
        var table = new DataTable(); table.Columns.Add("Source row", typeof(int)); for (int i = 0; i < headers.Length; i++) table.Columns.Add($"{i + 1}: {headers[i]}");
        foreach (var r in sheet.Rows.Where(r => r.Number > headerRow).Take(100)) table.Rows.Add(new object[] { r.Number }.Concat(Enumerable.Range(0, headers.Length).Select(i => (object)(i < r.Values.Length ? r.Values[i] : ""))).ToArray());
        return table.DefaultView;
    }
    [RelayCommand(CanExecute = nameof(Idle))] private Task PreviewMapping() => Begin(token => { LoadMapping(); Input = null; Run = null; return Task.CompletedTask; });
    [RelayCommand(CanExecute = nameof(Idle))] private Task ConfirmMapping() => Begin(async token =>
    {
        if (_file is null) throw new ArgumentException("Import a report first.");
        var mapping = new CoverageMapping(CveColumn, AddressColumn, HostnameColumn, PluginColumn, PluginNameColumn, SeverityColumn);
        Input = await Task.Run(() => CoverageText.CreateInput(_file, SelectedSheet, Header(HeaderRow), mapping), token); Run = null; ImportIssues = Input.Issues.ToArray();
        Message = InputSummary + ". CVE values come from the report; applicability/exploit is not verified."; SelectedTab = 1;
    });
    [RelayCommand] private async Task ImportSnapshot() { string? path = _files.Open("csv"); if (path is not null) await ImportSnapshotAsync(path); }
    public Task ImportSnapshotAsync(string path) => Begin(async token =>
    {
        if (!Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Check Point source must be a SmartConsole CSV export.");
        _snapshotFile = await CoverageImport.ReadAsync(path, token); _snapshot = null; Run = null; SnapshotHeaderRow = _snapshotFile.Sheets[0].Rows.FirstOrDefault()?.Number.ToString(CultureInfo.InvariantCulture) ?? "1";
        SnapshotPackage = ""; SnapshotDate = ""; LoadSnapshotMapping(); SnapshotStatus = "CSV loaded. Confirm mapping and Apply snapshot before checking."; SelectedTab = 1;
    });
    private void LoadSnapshotMapping()
    {
        var header = _snapshotFile?.Sheets[0].Rows.SingleOrDefault(r => r.Number == Header(SnapshotHeaderRow)) ?? throw new ArgumentException("Import a snapshot and select its header row.");
        SnapshotColumns = MakeColumns(header.Values); SnapshotCveColumn = CoverageText.FindColumn(header.Values, "Industry Reference", "CVE", "CVEs", "CVE ID"); SnapshotNameColumn = CoverageText.FindColumn(header.Values, "Protection", "Protection Name", "Name"); SnapshotIdColumn = CoverageText.FindColumn(header.Values, "Protection ID", "ID", "UID"); SnapshotActionColumn = CoverageText.FindColumn(header.Values, "Action", "Profile Action", "Profile_Name");
    }
    [RelayCommand(CanExecute = nameof(Idle))] private Task PreviewSnapshotMapping() => Begin(token => { LoadSnapshotMapping(); return Task.CompletedTask; });
    [RelayCommand(CanExecute = nameof(Idle))] private Task ApplySnapshot() => Begin(async token =>
    {
        if (_snapshotFile is null) throw new ArgumentException("Import a Check Point CSV first.");
        DateTimeOffset? date = null;
        if (SnapshotDate.Trim().Length > 0) date = DateTimeOffset.TryParseExact(SnapshotDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : throw new ArgumentException("Source date must be yyyy-MM-dd, or leave blank for unknown.");
        _snapshot = await Task.Run(() => CheckPointSnapshot.Create(_snapshotFile, Header(SnapshotHeaderRow), SnapshotCveColumn, SnapshotNameColumn, SnapshotIdColumn, SnapshotActionColumn, SnapshotPackage.Trim(), date), token);
        Run = null; SnapshotStatus = $"Ready · {_snapshot.FileName} · {_snapshot.ByCve.Count} indexed CVE · source {date?.ToString("yyyy-MM-dd") ?? "date unknown"}\nImported {_snapshot.ImportedAt.ToLocalTime():yyyy-MM-dd HH:mm} · snapshot {_snapshot.Identity[^12..]}\n" + string.Join("\n", _snapshot.Warnings); Message = "Snapshot applied; its hash, mapping and user metadata define a separate cache scope.";
    });
    public void SaveApiKey(string key)
    {
        if (IsBusy) { Message = "Cancel and wait for the active operation before changing credentials."; return; }
        key = key.Trim(); if (key.Length is < 8 or > 2048 || key.Any(char.IsControl)) { Message = "Enter a valid API key without control characters."; return; }
        try { _vault.Write("threat-vault", key); Run = null; UpdateKeyStatus(); Message = "Threat Vault key explicitly saved in Windows Credential Manager. Test connection before checking."; }
        catch { Message = "Windows protected credential could not be saved. No key was written to files/logs."; }
    }
    private void UpdateKeyStatus() { try { PaloAltoStatus = _vault.Read("threat-vault") is null ? "API key not configured" : "Key saved in Windows Credential Manager · connection not tested in this session"; } catch { PaloAltoStatus = "Windows protected credential unavailable"; } }
    [RelayCommand(CanExecute = nameof(Idle))] private void RemoveKey() { try { _vault.Delete("threat-vault"); Run = null; UpdateKeyStatus(); Message = "Saved Threat Vault key removed."; } catch { Message = "Windows protected credential could not be removed."; } }
    private string? ReadKey() { try { return _vault.Read("threat-vault"); } catch { return null; } }
    [RelayCommand(CanExecute = nameof(Idle))] private Task TestConnection() => Begin(async token =>
    {
        string? key = ReadKey(); if (key is null) { PaloAltoStatus = "API key not configured / unavailable · no request sent"; return; }
        var result = await _factory.PaloAlto(key).LookupAsync(Input?.Cves.FirstOrDefault() ?? "CVE-2021-1647", token);
        PaloAltoStatus = result.Failure.Length == 0 ? $"API responded successfully · {DateTimeOffset.Now:yyyy-MM-dd HH:mm} · metadata access only" : "Test failed · " + result.Reason;
        Message = PaloAltoStatus;
    });
    [RelayCommand(CanExecute = nameof(CanCheck))] private Task Check() => CheckAsync(false, false);
    [RelayCommand(CanExecute = nameof(CanCheck))] private Task Refresh() => CheckAsync(true, false);
    [RelayCommand(CanExecute = nameof(CanCheck))] private Task RetryFailed() => CheckAsync(true, true);
    public Task CheckAsync(bool refresh = false, bool retry = false) => Begin(async token =>
    {
        var input = Input ?? throw new ArgumentException("Confirm the report mapping first.");
        string? key = UsePaloAlto ? ReadKey() : null;
        IIpsCoverageProvider pa = key is not null ? _factory.PaloAlto(key) : new UnavailableIpsProvider(IpsVendor.PaloAlto, UsePaloAlto ? "Threat Vault API key not configured/unavailable; no request sent." : "Provider not selected; no request sent.");
        IIpsCoverageProvider cp = UseCheckPoint && _snapshot is not null ? new CheckPointCoverageProvider(_snapshot) : new UnavailableIpsProvider(IpsVendor.CheckPoint, UseCheckPoint ? "Apply a Check Point CSV snapshot first; no source available." : "Provider not selected; no lookup performed.");
        int generation = _generation; var previous = retry ? Run : null; SelectedTab = 2; AcknowledgePartial = false;
        var progress = new Progress<CoverageRun>(r => { if (generation == _generation && Input?.Id == input.Id) ApplyRun(r); });
        var run = await Task.Run(() => _coordinator.RunAsync(input, [pa, cp], refresh, progress, token, previous), CancellationToken.None);
        if (generation == _generation && Input?.Id == input.Id) { ApplyRun(run); Message = run.Cancelled ? "Cancelled. Completed evidence retained; export requires PARTIAL acknowledgement." : "Coverage lookup finished. Review Unknown, source freshness and import issues. No firewall configuration changed."; }
    });
    private void ApplyRun(CoverageRun run)
    {
        Run = run; CompletedJobs = run.CompletedJobs; TotalJobs = Math.Max(1, run.TotalJobs); RunSummary = $"Run {run.Id} · {run.CompletedJobs}/{run.TotalJobs} outcomes · {(run.Partial ? "PARTIAL" : "Complete")} · {run.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm}";
        var result = run.Results.ToDictionary(r => (r.Cve, r.Vendor)); var links = run.Input.Associations.ToLookup(a => a.Cve, a => a.Row);
        _allRows = run.Input.Cves.Select(c => new CoverageResultRow(c, result[(c, IpsVendor.PaloAlto)], result[(c, IpsVendor.CheckPoint)], string.Join(", ", links[c]))).ToArray();
        PaloSummary = Summary(run.Results.Where(r => r.Vendor == IpsVendor.PaloAlto)); CheckPointSummary = Summary(run.Results.Where(r => r.Vendor == IpsVendor.CheckPoint));
        RebuildFiltered();
    }
    private static string Summary(IEnumerable<IpsCoverageResult> source)
    {
        var rows = source.ToArray(); int found = rows.Count(r => r.IsIpsFound);
        return $"{found}/{rows.Length} Found" + (found > 0 && found < rows.Length ? " · รองรับบางส่วน" : "") + "\n" + string.Join(" · ", rows.GroupBy(r => r.State).OrderBy(g => g.Key).Select(g => $"{g.Count()} {CoverageText.State(g.Key)}")) + $"\n{rows.Count(r => r.FromCache)} cached · {rows.Count(r => r.Stale)} stale · {rows.Count(r => r.Previous is not null)} failed refresh with old evidence";
    }
    private void RebuildFiltered()
    {
        bool Match(CoverageResultRow r) => Filter switch { "Found · both" => r.PaloAlto.IsIpsFound && r.CheckPoint.IsIpsFound, "Found · one vendor" => r.PaloAlto.IsIpsFound != r.CheckPoint.IsIpsFound, "Not found in source" => r.PaloAlto.State == IpsCoverageState.NotFoundInSource || r.CheckPoint.State == IpsCoverageState.NotFoundInSource, "Unknown / not checked" => new[] { r.PaloAlto.State, r.CheckPoint.State }.Any(s => s is IpsCoverageState.Unknown or IpsCoverageState.NotChecked), "Pending" => r.PaloAlto.State == IpsCoverageState.Pending || r.CheckPoint.State == IpsCoverageState.Pending, "Vendor states cannot cover" => r.PaloAlto.State == IpsCoverageState.VendorCannotCover || r.CheckPoint.State == IpsCoverageState.VendorCannotCover, _ => true };
        string? selected = SelectedResult?.Cve; var filtered = _allRows.Where(Match).ToArray();
        if (Input is { } input && IsGroupView)
        {
            var links = input.Associations.ToLookup(a => a.Row, a => a.Cve); var all = _allRows.ToDictionary(r => r.Cve);
            string Key(CoverageSourceRow row) => GroupBy == "By plugin" ? input.Field(row, input.Mapping.Plugin) is { Length: > 0 } p ? p : "(Plugin not supplied)" : input.Field(row, input.Mapping.Address) is { Length: > 0 } ip ? ip : input.Field(row, input.Mapping.Hostname) is { Length: > 0 } host ? host : "(Asset not supplied)";
            GroupRows = input.Rows.GroupBy(Key).Select(g => { var cves = g.SelectMany(r => links[r.Number]).Distinct().ToArray(); var values = cves.Select(c => all.GetValueOrDefault(c)).Where(r => r is not null).Cast<CoverageResultRow>().ToArray(); return new CoverageGroupRow(g.Key, string.Join(", ", g.Select(r => r.Number)), cves.Length, Summary(values.Select(r => r.PaloAlto)), Summary(values.Select(r => r.CheckPoint))); }).ToArray();
            if (SelectedGroup is { } group) { var permitted = input.Rows.Where(r => Key(r) == group.Name).SelectMany(r => links[r.Number]).ToHashSet(); filtered = filtered.Where(r => permitted.Contains(r.Cve)).ToArray(); }
        }
        ResultRows = filtered; SelectedResult = filtered.FirstOrDefault(r => r.Cve == selected) ?? filtered.FirstOrDefault();
    }
    private void ShowEvidence(CoverageResultRow? row)
    {
        if (row is null) { Evidence = "Select a CVE to inspect its evidence."; Signatures = []; SelectedSourceRows = null; _sourceCve = null; return; }
        if (Input is { } input && (_sourceInputId != input.Id || _sourceCve != row.Cve))
        {
            var linked = input.Associations.Where(a => a.Cve == row.Cve).Select(a => a.Row).ToHashSet();
            var sourceTable = new DataTable(); sourceTable.Columns.Add("Source row", typeof(int)); for (int i = 0; i < input.Headers.Length; i++) sourceTable.Columns.Add($"{i + 1}: {input.Headers[i]}");
            foreach (var sourceRow in input.Rows.Where(r => linked.Contains(r.Number))) sourceTable.Rows.Add(new object[] { sourceRow.Number }.Concat(Enumerable.Range(0, input.Headers.Length).Select(i => (object)input.Field(sourceRow, i))).ToArray());
            SelectedSourceRows = sourceTable.DefaultView; _sourceInputId = input.Id; _sourceCve = row.Cve;
        }
        var results = new[] { row.PaloAlto, row.CheckPoint };
        Evidence = $"{row.Cve} · {Input?.Sheet} rows {row.SourceRows}\nCVE is referenced by the imported report; device applicability/exploit is not verified.\n\n" + string.Join("\n\n", results.Select(r => $"{CoverageText.Vendor(r.Vendor)} · {r.Status}\n{r.Source} / {r.Method}\n{CoverageResultRow.Fresh(r)} · cache age {(r.CheckedAt is { } time ? (DateTimeOffset.UtcNow - time).TotalHours.ToString("F1", CultureInfo.InvariantCulture) + "h" : "unknown")}\nSource identity: {r.SourceId}\n{r.Reason}" + (r.Previous is { } old ? $"\nLATEST REFRESH FAILED ({r.Failure}); previous {old.Status}, checked {old.CheckedAt:O}, source {old.SourceDate:O}. Old evidence is stale and not a fresh result." : "")));
        Signatures = results.SelectMany(r => new[] { (Result: r, Kind: "Result") }.Concat(r.Previous is { } p ? [(Result: p, Kind: "Previous / stale")] : [])).SelectMany(x => x.Result.Signatures.Select(s => new CoverageSignatureRow(CoverageText.Vendor(x.Result.Vendor), x.Kind, s.Id, s.Name, s.Type, s.Status, s.Release, s.ContentVersion, s.Limitations, s.SnapshotAction))).ToArray();
    }
    [RelayCommand(CanExecute = nameof(CanExport))] private async Task Export()
    {
        if (Run is not { } snapshot) return;
        if (snapshot.Partial && !AcknowledgePartial) { Message = "Tick Export partial results to acknowledge unchecked jobs before exporting."; return; }
        string? path = _files.Save("xlsx", $"ips-coverage-{DateTime.Now:yyyyMMdd-HHmmss}{(snapshot.Partial ? "-PARTIAL" : "")}.xlsx");
        if (path is null) return;
        await Begin(async token => { await CoverageWorkbook.WriteAsync(path, snapshot, token); Message = $"Exported new workbook: {Path.GetFileName(path)}. Original source retained."; });
    }
    [RelayCommand] private void Cancel() => _cancel?.Cancel();
    private async Task Begin(Func<CancellationToken, Task> action)
    {
        if (_disposed) return; int generation = ++_generation; _cancel?.Cancel(); if (_active is { } old) await old;
        if (generation != _generation || _disposed) return;
        _cancel?.Dispose(); _cancel = new(); IsBusy = true; var token = _cancel.Token; _active = Execute(); await _active;
        async Task Execute()
        {
            try { await action(token); }
            catch (OperationCanceledException) { if (generation == _generation) Message = "Operation cancelled; previous source files retained."; }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or FormatException or OverflowException or System.Xml.XmlException or System.Text.Json.JsonException)
            { if (generation == _generation) Message = "Operation failed: " + e.Message; }
            finally { if (generation == _generation) { _cancel?.Dispose(); _cancel = null; IsBusy = false; } }
        }
    }
    public async Task StopForUpdateAsync() { _cancel?.Cancel(); if (_active is { } active) await active; }
    public async ValueTask DisposeAsync() { if (_disposed) return; _disposed = true; await StopForUpdateAsync(); _cancel?.Dispose(); _cancel = null; }
}
