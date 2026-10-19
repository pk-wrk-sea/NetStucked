using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using NetStucked.Core;
using NetStucked.Desktop.Services;
using NetStucked.Desktop.ViewModels;

namespace NetStucked.Desktop.Views;

public partial class TerminalView : UserControl, ITerminalRenderer
{
    private const string Origin = "https://terminal.netstucked.invalid/";
    private WebView2? _browser;
    private Task? _initialization;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Dictionary<long, TaskCompletionSource> _acks = [];
    private readonly HashSet<Guid> _created = [];
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DispatcherTimer _themeTimer;
    private long _sequence;
    private bool _stopped;
    private string _theme = "";
    private TerminalWorkspaceViewModel Vm => (TerminalWorkspaceViewModel)DataContext;
    public TerminalView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) => { if(e.OldValue is TerminalWorkspaceViewModel old) old.PropertyChanged -= SelectedChanged; if(e.NewValue is TerminalWorkspaceViewModel vm) {vm.Renderer=this;vm.PropertyChanged+=SelectedChanged;} };
        IsVisibleChanged += async (_, _) => { if(IsVisible && !_stopped) {try {await EnsureAsync();await Vm.LoadAsync();await SelectAsync();}catch {Vm.SelectedTab.Message="Terminal requires Microsoft Edge WebView2 Runtime. No connection was started.";} } };
        _themeTimer=new DispatcherTimer(TimeSpan.FromMilliseconds(400),DispatcherPriority.Background,async (_,_)=>{if(_ready.Task.IsCompletedSuccessfully&&!_stopped&&_theme!=ThemeService.CurrentTheme) {try {await ApplyThemeAsync();}catch {await RendererFailedAsync();}}},Dispatcher);
        _themeTimer.Start();
    }
    private async void SelectedChanged(object? sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName!=nameof(Vm.SelectedTab))return;Password.Clear();Passphrase.Clear();
        if(_ready.Task.IsCompletedSuccessfully&&!_stopped)try{await SelectAsync();}catch{await RendererFailedAsync();}
    }
    private async Task EnsureAsync()
    {
        if(_stopped)throw new ObjectDisposedException(nameof(TerminalView));
        _initialization??=InitializeBrowserAsync();await _initialization;await _ready.Task.WaitAsync(TimeSpan.FromSeconds(20),_lifetime.Token);
    }
    private async Task InitializeBrowserAsync()
    {
        string data=Path.Combine(Environment.GetEnvironmentVariable("NETSTUCKED_QA_DATA_DIRECTORY")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NetStucked"),"TerminalBrowser");
        var environment=await CoreWebView2Environment.CreateAsync(null,data,new CoreWebView2EnvironmentOptions("--disable-features=msEdgeSidebarV2"));
        _browser=new WebView2();BrowserHost.Content=_browser;await _browser.EnsureCoreWebView2Async(environment);
        var core=_browser.CoreWebView2;
        core.SetVirtualHostNameToFolderMapping("terminal.netstucked.invalid",Path.Combine(AppContext.BaseDirectory,"Resources","Terminal"),CoreWebView2HostResourceAccessKind.DenyCors);
        core.Settings.AreHostObjectsAllowed=false;core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreBrowserAcceleratorKeysEnabled=false;core.Settings.IsStatusBarEnabled=false;core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
        core.NavigationStarting+=(_,e)=>{if(e.Uri!=Origin+"index.html")e.Cancel=true;};
        core.NewWindowRequested+=(_,e)=>e.Handled=true;core.DownloadStarting+=(_,e)=>e.Cancel=true;core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
        core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
        core.WebResourceRequested+=(_,e)=>{if(!Uri.TryCreate(e.Request.Uri,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="terminal.netstucked.invalid"||uri.Port!=443||uri.Query.Length>0||!new[]{"/index.html","/xterm.js","/xterm.css","/addon-fit.js","/terminal.js","/terminal.css"}.Contains(uri.AbsolutePath)) e.Response=environment.CreateWebResourceResponse(null,403,"Forbidden","");};
        core.WebMessageReceived+=MessageReceived;
        core.ProcessFailed+=async (_,_)=>await RendererFailedAsync();
        core.Navigate(Origin+"index.html");
    }
    private async void MessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if(_stopped||e.Source!=Origin+"index.html")return;
        try
        {
            string json=e.WebMessageAsJson;if(json.Length>524288)throw new FormatException();
            using var document=JsonDocument.Parse(json);var m=document.RootElement;string? kind=m.GetProperty("kind").GetString();
            if(kind=="ready") {_ready.TrySetResult();await ApplyThemeAsync();return;}
            if(kind is "ack" or "error") {long seq=m.GetProperty("seq").GetInt64();if(_acks.Remove(seq,out var ack)){if(kind=="ack")ack.TrySetResult();else ack.TrySetException(new InvalidOperationException("Renderer rejected command."));}return;}
            Guid id=Guid.Parse(m.GetProperty("tab").GetString()!);var tab=Vm.Tabs.FirstOrDefault(t=>t.Id==id);if(tab is null)return;
            switch(kind)
            {
                case "input": byte[] bytes=Convert.FromBase64String(m.GetProperty("data").GetString()!);if(bytes.Length is <1 or >4096)throw new FormatException();await Vm.ReceiveAsync(id,Guid.Parse(m.GetProperty("generation").GetString()!),bytes);break;
                case "resize": await Vm.ResizeAsync(id,Guid.Parse(m.GetProperty("generation").GetString()!),m.GetProperty("cols").GetInt32(),m.GetProperty("rows").GetInt32());break;
                case "paste": if(Vm.SelectedTab==tab)await PasteAsync(tab);break;
                case "copy": if(Vm.SelectedTab==tab)await SendAsync(new{kind="copy",tab=id});break;
                case "selection": string text=m.GetProperty("text").GetString()??"";if(text.Length>65536)throw new FormatException();if(text.Length>0)Clipboard.SetText(text);break;
                default: throw new FormatException();
            }
        }
        catch (System.Runtime.InteropServices.COMException) { Vm.SelectedTab.Message="Clipboard is busy. Try again."; }
        catch (Exception) { await RendererFailedAsync(); }
    }
    private async Task SendAsync(object command,CancellationToken token=default)
    {
        await EnsureAsync();token.ThrowIfCancellationRequested();
        long seq=++_sequence;var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);_acks.Add(seq,completion);
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(command));
        var values=json.RootElement.EnumerateObject().ToDictionary(p=>p.Name,p=>(object?)p.Value.Clone());values["seq"]=seq;
        try {_browser!.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(values));await completion.Task.WaitAsync(TimeSpan.FromSeconds(15),token).WaitAsync(_lifetime.Token);}
        finally {_acks.Remove(seq);}
    }
    public async Task BeginAsync(Guid tab,Guid generation) {await SendAsync(new{kind="begin",tab,generation});_created.Add(tab);if(Vm.SelectedTab.Id==tab)await SendAsync(new{kind="select",tab});}
    public Task WriteAsync(Guid tab,TerminalFrame frame,CancellationToken token)=>SendAsync(new{kind="write",tab,generation=frame.Generation,data=Convert.ToBase64String(frame.Bytes)},token);
    public async Task RemoveAsync(Guid tab) {if(_created.Remove(tab))await SendAsync(new{kind="remove",tab});}
    private async Task SelectAsync() {var tab=Vm.SelectedTab;if(!_created.Contains(tab.Id))await BeginAsync(tab.Id,tab.Session.Generation);else await SendAsync(new{kind="select",tab=tab.Id});}
    private async Task ApplyThemeAsync() {_theme=ThemeService.CurrentTheme;await SendAsync(new{kind="theme",dark=_theme=="Dark"});}
    private async Task RendererFailedAsync()
    {
        if(_stopped)return;await StopAsync();await Vm.StopForUpdateAsync();Vm.SelectedTab.Message="Terminal renderer stopped; all terminal connections were closed.";
    }
    public Task StopAsync()
    {
        if(_stopped)return Task.CompletedTask;_stopped=true;_themeTimer.Stop();_lifetime.Cancel();_ready.TrySetCanceled();foreach(var ack in _acks.Values)ack.TrySetCanceled();_acks.Clear();_browser?.Dispose();_browser=null;BrowserHost.Content=null;return Task.CompletedTask;
    }
    private async void Connect_Click(object sender,RoutedEventArgs e) {string password=Password.Password,passphrase=Passphrase.Password;Password.Clear();Passphrase.Clear();await Vm.ConnectAsync(password,passphrase);}
    private void Key_Click(object sender,RoutedEventArgs e) {var dialog=new Microsoft.Win32.OpenFileDialog{Title="Choose SSH private key",CheckFileExists=true};if(dialog.ShowDialog()==true)Vm.SelectedTab.KeyPath=dialog.FileName;}
    private async void Copy_Click(object sender,RoutedEventArgs e) {try{await SendAsync(new{kind="copy",tab=Vm.SelectedTab.Id});}catch{Vm.SelectedTab.Message="Copy unavailable. Open the terminal first.";}}
    private async void Paste_Click(object sender,RoutedEventArgs e) {try{await PasteAsync(Vm.SelectedTab);}catch(System.Runtime.InteropServices.COMException){Vm.SelectedTab.Message="Clipboard is busy. Try again.";}catch{await RendererFailedAsync();}}
    private async Task PasteAsync(TerminalTabViewModel tab)
    {
        if(tab.State!=TerminalState.Connected||!Clipboard.ContainsText())return;await PasteTextAsync(tab.Id,Clipboard.GetText());
    }
    public async Task PasteTextAsync(Guid id,string text)
    {
        var tab=Vm.Tabs.FirstOrDefault(t=>t.Id==id);if(tab is null||!Vm.ApprovePaste(tab,text))return;
        await SendAsync(new{kind="paste",tab=tab.Id,generation=tab.Session.Generation,text});
    }
}
