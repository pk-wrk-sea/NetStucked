using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace NetStucked.Desktop.Behaviors;

/// <summary>Lucide outline icons, licensed source SVGs embedded and rendered using native WPF geometry.</summary>
public sealed class NavigationIcon : Control
{
    public static readonly DependencyProperty IconNameProperty = DependencyProperty.Register(nameof(IconName), typeof(string), typeof(NavigationIcon), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
    public string IconName { get => (string)GetValue(IconNameProperty); set => SetValue(IconNameProperty, value); }
    private static readonly Dictionary<string, Geometry> Cache = [];
    protected override void OnRender(DrawingContext context)
    {
        if (IconName.Length == 0) return;
        if (!Cache.TryGetValue(IconName, out var geometry))
        {
            using var stream = typeof(NavigationIcon).Assembly.GetManifestResourceStream("NetStucked.Icons." + IconName + ".svg") ?? throw new InvalidOperationException("Unknown navigation icon.");
            var group = new GeometryGroup(); var document = XDocument.Load(stream);
            double Number(XElement e, string key, double fallback = 0) => e.Attribute(key) is { } a ? double.Parse(a.Value, CultureInfo.InvariantCulture) : fallback;
            foreach (var e in document.Root!.Elements())
            {
                Geometry? part = e.Name.LocalName switch
                {
                    "path" => Geometry.Parse(e.Attribute("d")!.Value),
                    "circle" => new EllipseGeometry(new Point(Number(e, "cx"), Number(e, "cy")), Number(e, "r"), Number(e, "r")),
                    "rect" => new RectangleGeometry(new Rect(Number(e, "x"), Number(e, "y"), Number(e, "width"), Number(e, "height")), Number(e, "rx"), Number(e, "ry", Number(e, "rx"))),
                    "line" => new LineGeometry(new Point(Number(e, "x1"), Number(e, "y1")), new Point(Number(e, "x2"), Number(e, "y2"))),
                    "polyline" => Geometry.Parse("M " + e.Attribute("points")!.Value),
                    _ => null
                };
                if (part is not null) group.Children.Add(part);
            }
            group.Freeze(); geometry = group; Cache[IconName] = geometry;
        }
        var pen = new Pen(Foreground, 1.7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        context.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 24)); context.DrawGeometry(null, pen, geometry); context.Pop();
    }
}
