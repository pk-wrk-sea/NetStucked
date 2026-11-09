using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
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
    private static int RunIpsCoverageQa(string[] args)
    {
        string output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        if (args.Length > 3)
        {
            foreach (var assembly in new[] { typeof(CoverageText).Assembly, typeof(CoverageImport).Assembly, typeof(App).Assembly })
            {
                string expected = Path.Combine(Path.GetFullPath(args[3]), Path.GetFileName(assembly.Location));
                Require(SHA256.HashData(File.ReadAllBytes(assembly.Location)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(expected))), "Exact review payload assembly: " + Path.GetFileName(expected));
            }
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/NetStucked;component/Resources/Theme.xaml") });
        var errors = new BindingErrors(); System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(errors); System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Error;
        int exit = 0;
        _ = app.Dispatcher.InvokeAsync(async () =>
        {
            try { await CheckIpsUiAsync(app, output, args.Length > 2 ? args[2] : null, errors); }
            catch (Exception e) { exit = 1; Console.Error.WriteLine("FAIL " + e); await File.WriteAllTextAsync(Path.Combine(output, "failure.txt"), e.ToString()); }
            finally { app.Shutdown(); }
        });
        app.Run(); return exit;
    }
    private sealed class IpsFiles(string directory) : IEngineeringFiles
    {
        public string? Saved;
        public string? Open(string kind) => null;
        public string? Save(string kind, string name) => Saved = Path.Combine(directory, name);
    }
    private sealed class IpsFixtureFactory : IIpsProviderFactory
    {
        public int Calls;
        public bool Fail, Slow;
        public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IIpsCoverageProvider PaloAlto(string key) => new IpsFixtureProvider(this);
        private sealed class IpsFixtureProvider(IpsFixtureFactory owner) : IIpsCoverageProvider
        {
            public IpsVendor Vendor => IpsVendor.PaloAlto;
            public string CacheIdentity => "owned-fixture-pa-v1";
            public async Task<IpsCoverageResult> LookupAsync(string cve, CancellationToken token)
            {
                int number = ++owner.Calls; owner.Entered.TrySetResult(); if (owner.Slow) await Task.Delay(Timeout.Infinite, token);
                var state = owner.Fail ? IpsCoverageState.Unknown : number % 11 == 0 ? IpsCoverageState.Unknown : number % 7 == 0 ? IpsCoverageState.Pending : number % 5 == 0 ? IpsCoverageState.NotFoundInSource : IpsCoverageState.Found;
                return new(cve, Vendor, state, "OWNED FIXTURE — simulated Threat Vault", "Test adapter (not live API)", CacheIdentity, DateTimeOffset.UtcNow, null, "Owned QA fixture. Simulated catalog state only; no protection/applicability claim.", state == IpsCoverageState.Found ? [new("FIXTURE-IPS-A", "Owned IPS fixture A", "Vulnerability Protection", "released (fixture)", "fixture", "fixture", "Simulated data only"), new("FIXTURE-IPS-B", "Owned IPS fixture B", "Vulnerability Protection", "released (fixture)", "fixture", "fixture", "Simulated data only")] : [], Failure: state == IpsCoverageState.Unknown ? "Fixture timeout" : "");
            }
        }
    }
    private static async Task CheckIpsUiAsync(Application app, string output, string? sample, BindingErrors errors)
    {
        var files = new IpsFiles(output); var factory = new IpsFixtureFactory(); var vault = new QaSecretVault(); var store = new UserSettingsStore(Path.Combine(output, "isolated-profile"));
        Directory.CreateDirectory(store.DirectoryPath); string legacy = Path.Combine(store.DirectoryPath, "legacy-data-sentinel.json"); await File.WriteAllTextAsync(legacy, "{\"retained\":true}"); byte[] legacyBefore = await File.ReadAllBytesAsync(legacy);
        await using var provider = App.ConfigureServices(store, new QaDialogs(), services => { services.AddSingleton<IEngineeringFiles>(files); services.AddSingleton<ISecretVault>(vault); services.AddSingleton<IIpsProviderFactory>(factory); });
        provider.GetRequiredService<ThemeService>(); var main = provider.GetRequiredService<MainViewModel>(); var vm = main.Coverage; var window = provider.GetRequiredService<MainWindow>(); app.MainWindow = window; window.DataContext = main; window.ShowActivated = false; window.ShowInTaskbar = false; window.Opacity = 0; window.Show();
        try
        {
            Require(!provider.GetRequiredService<Lazy<MonitoringViewModel>>().IsValueCreated && !provider.GetRequiredService<Lazy<EngineeringContext>>().IsValueCreated, "Retired Monitoring/engineering workflows stay cold when application shell resolves");
            main.NavigateCommand.Execute("Dashboard"); Require(main.CurrentPage is PlaceholderViewModel && ((PlaceholderViewModel)main.CurrentPage).Message.Contains("Coming soon"), "Dashboard remains a Coming soon page");
            main.NavigateCommand.Execute("CVE Checker | IPS"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var root = (FrameworkElement)window.Content;
            var labels = Descendants<RadioButton>(root).Select(AutomationProperties.GetName).ToArray();
            Require(labels.Contains("CVE Checker | IPS") && !labels.Any(x => x is "Monitoring" or "Topology Explorer" or "Inventory" or "URL Checker" or "CVE & Upgrade Planner"), "Sidebar contains exact new menu and excludes all retired routes");
            foreach (var name in new[] { "Monitoring", "Topology Explorer", "Inventory", "URL Checker", "CVE & Upgrade Planner" }) { main.NavigateCommand.Execute(name); Require(ReferenceEquals(main.CurrentPage, vm), "Retired navigation is rejected: " + name); }
            if (sample is not null && File.Exists(sample))
            {
                byte[] before = SHA256.HashData(await File.ReadAllBytesAsync(sample)); await vm.ImportAsync(sample); await vm.ConfirmMappingCommand.ExecuteAsync(null);
                Require(vm.Input is { Rows.Length: 3, AssetCount: 3, PluginCount: 1, Cves.Length: 34, Associations.Length: 102 }, "Actual user sample read-only: 3 rows / 3 assets / 1 plugin / 34 unique CVE / 102 links");
                byte[] after = SHA256.HashData(await File.ReadAllBytesAsync(sample)); Require(before.SequenceEqual(after), "Actual user sample bytes remain unchanged");
                await File.WriteAllTextAsync(Path.Combine(output, "sample-counts.json"), JsonSerializer.Serialize(new { rows = vm.Input!.Rows.Length, assets = vm.Input.AssetCount, plugins = vm.Input.PluginCount, cves = vm.Input.Cves.Length, associations = vm.Input.Associations.Length, unchanged = true }));
            }
            string report = Path.Combine(output, "owned-va-fixture.csv"); string[] cves = Enumerable.Range(1, 34).Select(i => "CVE-2024-" + (49000 + i)).ToArray();
            await File.WriteAllTextAsync(report, "IP Address,Hostname,Plugin,Plugin Name,Severity,CVE,Remarks,Status tracking\n" + string.Join("\n", Enumerable.Range(1, 3).Select(i => $"192.0.2.{i},OWNED-ASSET-{i},212232,Owned VA fixture,High,\"{string.Join(";", cves)}\",ทดสอบข้อมูลจำลอง 😀,Open")));
            await vm.ImportAsync(report); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); Require(vm.Preview?.Count == 3 && factory.Calls == 0, "Import preview performs no provider/network lookup");
            Require(vm.CveColumn == 5 && vm.HostnameColumn == 1 && vm.AddressColumn == 0, $"Reimport auto-maps current headers (CVE={vm.CveColumn}, Hostname={vm.HostnameColumn}, IP={vm.AddressColumn})");
            vm.CveColumn = -1; await vm.ConfirmMappingCommand.ExecuteAsync(null); Require(vm.Input is null && vm.Message.Contains("Select a CVE"), "Missing CVE mapping prevents lookup without Plugin ID inference");
            vm.CveColumn = 5; await vm.ConfirmMappingCommand.ExecuteAsync(null); Require(vm.Input?.Cves.Length == 34, "Explicit header mapping enables the immutable input snapshot");
            await vm.CheckAsync(); Require(factory.Calls == 0 && vm.Run!.Results.All(r => r.State == IpsCoverageState.Unknown), "Unconfigured providers are Unknown and send no requests");
            string snapshot = Path.Combine(output, "owned-ips-snapshot.csv"); await File.WriteAllTextAsync(snapshot, "Protection,Industry Reference,Profile_Name\n" + string.Join("\n", cves.Take(16).Select((c, i) => $"OWNED protection {i},{c},Detect")));
            await vm.ImportSnapshotAsync(snapshot); vm.SnapshotPackage = "OWNED fixture — user supplied"; vm.SnapshotDate = DateTime.UtcNow.AddDays(-8).ToString("yyyy-MM-dd"); await vm.ApplySnapshotCommand.ExecuteAsync(null);
            Require(vm.SnapshotStatus.Contains("Protection ID not supplied"), "Check Point snapshot works without Protection ID and exposes limitation");
            vm.SaveApiKey("owned-fixture-key-never-a-real-key"); Require(vault.Read("threat-vault") is not null, "Explicit key save goes through the injected protected vault contract");
            await vm.CheckAsync(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Require(factory.Calls == 34 && vm.Run is { CompletedJobs: 68, TotalJobs: 68, Partial: false }, "One job per unique CVE per vendor: 34 PA / 34 local CP outcomes");
            Require(vm.Run!.Results.Count(r => r.Vendor == IpsVendor.CheckPoint && r.State == IpsCoverageState.Found) == 16 && vm.Run.Results.Where(r => r.Vendor == IpsVendor.CheckPoint).All(r => r.Stale), "Check Point source-scoped Found count and stale freshness retained");
            vm.Filter = "Unknown / not checked"; Require(vm.ResultRows.Length > 0 && vm.ResultRows.Length < 34, "Unknown filter retains unresolved CVE"); vm.Filter = "All CVE";
            vm.GroupBy = "By plugin"; Require(vm.GroupRows.Single().CveCount == 34 && vm.GroupRows.Single().PaloAlto.Contains("/34 Found"), "Plugin summary uses 34 unique CVE with partial coverage");
            vm.GroupBy = "By asset"; Require(vm.GroupRows.Length == 3 && vm.GroupRows.All(r => r.CveCount == 34), "All three assets link to the same unique lookup outcomes"); vm.GroupBy = "By CVE";
            vm.SelectedResult = vm.ResultRows.First(r => r.PaloAlto.State == IpsCoverageState.Found); Require(vm.Signatures.Count(s => s.Vendor == "Palo Alto") == 2 && vm.Evidence.Contains("2, 3, 4") && vm.SelectedSourceRows?.Count == 3, "Selection shows multiple signatures and all original raw source rows");
            foreach (string theme in new[] { "Light", "Dark" })
            {
                main.Appearance.SelectedTheme = theme; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                for (int tab = 0; tab < 4; tab++) { vm.SelectedTab = tab; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    if (tab == 0) { var hostname = Descendants<ComboBox>(root).Single(c => AutomationProperties.GetName(c) == "Hostname column mapping"); Require(hostname.SelectedItem is CoverageColumn column && column.Index == vm.HostnameColumn && hostname.SelectedIndex >= 0, "Visible hostname selector matches confirmed mapping after reimport/tab navigation"); }
                    Render(window, output, $"IPS-{theme}-tab{tab}", 1536, 1024, 1); Render(window, output, $"IPS-{theme}-tab{tab}-compact", 1100, 700, 1); }
            }
            vm.SelectedTab = 3; await vm.ExportCommand.ExecuteAsync(null); Require(files.Saved is not null && File.Exists(files.Saved), "WPF export command writes an actual new XLSX workbook");
            using (var zip = ZipFile.OpenRead(files.Saved!)) { using var read = new StreamReader(zip.GetEntry("xl/worksheets/sheet3.xml")!.Open()); string text = await read.ReadToEndAsync(); Require(text.Contains("Original.Remarks") && text.Contains("Original.Status tracking") && text.Contains("ทดสอบข้อมูลจำลอง"), "Actual exported Findings preserve source columns and Thai text"); }
            factory.Fail = true; await vm.CheckAsync(true); Require(vm.Run!.Results.Where(r => r.Vendor == IpsVendor.PaloAlto).Any(r => r.Previous is not null && r.State == IpsCoverageState.Unknown), "Failed refresh surfaces Unknown with original cached evidence retained");
            factory.Fail = false; factory.Slow = true; factory.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var running = vm.CheckAsync(true); await factory.Entered.Task; vm.CancelCommand.Execute(null); await running; Require(vm.Run!.Partial, "Cancel awaits owned provider work and marks the run PARTIAL");
            vm.AcknowledgePartial = false; await vm.ExportCommand.ExecuteAsync(null); Require(vm.Message.Contains("acknowledge"), "Partial export needs explicit acknowledgement");
            vm.AcknowledgePartial = true; await vm.ExportCommand.ExecuteAsync(null); Require(files.Saved?.Contains("PARTIAL") == true && File.Exists(files.Saved), "Explicitly acknowledged partial export creates a PARTIAL workbook");
            factory.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously); var old = vm.CheckAsync(true); await factory.Entered.Task; var importing = vm.ImportAsync(report); await Task.WhenAll(old, importing);
            Require(vm.Run is null && vm.Input is null && !vm.IsBusy, "Importing a new file cancels/drains prior run and rejects late progress");
            string invalid = Path.Combine(output, "invalid-workbook.xlsx"); await File.WriteAllTextAsync(invalid, "not a zip workbook"); await vm.ImportAsync(invalid); Require(!vm.IsBusy && vm.Message.StartsWith("Operation failed"), "Malformed workbook is reported without crashing the WPF operation");
            byte[] legacyAfter = await File.ReadAllBytesAsync(legacy); Require(legacyBefore.SequenceEqual(legacyAfter), "Retired local data remain unchanged");
            Require(!provider.GetRequiredService<Lazy<MonitoringViewModel>>().IsValueCreated, "Coverage/navigation never instantiate or resume Monitoring");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); Require(errors.Messages.Count == 0, "Actual WPF workflow has no data-binding errors: " + string.Join("; ", errors.Messages));
            await File.WriteAllTextAsync(Path.Combine(output, "ips-qa-result.json"), JsonSerializer.Serialize(new { status = "PASS", bindingErrors = errors.Messages.Count, liveApi = "NOT TESTED — no real API key", data = "Owned fixtures; sample counts read-only", physicalDpiAccessibility = "NOT TESTED" }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { await main.ShutdownAsync(); window.RequestExit(); }
    }
}
