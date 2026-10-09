using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Win32;
using NetStucked.Core;

namespace NetStucked.Desktop.Services;

public sealed class WpfDialogService : IDesktopDialogs
{
    public async Task<string?> LoadTargetsAsync()
    {
        var dialog = new OpenFileDialog { Title = "Load target list", Filter = "UTF-8 target lists (*.txt)|*.txt|All files|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(Application.Current.MainWindow) != true) return null;
        if (new FileInfo(dialog.FileName).Length > 1_000_000) throw new IOException("Target list exceeds 1 MB.");
        return await File.ReadAllTextAsync(dialog.FileName, Encoding.UTF8);
    }
    public Task SaveTargetsAsync(string text) => SaveAsync("targets.txt", "UTF-8 target lists (*.txt)|*.txt", text);
    public Task ExportAsync(string suggestedName, string csv) => SaveAsync(suggestedName, "CSV files (*.csv)|*.csv", csv);
    private static async Task SaveAsync(string name, string filter, string text)
    {
        var dialog = new SaveFileDialog { FileName = name, Filter = filter, OverwritePrompt = true, AddExtension = true };
        if (dialog.ShowDialog(Application.Current.MainWindow) != true) return;
        string temporary = dialog.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllTextAsync(temporary, text, new UTF8Encoding(true)); File.Move(temporary, dialog.FileName, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void ShowError(string message) => MessageBox.Show(Application.Current.MainWindow, message, "NetStucked", MessageBoxButton.OK, MessageBoxImage.Error);

    public string? AskTemplateName(string suggested)
    {
        string? result = null;
        ShowSettings("Save address template", [("Template name", suggested)], new StackPanel(), values =>
        {
            string name = values[0].Trim();
            if (name.Length is < 1 or > 80) throw new ArgumentException("Use a name of 1–80 characters.");
            result = name;
        });
        return result;
    }
    public bool ConfirmTemplateDeletion(string name) => MessageBox.Show(Application.Current.MainWindow,
        $"Delete the template '{name}'?", "Delete address template", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    public ProbeSettings? EditPingSettings(ProbeSettings settings)
    {
        ProbeSettings? result = null;
        ShowSettings("Probe Settings", [("Interval (ms, minimum 250)", settings.IntervalMs.ToString()),
            ("Timeout (ms, minimum 500)", settings.TimeoutMs.ToString()), ("Packet size (bytes)", settings.PacketSize.ToString())], new StackPanel(), values =>
        {
            var edited = settings with { IntervalMs = Int(values[0]), TimeoutMs = Int(values[1]), PacketSize = Int(values[2]) };
            edited.Validate(); result = edited;
        });
        return result;
    }
    public TraceSettings? EditTraceSettings(TraceSettings settings)
    {
        TraceSettings? result = null;
        ShowSettings("Probe Settings", [("Max hops", settings.MaxHops.ToString()), ("Interval (ms, minimum 250)", settings.IntervalMs.ToString()),
            ("Timeout (ms, minimum 500)", settings.TimeoutMs.ToString()), ("Packet size (bytes)", settings.PacketSize.ToString())], new StackPanel(), values =>
        {
            var edited = settings with { MaxHops = Int(values[0]), IntervalMs = Int(values[1]), TimeoutMs = Int(values[2]), PacketSize = Int(values[3]), Continuous = true };
            edited.Validate(); result = edited;
        });
        return result;
    }
    public PortProbeSettings? EditPortSettings(PortProbeSettings settings)
    {
        PortProbeSettings? result = null;
        ShowSettings("TCP Probe Settings", [("Interval (ms, minimum 250)", settings.IntervalMs.ToString()),
            ("Timeout (ms, minimum 500)", settings.TimeoutMs.ToString())], new StackPanel(), values =>
        {
            var edited = settings with { IntervalMs = Int(values[0]), TimeoutMs = Int(values[1]) };
            edited.Validate(); result = edited;
        });
        return result;
    }
    private static int Int(string value) => int.Parse(value, CultureInfo.InvariantCulture);
    private static double Double(string value) => double.Parse(value, CultureInfo.InvariantCulture);
    private static void ShowSettings(string title, (string Label, string Value)[] fields, FrameworkElement extra, Action<string[]> accept)
    {
        var window = new Window { Title = title, Width = 440, SizeToContent = SizeToContent.Height, MaxHeight = 760,
            Owner = Application.Current.MainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false };
        var panel = new StackPanel { Margin = new(20) };
        var editors = new List<TextBox>();
        foreach (var (label, value) in fields)
        {
            var editor = new TextBox { Text = value, Margin = new(0, 3, 0, 9) };
            AutomationProperties.SetName(editor, label);
            panel.Children.Add(new Label { Content = label, Target = editor, Padding = new(0) });
            panel.Children.Add(editor); editors.Add(editor);
        }
        panel.Children.Add(extra);
        var error = new TextBlock { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new(0, 6, 0, 6) };
        panel.Children.Add(error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 10, 0, 0) };
        var save = new Button { Content = "Save", IsDefault = true, MinWidth = 75 };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 75, Margin = new(8, 0, 0, 0) };
        save.Click += (_, _) =>
        {
            try { accept(editors.Select(e => e.Text).ToArray()); window.DialogResult = true; }
            catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException) { error.Text = ex.Message; }
        };
        actions.Children.Add(save); actions.Children.Add(cancel); panel.Children.Add(actions);
        window.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        window.ShowDialog();
    }
}

