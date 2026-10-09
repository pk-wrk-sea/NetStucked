using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace NetStucked.Desktop.Behaviors;

public sealed class CopyableTextColumn : DataGridTextColumn
{
    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
    {
        var text = new TextBox { Style = ElementStyle is { TargetType: var type } && type == typeof(TextBox) ? ElementStyle : (Style)cell.FindResource("TableText"), IsReadOnly = true };
        // TextBox.Text defaults to TwoWay even when IsReadOnly. Display cells must never
        // attempt writes to measured/read-only models; retain the original binding for editing.
        if (Binding is Binding source)
            BindingOperations.SetBinding(text, TextBox.TextProperty, new Binding { Path = source.Path, Mode = BindingMode.OneWay,
                StringFormat = source.StringFormat, TargetNullValue = source.TargetNullValue, FallbackValue = source.FallbackValue,
                Converter = source.Converter, ConverterParameter = source.ConverterParameter, ConverterCulture = source.ConverterCulture });
        else if (Binding is not null) BindingOperations.SetBinding(text, TextBox.TextProperty, Binding);
        return text;
    }
}
