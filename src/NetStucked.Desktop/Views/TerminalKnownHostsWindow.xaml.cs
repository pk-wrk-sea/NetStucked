using System.Windows;
using NetStucked.Desktop.Services;
namespace NetStucked.Desktop.Views;
public partial class TerminalKnownHostsWindow : Window
{
    public TerminalKnownHostsWindow() { InitializeComponent(); Icon = BrandAssets.Icon; ThemeService.ApplyWindow(this); }
}
