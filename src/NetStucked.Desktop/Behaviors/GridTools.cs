using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NetStucked.Desktop.Behaviors;

public static class GridTools
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(GridTools), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty UnselectCommandProperty = DependencyProperty.RegisterAttached("UnselectCommand", typeof(ICommand), typeof(GridTools));
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("State", typeof(GridState), typeof(GridTools));
    public static void SetEnabled(DependencyObject value, bool enabled) => value.SetValue(EnabledProperty, enabled);
    public static bool GetEnabled(DependencyObject value) => (bool)value.GetValue(EnabledProperty);
    public static void SetUnselectCommand(DependencyObject value, ICommand command) => value.SetValue(UnselectCommandProperty, command);
    public static ICommand? GetUnselectCommand(DependencyObject value) => (ICommand?)value.GetValue(UnselectCommandProperty);
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not DataGrid grid) return;
        if (args.NewValue is true) grid.SetValue(StateProperty, new GridState(grid));
    }
    public static void FitColumns(DataGrid grid)
    {
        foreach (var column in grid.Columns.Where(c => c.Visibility == Visibility.Visible))
        {
            column.MinWidth = 35;
            column.MaxWidth = 380;
            column.Width = DataGridLength.Auto;
        }
        grid.UpdateLayout();
        foreach (var column in grid.Columns.Where(c => c.Visibility == Visibility.Visible)) column.MinWidth = Math.Max(35, column.ActualWidth);
        StretchLast(grid);
        ColumnLayout.Capture(grid);
    }
    public static void StretchLast(DataGrid grid)
    {
        var visible = grid.Columns.Where(c => c.Visibility == Visibility.Visible).OrderBy(c => c.DisplayIndex).ToArray();
        foreach (var column in visible.Where(c => c.Width.IsStar)) column.Width = Math.Max(column.MinWidth, column.ActualWidth);
        if (visible.LastOrDefault() is { } last) { last.MaxWidth = double.PositiveInfinity; last.MinWidth = 100; last.Width = new DataGridLength(1, DataGridLengthUnitType.Star); }
    }
    public static T? Ancestor<T>(DependencyObject? value) where T : DependencyObject
    {
        while (value is not null)
        {
            if (value is T found) return found;
            value = value is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(value) : LogicalTreeHelper.GetParent(value);
        }
        return null;
    }
    public static IEnumerable<T> Descendants<T>(DependencyObject value) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(value); i++)
        {
            var child = VisualTreeHelper.GetChild(value, i);
            if (child is T typed) yield return typed;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private sealed class GridState
    {
        private readonly DataGrid _grid;
        private IStableUpdates? _updates;
        private ScrollViewer? _scroll;
        private object? _anchor;
        private double _fraction;
        private int _userVersion, _capturedVersion;
        private DispatcherOperation? _restore;
        private ResizeGuide? _guide;
        public GridState(DataGrid grid)
        {
            _grid = grid;
            grid.Loaded += (_, _) => Attach();
            grid.Unloaded += (_, _) => Detach();
            grid.TargetUpdated += (_, _) => Attach();
            grid.DataContextChanged += (_, _) => grid.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(Attach));
            grid.PreviewMouseWheel += (_, _) => _userVersion++;
            grid.PreviewMouseLeftButtonDown += Click;
            grid.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(ResizeStart), true);
            grid.AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(ResizeDelta), true);
            grid.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(ResizeEnd), true);
        }
        private void Attach()
        {
            if (!_grid.IsLoaded) return;
            foreach (var column in _grid.Columns.Where(c => c.Width.IsAbsolute && c.MinWidth < c.Width.Value)) column.MinWidth = column.Width.Value;
            _scroll = Descendants<ScrollViewer>(_grid).FirstOrDefault();
            var next = _grid.ItemsSource as IStableUpdates;
            if (_updates == next) return;
            Detach(); _updates = next;
            if (_updates is not null) { _updates.BeforeUpdate += Before; _updates.AfterUpdate += After; }
        }
        private void Detach()
        {
            if (_updates is not null) { _updates.BeforeUpdate -= Before; _updates.AfterUpdate -= After; }
            _updates = null; _restore?.Abort(); RemoveGuide();
        }
        private void Before(object? sender, EventArgs e)
        {
            _restore?.Abort(); _anchor = null;
            if (_scroll is null || _scroll.VerticalOffset < 0.1) return;
            int index = (int)Math.Floor(_scroll.VerticalOffset);
            if (index >= _grid.Items.Count) return;
            _anchor = _grid.Items[index]; _fraction = _scroll.VerticalOffset - index; _capturedVersion = _userVersion;
        }
        private void After(object? sender, EventArgs e)
        {
            if (_anchor is null) return;
            var anchor = _anchor; int version = _capturedVersion; double fraction = _fraction;
            _restore = _grid.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (version != _userVersion || _scroll is null) return;
                int index = _grid.Items.IndexOf(anchor);
                if (index >= 0) _scroll.ScrollToVerticalOffset(index + fraction);
            }));
        }
        private void Click(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            if (Ancestor<ScrollBar>(source) is not null) { _userVersion++; return; }
            if (GetUnselectCommand(_grid) is not { } command || Ancestor<DataGridRow>(source) is not null || Ancestor<DataGridColumnHeader>(source) is not null) return;
            _grid.UnselectAll(); _grid.SelectedItem = null;
            if (command.CanExecute(null)) command.Execute(null);
            e.Handled = true;
        }
        private void ResizeStart(object sender, DragStartedEventArgs e)
        {
            var header = Ancestor<DataGridColumnHeader>(e.OriginalSource as DependencyObject);
            if (header?.Column is null || e.OriginalSource is not Thumb thumb) { _userVersion++; return; }
            bool left = thumb.Name == "PART_LeftHeaderGripper";
            var column = left ? _grid.Columns.FirstOrDefault(c => c.DisplayIndex == header.Column.DisplayIndex - 1) : header.Column;
            if (column is null) return;
            column.MinWidth = 35;
            var layer = AdornerLayer.GetAdornerLayer(_grid);
            if (layer is null) return;
            RemoveGuide(); _guide = new ResizeGuide(_grid, header, column, left); layer.Add(_guide);
        }
        private void ResizeDelta(object sender, DragDeltaEventArgs e) => _guide?.InvalidateVisual();
        private void ResizeEnd(object sender, DragCompletedEventArgs e)
        {
            RemoveGuide();
            foreach (var column in _grid.Columns.Where(c => !c.Width.IsStar && c.Visibility == Visibility.Visible)) column.MinWidth = Math.Max(35, column.ActualWidth);
            ColumnLayout.Capture(_grid);
        }
        private void RemoveGuide()
        {
            if (_guide is null) return;
            AdornerLayer.GetAdornerLayer(_grid)?.Remove(_guide); _guide = null;
        }
    }
    private sealed class ResizeGuide : Adorner
    {
        private readonly DataGrid grid;
        private readonly DataGridColumnHeader header;
        private readonly DataGridColumn column;
        private readonly bool left;
        public ResizeGuide(DataGrid grid, DataGridColumnHeader header, DataGridColumn column, bool left) : base(grid)
        {
            this.grid = grid; this.header = header; this.column = column; this.left = left;
            IsHitTestVisible = false;
        }
        protected override void OnRender(DrawingContext dc)
        {
            double x = header.TranslatePoint(new Point(left ? 0 : header.ActualWidth, 0), grid).X;
            var blue = new SolidColorBrush(Color.FromRgb(36, 107, 253));
            dc.DrawLine(new Pen(blue, 1.5), new Point(x, 0), new Point(x, grid.ActualHeight));
            var text = new FormattedText($"{column.ActualWidth:0} px", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Brushes.White, VisualTreeHelper.GetDpi(grid).PixelsPerDip);
            double labelX = Math.Clamp(x - text.Width / 2 - 8, 0, Math.Max(0, grid.ActualWidth - text.Width - 16));
            dc.DrawRoundedRectangle(blue, null, new Rect(labelX, grid.ColumnHeaderHeight + 4, text.Width + 16, 24), 4, 4);
            dc.DrawText(text, new Point(labelX + 8, grid.ColumnHeaderHeight + 8));
        }
    }
}
