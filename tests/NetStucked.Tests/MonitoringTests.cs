using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;
namespace NetStucked.Tests;
public sealed class MonitoringTests
{
    private static MonitorDevice Device() => new() { Name="Owned device",Host="127.0.0.1" };
    private static MonitorObservation Observation(bool? reachable, DateTimeOffset time, SnmpSnapshot? snmp=null) => new(time,reachable,reachable,reachable==true?1:null,null,"Owned fixture evidence",snmp);
    [Fact] public void ThreeFailuresOpenOneIncidentAndTwoRepliesRecoverSameIncident()
    {
        var policy=new MonitorPolicy(); var device=Device(); var time=DateTimeOffset.UtcNow;
        Assert.Equal(MonitorHealth.Suspected,policy.Apply(device,Observation(false,time)).Snapshot.Health);
        Assert.Empty(policy.Apply(device,Observation(false,time.AddSeconds(10))).Changes);
        var third=policy.Apply(device,Observation(false,time.AddSeconds(20))); var incident=Assert.Single(third.Changes); Assert.Equal(MonitorHealth.Unreachable,third.Snapshot.Health); Assert.Equal(time,incident.StartedAt);
        Assert.Empty(policy.Apply(device,Observation(false,time.AddSeconds(30))).Changes);
        Assert.Equal(MonitorHealth.Recovering,policy.Apply(device,Observation(true,time.AddSeconds(40))).Snapshot.Health);
        var recovered=policy.Apply(device,Observation(true,time.AddSeconds(50))); Assert.Equal(MonitorHealth.Warning,recovered.Snapshot.Health); // Recent loss remains above its separate warning threshold.
        Assert.Equal(incident.Id,Assert.Single(recovered.Changes).Id); Assert.NotNull(recovered.Changes[0].EndedAt);
    }
    [Fact] public void MaintenanceUnknownAndGapsCannotCreateDeviceDown()
    {
        var policy=new MonitorPolicy(); var device=Device(); var time=DateTimeOffset.UtcNow;
        for(int i=0;i<10;i++) Assert.Empty(policy.Apply(device with { Maintenance=true },Observation(false,time)).Changes);
        Assert.Equal(MonitorHealth.Unknown,policy.Apply(device,Observation(null,time)).Snapshot.Health);
        policy.Apply(device,Observation(false,time)); policy.Apply(device,Observation(false,time)); policy.ResetCounterBaseline(); Assert.Empty(policy.Apply(device,Observation(false,time)).Changes);
    }
    [Fact] public void OpenPersistedIncidentRecoversWithoutDuplicateStart()
    {
        var d=Device();var incident=new MonitorIncident(Guid.NewGuid(),d.Id,d.Name,"Reachability",DateTimeOffset.UtcNow.AddMinutes(-5),null,"Persisted evidence");var policy=new MonitorPolicy([incident]);
        Assert.Empty(policy.Apply(d,Observation(false,DateTimeOffset.UtcNow)).Changes); Assert.Equal(MonitorHealth.Recovering,policy.Apply(d,Observation(true,DateTimeOffset.UtcNow)).Snapshot.Health);
        Assert.Equal(incident.Id,Assert.Single(policy.Apply(d,Observation(true,DateTimeOffset.UtcNow)).Changes).Id);
    }
    [Fact] public void BandwidthNeedsTwoCompatibleSamplesAndResetsAfterUptimeDecrease()
    {
        var p=new MonitorPolicy(); var d=Device(); var t=DateTimeOffset.UtcNow;
        SnmpSnapshot Sample(int seconds,ulong uptime,ulong bytes,int oper=1) => new(t.AddSeconds(seconds),"Success","Owned","Test-only",uptime,[new(1,"Gi1/0/1",1,oper,100_000_000,bytes,bytes,0,0,0,0,0,0,true)],new Dictionary<string,string>());
        Assert.Null(Assert.Single(p.Apply(d,Observation(true,t,Sample(0,1000,100))).Snapshot.Interfaces).InMbps);
        var measured=Assert.Single(p.Apply(d,Observation(true,t.AddSeconds(10),Sample(10,2000,1_000_100))).Snapshot.Interfaces); Assert.Equal(.8,measured.InMbps); Assert.Equal(.8,measured.UtilizationPercent);
        var icmpOnly=p.Apply(d,Observation(true,t.AddSeconds(11))); Assert.Equal(measured,Assert.Single(icmpOnly.Snapshot.Interfaces));
        Assert.Null(Assert.Single(p.Apply(d,Observation(true,t.AddSeconds(20),Sample(20,10,0))).Snapshot.Interfaces).InMbps);
    }
    [Fact] public void AdminDownIsExcludedAndFailedSnmpLeavesDeviceReachabilitySeparate()
    {
        var d=Device();var t=DateTimeOffset.UtcNow;var snmp=new SnmpSnapshot(t,"Success","","",1,[new(1,"Shutdown",2,2,100,0,0,0,0,0,0,0,0,true)],new Dictionary<string,string>());
        var p=new MonitorPolicy();var sample=p.Apply(d,Observation(true,t,snmp));Assert.Equal("Admin down / excluded",Assert.Single(sample.Snapshot.Interfaces).Status);Assert.Equal(MonitorHealth.Healthy,sample.Snapshot.Health);
        var failure=new SnmpSnapshot(t,"Timeout","","",null,[],new Dictionary<string,string>());Assert.Equal(MonitorHealth.Warning,p.Apply(d,Observation(true,t,failure)).Snapshot.Health);
        Assert.Equal((1UL<<32)-uint.MaxValue+5,MonitorPolicy.Delta(5,uint.MaxValue,false));
    }
    [Fact] public void LinkWarningsSurviveIcmpTicksButOnlyFreshSnmpSamplesCountTowardIncident()
    {
        var p=new MonitorPolicy();var d=Device();var t=DateTimeOffset.UtcNow;
        SnmpSnapshot Link(int second,int oper)=>new(t.AddSeconds(second),"Success","","",(ulong)(1000+second*100),[new(1,"Owned link",1,oper,100000000,100,100,0,0,0,0,0,0,true)],new Dictionary<string,string>());
        Assert.Empty(p.Apply(d,Observation(true,t,Link(0,2))).Changes);
        Assert.Equal(MonitorHealth.Warning,p.Apply(d,Observation(true,t.AddSeconds(10))).Snapshot.Health);
        Assert.Empty(p.Apply(d,Observation(true,t.AddSeconds(20))).Changes);
        Assert.Empty(p.Apply(d,Observation(true,t.AddSeconds(30),Link(30,2))).Changes);
        Assert.Single(p.Apply(d,Observation(true,t.AddSeconds(60),Link(60,2))).Changes);
        Assert.Empty(p.Apply(d,Observation(true,t.AddSeconds(90),Link(90,1))).Changes); // Transition itself is observed link evidence.
        Assert.Empty(p.Apply(d,Observation(true,t.AddSeconds(120),Link(120,1))).Changes);
        Assert.NotNull(Assert.Single(p.Apply(d,Observation(true,t.AddSeconds(150),Link(150,1))).Changes).EndedAt);
    }
    [Fact] public async Task FiftySavedMonitorsUseBoundedWorkersAndStopDrainsEverything()
    {
        var probe=new BlockingProbe();var devices=Enumerable.Range(0,50).Select(i=>Device() with { Name="Owned "+i,Started=true }).ToArray();var store=new MemoryStore(new(1,devices,[],null));
        await using var service=new MonitoringService(probe,store);await service.ResumeSavedAsync();
        for(int i=0;i<100&&probe.Active<8;i++) await Task.Delay(10);
        Assert.Equal(50,service.ActiveCount);Assert.Equal(8,probe.Active);Assert.Equal(8,probe.Maximum);
        await service.StopAllAsync(false);Assert.Equal(0,service.ActiveCount);Assert.Equal(0,probe.Active);Assert.All(store.Data.Devices,d=>Assert.False(d.Started));
    }
    [Fact] public async Task ExitPreservesOnlyPreviouslyStartedDevicesForResume()
    {
        var first=Device();var stopped=Device() with { Name="Stopped" };var store=new MemoryStore(new(1,[first,stopped],[],null));
        await using(var service=new MonitoringService(new BlockingProbe(),store)) { await service.StartAsync(first.Id); await service.StopAllAsync(); }
        Assert.True(store.Data.Devices.Single(d=>d.Id==first.Id).Started);Assert.False(store.Data.Devices.Single(d=>d.Id==stopped.Id).Started);
        await using(var reopened=new MonitoringService(new BlockingProbe(),store)) { await reopened.ResumeSavedAsync();Assert.Equal(1,reopened.ActiveCount); }
    }
    [Fact] public async Task SeparateStorePreservesLegacySettingsAndContainsNoSecrets()
    {
        string root=Path.Combine(Path.GetTempPath(),"NetStucked-owned-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try { var preferences=new UserSettingsStore(root);preferences.Preferences.TargetText="Legacy retained";await preferences.SaveAsync();byte[] original=File.ReadAllBytes(Path.Combine(root,"settings.json"));var store=new MonitorStore(preferences);var d=Device() with { Snmp=true,SnmpSettings=new() { Version=SnmpVersion.V3,Username="engineer" } };await store.SaveAsync(new(1,[d],[],DateTimeOffset.UtcNow),CancellationToken.None);Assert.Equal(d.Id,store.Load().Devices.Single().Id);Assert.Equal(original,File.ReadAllBytes(Path.Combine(root,"settings.json")));Assert.DoesNotContain("Password",File.ReadAllText(Path.Combine(root,"monitoring.json"))); }
        finally { Directory.Delete(root,true); }
    }
    [Theory] [InlineData(SnmpAuth.None,SnmpPrivacy.None)] [InlineData(SnmpAuth.SHA1,SnmpPrivacy.AES128)] [InlineData(SnmpAuth.SHA256,SnmpPrivacy.AES128)] [InlineData(SnmpAuth.MD5,SnmpPrivacy.DES)] [InlineData(SnmpAuth.SHA384,SnmpPrivacy.AES192)] [InlineData(SnmpAuth.SHA512,SnmpPrivacy.AES256)]
    public void ExplicitV3SecuritySelectionsCreateProviders(SnmpAuth auth,SnmpPrivacy privacy) => Assert.NotNull(SharpSnmpClient.Privacy(new() { Version=SnmpVersion.V3,Username="owned",Authentication=auth,Privacy=privacy },new("","owned-auth-password","owned-privacy-password")));
    [Fact] public void InvalidConfigurationCannotLaunchUnboundedOrUnnamedWork()
    {
        Assert.Throws<ArgumentException>(()=>(Device() with { IcmpIntervalSeconds=0 }).Validate());
        Assert.Throws<ArgumentException>(()=>new SnmpSettings { Version=SnmpVersion.V3,Username="",InterfaceIndexes=Enumerable.Range(1,33).ToArray() }.Validate());
        Assert.Throws<ArgumentException>(()=>new SnmpSettings { Version=SnmpVersion.V3,Username="u",Authentication=SnmpAuth.None,Privacy=SnmpPrivacy.AES128 }.Validate());
        Assert.Equal(10,Device().IcmpIntervalSeconds);Assert.Equal(30,Device().SnmpIntervalSeconds);
    }
    private sealed class MemoryStore(MonitorData data):IMonitorStore { public MonitorData Data=data;public MonitorData Load()=>Data;public Task SaveAsync(MonitorData snapshot,CancellationToken token) { Data=snapshot;return Task.CompletedTask; } }
    private sealed class BlockingProbe:IMonitorProbe
    {
        public int Active,Maximum;
        public async Task<MonitorObservation> PollAsync(MonitorDevice device,bool snmp,CancellationToken token) { int active=Interlocked.Increment(ref Active);int max;do { max=Maximum;if(max>=active)break; } while(Interlocked.CompareExchange(ref Maximum,active,max)!=max);try { await Task.Delay(Timeout.Infinite,token);return Observation(true,DateTimeOffset.UtcNow); } finally { Interlocked.Decrement(ref Active); } }
    }
}
