using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows;

namespace NetStucked.Desktop.Converters;

public sealed class StatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value?.ToString() switch
    {
        "Up" or "Reply" or "Connected" => new SolidColorBrush(Color.FromRgb(0, 163, 105)),
        "Warn" or "Route" => new SolidColorBrush(Color.FromRgb(219, 126, 0)),
        "Unreachable" or "Timeout" or "Error" or "Refused" or "DNS error" => new SolidColorBrush(Color.FromRgb(224, 55, 55)),
        "Info" or "DNS" => new SolidColorBrush(Color.FromRgb(36, 107, 253)),
        _ => new SolidColorBrush(Color.FromRgb(86, 105, 140))
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class PageSelectedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Equals(value, parameter);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class PageVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (parameter?.ToString() == "Other" ? value?.ToString() is not ("Live Ping" or "Traceroute" or "Port Test" or "Updates") : Equals(value, parameter)) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ButtonHintConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) => values[0] is string name && name.Length > 0 ? name : values[1]?.ToString() ?? "Action";
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
