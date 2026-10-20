using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NetStucked.Desktop.ViewModels;

namespace NetStucked.Desktop.Views;
public partial class TopologyGraph : UserControl
{
    private TopologyViewModel? _viewModel;
    public TopologyGraph() { InitializeComponent(); Loaded += (_, _) => Attach(); Unloaded += (_, _) => Detach(); DataContextChanged += (_, _) => { Detach(); if (IsLoaded) Attach(); }; }
    private void Attach() { Detach(); _viewModel = DataContext as TopologyViewModel; if (_viewModel is not null) _viewModel.Links.CollectionChanged += LinksChanged; Draw(); }
    private void Detach() { if (_viewModel is not null) _viewModel.Links.CollectionChanged -= LinksChanged; _viewModel = null; }
    private void LinksChanged(object? sender, NotifyCollectionChangedEventArgs e) => Draw();
    private void Draw()
    {
        Surface.Children.Clear(); if (_viewModel is null) return;
        var links = _viewModel.Links.Take(24).ToArray(); Surface.Height = Math.Max(340, links.Length * 88 + 40);
        var root = new TextBlock { Text = (_viewModel.SelectedDevice?.Host ?? "No captured device") + "\nOne hop · captured evidence\nFirst 24 links shown; table lists all", TextWrapping = TextWrapping.Wrap, Width = 220 };
        root.SetResourceReference(ForegroundProperty, "TextBrush"); Canvas.SetLeft(root, 20); Canvas.SetTop(root, 20); Surface.Children.Add(root);
        for (int i = 0; i < links.Length; i++)
        {
            var link = links[i]; double y = i * 88 + 20;
            var line = new Line { X1 = 240, Y1 = 90, X2 = 430, Y2 = y + 28, StrokeThickness = 1.5 }; line.SetResourceReference(Shape.StrokeProperty, "MutedBrush"); Surface.Children.Add(line);
            var button = new Button { Width = 335, MinHeight = 65, Content = new TextBlock { Text = link.Neighbor + "\n" + link.LocalInterface + " ↔ " + link.RemoteInterface + "\n" + link.Classification, TextWrapping = TextWrapping.Wrap }, ToolTip = link.Detail };
            System.Windows.Automation.AutomationProperties.SetName(button, "Inspect neighbor " + link.Neighbor); button.Click += (_, _) => _viewModel.SelectedLink = link;
            Canvas.SetLeft(button, 430); Canvas.SetTop(button, y); Surface.Children.Add(button);
        }
    }
}
