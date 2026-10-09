using System.Windows;
using System.Windows.Controls;
using NetStucked.Desktop.Behaviors;

namespace NetStucked.Desktop.Views;

public partial class PortTestView : UserControl
{
    public PortTestView() => InitializeComponent();
    private void Columns_Click(object sender, RoutedEventArgs e) => ColumnLayout.ShowChooser(ResultsGrid);
    private void FitResults_Click(object sender, RoutedEventArgs e) => GridTools.FitColumns(ResultsGrid);
    private void FitHistory_Click(object sender, RoutedEventArgs e) => GridTools.FitColumns(HistoryGrid);
}
