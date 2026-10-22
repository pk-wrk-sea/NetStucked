using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace NetStucked.Desktop.Behaviors;

public sealed class CopyableTextColumn : DataGridTextColumn
{
    public bool Wrap { get; set; }
    public bool DeferTextEditor { get; set; }
    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
    {
        var style = ElementStyle is { TargetType: var type } && type == typeof(TextBox) ? ElementStyle : (Style)cell.FindResource("TableText");
        FrameworkElement text = DeferTextEditor && !Wrap ? new DeferredCellText(style) : new TextBox { Style = style, IsReadOnly = true };
        var property = text is DeferredCellText ? DeferredCellText.TextProperty : TextBox.TextProperty;
        if (Wrap && text is TextBox box) { box.TextWrapping = TextWrapping.Wrap; box.AcceptsReturn = true; box.MaxHeight = 300; box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; }
        // TextBox.Text defaults to TwoWay even when IsReadOnly. Display cells must never
        // attempt writes to measured/read-only models; retain the original binding for editing.
        if (Binding is Binding source)
            BindingOperations.SetBinding(text, property, new Binding { Path = source.Path, Mode = BindingMode.OneWay,
                StringFormat = source.StringFormat, TargetNullValue = source.TargetNullValue, FallbackValue = source.FallbackValue,
                Converter = source.Converter, ConverterParameter = source.ConverterParameter, ConverterCulture = source.ConverterCulture });
        else if (Binding is not null) BindingOperations.SetBinding(text, property, Binding);
        return text;
    }
}
