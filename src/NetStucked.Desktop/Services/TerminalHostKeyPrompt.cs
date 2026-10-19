using System.Windows;
using NetStucked.Core;
using NetStucked.Desktop.Views;

namespace NetStucked.Desktop.Services;
public sealed class TerminalHostKeyPrompt : ITerminalHostKeyPrompt
{
    public async Task<TerminalHostKeyDecision> ConfirmAsync(TerminalHostKeyChallenge challenge, CancellationToken token)
    {
        var dispatcher = Application.Current.Dispatcher;
        return await dispatcher.InvokeAsync(() =>
        {
            token.ThrowIfCancellationRequested();
            var window = new TerminalHostKeyDialog(challenge) { Owner = Application.Current.MainWindow };
            using var cancellation = token.Register(() => dispatcher.BeginInvoke(new Action(window.Close)));
            window.ShowDialog(); token.ThrowIfCancellationRequested(); return window.Decision;
        });
    }
}
