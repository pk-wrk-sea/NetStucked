using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using NetStucked.Core;
using NetStucked.Desktop.Behaviors;
using NetStucked.Desktop.Services;
using NetStucked.Desktop.ViewModels;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;

internal static partial class Program
{
    private static async Task<int> ReadWanMetadataAsync(string text, string path)
    {
        using var source = new RipeWanIdentitySource(); var address = IPAddress.Parse(text);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var result = await source.LookupAsync(address, timeout.Token);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(new { Address = text, Identity = result, Source = "Actual RIPEstat data API through production adapter", ProbePacketsSent = 0 }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Require(result is not null, "Actual RIPEstat metadata read returned organization / ASN; no probe packets were sent");
        return 0;
    }
    private static void SetPortFarm(PortTestViewModel vm, string endpoints)
    {
        var parsed = PortInputParser.Parse(endpoints); Require(parsed.IsValid && parsed.Targets.All(t => t.Host == "127.0.0.1"), "TCP QA farm contains only owned loopback endpoints");
        vm.Protocol = PortProtocol.TCP; vm.TargetText = "127.0.0.1 TCP QA";
        vm.PortNumbers = string.Join(',', parsed.Targets.Select(t => t.Port).Distinct()); vm.MultipleTargets = true; vm.Continuous = true;
    }
    private static async Task CheckBrandingScanAsync(Window window, MainViewModel vm, QaDialogs dialogs, ServiceProvider provider, string output)
    {
        await CheckMetadataCacheAsync(output);
        var workspace = vm.TraceWorkspace; var primary = workspace.Sessions.Single();
        Require(primary.IsPrimary, "The initial trace session is the protected main session");
        await workspace.RemoveSessionCommand.ExecuteAsync(primary); Require(workspace.Sessions.Count == 1, "Main trace session cannot be deleted even through its command");
        workspace.AddSessionCommand.Execute(null); Require(workspace.Sessions.Count == 2 && !workspace.AddSessionCommand.CanExecute(null), "A blank new session disables adding another");
        workspace.AddSessionCommand.Execute(null); Require(workspace.Sessions.Count == 2, "Direct Add command also respects an incomplete session");
        workspace.SelectedSession.Target = "bad host /"; Require(!workspace.AddSessionCommand.CanExecute(null), "Invalid destination keeps Add disabled");
        while (workspace.Sessions.Count < 5) { workspace.SelectedSession.Target = $"127.0.0.{workspace.Sessions.Count}"; workspace.AddSessionCommand.Execute(null); }
        workspace.SelectedSession.Target = "127.0.0.5";
        Require(!workspace.AddSessionCommand.CanExecute(null), "Five total sessions is the workspace limit");
        workspace.AddSessionCommand.Execute(null); Require(workspace.Sessions.Count == 5, "Direct Add cannot exceed five sessions");
        vm.NavigateCommand.Execute("Traceroute"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Render(window, output, "Traceroute-five-sessions-light", 1536, 1024, 1);
        foreach (var session in workspace.Sessions.Where(s => !s.IsPrimary).ToArray()) await workspace.RemoveSessionCommand.ExecuteAsync(session);
        Require(workspace.Sessions.Count == 1 && workspace.SelectedSession == primary, "Removing added sessions preserves the main session");

        var descriptions = provider.GetRequiredService<HopDescriptionService>();
        await descriptions.SaveAsync("127.0.0.1 CSW-HQ ไทย", false);
        primary.Target = "127.0.0.1"; await primary.StartCommand.ExecuteAsync(null);
        workspace.AddSessionCommand.Execute(null); var second = workspace.SelectedSession; second.Target = "127.0.0.1"; await second.StartCommand.ExecuteAsync(null);
        await Until(() => { primary.Refresh(true); second.Refresh(true); return primary.Rows.Count == 1 && second.Rows.Count == 1 && primary.Rows[0].Description == "CSW-HQ ไทย" && second.Rows[0].Description == "CSW-HQ ไทย"; });
        await descriptions.SaveAsync("127.0.0.1 Updated-HQ", false);
        Require(primary.Rows[0].Description == "Updated-HQ" && second.Rows[0].Description == "Updated-HQ", "Changing the IP mapping updates all existing sessions immediately");
        await second.ExportCommand.ExecuteAsync(null); Require(dialogs.Exports.Last().Contains("Updated-HQ"), "Shared hop description is included in actual CSV export");
        await descriptions.SaveAsync("", false); Require(primary.Rows[0].Description != "Updated-HQ", "Removing a shared mapping removes its displayed value");
        await second.StopCommand.ExecuteAsync(null); await primary.StopCommand.ExecuteAsync(null); await workspace.RemoveSessionCommand.ExecuteAsync(second);

        vm.Appearance.SelectedTheme = "Dark";
        Require(ThemeService.CurrentTheme == "Dark" && ((SolidColorBrush)Application.Current.Resources["SurfaceBrush"]).Color == Color.FromRgb(24, 34, 48), "Dark theme changes actual WPF brush resources");
        vm.NavigateCommand.Execute("Live Ping"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        vm.Ping.TargetText = "";
        Require(Descendants<TextBox>((DependencyObject)window.Content).Any(t => Watermark.GetText(t) == "Host, IP or CIDR <space> Description is optional"), "Ping textbox has the exact requested faint input guide");
        Render(window, output, "LivePing-dark", 1536, 1024, 1);
        vm.ToggleSidebarCommand.Execute(null); vm.Ping.ToggleAddressesCommand.Execute(null); vm.Ping.ToggleHistoryCommand.Execute(null);
        await Task.Delay(300); window.UpdateLayout();
        var slideGrids = Descendants<Grid>((DependencyObject)window.Content).Where(g => g.IsVisible && g.ReadLocalValue(SlideTrack.IsExpandedProperty) != DependencyProperty.UnsetValue).ToArray();
        Require(slideGrids.Where(g => !SlideTrack.GetIsExpanded(g)).All(g => SlideTrack.GetAxis(g) == Orientation.Horizontal
            ? Math.Abs(g.ColumnDefinitions[SlideTrack.GetIndex(g)].ActualWidth - 64) < 2
            : Math.Abs(g.RowDefinitions[SlideTrack.GetIndex(g)].ActualHeight - 64) < 2), "Collapsed menu, addresses and history reach their compact dimensions");
        Require(Descendants<Button>((DependencyObject)window.Content).Single(b => AutomationProperties.GetName(b) == "Toggle slide bar: main menu").IsVisible, "Main menu toggle stays available when the sidebar is folded");
        Render(window, output, "LivePing-collapsed-dark", 1280, 800, 1);
        vm.ToggleSidebarCommand.Execute(null); vm.Ping.ToggleAddressesCommand.Execute(null); vm.Ping.ToggleHistoryCommand.Execute(null); await Task.Delay(300);
        Require(slideGrids.Where(g => SlideTrack.GetIsExpanded(g)).All(g => SlideTrack.GetAxis(g) == Orientation.Horizontal
            ? g.ColumnDefinitions[SlideTrack.GetIndex(g)].ActualWidth > 64 : g.RowDefinitions[SlideTrack.GetIndex(g)].ActualHeight > 64), "Expanded panels restore their usable dimensions");
        await CheckPanelResizeAsync(window);

        CheckThemedEditors(output);
        vm.NavigateCommand.Execute("Settings"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Require(Descendants<Image>((DependencyObject)window.Content).Any(i => i.Source == BrandAssets.Mark), "Original logo mark appears in Settings and the main sidebar");
        Render(window, output, "Settings-dark", 1280, 800, 1);
        vm.NavigateCommand.Execute("Traceroute"); primary.Target = ""; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Require(Descendants<TextBox>((DependencyObject)window.Content).Any(t => Watermark.GetText(t) == "Input Host or IP"), "Editable trace input inherits its host/IP guide");
        primary.ToggleEventsCommand.Execute(null); await Task.Delay(300); Render(window, output, "Traceroute-events-collapsed-dark", 1280, 800, 1); primary.ToggleEventsCommand.Execute(null); await Task.Delay(300);
        Render(window, output, "Traceroute-dark", 1536, 1024, 1);

        vm.NavigateCommand.Execute("Port Test"); var port = vm.Port;
        port.Protocol = PortProtocol.TCP; port.MultipleTargets = true; port.TargetText = "192.168.0.0/22"; port.PortNumbers = "22,443,80,25,65525,50000";
        Require(port.RequiresScopeAcknowledgement && !port.StartCommand.CanExecute(null) && port.TargetPreview.Contains("6,132"), "A /22 × six ports is previewed but blocked before acknowledgement");
        port.ScopeAcknowledged = true; Require(port.StartCommand.CanExecute(null), "Explicit acknowledgement enables the current valid large scope");
        port.PortNumbers = "443"; Require(!port.ScopeAcknowledged && !port.RequiresScopeAcknowledgement, "Editing scope resets acknowledgement");
        // No private network scan is launched by this test; only the preview parser was exercised.
        port.SelectedPortTemplate = "Standard TCP ports"; port.PortNumbers = "22,443"; dialogs.TemplateName = "Standard TCP ports";
        await port.SavePortNumbersCommand.ExecuteAsync(null); Require(port.Store.Preferences.PortNumberTemplates["Standard TCP ports"] == "22,443", "Port template saves edited values");
        await port.RestorePortTemplateCommand.ExecuteAsync(null); Require(port.PortNumbers == PortScanPlanner.StandardTemplates["Standard TCP ports"], "Restore returns a built-in port template to its default ports");

        using var responding = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp); responding.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        using var silent = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp); silent.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var response = Task.Run(async () => { var buffer = new byte[16]; var received = await responding.ReceiveFromAsync(buffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0), lifetime.Token); await responding.SendToAsync(new byte[] { 1 }, SocketFlags.None, received.RemoteEndPoint, lifetime.Token); });
        port.TargetText = "127.0.0.1 UDP loopback QA"; port.PortNumbers = $"{((IPEndPoint)responding.LocalEndPoint!).Port},{((IPEndPoint)silent.LocalEndPoint!).Port}";
        port.Protocol = PortProtocol.UDP; port.MultipleTargets = true; port.Continuous = false;
        await port.StartCommand.ExecuteAsync(null);
        await Until(() => { port.Refresh(true); return port.State == SessionState.Stopped && port.Sent == 2; }); await response;
        Require(port.Reachable == 1 && port.NoResponse == 1 && port.Unreachable == 0 && port.Rows.Single(r => r.Status == "No response").Last is null, "Actual UDP reply and silent socket stay distinct, with no invented RTT or closed-port claim");
        await Task.Delay(600); port.Refresh(); Require(port.Sent == 2, "Unchecked Continuous completes exactly one pass");
        port.SelectedRow = port.Rows.Single(r => r.Status == "No response"); await port.ExportCommand.ExecuteAsync(null);
        Require(dialogs.Exports.Last().Contains("UDP") && dialogs.Exports.Last().Contains("No response"), "Port CSV records protocol and inconclusive outcome");
        Render(window, output, "PortTest-udp-dark", 1536, 1024, 1); Render(window, output, "PortTest-udp-125pct", 1280, 800, 1.25); Render(window, output, "PortTest-udp-150pct", 1280, 800, 1.5);
        port.ToggleAddressesCommand.Execute(null); port.ToggleHistoryCommand.Execute(null); await Task.Delay(300); Render(window, output, "PortTest-collapsed-dark", 1280, 800, 1); port.ToggleAddressesCommand.Execute(null); port.ToggleHistoryCommand.Execute(null); await Task.Delay(300);
        vm.Appearance.SelectedTheme = "Light"; port.Protocol = PortProtocol.TCP;
        vm.NavigateCommand.Execute("Live Ping"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Console.WriteLine("PASS Branding, dark theme, panel toggles, shared aliases, five-session guard, scan acknowledgement, templates and actual one-pass UDP");
    }
    private static async Task CheckPanelResizeAsync(Window window)
    {
        window.UpdateLayout();
        var horizontal = Descendants<GridSplitter>((DependencyObject)window.Content).Single(s => s.IsVisible && AutomationProperties.GetName(s) == "Resize addresses panel");
        var left = (Grid)horizontal.Parent; var originalLeft = left.ColumnDefinitions[0].Width; var originalRight = left.ColumnDefinitions[2].Width; double width = left.ColumnDefinitions[0].ActualWidth;
        horizontal.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
        horizontal.RaiseEvent(new DragDeltaEventArgs(20, 0) { RoutedEvent = Thumb.DragDeltaEvent });
        horizontal.RaiseEvent(new DragCompletedEventArgs(20, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Require(Math.Abs(left.ColumnDefinitions[0].ActualWidth - width - 20) < 2, "Actual address-panel splitter moves its grid boundary horizontally");
        left.ColumnDefinitions[0].Width = originalLeft; left.ColumnDefinitions[2].Width = originalRight;
        var vertical = Descendants<GridSplitter>((DependencyObject)window.Content).Single(s => s.IsVisible && AutomationProperties.GetName(s) == "Resize history panel");
        var right = (Grid)vertical.Parent; var resultHeight = right.RowDefinitions[2].Height; var historyHeight = right.RowDefinitions[4].Height; double height = right.RowDefinitions[2].ActualHeight;
        vertical.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
        vertical.RaiseEvent(new DragDeltaEventArgs(0, 20) { RoutedEvent = Thumb.DragDeltaEvent });
        vertical.RaiseEvent(new DragCompletedEventArgs(0, 20, false) { RoutedEvent = Thumb.DragCompletedEvent });
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Require(Math.Abs(right.RowDefinitions[2].ActualHeight - height - 20) < 2, "Actual history splitter moves the results/history boundary vertically");
        right.RowDefinitions[2].Height = resultHeight; right.RowDefinitions[4].Height = historyHeight;
    }
    private static async Task CheckMetadataCacheAsync(string output)
    {
        var store = new UserSettingsStore(Path.Combine(output, "metadata-cache-test")); var source = new MetadataFixture();
        await using var metadata = new HopDescriptionService(store, source);
        Require(metadata.Resolve("192.168.1.1") == "" && source.Calls == 0, "Private hops never invoke the WAN adapter");
        await metadata.SaveAsync("1.1.1.1 Manual HQ", true);
        Require(metadata.Resolve("1.1.1.1") == "Manual HQ" && source.Calls == 0, "Manual IP descriptions take priority and avoid a WAN request");
        await metadata.SaveAsync("", true);
        for (int i = 0; i < 20; i++) metadata.Resolve("1.1.1.1");
        await Until(() => metadata.Resolve("1.1.1.1") == "Fixture organization · AS64500");
        Require(source.Calls == 1, "Repeated IP lookup across sessions shares one cached request");
        await metadata.SaveAsync("1.1.1.1 Manual override", true);
        Require(metadata.Resolve("1.1.1.1") == "Manual override" && source.Calls == 1, "Manual mapping overrides a cached WAN identity");
        await metadata.SaveAsync("", true); source.Block = true;
        for (int i = 1; i <= 64; i++) metadata.Resolve($"8.8.8.{i}");
        await Until(() => source.Active == 2);
        await metadata.CancelPendingAsync(); int requests = source.Calls;
        Require(source.Active == 0 && source.Maximum <= 2 && requests <= 3, "Metadata cancellation drains two active requests and discards queued work");
        metadata.Resolve("9.9.9.9"); await Task.Delay(50); Require(source.Calls == requests, "Metadata remains suspended during installer handoff");
    }
    private sealed class MetadataFixture : IWanIdentitySource
    {
        public int Calls, Active, Maximum; public bool Block;
        public async Task<WanIdentity?> LookupAsync(IPAddress address, CancellationToken token)
        {
            Interlocked.Increment(ref Calls); int active = Interlocked.Increment(ref Active); Maximum = Math.Max(Maximum, active);
            try { await Task.Delay(Block ? Timeout.Infinite : 30, token); return new("Fixture organization · AS64500", DateTimeOffset.UtcNow); }
            finally { Interlocked.Decrement(ref Active); }
        }
    }
    private static void CheckThemedEditors(string output)
    {
        Exception? failure = null; var dialogs = new WpfDialogService();
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var dialog = Application.Current.Windows.Cast<Window>().Single(w => w.Title == "Hop Description"); dialog.Opacity = 0;
            try
            {
                Require(ReferenceEquals(dialog.Background, Application.Current.Resources["CanvasBrush"]), "Hop Description popup uses the current dark palette");
                var editor = Descendants<TextBox>((DependencyObject)dialog.Content).Single(); Require(editor.AcceptsReturn, "Hop Description is a multiline textbox");
                editor.Text = "10.10.10.1"; var save = Descendants<Button>((DependencyObject)dialog.Content).Single(b => Equals(b.Content, "Save")); save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(dialog.IsVisible && Descendants<TextBlock>((DependencyObject)dialog.Content).Any(t => t.Text.Contains("Line 1")), "Invalid mapping stays open with a line-specific error");
                editor.Text = "10.10.10.1 CSW-HQ\n192.168.1.1 Core Switch"; SaveDialogRender(dialog, output, "HopDescription-dark"); save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception ex) { failure = ex; dialog.DialogResult = false; }
        }));
        var result = dialogs.EditHopDescriptions("", true); if (failure is not null) throw failure;
        Require(result?.Text.Contains("10.10.10.1 CSW-HQ") == true, "Popup accepts the requested space-separated mapping format");
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var dialog = Application.Current.Windows.Cast<Window>().Single(w => w.Title == "Probe Settings"); dialog.Opacity = 0;
            try { Require(ReferenceEquals(dialog.Background, Application.Current.Resources["CanvasBrush"]), "Probe Settings popup follows the current dark theme"); SaveDialogRender(dialog, output, "ProbeSettings-dark"); }
            catch (Exception ex) { failure = ex; } finally { dialog.DialogResult = false; }
        }));
        dialogs.EditPingSettings(new()); if (failure is not null) throw failure;
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var dialog = Application.Current.Windows.Cast<Window>().Single(w => w.Title == "Visible columns"); dialog.Opacity = 0;
            try { Require(ReferenceEquals(dialog.Background, Application.Current.Resources["CanvasBrush"]), "Column chooser follows the current dark palette"); SaveDialogRender(dialog, output, "ConfigureColumns-dark"); }
            catch (Exception ex) { failure = ex; } finally { dialog.Close(); }
        }));
        var grid = Descendants<DataGrid>((DependencyObject)Application.Current.MainWindow.Content).Single(g => AutomationProperties.GetName(g) == "Realtime Results");
        ColumnLayout.ShowChooser(grid); if (failure is not null) throw failure;
    }
    private static void SaveDialogRender(Window window, string output, string name)
    {
        window.UpdateLayout(); var root = (FrameworkElement)window.Content; var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
        var painted = new DrawingVisual(); using (var drawing = painted.RenderOpen()) { drawing.DrawRectangle(window.Background, null, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight)); drawing.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight)); }
        var complete = new RenderTargetBitmap(bitmap.PixelWidth, bitmap.PixelHeight, 96, 96, PixelFormats.Pbgra32); complete.Render(painted);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(complete)); using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
    }
}
