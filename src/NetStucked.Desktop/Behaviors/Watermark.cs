using System.Windows;

namespace NetStucked.Desktop.Behaviors;

public static class Watermark
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached("Text", typeof(string), typeof(Watermark), new PropertyMetadata(""));
    public static void SetText(DependencyObject element, string text) => element.SetValue(TextProperty, text);
    public static string GetText(DependencyObject element) => (string)element.GetValue(TextProperty);
}
