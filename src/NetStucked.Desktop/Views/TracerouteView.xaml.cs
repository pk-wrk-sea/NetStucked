using System.Windows;
using System.Windows.Controls;
using NetStucked.Desktop.Behaviors;

namespace NetStucked.Desktop.Views;

public partial class TracerouteView : UserControl
{
    public TracerouteView() => InitializeComponent();
    private void Columns_Click(object sender, RoutedEventArgs e) => ColumnLayout.ShowChooser(ResultsGrid);
    private void FitResults_Click(object sender, RoutedEventArgs e) => GridTools.FitColumns(ResultsGrid);
}
