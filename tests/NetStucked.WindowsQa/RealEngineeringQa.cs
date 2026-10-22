using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using NetStucked.Core;
using NetStucked.Desktop;
using NetStucked.Desktop.Services;
using NetStucked.Desktop.ViewModels;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;

internal static partial class Program
{
    // Real WPF consumers of production SSH capture files; the SSH endpoints are owned software fixtures.
    // File dialogs/general confirmations are adapted for repeatable QA; parser, store and metadata provider are production.
    private static async Task RunRealEngineeringOnlyAsync(Application app, string output, string manifest, BindingErrors errors)
    {
        Directory.CreateDirectory(output);
        using var captures = JsonDocument.Parse(await File.ReadAllTextAsync(manifest));
        var entries = captures.RootElement.EnumerateArray().Select(e => new { Path = Path.GetFullPath(e.GetProperty("JsonPath").GetString()!), Label = e.GetProperty("Description").GetString()!.Split(' ')[0] }).ToArray();
        Require(entries.Length == 2 && entries.Select(e => e.Label).Order().SequenceEqual(new[] { "LAB-SW-A", "LAB-SW-B" }) && entries.All(e => File.Exists(e.Path)), "Consumer QA receives the two actual completed owned SSH capture paths");
        var files = new QaEngineeringFiles(output); var dialogs = new QaDialogs { ConfirmGeneral = true };
        var settings = new UserSettingsStore(Path.Combine(output, "isolated-user-data"));
        await using var provider = App.ConfigureServices(settings, dialogs, services => services.AddSingleton<IEngineeringFiles>(files));
        Require(provider.GetRequiredService<IEngineeringStore>().GetType() == typeof(EngineeringStore) && provider.GetRequiredService<IAdvisoryLookup>().GetType() == typeof(AdvisoryLookup), "Real engineering consumers use production store and public NVD/CISA adapter");
        provider.GetRequiredService<ThemeService>(); var vm = provider.GetRequiredService<MainViewModel>();
        try
        {
            var window = provider.GetRequiredService<MainWindow>(); app.MainWindow = window; window.DataContext = vm; window.ShowInTaskbar = true; window.ShowActivated = false; window.Show();
            await vm.Engineering.InitializeAsync(); Require(vm.Engineering.Loaded && vm.Engineering.Data.Assets.Length == 0, "Real consumer profile starts empty and isolated");
            vm.NavigateCommand.Execute("Inventory"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var root = (DependencyObject)window.Content;
            void Invoke(ICommand command)
            {
                var button = Descendants<Button>(root).Single(b => b.IsVisible && ReferenceEquals(b.Command, command));
                ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            }
            foreach (var entry in entries)
            {
                Invoke(vm.Inventory.NewAssetCommand); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Descendants<TextBox>(root).Single(t => AutomationProperties.GetName(t) == "Asset hostname").Text = entry.Label;
                Descendants<TextBox>(root).Single(t => AutomationProperties.GetName(t) == "Asset address").Text = "127.0.0.1";
                vm.Inventory.Site = "Owned software SSH fixture; no real Cisco hardware"; vm.Inventory.Role = "Owned acceptance QA";
                Invoke(vm.Inventory.SaveAssetCommand); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); await Until(() => !vm.Engineering.IsBusy);
                var asset = vm.Engineering.Data.Assets.Single(a => a.Hostname == entry.Label); vm.Inventory.SelectedAsset = vm.Inventory.Assets.Single(a => a.Asset.Id == asset.Id);
                files.OpenPath = entry.Path; await vm.Inventory.ImportEvidenceCommand.ExecuteAsync(null);
                var evidence = EngineeringRules.Latest(vm.Engineering.Data, asset.Id)!;
                Require(evidence is not null && evidence.Complete && evidence.Facts.Any(f => f.Field == "Hostname" && f.Value == entry.Label) && evidence.Facts.Any(f => f.Field == "Version") && evidence.Neighbors.Length == 2, "Production importer attaches actual completed " + entry.Label + " capture with source-line facts and CDP/LLDP");
                string sourceHash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(entry.Path)));
                Require(evidence!.Sha256 == sourceHash && (await EvidenceFile.ReadVerifiedTextAsync(evidence, default)).Contains("Owned software fixture; no real Cisco hardware"), "Production source hash and verified source text match actual " + entry.Label + " Collector JSON");
            }
            await vm.Inventory.ExportWorkbookCommand.ExecuteAsync(null); string inventoryWorkbook = files.LastSaved!;
            files.OpenPath = inventoryWorkbook; await vm.Inventory.ImportPreviewCommand.ExecuteAsync(null);
            Require(vm.Inventory.Preview.Count == 2 && vm.Inventory.Preview.All(r => !r.Selected && r.Data.Error.Length == 0) && vm.Engineering.Data.Assets.Length == 2, "Actual exported inventory workbook previews stable IDs without applying changes");
            await vm.Inventory.ExportObservationsCommand.ExecuteAsync(null); Require((await File.ReadAllTextAsync(files.LastSaved!)).Contains("show version"), "Actual observed CSV exports source command, line and capture provenance");
            await vm.Inventory.ExportAuditCommand.ExecuteAsync(null); Require((await File.ReadAllTextAsync(files.LastSaved!)).Contains("LAB-SW-A"), "Actual manual asset edits appear in separate before/after audit CSV");

