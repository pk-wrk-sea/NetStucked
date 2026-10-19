using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NetStucked.Core;
using NetStucked.Desktop;
using NetStucked.Desktop.Services;
using NetStucked.Desktop.ViewModels;
using NetStucked.Desktop.Views;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;
internal static partial class Program
{
    private static async Task<TerminalHostKeyDialog> WaitForHostKeyDialogAsync(Task operation)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while(true)
        {
            if(Application.Current.Windows.OfType<TerminalHostKeyDialog>().FirstOrDefault() is {} dialog)return dialog;
            if(operation.IsCompleted){await operation;throw new InvalidOperationException("Expected an SSH host-key dialog.");}
            await Task.Delay(20,timeout.Token);
        }
    }
    private static void ClickHostKeyButton(TerminalHostKeyDialog dialog,string name) => Descendants<Button>((DependencyObject)dialog.Content).Single(b=>AutomationProperties.GetName(b)==name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static async Task CheckHostKeyDialogsAsync(Application app,MainWindow window,ServiceProvider provider,TerminalWorkspaceViewModel workspace,QaDialogs dialogs,string pin,string output)
    {
        var keys=provider.GetRequiredService<ITerminalKnownHostStore>();var saved=AssertKnown((await keys.LoadAsync(default)).Single(),pin);
        var changed=new TerminalHostKeyChallenge(saved with{Fingerprint="SHA256:"+new string('A',43)},saved);
        vmTheme("Dark");var prompt=provider.GetRequiredService<ITerminalHostKeyPrompt>();Task<TerminalHostKeyDecision> pending=prompt.ConfirmAsync(changed,default);var dialog=await WaitForHostKeyDialogAsync(pending);
        Require(dialog.Challenge.Previous!.Fingerprint==pin&&dialog.Title.Contains("changed"),"Changed-key dialog shows previous and presented identities");
        ClickHostKeyButton(dialog,"Accept and save SSH key");Require(dialog.IsVisible&&dialog.Decision==TerminalHostKeyDecision.Reject,"Changed key cannot be accepted before explicit verification checkbox");
        Render(dialog,output,"SSH-changed-host-key-Dark",650,570,1);
        ((CheckBox)dialog.FindName("ReplacementConsent")).IsChecked=true;ClickHostKeyButton(dialog,"Accept SSH key once");Require(await pending==TerminalHostKeyDecision.AcceptOnce,"Actual changed-key dialog supports explicit Accept Once");
        using(var cancellation=new CancellationTokenSource())
        {
            Task<TerminalHostKeyDecision> wait=prompt.ConfirmAsync(new(saved,null),cancellation.Token);await WaitForHostKeyDialogAsync(wait);cancellation.Cancel();bool cancelled=false;try{await wait;}catch(OperationCanceledException){cancelled=true;}Require(cancelled&&!app.Windows.OfType<TerminalHostKeyDialog>().Any(),"Cancelling connection closes pending host-key dialog");
        }
        await workspace.RefreshHostKeysCommand.ExecuteAsync(null);var manager=new TerminalKnownHostsWindow{Owner=window,DataContext=workspace};manager.Show();
        var grid=Descendants<DataGrid>((DependencyObject)manager.Content).Single();Require(grid.CanUserResizeColumns&&workspace.KnownHosts.Single().Fingerprint==pin,"Saved-host manager displays actual keys with resizable columns");Render(manager,output,"SSH-saved-hosts-Dark",850,440,1);
        workspace.SelectedKnownHost=workspace.KnownHosts.Single();dialogs.ConfirmGeneral=false;await workspace.RemoveHostKeyCommand.ExecuteAsync(null);Require((await keys.LoadAsync(default)).Count==1,"Declining key removal preserves trust");
        dialogs.ConfirmGeneral=true;await workspace.RemoveHostKeyCommand.ExecuteAsync(null);dialogs.ConfirmGeneral=false;Require((await keys.LoadAsync(default)).Count==0,"Confirmed key removal forgets device without network work");manager.Close();
        void vmTheme(string theme)=>provider.GetRequiredService<MainViewModel>().Appearance.SelectedTheme=theme;
        static TerminalKnownHost AssertKnown(TerminalKnownHost host,string expected){Require(host.Fingerprint==expected,"First-use Accept & Save persists actual SSH key");return host;}
    }
    private static async Task CheckInteractiveHostTrustAsync(UserSettingsStore settings,TerminalProfile profile,CancellationToken token)
    {
        var store=new TerminalKnownHostStore(settings);var prompt=new OwnedHostKeyPrompt();var trust=new TerminalHostTrust(store,prompt);var transport=new TerminalTransport(trust);
        var request=new TerminalRequest(profile with{HostPin=""},new("owned-terminal-password",""));
        async Task Connect(){await using var connection=await transport.ConnectAsync(request,token);}
        async Task Reject(){try{await Connect();throw new InvalidOperationException("Key accepted unexpectedly.");}catch(CollectorException ex){Require(ex.Failure==CollectorFailure.HostKey,"Rejected interactive SSH key fails before authentication");}}
        await Reject();Require(prompt.Seen.Single().Previous is null,"Forgotten host prompts again even when legacy profile metadata exists");
        prompt.Decision=TerminalHostKeyDecision.AcceptOnce;await Connect();await Connect();Require(prompt.Seen.Count==3&&(await store.LoadAsync(token)).Count==0,"Real SSH Accept Once requires approval next connection and writes no saved key");
        prompt.Decision=TerminalHostKeyDecision.AcceptAndSave;await Connect();int seen=prompt.Seen.Count;
        await using(var connection=await new TerminalTransport(new TerminalHostTrust(new TerminalKnownHostStore(settings),prompt)).ConnectAsync(request,token)){}
        Require(prompt.Seen.Count==seen,"Real SSH saved host connects again without fingerprint input or another prompt");
        var known=(await store.LoadAsync(token)).Single();await store.SaveAsync(known with{Fingerprint="SHA256:"+new string('A',43)},known.Fingerprint,token);
        prompt.Decision=TerminalHostKeyDecision.Reject;await Reject();Require(prompt.Seen.Last().Changed&&prompt.Seen.Last().Previous!.Fingerprint!=profile.HostPin,"Real SSH changed key is presented for review and never silently replaced");
        prompt.Decision=TerminalHostKeyDecision.AcceptAndSave;await Connect();Require((await store.LoadAsync(token)).Single().Fingerprint==profile.HostPin,"Explicit real SSH replacement saves the presented key after approval");
        await store.RemoveAsync((await store.LoadAsync(token)).Single(),token);prompt.Wait=true;
        using var cancellation=new CancellationTokenSource(TimeSpan.FromMilliseconds(500));bool cancelled=false;
        try {await using var connection=await transport.ConnectAsync(new(profile,new("owned-terminal-password","")),cancellation.Token);}
        catch(OperationCanceledException){cancelled=true;}
        Require(cancelled&&prompt.Seen.Last().Previous is null,"Real SSH cancellation while trust is pending ignores legacy pins and sends no password");
    }
    private sealed class OwnedHostKeyPrompt : ITerminalHostKeyPrompt
    {
        public List<TerminalHostKeyChallenge> Seen {get;}=[];public TerminalHostKeyDecision Decision;public bool Wait;
        public async Task<TerminalHostKeyDecision> ConfirmAsync(TerminalHostKeyChallenge challenge,CancellationToken token){token.ThrowIfCancellationRequested();Seen.Add(challenge);if(Wait)await Task.Delay(Timeout.Infinite,token);return Decision;}
    }
}
