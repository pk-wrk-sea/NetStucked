using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace NetStucked.Desktop.Behaviors;

// Scrolling through thousands of read-only cells must not initialize Windows text
// input services for every recycled value. Create the selectable editor on interaction.
public sealed class DeferredCellText : Grid
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(DeferredCellText), new PropertyMetadata(""));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    private readonly Style _style;
    private readonly TextBlock _label;
    private TextBox? _editor;
    public DeferredCellText(Style style)
    {
        _style = style; Background = Brushes.Transparent; Focusable = true; Cursor = Cursors.IBeam;
        _label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        _label.SetBinding(TextBlock.TextProperty, new Binding(nameof(Text)) { Source = this, Mode = BindingMode.OneWay }); Children.Add(_label);
        MouseEnter += (_, _) => EnsureEditor();
        MouseLeave += (_, _) => ReleaseEditor();
        GotKeyboardFocus += (_, _) => { if (IsKeyboardFocused) EnsureEditor().Focus(); };
        LostKeyboardFocus += (_, _) => ReleaseEditor();
        Unloaded += (_, _) => RemoveEditor();
    }
    public TextBox EnsureEditor()
    {
        if (_editor is not null) return _editor;
        _editor = new TextBox { Style = _style, IsReadOnly = true };
        _editor.SetBinding(TextBox.TextProperty, new Binding(nameof(Text)) { Source = this, Mode = BindingMode.OneWay });
        _label.Visibility = Visibility.Collapsed; Children.Add(_editor);
        return _editor;
    }
    private void ReleaseEditor() { if (!IsMouseOver && !IsKeyboardFocusWithin) RemoveEditor(); }
    private void RemoveEditor()
    {
        if (_editor is null) return;
        Children.Remove(_editor); BindingOperations.ClearAllBindings(_editor); _editor = null; _label.Visibility = Visibility.Visible;
    }
}
