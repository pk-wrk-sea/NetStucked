using System.Globalization;
using System.Windows.Data;
namespace NetStucked.Desktop.Converters;
public sealed class IncidentDurationConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3 || values[0] is not DateTimeOffset start) return "—";
        DateTimeOffset end = values[1] is DateTimeOffset closed ? closed : values[2] is DateTimeOffset now ? now : start;
        TimeSpan span = end > start ? end-start : TimeSpan.Zero;
        return span.TotalDays >= 1 ? span.ToString(@"d\.hh\:mm\:ss") : span.ToString(@"hh\:mm\:ss");
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
