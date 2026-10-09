using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using NetStucked.Core;
using NetStucked.Desktop;
using NetStucked.Desktop.Behaviors;
using NetStucked.Desktop.Services;
using NetStucked.Desktop.ViewModels;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;

internal static class Program
{
    private static int _exitCode;
    [STAThread]
    public static int Main(string[] args)
    {
        string output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/windows-qa");
        int soakSeconds = args.Length > 1 ? int.Parse(args[1]) : 0;
        if (args.Length > 2)
        {
            string published = Path.GetFullPath(args[2]);
            foreach (string file in new[] { "NetStucked.Core.dll", "NetStucked.Infrastructure.dll", "NetStucked.dll" })
            {
                string expected = Path.Combine(published, file);
                Assembly loaded = AssemblyLoadContext.Default.LoadFromAssemblyPath(expected);
                Require(SHA256.HashData(File.ReadAllBytes(loaded.Location)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(expected))), "QA uses exact published assembly bytes " + file);
            }
        }
        Directory.CreateDirectory(output);
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/NetStucked;component/Resources/Theme.xaml") });
        var listener = new BindingErrors();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        _ = application.Dispatcher.InvokeAsync(async () =>
        {
            try { await RunAsync(application, output, soakSeconds, listener); }
            catch (Exception ex) { _exitCode = 1; Console.Error.WriteLine("FAIL " + ex); }
            finally { application.Shutdown(); }
        });
        application.Run();
        return _exitCode;
    }

    private static async Task RunAsync(Application app, string output, int soakSeconds, BindingErrors errors)
    {
        var store = new UserSettingsStore(Path.Combine(output, "isolated-user-data"));
        // Only loopback addresses are probed. No stored user targets are loaded by this harness.
        store.Preferences.TargetText = ""; store.Preferences.TraceTarget = ""; store.Preferences.PortTargetText = "";
        store.Preferences.Port = new PortProbeSettings { IntervalMs = 250, TimeoutMs = 500 };
        store.Preferences.Ping = new ProbeSettings { IntervalMs = 250, TimeoutMs = 1000, Concurrency = 64 };
        store.Preferences.Trace = new TraceSettings { IntervalMs = 250, MaxHops = 5, TimeoutMs = 1000 };
        var dialogs = new QaDialogs();
        var measured = new MeasuredRealProbe();
        var measuredTcp = new MeasuredRealTcp();
        await using var provider = App.ConfigureServices(store, dialogs, services => { services.AddSingleton<IIcmpProbe>(measured); services.AddSingleton<ITcpProbe>(measuredTcp); });
        var vm = provider.GetRequiredService<MainViewModel>();
        var window = provider.GetRequiredService<MainWindow>();
        app.MainWindow = window; window.DataContext = vm;
        window.ShowActivated = false; window.ShowInTaskbar = false; window.Opacity = 0;
        window.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        window.WindowState = WindowState.Maximized;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Require(WindowWorkArea.IsWithinWorkArea(window), "Maximized native window stays inside the current monitor work area above the taskbar");
        window.WindowState = WindowState.Normal;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        CheckSettingsDialogs();
        Render(window, output, "LivePing-idle", 1536, 1024, 1);
        Require(vm.Ping.Rows.Count == 0 && vm.Ping.Sent == 0, "Idle contains no fabricated telemetry");
        vm.Ping.TargetText = "127.0.0.1 Loopback IP\nlocalhost Loopback DNS ไทย";
        await vm.Ping.StartCommand.ExecuteAsync(null);
        await Until(() => { vm.Ping.Refresh(); return vm.Ping.Rows.Count == 2 && vm.Ping.Rows.All(row => row.Data.Received > 0); });
        Require(vm.Ping.State == SessionState.Running, "ViewModel enters Running with actual ICMP results");
        vm.Ping.SelectedRow = vm.Ping.Rows[1];
        vm.Ping.Refresh();
        Require(vm.Ping.History.SequenceEqual(provider.GetRequiredService<MultiTargetPingService>().History("localhost")), "Selected-target history binds to the exact host");
        vm.Ping.Results.SortDescriptions.Add(new SortDescription("Host", ListSortDirection.Descending));
        vm.Ping.StatusFilter = "Unknown"; Require(vm.Ping.Results.IsEmpty, "Status filter removes sampled targets");
        vm.Ping.StatusFilter = "All status";
        Require(vm.Ping.Results.Cast<TargetRow>().Count() == 2, "Sort/filter retains target models");
        Require(vm.Ping.SelectedRow?.Data.Host == "localhost" && vm.Ping.History.Count > 0, "Selection/history are restored when filter is removed");
        await vm.Ping.PauseCommand.ExecuteAsync(null);
        Require(vm.Ping.State == SessionState.Paused && measured.Active == 0, "Pause drains real outstanding probes");
        long sent = vm.Ping.Sent; await Task.Delay(1100); vm.Ping.Refresh();
        Require(vm.Ping.Sent == sent, "Paused counters remain stable");
        await vm.Ping.ExportCommand.ExecuteAsync(null);
        Require(dialogs.Exports.Single().Contains("Loopback DNS ไทย"), "CSV contains measured rows and Unicode descriptions");
        Render(window, output, "LivePing", 1536, 1024, 1);
        Render(window, output, "LivePing-narrow", 1280, 800, 1);
        Render(window, output, "LivePing-125pct", 1280, 800, 1.25);
        Render(window, output, "LivePing-150pct", 1280, 800, 1.5);
        CheckButtons(window, vm.Ping.StartCommand, vm.Ping.PauseCommand, vm.Ping.StopCommand);
        var pingGrid = Descendants<DataGrid>((DependencyObject)window.Content).Single(g => AutomationProperties.GetName(g) == "Realtime Results");
        pingGrid.Columns[2].Width = 180; pingGrid.Columns[3].Visibility = Visibility.Collapsed;
        ColumnLayout.Capture(pingGrid);
        Require(store.Preferences.Columns["Ping"].Any(c => c.Key == "Description" && !c.Visible), "Column visibility/width preference captured");
        await vm.Ping.PauseCommand.ExecuteAsync(null); Require(vm.Ping.State == SessionState.Running, "Resume continues same session");
        await vm.Ping.StopCommand.ExecuteAsync(null); Require(vm.Ping.State == SessionState.Stopped && measured.Active == 0, "Stop awaits real cleanup");
        Require(provider.GetRequiredService<MultiTargetPingService>().Diagnostics.UiLag.Count > 0, "Ping records actual publish-to-UI timing");
        await CheckUiRevisionAsync(window, vm, dialogs, output);
        await CheckPortAndUpdatesAsync(window, vm, dialogs, store, output);
        vm.NavigateCommand.Execute("Traceroute");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Require(Descendants<RadioButton>((DependencyObject)window.Content).Single(b => Equals(b.CommandParameter, "Traceroute")).IsChecked == true, "Sidebar selection follows the active page");
        vm.Trace.Target = "127.0.0.1";
        await vm.Trace.StartCommand.ExecuteAsync(null);
        await Until(() => { vm.Trace.Refresh(); return vm.Trace.Engine.CompletedCycles >= 2; });
        Require(vm.Trace.Rows.Count == 1 && vm.Trace.Rows[0].Data.Received >= 2, "Actual TTL-1 destination replies repeat under independent continuous polling");
        await CheckTraceEditingAsync(window, vm.Trace, dialogs);
        CheckHorizontalScrollBar(window);
        await vm.Trace.PauseCommand.ExecuteAsync(null);
        Require(vm.Trace.State == SessionState.Paused, "Trace pause retains partial path");
        vm.Trace.EventFilter = "Route"; Require(vm.Trace.Events.IsEmpty, "No fabricated loopback route changes");
        vm.Trace.EventFilter = "All events";
        Render(window, output, "Traceroute", 1536, 1024, 1);
        Render(window, output, "Traceroute-narrow", 1280, 800, 1);
        Render(window, output, "Traceroute-125pct", 1280, 800, 1.25);
        Render(window, output, "Traceroute-150pct", 1280, 800, 1.5);
        CheckButtons(window, vm.Trace.StartCommand, vm.Trace.PauseCommand, vm.Trace.StopCommand);
        await vm.Trace.StopCommand.ExecuteAsync(null);
        Require(vm.Trace.Engine.Diagnostics.UiLag.Count > 0, "Trace records actual publish-to-UI timing");
        await CheckBackgroundPageAsync(vm, provider.GetRequiredService<MultiTargetPingService>());
        vm.NavigateCommand.Execute("Live Ping");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var restoredGrid = Descendants<DataGrid>((DependencyObject)window.Content).Single(g => AutomationProperties.GetName(g) == "Realtime Results");
        Require(ReferenceEquals(restoredGrid, pingGrid), "Main menu navigation reuses the same results grid");
        Require(restoredGrid.Columns[2].Width.Value == 180 && restoredGrid.Columns[3].Visibility == Visibility.Collapsed, "Column settings survive cached page navigation");
        Render(window, output, "LivePing-configured", 1536, 1024, 1);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        Require(Descendants<DataGridColumnHeader>(restoredGrid).Any(h => Equals(h.Column?.Header, "Lost") && h.IsVisible), "Visible Lost column remains realized after configuring columns and resizing");
        Render(window, output, "LivePing-configured-settled", 1536, 1024, 1);
        vm.NavigateCommand.Execute("Dashboard"); Require(vm.CurrentPage is PlaceholderViewModel, "Dashboard remains neutral placeholder");
        vm.NavigateCommand.Execute("Network Info"); Require(vm.CurrentPage is PlaceholderViewModel, "Network Info remains neutral placeholder");

        if (soakSeconds > 0)
        {
            using var tcpFarm = new LoopbackTcpFarm(32);
            measuredTcp.Reset();
            vm.Port.TargetText = tcpFarm.Targets;
            await vm.Port.StartCommand.ExecuteAsync(null);
            await Until(() => { vm.Port.Refresh(); return vm.Port.Rows.Count == 32 && vm.Port.Rows.All(r => r.Data.Connected > 0); });
            vm.NavigateCommand.Execute("Live Ping");
            measured.Reset();
            vm.Ping.TargetText = "127.0.0.0/24 Loopback QA only";
            await vm.Ping.StartCommand.ExecuteAsync(null);
            await Until(() => { vm.Ping.Refresh(); return vm.Ping.Rows.Count == 254 && vm.Ping.Rows.All(row => row.Data.Sent > 0); });
            // Keep the second engine running on another loopback address while its presentation is inactive.
            vm.Trace.Target = "127.0.1.1";
            await vm.Trace.StartCommand.ExecuteAsync(null);
            var traceService = vm.Trace.Engine;
            vm.TraceWorkspace.AddSessionCommand.Execute(null);
            var secondTrace = vm.Trace;
            secondTrace.Target = "127.0.2.1";
            await secondTrace.StartCommand.ExecuteAsync(null);
            var elapsed = Stopwatch.StartNew();
            var heartbeat = Stopwatch.StartNew();
            double maximumGap = 0;
            long startingMemory = GC.GetTotalMemory(true);
            int nextReport = 30, nextNavigation = 5;
            string[] soakPages = ["Port Test", "Traceroute", "Live Ping", "Updates"];
            int navigationIndex = 0;
            var navigationTimes = new List<double>();
            while (elapsed.Elapsed.TotalSeconds < soakSeconds)
            {
                await Task.Delay(50);
                maximumGap = Math.Max(maximumGap, heartbeat.Elapsed.TotalMilliseconds); heartbeat.Restart();
                if (elapsed.Elapsed.TotalSeconds >= nextNavigation)
                {
                    var navigation = Stopwatch.StartNew();
                    // Keep each page visible for five seconds so TCP presentation is exercised continuously, too.
                    vm.NavigateCommand.Execute(soakPages[navigationIndex++ % soakPages.Length]); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    navigationTimes.Add(navigation.Elapsed.TotalMilliseconds);
                    nextNavigation += 5;
                }
                if (elapsed.Elapsed.TotalSeconds >= nextReport)
                {
                    vm.Ping.Refresh();
                    Console.WriteLine($"SOAK {elapsed.Elapsed.TotalSeconds:0}s targets={vm.Ping.TargetCount} sent={vm.Ping.Sent} received={vm.Ping.Received} active={measured.Active} max-active={measured.Maximum} max-dispatcher-gap={maximumGap:0.0}ms managed-memory={GC.GetTotalMemory(false)}");
                    nextReport += 30;
                }
                if (vm.Ping.State == SessionState.Error || traceService.State == SessionState.Error || vm.Port.State == SessionState.Error) throw new InvalidOperationException(vm.Ping.Message + " " + traceService.LastError + " " + vm.Port.Message);
            }
            await vm.Ping.StopCommand.ExecuteAsync(null); await secondTrace.StopCommand.ExecuteAsync(null); await vm.TraceWorkspace.Sessions[0].StopCommand.ExecuteAsync(null); vm.Ping.Refresh(true);
            await vm.Port.StopCommand.ExecuteAsync(null); vm.Port.Refresh(true);
            Require(vm.Port.Rows.Count == 32 && vm.Port.Rows.All(r => r.Connected > 0 && r.Attempts == r.Connected) && measuredTcp.Active == 0 && measuredTcp.Maximum <= 32 && measuredTcp.Overlap == 0, "32 actual TCP endpoints run alongside 254 Ping targets and two trace sessions without TCP overlap; Stop drains all sockets");
            vm.NavigateCommand.Execute("Port Test"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Render(window, output, "PortTest-32-soak", 1536, 1024, 1);
            await File.WriteAllTextAsync(Path.Combine(output, "tcp-soak-results.json"), JsonSerializer.Serialize(new { Endpoints = vm.Port.TargetCount, Attempts = vm.Port.Sent, Connected = vm.Port.Received, measuredTcp.Maximum, measuredTcp.Overlap, measuredTcp.Active, Diagnostics = provider.GetRequiredService<MultiTargetPortService>().Diagnostics }, new JsonSerializerOptions { WriteIndented = true }));
            vm.NavigateCommand.Execute("Live Ping"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Require(vm.Ping.Rows.Count == 254 && measured.Active == 0 && measured.Maximum <= 66, "254 real loopback targets and concurrent trace remain bounded and Stop drains requests");
            Require(measured.Overlap == 0, "No overlapping request for the same loopback address and TTL");
            Require(traceService.Events().Count <= 3 && secondTrace.Engine.Events().Count <= 3, "Stable continuous traces log lifecycle and destination changes without probe reply spam");
            Require(secondTrace.Engine.CompletedCycles > 0 && traceService.CompletedCycles > 0, "Background continuous traceroute completes real ICMP cycles during Ping soak");
            Require(vm.Ping.Rows.All(r => r.Data.Sent == r.Data.Received + r.Data.Lost), "Real counters stay coherent under soak");
            Render(window, output, "LivePing-254-soak", 1536, 1024, 1);
            string report = $"DurationSeconds={elapsed.Elapsed.TotalSeconds:0.0}\nTargets={vm.Ping.TargetCount}\nSent={vm.Ping.Sent}\nReceived={vm.Ping.Received}\nMaximumCombinedConcurrentIcmp={measured.Maximum}\nPerAddressTtlOverlap={measured.Overlap}\nTraceCompletedCycles={traceService.CompletedCycles}\nTraceSent={traceService.Snapshot().Sum(h => h.Sent)}\nSecondTraceCompletedCycles={secondTrace.Engine.CompletedCycles}\nSecondTraceSent={secondTrace.Engine.Snapshot().Sum(h => h.Sent)}\nMaximumDispatcherGapMs={maximumGap:0.0}\nNavigationSamples={navigationTimes.Count}\nNavigationMeanMs={navigationTimes.Average():0.0}\nNavigationMaximumMs={navigationTimes.Max():0.0}\nManagedMemoryBefore={startingMemory}\nManagedMemoryAfter={GC.GetTotalMemory(true)}\n";
            await File.WriteAllTextAsync(Path.Combine(output, "soak-results.txt"), report); Console.WriteLine(report);
            await File.WriteAllTextAsync(Path.Combine(output, "engine-diagnostics.json"), JsonSerializer.Serialize(new { Ping = provider.GetRequiredService<MultiTargetPingService>().Diagnostics, Trace = traceService.Diagnostics }, new JsonSerializerOptions { WriteIndented = true }));
        }
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Require(errors.Messages.Count == 0, "Actual WPF renders have no data-binding errors: " + string.Join(" | ", errors.Messages.Take(3)));
        await vm.ShutdownAsync();
        Require(measured.Active == 0 && measuredTcp.Active == 0 && vm.Port.State == SessionState.Stopped, "Window shutdown awaits Ping, Traceroute, TCP and release checks");
        Console.WriteLine("WINDOWS QA PASSED; images are actual WPF renders, not manual desktop screenshots.");
        window.Close();
    }

    private static void CheckSettingsDialogs()
    {
        var dialogs = new WpfDialogService();
        foreach (bool trace in new[] { false, true })
        {
            Exception? error = null;
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                var dialog = Application.Current.Windows.Cast<Window>().Single(w => w.Title == "Probe Settings");
                dialog.Opacity = 0;
                try
                {
                    var inputs = Descendants<TextBox>((DependencyObject)dialog.Content).ToArray();
                    Require(inputs.Length == (trace ? 4 : 3), $"{(trace ? "Trace" : "Ping")} Probe Settings exposes only the requested fields");
                    int interval = trace ? 1 : 0;
                    inputs[interval].Text = "249";
                    var save = Descendants<Button>((DependencyObject)dialog.Content).Single(b => Equals(b.Content, "Save"));
                    save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(dialog.IsVisible && Descendants<TextBlock>((DependencyObject)dialog.Content).Any(t => t.Text.StartsWith("Invalid")), "Invalid interval remains in the dialog with a validation error");
                }
                catch (Exception ex) { error = ex; }
                finally { dialog.DialogResult = false; }
            }));
            if (trace) dialogs.EditTraceSettings(new()); else dialogs.EditPingSettings(new());
            if (error is not null) throw error;
        }
        Exception? portError = null;
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var dialog = Application.Current.Windows.Cast<Window>().Single(w => w.Title == "TCP Probe Settings"); dialog.Opacity = 0;
            try
            {
                var inputs = Descendants<TextBox>((DependencyObject)dialog.Content).ToArray();
                Require(inputs.Length == 2, "TCP settings expose only interval and timeout"); inputs[0].Text = "249";
                Descendants<Button>((DependencyObject)dialog.Content).Single(b => Equals(b.Content, "Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(dialog.IsVisible && Descendants<TextBlock>((DependencyObject)dialog.Content).Any(t => t.Text.Contains("250–60000")), "TCP settings reject intervals below 250ms");
            }
            catch(Exception ex) { portError = ex; }
            finally { dialog.DialogResult = false; }
        }));
        dialogs.EditPortSettings(new()); if(portError is not null) throw portError;
    }

    private static async Task CheckPortAndUpdatesAsync(Window window, MainViewModel vm, QaDialogs dialogs, UserSettingsStore store, string output)
    {
        vm.NavigateCommand.Execute("Port Test"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Require(vm.Port.Rows.Count == 0 && vm.Port.Sent == 0, "Port Test starts without illustrative measurements");
        using var farm = new LoopbackTcpFarm(2);
        using var closed = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); closed.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)closed.LocalEndPoint!).Port;
        vm.Port.TargetText = farm.Targets.Replace("127.0.0.1:", "localhost:") + $"\n127.0.0.1:{port} closed socket";
        await vm.Port.StartCommand.ExecuteAsync(null);
        await Until(() => { vm.Port.Refresh(); return vm.Port.Rows.Count == 3 && vm.Port.Rows.All(r => r.Attempts > 0); });
        Require(vm.Port.Rows.Count(r => r.Status == "Connected") == 2 && vm.Port.Rows.Single(r => r.Port == port).Last is null, "Actual TCP successes have connect times; a closed endpoint has no invented time");
        vm.Port.SelectedRow = vm.Port.Rows[0]; vm.Port.Refresh();
        Require(vm.Port.History.Count > 0 && vm.Port.History[0].ConnectMs is not null, "Selected TCP endpoint history uses measured handshake results");
        vm.Port.Results.SortDescriptions.Add(new SortDescription("Endpoint", ListSortDirection.Descending));
        vm.Port.StatusFilter = "Connected"; Require(vm.Port.Results.Cast<PortRow>().Count() == 2, "TCP status filtering and sorting operate on live endpoints");
        vm.Port.StatusFilter = "All status";
        await vm.Port.PauseCommand.ExecuteAsync(null); long attempts = vm.Port.Sent; await Task.Delay(600); vm.Port.Refresh();
        Require(vm.Port.State == SessionState.Paused && vm.Port.Sent == attempts, "TCP Pause drains sockets and preserves counters");
        await vm.Port.ExportCommand.ExecuteAsync(null); Require(dialogs.Exports.Last().Contains("Connect (ms)") && dialogs.Exports.Last().Contains("closed socket"), "TCP CSV exports actual outcomes and connection time");
        Render(window, output, "PortTest", 1536, 1024, 1); Render(window, output, "PortTest-narrow", 1280, 800, 1);
        var grid = Descendants<DataGrid>((DependencyObject)window.Content).Single(g => AutomationProperties.GetName(g) == "TCP Port Test Results");
        GridTools.FitColumns(grid); Require(grid.Columns[^1].Width.IsStar, "TCP Fit Columns stretches the final column");
        vm.Port.UnselectCommand.Execute(null); Require(vm.Port.SelectedRow is null && vm.Port.History.Count == 0, "TCP row selection can be cleared");
        await vm.Port.PauseCommand.ExecuteAsync(null); await Until(() => { vm.Port.Refresh(); return vm.Port.Sent > attempts; });
        vm.NavigateCommand.Execute("Updates"); await Task.Delay(400);
        Require(vm.IsPortActive && vm.Port.State == SessionState.Running, "TCP keeps running while Updates is visible");
        await vm.Port.StopCommand.ExecuteAsync(null);
        vm.Port.TargetText = farm.Targets; dialogs.TemplateName = "TCP QA ไทย"; await vm.Port.SaveCommand.ExecuteAsync(null);
        Require(vm.Port.Templates.Any(t => t.Name == "TCP QA ไทย") && store.Preferences.PortTemplates.Any(t => t.Name == "TCP QA ไทย"), "TCP templates persist named endpoints");
        var template = vm.Port.Templates.Single(t => t.Name == "TCP QA ไทย"); dialogs.ConfirmDelete = false;
        await template.DeleteCommand.ExecuteAsync(null); Require(vm.Port.Templates.Contains(template), "TCP template deletion respects No");
        dialogs.ConfirmDelete = true; await template.DeleteCommand.ExecuteAsync(null); Require(!vm.Port.Templates.Contains(template), "TCP template deletion persists Yes");
        vm.NavigateCommand.Execute("Updates"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Require(vm.Updates.CurrentVersion == vm.Version && vm.Updates.History.Any(h => h.Version == vm.Version) && vm.Updates.History.Any(h => h.Version == "0.1.0"), "Update page shows the executable version and actual recorded changes");
        Require(vm.Updates.Available is null && !vm.Updates.OpenReleaseCommand.CanExecute(null), "No update or install action is fabricated before checking GitHub");
        var links = new RecordingLinks();
        var fixture = new ReleaseFixture();
        await using (var update = new UpdatesViewModel(fixture, links))
        {
            SemanticVersion.TryParse(update.CurrentVersion, out var current);
            var next = new SemanticVersion(current!.Major, current.Minor + 1, 0);
            var page = new Uri($"https://github.com/pk-wrk-sea/NetStucked/releases/tag/v{next}");
            fixture.Result = new([new(next, "test release", "test fixture notes", page, page, DateTimeOffset.Now)], DateTimeOffset.Now);
            await update.CheckCommand.ExecuteAsync(null);
            Require(update.Available?.Version == next && update.Badge == "Minor update" && update.DownloadLabel.Contains("installer"), "Updater fixture enables the manual installer link for a genuinely newer listed version");
            update.OpenReleaseCommand.Execute(null); Require(links.Page == page, "Updater uses the reviewed release page, without executing an installer");
            fixture.Error = new HttpRequestException("test offline"); await update.CheckCommand.ExecuteAsync(null);
            Require(update.Available is null && update.StatusTitle == "Unable to check for updates" && !update.OpenReleaseCommand.CanExecute(null), "Updater clears stale install actions when a new check fails");
        }
        var blocking = new ReleaseFixture { Block = true };
        var closing = new UpdatesViewModel(blocking, links); var check = closing.CheckCommand.ExecuteAsync(null);
        await closing.DisposeAsync(); await check; await closing.DisposeAsync();
        Require(blocking.Cancelled && !closing.CheckCommand.CanExecute(null), "Closing cancels and awaits the release request and disposal is repeatable");
        // This is a real public read; API errors remain visible as failures rather than up-to-date claims.
        await vm.Updates.CheckCommand.ExecuteAsync(null);
        Require(!vm.Updates.IsChecking && vm.Updates.LastChecked != "Not checked yet", "Manual GitHub check completes and records its actual outcome");
        await File.WriteAllTextAsync(Path.Combine(output, "github-check.txt"), $"{vm.Updates.StatusTitle}\n{vm.Updates.StatusDetails}\n{vm.Updates.LastChecked}\n");
        Render(window, output, "Updates", 1536, 1024, 1); Render(window, output, "Updates-narrow", 1280, 800, 1);
        vm.Updates.RecoveryCommand.Execute(null); Require(vm.Updates.ShowRecoveryGuide, "Rollback opens manual recovery instructions");
        Render(window, output, "Updates-recovery", 1536, 1024, 1); vm.Updates.RecoveryCommand.Execute(null);
        window.UpdateLayout();
        var settings = Descendants<RadioButton>((DependencyObject)window.Content).Single(b => Equals(b.CommandParameter, "Settings"));
        var updates = Descendants<RadioButton>((DependencyObject)window.Content).Single(b => Equals(b.CommandParameter, "Updates"));
        var tools = Descendants<RadioButton>((DependencyObject)window.Content).Where(b => !Equals(b.CommandParameter, "Settings") && !Equals(b.CommandParameter, "Updates"));
        Require(tools.All(b => b.TranslatePoint(new Point(), window).Y < settings.TranslatePoint(new Point(), window).Y) && updates.TranslatePoint(new Point(), window).Y > settings.TranslatePoint(new Point(), window).Y, "Settings and Updates are grouped below diagnostic tools at the sidebar bottom");
        vm.NavigateCommand.Execute("Live Ping"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private static async Task CheckUiRevisionAsync(Window window, MainViewModel vm, QaDialogs dialogs, string output)
    {
        var grid = Descendants<DataGrid>((DependencyObject)window.Content).Single(g => AutomationProperties.GetName(g) == "Realtime Results");
        Require(new[] { "Last ping", "Last success", "Reachable since", "Unreachable since", "Result / error" }.All(h => grid.Columns.Any(c => Equals(c.Header, h) && c.Visibility == Visibility.Visible)), "All five requested result columns are available and visible");
        var header = Descendants<DataGridColumnHeader>(grid).First(h => Equals(h.Column?.Header, "Host"));
        var thumb = Descendants<Thumb>(header).Single(t => t.Name == "PART_RightHeaderGripper");
        thumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
        var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(grid);
        Require(layer?.GetAdorners(grid)?.Any(a => a.GetType().Name == "ResizeGuide" && !a.IsHitTestVisible) == true, "Column resize displays a visual guide without capturing mouse input");
        Render(window, output, "LivePing-resize-guide", 1536, 1024, 1);
        thumb.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
        Require(layer?.GetAdorners(grid)?.All(a => a.GetType().Name != "ResizeGuide") != false, "Column resize removes its guide on completion");
        var widths = grid.Columns.Select(c => c.Width).ToArray();
        GridTools.FitColumns(grid);
        Require(grid.Columns.Where(c => c.Visibility == Visibility.Visible).MaxBy(c => c.DisplayIndex)!.Width.IsStar, "Fit Columns stretches the last visible column to the right edge");
        for (int i = 0; i < widths.Length; i++) grid.Columns[i].Width = widths[i];
        var buttons = Descendants<Button>((DependencyObject)window.Content).Where(b => b.FontFamily.Source == "Segoe MDL2 Assets").ToArray();
        Require(buttons.All(b => ToolTipService.GetInitialShowDelay(b) == 1000 && b.ToolTip is string text && text.Length > 0), "Icon buttons show meaningful one-second tooltips");
        vm.Ping.TargetText = "10.1.1.1 Branch A\n20.3.3.3 Branch B";
        dialogs.TemplateName = "Branches ไทย";
        await vm.Ping.SaveCommand.ExecuteAsync(null);
        var template = vm.Ping.Templates.Single();
        vm.Ping.TargetText = "127.0.0.1"; vm.Ping.SelectedTemplate = null; vm.Ping.SelectedTemplate = template;
        Require(vm.Ping.TargetText.Contains("20.3.3.3"), "Named template selection loads its exact address list without network work");
        dialogs.ConfirmDelete = false; await template.DeleteCommand.ExecuteAsync(null);
        Require(vm.Ping.Templates.Count == 1, "No preserves an address template");
        dialogs.ConfirmDelete = true; await template.DeleteCommand.ExecuteAsync(null);
        Require(vm.Ping.Templates.Count == 0 && dialogs.Confirmations == 2, "Yes deletes an address template after explicit confirmation");
        vm.Ping.TargetText = "127.0.0.1 History scroll QA";
        await vm.Ping.StartCommand.ExecuteAsync(null);
        await Until(() => { vm.Ping.Refresh(); return vm.Ping.Rows.Count == 1 && vm.Ping.Sent >= 1; });
        vm.Ping.SelectedRow = vm.Ping.Rows[0];
        var history = Descendants<DataGrid>((DependencyObject)window.Content).Single(g => AutomationProperties.GetName(g) == "Selected target Ping History");
        history.MaxHeight = 125;
        await Until(() => vm.Ping.History.Count >= 12);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var scroll = Descendants<ScrollViewer>(history).First();
        scroll.ScrollToVerticalOffset(4); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var anchor = history.Items[(int)Math.Floor(scroll.VerticalOffset)];
        long sent = vm.Ping.Sent;
        long refreshed = vm.Ping.ViewRefreshCount;
        vm.Ping.Results.SortDescriptions.Clear(); vm.Ping.Results.SortDescriptions.Add(new SortDescription("Host", ListSortDirection.Ascending));
        await Until(() => vm.Ping.Sent >= sent + 3);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Require(Equals(anchor, history.Items[(int)Math.Floor(scroll.VerticalOffset)]), "Manual history scroll keeps the same actual sample in view as new replies arrive");
        Require(vm.Ping.ViewRefreshCount == refreshed, "Sorting by an unchanged host does not reset the collection on each probe");
        Render(window, output, "LivePing-history-scrolled", 1536, 1024, 1);
        var click = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent, Source = grid };
        grid.RaiseEvent(click);
        Require(vm.Ping.SelectedRow is null && vm.Ping.History.Count == 0, "Clicking blank results space clears selection and history");
        await Task.Delay(350); Require(vm.Ping.SelectedRow is null, "Incoming results do not select a target again after deselection");
        await vm.Ping.StopCommand.ExecuteAsync(null); history.ClearValue(FrameworkElement.MaxHeightProperty);
        vm.Ping.Results.SortDescriptions.Clear();
        vm.TraceWorkspace.AddSessionCommand.Execute(null);
        var second = vm.Trace; second.Target = "127.0.2.1"; await second.StartCommand.ExecuteAsync(null);
        var first = vm.TraceWorkspace.Sessions[0]; first.Target = "127.0.1.1"; await first.StartCommand.ExecuteAsync(null);
        await Until(() => first.Engine.CompletedCycles >= 2 && second.Engine.CompletedCycles >= 2);
        await Until(() => vm.IsTraceActive);
        vm.NavigateCommand.Execute("Traceroute");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var combo = Descendants<ComboBox>((DependencyObject)window.Content).Single(c => AutomationProperties.GetName(c) == "Traceroute destination IP or hostname");
        Require(combo.Template.FindName("PART_EditableTextBox", combo) is TextBox, "Destination dropdown supports actual editable text input");
        Render(window, output, "Traceroute-two-running-sessions", 1536, 1024, 1);
        vm.NavigateCommand.Execute("Live Ping");
        await first.PauseCommand.ExecuteAsync(null); long frozen = first.Engine.Snapshot()[0].Sent, other = second.Engine.Snapshot()[0].Sent;
        await Until(() => second.Engine.Snapshot()[0].Sent > other);
        Require(first.Engine.Snapshot()[0].Sent == frozen, "Pausing one real trace session leaves the other trace polling independently");
        int confirmations = dialogs.Confirmations;
        await vm.TraceWorkspace.RecentAddresses.Single(a => a.Value == "127.0.1.1").DeleteCommand.ExecuteAsync(null);
        Require(dialogs.Confirmations == confirmations && vm.TraceWorkspace.RecentAddresses.All(a => a.Value != "127.0.1.1"), "Trace destination history deletes immediately without a confirmation");
        Require(first.Target == "127.0.1.1", "Deleting history does not change an existing session destination");
        var reloaded = new UserSettingsStore(vm.Ping.Store.DirectoryPath);
        Require(reloaded.LoadError is null && reloaded.Preferences.TraceHistory.Contains("127.0.2.1"), "Destination history persists for the next application run");
        await vm.TraceWorkspace.RemoveSessionCommand.ExecuteAsync(second);
        Require(vm.TraceWorkspace.Sessions.Count == 1 && second.Engine.State == SessionState.Stopped, "Closing a trace session awaits its network cleanup");
        await first.StopCommand.ExecuteAsync(null);
        vm.NavigateCommand.Execute("Traceroute");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        combo = Descendants<ComboBox>((DependencyObject)window.Content).Single(c => AutomationProperties.GetName(c) == "Traceroute destination IP or hostname");
        var saved = vm.TraceWorkspace.RecentAddresses.Single(a => a.Value == "127.0.2.1");
        combo.SelectedItem = saved;
        Require(vm.Trace.Target == "127.0.2.1", "Selecting an actual dropdown history item loads its address into the input");
        await saved.DeleteCommand.ExecuteAsync(null);
        Require(vm.Trace.Target == "127.0.2.1" && combo.Text == "127.0.2.1", "Deleting the selected history item retains editable destination text");
        vm.NavigateCommand.Execute("Live Ping");
    }

    private static async Task CheckBackgroundPageAsync(MainViewModel vm, MultiTargetPingService service)
    {
        vm.NavigateCommand.Execute("Live Ping");
        vm.Ping.TargetText = "127.0.0.1 Background loopback only";
        await vm.Ping.StartCommand.ExecuteAsync(null);
        await Until(() => { vm.Ping.Refresh(); return vm.Ping.Sent > 0; });
        vm.NavigateCommand.Execute("Traceroute");
        long displayed = vm.Ping.Sent, actual = service.Snapshot()[0].Sent;
        await Until(() => service.Snapshot()[0].Sent > actual);
        Require(vm.Ping.Sent == displayed, "Inactive page pauses its presentation timer while real probing continues");
        vm.NavigateCommand.Execute("Live Ping");
        await Until(() => vm.Ping.Sent > displayed); Require(vm.Ping.Sent > displayed, "Returning to the page applies the latest real results without synchronous navigation work");
        await vm.Ping.StopCommand.ExecuteAsync(null);
    }

    private static void CheckButtons(Window window, object start, object pause, object stop)
    {
        var buttons = Descendants<Button>((DependencyObject)window.Content).ToArray();
        Require(buttons.Count(b => ReferenceEquals(b.Command, start)) == 1 && buttons.Count(b => ReferenceEquals(b.Command, pause)) == 1 && buttons.Count(b => ReferenceEquals(b.Command, stop)) == 1, "Exactly one action group on approved page");
        Require(buttons.Where(b => b.FontFamily.Source == "Segoe MDL2 Assets").All(b => AutomationProperties.GetName(b).Length > 0), "Icon buttons have accessible names");
    }
    private static async Task CheckTraceEditingAsync(Window window, TracerouteViewModel trace, QaDialogs dialogs)
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var grid = Descendants<DataGrid>((DependencyObject)window.Content).Single(g => AutomationProperties.GetName(g) == "Traceroute Results");
        var row = trace.Rows[0];
        grid.SelectedItem = row;
        grid.CurrentCell = new DataGridCellInfo(row, grid.Columns[3]);
        Require(grid.BeginEdit(), "Description cell enters a real DataGrid edit transaction");
        var view = (IEditableCollectionView)trace.Results;
        Require(view.IsEditingItem, "Collection view reports active description editing");
        try
        {
            row.Description = "Loopback edit ไทย";
            await trace.ExportCommand.ExecuteAsync(null);
            Require(dialogs.Exports.Last().Contains("Loopback edit ไทย"), "Immediate trace CSV export includes the latest description edit");
            trace.Refresh();
            Require(view.IsEditingItem, "Realtime refresh preserves the active description edit");
            long sent = row.Data.Sent;
            await Until(() => row.Data.Sent > sent);
            Require(view.IsEditingItem, "Dispatcher timer keeps updating real probes while description editing remains active");
            trace.StatusFilter = "Timeout";
            trace.Refresh();
            Require(view.IsEditingItem, "Status filtering waits for the description edit to finish");
            Require(grid.CommitEdit(DataGridEditingUnit.Row, true), "Description edit commits successfully");
            trace.Refresh();
            Require(trace.Results.IsEmpty, "Deferred status filter is applied after committing the edit");
            trace.StatusFilter = "All status";
            trace.Refresh();
            Require(row.Data.Description == "Loopback edit ไทย", "Edited description reaches the real trace snapshot");
            Console.WriteLine("PASS Trace editing/refresh regression");
        }
        finally
        {
            grid.CancelEdit(DataGridEditingUnit.Row);
            trace.StatusFilter = "All status";
        }
    }
    private static void CheckHorizontalScrollBar(Window window)
    {
        var bar = Descendants<ScrollBar>((DependencyObject)window.Content).First(b => b.Orientation == Orientation.Horizontal && b.IsVisible && b.Maximum > b.Minimum);
        var track = (Track)bar.Template.FindName("PART_Track", bar);
        Require(!track.IsDirectionReversed, "Horizontal scrollbar increases from left to right");
        Require(track.ValueFromDistance(10, 0) > 0, "Dragging the horizontal thumb right increases its value");
        Require(Equals(track.DecreaseRepeatButton.Command, ScrollBar.PageLeftCommand) && Equals(track.IncreaseRepeatButton.Command, ScrollBar.PageRightCommand), "Horizontal scrollbar track uses left/right paging commands");
    }
    private static void Render(Window window, string output, string name, int width, int height, double scale)
    {
        var root = (FrameworkElement)window.Content;
        window.Width = width; window.Height = height;
        root.Width = width; root.Height = height;
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(root);
        root.ClearValue(FrameworkElement.WidthProperty); root.ClearValue(FrameworkElement.HeightProperty);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
        Console.WriteLine($"RENDER {name} logical={width}x{height} scale={scale}");
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T value) yield return value;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Console.WriteLine("PASS " + message); }
    private static async Task Until(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition()) { if (watch.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("QA condition not reached"); await Task.Delay(25); }
    }
    private sealed class BindingErrors : TraceListener
    {
        public List<string> Messages { get; } = [];
        public override void Write(string? message) { if (message is not null) Messages.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
    private sealed class QaDialogs : IDesktopDialogs
    {
        public List<string> Exports { get; } = [];
        public string? TemplateName { get; set; }
        public bool ConfirmDelete { get; set; }
        public int Confirmations { get; private set; }
        public string? AskTemplateName(string suggested) => TemplateName;
        public bool ConfirmTemplateDeletion(string name) { Confirmations++; return ConfirmDelete; }
        public Task<string?> LoadTargetsAsync() => Task.FromResult<string?>(null);
        public Task SaveTargetsAsync(string text) => Task.CompletedTask;
        public Task ExportAsync(string suggestedName, string csv) { Exports.Add(csv); return Task.CompletedTask; }
        public ProbeSettings? EditPingSettings(ProbeSettings settings) => null;
        public TraceSettings? EditTraceSettings(TraceSettings settings) => null;
        public void ShowError(string message) => throw new InvalidOperationException(message);
    }
    private sealed class RecordingLinks : IExternalLinks
    {
        public Uri? Page;
        public void OpenRelease(Uri page) => Page = page;
    }
    private sealed class ReleaseFixture : IReleaseSource
    {
        public ReleaseCheck Result = new([], DateTimeOffset.Now);
        public Exception? Error; public bool Block, Cancelled;
        public async Task<ReleaseCheck> CheckAsync(CancellationToken token)
        {
            if (Block) try { await Task.Delay(Timeout.Infinite, token); } catch(OperationCanceledException) { Cancelled = true; throw; }
            if (Error is not null) throw Error;
            return Result;
        }
    }
    private sealed class LoopbackTcpFarm : IDisposable
    {
        private readonly CancellationTokenSource _lifetime = new();
        private readonly List<TcpListener> _listeners = [];
        private readonly List<Task> _acceptors = [];
        public string Targets => string.Join("\n", _listeners.Select((l, i) => $"127.0.0.1:{((IPEndPoint)l.LocalEndpoint).Port} TCP QA {i + 1}"));
        public LoopbackTcpFarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); _listeners.Add(listener);
                _acceptors.Add(AcceptAsync(listener));
            }
        }
        private async Task AcceptAsync(TcpListener listener)
        {
            try { while (true) { using var client = await listener.AcceptTcpClientAsync(_lifetime.Token).ConfigureAwait(false); } }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        }
        public void Dispose()
        {
            _lifetime.Cancel(); Task.WhenAll(_acceptors).GetAwaiter().GetResult();
            foreach (var listener in _listeners) listener.Stop(); _lifetime.Dispose();
        }
    }
    private sealed class MeasuredRealTcp : ITcpProbe
    {
        private readonly TcpPortProbe _real = new();
        private readonly ConcurrentDictionary<string, int> _endpoints = new();
        private int _active, _maximum, _overlap;
        public int Active => Volatile.Read(ref _active);
        public int Maximum => Volatile.Read(ref _maximum);
        public int Overlap => Volatile.Read(ref _overlap);
        public void Reset() { if (Active != 0) throw new InvalidOperationException("TCP is still active."); _endpoints.Clear(); _maximum = _overlap = 0; }
        public async Task<TcpProbeResult> ConnectAsync(IPAddress address, int port, int timeoutMs, CancellationToken token)
        {
            string key = $"{address}:{port}";
            if (_endpoints.AddOrUpdate(key, 1, (_, count) => count + 1) > 1) Interlocked.Increment(ref _overlap);
            int active = Interlocked.Increment(ref _active), previous;
            do { previous = _maximum; } while (active > previous && Interlocked.CompareExchange(ref _maximum, active, previous) != previous);
            try { return await _real.ConnectAsync(address, port, timeoutMs, token); }
            finally { Interlocked.Decrement(ref _active); _endpoints.AddOrUpdate(key, 0, (_, count) => count - 1); }
        }
    }
    private sealed class MeasuredRealProbe : IIcmpProbe
    {
        private readonly IcmpPingProbe _real = new();
        private readonly ConcurrentDictionary<string, int> _hosts = new();
        private int _active, _maximum, _overlap;
        public int Active => Volatile.Read(ref _active);
        public int Maximum => Volatile.Read(ref _maximum);
        public int Overlap => Volatile.Read(ref _overlap);
        public void Reset()
        {
            if (Active != 0) throw new InvalidOperationException("Cannot reset measurement while requests are active.");
            _hosts.Clear(); _maximum = 0; _overlap = 0;
        }
        public async Task<ProbeResult> SendAsync(IPAddress address, int timeoutMs, int packetSize, int ttl, CancellationToken token)
        {
            string key = $"{address}:{ttl}";
            int perHost = _hosts.AddOrUpdate(key, 1, (_, value) => value + 1);
            if (perHost > 1) Interlocked.Increment(ref _overlap);
            int active = Interlocked.Increment(ref _active); int previous;
            do { previous = _maximum; } while (active > previous && Interlocked.CompareExchange(ref _maximum, active, previous) != previous);
            try { return await _real.SendAsync(address, timeoutMs, packetSize, ttl, token); }
            finally { Interlocked.Decrement(ref _active); _hosts.AddOrUpdate(key, 0, (_, value) => value - 1); }
        }
    }
}
