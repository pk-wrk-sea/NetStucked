using System.Windows;
using System.Windows.Controls;
using NetStucked.Desktop.Behaviors;

namespace NetStucked.Desktop.Views;

public partial class NetworkInfoView : UserControl
{
    public NetworkInfoView() => InitializeComponent();
    private void FitProfiles_Click(object sender, RoutedEventArgs e) => GridTools.FitColumns(ProfilesGrid);
    private void Columns_Click(object sender, RoutedEventArgs e) => ColumnLayout.ShowChooser(ProfilesGrid);
}
