using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using NetStucked.Core;
using NetStucked.Desktop.Services;
using NetStucked.Desktop.ViewModels;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;

internal static partial class Program
{
    // Headless actual ViewModel/store/export regression. No Window, target probes or provider calls.
    private static async Task<int> CheckCveRegressionAsync(string output)
    {
        Directory.CreateDirectory(output);
        try
        {
            var now = DateTimeOffset.UtcNow.AddDays(-10); var clock = new CveQaClock(now);
            var asset = new Asset(Guid.NewGuid(), "CVE-owned-QA", "192.0.2.99", "Cisco", "Owned fixture", "", "Owned QA only", "", "Switch", "", null, null, now);
            var evidence = CiscoEvidenceParser.Parse("--- show version ---\nCisco IOS XE Software, Version 17.9.4a\n", "Owned CVE regression fixture", new string('a', 64), asset.Hostname, now, true, asset.Id);
            var advisory = new Advisory("CVE-2023-20198", "OWNED QA METADATA FIXTURE", "HIGH", "Owned fixture only; never a real-device vulnerability determination", "https://nvd.nist.gov/vuln/detail/CVE-2023-20198", "owned-cve-regression-1", now, ["https://www.cisco.com/c/en/us/support/docs/owned-qa-fixture"], null, "Owned fixture: unknown");
            var store = new EngineeringStore(new UserSettingsStore(Path.Combine(output, "isolated-user-data")));
            var initial = await store.LoadAsync(default);
            Require(initial.Revision == 0, "CVE owned regression requires a fresh isolated workspace");
            await store.SaveAsync(initial with { Assets = [asset], Evidence = [evidence], Advisories = [advisory] }, initial.Revision, default);
            await using var workspace = new EngineeringContext(store);
            var files = new QaEngineeringFiles(output); var dialogs = new QaDialogs { ConfirmGeneral = true };
            var vm = new AdvisoryPlannerViewModel(workspace, files, dialogs, new QaAdvisoryLookup(), clock);
            await workspace.InitializeAsync();
            Require(vm.Assessments.Single().State == "Needs Review", "Owned fixture metadata/version alone never produces an applicability verdict");

            vm.Decision = Applicability.Affected; vm.ProductEvidence = "Owned QA product review"; vm.ConfigurationEvidence = "Owned QA feature review";
            vm.Engineer = "Owned QA engineer"; vm.Reason = "Owned QA assessment only; no production applicability claim"; vm.VendorUrl = advisory.CiscoReferences[0];
            await vm.SaveReviewCommand.ExecuteAsync(null);
            Require(vm.Assessments.Single().State == "Affected (engineer reviewed)", "Owned fixture explicit reviewed assessment is saved");
            vm.NewPlanCommand.Execute(null); vm.TargetVersion = "17.12.4-owned"; vm.Owner = "Owned QA owner"; vm.ChangeId = "OWNED-QA-CHANGE"; vm.Wave = "Owned QA wave"; vm.WindowUtc = "2026-11-01T01:00:00Z";
            await vm.ApprovePlanCommand.ExecuteAsync(null);
            Require(workspace.Data.Plans.Length == 0, "Approval rejects missing official fixed/compatibility evidence");
            vm.FixedUrl = vm.CompatibilityUrl = advisory.CiscoReferences[0];
            await vm.ApprovePlanCommand.ExecuteAsync(null);
            Require(vm.Plans.Single().State == "Engineer approved plan", "Owned fixture reviewed plan approval is recorded without executing any device operation");
            Guid planId = vm.Plans.Single().Plan.Id; vm.SelectedPlan = vm.Plans.Single();

            // Reproduce bound selectors clearing on collection Reset without creating a native window.
            vm.Assets.CollectionChanged += (_, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) vm.SelectedAsset = null; };
            vm.Advisories.CollectionChanged += (_, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) vm.SelectedAdvisory = null; };
            vm.Plans.CollectionChanged += (_, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) vm.SelectedPlan = null; };
            vm.TargetVersion = "unsaved target"; vm.Owner = "unsaved owner"; vm.Notes = "unsaved plan notes";
            vm.ProductEvidence = "unsaved product review"; vm.ConfigurationEvidence = "unsaved configuration review"; vm.Reason = "unsaved review reason"; vm.Engineer = "unsaved engineer";
            await workspace.CommitAsync(d => d with { Assets = d.Assets.Select(a => a.Id == asset.Id ? a with { Site = "Unrelated inventory edit", Hostname = "CVE-owned-QA-renamed" } : a).ToArray() }, default);
            Require(vm.SelectedAsset?.Id == asset.Id && vm.SelectedAdvisory?.Cve == advisory.Cve && vm.SelectedPlan?.Plan.Id == planId, "Workspace refresh restores selected asset/advisory/plan stable IDs after bound Reset");
            Require(vm.TargetVersion == "unsaved target" && vm.Owner == "unsaved owner" && vm.Notes == "unsaved plan notes" && vm.ProductEvidence == "unsaved product review" && vm.ConfigurationEvidence == "unsaved configuration review" && vm.Reason == "unsaved review reason" && vm.Engineer == "unsaved engineer", "Unrelated inventory writes preserve dirty review and plan editors");

            clock.UtcNow = now.AddDays(8); vm.RefreshTimeSensitiveState();
            Require(vm.Assessments.Single().State == "Stale Assessment" && vm.Plans.Single().State == "Needs review / changed evidence", "Manual clock aging invalidates seven-day advisory/review and plan display without workspace writes");
            Require(vm.SelectedPlan?.Plan.Id == planId && vm.TargetVersion == "unsaved target" && vm.Reason == "unsaved review reason", "Time-based status refresh preserves selection and dirty editors");
            long revision = workspace.Data.Revision; await vm.ApprovePlanCommand.ExecuteAsync(null);
            Require(workspace.Data.Revision == revision && workspace.Message.Contains("Review current advisory/device evidence"), "Stale review cannot approve a plan or mutate saved approval");

            foreach (string format in new[] { "csv", "html", "xlsx" })
            {
                clock.UtcNow = now; vm.RefreshTimeSensitiveState(force: true);
                Require(vm.Plans.Single().State == "Engineer approved plan", "Owned fixture is current before independent " + format + " export aging case");
                clock.UtcNow = now.AddDays(8);
                if (format == "csv") await vm.ExportCsvCommand.ExecuteAsync(null);
                else if (format == "html") await vm.ExportHtmlCommand.ExecuteAsync(null);
                else await vm.ExportWorkbookCommand.ExecuteAsync(null);
                string report;
                if (format == "xlsx")
                {
                    using var zip = ZipFile.OpenRead(files.LastSaved!); using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()); report = await reader.ReadToEndAsync();
                }
                else report = await File.ReadAllTextAsync(files.LastSaved!);
                Require(report.Contains("Needs review / changed evidence") && !report.Contains("Engineer approved plan"), "Export " + format + " recomputes expired approval state before serializing");
            }
            clock.UtcNow = now; vm.RefreshTimeSensitiveState(force: true);
            await vm.SaveDraftCommand.ExecuteAsync(null);
            Require(workspace.Data.Plans.Single().ApprovedAt is null && workspace.Data.Plans.Single().ApprovedBy.Length == 0 && workspace.Data.Plans.Single().TargetVersion == "unsaved target", "Saving an edited plan clears approval and keeps edited content");
            await File.WriteAllTextAsync(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { status = "PASS", scope = "Owned fixture headless actual CVE ViewModel/store/export regression; no device probes or public-provider claims", cases = 17, generatedUtc = DateTimeOffset.UtcNow }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(output, "failure.txt"), ex.ToString()); Console.Error.WriteLine("FAIL CVE regression " + ex); return 1; }
    }
    private sealed class CveQaClock(DateTimeOffset now) : TimeProvider
    { public DateTimeOffset UtcNow { get; set; } = now; public override DateTimeOffset GetUtcNow() => UtcNow; }
}
