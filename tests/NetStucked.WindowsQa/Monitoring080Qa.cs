using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NetStucked.Core;
using NetStucked.Desktop;
using NetStucked.Desktop.Behaviors;
using NetStucked.Desktop.ViewModels;
using NetStucked.Desktop.Views;
namespace NetStucked.WindowsQa;
internal static partial class Program
{
    private static async Task Check080Async(MainWindow window, MainViewModel vm, string output)
    {
        vm.NavigateCommand.Execute("MAC Checker");await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        int prior=vm.Mac.Results.Count;vm.Mac.TargetText="Internet 10.100.144.23 14 5811.2245.f922 ARPA Vlan30\nInternet 10.100.144.12 - 8875.56b4.a241 ARPA Vlan30\n10.100.144.13 5811.2245.f922";
        Require(vm.Mac.Results.Count==prior,"Pasting MAC/ARP text does not parse or call the provider before Check");
        await vm.Mac.StartCommand.ExecuteAsync(null);Require(vm.Mac.Results.Count==3&&vm.Mac.HasIpMatches&&vm.Mac.Results[0].IpAddress=="10.100.144.23"&&vm.Mac.Results[2].Address==vm.Mac.Results[0].Address,"Check extracts MACs and preserves same-line IPs and repeated mappings");
        var macGrid=Descendants<DataGrid>((DependencyObject)window.Content).Single(g=>AutomationProperties.GetName(g)=="MAC vendor lookup results");Require(macGrid.Columns.Single(c=>Equals(c.Header,"IP Address")).Visibility==Visibility.Visible,"Detected IPs make the optional IP column visible");
        Require(ResultZoom.GetEnabled(macGrid)&&!ResultZoom.HandleWheel(macGrid,120,ModifierKeys.None),"Ordinary wheel scroll remains unchanged");
        Require(ResultZoom.HandleWheel(macGrid,120,ModifierKeys.Control)&&ResultZoom.GetFactor(macGrid)==1.1,"Ctrl+wheel increases result scale");
        ResultZoom.HandleWheel(macGrid,-120,ModifierKeys.Control);Require(ResultZoom.GetFactor(macGrid)==1,"Ctrl+wheel decreases result scale");
        for(int i=0;i<30;i++)ResultZoom.Adjust(macGrid,1);Require(ResultZoom.GetFactor(macGrid)==2,"Result zoom has a bounded upper limit");ResultZoom.Adjust(macGrid,0);Require(ResultZoom.GetFactor(macGrid)==1,"Zoom resets without changing source results");
        vm.NavigateCommand.Execute("DNS Resolver");await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Require(!vm.Dns.IncludeIpv6Dns&&Descendants<CheckBox>((DependencyObject)window.Content).Any(c=>Equals(c.Content,"Include IPv6 DNS servers")),"Optional active-adapter IPv6 DNS checkbox defaults off; AAAA is independent");
        var dnsGrid=Descendants<DataGrid>((DependencyObject)window.Content).Single(g=>AutomationProperties.GetName(g)=="DNS resolver results");Require(double.IsNaN(dnsGrid.RowHeight)&&dnsGrid.Columns.OfType<CopyableTextColumn>().Single(c=>Equals(c.Header,"Answer data")).Wrap,"DNS answer rows wrap and grow automatically");
        var column=dnsGrid.Columns.Single(c=>Equals(c.Header,"Answer data"));column.Width=1500;ColumnLayout.Capture(dnsGrid);Require(vm.Ping.Store.Preferences.Columns["DnsTest"].Any(c=>c.Key=="Answers"&&c.Width>1200),"Expanded DNS answer column width is retained");
        vm.NavigateCommand.Execute("Monitoring");await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);var monitor=vm.Monitoring;Require(vm.CurrentPage==monitor,"Monitoring is a functional page rather than a placeholder");
        monitor.Name="Owned QA monitor";monitor.Host="127.0.0.1";monitor.UseSnmp=true;monitor.Username="owned-user";monitor.Notifications=false;
        foreach(var version in Enum.GetValues<SnmpVersion>()) { monitor.SnmpVersion=version;await monitor.TestSnmpAsync("owned-community","owned-auth-password","owned-privacy-password");Require(monitor.DiscoveredInterfaces.Count==2&&monitor.Message.Contains("succeeded"),"SNMP Test UI supports explicit "+version+" (test-only adapter)"); }
        monitor.DiscoveredInterfaces[0].Watched=true;monitor.SaveCredentials("owned-community","owned-auth-password","owned-privacy-password");await monitor.SaveCommand.ExecuteAsync(null);
        Require(monitor.SelectedDevice?.Device.SnmpSettings.InterfaceIndexes.SequenceEqual([1])==true,"Selected interfaces and monitor settings are saved independently of secrets");
        await monitor.StartCommand.ExecuteAsync(null);await Until(()=>{monitor.Refresh();return monitor.SelectedDevice?.Snapshot is not null;});Require(monitor.Engine.ActiveCount==1&&monitor.SelectedDevice!.Device.Started,"Explicit Start persists auto-resume intent and begins owned fixture polling");
        foreach(string theme in new[]{"Light","Dark"}) {vm.Appearance.SelectedTheme=theme;Render(window,output,"Monitoring-"+theme.ToLowerInvariant(),1280,800,1);}
        window.EnableTray(true);window.Close();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);Require(!window.IsVisible&&monitor.Engine.ActiveCount==1,"X hides the owned window while monitoring continues");
        window.RestoreFromTray();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);Require(window.IsVisible&&monitor.Engine.ActiveCount==1,"Restore brings back the same live monitoring session");window.EnableTray(false);window.ShowInTaskbar=false;
        await monitor.StopCommand.ExecuteAsync(null);Require(monitor.Engine.ActiveCount==0&&monitor.SelectedDevice?.Device.Started==false,"Explicit Stop drains collection and clears future auto-resume");
        await monitor.StartCommand.ExecuteAsync(null);await vm.PrepareForUpdateAsync();Require(monitor.Engine.ActiveCount==0&&monitor.Engine.Devices.Single().Started,"Update preparation drains Monitoring while preserving requested auto-resume");
        string json=await File.ReadAllTextAsync(Path.Combine(vm.Ping.Store.DirectoryPath,"monitoring.json"));Require(!json.Contains("owned-community")&&!json.Contains("owned-auth-password"),"Monitoring JSON/export state contains no SNMP secrets");
        vm.Appearance.SelectedTheme="Light";vm.NavigateCommand.Execute("Live Ping");
    }
    private sealed class QaSecretVault:ISecretVault
    {private readonly Dictionary<string,string> _values=[];public string? Read(string r)=>_values.GetValueOrDefault(r);public void Write(string r,string s)=>_values[r]=s;public void Delete(string r)=>_values.Remove(r);}
    private sealed class QaSnmpClient:ISnmpClient
    {
        public Task<SnmpSnapshot> ReadAsync(string host,SnmpSettings settings,SnmpSecrets secret,int timeout,bool discover,CancellationToken token)
        {token.ThrowIfCancellationRequested();return Task.FromResult(QaSnmp());}
    }
    private static SnmpSnapshot QaSnmp()=>new(DateTimeOffset.UtcNow,"Success","QA fixture device","Test-only adapter; no Cisco target was contacted",1000,[new(1,"Gi1/0/1",1,1,1000000000,100,100,0,0,0,0,0,0,true),new(2,"Gi1/0/2",2,2,1000000000,0,0,0,0,0,0,0,0,true)],new Dictionary<string,string>());
    private sealed class QaMonitorProbe:IMonitorProbe
    {public Task<MonitorObservation> PollAsync(MonitorDevice device,bool snmp,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(new MonitorObservation(DateTimeOffset.UtcNow,true,true,1,null,"Test-only monitor adapter; no network target contacted",snmp?QaSnmp():null));}}
}
