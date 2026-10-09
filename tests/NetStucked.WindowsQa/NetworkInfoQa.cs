using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using NetStucked.Core;
using NetStucked.Infrastructure;
using NetStucked.Desktop.Behaviors;
using NetStucked.Desktop.ViewModels;

namespace NetStucked.WindowsQa;

internal static partial class Program
{
    private static async Task CheckNetworkInfoAsync(Window window, MainViewModel main, QaWifi wifi, string output)
    {
        main.NavigateCommand.Execute("Network Info"); var vm = main.Network;
        Require(ReferenceEquals(main.CurrentPage, vm), "Network Info is the approved cached Wi-Fi ViewModel");
        await Until(() => vm.Rows.Count == 30);
        Require(vm.Rows.Single(r => r.Name == "QA Customer 01").Status == "Connected", "Observed WLAN connection binds to the profile, not the command result");
        var grid = Descendants<DataGrid>(window).Single(g => AutomationProperties.GetName(g) == "Saved Wi-Fi Profiles");
        Require(grid.EnableRowVirtualization && grid.IsReadOnly, "Compact Wi-Fi table is virtualized and read-only");
        CheckSelectableTable(grid);
        Require(TableTextSupport.BuildText(grid).Contains("QA Customer 01"), "Wi-Fi profile table copy includes observed fixture values");
        vm.Search = "Customer 02"; Require(vm.Profiles.Cast<WifiProfileRow>().Count() == 1, "Profile search filters dozens of profiles"); vm.Search = "";
        vm.Profiles.SortDescriptions.Add(new("Name", System.ComponentModel.ListSortDirection.Ascending));
        vm.SelectedProfile = vm.Rows.Single(r => r.Name == "QA Customer 01");
        Render(window, output, "WifiProfiles-fixture-light", 1536, 1024, 1);
        Render(window, output, "WifiProfiles-fixture-narrow", 1280, 800, 1);
        Render(window, output, "WifiProfiles-fixture-150pct", 1280, 800, 1.5);
        main.Appearance.SelectedTheme = "Dark";
        Render(window, output, "WifiProfiles-fixture-dark", 1536, 1024, 1);
        main.Appearance.SelectedTheme = "Light";
        grid.Columns[0].Width = 180; ColumnLayout.Capture(grid);
        main.NavigateCommand.Execute("Live Ping"); main.NavigateCommand.Execute("Network Info");
        Require(ReferenceEquals(grid, Descendants<DataGrid>(window).Single(g => AutomationProperties.GetName(g) == "Saved Wi-Fi Profiles")), "Network Info navigation reuses the same table");
        Require(grid.Columns[0].Width.Value == 180, "Wi-Fi column configuration survives navigation");
        wifi.GateConnection = true;
        var connect = vm.ConnectCommand.ExecuteAsync(null);
        await Until(() => wifi.Active == 1);
        Require(vm.IsBusy && !vm.ScanCommand.CanExecute(null) && !vm.ConnectCommand.CanExecute(null), "Connection serializes UI mutation commands");
        // Complete queued theme/navigation/busy-state layout before timing a waiting operation.
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        int heartbeats = 0; var response = Stopwatch.StartNew(); double lastTick = 0, maxGap = 0;
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(20), DispatcherPriority.Normal, (_, _) => { double now = response.Elapsed.TotalMilliseconds; maxGap = Math.Max(maxGap, now - lastTick); lastTick = now; heartbeats++; }, Dispatcher.CurrentDispatcher); timer.Start();
        await Task.Delay(600); timer.Stop(); Console.WriteLine($"WIFI RESPONSE ticks={heartbeats} elapsedMs={response.Elapsed.TotalMilliseconds:0.0} maxGapMs={maxGap:0.0}");
        Require(heartbeats >= 5 && Math.Max(maxGap, response.Elapsed.TotalMilliseconds - lastTick) < 500, "Dispatcher remains responsive while connection waits");
        await vm.ScanCommand.ExecuteAsync(null); Require(wifi.Scans == 0, "Direct overlapping command execution is also rejected");
        vm.CancelCommand.Execute(null); await connect;
        Require(!vm.IsBusy && wifi.Active == 0 && wifi.MaximumActive == 1, "Cancel awaits outstanding connection and never overlaps operations");
        wifi.GateConnection = false;
        var profile = new ServiceTestProfile { Name = "Owned-loopback QA fixture", TestGateway = true, TimeoutMs = 1000 };
        vm.TestProfiles.Add(profile); vm.SelectedTestProfile = profile;
        vm.Store.Preferences.WifiTestProfiles = vm.TestProfiles.ToList();
        await vm.RunTestsCommand.ExecuteAsync(null);
        Require(vm.TestResults.Count == 1 && vm.TestResults[0].Status == "PASS" && vm.TestResults[0].LatencyMs is not null, "WPF Connect & Test renders actual source-bound loopback ICMP timing");
        Require(vm.Store.Preferences.WifiTestRuns.Count > 0, "Completed results persist with source adapter, SSID and timestamps");
        await vm.ExportTestsCommand.ExecuteAsync(null);
        Render(window, output, "WifiProfiles-fixture-measured-services", 1536, 1024, 1);
        foreach (var table in Descendants<DataGrid>(window).Where(g => g.IsVisible && g.Items.Count > 0)) CheckSelectableTable(table);
        wifi.CurrentSsid = "QA Transition fixture"; wifi.CurrentProfile = "QA Customer 02";
        await Until(() => vm.Events.Any(e => e.Event == "Network transition"));
        Require(main.HasNetworkTransition, "Network transitions remain visible in the main shell during diagnostics");
        Require(vm.Rows.Single(r => r.Name == "QA Customer 02").Status == "Connected", "External Wi-Fi transition refreshes profile state");
        vm.SelectedHistory = vm.Events.Last(e => e.Event == "Connect & Test");
        Require(vm.TestSummary.Contains("Recorded") && vm.TestResults.Count == 1, "History restores measured results with original network context");
        vm.SelectedHistory = null;
        await vm.StopForUpdateAsync();
        vm.SetPresentationActive(true); await Until(() => vm.Rows.Count == 30);
        wifi.NoAdapters = true;
        await Until(() => vm.Adapters.Count == 0);
        Require(!vm.ConnectCommand.CanExecute(null) && vm.Rows.Count == 0, "No hardware clears stale profiles and disables Wi-Fi mutations");
        Render(window, output, "WifiProfiles-no-adapter", 1280, 800, 1);
        wifi.NoAdapters = false; vm.SetPresentationActive(true); await Until(() => vm.Rows.Count == 30);
        var loaded = new UserSettingsStore(vm.Store.DirectoryPath);
        Require(loaded.LoadError is null && loaded.Preferences.WifiTestRuns.Count > 0, "Persisted Wi-Fi data reloads with schema-one compatibility");
        var transitions = new QaWifi(); var slow = new QaSlowTests();
        var transientStore = new UserSettingsStore(Path.Combine(output, "isolated-transition-test"));
        await using var changing = new NetworkInfoViewModel(transitions, new QaVault(), slow, new QaDialogs(), transientStore);
        changing.SetPresentationActive(true); await Until(() => changing.Rows.Count == 30);
        changing.TestProfiles.Add(profile); changing.SelectedTestProfile = profile;
        var testing = changing.RunTestsCommand.ExecuteAsync(null); await Until(() => slow.Active == 1);
        transitions.CurrentProfile = "QA Customer 02"; transitions.CurrentSsid = "QA test network changed"; changing.SetPresentationActive(true);
        await testing;
        Require(changing.TestResults.Count == 0 && transientStore.Preferences.WifiTestRuns.Count == 0 && slow.Active == 0, "Network transition cancels and drains in-flight service checks without publishing stale PASS/FAIL");
        Require(changing.TestSummary.Contains("invalidated"), "Invalidated results explain the network transition");
        transitions.NoIp = true; changing.SetPresentationActive(true); await Until(() => changing.Gateway == "—");
        changing.SelectedProfile = changing.Rows.Single(r => r.Name == "QA Customer 02");
        var pendingDhcp = changing.RunTestsCommand.ExecuteAsync(null); await Task.Delay(150); changing.CancelCommand.Execute(null); await pendingDhcp;
        Require(!changing.IsBusy && slow.Active == 0, "Cancellation during DHCP readiness produces no network checks");
        await changing.StopForUpdateAsync(); Require(slow.Active == 0 && !changing.IsBusy, "Update preparation drains Wi-Fi tasks before installer handoff");
        vm.SelectedProfile = vm.Rows.Single(r => r.Name == "QA Customer 02");
        wifi.GateConnection = true;
        var pendingConnect = vm.ConnectCommand.ExecuteAsync(null);
        await Until(() => wifi.Active == 1);
        await main.PrepareForUpdateAsync(); await pendingConnect;
        Require(wifi.Active == 0 && !vm.IsBusy, "Shared updater preparation awaits a pending Wi-Fi operation alongside all diagnostics");
        wifi.GateConnection = false;
    }
    private static async Task<int> ReadWifiNativeAsync(string output)
    {
        Directory.CreateDirectory(output);
        using var native = new WindowsWifiService();
        IReadOnlyList<WifiAdapter> adapters;
        try { adapters = await native.GetAdaptersAsync(CancellationToken.None); }
        catch (InvalidOperationException ex)
        {
            await File.WriteAllTextAsync(Path.Combine(output, "native-wifi-read.json"), System.Text.Json.JsonSerializer.Serialize(new { Time = DateTimeOffset.Now, Status = "BLOCKED", Reason = ex.Message, Mutations = 0, Connect = "NOT TESTED", Enterprise = "NOT TESTED" }));
            Console.WriteLine("NOT TESTED: native adapter enumeration blocked: " + ex.Message); return 2;
        }
        var observations = new List<object>();
        foreach (var adapter in adapters)
        {
            var snapshot = await native.ReadAsync(adapter.Id, CancellationToken.None);
            observations.Add(new { adapter.Id, adapter.Name, snapshot.Adapter.State, Profiles = snapshot.Profiles.Count, Networks = snapshot.Networks.Count, IPv4Index = snapshot.Ip.IPv4Index, IPv6Index = snapshot.Ip.IPv6Index, AddressCount = snapshot.Ip.Addresses.Count, GatewayCount = snapshot.Ip.Gateways.Count, DnsCount = snapshot.Ip.DnsServers.Count, snapshot.Notice });
        }
        await File.WriteAllTextAsync(Path.Combine(output, "native-wifi-read.json"), System.Text.Json.JsonSerializer.Serialize(new { Time = DateTimeOffset.Now, AdapterCount = adapters.Count, Observations = observations, Mutations = 0, Scan = "NOT EXECUTED", Connect = "NOT TESTED", Enterprise = "NOT TESTED" }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS: Native WLAN read-only API; {adapters.Count} adapters. No scan, connect, disconnect or profile mutation."); return 0;
    }
    private sealed class QaVault : IWifiCredentialVault
    {
        public EnterpriseCredentials? Read(Guid adapter, string name) => null;
        public void Write(Guid adapter, string name, EnterpriseCredentials credentials) { }
        public void Delete(Guid adapter, string name) { }
    }
    private sealed class QaWifi : IWifiService
    {
        public readonly Guid Id = Guid.NewGuid();
        public bool GateConnection, NoAdapters, NoIp;
        public int Active, MaximumActive, Scans;
        public string CurrentProfile = "QA Customer 01", CurrentSsid = "QA Customer 01";
        public Task<IReadOnlyList<WifiAdapter>> GetAdaptersAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<WifiAdapter>>(NoAdapters ? [] : [new(Id, "QA fixture • actual probes use owned loopback", "Connected")]);
        public Task<WifiSnapshot> ReadAsync(Guid adapter, CancellationToken token)
        {
            var loopback = NetworkInterface.GetAllNetworkInterfaces().First(n => n.GetIPProperties().UnicastAddresses.Any(a => a.Address.Equals(IPAddress.Loopback)));
            var ip = new AdapterIpConfiguration(loopback.GetIPProperties().GetIPv4Properties().Index, 0, [IPAddress.Loopback], [IPAddress.Loopback], []);
            if (NoIp) ip = AdapterIpConfiguration.Empty;
            var profiles = Enumerable.Range(1, 30).Select(i => { string name = $"QA Customer {i:00}"; return WifiProfileXml.Describe(WifiProfileXml.Personal(new(name, name, "WPA2-Personal", false, "", "test-only-password")), 2, i); }).ToArray();
            return Task.FromResult(new WifiSnapshot(DateTimeOffset.Now, new(Id, "QA fixture • actual probes use owned loopback", "Connected"), profiles, profiles.Select(p => new WifiNetwork(p.Ssid, p.SsidHex, p.Name, p.Security, 70, true)).ToArray(), CurrentProfile, CurrentSsid, 70, ip));
        }
        public Task ScanAsync(Guid adapter, CancellationToken token) { Scans++; return Task.CompletedTask; }
        public async Task ConnectAsync(Guid adapter, string name, CancellationToken token)
        {
            int active = Interlocked.Increment(ref Active); MaximumActive = Math.Max(MaximumActive, active);
            try { if (GateConnection) await Task.Delay(Timeout.Infinite, token); CurrentProfile = name; CurrentSsid = name; }
            finally { Interlocked.Decrement(ref Active); }
        }
        public Task DisconnectAsync(Guid adapter, CancellationToken token) => Task.CompletedTask;
        public Task SaveProfileAsync(Guid adapter, string xml, bool overwrite, CancellationToken token) => Task.CompletedTask;
        public Task DeleteProfileAsync(Guid adapter, string name, CancellationToken token) => Task.CompletedTask;
        public Task SetEnterpriseCredentialsAsync(Guid adapter, string name, EnterpriseCredentials credentials, CancellationToken token) => Task.CompletedTask;
    }
    private sealed class QaSlowTests : IWifiServiceTests
    {
        public int Active;
        public async Task<IReadOnlyList<ServiceTestResult>> RunAsync(WifiSnapshot context, ServiceTestProfile profile, CancellationToken token)
        {
            Interlocked.Increment(ref Active);
            try { await Task.Delay(Timeout.Infinite, token); return []; }
            finally { Interlocked.Decrement(ref Active); }
        }
    }
}
