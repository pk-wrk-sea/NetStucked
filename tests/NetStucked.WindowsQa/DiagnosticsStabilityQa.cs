using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using NetStucked.Core;
using NetStucked.Desktop.Behaviors;
using NetStucked.Desktop.Services;
using NetStucked.Desktop.ViewModels;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;

internal static partial class Program
{
    private static async Task CheckDiagnosticsStabilityAsync(Window window, MainViewModel vm, QaDialogs dialogs, UserSettingsStore store, string output)
    {
        string previousTheme = vm.Appearance.SelectedTheme;
        vm.Appearance.SelectedTheme = "Dark";
        CheckHopTcpSettingsDialog();
        vm.NavigateCommand.Execute("Live Ping"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        vm.Ping.TargetText = "127.0.0.1 QA IP\nlocalhost QA host";
        await vm.Ping.StartCommand.ExecuteAsync(null);
        await Until(() => { vm.Ping.Refresh(true); return vm.Ping.Rows.Count == 2 && vm.Ping.Rows.All(r => r.Received > 0); });
        await vm.Ping.StopCommand.ExecuteAsync(null); vm.Ping.SelectedRow = vm.Ping.Rows.Single(r => r.Host == "localhost"); vm.Ping.Refresh(true);
        Require(vm.Ping.Rows.All(r => r.ReplyIpAddress == r.ResolvedIp) && vm.Ping.History.All(r => r.Host == "localhost" && r.ReplyIpAddress == "127.0.0.1"), "Live Ping and history use the actual loopback reply IP and exact selected host");
        var ping = Descendants<DataGrid>((DependencyObject)window.Content).Single(g => AutomationProperties.GetName(g) == "Realtime Results");
        var pingVisibility = ping.Columns.Select(c => c.Visibility).ToArray();
        // Previous preference tests deliberately hid Description; restore for text QA.
        foreach (var column in ping.Columns) column.Visibility = Visibility.Visible;
        window.UpdateLayout();
        Require(TableTextSupport.BuildText(ping).Contains("Reply IP Address") && TableTextSupport.BuildText(ping).Contains("127.0.0.1"), "Copy table includes new headers and actual reply IP data");
        CheckSelectableTable(ping);
        var textAction = ping.ContextMenu!.Items.Cast<MenuItem>().Single(i => i.Header.ToString()!.StartsWith("Select / copy"));
        Exception? failure = null;
        _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var dialog = Application.Current.Windows.Cast<Window>().Single(w => w.Title == "Table text — select and copy"); dialog.Opacity = 0;
            try
            {
                var text = (TextBox)dialog.Content; text.Select(0, text.Text.Length);
                Require(text.SelectedText.StartsWith("#\t") && text.SelectedText.Contains("localhost") && text.IsReadOnlyCaretVisible, "Themed table text supports a continuous selection across headers, columns and rows");
                Render(dialog, output, "Table-selectable-text-dark", 820, 520, 1);
            }
            catch (Exception ex) { failure = ex; }
            finally { dialog.Close(); }
        }));
        textAction.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); if (failure is not null) throw failure;
        Render(window, output, "LivePing-reply-ip-dark", 1536, 1024, 1);
        for (int i = 0; i < ping.Columns.Count; i++) ping.Columns[i].Visibility = pingVisibility[i];

        vm.NavigateCommand.Execute("Port Test"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var port = Descendants<DataGrid>((DependencyObject)window.Content).Single(g => AutomationProperties.GetName(g) == "TCP Port Test Results");
        var endpoint = port.Columns.Single(c => Equals(c.Header, "Endpoint")); endpoint.DisplayIndex = 2;
        _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var dialog = Application.Current.Windows.Cast<Window>().Single(w => w.Title == "Visible columns"); dialog.Opacity = 0;
            try
            {
                var option = Descendants<CheckBox>((DependencyObject)dialog.Content).Single(c => Equals(c.Content, "Description"));
                option.IsChecked = false; option.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                Require(port.Columns.Single(c => Equals(c.Header, "Description")).Visibility == Visibility.Collapsed, "Port column chooser applies checkbox visibility immediately");
            }
            catch (Exception ex) { failure = ex; }
            finally { dialog.Close(); }
        }));
        ColumnLayout.ShowChooser(port); if (failure is not null) throw failure;
        ColumnLayout.Capture(port); await store.SaveAsync();
        var loaded = new UserSettingsStore(store.DirectoryPath);
        Require(loaded.Preferences.Columns["Port"].Any(c => c.Key == "Description" && !c.Visible) && loaded.Preferences.Columns["Port"].Any(c => c.Key == "Endpoint" && c.Order == 2), "Port column order and visibility survive settings serialization");
        CheckSelectableTable(port);

        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); int tcpPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        vm.NavigateCommand.Execute("Traceroute"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        dialogs.NextTraceSettings = new() { IntervalMs = 250, TimeoutMs = 500, MaxHops = 5, CheckHopTcp = true, HopTcpPorts = tcpPort.ToString() };
        vm.Trace.SettingsCommand.Execute(null); Require(store.Preferences.Trace.CheckHopTcp, "Trace ViewModel preserves optional TCP settings instead of normalizing them away");
        vm.Trace.Target = "127.0.0.1"; await vm.Trace.StartCommand.ExecuteAsync(null);
        using var accepted = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await Until(() => { vm.Trace.Refresh(true); return vm.Trace.Rows.Any(r => r.TcpOpenPorts == tcpPort.ToString()); });
        await vm.Trace.StopCommand.ExecuteAsync(null);
        var trace = Descendants<DataGrid>((DependencyObject)window.Content).Single(g => AutomationProperties.GetName(g) == "Traceroute Results");
        Require(trace.Columns.Any(c => Equals(c.Header, "TCP open ports")) && TableTextSupport.BuildText(trace).Contains(tcpPort.ToString()), "Traceroute shows and copies the actual successful TCP port");
        CheckSelectableTable(trace);
        var events = Descendants<DataGrid>((DependencyObject)window.Content).Single(g => AutomationProperties.GetName(g) == "Trace Event Log"); CheckSelectableTable(events);
        Render(window, output, "Traceroute-hop-tcp-dark", 1536, 1024, 1);
        dialogs.NextTraceSettings = new() { IntervalMs = 250, TimeoutMs = 500, MaxHops = 5 }; vm.Trace.SettingsCommand.Execute(null);
        vm.Appearance.SelectedTheme = previousTheme;
    }

    private static void CheckSelectableTable(DataGrid grid)
    {
        grid.UpdateLayout();
        var cells = Descendants<TextBox>(grid).Where(t => t.IsVisible && GridTools.Ancestor<DataGridCell>(t) is not null && t.Text.Length > 0).ToArray();
        Require(cells.Length > 0 && cells.All(t => t.IsReadOnly && t.IsReadOnlyCaretVisible && t.Cursor == Cursors.IBeam), "Measured table cells expose selectable readonly text with a visible caret");
        Require(cells.All(t => Watermark.GetText(t) == ""), "Read-only measured cells never display editable-input placeholder hints");
        var cell = cells[0]; cell.Select(0, cell.Text.Length); Require(cell.SelectedText == cell.Text, "Cell text supports a selection for native copy");
        var headers = Descendants<TextBox>(grid).Where(t => GridTools.Ancestor<DataGridColumnHeader>(t) is { Column: not null } && t.Text.Length > 0).ToArray();
        Require(headers.Length > 0 && headers.All(t => t.IsReadOnlyCaretVisible), "Column headers also expose selectable text");
        var header = headers[0]; header.Select(0, header.Text.Length); Require(header.SelectedText == header.Text, "Header text selection retains the complete label");
        var columnHeader = GridTools.Ancestor<DataGridColumnHeader>(header)!;
        var oldSort = columnHeader.Column.SortDirection;
        TableTextSupport.InvokeHeader(columnHeader);
        grid.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Require(columnHeader.Column.SortDirection != oldSort, "Sortable headers retain their sorting action alongside text selection");
    }

    private static void CheckHopTcpSettingsDialog()
    {
        Exception? failure = null;
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var dialog = Application.Current.Windows.Cast<Window>().Single(w => w.Title == "Probe Settings"); dialog.Opacity = 0;
            try
            {
                var optional = Descendants<CheckBox>((DependencyObject)dialog.Content).Single();
                var ports = Descendants<TextBox>((DependencyObject)dialog.Content).Single(t => AutomationProperties.GetName(t) == "Optional hop TCP ports");
                Require(optional.IsChecked == false && !ports.IsEnabled && ports.Text == "22,23,80,443", "TCP hop checks are disabled by default with common port suggestions");
                optional.IsChecked = true; ports.Text = "1-17";
                var save = Descendants<Button>((DependencyObject)dialog.Content).Single(b => Equals(b.Content, "Save")); save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(dialog.IsVisible && Descendants<TextBlock>((DependencyObject)dialog.Content).Any(t => t.Text.Contains("1–16 unique ports")), "The actual Trace settings dialog rejects an oversized optional TCP scope");
                ports.Text = "22,443,8088"; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception ex) { failure = ex; dialog.Close(); }
        }));
        var settings = new WpfDialogService().EditTraceSettings(new()); if (failure is not null) throw failure;
        Require(settings is { CheckHopTcp: true, HopTcpPorts: "22,443,8088" }, "Trace settings accept and retain custom TCP ports");
    }
}
