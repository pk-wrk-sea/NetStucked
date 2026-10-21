using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetStucked.Core;
using NetStucked.Desktop.Services;

namespace NetStucked.Desktop.ViewModels;

public sealed record AssessmentRow(Asset Asset, string State, string Version, string Reason);
public sealed record PlanRow(UpgradePlan Plan, string Device, string State);
public partial class AdvisoryPlannerViewModel : EngineeringPageViewModel
{
    private readonly IAdvisoryLookup _lookup;
    private readonly TimeProvider _clock;
    private bool _refreshing;
    private DateTimeOffset? _lastStatusRefresh;
    private Guid _planId = Guid.NewGuid();
    public ObservableCollection<Asset> Assets { get; } = [];
    public ObservableCollection<Advisory> Advisories { get; } = [];
    public ObservableCollection<AssessmentRow> Assessments { get; } = [];
    public ObservableCollection<PlanRow> Plans { get; } = [];
    public Applicability[] Decisions { get; } = Enum.GetValues<Applicability>();
    [ObservableProperty] private string _cve = "";
    [ObservableProperty] private Advisory? _selectedAdvisory;
    [ObservableProperty] private Asset? _selectedAsset;
    [ObservableProperty] private PlanRow? _selectedPlan;
    [ObservableProperty] private Applicability _decision = Applicability.NeedsReview;
    [ObservableProperty] private string _productEvidence = "";
    [ObservableProperty] private string _configurationEvidence = "";
    [ObservableProperty] private string _vendorUrl = "";
    [ObservableProperty] private string _engineer = "";
    [ObservableProperty] private string _reason = "";
    [ObservableProperty] private string _targetVersion = "";
    [ObservableProperty] private string _fixedUrl = "";
    [ObservableProperty] private string _compatibilityUrl = "";
    [ObservableProperty] private string _owner = "";
    [ObservableProperty] private string _changeId = "";
    [ObservableProperty] private string _wave = "";
    [ObservableProperty] private string _windowUtc = "";
    [ObservableProperty] private string _notes = "";
    public string AdvisoryDetails => SelectedAdvisory is { } a ? $"{a.Cve} · {a.Severity} · KEV: {(a.Kev is null ? "Unknown" : a.Kev == true ? "Listed" : "Not listed at retrieval")}\nNVD revision {a.Revision} · retrieved {a.RetrievedAt:u}\n{a.Source}\n{a.KevSource}\n{a.Description}\nOfficial Cisco references:\n{string.Join("\n", a.CiscoReferences)}\nPrevious advisory revisions retained: {string.Join(", ", (Workspace.Data.AdvisoryHistory ?? []).Where(h => h.Cve == a.Cve).Select(h => h.Revision))}\nNVD API data; this product is not endorsed or certified by NVD." : "Enter a CVE ID and fetch metadata. Only this CVE ID is sent to NVD; CISA KEV is downloaded. No device/configuration upload. No automatic verdict or universally safe version.";
    public string CurrentEvidence => SelectedAsset is { } a && EngineeringRules.Latest(Workspace.Data, a.Id) is { } e ? $"{e.Family} · {e.CollectedAt:u} · evidence {e.Id}\n{string.Join("; ", e.Facts.Where(f => f.Field is "Model" or "Version" or "Serial").Select(f => f.Field + ": " + f.Value))}\n{string.Join("; ", e.Warnings)}" : "No collected evidence attached to selected asset.";
    public AdvisoryPlannerViewModel(EngineeringContext w, IEngineeringFiles f, IDesktopDialogs d, IAdvisoryLookup lookup, TimeProvider? clock = null) : base(w, f, d) { _lookup = lookup; _clock = clock ?? TimeProvider.System; }
    protected override void Refresh()
    {
        var assetId = SelectedAsset?.Id; string? cve = SelectedAdvisory?.Cve; Guid? planId = SelectedPlan?.Plan.Id;
        // Collection resets can clear bound selections. Restore IDs without reloading the active editors.
        _refreshing = true;
        try
        {
            Replace(Assets, Workspace.Data.Assets); Replace(Advisories, Workspace.Data.Advisories.OrderByDescending(a => a.RetrievedAt));
            SelectedAsset = Assets.FirstOrDefault(a => a.Id == assetId) ?? Assets.FirstOrDefault(); SelectedAdvisory = Advisories.FirstOrDefault(a => a.Cve == cve) ?? Advisories.FirstOrDefault();
            RefreshStatusRows(_clock.GetUtcNow(), planId);
        }
        finally { _refreshing = false; }
        if (assetId != SelectedAsset?.Id || cve != SelectedAdvisory?.Cve) LoadReview();
        OnPropertyChanged(nameof(AdvisoryDetails)); OnPropertyChanged(nameof(CurrentEvidence));
    }
    public void RefreshTimeSensitiveState(bool force = false)
    {
        if (!Workspace.Loaded || _refreshing) return;
        var now = _clock.GetUtcNow();
        if (!force && _lastStatusRefresh is { } last && now >= last && now - last < TimeSpan.FromMinutes(1)) return;
        Guid? planId = SelectedPlan?.Plan.Id; _refreshing = true;
        try { RefreshStatusRows(now, planId); }
        finally { _refreshing = false; }
    }
    private void RefreshStatusRows(DateTimeOffset now, Guid? planId)
    {
        RefreshAssessments(now);
        var plans = Workspace.Data.Plans.Select(p => new PlanRow(p, Workspace.Data.Assets.FirstOrDefault(a => a.Id == p.AssetId)?.Hostname ?? "Unknown", EngineeringRules.PlanStatus(p, Workspace.Data, now))).ToArray();
        if (!Plans.SequenceEqual(plans)) Replace(Plans, plans);
        SelectedPlan = Plans.FirstOrDefault(p => p.Plan.Id == planId); _lastStatusRefresh = now;
    }
    private void RefreshAssessments(DateTimeOffset? evaluatedAt = null)
    {
        var now = evaluatedAt ?? _clock.GetUtcNow();
        var rows = Assets.Select(a =>
        {
            var evidence = EngineeringRules.Latest(Workspace.Data, a.Id); var r = Workspace.Data.Reviews.Where(r => r.AssetId == a.Id && r.Cve == SelectedAdvisory?.Cve).OrderByDescending(r => r.ReviewedAt).FirstOrDefault();
            return new AssessmentRow(a, EngineeringRules.Assessment(r, evidence, SelectedAdvisory, now), evidence?.Facts.FirstOrDefault(f => f.Field == "Version")?.Value ?? "Unknown", r?.Reason ?? "Review product/version and enabled configuration against official advisory; no automatic applicability.");
        }).ToArray();
        if (!Assessments.SequenceEqual(rows)) Replace(Assessments, rows);
    }
    partial void OnSelectedAdvisoryChanged(Advisory? value) { if (_refreshing) return; OnPropertyChanged(nameof(AdvisoryDetails)); RefreshAssessments(); LoadReview(); }
    partial void OnSelectedAssetChanged(Asset? value) { if (_refreshing) return; OnPropertyChanged(nameof(CurrentEvidence)); LoadReview(); }
    private void LoadReview()
    {
        var r = Workspace.Data.Reviews.Where(r => r.AssetId == SelectedAsset?.Id && r.Cve == SelectedAdvisory?.Cve).OrderByDescending(r => r.ReviewedAt).FirstOrDefault();
        Decision = r?.Decision ?? Applicability.NeedsReview; ProductEvidence = r?.ProductVersionEvidence ?? ""; ConfigurationEvidence = r?.ConfigurationEvidence ?? ""; VendorUrl = r?.VendorUrl ?? SelectedAdvisory?.CiscoReferences.FirstOrDefault() ?? ""; Engineer = r?.Engineer ?? ""; Reason = r?.Reason ?? "";
    }
    [RelayCommand] private Task Fetch() => Run(async token =>
    {
        string cve = EngineeringRules.CveId(Cve.Trim().ToUpperInvariant()); var result = await _lookup.FetchAsync(cve, token);
        await Workspace.CommitAsync(d => d with { Advisories = d.Advisories.Where(a => a.Cve != result.Cve).Append(result).ToArray() }, token);
        SelectedAdvisory = Advisories.FirstOrDefault(a => a.Cve == cve); Workspace.Message = "Metadata cached. Review official product/configuration conditions before assigning applicability.";
    });
    [RelayCommand] private Task SaveReview() => Run(async token =>
    {
        var asset = SelectedAsset ?? throw new ArgumentException("Select an asset."); var advisory = SelectedAdvisory ?? throw new ArgumentException("Select cached advisory metadata.");
        var evidence = EngineeringRules.Latest(Workspace.Data, asset.Id) ?? throw new ArgumentException("Attach captured evidence through MA Inventory first.");
        var r = new AdvisoryReview(Guid.NewGuid(), asset.Id, evidence.Id, advisory.Cve, advisory.Revision, Decision, ProductEvidence, ConfigurationEvidence, VendorUrl, Engineer.Trim(), Reason, _clock.GetUtcNow());
        var candidate = Workspace.Data with { Reviews = Workspace.Data.Reviews.Append(r).ToArray() }; EngineeringRules.Validate(candidate);
        if (Decision is Applicability.Affected or Applicability.NotAffected && !Dialogs.Confirm("Record engineer assessment", $"Record {Decision} for {asset.Hostname} / {advisory.Cve}?\n{Reason}\nThis classification is limited to the evaluated advisory and captured configuration.")) return;
        await Workspace.CommitAsync(_ => candidate, token);
    });
    partial void OnSelectedPlanChanged(PlanRow? value)
    {
        if (_refreshing || value is null) return; var p = value.Plan; _planId = p.Id;
        SelectedAsset = Assets.FirstOrDefault(a => a.Id == p.AssetId); SelectedAdvisory = Advisories.FirstOrDefault(a => a.Cve == p.Cve);
        TargetVersion = p.TargetVersion; FixedUrl = p.FixedEvidenceUrl; CompatibilityUrl = p.CompatibilityEvidenceUrl; Owner = p.Owner; ChangeId = p.ChangeId; Wave = p.Wave; WindowUtc = p.WindowUtc; Notes = p.Notes;
        if (p.ApprovedBy.Length > 0) Engineer = p.ApprovedBy;
    }
    [RelayCommand] private void NewPlan() { SelectedPlan = null; _planId = Guid.NewGuid(); TargetVersion = FixedUrl = CompatibilityUrl = Owner = ChangeId = Wave = WindowUtc = Notes = ""; }
    private UpgradePlan BuildPlan(bool approve)
    {
        var asset = SelectedAsset ?? throw new ArgumentException("Select an asset."); var advisory = SelectedAdvisory ?? throw new ArgumentException("Select an advisory.");
        var evidence = EngineeringRules.Latest(Workspace.Data, asset.Id) ?? throw new ArgumentException("Attach captured device evidence first.");
        var review = Workspace.Data.Reviews.Where(r => r.AssetId == asset.Id && r.Cve == advisory.Cve).OrderByDescending(r => r.ReviewedAt).FirstOrDefault();
        if (approve)
        {
            string state = EngineeringRules.Assessment(review, evidence, advisory, _clock.GetUtcNow());
            if (state is not ("Affected (engineer reviewed)" or "Not Affected by evaluated advisory")) throw new ArgumentException("Review current advisory/device evidence before approving a plan.");
        }
        var now = _clock.GetUtcNow();
        return new(_planId, asset.Id, evidence.Id, advisory.Cve, TargetVersion.Trim(), FixedUrl.Trim(), CompatibilityUrl.Trim(), Owner.Trim(), ChangeId.Trim(), Wave.Trim(), WindowUtc.Trim(), Notes, approve ? Engineer.Trim() : "", approve ? now : null, now, ReviewId: review?.Id, AdvisoryRevision: advisory.Revision);
    }
    private Task SavePlanAsync(bool approve) => Run(async token =>
    {
        RefreshTimeSensitiveState(force: true);
        var p = BuildPlan(approve); var candidate = Workspace.Data with { Plans = Workspace.Data.Plans.Where(x => x.Id != p.Id).Append(p).ToArray() }; EngineeringRules.Validate(candidate);
        if (approve && !Dialogs.Confirm("Engineer approval", $"Approve this plan for {SelectedAsset?.Hostname} → {p.TargetVersion}?\nChange {p.ChangeId}, wave {p.Wave}, window {p.WindowUtc}\nConfirm official fixed-release and hardware/feature compatibility evidence. This does not execute any device upgrade.")) return;
        await Workspace.CommitAsync(_ => candidate, token); SelectedPlan = Plans.FirstOrDefault(x => x.Plan.Id == p.Id);
    });
    [RelayCommand] private Task SaveDraft() => SavePlanAsync(false);
    [RelayCommand] private Task ApprovePlan() => SavePlanAsync(true);
    [RelayCommand] private Task CompareAfter() => Run(async token =>
    {
        var p = SelectedPlan?.Plan ?? throw new ArgumentException("Select a saved plan.");
        var after = EngineeringRules.Latest(Workspace.Data, p.AssetId) ?? throw new ArgumentException("No evidence.");
        var before = Workspace.Data.Evidence.First(e => e.Id == p.EvidenceId);
        if (after.Id == before.Id || after.CollectedAt <= before.CollectedAt) throw new ArgumentException("Import newer post-change evidence for this asset through MA Inventory first.");
        await Workspace.CommitAsync(d => d with { Plans = d.Plans.Select(x => x.Id == p.Id ? x with { AfterEvidenceId = after.Id, ModifiedAt = _clock.GetUtcNow() } : x).ToArray() }, token);
        Workspace.Message = "Compared observed version with target. Reassess CVE against the new evidence; no upgrade commands were sent.";
    });
    [RelayCommand] private Task ExportCsv() => Run(async token =>
    { string? path = Files.Save("csv", "NetStucked-upgrade-plans.csv"); if (path is null) return; RefreshTimeSensitiveState(force: true); await ReportAsync(path, CsvExporter.Rows(["Schema", "PlanId", "AssetId", "Device", "CVE", "Target", "Status", "Fixed source", "Compatibility source", "Owner", "ChangeId", "Wave", "Window", "Approved by", "Approved UTC", "Before evidence", "After evidence", "Notes"], Plans.Select(x => { var p = x.Plan; return new object?[] { 1, p.Id, p.AssetId, x.Device, p.Cve, p.TargetVersion, x.State, p.FixedEvidenceUrl, p.CompatibilityEvidenceUrl, p.Owner, p.ChangeId, p.Wave, p.WindowUtc, p.ApprovedBy, p.ApprovedAt, p.EvidenceId, p.AfterEvidenceId, p.Notes }; })), token); });
    [RelayCommand] private Task ExportHtml() => Run(async token =>
    {
        string? path = Files.Save("html", "NetStucked-advisory-plan.html"); if (path is null) return; string E(object? x) => WebUtility.HtmlEncode(x?.ToString() ?? "");
        RefreshTimeSensitiveState(force: true);
        string body = "<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'\"><title>NetStucked advisory and upgrade review</title><style>body{font:14px system-ui;margin:2em}table{border-collapse:collapse;width:100%}td,th{padding:8px;border:1px solid #999;text-align:left;white-space:pre-wrap}</style><h1>Advisory review and device upgrade plans</h1><p>Generated UTC " + E(DateTimeOffset.UtcNow.ToString("O")) + ". Versioned schema 1. Engineer review; no device execution. No universal safe-release claim.</p><h2>Selected advisory</h2><pre>" + E(AdvisoryDetails) + "</pre><table><tr><th>Asset</th><th>Version</th><th>Assessment</th><th>Reason</th></tr>" + string.Concat(Assessments.Select(a => $"<tr><td>{E(a.Asset.Hostname)}</td><td>{E(a.Version)}</td><td>{E(a.State)}</td><td>{E(a.Reason)}</td></tr>")) + "</table><h2>Plans</h2><table><tr><th>Device / CVE</th><th>Target / state</th><th>Change / wave / window</th><th>Engineer</th><th>Official evidence</th></tr>" + string.Concat(Plans.Select(x => $"<tr><td>{E(x.Device)} / {E(x.Plan.Cve)}</td><td>{E(x.Plan.TargetVersion)} / {E(x.State)}</td><td>{E(x.Plan.ChangeId)} / {E(x.Plan.Wave)} / {E(x.Plan.WindowUtc)}</td><td>{E(x.Plan.ApprovedBy)}</td><td>{E(x.Plan.FixedEvidenceUrl)}<br>{E(x.Plan.CompatibilityEvidenceUrl)}</td></tr>")) + "</table></html>";
        await ReportAsync(path, body, token);
    });
    [RelayCommand] private Task ExportWorkbook() => Run(async token =>
    {
        string? path = Files.Save("xlsx", "NetStucked-device-upgrade-plans.xlsx"); if (path is null) return;
        RefreshTimeSensitiveState(force: true);
        await Infrastructure.InventoryWorkbook.WriteTableAsync(path, "NetStucked Upgrade Plan report schema 1 · engineer review only · no device execution", ["PlanId", "AssetId", "Device", "CVE", "Target", "Status", "Fixed source", "Compatibility source", "Owner", "ChangeId", "Wave", "Window UTC", "Approved by", "Approved UTC", "Before evidence", "After evidence", "Notes"], Plans.Select(x => { var p = x.Plan; return new[] { p.Id.ToString(), p.AssetId.ToString(), x.Device, p.Cve, p.TargetVersion, x.State, p.FixedEvidenceUrl, p.CompatibilityEvidenceUrl, p.Owner, p.ChangeId, p.Wave, p.WindowUtc, p.ApprovedBy, p.ApprovedAt?.ToString("O") ?? "", p.EvidenceId.ToString(), p.AfterEvidenceId?.ToString() ?? "", p.Notes }; }).ToArray(), token);
    });
}
