using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using NetStucked.Desktop.Services;

namespace NetStucked.Desktop.Behaviors;

public static class TableTextSupport
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(TableTextSupport), new PropertyMetadata(false, Changed));
    private static readonly DependencyProperty StartProperty = DependencyProperty.RegisterAttached("Start", typeof(Point), typeof(TableTextSupport));
    public static void SetEnabled(DependencyObject value, bool enabled) => value.SetValue(EnabledProperty, enabled);
    public static bool GetEnabled(DependencyObject value) => (bool)value.GetValue(EnabledProperty);
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is not true) return;
        if (target is TextBox box)
        {
            box.PreviewMouseLeftButtonDown += (_, e) =>
            {
                box.SetValue(StartProperty, e.GetPosition(box));
                if (GridTools.Ancestor<DataGridRow>(box) is { } row && GridTools.Ancestor<DataGrid>(box) is { } grid)
                {
                    grid.SelectedItem = row.Item;
                    if (GridTools.Ancestor<DataGridCell>(box) is { } cell && !grid.IsReadOnly && !cell.Column.IsReadOnly && e.ClickCount == 2)
                    { grid.CurrentCell = new(row.Item, cell.Column); grid.BeginEdit(); e.Handled = true; }
                }
            };
            box.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (GridTools.Ancestor<DataGridColumnHeader>(box) is { } header &&
                    (e.GetPosition(box) - (Point)box.GetValue(StartProperty)).Length < 4 && e.ClickCount == 1)
                {
                    InvokeHeader(header);
                }
            };
            box.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.F2 && GridTools.Ancestor<DataGridCell>(box) is { } cell && GridTools.Ancestor<DataGrid>(box) is { IsReadOnly: false } grid && !cell.Column.IsReadOnly)
                { grid.CurrentCell = new(cell.DataContext, cell.Column); grid.BeginEdit(); e.Handled = true; }
                if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && box.SelectionLength == 0 && box.Text.Length > 0)
                { Copy(box.Text); e.Handled = true; }
            };
            box.ContextMenuOpening += (_, e) =>
            {
                if (GridTools.Ancestor<DataGrid>(box) is not { ContextMenu: { } menu } table) return;
                if (GridTools.Ancestor<DataGridRow>(box) is { } row) table.SelectedItem = row.Item;
                var textCopy = new MenuItem { Header = box.SelectionLength > 0 ? "Copy selected cell text" : "Copy cell text" };
                textCopy.Click += (_, _) => Copy(box.SelectionLength > 0 ? box.SelectedText : box.Text);
                // Each generated cell reuses the bounded table menu; retain no old cell references.
                if (menu.Items.Count > 3) menu.Items.RemoveAt(0);
                menu.Items.Insert(0, textCopy); menu.PlacementTarget = box; menu.IsOpen = true; e.Handled = true;
            };
        }
        if (target is DataGrid table)
        {
            var menu = new ContextMenu();
            menu.Closed += (_, _) => { if (menu.Items.Count > 3) menu.Items.RemoveAt(0); menu.PlacementTarget = null; };
            var selected = new MenuItem { Header = "Copy selected row with headers" };
            selected.Click += (_, _) => Copy(BuildText(table, selectedOnly: true)); menu.Items.Add(selected);
            var copy = new MenuItem { Header = "Copy table with headers" };
            copy.Click += (_, _) => Copy(BuildText(table)); menu.Items.Add(copy);
            var inspect = new MenuItem { Header = "Select / copy table text…" };
            inspect.Click += (_, _) => ShowText(table); menu.Items.Add(inspect);
            table.ContextMenu = menu;
        }
    }
    public static void InvokeHeader(DataGridColumnHeader header)
    {
        if (header.Column is not { CanUserSort: true } column || GridTools.Ancestor<DataGridColumnHeadersPresenter>(header) is not { } presenter) return;
        var owner = UIElementAutomationPeer.CreatePeerForElement(presenter) as DataGridColumnHeadersPresenterAutomationPeer ?? new DataGridColumnHeadersPresenterAutomationPeer(presenter);
        var peer = new DataGridColumnHeaderItemAutomationPeer(header.Content, column, owner);
        (peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider)?.Invoke();
    }
    private static void Copy(string value)
    {
        if (value.Length == 0) return;
        try { Clipboard.SetText(value); }
        catch (System.Runtime.InteropServices.ExternalException) { new WpfDialogService().ShowError("The clipboard is busy. Please try copying again."); }
    }
    public static string BuildText(DataGrid table, bool selectedOnly = false)
    {
        var columns = table.Columns.Where(c => c.Visibility == Visibility.Visible).OrderBy(c => c.DisplayIndex).ToArray();
        var result = new StringBuilder();
        result.AppendLine(string.Join('\t', columns.Select(c => c.Header?.ToString() ?? "")));
        var items = selectedOnly ? table.SelectedItems.Cast<object>() : table.Items.Cast<object>();
        int copied = 0; bool textLimited = false;
        foreach (object item in items.Take(4096))
        {
            copied++;
            result.AppendLine(string.Join('\t', columns.Select(c => Read(item, c).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' '))));
            if (result.Length > 1_000_000) { result.AppendLine("… Table text limited to 1 MB; use CSV export for the complete result."); textLimited = true; break; }
        }
        if (!textLimited && copied == 4096 && (selectedOnly ? table.SelectedItems.Count : table.Items.Count) > copied)
            result.AppendLine("… Table text limited to 4096 rows; use CSV export for the complete result.");
        return result.ToString();
    }
    private static string Read(object item, DataGridColumn column)
    {
        var binding = (column as DataGridBoundColumn)?.Binding as Binding;
        string? path = binding?.Path?.Path ?? column.SortMemberPath;
        object? value = item;
        if (string.IsNullOrWhiteSpace(path)) return "";
        foreach (string part in path.Split('.')) value = value?.GetType().GetProperty(part)?.GetValue(value);
        if (value is null) return binding is not null && binding.TargetNullValue != DependencyProperty.UnsetValue ? binding.TargetNullValue?.ToString() ?? "" : "—";
        string? format = binding?.StringFormat;
        if (format is null) return Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";
        if (format.StartsWith("{}")) format = format[2..];
        return format.Contains('{') ? string.Format(CultureInfo.CurrentCulture, format, value) : value is IFormattable typed ? typed.ToString(format, CultureInfo.CurrentCulture) : value.ToString() ?? "";
    }
    private static void ShowText(DataGrid table)
    {
        var window = new Window { Title = "Table text — select and copy", Width = 820, Height = 520, Owner = Window.GetWindow(table), WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        ThemeService.ApplyWindow(window);
        var text = new TextBox { Text = BuildText(table), IsReadOnly = true, IsReadOnlyCaretVisible = true, AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new(12), VerticalContentAlignment = VerticalAlignment.Top };
        System.Windows.Automation.AutomationProperties.SetName(text, "Selectable table text including headers");
        window.Content = text; window.ShowDialog();
    }
}
