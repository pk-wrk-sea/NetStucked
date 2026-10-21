using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using NetStucked.Core;
using NetStucked.Desktop;
using NetStucked.Desktop.Services;
using NetStucked.Desktop.ViewModels;
using NetStucked.Desktop.Views;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;
internal static partial class Program
{
    private static async Task RunTerminalOnlyAsync(Application app,string output,string fixture,BindingErrors errors)
    {
        var start=new ProcessStartInfo("python"){RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
        start.ArgumentList.Add(fixture);start.ArgumentList.Add(Path.Combine(output,"ephemeral-fixture-key.pem"));
        using var server=Process.Start(start)??throw new IOException("Owned fixture unavailable.");Task<string> stderr=server.StandardError.ReadToEndAsync();
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var store=new UserSettingsStore(Path.Combine(output,"isolated-user-data"));
        Environment.SetEnvironmentVariable("NETSTUCKED_QA_DATA_DIRECTORY",store.DirectoryPath);
        var dialogs=new QaDialogs();await using var provider=App.ConfigureServices(store,dialogs);
        var vm=provider.GetRequiredService<MainViewModel>();MainWindow? window=null;
        try
        {
            using var ready=JsonDocument.Parse(await server.StandardOutput.ReadLineAsync(deadline.Token)??throw new IOException("No fixture ready."));
            int port=ready.RootElement.GetProperty("port").GetInt32();string pin=ready.RootElement.GetProperty("pin").GetString()!,key=ready.RootElement.GetProperty("key").GetString()!;
            var profile=new TerminalProfile(Guid.NewGuid(),"Owned fixture","127.0.0.1",port,Username:"password-fixture",HostPin:pin);
            var transport=new TerminalTransport();
            foreach(string wrongPin in new[]{"","SHA256:"+new string('A',43)})
            {
                try{await transport.ConnectAsync(new(profile with{HostPin=wrongPin},new("owned-terminal-password","")),deadline.Token);throw new Exception("Host key accepted unexpectedly.");}
                catch(CollectorException ex){Require(ex.Message.Contains(pin),"Terminal rejects unknown/changed host keys and reports observed fingerprint");}
            }
            try{await transport.ConnectAsync(new(profile,new("wrong-owned-password","")),deadline.Token);throw new Exception("Wrong password accepted.");}
            catch(CollectorException){Require(true,"Terminal rejects incorrect password without fallback");}
            await using(var keyConnection=await transport.ConnectAsync(new(profile with{Username="key-fixture",Authentication=TerminalAuthentication.PrivateKey,KeyPath=key},new("","owned-key-passphrase")),deadline.Token))
            {byte[] b=new byte[4096];Require(await keyConnection.ReadAsync(b,deadline.Token)>0,"Real SSH authenticates with encrypted private key and passphrase");await keyConnection.ResizeAsync(132,44,deadline.Token);}
            provider.GetRequiredService<ThemeService>();window=provider.GetRequiredService<MainWindow>();app.MainWindow=window;window.DataContext=vm;window.Width=1440;window.Height=960;window.ShowInTaskbar=true;window.ShowActivated=false;window.Show();
            vm.NavigateCommand.Execute("Terminal");await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var view=(TerminalView)window.FindName("TerminalSurface");var workspace=vm.Terminal;
            Require(vm.CurrentPage==workspace&&workspace.Tabs.Count==1,"Terminal navigation uses persistent session workspace");
            var tab=workspace.SelectedTab;tab.Name="Owned SSH fixture";tab.Host="127.0.0.1";tab.Port=port;tab.Username="password-fixture";tab.HostPin=pin;
            Task connecting=workspace.ConnectAsync("owned-terminal-password","");
            var hostDialog=await WaitForHostKeyDialogAsync(connecting);
            Require(!hostDialog.Challenge.Changed&&hostDialog.Challenge.Presented.Fingerprint==pin,"First-use SSH dialog displays actual server fingerprint without manual input");
            Render(hostDialog,output,"SSH-new-host-key-Light",650,470,1);
            await Task.Delay(16000);Require(!connecting.IsCompleted,"Operator can review host key longer than the 15-second connection timeout");
            ClickHostKeyButton(hostDialog,"Accept and save SSH key");await connecting;await Until(()=>tab.State==TerminalState.Connected);
            Require(!Descendants<System.Windows.Controls.TextBox>(view).Any(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Terminal SSH fingerprint"),"Terminal connection form has no fingerprint input");
            var browser=(WebView2)typeof(TerminalView).GetField("_browser",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(view)!;
            var core=browser.CoreWebView2;
            const string terminal="document.querySelector('.session.active').terminal";
            async Task<string> Text()=>JsonSerializer.Deserialize<string>(await core.ExecuteScriptAsync($"(()=>{{const t={terminal};return Array.from({{length:t.buffer.active.length}},(_,i)=>t.buffer.active.getLine(i).translateToString(true)).join('\\n');}})()"))??"";
            async Task Expect(string contains){using var wait=new CancellationTokenSource(15000);while(!(await Text()).Contains(contains))await Task.Delay(30,wait.Token);}
            async Task Type(string text)=>await core.ExecuteScriptAsync($"{terminal}.input({JsonSerializer.Serialize(text)},true)");
            await Expect("Unicode ไทย");Require(!(await Text()).Contains('\x1b'),"Actual xterm renders ANSI sequences and UTF-8 as terminal cells");
            Require(await core.ExecuteScriptAsync($"{terminal}.buffer.active.getLine(0).getCell(0).getFgColor()") == "1","Actual xterm applies ANSI red cell attribute");
            await Type("cursor-test\r");await Expect("SECOND");Require(!(await Text()).Contains("first"),"Actual emulator applies carriage return and erase-line");
            await Type("ansi-test\r");await Expect("Primary restored");Require(!(await Text()).Contains("Alternate screen"),"Alternate screen restores primary scrollback");
            await core.ExecuteScriptAsync($"{terminal}.focus()");
            await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent",JsonSerializer.Serialize(new{type="keyDown",key="c",code="KeyC",modifiers=2,windowsVirtualKeyCode=67}));
            await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent",JsonSerializer.Serialize(new{type="keyUp",key="c",code="KeyC",modifiers=2,windowsVirtualKeyCode=67}));
            await Expect("^C");Require(true,"Actual browser keyboard Ctrl+C reaches owned SSH endpoint as interrupt");
            await core.ExecuteScriptAsync("window.clipboardWrites=0;Object.defineProperty(navigator,'clipboard',{value:{writeText:()=>{window.clipboardWrites++;return Promise.resolve();}}})");await Type("osc-test\r");await Expect("Clipboard blocked");Require(await core.ExecuteScriptAsync("window.clipboardWrites")=="0","Remote OSC52 never invokes browser clipboard-write API");
            await view.PasteTextAsync(tab.Id,"single-paste");await Expect("single-paste");Require(true,"Approved single-line paste flows through emulator and SSH input queue");
            await view.PasteTextAsync(tab.Id,"declined-one\rdeclined-two");await Task.Delay(100);Require(!(await Text()).Contains("declined-one"),"Declining multiline confirmation sends no commands");
            await core.ExecuteScriptAsync($"{terminal}.element.dispatchEvent(new WheelEvent('wheel',{{ctrlKey:true,deltaY:-100,bubbles:true,cancelable:true}}))");
            Require(await core.ExecuteScriptAsync($"{terminal}.options.fontSize")=="15","Ctrl+wheel increases terminal text size");
            vm.NavigateCommand.Execute("Live Ping");await Task.Delay(100);vm.NavigateCommand.Execute("Terminal");await Task.Delay(100);Require((await Text()).Contains("Primary restored")&&tab.State==TerminalState.Connected,"Navigation retains live terminal and scrollback");
            workspace.AddTabCommand.Execute(null);await Task.Delay(150);Require(workspace.Tabs.Count==2,"Second terminal tab is independent");workspace.SelectedTab=tab;await Task.Delay(150);Require((await Text()).Contains("Primary restored"),"Switching tabs restores original emulator buffer");
            workspace.SelectedTab=workspace.Tabs[1];await workspace.CloseTabCommand.ExecuteAsync(null);await Task.Delay(100);Require(workspace.Tabs.Count==1&&(await Text()).Contains("Primary restored"),"Closing another tab preserves active connection and terminal buffer");
            for(int i=0;i<5;i++){workspace.AddTabCommand.Execute(null);await Task.Delay(60);}Require(workspace.Tabs.Count==5&&workspace.Message.Contains("Maximum 5"),"Workspace enforces maximum five terminal tabs and reports overflow");
            while(workspace.Tabs.Count>1){workspace.SelectedTab=workspace.Tabs.Last();await workspace.CloseTabCommand.ExecuteAsync(null);}await Task.Delay(100);Require(workspace.SelectedTab==tab&&tab.State==TerminalState.Connected,"Closing all other tabs leaves owned SSH session connected");
            Require(!workspace.Message.Contains("Maximum 5"),"Closing surplus tabs clears obsolete tab-limit warning while the real SSH session remains connected");
            await Type("large-test\r");await Expect("large complete");Require(int.Parse(await core.ExecuteScriptAsync($"{terminal}.buffer.active.length"))<=5200,"Actual terminal scrollback stays within 5000 lines plus visible rows");
            foreach(string theme in new[]{"Light","Dark"})
            {
                vm.Appearance.SelectedTheme=theme;await Task.Delay(600);
                Render(window,output,"Terminal-"+theme+"-chrome",1440,960,1);
                await using var image=File.Create(Path.Combine(output,"Terminal-"+theme+"-emulator.png"));await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,image);
                Require(await core.ExecuteScriptAsync($"{terminal}.options.theme.background")==JsonSerializer.Serialize(theme=="Dark"?"#182230":"#FFFFFF"),"Actual terminal palette follows "+theme);
            }
            window.Width=1100;window.Height=760;await Task.Delay(200);await using(var image=File.Create(Path.Combine(output,"Terminal-narrow-emulator.png")))await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,image);
            if(Environment.GetEnvironmentVariable("NETSTUCKED_QA_NATIVE_CAPTURE")=="1")
            {
                window.Width=1440;window.Height=960;await Task.Delay(200);
                Console.WriteLine("VISIBLE OWNED TERMINAL WINDOW: real loopback SSH connected; native screenshot interval 30 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(30),deadline.Token);
            }
            await vm.PrepareForUpdateAsync();await Until(()=>tab.State==TerminalState.Disconnected);Require(true,"Update preparation drains interactive terminal sessions");
            await workspace.SaveProfileCommand.ExecuteAsync(null);Require(File.Exists(Path.Combine(store.DirectoryPath,"terminal-profiles.json")),"Terminal saves nonsecret connection profile");
            string saved=await File.ReadAllTextAsync(Path.Combine(store.DirectoryPath,"terminal-profiles.json"));Require(!saved.Contains("owned-terminal-password")&&!saved.Contains("owned-key-passphrase"),"Profile excludes password and key passphrase");
            await CheckHostKeyDialogsAsync(app,window,provider,workspace,dialogs,pin,output);
            await CheckInteractiveHostTrustAsync(store,profile,deadline.Token);
            await server.StandardInput.WriteLineAsync("stats");await server.StandardInput.FlushAsync(deadline.Token);string audit=await server.StandardOutput.ReadLineAsync(deadline.Token)??"{}";await File.WriteAllTextAsync(Path.Combine(output,"terminal-fixture-audit.json"),audit);
            using var stats=JsonDocument.Parse(audit);Require(stats.RootElement.GetProperty("auth").GetArrayLength()==8&&stats.RootElement.GetProperty("blank_commands").GetInt32()==0,"Rejected unknown/changed/cancelled host keys send no credentials; exactly eight explicit login attempts and no blank extra commands");
            Require(stats.RootElement.GetProperty("pty").EnumerateArray().All(p=>p.GetProperty("term").GetString()=="xterm-256color")&&stats.RootElement.GetProperty("resize").GetArrayLength()>0,"Real SSH requests xterm PTY and sends window resize events");
            Require(errors.Messages.Count==0,"Terminal has no WPF binding errors");
            await workspace.CloseTabCommand.ExecuteAsync(null);await Task.Delay(100);Require(workspace.Tabs.Count==1&&workspace.SelectedTab!=tab,"Closing last disconnected tab leaves a usable fresh connection tab");
            await File.WriteAllTextAsync(Path.Combine(output,"result.txt"),"PASS: actual xterm/WebView2 + owned loopback SSH. Real Cisco/physical DPI/Serial/SFTP not tested.");
        }
        finally
        {
            await vm.ShutdownAsync();window?.Hide();
            if(!server.HasExited){await server.StandardInput.WriteLineAsync("stop");server.StandardInput.Close();try{await server.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));}catch(TimeoutException){server.Kill(true);}}
            await File.WriteAllTextAsync(Path.Combine(output,"fixture-stderr.txt"),await stderr);
        }
    }
}
