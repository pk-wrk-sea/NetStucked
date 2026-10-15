using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NetStucked.Desktop.Behaviors;

public static class ResultZoom
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(ResultZoom), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty FactorProperty = DependencyProperty.RegisterAttached("Factor", typeof(double), typeof(ResultZoom), new PropertyMetadata(1d));
    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o, bool value) => o.SetValue(EnabledProperty, value);
    public static double GetFactor(DependencyObject o) => (double)o.GetValue(FactorProperty);
    public static void SetFactor(DependencyObject o, double value) => o.SetValue(FactorProperty, value);
    private static void Changed(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not FrameworkElement element) return;
        element.PreviewMouseWheel -= Wheel; element.PreviewKeyDown -= Key;
        if ((bool)e.NewValue) { element.PreviewMouseWheel += Wheel; element.PreviewKeyDown += Key; }
    }
    public static void Adjust(FrameworkElement element, int direction)
    {
        double factor = direction == 0 ? 1 : Math.Round(Math.Clamp(GetFactor(element) + Math.Sign(direction) * .1, .7, 2), 2);
        SetFactor(element, factor); element.LayoutTransform = new ScaleTransform(factor, factor);
        element.ToolTip = $"Results zoom {factor:P0} · Ctrl + mouse wheel · Ctrl + 0 resets";
    }
    private static bool IsTableChild(FrameworkElement element) => element is TextBox && GridTools.Ancestor<DataGrid>(element) is not null;
    private static void Wheel(object sender, MouseWheelEventArgs e)
    {
        if (HandleWheel((FrameworkElement)sender, e.Delta, Keyboard.Modifiers)) e.Handled = true;
    }
    public static bool HandleWheel(FrameworkElement element, int delta, ModifierKeys modifiers) { if ((modifiers & ModifierKeys.Control) == 0 || IsTableChild(element)) return false; Adjust(element, delta); return true; }
    private static void Key(object sender, KeyEventArgs e)
    {
        if (e.Key is not (System.Windows.Input.Key.D0 or System.Windows.Input.Key.NumPad0) || (Keyboard.Modifiers & ModifierKeys.Control) == 0 || IsTableChild((FrameworkElement)sender)) return;
        Adjust((FrameworkElement)sender, 0); e.Handled = true;
    }
}
