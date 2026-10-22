using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using NetStucked.Core;
using NetStucked.Desktop;
using NetStucked.Desktop.Services;
using NetStucked.Desktop.ViewModels;
using NetStucked.Desktop.Views;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;
internal static partial class Program
{
    private static async Task RunCollectorRevisionUiAsync(Application app, string output, string fixture, BindingErrors errors)
    {
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "CollectorRuntime", "python.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-I", "-B", "-u", fixture }) start.ArgumentList.Add(arg);
        using var server = Process.Start(start)!; var stderr = server.StandardError.ReadToEndAsync();
        var files = new QaEngineeringFiles(output); var dialogs = new QaDialogs { ConfirmGeneral = true };
        var store = new UserSettingsStore(Path.Combine(output, "isolated-settings"));
        await using var provider = App.ConfigureServices(store, dialogs, services => services.AddSingleton<IEngineeringFiles>(files));
        var vm = provider.GetRequiredService<MainViewModel>(); var window = provider.GetRequiredService<MainWindow>();
        app.MainWindow = window; window.DataContext = vm; window.ShowActivated = false; window.ShowInTaskbar = false; window.Show();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            using var ready = JsonDocument.Parse((await server.StandardOutput.ReadLineAsync(timeout.Token))!);
            var targets = ready.RootElement.GetProperty("targets").EnumerateArray().Where(t => t.GetProperty("mode").GetString() is "ios" or "xe").ToArray();
            Require(!Descendants<RadioButton>((DependencyObject)window.Content).Any(b => b.CommandParameter?.ToString() == "Terminal"), "Terminal is absent from actual navigation");
            vm.NavigateCommand.Execute("Config Collector"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var collector = vm.Collector; collector.TargetText = string.Join('\n', targets.Select(t => "127.0.0.1:" + t.GetProperty("port").GetInt32()));
            collector.Username1 = "fixture"; collector.Profile = "Cisco IOS-XE"; collector.Concurrency = 2; collector.OutputFolder = Path.Combine(output, "captures");
            await collector.SaveUsernamesCommand.ExecuteAsync(null);
            Require(new UserSettingsStore(store.DirectoryPath).Preferences.CollectorUsername1 == "fixture", "Explicit Save usernames survives settings reload");
            var view = Descendants<ConfigCollectorView>((DependencyObject)window.Content).Single();
            var password = (PasswordBox)view.FindName("Password1"); password.Password = "initial-fixture-value";
            var show = (CheckBox)view.FindName("ShowPasswords"); show.IsChecked = true;
            var revealed = (TextBox)view.FindName("RevealPassword1");
            Require(revealed.Text == password.Password && revealed.IsVisible == false, "Show password copies only into view memory (AUTH tab not selected yet)");
            revealed.Text = "fixture-password";
            var button = Descendants<Button>(view).Single(b => Equals(b.Content, "Start")); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(collector.IsRunning && password.Password.Length == 0 && revealed.Text.Length == 0 && show.IsChecked == false, "Actual Start consumes edited visible password and immediately clears all secret controls");
            int prompts = 0;
            while (collector.IsRunning)
            {
                timeout.Token.ThrowIfCancellationRequested();
                if (app.Windows.OfType<TerminalHostKeyDialog>().FirstOrDefault() is { } dialog)
                {
                    if (prompts == 0)
                    {
                        await server.StandardInput.WriteLineAsync("stats"); await server.StandardInput.FlushAsync();
                        using var audit = JsonDocument.Parse((await server.StandardOutput.ReadLineAsync(timeout.Token))!);
                        Require(audit.RootElement.GetProperty("auth").GetArrayLength() == 0, "Actual WPF host-key dialog appears before fixture receives credentials");
                        Render(dialog, output, "Collector-first-use-host-key", 650, 570, 1);
                    }
                    Descendants<Button>((DependencyObject)dialog.Content).Single(b => AutomationProperties.GetName(b) == "Accept and save SSH key").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); prompts++;
                }
                await Task.Delay(30, timeout.Token);
            }
            Require(prompts == 2 && collector.State == CollectorBatchState.Completed && collector.Results.Count == 2, "Actual WPF -> bundled Netmiko -> two owned devices -> completed captures: " + collector.Message);
            Require(collector.Results.All(r => File.Exists(r.Result.TxtPath) && r.Result.DeviceHostname.StartsWith("LAB-")), "Actual WPF exposes automatic hostname/IP capture paths");
            foreach (string theme in new[] { "Light", "Dark" })
            {
                vm.Appearance.SelectedTheme = theme; Render(window, output, "Collector-Netmiko-" + theme, 1536, 1024, 1);
                var tabs = Descendants<TabControl>(view).Single(t => AutomationProperties.GetName(t) == "Collector setup"); tabs.SelectedIndex = 2;
                Render(window, output, "Collector-auth-" + theme, 1280, 800, 1); tabs.SelectedIndex = 0;
            }
            Require(!File.ReadAllText(Path.Combine(store.DirectoryPath, "settings.json")).Contains("fixture-password"), "Saved settings contain no Collector password");
            await vm.Engineering.InitializeAsync(); vm.NavigateCommand.Execute("Inventory");
            vm.Inventory.NewAssetCommand.Execute(null); vm.Inventory.Company = "OWNED fixture"; vm.Inventory.Remark = "Optional component with no hostname/IP";
            await vm.Inventory.SaveAssetCommand.ExecuteAsync(null);
            Require(vm.Inventory.Assets.Count == 1 && vm.Inventory.Assets[0].Asset.Address.Length == 0, "Actual Inventory saves component with optional hostname/IP empty");
            var one = vm.Engineering.Data.Assets[0] with { Hostname = "LAB-SW", Address = "192.0.2.1", Number = "1", Role = "Switch", Model = "OWNED-CHASSIS", Province = "กรุงเทพมหานคร", Location = "ENCO", Building = "A", Floor = "G", Room = "Control Room", Rack = "NW-A-G", MaType = "Smartnet", SlaType = "1", Ldos = new(2027, 10, 31) };
            var two = one with { Id = Guid.NewGuid(), Number = "2", Role = "Line Card", Model = "OWNED-CARD", Ldos = null };
            var three = one with { Id = Guid.NewGuid(), Number = "3", Role = "Power Supply", Model = "OWNED-PSU", Serial = "", Remark = "" };
            string workbook = Path.Combine(output, "image-layout-owned.xlsx");
            await InventoryWorkbook.WriteAsync(workbook, [one, two, three], default); files.OpenPath = workbook;
            await vm.Inventory.ImportPreviewCommand.ExecuteAsync(null); vm.Inventory.SelectValidCommand.Execute(null); await vm.Inventory.ApplyImportCommand.ExecuteAsync(null);
            Require(vm.Inventory.Assets.Count == 3 && vm.Inventory.Assets.All(a => a.Asset.Address == "192.0.2.1"), "Actual import adds components sharing IP and updates stable existing row");
            await InventoryWorkbook.WriteAsync(workbook, [one with { Company = "", Remark = "Edited in workbook" }, two, three], default);
            await vm.Inventory.ImportPreviewCommand.ExecuteAsync(null); vm.Inventory.SelectValidCommand.Execute(null); await vm.Inventory.ApplyImportCommand.ExecuteAsync(null);
            Require(vm.Inventory.Assets.Count == 3 && vm.Engineering.Data.Assets.Single(a => a.Id == one.Id).Company.Length == 0, "Actual import clears optional values and updates three stable IDs without duplicate rows");
            await vm.Inventory.TemplateCommand.ExecuteAsync(null); await vm.Inventory.ExportWorkbookCommand.ExecuteAsync(null);
            var grid = Descendants<DataGrid>((DependencyObject)window.Content).Single(g => AutomationProperties.GetName(g) == "Inventory assets");
            Require(grid.Columns.Any(c => Equals(c.Header, "LDoS")) && grid.Columns.Any(c => Equals(c.Header, "Company")) && grid.CanUserResizeColumns, "Actual Inventory presents image columns with resizing");
            foreach (string theme in new[] { "Light", "Dark" }) { vm.Appearance.SelectedTheme = theme; Render(window, output, "Inventory-" + theme, 1536, 1024, 1); Render(window, output, "Inventory-narrow-" + theme, 1280, 800, 1); }
            vm.NavigateCommand.Execute("Settings"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var toggle = Descendants<ToggleButton>((DependencyObject)window.Content).Single(t => AutomationProperties.GetName(t) == "Dark mode");
            vm.Appearance.SelectedTheme = "Light"; ((IToggleProvider)new ToggleButtonAutomationPeer(toggle).GetPattern(PatternInterface.Toggle)).Toggle();
            Require(vm.Appearance.IsDarkMode && vm.Appearance.SelectedTheme == "Dark", "Actual Dark-mode switch applies theme immediately");
            Render(window, output, "Settings-Dark-switch", 1280, 800, 1);
            Require(errors.Messages.Count == 0, "Collector/Inventory/appearance actual WPF have no binding errors: " + string.Join("; ", errors.Messages));
            await File.WriteAllTextAsync(Path.Combine(output, "result.txt"), "PASS: actual WPF + bundled Netmiko + owned SSH devices + trust dialog + secret controls + Inventory optional template roundtrip + Dark switch + Terminal removal. Physical vendor/manual DPI/Setup NOT TESTED.");
        }
        finally
        {
            await vm.ShutdownAsync();
            if (!server.HasExited) { await server.StandardInput.WriteLineAsync("stop"); server.StandardInput.Close(); await server.WaitForExitAsync(); }
            await File.WriteAllTextAsync(Path.Combine(output, "fixture-stderr.txt"), await stderr);
        }
    }
}
