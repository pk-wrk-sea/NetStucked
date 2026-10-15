using System.Windows;
using System.Windows.Controls;
using NetStucked.Desktop.ViewModels;
namespace NetStucked.Desktop.Views;
public partial class MonitoringView : UserControl
{
    public MonitoringView() => InitializeComponent();
    private async void TestSnmp_Click(object sender, RoutedEventArgs e) => await ((MonitoringViewModel)DataContext).TestSnmpAsync(CommunityBox.Password, AuthPasswordBox.Password, PrivacyPasswordBox.Password);
    private void SaveCredentials_Click(object sender, RoutedEventArgs e)
    {
        try { ((MonitoringViewModel)DataContext).SaveCredentials(CommunityBox.Password, AuthPasswordBox.Password, PrivacyPasswordBox.Password); CommunityBox.Clear(); AuthPasswordBox.Clear(); PrivacyPasswordBox.Clear(); }
        catch (Exception ex) when (ex is ArgumentException or System.ComponentModel.Win32Exception) { ((MonitoringViewModel)DataContext).Message = "Credentials could not be saved. Check the selected SNMP version/password lengths and Windows Credential Manager."; }
    }
}