            vm.NavigateCommand.Execute("Topology Explorer"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            vm.Topology.SelectedDevice = vm.Topology.Devices.Single(e => e.AssetId == vm.Engineering.Data.Assets.Single(a => a.Hostname == "LAB-SW-A").Id);
            vm.Topology.AnalyzeCommand.Execute(null); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Require(vm.Topology.Links.Count == 2 && vm.Topology.Links.All(l => l.Classification.StartsWith("Needs review")), "Actual captured topology honestly requires identity review: loopback collector endpoint differs from reported neighbor management address");
            var graphButton = Descendants<Button>(root).First(b => AutomationProperties.GetName(b).StartsWith("Inspect neighbor "));
            graphButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(vm.Topology.LinkDetails.Contains("SHA-256") && vm.Topology.LinkDetails.Contains("line ") && vm.Topology.LinkDetails.Contains("review identity"), "Actual topology graph inspection exposes captured interface/source/hash and identity limitation");
            await vm.Topology.ExportCommand.ExecuteAsync(null); Require((await File.ReadAllTextAsync(files.LastSaved!)).Contains("Needs review"), "Actual topology CSV retains source-backed identity review classification");

            vm.NavigateCommand.Execute("CVE & Upgrade Planner"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            vm.Planner.Cve = "CVE-2023-20198"; await vm.Planner.FetchCommand.ExecuteAsync(null);
            string publicStatus = "PASS"; string? blockedMessage = null; string metadataDisplay = "Fresh public metadata";
            if (vm.Engineering.Data.Advisories.Length == 0)
            {
                blockedMessage = vm.Engineering.Message; publicStatus = "BLOCKED";
                await File.WriteAllTextAsync(Path.Combine(output, "fresh-public-fetch-blocked.txt"), blockedMessage);
                // Explicitly authorized fallback: an actual public response collected in this same acceptance session.
                string cache = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifest)!, "..", "..", "cve-qa", "public-metadata", "advisory.json"));
                var earlier = JsonSerializer.Deserialize<Advisory>(await File.ReadAllTextAsync(cache)) ?? throw new IOException("Earlier actual metadata snapshot is invalid.");
                Require(earlier.Cve == "CVE-2023-20198" && earlier.Source == "https://nvd.nist.gov/vuln/detail/CVE-2023-20198" && DateTimeOffset.UtcNow - earlier.RetrievedAt < TimeSpan.FromDays(1), "Fallback is the verified current-turn actual public NVD response, not a fixture/provider-success claim");
                await vm.Engineering.CommitAsync(d => d with { Advisories = [earlier] }, default);
                metadataDisplay = "Cached actual public response retrieved " + earlier.RetrievedAt.ToString("O");
                vm.Engineering.Message = blockedMessage + " Displaying cached actual public metadata from " + earlier.RetrievedAt.ToString("u") + "; no applicability verdict.";
            }
            var advisory = vm.Engineering.Data.Advisories.Single();
            await File.WriteAllTextAsync(Path.Combine(output, "actual-public-advisory-displayed.json"), JsonSerializer.Serialize(advisory, new JsonSerializerOptions { WriteIndented = true }));
            Require(vm.Planner.Assessments.Count == 2 && vm.Planner.Assessments.All(a => a.State == "Needs Review") && vm.Engineering.Data.Reviews.Length == 0 && vm.Engineering.Data.Plans.Length == 0, "Actual public CVE metadata leaves both captured assets Needs Review and creates no affected/not-affected verdict or upgrade plan");
            await vm.Planner.ExportHtmlCommand.ExecuteAsync(null); Require((await File.ReadAllTextAsync(files.LastSaved!)).Contains("Needs Review"), "Actual advisory HTML exports metadata and honest Needs Review assessments");
            await vm.Planner.ExportCsvCommand.ExecuteAsync(null); Require(File.Exists(files.LastSaved!), "Plan CSV exports actual zero-plan workspace without inventing a plan");
            await vm.Planner.ExportWorkbookCommand.ExecuteAsync(null); Require(File.Exists(files.LastSaved!), "Plan Excel exports actual zero-plan workspace without inventing a plan");

            foreach (string theme in new[] { "Light", "Dark" })
            {
                vm.Appearance.SelectedTheme = theme;
                foreach (string page in new[] { "Inventory", "Topology Explorer", "CVE & Upgrade Planner" })
                {
                    vm.NavigateCommand.Execute(page); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    Render(window, output, page.Replace(" ", "").Replace("&", "") + "-real-capture-" + theme.ToLowerInvariant(), 1536, 1024, 1);
                }
            }
            vm.Appearance.SelectedTheme = "Dark"; vm.NavigateCommand.Execute("CVE & Upgrade Planner");
            Require(errors.Messages.Count == 0, "Real engineering consumer WPF bindings have no errors: " + string.Join("; ", errors.Messages.Take(5)));
            await File.WriteAllTextAsync(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { consumerStatus = "PASS", freshPublicMetadata = publicStatus, blockedMessage, metadataDisplay, captures = entries.Select(e => e.Path), assets = 2, reviews = 0, plans = 0, scope = "Actual WPF/production parser/store/public adapter; real SSH capture from two owned software fixtures; no real Cisco hardware or automatic vulnerability/physical-link verdict; renders are not physical DPI acceptance", generatedUtc = DateTimeOffset.UtcNow }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { await vm.ShutdownAsync(); }
    }
}
