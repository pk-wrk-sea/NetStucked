using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NetStucked.Core;
using NetStucked.Desktop.Services;
using NetStucked.Desktop.ViewModels;

namespace NetStucked.WindowsQa;

internal static partial class Program
{
    private static async Task CheckDnsHttpAsync(Window window, MainViewModel vm, QaDialogs dialogs, string output)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        using var stop = new CancellationTokenSource();
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var dnsEndpoint = (IPEndPoint)udp.Client.LocalEndPoint!;
        Task dnsServer = Task.Run(async () =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var received = await udp.ReceiveAsync(stop.Token); byte[] query = received.Buffer;
                    ushort type = BinaryPrimitives.ReadUInt16BigEndian(query.AsSpan(query.Length - 4));
                    byte[] value = type == 28 ? IPAddress.IPv6Loopback.GetAddressBytes() : new byte[] { 127, 0, 0, 1 };
                    byte[] response = query.Concat(new byte[] { 0xc0, 12, 0, (byte)type, 0, 1, 0, 0, 0, 30, 0, (byte)value.Length }).Concat(value).ToArray(); response[2] = 0x81; response[3] = 0x80; response[7] = 1;
                    await udp.SendAsync(response, received.RemoteEndPoint, stop.Token);
                }
            }
            catch (OperationCanceledException) { }
        });
        var httpListener = new TcpListener(IPAddress.Loopback, 0); httpListener.Start(); int httpPort = ((IPEndPoint)httpListener.LocalEndpoint).Port;
        Task httpServer = Task.Run(async () =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    using var client = await httpListener.AcceptTcpClientAsync(stop.Token); using var stream = client.GetStream();
                    var text = new StringBuilder(); byte[] b = new byte[1];
                    while (text.Length < 4096 && !text.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal)) { if (await stream.ReadAsync(b, stop.Token) == 0) break; text.Append((char)b[0]); }
                    string status = text.ToString().Contains("/redirect ") ? "302 Found\r\nLocation: /ok" : "200 OK";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Length: 100000000\r\nConnection: close\r\n\r\n"), stop.Token);
                }
            }
            catch (OperationCanceledException) { }
        });
        try
        {
            vm.Dns.UseSystemResolvers = false; vm.Dns.ResolverText = dnsEndpoint.ToString(); vm.Dns.TargetText = "first.example\nsecond.example"; vm.Dns.QueryAaaa = true;
            vm.NavigateCommand.Execute("DNS Test"); await vm.Dns.StartCommand.ExecuteAsync(null);
            Require(vm.Dns.Results.Count == 4 && vm.Dns.Results.All(r => r.Status == "PASS" && r.ElapsedMs >= 0), "DNS WPF displays actual owned UDP A/AAAA queries, resolver, answer and timing");
            await vm.Dns.ExportCommand.ExecuteAsync(null); Require(dialogs.Exports.Last().Contains("first.example") && dialogs.Exports.Last().Contains(dnsEndpoint.ToString()), "DNS CSV exports the actual query and resolver");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            foreach (string theme in new[] { "Light", "Dark" }) { vm.Appearance.SelectedTheme = theme; Render(window, output, "DnsTest-owned-" + theme.ToLowerInvariant(), 1536, 1024, 1); Render(window, output, "DnsTest-owned-" + theme.ToLowerInvariant() + "-1280", 1280, 800, 1); }
            vm.Http.TargetText = $"http://127.0.0.1:{httpPort}/ok\nhttp://127.0.0.1:{httpPort}/redirect";
            vm.NavigateCommand.Execute("HTTP / HTTPS Test"); await vm.Http.StartCommand.ExecuteAsync(null);
            Require(vm.Http.Results.Count == 2 && vm.Http.Results.Any(r => r.StatusCode == 200 && r.Status == "PASS") && vm.Http.Results.Any(r => r.Status == "Redirect"), "HTTP WPF displays actual owned headers, status/redirect and timing without response body downloads");
            await vm.Http.ExportCommand.ExecuteAsync(null); Require(dialogs.Exports.Last().Contains("Redirect") && dialogs.Exports.Last().Contains("127.0.0.1"), "HTTP CSV exports actual outcomes and addresses");
            foreach (string theme in new[] { "Light", "Dark" }) { vm.Appearance.SelectedTheme = theme; Render(window, output, "HttpTest-owned-" + theme.ToLowerInvariant(), 1536, 1024, 1); Render(window, output, "HttpTest-owned-" + theme.ToLowerInvariant() + "-1280", 1280, 800, 1); }
            var grid = Descendants<DataGrid>((DependencyObject)window.Content).Single(g => System.Windows.Automation.AutomationProperties.GetName(g) == "Http test results");
            Require(grid.Columns.OfType<NetStucked.Desktop.Behaviors.CopyableTextColumn>().Any() && Descendants<TextBox>(grid).Any(t => t.IsReadOnly && t.IsReadOnlyCaretVisible), "New diagnostic tables use selectable/copyable text");
            vm.Http.ToggleAddressesCommand.Execute(null); vm.Http.ToggleDetailsCommand.Execute(null); await Task.Delay(250); Render(window, output, "HttpTest-collapsed-dark", 1280, 800, 1); vm.Http.ToggleAddressesCommand.Execute(null); vm.Http.ToggleDetailsCommand.Execute(null);
            vm.NavigateCommand.Execute("Updates"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            var versions = Descendants<TextBlock>((DependencyObject)window.Content).Where(t => t.IsVisible && t.Text.StartsWith("v0.", StringComparison.Ordinal)).ToArray();
            Require(versions.Length > 1 && versions.All(t => t.Foreground is SolidColorBrush b && b.Color != Colors.Black), "Version History and current/sidebar version labels are readable in actual Dark theme");
            Render(window, output, "Updates-version-dark", 1280, 800, 1);
            var updatesView = Descendants<NetStucked.Desktop.Views.UpdatesView>((DependencyObject)window.Content).Single();
            var updatesScroll = (ScrollViewer)updatesView.Content; updatesScroll.ScrollToEnd(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Render(window, output, "Updates-version-history-dark", 1280, 800, 1); updatesScroll.ScrollToHome();
            foreach (string page in new[] { "Dashboard", "Monitoring", "Terminal", "Config Collector", "Topology Explorer", "MA Inventory", "CVE & Upgrade Planner" }) { vm.NavigateCommand.Execute(page); Require(vm.CurrentPage is PlaceholderViewModel { Message: "Pending - Coming Soon" }, "Pending navigation: " + page); }
            Render(window, output, "Pending-page-dark", 1280, 800, 1);
            var hwnd = new WindowInteropHelper(window).Handle;
            for (int size = 0; size < 2; size++)
            {
                IntPtr icon = GetNativeWindowIcon(hwnd, 0x7f, new IntPtr(size), IntPtr.Zero); Require(icon != IntPtr.Zero, "Native branded taskbar/Alt-Tab icon handle exists at size " + size);
                var bitmap = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, size == 0 ? "taskbar-native-small.png" : "taskbar-native-large.png")); png.Save(file);
            }
            // Cancel during a real pending UDP read and await both presentation and transport work.
            using var sink = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)); vm.Dns.ResolverText = sink.Client.LocalEndPoint!.ToString()!; vm.Dns.TimeoutText = "60000";
            Task running = vm.Dns.StartCommand.ExecuteAsync(null); await Task.Delay(50); await vm.PrepareForUpdateAsync(); await running;
            Require(!vm.Dns.IsRunning && !vm.Http.IsRunning, "Update preparation cancels and drains new DNS/HTTP sessions");
            await File.WriteAllTextAsync(Path.Combine(output, "dns-http-qa.json"), System.Text.Json.JsonSerializer.Serialize(new { Status = "PASS", ActualDnsQueries = 4, ActualHttpResponses = 2, DarkVersionLabels = versions.Length, NativeWindowIcons = 2, UpdateDrain = true, WifiMutations = 0, Seconds = clock.Elapsed.TotalSeconds }));
        }
        finally { await stop.CancelAsync(); await Task.WhenAll(dnsServer, httpServer); httpListener.Stop(); }
    }
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] private static extern IntPtr GetNativeWindowIcon(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
