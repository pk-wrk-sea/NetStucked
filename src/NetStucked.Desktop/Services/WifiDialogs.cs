using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using Microsoft.Win32;
using NetStucked.Core;
using NetStucked.Infrastructure;

namespace NetStucked.Desktop.Services;

public sealed partial class WpfDialogService
{
    public async Task<string?> LoadWifiProfileAsync()
    {
        var file = new OpenFileDialog { Title = "Import IT-approved Windows WLAN profile", Filter = "WLAN profile XML (*.xml)|*.xml", CheckFileExists = true };
        if (file.ShowDialog(Application.Current.MainWindow) != true) return null;
        if (new FileInfo(file.FileName).Length > 1_000_000) throw new IOException("WLAN profile exceeds 1 MB.");
        return await File.ReadAllTextAsync(file.FileName);
    }
    public Task SaveWifiProfileAsync(string name, string xml) => SaveAsync("wifi-profile.xml", "WLAN configuration without passwords (*.xml)|*.xml", xml);
    public bool ConfirmWifiDeletion(string name) => ShowMessage("Delete Wi-Fi profile?", $"Delete Windows WLAN profile '{name}' and its NetStucked credential reference?\nIt may need credentials or IT provisioning to connect again.", true);
    public bool ConfirmWifiImport(WifiProfile profile) => ShowMessage("Import Windows profile?", $"Import '{profile.Name}' for SSID '{profile.Ssid}'?\nSecurity: {profile.Security} · {profile.Eap}\nOnly import configuration supplied by your IT team. Enterprise certificate/server trust settings will be preserved. Windows owns authentication. Auto-connect: {(profile.AutoConnect ? "Enabled by this profile" : "Disabled")}", true);
    public WifiMetadataEdit? EditWifiMetadata(string description, bool autoConnect)
    {
        WifiMetadataEdit? result = null;
        var checkbox = new CheckBox { Content = "Windows auto-connect for this profile", IsChecked = autoConnect, Margin = new(0, 10, 0, 0) };
        ShowSettings("Edit Wi-Fi profile", [("Description (nonsecret)", description)], checkbox, v => { if (v[0].Length > 512) throw new ArgumentException("Description maximum 512 characters."); result = new(v[0], checkbox.IsChecked == true); });
        return result;
    }
    public WifiProfileInput? EditWifiProfile(WifiProfileInput input, bool allowIdentityEdit)
    {
        WifiProfileInput? result = null;
        var panel = new StackPanel { Margin = new(20) };
        TextBox Field(string label, string value, int max, bool editable = true)
        {
            panel.Children.Add(new TextBlock { Text = label, Margin = new(0, 10, 0, 5) });
            var text = new TextBox { Text = value, MaxLength = max, IsReadOnly = !editable }; AutomationProperties.SetName(text, label); panel.Children.Add(text); return text;
        }
        var name = Field("Profile name", input.Name, 255, allowIdentityEdit);
        var ssid = Field("SSID", input.Ssid, 32, allowIdentityEdit);
        panel.Children.Add(new TextBlock { Text = "Security type", Margin = new(0, 10, 0, 5) });
        var security = new ComboBox { ItemsSource = new[] { "WPA2-Personal", "WPA3-Personal", "Open" }, SelectedItem = input.Security, IsEnabled = allowIdentityEdit }; panel.Children.Add(security);
        panel.Children.Add(new TextBlock { Text = "Password (stored by Windows WLAN only)", Margin = new(0, 10, 0, 5) });
        var password = Password(); panel.Children.Add(password);
        var description = Field("Description (nonsecret)", input.Description, 512, allowIdentityEdit);
        var automatic = new CheckBox { Content = "Windows auto-connect", IsChecked = input.AutoConnect, IsEnabled = allowIdentityEdit, Margin = new(0, 12, 0, 0) }; panel.Children.Add(automatic);
        panel.Children.Add(new TextBlock { Text = "Enterprise / EAP-TLS: use an existing Windows profile or import your IT-approved WLAN XML. Certificate trust and private keys remain managed by Windows.", TextWrapping = TextWrapping.Wrap, Margin = new(0, 14, 0, 0) });
        WifiModal(allowIdentityEdit ? "Add Personal Wi-Fi profile" : "Set Personal Wi-Fi key", panel, () => { var edited = new WifiProfileInput(name.Text, ssid.Text, (string)security.SelectedItem, automatic.IsChecked == true, description.Text, password.Password); WifiProfileXml.Personal(edited); result = edited; });
        password.Clear(); return result;
    }
    public EnterpriseCredentials? EditEnterpriseCredentials()
    {
        EnterpriseCredentials? result = null;
        var panel = new StackPanel { Margin = new(20) };
        panel.Children.Add(new TextBlock { Text = "PEAP / MSCHAPv2 user credentials", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Username", Margin = new(0, 12, 0, 5) }); var user = new TextBox { MaxLength = 256 }; panel.Children.Add(user);
        panel.Children.Add(new TextBlock { Text = "Domain (optional)", Margin = new(0, 12, 0, 5) }); var domain = new TextBox { MaxLength = 256 }; panel.Children.Add(domain);
        panel.Children.Add(new TextBlock { Text = "Password", Margin = new(0, 12, 0, 5) }); var password = Password(); panel.Children.Add(password);
        var remember = new CheckBox { Content = "Remember in Windows Credential Manager", IsChecked = false, Margin = new(0, 16, 0, 0) }; panel.Children.Add(remember);
        panel.Children.Add(new TextBlock { Text = "Windows may retain EAP credentials for this user. Server certificate validation and company policy stay intact. EAP-TLS and other methods require Windows/IT provisioning.", TextWrapping = TextWrapping.Wrap, Margin = new(0, 14, 0, 0) });
        WifiModal("Secure Enterprise credentials", panel, () => { var edited = new EnterpriseCredentials(user.Text, password.Password, domain.Text, remember.IsChecked == true); WifiProfileXml.PeapCredentials(edited); result = edited; });
        password.Clear(); return result;
    }
    private static PasswordBox Password()
    {
        var box = new PasswordBox { MaxLength = 256, Padding = new(8, 6, 8, 6), MinHeight = 32 };
        box.SetResourceReference(Control.BackgroundProperty, "InputBrush"); box.SetResourceReference(Control.ForegroundProperty, "TextBrush"); box.SetResourceReference(Control.BorderBrushProperty, "BorderBrush"); box.SetResourceReference(PasswordBox.CaretBrushProperty, "TextBrush");
        AutomationProperties.SetName(box, "Password"); return box;
    }
    private static void WifiModal(string title, StackPanel panel, Action accept)
    {
        var window = new Window { Title = title, Width = 490, MaxHeight = 760, SizeToContent = SizeToContent.Height, Owner = Application.Current.MainWindow, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        ThemeService.ApplyWindow(window);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new(0, 10, 0, 0) }; error.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush"); panel.Children.Add(error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 16, 0, 0) };
        var save = new Button { Content = "Save", IsDefault = true, MinWidth = 80 };
        save.Click += (_, _) => { try { accept(); window.DialogResult = true; } catch (ArgumentException ex) { error.Text = ex.Message; } };
        actions.Children.Add(save); actions.Children.Add(new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Margin = new(8, 0, 0, 0) }); panel.Children.Add(actions);
        window.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; window.ShowDialog();
    }
    public ServiceTestProfile? EditServiceTestProfile(ServiceTestProfile profile)
    {
        ServiceTestProfile? result = null;
        var extra = new StackPanel(); var gateway = new CheckBox { Content = "Test selected adapter gateway with source-bound ICMP", IsChecked = profile.TestGateway }; extra.Children.Add(gateway);
        extra.Children.Add(new TextBlock { Text = "Only these configured endpoints are tested. DNS uses the selected adapter's servers. HTTP uses a direct connection (no proxy), normal TLS validation, and does not follow redirects or submit portal credentials.", TextWrapping = TextWrapping.Wrap, Margin = new(0, 10, 0, 0) });
        ShowSettings("Company service test profile", [("Profile name", profile.Name), ("DNS name (optional)", profile.DnsName), ("TCP host / IP (optional)", profile.TcpHost), ("TCP port", profile.TcpPort.ToString()), ("HTTP / HTTPS URL (optional, no credentials)", profile.HttpUrl), ("Expected HTTP status", profile.ExpectedHttpStatus.ToString()), ("Timeout per check (ms)", profile.TimeoutMs.ToString())], extra, v =>
        {
            var edited = profile with { Name = v[0].Trim(), DnsName = v[1].Trim(), TcpHost = v[2].Trim(), TcpPort = Int(v[3]), HttpUrl = v[4].Trim(), ExpectedHttpStatus = Int(v[5]), TimeoutMs = Int(v[6]), TestGateway = gateway.IsChecked == true }; edited.Validate();
            if (!edited.TestGateway && edited.DnsName.Length == 0 && edited.TcpHost.Length == 0 && edited.HttpUrl.Length == 0) throw new ArgumentException("Configure at least one check."); result = edited;
        });
        return result;
    }
}
