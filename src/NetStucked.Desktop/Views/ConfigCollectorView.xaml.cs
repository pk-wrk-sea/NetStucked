using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NetStucked.Desktop.ViewModels;
using System.Diagnostics;
using System.IO;

namespace NetStucked.Desktop.Views;
public partial class ConfigCollectorView : UserControl
{
    public ConfigCollectorView() { InitializeComponent(); Unloaded += (_, _) => ClearPasswords(); }
    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        var vm = (ConfigCollectorViewModel)DataContext;
        if (HasInvalidInput(this)) { vm.Message = "Correct invalid numeric fields before starting."; ClearPasswords(); return; }
        // Take all password values once, then immediately clear the view; no secret binding/preferences.
        bool shown = ShowPasswords.IsChecked == true;
        var task = vm.StartAsync(shown ? RevealPassword1.Text : Password1.Password, shown ? RevealEnable1.Text : Enable1.Password, shown ? RevealPassword2.Text : Password2.Password, shown ? RevealEnable2.Text : Enable2.Password);
        ClearPasswords(); await task;
    }
    private void ClearPasswords()
    {
        Password1.Clear(); Enable1.Clear(); Password2.Clear(); Enable2.Clear();
        RevealPassword1.Clear(); RevealEnable1.Clear(); RevealPassword2.Clear(); RevealEnable2.Clear();
        ShowPasswords.IsChecked = false;
    }
    private void ShowPasswords_Changed(object sender, RoutedEventArgs e)
    {
        bool show = ShowPasswords.IsChecked == true;
        foreach (var (password, text) in new[] { (Password1, RevealPassword1), (Enable1, RevealEnable1), (Password2, RevealPassword2), (Enable2, RevealEnable2) })
        {
            if (show) { text.Text = password.Password; password.Visibility = Visibility.Collapsed; text.Visibility = Visibility.Visible; }
            else { password.Password = text.Text; text.Clear(); password.Visibility = Visibility.Visible; text.Visibility = Visibility.Collapsed; }
        }
    }
    private async void HostKeys_Click(object sender, RoutedEventArgs e)
    {
        var vm = (ConfigCollectorViewModel)DataContext;
        await vm.RefreshHostKeysCommand.ExecuteAsync(null);
        new TerminalKnownHostsWindow { Owner = Window.GetWindow(this), DataContext = vm }.ShowDialog();
    }
    private static bool HasInvalidInput(DependencyObject node)
        => Validation.GetHasError(node) || LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().Any(HasInvalidInput);
    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var vm = (ConfigCollectorViewModel)DataContext;
        string? file = vm.SelectedRow is { } row ? new[] { row.Result.TxtPath, row.Result.JsonPath }.FirstOrDefault(s => s.Length > 0) : null;
        string folder = file is null ? vm.OutputFolder : Path.GetDirectoryName(file)!;
        try { if (Directory.Exists(folder)) Process.Start(new ProcessStartInfo(Path.GetFullPath(folder)) { UseShellExecute = true }); else vm.Message = "No capture folder exists yet."; }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or ArgumentException) { vm.Message = "The capture folder could not be opened."; }
    }
    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Collector output folder", Multiselect = false };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) ((ConfigCollectorViewModel)DataContext).OutputFolder = dialog.FolderName;
    }
}
