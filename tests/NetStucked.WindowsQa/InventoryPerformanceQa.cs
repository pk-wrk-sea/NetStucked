using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using NetStucked.Core;
using NetStucked.Desktop;
using NetStucked.Desktop.Behaviors;
using NetStucked.Desktop.Services;
using NetStucked.Desktop.ViewModels;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;
internal static partial class Program
{
    // Read only: never initialize the application or save the user's preferences/workspace.
    private static async Task<int> AuditInventoryAsync(string directory)
    {
        string path = Path.Combine(directory, "engineering", "workspace-v1.json");
        byte[] before = await File.ReadAllBytesAsync(path);
        var data = await new EngineeringStore(new UserSettingsStore(directory)).LoadAsync(default);
        byte[] after = await File.ReadAllBytesAsync(path);
        Require(before.SequenceEqual(after), "Read-only inventory validation leaves original workspace bytes intact");
        Console.WriteLine(JsonSerializer.Serialize(new { data.Revision, Assets = data.Assets.Length, Evidence = data.Evidence.Length, Changes = data.Changes?.Length, Bytes = before.Length, Sha256 = Convert.ToHexStringLower(SHA256.HashData(before)), Valid = true }));
        return 0;
    }

    private static async Task RunInventoryPerformanceAsync(Application app, string output, BindingErrors errors)
    {
        var files = new QaEngineeringFiles(output); var store = new UserSettingsStore(Path.Combine(output, "isolated-user-data"));
        await using var provider = App.ConfigureServices(store, new QaDialogs { ConfirmGeneral = true }, services => services.AddSingleton<IEngineeringFiles>(files));
        provider.GetRequiredService<ThemeService>(); var vm = provider.GetRequiredService<MainViewModel>();
        var metrics = new Dictionary<string, object>();
        var window = provider.GetRequiredService<MainWindow>(); app.MainWindow = window; window.DataContext = vm;
        window.ShowActivated = false; window.ShowInTaskbar = false; window.Opacity = 0; window.Show();
        async Task Measure(string name, Func<Task> action)
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var watch = Stopwatch.StartNew(); await action(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            metrics[name + "Ms"] = Math.Round(watch.Elapsed.TotalMilliseconds, 1); Console.WriteLine($"MEASURE {name} {watch.Elapsed.TotalMilliseconds:F1} ms");
            await File.WriteAllTextAsync(Path.Combine(output, "metrics.json"), JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
        }
        try
        {
            await vm.Engineering.InitializeAsync(); vm.NavigateCommand.Execute("Inventory"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var grid = Descendants<DataGrid>(window).Single(g => AutomationProperties.GetName(g) == "Inventory assets");
            int notifications = 0; vm.Inventory.Assets.CollectionChanged += (_, _) => notifications++;
            foreach (int count in new[] { 708, 2000 })
            {
                var rows = Enumerable.Range(1, count).Select(i => new Asset(Guid.NewGuid(), $"LAB-SW-{i:00000}", "192.0.2.1", "Cisco", "LAB-Component", $"LABSERIAL{count}-{i:00000}", "Owned fixture", "Lab ไทย", "Switch", "", null, null, DateTimeOffset.UtcNow) { Number = i.ToString(), Company = "Example", Province = "Bangkok", Building = "A", Floor = "1", Room = "Fixture room", Rack = "R1", Remark = "Synthetic performance fixture" }).ToArray();
                string workbook = Path.Combine(output, $"owned-{count}.xlsx"); await InventoryWorkbook.WriteAsync(workbook, rows, default);
                files.OpenPath = workbook;
                await Measure($"{count}-Preview", () => vm.Inventory.ImportPreviewCommand.ExecuteAsync(null));
                Require(vm.Inventory.Preview.Count == count && vm.Inventory.Preview.All(p => p.Data.Error.Length == 0), $"{count} valid component rows previewed without saving");
                vm.Inventory.SelectValidCommand.Execute(null); notifications = 0;
                await Measure($"{count}-Apply", () => vm.Inventory.ApplyImportCommand.ExecuteAsync(null));
                metrics[$"{count}-ApplyNotifications"] = notifications;
                Require(notifications <= 1, "Import publishes one inventory snapshot instead of one notification per asset");
                Require(rows.All(a => vm.Engineering.Data.Assets.Any(saved => saved.Id == a.Id && saved.Serial == a.Serial && saved.Address == a.Address)) && vm.Inventory.Preview.Count == 0, $"{count} imported assets retain stable IDs and nullable/shared-IP values");
                int total = vm.Inventory.Assets.Count;
                metrics[$"{count}-RealizedRows"] = Descendants<DataGridRow>(grid).Count();
                metrics[$"{count}-RealizedCells"] = Descendants<DataGridCell>(grid).Count();
                Require(Descendants<DataGridRow>(grid).Count() < 100, "Row virtualization bounds realized controls");
                vm.Inventory.SelectedAsset = vm.Inventory.Assets[0]; string editingId = vm.Inventory.AssetId;
                vm.Inventory.Hostname = "Unsaved user edit";
                notifications = 0;
                await Measure($"{count}-RapidSearch", async () =>
                {
                    foreach (string query in new[] { "L", "LA", "LAB", "LAB-", "LAB-SW-", "LAB-SW-005", "no-match", "" }) vm.Inventory.Search = query;
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                });
                metrics[$"{count}-SearchNotifications"] = notifications;
                Require(notifications <= 8, "Search notifications are bounded by queries, not total row count");
                Require(vm.Inventory.Assets.Count == total && vm.Inventory.Hostname == "Unsaved user edit" && vm.Inventory.AssetId == editingId, "Rapid search preserves editing identity and unsaved fields");
                await Measure($"{count}-Scroll", async () =>
                {
                    var scroll = GridTools.Descendants<ScrollViewer>(grid).First();
                    for (int step = 0; step < 20; step++) { scroll.ScrollToVerticalOffset(step * (total / 21)); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); }
                    scroll.ScrollToTop();
                });
                await Measure($"{count}-Selections", async () =>
                {
                    for (int i = 0; i < 30; i++) { grid.SelectedItem = vm.Inventory.Assets[i]; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); }
                });
                Require(vm.Inventory.AssetId == vm.Inventory.Assets[29].Asset.Id.ToString(), "Actual grid selection maps to the correct editor identity");
                await Measure($"{count}-Export", () => vm.Inventory.ExportWorkbookCommand.ExecuteAsync(null));
                files.OpenPath = files.LastSaved;
                await Measure($"{count}-RoundtripPreview", () => vm.Inventory.ImportPreviewCommand.ExecuteAsync(null));
                Require(vm.Inventory.Preview.Count == total && vm.Inventory.Preview.All(p => p.Data.Error.Length == 0), "Exported workbook previews updates without duplicate identity errors");
                vm.Inventory.SelectValidCommand.Execute(null);
                await Measure($"{count}-RoundtripApply", () => vm.Inventory.ApplyImportCommand.ExecuteAsync(null));
                Require(vm.Engineering.Data.Assets.Length == total, "Unmodified roundtrip does not duplicate assets");
                metrics[$"{count}-AuditCountAfterRoundtrip"] = vm.Engineering.Data.Changes?.Length ?? 0;
                Require(vm.Engineering.Data.Changes?.Length == total, "Unchanged import retains original timestamps without creating false audit edits");
            }
            var brand = Descendants<TextBox>(window).FirstOrDefault(t => System.Windows.Data.BindingOperations.GetBinding(t, TextBox.TextProperty)?.Path.Path is "Brand" or "Vendor");
            metrics["BrandBinding"] = brand is null ? "missing" : System.Windows.Data.BindingOperations.GetBinding(brand, TextBox.TextProperty)!.Path.Path;
            Require(brand is not null && System.Windows.Data.BindingOperations.GetBinding(brand, TextBox.TextProperty)!.Path.Path == "Vendor", "Brand editor binds to persisted Vendor field");
            brand!.BringIntoView(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            brand.Text = "Edited fixture brand"; Require(vm.Inventory.Vendor == "Edited fixture brand", "Actual Brand textbox updates the inventory editor");
            await vm.Inventory.SaveAssetCommand.ExecuteAsync(null);
            Require(vm.Engineering.Data.Assets.Single(a => a.Id.ToString() == vm.Inventory.AssetId).Vendor == "Edited fixture brand", "Brand edit persists with the selected stable identity");
            var lazyCell = Descendants<DeferredCellText>(grid).First();
            Require(!Descendants<TextBox>(lazyCell).Any(), "Unfocused inventory cells do not initialize text input services");
            lazyCell.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
            var cellEditor = Descendants<TextBox>(lazyCell).Single(); cellEditor.SelectAll();
            Require(cellEditor.IsReadOnly && cellEditor.SelectedText == lazyCell.Text, "Hover exposes read-only selectable text with the correct recycled cell value");
            // Mouse event path still selects the row for the side editor.
            cellEditor.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
            Require(ReferenceEquals(grid.SelectedItem, GridTools.Ancestor<DataGridRow>(lazyCell)!.Item), "Selectable lazy cell retains normal row selection");
            var searchBox = Descendants<TextBox>(window).Single(t => AutomationProperties.GetName(t) == "Search inventory");
            int refreshes = 0; NotifyCollectionChangedEventHandler changed = (_, _) => refreshes++;
            vm.Inventory.Assets.CollectionChanged += changed;
            foreach (string query in new[] { "L", "LA", "LAB", "LAB-SW-00001" }) searchBox.Text = query;
            await Task.Delay(260); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Require(vm.Inventory.Search == "LAB-SW-00001" && vm.Inventory.Assets.Count == 2 && refreshes == 1, "Actual search textbox coalesces rapid typing and shows matching rows");
            searchBox.Text = ""; await Task.Delay(260); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            vm.Inventory.Assets.CollectionChanged -= changed;
            metrics["BindingErrors"] = errors.Messages.Distinct().ToArray();
            Require(errors.Messages.Count == 0, "Scaled Inventory has no WPF binding errors including the Brand field");
            Render(window, output, "Inventory-scale-Light", 1536, 1024, 1);
            provider.GetRequiredService<ThemeService>().Apply("Dark");
            Render(window, output, "Inventory-scale-Dark", 1536, 1024, 1);
            await vm.Engineering.ReloadAsync(); Require(vm.Inventory.Assets.Count == 2708, "Persisted scaled inventory reloads with all imported stable identities");
            await File.WriteAllTextAsync(Path.Combine(output, "metrics.json"), JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { await vm.ShutdownAsync(); }
    }
}
