using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using NetStucked.Infrastructure;

namespace NetStucked.Desktop.Behaviors;

public static class ColumnLayout
{
    public static readonly DependencyProperty StoreProperty = DependencyProperty.RegisterAttached("Store", typeof(UserSettingsStore), typeof(ColumnLayout), new PropertyMetadata(null, OnStoreChanged));
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached("Key", typeof(string), typeof(ColumnLayout), new PropertyMetadata(""));
    public static void SetStore(DependencyObject target, UserSettingsStore value) => target.SetValue(StoreProperty, value);
    public static UserSettingsStore? GetStore(DependencyObject target) => (UserSettingsStore?)target.GetValue(StoreProperty);
    public static void SetKey(DependencyObject target, string value) => target.SetValue(KeyProperty, value);
    public static string GetKey(DependencyObject target) => (string)target.GetValue(KeyProperty);
    private static string ColumnKey(DataGridColumn column) => column.SortMemberPath.Length > 0 ? column.SortMemberPath : (column as DataGridBoundColumn)?.Binding is Binding binding ? binding.Path.Path : column.Header.ToString() ?? "";
    private static void OnStoreChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not DataGrid grid) return;
        grid.Loaded -= Loaded; grid.Unloaded -= Unloaded; grid.ColumnReordered -= Reordered; grid.LostMouseCapture -= LostCapture;
        if (args.NewValue is null) return;
        grid.Loaded += Loaded; grid.Unloaded += Unloaded; grid.ColumnReordered += Reordered; grid.LostMouseCapture += LostCapture;
    }
    private static void Loaded(object sender, RoutedEventArgs args)
    {
        var grid = (DataGrid)sender;
        if (GetStore(grid)?.Preferences.Columns.TryGetValue(GetKey(grid), out var saved) != true || saved is null) return;
        foreach (var preference in saved.OrderBy(s => s.Order))
        {
            var column = grid.Columns.FirstOrDefault(c => ColumnKey(c) == preference.Key.Replace("Data.", ""));
            if (column is null) continue;
            if (preference.Key != "Data.LastPing") column.Visibility = preference.Visible ? Visibility.Visible : Visibility.Collapsed;
            if (double.IsFinite(preference.Width) && preference.Width is >= 35 and <= 1200) { column.MinWidth = preference.Width; column.Width = preference.Width; }
            column.DisplayIndex = Math.Clamp(preference.Order, 0, grid.Columns.Count - 1);
        }
        GridTools.StretchLast(grid);
    }
    private static void Unloaded(object sender, RoutedEventArgs args) => Capture((DataGrid)sender);
    private static void Reordered(object? sender, DataGridColumnEventArgs args) => Capture((DataGrid)sender!);
    private static void LostCapture(object sender, System.Windows.Input.MouseEventArgs args) => Capture((DataGrid)sender);
    public static void Capture(DataGrid grid)
    {
        var store = GetStore(grid); if (store is null) return;
        store.Preferences.Columns[GetKey(grid)] = grid.Columns.Select(c => new ColumnPreference(ColumnKey(c), c.DisplayIndex, c.ActualWidth, c.Visibility == Visibility.Visible, c.Width.IsStar)).ToList();
    }
    public static void ShowChooser(DataGrid grid)
    {
        var window = new Window { Title = "Visible columns", Width = 260, SizeToContent = SizeToContent.Height, MaxHeight = 650,
            Owner = Window.GetWindow(grid), WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false };
        var panel = new StackPanel { Margin = new(16) };
        foreach (var column in grid.Columns.OrderBy(c => c.DisplayIndex))
        {
            var checkbox = new CheckBox { Content = column.Header, IsChecked = column.Visibility == Visibility.Visible, Margin = new(0, 4, 0, 4) };
            AutomationProperties.SetName(checkbox, $"Show {column.Header} column");
            checkbox.Click += (_, _) => { column.Visibility = checkbox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed; GridTools.StretchLast(grid); Capture(grid); };
            panel.Children.Add(checkbox);
        }
        var close = new Button { Content = "Done", IsDefault = true, Margin = new(0, 12, 0, 0) };
        close.Click += (_, _) => window.Close(); panel.Children.Add(close);
        window.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        window.ShowDialog();
    }
}
