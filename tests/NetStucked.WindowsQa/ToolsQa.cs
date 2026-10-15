using System.Windows;
using System.IO;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using NetStucked.Core;
using NetStucked.Desktop.Behaviors;
using NetStucked.Desktop.ViewModels;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;

internal static partial class Program
{
    private static async Task<int> ReadMacVendorAsync(string output)
    {
        Directory.CreateDirectory(output);
        using var lookup = new MacVendorLookup();
        // The public example address is from MACVendors.com's API documentation,
        // not an address collected from this machine or a user's inventory.
        var result = await lookup.LookupAsync(MacAddressParser.Parse("FC-A1-3E-2A-1C-33"), 10000, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(output, "mac-api-result.json"), System.Text.Json.JsonSerializer.Serialize(result));
        Console.WriteLine($"MACVendors.com actual API: {result.Status}; vendor={result.Vendor ?? "none"}; {result.Details}");
        return result.Status == "Found" ? 0 : 2;
    }
    private static async Task CheckToolsAsync(Window window, MainViewModel vm, string output)
    {
        var names = Descendants<RadioButton>((DependencyObject)window.Content).Select(AutomationProperties.GetName).ToArray();
        Require(new[] { "Subnet Calculator", "DNS Resolver", "MAC Checker", "URL Checker" }.All(names.Contains), "All approved utility menus exist");
        Require(new[] { "Diagnostics", "DNS Test", "HTTP / HTTPS Test", "Network Info" }.All(n => !names.Contains(n)), "Retired diagnostics and Wi-Fi menus are absent");
        vm.NavigateCommand.Execute("Subnet Calculator"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var view = Descendants<NetStucked.Desktop.Views.SubnetCalculatorView>((DependencyObject)window.Content).Single();
        var input = (TextBox)view.FindName("SubnetInputBox"); var status = (TextBlock)view.FindName("SyntaxMessage");
        Require(!Descendants<ComboBox>(view).Any() && !Descendants<Button>(view).Any(b => b.Content is string label && label.Contains("Calculate", StringComparison.Ordinal)), "Subnet uses one input with no CIDR selector or Calculate button");
        foreach (string value in new[] { "10.10.10.10/23", "10.10.10.10 /23", "10.10.10.10. 255.255.255.0" })
        {
            input.SetCurrentValue(TextBox.TextProperty, value); await Dispatcher.Yield(DispatcherPriority.DataBind);
            Require(vm.Subnet.HasResult && vm.Subnet.Rows.Count == 8 && status.Text.StartsWith("Valid syntax"), "Typing automatically calculates: " + value);
        }
        input.SetCurrentValue(TextBox.TextProperty, "10.10.10.10 /"); await Dispatcher.Yield(DispatcherPriority.DataBind);
        Require(!vm.Subnet.HasResult && vm.Subnet.Rows.Count == 0 && status.Text.StartsWith("Input incomplete"), "Partial input clears old results and announces incomplete syntax");
        input.SetCurrentValue(TextBox.TextProperty, "10.10.10.10 255.0.255.0"); await Dispatcher.Yield(DispatcherPriority.DataBind);
        Require(vm.Subnet.IsInvalid && status.Text.StartsWith("Syntax error"), "Noncontiguous mask gets an inline syntax error");
        Render(window, output, "Subnet-invalid-light", 1280, 800, 1);
        input.SetCurrentValue(TextBox.TextProperty, "10.10.10.10/23"); await Dispatcher.Yield(DispatcherPriority.DataBind);
        foreach (string theme in new[] { "Light", "Dark" })
        {
            vm.Appearance.SelectedTheme = theme;
            Render(window, output, "Subnet-" + theme.ToLowerInvariant(), 1280, 800, 1);
            vm.NavigateCommand.Execute("MAC Checker"); vm.Mac.TargetText = "0011.2233.4455\n02:AA:BB:CC:DD:EE";
            await vm.Mac.StartCommand.ExecuteAsync(null);
            Require(vm.Mac.Results.Count == 2 && vm.Mac.Results[1].Vendor is null, "MAC table displays deterministic test-only provider results without inventing local-address vendor identity");
            Render(window, output, "MAC-" + theme.ToLowerInvariant(), 1280, 800, 1);
            vm.NavigateCommand.Execute("URL Checker"); vm.Url.Target = "https://example.com"; vm.Url.CheckCommand.Execute(null);
            Require(vm.Url.Message.Contains("No request was sent"), "URL Checker truthfully blocks API lookup until provider access is configured");
            Render(window, output, "URL-" + theme.ToLowerInvariant(), 1280, 800, 1);
            vm.NavigateCommand.Execute("Subnet Calculator");
        }
        vm.NavigateCommand.Execute("DNS Resolver"); Require(vm.Dns.QueryNs == false && vm.Dns.QuerySoa == false, "DNS Resolver exposes optional NS/SOA records");
        Render(window, output, "DNS-Resolver-dark", 1280, 800, 1);
        vm.NavigateCommand.Execute("Port Test"); Require(vm.Port.Protocol == PortProtocol.TCP, "Port Test is fixed to TCP");
        Require(!Descendants<ComboBox>((DependencyObject)window.Content).Any(c => c.IsVisible && AutomationProperties.GetName(c) == "Port protocol"), "Port protocol selector removed");
        Render(window, output, "Port-TCP-dark", 1280, 800, 1);
        vm.Appearance.SelectedTheme = "Light"; vm.NavigateCommand.Execute("Live Ping");
    }
    private sealed class QaMacLookup : IMacVendorLookup
    {
        public Task<MacVendorResult> LookupAsync(MacAddressInfo address, int timeoutMs, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new MacVendorResult(DateTimeOffset.UtcNow, address.Address, address.Oui, address.CanIdentifyVendor ? "QA fixture vendor" : null, address.AddressType, address.CanIdentifyVendor ? "Found" : "Not identifiable", "Test-only provider fixture"));
        }
    }
}
