using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows;

namespace NetStucked.Desktop.Converters;

public sealed class StatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value?.ToString() switch
    {
        "Up" or "Reply" or "Connected" or "Responded" or "PASS" or "Success" => Brush("SuccessBrush"),
        "Warn" or "Route" or "No response" => Brush("WarningBrush"),
        "Unreachable" or "Timeout" or "Error" or "Refused" or "Closed" or "DNS error" or "FAIL" or "Failed" => Brush("DangerBrush"),
        "Info" or "DNS" => Brush("AccentBrush"),
        _ => Brush("MutedBrush")
    };
    private static Brush Brush(string key) => Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class InverseVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class PageSelectedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Equals(value, parameter);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class PageVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (parameter?.ToString() == "Other" ? value?.ToString() is not ("Live Ping" or "Traceroute" or "Port Test" or "Updates" or "Network Info") : Equals(value, parameter)) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ButtonHintConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) => values[0] is string name && name.Length > 0 ? name : values[1]?.ToString() ?? "Action";
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
