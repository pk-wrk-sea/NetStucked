using System.Globalization;
using System.Windows.Data;

namespace NetStucked.Desktop.Behaviors;

public sealed class BooleanInverter : IValueConverter
{
    public static BooleanInverter Instance { get; } = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}
