using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using NetStucked.Core;
using NetStucked.Desktop;
using NetStucked.Desktop.Behaviors;
using NetStucked.Desktop.Services;
using NetStucked.Desktop.ViewModels;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;
internal static partial class Program
{
    private static async Task<int> CheckAdvisoryMetadataAsync(string output, string cve)
    {
        Directory.CreateDirectory(output);
        try
        {
            using var lookup = new AdvisoryLookup(); var record = await lookup.FetchAsync(cve, default);
            await File.WriteAllTextAsync(Path.Combine(output, "advisory.json"), JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
            Require(record.Cve == cve && record.Source == "https://nvd.nist.gov/vuln/detail/" + cve && record.Description.Length > 0, "Actual public NVD metadata identity and description");
            Console.WriteLine($"METADATA {record.Cve} · {record.Severity} · {record.CiscoReferences.Length} Cisco references · KEV {record.Kev?.ToString() ?? "Unknown"}");
            return 0;
        }
        catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(output, "blocked.txt"), ex.GetType().Name + ": " + ex.Message); Console.WriteLine("BLOCKED public advisory metadata: " + ex.Message); return 1; }
    }
    private static async Task RunEngineeringOnlyAsync(Application app, string output, BindingErrors errors)
    {
        Directory.CreateDirectory(output); var files = new QaEngineeringFiles(output); var dialogs = new QaDialogs { ConfirmGeneral = true };
        var store = new UserSettingsStore(Path.Combine(output, "isolated-user-data")); var lookup = new QaAdvisoryLookup();
        await using var provider = App.ConfigureServices(store, dialogs, services => { services.AddSingleton<IEngineeringFiles>(files); services.AddSingleton<IAdvisoryLookup>(lookup); });
        provider.GetRequiredService<ThemeService>(); var vm = provider.GetRequiredService<MainViewModel>();
        try
        {
            var window = provider.GetRequiredService<MainWindow>(); app.MainWindow = window; window.DataContext = vm; window.ShowActivated = false; window.ShowInTaskbar = false; window.Opacity = 0; window.Show();
            await vm.Engineering.InitializeAsync(); Require(vm.Engineering.Loaded && vm.Engineering.Data.Assets.Length == 0, "Engineering workspace begins empty without fabricated assets");
            vm.NavigateCommand.Execute("MA Inventory"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var root = (DependencyObject)window.Content;
            var hostname = Descendants<TextBox>(root).Single(t => AutomationProperties.GetName(t) == "Asset hostname"); hostname.Text = "SW1-owned";
            vm.Inventory.Address = "192.0.2.1"; vm.Inventory.Model = "Manual model"; vm.Inventory.Site = "Owned fixture";
            var saveButton = Descendants<Button>(root).Single(b => b.IsVisible && Equals(b.Content, "Save"));
            Require(ReferenceEquals(saveButton.Command, vm.Inventory.SaveAssetCommand), "Actual Save button binds to inventory command");
            ((System.Windows.Automation.Provider.IInvokeProvider)new System.Windows.Automation.Peers.ButtonAutomationPeer(saveButton).GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)).Invoke();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await Until(() => !vm.Engineering.IsBusy); Require(vm.Inventory.Assets.Count == 1 && vm.Inventory.Assets[0].Asset.Hostname == "SW1-owned", "Actual editor binding and Save button persist stable asset identity: " + vm.Engineering.Message);
            Guid asset = vm.Inventory.Assets[0].Asset.Id; Require(vm.Engineering.Data.Changes?.Length == 1, "Manual edits create a separate before/after audit record");
            vm.Inventory.SelectedAsset = vm.Inventory.Assets[0];
            hostname.Text = "SW1-owned-unsaved"; vm.Inventory.Site = "Dirty site ไทย";
            vm.Inventory.Search = "SW1"; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Require(vm.Inventory.Hostname == "SW1-owned-unsaved" && vm.Inventory.Site == "Dirty site ไทย", "Actual WPF grid refresh preserves unsaved inventory editor");
            vm.Inventory.Search = "no-matching-owned-asset"; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            vm.Inventory.Search = ""; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Require(vm.Inventory.AssetId == asset.ToString() && vm.Inventory.Hostname == "SW1-owned-unsaved", "Filtering asset out and back preserves stable editing identity and dirty fields");
            await vm.Engineering.CommitAsync(data => data, default); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Require(vm.Inventory.Site == "Dirty site ไทย" && vm.Inventory.Hostname == "SW1-owned-unsaved", "Unrelated saved workspace revision does not replace active inventory edits");
            vm.Inventory.Hostname = "SW1-owned"; vm.Inventory.Site = "Owned fixture";
            files.OpenPath = await WriteOwnedEvidenceAsync(output, "sw1", "SW1-owned", "SW2-owned", "Gi1/0/1", "Gi1/0/2", "17.9.4a");
            await vm.Inventory.ImportEvidenceCommand.ExecuteAsync(null);
            Require(vm.Engineering.Data.Evidence.Length == 1 && vm.Inventory.Assets[0].ObservedVersion == "17.9.4a" && vm.Inventory.Assets[0].Asset.Model == "Manual model", "Collector hash-verified observed facts remain separate from human inventory: " + vm.Engineering.Message);
            await vm.Inventory.ExportWorkbookCommand.ExecuteAsync(null); string workbook = files.LastSaved!; Require(File.Exists(workbook), "Inventory writes actual versioned XLSX");
            files.OpenPath = workbook; await vm.Inventory.ImportPreviewCommand.ExecuteAsync(null);
            Require(vm.Inventory.Preview.Count == 1 && vm.Inventory.Preview.All(p => !p.Selected) && vm.Engineering.Data.Assets.Length == 1, "Excel import preview does not modify assets or preselect updates");
            vm.Inventory.SelectValidCommand.Execute(null); await vm.Inventory.ApplyImportCommand.ExecuteAsync(null); Require(vm.Inventory.Preview.Count == 0 && vm.Engineering.Data.Assets.Single().Id == asset, "Explicit selected import applies atomically and retains stable AssetId: " + vm.Engineering.Message);
            vm.Inventory.NewAssetCommand.Execute(null); vm.Inventory.Hostname = "SW2-owned"; vm.Inventory.Address = "192.0.2.2"; await vm.Inventory.SaveAssetCommand.ExecuteAsync(null);
            files.OpenPath = await WriteOwnedEvidenceAsync(output, "sw2", "SW2-owned", "SW1-owned", "Gi1/0/2", "Gi1/0/1", "17.9.4a"); await vm.Inventory.ImportEvidenceCommand.ExecuteAsync(null);
            Require(vm.Engineering.Data.Assets.Length == 2 && vm.Engineering.Data.Evidence.Length == 2, "Second owned fixture reconciles to a separate asset");
            vm.NavigateCommand.Execute("Topology Explorer"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            vm.Topology.SelectedDevice = vm.Topology.Devices.First(e => e.AssetId == asset); vm.Topology.AnalyzeCommand.Execute(null);
            Require(vm.Topology.Links.Single().Classification.StartsWith("Verified"), "One-hop projection requires reciprocal matching interface evidence");
            var graphButton = Descendants<Button>(root).Single(b => AutomationProperties.GetName(b).StartsWith("Inspect neighbor "));
            graphButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Require(vm.Topology.LinkDetails.Contains("SHA-256") && vm.Topology.LinkDetails.Contains("Gi1/0/1"), "Actual graph node selection exposes interface and source/hash inspector");
            vm.NavigateCommand.Execute("CVE & Upgrade Planner"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            vm.Planner.Cve = "CVE-2023-20198"; await vm.Planner.FetchCommand.ExecuteAsync(null); vm.Planner.SelectedAsset = vm.Planner.Assets.First(a => a.Id == asset);
            Require(lookup.Requests == 1 && vm.Planner.Assessments.All(a => a.State == "Needs Review"), "Metadata fetch alone never classifies version-matching assets as affected");
            vm.Planner.Decision = Applicability.Affected; vm.Planner.ProductEvidence = "Owned fixture product/version review"; vm.Planner.ConfigurationEvidence = "Owned fixture feature present"; vm.Planner.Engineer = "Owned QA engineer"; vm.Planner.Reason = "Owned fixture only, not a production CVE claim"; vm.Planner.VendorUrl = "https://www.cisco.com/c/en/us/support/docs/owned-fixture";
            await vm.Planner.SaveReviewCommand.ExecuteAsync(null); Require(vm.Planner.Assessments.Single(a => a.Asset.Id == asset).State.StartsWith("Affected"), "Explicit source/config/engineer review records scoped applicability: " + vm.Engineering.Message);
            vm.Planner.NewPlanCommand.Execute(null); vm.Planner.TargetVersion = "17.12.4"; vm.Planner.Owner = "Owned QA owner"; vm.Planner.ChangeId = "LAB-CHG1"; vm.Planner.Wave = "Lab wave 1"; vm.Planner.WindowUtc = "2026-11-01T01:00:00Z";
            await vm.Planner.ApprovePlanCommand.ExecuteAsync(null); Require(vm.Engineering.Data.Plans.Length == 0, "Plan cannot be approved without fixed and compatibility evidence");
            vm.Planner.FixedUrl = vm.Planner.CompatibilityUrl = "https://www.cisco.com/c/en/us/support/docs/owned-fixture";
            await vm.Planner.ApprovePlanCommand.ExecuteAsync(null); Require(vm.Planner.Plans.Single().State == "Engineer approved plan", "Explicit engineer approval saves evidence-linked plan without issuing device commands: " + vm.Engineering.Message);
            await vm.Planner.ExportCsvCommand.ExecuteAsync(null); Require(File.ReadAllText(files.LastSaved!).Contains("LAB-CHG1"), "Plan CSV exports actual recorded change metadata");
            await vm.Planner.ExportHtmlCommand.ExecuteAsync(null); Require(File.ReadAllText(files.LastSaved!).Contains("Content-Security-Policy"), "HTML report is escaped and contains no executable external resources");
            await vm.Planner.ExportWorkbookCommand.ExecuteAsync(null); Require(File.Exists(files.LastSaved!) && new FileInfo(files.LastSaved!).Length > 1000, "Device plan Excel report uses actual versioned workbook content");
            vm.Planner.Notes = "Edited after approval"; await vm.Planner.SaveDraftCommand.ExecuteAsync(null); Require(vm.Planner.Plans.Single().Plan.ApprovedAt is null, "Saving edited plan clears approval and returns it to draft");
            vm.Inventory.SelectedAsset = vm.Inventory.Assets.First(a => a.Asset.Id == asset);
            files.OpenPath = await WriteOwnedEvidenceAsync(output, "sw1-after", "SW1-owned", "SW2-owned", "Gi1/0/1", "Gi1/0/2", "17.12.4"); await vm.Inventory.ImportEvidenceCommand.ExecuteAsync(null);
            Require(vm.Planner.Assessments.Single(a => a.Asset.Id == asset).State == "Stale Assessment", "New observation invalidates earlier applicability review");
            vm.Planner.SelectedPlan = vm.Planner.Plans.Single(); await vm.Planner.CompareAfterCommand.ExecuteAsync(null);
            Require(vm.Planner.Plans.Single().State.Contains("target observed; reassess CVE"), "Post-change comparison checks observed target and still requires reassessment");
            vm.Topology.SelectedDevice = vm.Topology.Devices.Single(e => e.AssetId == asset); await vm.Topology.ComparePreviousCommand.ExecuteAsync(null);
            Require(vm.Topology.ConfigurationDiff.Contains("17.9.4a") && vm.Topology.ConfigurationDiff.Contains("17.12.4"), "Hash-verified previous/current capture comparison exposes actual changed text");
            foreach (string theme in new[] { "Light", "Dark" })
            {
                vm.Appearance.SelectedTheme = theme;
                foreach (string page in new[] { "Dashboard", "MA Inventory", "Topology Explorer", "CVE & Upgrade Planner" })
                {
                    vm.NavigateCommand.Execute(page); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    Require(vm.CurrentPage is not PlaceholderViewModel, theme + " actual functional navigation: " + page);
                    Render(window, output, page.Replace(" ", "").Replace("&", "") + "-" + theme, 1536, 1024, 1);
                    Render(window, output, page.Replace(" ", "").Replace("&", "") + "-narrow-" + theme, 1100, 700, 1);
                    foreach (var grid in Descendants<DataGrid>(root).Where(g => g.IsVisible))
                    { ResultZoom.Adjust(grid, 1); Require(ResultZoom.GetFactor(grid) == 1.1, page + " result grid supports Ctrl/wheel zoom"); ResultZoom.Adjust(grid, 0); }
                    var tabs = Descendants<TabControl>(root).FirstOrDefault(t => t.IsVisible);
                    if (tabs is not null)
                    {
                        for (int i = 1; i < tabs.Items.Count; i++) { tabs.SelectedIndex = i; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); Render(window, output, page.Replace(" ", "").Replace("&", "") + "-tab" + i + "-narrow-" + theme, 1100, 700, 1); }
                        tabs.SelectedIndex = 0;
                    }
                }
            }
            vm.Dashboard.Update(true); Require(vm.Dashboard.Summary.Contains("2 assets") && vm.Dashboard.Incidents.Count == 0, "Dashboard uses actual inventory and no fabricated incidents");
            vm.Dashboard.OpenCommand.Execute("MA Inventory"); Require(vm.CurrentPage == vm.Inventory, "Dashboard drill-down opens cached functional menu");
            var reloaded = await provider.GetRequiredService<IEngineeringStore>().LoadAsync(default); Require(reloaded.Assets.Length == 2 && reloaded.Evidence.Length == 3 && reloaded.Plans.Length == 1, "Real local persistence round trip retains assets, evidence and plans");
            var legacy = EngineeringRules.Latest(vm.Engineering.Data, asset)! with { Parser = "cisco-text/1" };
            await vm.Engineering.CommitAsync(data => data with { Evidence = data.Evidence.Select(e => e.Id == legacy.Id ? legacy : e).ToArray() }, default);
            vm.Topology.SelectedDevice = vm.Topology.Devices.Single(e => e.AssetId == asset);
            await vm.Topology.ReanalyzeSourceCommand.ExecuteAsync(null);
            var reparsed = EngineeringRules.Latest(vm.Engineering.Data, asset)!;
            Require(reparsed.Id != legacy.Id && reparsed.Parser == CiscoEvidenceParser.Version && reparsed.Sha256 == legacy.Sha256 && vm.Engineering.Data.Evidence.Contains(legacy), "Explicit WPF reanalysis appends corrected parser facts while retaining source hash and original observation");
            Require(errors.Messages.Count == 0, "Engineering actual WPF bindings: " + string.Join(" | ", errors.Messages.Take(5)));
            await File.WriteAllTextAsync(Path.Combine(output, "result.txt"), "PASS · owned fixtures only; WPF workflow/theme/geometry renders, not physical DPI or human accessibility acceptance.");
        }
        finally { await vm.ShutdownAsync(); }
    }
    private static async Task<string> WriteOwnedEvidenceAsync(string output, string name, string host, string neighbor, string local, string remote, string version)
    {
        string text = $"--- show version ---\nCisco IOS XE Software, Version {version}\n{host} uptime is 1 day\nModel Number : Owned-fixture-model\n--- show cdp neighbors detail ---\nDevice ID: {neighbor}\nInterface: {local}, Port ID (outgoing port): {remote}\n";
        var json = new { schema = 1, result = new { Target = new { Host = host }, State = "Completed", Finished = DateTimeOffset.UtcNow, Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))) }, outputChunks = new[] { text } };
        string path = Path.Combine(output, name + ".json"); await File.WriteAllTextAsync(path, JsonSerializer.Serialize(json)); return path;
    }
    private sealed class QaEngineeringFiles(string output) : IEngineeringFiles
    {
        public string? OpenPath { get; set; }
        public string? LastSaved { get; private set; }
        public string? Open(string kind) => OpenPath;
        public string? Save(string kind, string name) => LastSaved = Path.Combine(output, name);
    }
    private sealed class QaAdvisoryLookup : IAdvisoryLookup
    {
        public int Requests { get; private set; }
        public Task<Advisory> FetchAsync(string cve, CancellationToken token) { Requests++; return Task.FromResult(new Advisory(cve, "Owned fixture", "HIGH", "Owned metadata fixture; no production classification", "https://nvd.nist.gov/vuln/detail/" + cve, "owned-1", DateTimeOffset.UtcNow, ["https://www.cisco.com/c/en/us/support/docs/owned-fixture"], null, "Owned fixture: unknown")); }
    }
}
