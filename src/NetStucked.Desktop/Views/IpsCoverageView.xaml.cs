using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using NetStucked.Desktop.ViewModels;

namespace NetStucked.Desktop.Views;

public partial class IpsCoverageView : UserControl
{
    public IpsCoverageView() { InitializeComponent(); Unloaded += (_, _) => ApiKey.Clear(); }
    private void SaveKey_Click(object sender, RoutedEventArgs e) { if (DataContext is IpsCoverageViewModel vm) { string key = ApiKey.Password; ApiKey.Clear(); vm.SaveApiKey(key); } }
    private void Files_DragOver(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private async void Files_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true; if (DataContext is not IpsCoverageViewModel vm) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } paths) await vm.ImportAsync(paths[0]);
        else vm.Message = "Drop one VA .xlsx or .csv at a time. Use Sources → Import IPS Protections CSV for a Check Point snapshot.";
    }
    private static void OpenDocumentation(string url) { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (System.ComponentModel.Win32Exception) { } }
    private void PaloDocs_Click(object sender, RoutedEventArgs e) => OpenDocumentation("https://pan.dev/threat-vault/docs/examples/get-cve-coverage-information/");
    private void CheckPointDocs_Click(object sender, RoutedEventArgs e) => OpenDocumentation("https://sc1.checkpoint.com/documents/R82/WebAdminGuides/EN/CP_R82_ThreatPrevention_AdminGuide/Content/Topics-TPG/IPS_Protections_for_Custom_Threat_Prevention.htm");
    private void CheckPointAdvisories_Click(object sender, RoutedEventArgs e) => OpenDocumentation("https://advisories.checkpoint.com/");
    private void PaloSearch_Click(object sender, RoutedEventArgs e) => OpenDocumentation("https://threatvault.paloaltonetworks.com/");
}
