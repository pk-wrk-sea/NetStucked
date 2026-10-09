using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NetStucked.Core;
using NetStucked.Desktop.Services;
using NetStucked.Desktop.ViewModels;

namespace NetStucked.WindowsQa;

internal static partial class Program
{
    private static async Task CheckDefectViewsAsync(Window window, MainViewModel vm, string output)
    {
        var root = (DependencyObject)window.Content;
        Require(!Descendants<TextBlock>(root).Any(t => t.IsVisible && t.Text == "NetStucked" && t.TransformToAncestor(window).Transform(new Point()).Y < 34), "Redundant NetStucked caption text is removed while window controls remain");
        foreach (string page in new[] { "Live Ping", "Port Test", "Traceroute" })
        {
            vm.NavigateCommand.Execute(page); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            window.Width = 1280; window.Height = 800; window.UpdateLayout();
            Require(Descendants<TextBox>(root).Where(t => t.IsVisible).All(t => SameColor(t.CaretBrush, Application.Current.Resources["TextBrush"])), page + " dark caret uses the readable theme text color");
            if (page != "Traceroute")
            {
                var save = Descendants<Button>(root).Single(b => b.IsVisible && AutomationProperties.GetName(b) == "Save address template");
                var settings = Descendants<Button>(root).Single(b => b.IsVisible && AutomationProperties.GetName(b) == "Probe Settings");
                double right = save.TransformToAncestor(window).Transform(new Point(save.ActualWidth, 0)).X;
                double left = settings.TransformToAncestor(window).Transform(new Point()).X;
                Require(save.ActualWidth >= 35.5 && left - right >= 7.5, page + " Save button retains full width and an eight-pixel settings gap");
            }
            else
            {
                var primary = vm.TraceWorkspace.SelectedSession;
                while (vm.TraceWorkspace.Sessions.Count < 5)
                {
                    vm.TraceWorkspace.AddSessionCommand.Execute(null);
                    vm.TraceWorkspace.SelectedSession.Target = $"127.0.0.{vm.TraceWorkspace.Sessions.Count}";
                }
                vm.TraceWorkspace.SelectedSession = primary; window.UpdateLayout();
                var sessions = Descendants<ListBox>(root).Single(l => AutomationProperties.GetName(l) == "Traceroute sessions");
                Require(Descendants<TextBlock>(sessions).Where(t => vm.TraceWorkspace.Sessions.Any(s => s.Title == t.Text)).All(t => SameColor(t.Foreground, Application.Current.Resources["TextBrush"])), "Actual session title elements use the dark text color");
                var hop = Descendants<Button>(root).Single(b => b.IsVisible && AutomationProperties.GetName(b) == "Edit shared hop descriptions");
                var filter = Descendants<ComboBox>(root).Single(c => c.IsVisible && AutomationProperties.GetName(c) == "Traceroute status filter");
                Require(ReferenceEquals(hop.Command, vm.TraceWorkspace.HopDescriptionsCommand) && ReferenceEquals(hop.Parent, filter.Parent) && ((Panel)hop.Parent).Children.IndexOf(hop) < ((Panel)filter.Parent).Children.IndexOf(filter), "One shared Hop Description command appears directly before the result status filter");
                Render(window, output, "Traceroute-five-sessions-dark", 1280, 800, 1);
                foreach (var extra in vm.TraceWorkspace.Sessions.Where(s => !s.IsPrimary).ToArray()) await vm.TraceWorkspace.RemoveSessionCommand.ExecuteAsync(extra);
                var grid = Descendants<DataGrid>(root).Single(g => g.IsVisible && AutomationProperties.GetName(g) == "Traceroute Results");
                grid.SelectedItem = primary.Rows[0]; grid.CurrentCell = new DataGridCellInfo(primary.Rows[0], grid.Columns[3]);
                Require(grid.BeginEdit(), "Dark Description cell enters actual editing"); window.UpdateLayout();
                Require(Descendants<TextBox>(grid).Any(t => t.IsVisible && SameColor(t.CaretBrush, Application.Current.Resources["TextBrush"])), "Generated Description cell editor also has a readable dark caret");
                grid.CancelEdit(DataGridEditingUnit.Row); grid.SelectedItem = null;
            }
            Render(window, output, page.Replace(" ", "") + "-defects-dark", 1280, 800, 1);
        }
        vm.NavigateCommand.Execute("Port Test"); window.UpdateLayout();
        Require(vm.Port.MultipleTargets && vm.Port.Continuous && !Descendants<CheckBox>(root).Any(c => c.IsVisible && (Equals(c.Content, "Multiple IP Scan") || Equals(c.Content, "Continuous test"))), "Multiple-target continuous Port Test is always enabled with both checkboxes removed");
        vm.Appearance.SelectedTheme = "Light"; window.UpdateLayout();
        Require(Descendants<TextBox>(root).Where(t => t.IsVisible).All(t => SameColor(t.CaretBrush, Application.Current.Resources["TextBrush"])), "Existing carets also follow a runtime switch back to Light");
        vm.Appearance.SelectedTheme = "Dark";
        CheckPortPayloadEditor(output);
        CheckPortRangeEditor(output);
        vm.NavigateCommand.Execute("Live Ping");
    }
    private static bool SameColor(object? first, object? second) => first is SolidColorBrush a && second is SolidColorBrush b && a.Color == b.Color;
    private static void CheckPortRangeEditor(string output)
    {
        Exception? failure = null;
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var dialog = Application.Current.Windows.Cast<Window>().Single(w => w.Title == "Edit Range QA"); dialog.Opacity = 0;
            try
            {
                var editor = Descendants<TextBox>((DependencyObject)dialog.Content).Single(); editor.Text = "1000-1005,443,1003";
                SaveDialogRender(dialog, output, "PortTemplate-ranges-dark");
                Descendants<Button>((DependencyObject)dialog.Content).Single(b => Equals(b.Content, "Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception ex) { failure = ex; dialog.DialogResult = false; }
        }));
        string? result = new WpfDialogService().EditPortTemplate("Range QA", ""); if (failure is not null) throw failure;
        Require(result == "1000,1001,1002,1003,1004,1005,443", "Actual port template editor expands ranges inclusively and deduplicates overlap");
    }
    private static void CheckPortPayloadEditor(string output)
    {
        Exception? failure = null;
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var dialog = Application.Current.Windows.Cast<Window>().Single(w => w.Title == "Port Probe Settings"); dialog.Opacity = 0;
            try
            {
                var editors = Descendants<TextBox>((DependencyObject)dialog.Content).ToArray();
                Require(editors.Length == 3 && editors.All(t => SameColor(t.CaretBrush, Application.Current.Resources["TextBrush"])), "Port settings payload input and all popup carets follow Dark");
                editors[2].Text = "1401";
                var save = Descendants<Button>((DependencyObject)dialog.Content).Single(b => Equals(b.Content, "Save"));
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Require(dialog.IsVisible, "Oversized payload cannot be saved");
                editors[2].Text = "128"; SaveDialogRender(dialog, output, "PortProbeSettings-payload-dark"); save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception ex) { failure = ex; dialog.DialogResult = false; }
        }));
        var settings = new WpfDialogService().EditPortSettings(new()); if (failure is not null) throw failure;
        Require(settings?.PacketSize == 128 && settings.Continuous, "Actual settings dialog accepts TCP/UDP payload size and preserves continuous mode");
    }
}
