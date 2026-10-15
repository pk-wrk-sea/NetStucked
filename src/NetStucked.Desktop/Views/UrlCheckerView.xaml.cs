using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using NetStucked.Desktop.ViewModels;
namespace NetStucked.Desktop.Views;
public partial class UrlCheckerView : UserControl
{
    private WebView2? _browser;
    private bool _opening;
    public UrlCheckerView() { InitializeComponent(); Unloaded += (_, _) => { _browser?.Dispose(); _browser = null; BrowserHost.Content = null; }; }
    private void SaveKey_Click(object sender, RoutedEventArgs e)
    {
        try { ((UrlCheckerViewModel)DataContext).SaveKey(ApiKeyBox.Password); ApiKeyBox.Clear(); }
        catch (Exception ex) when (ex is ArgumentException or System.ComponentModel.Win32Exception) { ((UrlCheckerViewModel)DataContext).Message = "Unable to save the key. Enter a valid account key and check Windows Credential Manager access."; }
    }
    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(((UrlCheckerViewModel)DataContext).ValidUrl().AbsoluteUri); BrowserStatus.Text = "URL copied. Paste it into the provider's search field."; }
        catch (ArgumentException) { BrowserStatus.Text = "Enter a valid website URL first."; }
    }
    private async void OpenPalo_Click(object sender, RoutedEventArgs e) => await OpenPaloAsync();
    public async Task OpenPaloAsync()
    {
        if (_opening) return;
        _opening = true;
        try
        {
            if (_browser is null)
            {
                var vm = (UrlCheckerViewModel)DataContext;
                _browser = new WebView2 { CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = Path.Combine(vm.Store.DirectoryPath, "browser", "palo-alto") } };
                BrowserHost.Content = _browser;
                var opening = _browser; await opening.EnsureCoreWebView2Async().WaitAsync(TimeSpan.FromSeconds(30));
                if (_browser != opening || !IsLoaded) return;
                _browser.CoreWebView2.Settings.AreHostObjectsAllowed = false;
                _browser.CoreWebView2.Settings.IsWebMessageEnabled = false;
                _browser.CoreWebView2.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
                _browser.CoreWebView2.DownloadStarting += (_, args) => args.Cancel = true;
                _browser.CoreWebView2.NewWindowRequested += (_, args) => args.Handled = true;
                _browser.CoreWebView2.NavigationStarting += (_, args) => { if (!Allowed(args.Uri)) { args.Cancel = true; BrowserStatus.Text = "Navigation outside Palo Alto provider pages was blocked."; } };
                _browser.CoreWebView2.NavigationCompleted += (_, args) => BrowserStatus.Text = args.IsSuccess ? "Actual Palo Alto Test A Site loaded. Use its search field and complete any provider captcha/verification to retrieve categories." : "Provider page did not load: " + args.WebErrorStatus;
            }
            _browser.Source = new Uri("https://urlfiltering.paloaltonetworks.com/");
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or System.Runtime.InteropServices.COMException or InvalidOperationException or ObjectDisposedException or IOException or TimeoutException)
        { BrowserStatus.Text = "Embedded browser is unavailable. Install/repair Microsoft Edge WebView2 Runtime, then reopen this page."; _browser?.Dispose(); _browser = null; BrowserHost.Content = null; }
        finally { _opening = false; }
    }
    public static bool Allowed(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0 && (uri.Host.Equals("paloaltonetworks.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".paloaltonetworks.com", StringComparison.OrdinalIgnoreCase));
}
