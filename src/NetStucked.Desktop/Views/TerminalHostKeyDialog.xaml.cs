using System.Windows;
using NetStucked.Core;
using NetStucked.Desktop.Services;

namespace NetStucked.Desktop.Views;
public partial class TerminalHostKeyDialog : Window
{
    public TerminalHostKeyChallenge Challenge { get; }
    public string TitleText => Challenge.Changed ? "SSH host key has changed" : "New SSH host key";
    public string Explanation => Challenge.Changed ? "This device presented a different key from the one saved. A device rebuild or key rotation can cause this; an unexpected change may indicate a different device. Review both fingerprints before continuing." : "This device is not in your saved host keys yet. Accept for this connection, or save the key to recognize this device next time.";
    public string SaveLabel => Challenge.Changed ? "Replace & Save" : "Accept & Save";
    public TerminalHostKeyDecision Decision { get; private set; }
    public TerminalHostKeyDialog(TerminalHostKeyChallenge challenge)
    {
        Challenge = challenge; Title = TitleText; InitializeComponent(); Title = TitleText; DataContext = this;
        Icon = BrandAssets.Icon; ThemeService.ApplyWindow(this);
    }
    private bool ConfirmChanged()
    {
        if (!Challenge.Changed || ReplacementConsent.IsChecked == true) return true;
        ValidationMessage.Text = "Confirm that you verified the changed key before accepting it."; return false;
    }
    private void Once_Click(object sender, RoutedEventArgs e) { if (ConfirmChanged()) { Decision = TerminalHostKeyDecision.AcceptOnce; DialogResult = true; } }
    private void Save_Click(object sender, RoutedEventArgs e) { if (ConfirmChanged()) { Decision = TerminalHostKeyDecision.AcceptAndSave; DialogResult = true; } }
}
