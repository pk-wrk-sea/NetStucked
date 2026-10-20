using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetStucked.Core;
using NetStucked.Desktop.Services;
using NetStucked.Infrastructure;

namespace NetStucked.Desktop.ViewModels;

public abstract partial class EngineeringPageViewModel : ObservableObject
{
    public EngineeringContext Workspace { get; }
    protected IEngineeringFiles Files { get; }
    protected IDesktopDialogs Dialogs { get; }
    protected EngineeringPageViewModel(EngineeringContext workspace, IEngineeringFiles files, IDesktopDialogs dialogs)
    { Workspace = workspace; Files = files; Dialogs = dialogs; workspace.Changed += Refresh; }
    protected abstract void Refresh();
    [RelayCommand] private Task Reload() => Workspace.ReloadAsync();
    [RelayCommand] private void Cancel() => Workspace.Cancel();
    protected Task Run(Func<CancellationToken, Task> action) => Workspace.RunAsync(action);
    protected static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source) { var array = source.ToArray(); target.Clear(); foreach (var item in array) target.Add(item); }
    protected static async Task ReportAsync(string path, string text, CancellationToken token)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllTextAsync(temp, text, new UTF8Encoding(true), token); token.ThrowIfCancellationRequested(); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
public sealed record AssetRow(Asset Asset, string ObservedVersion, string ObservedModel, string ObservedSerial, DateTimeOffset? LastCollected);
public partial class AssetPreviewRow(AssetImportRow data) : ObservableObject
{
    public AssetImportRow Data { get; } = data;
    [ObservableProperty] private bool _selected;
}
public partial class InventoryViewModel : EngineeringPageViewModel
{
    public ObservableCollection<AssetRow> Assets { get; } = [];
    public ObservableCollection<AssetPreviewRow> Preview { get; } = [];
    [ObservableProperty] private AssetRow? _selectedAsset;
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _hostname = "";
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _vendor = "Cisco";
    [ObservableProperty] private string _model = "";
    [ObservableProperty] private string _serial = "";
    [ObservableProperty] private string _site = "";
    [ObservableProperty] private string _location = "";
    [ObservableProperty] private string _role = "";
    [ObservableProperty] private string _contract = "";
    [ObservableProperty] private string _maStart = "";
    [ObservableProperty] private string _maEnd = "";
    [ObservableProperty] private string _previewSummary = "No import preview. Dates accept yyyy-MM-dd text or whole-day Excel date cells.";
    private Guid _editingId = Guid.NewGuid();
    private long _previewRevision = -1;
    public string AssetId => _editingId.ToString();
    public string EvidenceDetails => SelectedAsset is { } a ? string.Join('\n', Workspace.Data.Evidence.Where(e => e.AssetId == a.Asset.Id).OrderByDescending(e => e.CollectedAt).Select(e => $"{e.CollectedAt:u} · {e.Family} · {e.Parser} · {e.Source}\nSHA-256 {e.Sha256}\n{string.Join("; ", e.Warnings)}")) : "Select an asset to inspect observation provenance.";
    public InventoryViewModel(EngineeringContext w, IEngineeringFiles f, IDesktopDialogs d) : base(w, f, d) { }
    partial void OnSearchChanged(string value) => Refresh();
    partial void OnSelectedAssetChanged(AssetRow? value)
    {
        if (value is not null) { var a = value.Asset; _editingId = a.Id; Hostname = a.Hostname; Address = a.Address; Vendor = a.Vendor; Model = a.Model; Serial = a.Serial; Site = a.Site; Location = a.Location; Role = a.Role; Contract = a.Contract; MaStart = a.MaStart?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""; MaEnd = a.MaEnd?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""; }
        OnPropertyChanged(nameof(AssetId)); OnPropertyChanged(nameof(EvidenceDetails));
    }
    protected override void Refresh()
    {
        var selected = SelectedAsset?.Asset.Id;
        Replace(Assets, Workspace.Data.Assets.Where(a => string.Join(' ', a.Hostname, a.Address, a.Serial, a.Site, a.Model, a.Contract, a.Role, a.MaStatus).Contains(Search, StringComparison.OrdinalIgnoreCase)).Select(a =>
        { var e = EngineeringRules.Latest(Workspace.Data, a.Id); string Field(string name) => e?.Facts.FirstOrDefault(f => f.Field == name)?.Value ?? "Unknown"; return new AssetRow(a, Field("Version"), Field("Model"), Field("Serial"), e?.CollectedAt); }));
        if (selected is not null) SelectedAsset = Assets.FirstOrDefault(a => a.Asset.Id == selected);
        OnPropertyChanged(nameof(EvidenceDetails));
    }
    [RelayCommand] private void NewAsset() { SelectedAsset = null; _editingId = Guid.NewGuid(); Hostname = Address = Model = Serial = Site = Location = Role = Contract = MaStart = MaEnd = ""; Vendor = "Cisco"; OnPropertyChanged(nameof(AssetId)); }
    private static DateOnly? Date(string text) => text.Length == 0 ? null : DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var x) ? x : throw new ArgumentException("Use MA date yyyy-MM-dd, or leave empty.");
    [RelayCommand] private Task SaveAsset() => Run(async token =>
    {
        var a = new Asset(_editingId, Hostname.Trim(), Address.Trim(), Vendor.Trim(), Model.Trim(), Serial.Trim(), Site.Trim(), Location.Trim(), Role.Trim(), Contract.Trim(), Date(MaStart), Date(MaEnd), DateTimeOffset.UtcNow); EngineeringRules.Validate(a);
        if (a.Serial.Length > 0 && Workspace.Data.Assets.Any(x => x.Id != a.Id && x.Serial.Equals(a.Serial, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Serial belongs to another asset. Review identity before saving.");
        await Workspace.CommitAsync(d => d with { Assets = d.Assets.Where(x => x.Id != a.Id).Append(a).ToArray() }, token); SelectedAsset = Assets.FirstOrDefault(x => x.Asset.Id == a.Id);
    });
    [RelayCommand] private Task DeleteAsset() => Run(async token =>
    {
        if (SelectedAsset is not { } a) return;
        if (Workspace.Data.Evidence.Any(e => e.AssetId == a.Asset.Id) || Workspace.Data.Plans.Any(p => p.AssetId == a.Asset.Id)) throw new ArgumentException("Asset has retained evidence/plans. Deletion is blocked to preserve provenance.");
        if (!Dialogs.Confirm("Delete asset", "Delete this manual asset: " + a.Asset.Hostname + "?")) return;
        await Workspace.CommitAsync(d => d with { Assets = d.Assets.Where(x => x.Id != a.Asset.Id).ToArray() }, token); NewAsset();
    });
    [RelayCommand] private Task ExportWorkbook() => Run(async token => { string? p = Files.Save("xlsx", "NetStucked-Inventory.xlsx"); if (p is null) return; await InventoryWorkbook.WriteAsync(p, Workspace.Data.Assets, token); Workspace.Message = "Exported versioned Excel inventory. Human fields only; observations stay separate."; });
    [RelayCommand] private Task Template() => Run(async token => { string? p = Files.Save("xlsx", "NetStucked-Inventory-template.xlsx"); if (p is null) return; await InventoryWorkbook.WriteAsync(p, [], token); Workspace.Message = "Template saved. Keep schema/header rows; blank AssetId creates a new stable identity."; });
    [RelayCommand] private Task ImportPreview() => Run(async token =>
    {
        string? p = Files.Open("xlsx"); if (p is null) return; Preview.Clear(); _previewRevision = -1;
        var rows = await InventoryWorkbook.ReadAsync(p, Workspace.Data.Assets, token); Replace(Preview, rows.Select(r => new AssetPreviewRow(r))); _previewRevision = Workspace.Data.Revision;
        PreviewSummary = $"{rows.Length} rows · {rows.Count(r => r.Error.Length > 0)} rejected · select rows, then Apply. No changes saved yet.";
    });
    [RelayCommand] private void SelectValid() { foreach (var row in Preview) row.Selected = row.Data.Asset is not null && row.Data.Error.Length == 0; }
    [RelayCommand] private Task ApplyImport() => Run(async token =>
    {
        if (_previewRevision != Workspace.Data.Revision) throw new ArgumentException("Workspace changed since preview; preview the workbook again.");
        var apply = Preview.Where(r => r.Selected && r.Data.Asset is not null && r.Data.Error.Length == 0).Select(r => r.Data.Asset!).ToArray();
        if (apply.Length == 0) throw new ArgumentException("Select valid preview rows first.");
        if (!Dialogs.Confirm("Apply inventory import", $"Apply {apply.Length} selected asset rows? Updates replace human fields shown in the preview; observed facts are preserved.")) return;
        await Workspace.CommitAsync(d => d with { Assets = d.Assets.Where(a => !apply.Any(x => x.Id == a.Id)).Concat(apply).ToArray() }, token); Preview.Clear(); _previewRevision = -1; PreviewSummary = $"Applied {apply.Length} rows atomically.";
    });
    [RelayCommand] private Task ImportEvidence() => Run(async token =>
    {
        if (SelectedAsset is not { } selected) throw new ArgumentException("Select the asset to reconcile with captured evidence first.");
        string? p = Files.Open("evidence"); if (p is null) return;
        var e = await EvidenceFile.ReadAsync(p, selected.Asset.Id, selected.Asset.Address.Length > 0 ? selected.Asset.Address : selected.Asset.Hostname, token);
        if (Workspace.Data.Evidence.Any(x => x.Sha256 == e.Sha256 && x.AssetId == e.AssetId)) throw new ArgumentException("This evidence is already attached to the asset.");
        string facts = string.Join("\n", e.Facts.Where(f => f.Field is "Hostname" or "Model" or "Serial" or "Version").Select(f => f.Field + ": " + f.Value));
        if (!Dialogs.Confirm("Review observed identity", $"Attach this evidence to {selected.Asset.Hostname} ({selected.Asset.Id})?\nReported host: {e.Host}\n{facts}\n{string.Join("\n", e.Warnings)}\nHuman inventory fields will remain separate. Confirm device identity before attaching.")) return;
        await Workspace.CommitAsync(d => d with { Evidence = d.Evidence.Append(e).ToArray() }, token);
    });
    [RelayCommand] private Task ExportObservations() => Run(async token =>
    { string? p = Files.Save("csv", "NetStucked-observations.csv"); if (p is null) return; await ReportAsync(p, CsvExporter.Rows(["AssetId", "EvidenceId", "Host", "Collected UTC", "Family", "Field", "Value", "Command", "Line", "Confidence", "Source", "SHA256"], Workspace.Data.Evidence.SelectMany(e => e.Facts.Select(f => new object?[] { e.AssetId, e.Id, e.Host, e.CollectedAt, e.Family, f.Field, f.Value, f.Command, f.Line, f.Confidence, e.Source, e.Sha256 }))), token); });
    [RelayCommand] private Task ExportAudit() => Run(async token =>
    { string? p = Files.Save("csv", "NetStucked-asset-audit.csv"); if (p is null) return; await ReportAsync(p, CsvExporter.Rows(["ChangeId", "UTC", "Actor", "Before JSON", "After JSON"], (Workspace.Data.Changes ?? []).Select(c => new object?[] { c.Id, c.Time, c.Actor, System.Text.Json.JsonSerializer.Serialize(c.Before), System.Text.Json.JsonSerializer.Serialize(c.After) })), token); });
}

public partial class TopologyViewModel : EngineeringPageViewModel
{
    public ObservableCollection<EvidenceDocument> Devices { get; } = [];
    public ObservableCollection<TopologyLink> Links { get; } = [];
    public ObservableCollection<DeviceFact> Facts { get; } = [];
    [ObservableProperty] private EvidenceDocument? _selectedDevice;
    [ObservableProperty] private TopologyLink? _selectedLink;
    [ObservableProperty] private string _configurationDiff = "Select a device and compare its previous capture. Original source files must remain available and match their saved hashes.";
    public string Summary => SelectedDevice is { } e ? $"{e.Host} · {e.Family} · {e.CollectedAt:u}\n{e.Source}\nSHA-256 {e.Sha256}\n{string.Join("\n", e.Warnings)}" : "Import captured evidence through MA Inventory, then select a device. No discovery or collection runs from this page.";
    public string LinkDetails => SelectedLink is { } l ? $"{l.Device} [{l.LocalInterface}] ↔ {l.Neighbor} [{l.RemoteInterface}]\n{l.Classification} · {l.Freshness}\n{l.Detail}\nSource {l.Source}, line {l.Line}\nSHA-256 {l.Sha256}" : "Select a neighbor/link to inspect its supporting evidence.";
    public TopologyViewModel(EngineeringContext w, IEngineeringFiles f, IDesktopDialogs d) : base(w, f, d) { }
    protected override void Refresh() { var id = SelectedDevice?.Id; Replace(Devices, Workspace.Data.Evidence.GroupBy(e => e.AssetId?.ToString() ?? e.Host).Select(g => g.OrderByDescending(e => e.CollectedAt).First())); SelectedDevice = Devices.FirstOrDefault(e => e.Id == id) ?? Devices.FirstOrDefault(); }
    partial void OnSelectedDeviceChanged(EvidenceDocument? value)
    { Replace(Links, value is null ? [] : TopologyProjection.Build(value, Devices, DateTimeOffset.UtcNow)); Replace(Facts, value?.Facts ?? []); SelectedLink = Links.FirstOrDefault(); OnPropertyChanged(nameof(Summary)); }
    partial void OnSelectedLinkChanged(TopologyLink? value) => OnPropertyChanged(nameof(LinkDetails));
    [RelayCommand] private void Analyze() { OnSelectedDeviceChanged(SelectedDevice); Workspace.Message = "Analyzed existing captured evidence only. No network requests."; }
    [RelayCommand] private Task ComparePrevious() => Run(async token =>
    {
        var current = SelectedDevice ?? throw new ArgumentException("Select a captured device first.");
        var previous = Workspace.Data.Evidence.Where(e => e.AssetId == current.AssetId && e.Id != current.Id && e.CollectedAt < current.CollectedAt).OrderByDescending(e => e.CollectedAt).FirstOrDefault() ?? throw new ArgumentException("No earlier capture for this asset. Attach another capture through MA Inventory.");
        string before = await EvidenceFile.ReadVerifiedTextAsync(previous, token), after = await EvidenceFile.ReadVerifiedTextAsync(current, token);
        string diff = await Task.Run(() => CaptureDiff.Compare(before, after, token), token);
        ConfigurationDiff = $"Before {previous.CollectedAt:u} · {previous.Source}\nSHA-256 {previous.Sha256}\nAfter {current.CollectedAt:u} · {current.Source}\nSHA-256 {current.Sha256}\n\n{diff}";
    });
    [RelayCommand] private Task Export() => Run(async token => { string? p = Files.Save("csv", "NetStucked-one-hop-topology.csv"); if (p is null) return; await ReportAsync(p, CsvExporter.Rows(["Device", "Neighbor", "Address", "Local interface", "Remote interface", "Protocol", "Class", "Freshness", "Collected UTC", "Source", "SHA256", "Line", "Detail"], Links.Select(l => new object?[] { l.Device, l.Neighbor, l.Address, l.LocalInterface, l.RemoteInterface, l.Protocol, l.Classification, l.Freshness, l.CollectedAt, l.Source, l.Sha256, l.Line, l.Detail })), token); });
}
