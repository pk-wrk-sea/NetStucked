using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace NetStucked.Desktop.Behaviors;

public static class SlideTrack
{
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.RegisterAttached("IsExpanded", typeof(bool), typeof(SlideTrack), new PropertyMetadata(true, Changed));
    public static readonly DependencyProperty IndexProperty = DependencyProperty.RegisterAttached("Index", typeof(int), typeof(SlideTrack), new PropertyMetadata(0));
    public static readonly DependencyProperty AxisProperty = DependencyProperty.RegisterAttached("Axis", typeof(Orientation), typeof(SlideTrack), new PropertyMetadata(Orientation.Horizontal));
    public static readonly DependencyProperty CollapsedSizeProperty = DependencyProperty.RegisterAttached("CollapsedSize", typeof(double), typeof(SlideTrack), new PropertyMetadata(64d));
    private static readonly DependencyProperty SavedProperty = DependencyProperty.RegisterAttached("Saved", typeof(GridLength?), typeof(SlideTrack));
    private static readonly DependencyProperty SavedPixelsProperty = DependencyProperty.RegisterAttached("SavedPixels", typeof(double), typeof(SlideTrack), new PropertyMetadata(240d));
    public static void SetIsExpanded(DependencyObject value, bool expanded) => value.SetValue(IsExpandedProperty, expanded);
    public static bool GetIsExpanded(DependencyObject value) => (bool)value.GetValue(IsExpandedProperty);
    public static void SetIndex(DependencyObject value, int index) => value.SetValue(IndexProperty, index);
    public static int GetIndex(DependencyObject value) => (int)value.GetValue(IndexProperty);
    public static void SetAxis(DependencyObject value, Orientation axis) => value.SetValue(AxisProperty, axis);
    public static Orientation GetAxis(DependencyObject value) => (Orientation)value.GetValue(AxisProperty);
    public static void SetCollapsedSize(DependencyObject value, double size) => value.SetValue(CollapsedSizeProperty, size);
    public static double GetCollapsedSize(DependencyObject value) => (double)value.GetValue(CollapsedSizeProperty);
    private static void Changed(DependencyObject value, DependencyPropertyChangedEventArgs e)
    {
        if (value is not Grid grid) return;
        if (!grid.IsLoaded) { RoutedEventHandler? loaded = null; loaded = (_, _) => { grid.Loaded -= loaded; Animate(grid); }; grid.Loaded += loaded; }
        else Animate(grid);
    }
    private static void Animate(Grid grid)
    {
        bool horizontal = GetAxis(grid) == Orientation.Horizontal;
        int index = GetIndex(grid);
        if (horizontal && index >= grid.ColumnDefinitions.Count || !horizontal && index >= grid.RowDefinitions.Count) return;
        var definition = horizontal ? (DependencyObject)grid.ColumnDefinitions[index] : grid.RowDefinitions[index];
        var property = horizontal ? ColumnDefinition.WidthProperty : RowDefinition.HeightProperty;
        var current = (GridLength)definition.GetValue(property);
        double actual = horizontal ? ((ColumnDefinition)definition).ActualWidth : ((RowDefinition)definition).ActualHeight;
        bool expanded = GetIsExpanded(grid);
        if (!expanded && !((ContentElement)definition).HasAnimatedProperties) { grid.SetValue(SavedProperty, current); grid.SetValue(SavedPixelsProperty, actual); }
        var saved = (GridLength?)grid.GetValue(SavedProperty) ?? current;
        double target = expanded ? saved.IsAbsolute ? saved.Value : (double)grid.GetValue(SavedPixelsProperty) : GetCollapsedSize(grid);
        GridLength final = expanded ? saved : new GridLength(GetCollapsedSize(grid));
        var animation = new GridTrackAnimation { From = actual, To = target, Duration = TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 200 : 0), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
        animation.Completed += (_, _) => { ((ContentElement)definition).BeginAnimation(property, null); definition.SetValue(property, final); };
        ((ContentElement)definition).BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }
    private sealed class GridTrackAnimation : AnimationTimeline
    {
        public double From { get; init; }
        public double To { get; init; }
        public IEasingFunction? EasingFunction { get; init; }
        public override Type TargetPropertyType => typeof(GridLength);
        protected override Freezable CreateInstanceCore() => new GridTrackAnimation { From = From, To = To, EasingFunction = EasingFunction };
        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock clock)
        {
            double progress = clock.CurrentProgress ?? 0;
            progress = EasingFunction?.Ease(progress) ?? progress;
            return new GridLength(Math.Max(0, From + (To - From) * progress));
        }
    }
}
