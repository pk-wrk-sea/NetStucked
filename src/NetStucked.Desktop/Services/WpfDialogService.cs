using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Win32;
using NetStucked.Core;

namespace NetStucked.Desktop.Services;

public sealed partial class WpfDialogService : IDesktopDialogs
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
    public void ShowError(string message) => ShowMessage("NetStucked", message, false);

    public string? AskTemplateName(string suggested)
        => AskName(suggested, "Save address template");
    public string? AskPortTemplateName(string suggested)
        => AskName(suggested, "Save port template");
    private static string? AskName(string suggested, string title)
    {
        string? result = null;
        ShowSettings(title, [("Template name", suggested)], new StackPanel(), values =>
        {
            string name = values[0].Trim();
            if (name.Length is < 1 or > 80) throw new ArgumentException("Use a name of 1–80 characters.");
            result = name;
        });
        return result;
    }
    public bool ConfirmTemplateDeletion(string name) => ShowMessage("Confirm", $"Continue with '{name}'?", true);

    public HopDescriptionEdit? EditHopDescriptions(string text, bool wan)
    {
        HopDescriptionEdit? result = null;
        var editor = new TextBox { Text = text, AcceptsReturn = true, VerticalContentAlignment = VerticalAlignment.Top, TextWrapping = TextWrapping.NoWrap, Height = 280,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, MaxLength = 1_000_000 };
        Behaviors.Watermark.SetText(editor, "10.10.10.1 CSW-HQ\n192.168.1.1 Core Switch");
        AutomationProperties.SetName(editor, "IP addresses and shared hop descriptions");
        var checkbox = new CheckBox { Content = "Look up public WAN IP organization / ASN (RIPEstat)", IsChecked = wan, Margin = new(0, 12, 0, 0) };
        var extra = new StackPanel(); extra.Children.Add(new TextBlock { Text = "IP <space> Description · one per line · shared across all sessions", Margin = new(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
        extra.Children.Add(editor); extra.Children.Add(checkbox);
        ShowSettings("Hop Description", [], extra, _ => { HopDescriptionParser.Parse(editor.Text); result = new(editor.Text, checkbox.IsChecked == true); });
        return result;
    }
    public string? EditPortTemplate(string name, string numbers)
    {
        string? result = null;
        ShowSettings($"Edit {name}", [("Ports (comma separated or ranges, e.g. 443,1000-1005)", numbers)], new TextBlock { Text = "Ranges include both endpoints. Common ports based on IANA service assignments do not guarantee the service running there.", TextWrapping = TextWrapping.Wrap }, values =>
        {
            var parsed = PortScanPlanner.Parse("127.0.0.1", values[0], PortProtocol.TCP, false);
            if (!parsed.IsValid) throw new ArgumentException(string.Join(Environment.NewLine, parsed.Errors));
            result = string.Join(',', parsed.Targets.Select(t => t.Port));
        });
        return result;
    }
    private static bool ShowMessage(string title, string message, bool confirm)
    {
        var window = new Window { Title = title, Width = 460, SizeToContent = SizeToContent.Height, MaxHeight = 700, Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false };
        ThemeService.ApplyWindow(window);
        var panel = new StackPanel { Margin = new(20) }; panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 18, 0, 0) };
        var ok = new Button { Content = confirm ? "Confirm" : "OK", MinWidth = 80, IsDefault = !confirm };
        ok.Click += (_, _) => window.DialogResult = true; actions.Children.Add(ok);
        if (confirm) actions.Children.Add(new Button { Content = "Cancel", MinWidth = 80, IsCancel = true, IsDefault = true, Margin = new(8, 0, 0, 0) });
        panel.Children.Add(actions); window.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        return window.ShowDialog() == true;
    }

    public ProbeSettings? EditPingSettings(ProbeSettings settings)
    {
        ProbeSettings? result = null;
        ShowSettings("Probe Settings", [("Interval (ms, minimum 250)", settings.IntervalMs.ToString()),
            ("Timeout (ms, minimum 500)", settings.TimeoutMs.ToString()), ("Packet size (bytes)", settings.PacketSize.ToString())], new TextBlock { Text = "Sends are paced at up to 128/s, with separate Traceroute capacity. A large scope may run slower than the requested interval. Warn means current RTT ≥100 ms, recent ICMP loss ≥5%, or consecutive missing replies; it does not establish a service outage.", TextWrapping = TextWrapping.Wrap }, values =>
        {
            var edited = settings with { IntervalMs = Int(values[0]), TimeoutMs = Int(values[1]), PacketSize = Int(values[2]) };
            edited.Validate(); result = edited;
        });
        return result;
    }
    public TraceSettings? EditTraceSettings(TraceSettings settings)
    {
        TraceSettings? result = null;
        var tcpCheck = new CheckBox { Content = "Optional TCP check on responding hop IPs", IsChecked = settings.CheckHopTcp, Margin = new(0, 6, 0, 6) };
        var tcpPorts = new TextBox { Text = settings.HopTcpPorts, IsEnabled = settings.CheckHopTcp, MaxLength = 2048 };
        Behaviors.Watermark.SetText(tcpPorts, "22,23,80,443 or custom ports / ranges");
        AutomationProperties.SetName(tcpPorts, "Optional hop TCP ports");
        tcpCheck.Checked += (_, _) => tcpPorts.IsEnabled = true; tcpCheck.Unchecked += (_, _) => tcpPorts.IsEnabled = false;
        var extra = new StackPanel(); extra.Children.Add(tcpCheck); extra.Children.Add(tcpPorts);
        extra.Children.Add(new TextBlock { Text = "Direct TCP connects to each responding hop IP (no payload). Up to 16 ports; results refresh about every 30s. ICMP still discovers the route; this is not TCP traceroute.", TextWrapping = TextWrapping.Wrap, Margin = new(0, 8, 0, 0) });
        ShowSettings("Probe Settings", [("Max hops", settings.MaxHops.ToString()), ("Interval (ms, minimum 250)", settings.IntervalMs.ToString()),
            ("Timeout (ms, minimum 500)", settings.TimeoutMs.ToString()), ("Packet size (bytes)", settings.PacketSize.ToString())], extra, values =>
        {
            var edited = settings with { CheckHopTcp = tcpCheck.IsChecked == true, HopTcpPorts = tcpPorts.Text, MaxHops = Int(values[0]), IntervalMs = Int(values[1]), TimeoutMs = Int(values[2]), PacketSize = Int(values[3]), Continuous = true };
            edited.Validate(); result = edited;
        });
        return result;
    }
    public PortProbeSettings? EditPortSettings(PortProbeSettings settings)
    {
        PortProbeSettings? result = null;
        ShowSettings("Port Probe Settings", [("Interval (ms, minimum 250)", settings.IntervalMs.ToString()),
            ("Timeout (ms, minimum 500)", settings.TimeoutMs.ToString()), ("Packet size (payload bytes, 0–1400)", settings.PacketSize.ToString())], new TextBlock { Text = "TCP and UDP send zero-filled payloads. Size 0 tests TCP connection only or sends an empty UDP datagram.", TextWrapping = TextWrapping.Wrap }, values =>
        {
            var edited = settings with { IntervalMs = Int(values[0]), TimeoutMs = Int(values[1]), PacketSize = Int(values[2]), Continuous = true };
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
        ThemeService.ApplyWindow(window);
        var panel = new StackPanel { Margin = new(20) };
        var editors = new List<TextBox>();
        foreach (var (label, value) in fields)
        {
            var editor = new TextBox { Text = value, Margin = new(0, 3, 0, 9) };
            Behaviors.Watermark.SetText(editor, label);
            AutomationProperties.SetName(editor, label);
            panel.Children.Add(new Label { Content = label, Target = editor, Padding = new(0) });
            panel.Children.Add(editor); editors.Add(editor);
        }
        panel.Children.Add(extra);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new(0, 6, 0, 6) }; error.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
        foreach (var editor in editors) editor.TextChanged += (_, _) => error.Text = "";
        extra.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => error.Text = ""));
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

