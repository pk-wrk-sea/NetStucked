using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public class PortTests
{
    [Fact]
    public void ParserSupportsIpv4HostIpv6DescriptionsAndDeduplicatesEndpoints()
    {
        var parsed = PortInputParser.Parse("# comment\nEXAMPLE.COM.:443 HTTPS ไทย\nexample.com 443 duplicate\n127.0.0.1 22 SSH\n[::1]:443 IPv6\n::1 443 duplicate\nexample.com:80 HTTP");
        Assert.True(parsed.IsValid); Assert.Equal(4, parsed.Targets.Count); Assert.Equal("HTTPS ไทย", parsed.Targets[0].Description);
        Assert.Equal("[::1]:443", parsed.Targets[2].Endpoint); Assert.Equal(80, parsed.Targets[3].Port);
    }
    [Theory]
    [InlineData("127.0.0.1")][InlineData("127.0.0.1:0")][InlineData("127.0.0.1:65536")][InlineData("host:-1")]
    [InlineData("https://example.com:443")][InlineData("192.0.2.0/24:80")][InlineData("host:80-100")][InlineData("host:+443")]
    public void InvalidInputDoesNotAuthorizeConnections(string value) => Assert.False(PortInputParser.Parse(value).IsValid);
    [Fact]
    public void ParserEnforcesSizeDescriptionAndEndpointLimits()
    {
        Assert.False(PortInputParser.Parse(new string('a', 1000001)).IsValid);
        Assert.False(PortInputParser.Parse("host 443 " + new string('a', 513)).IsValid);
        var parsed = PortInputParser.Parse("host 80\nhost 443", 1);
        Assert.False(parsed.IsValid); Assert.Single(parsed.Targets);
    }
    [Theory]
    [InlineData(249,500)][InlineData(250,499)][InlineData(60001,500)][InlineData(250,10001)]
    public void SettingsEnforceBounds(int interval, int timeout) => Assert.Throws<ArgumentException>(() => new PortProbeSettings { IntervalMs=interval, TimeoutMs=timeout }.Validate());
    [Fact]
    public async Task BlockedEndpointDoesNotDelayOtherEndpointAndCancelledConnectIsNotFailed()
    {
        var probe = new MeasuredTcp { Handler = async (port, token) => { if (port == 1) await Task.Delay(Timeout.Infinite, token); return new(PortOutcome.Connected, 2.25, "test adapter"); } };
        await using var service = new MultiTargetPortService(probe, new FakeDns());
        await service.StartAsync([new("127.0.0.1",1,"slow"),new("127.0.0.1",2,"fast")], new() { IntervalMs=250 });
        await Eventually.Until(() => service.Snapshot()[1].Attempts >= 4);
        await service.PauseAsync();
        var rows = service.Snapshot(); Assert.Equal(0, rows[0].Attempts); Assert.Equal(0, rows[0].Connected);
        Assert.Equal("Connected",rows[1].Status); Assert.Equal(2.25,rows[1].Last); Assert.Equal(0,probe.Active);
        long attempts=rows[1].Attempts; await Task.Delay(350); Assert.Equal(attempts,service.Snapshot()[1].Attempts);
        await service.ResumeAsync(); await Eventually.Until(() => service.Snapshot()[1].Attempts > attempts);
        await service.StopAsync(); Assert.Equal(0,probe.Active); Assert.Equal(0,probe.Overlap);
    }
    [Fact]
    public async Task CancelPauseStopRestartCannotOverlapOrCountCancelledRequests()
    {
        var probe=new MeasuredTcp { Handler=async (_, token) => { await Task.Delay(Timeout.Infinite,token); throw new InvalidOperationException(); } };
        await using var service=new MultiTargetPortService(probe,new FakeDns());
        for(int i=0;i<4;i++)
        {
            await service.StartAsync([new("127.0.0.1",443,"")],new()); await Eventually.Until(()=>probe.Active==1);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>service.StartAsync([new("127.0.0.1",443,"")],new()));
            await service.PauseAsync(); Assert.Equal(0,probe.Active); Assert.Equal(0,service.Snapshot()[0].Attempts);
            await service.ResumeAsync(); await Eventually.Until(()=>probe.Active==1);
            await service.StopAsync(); Assert.Equal(0,probe.Active); Assert.Equal(0,service.Snapshot()[0].Attempts);
        }
        Assert.Equal(0,probe.Overlap);
    }
    [Fact]
    public async Task SlowDnsDoesNotStarveLiteralEndpointsAndDnsErrorsHaveNoConnectTime()
    {
        var dns=new SlowDns(); var probe=new MeasuredTcp();
        await using var service=new MultiTargetPortService(probe,dns);
        var targets=Enumerable.Range(1,4).Select(i=>new PortTarget($"slow{i}.invalid",443,"")).Append(new("127.0.0.1",80,"")).Append(new("missing.invalid",443,"")).ToArray();
        await service.StartAsync(targets,new() {IntervalMs=250});
        await Eventually.Until(()=>service.Snapshot()[4].Attempts>=4 && service.Snapshot()[5].Status=="DNS error");
        await service.StopAsync(); var error=service.Snapshot()[5];
        Assert.Equal(0,error.Attempts); Assert.Equal(0,error.Connected); Assert.Null(error.Last); Assert.Null(error.Average);
        Assert.Equal("DNS error",Assert.Single(service.History(error.Key)).Status); Assert.Equal(0,probe.Overlap);
    }
    [Fact]
    public async Task GlobalRateConcurrencyAndPerEndpointOwnershipAreBounded()
    {
        var probe=new MeasuredTcp { Handler=async (_,token)=> { await Task.Delay(100,token); return new(PortOutcome.Refused,null,"test refusal"); } };
        await using var service=new MultiTargetPortService(probe,new FakeDns());
        var targets=Enumerable.Range(1,96).Select(i=>new PortTarget("127.0.0.1",i,"")).ToArray();
        await service.StartAsync(targets,new(){IntervalMs=250});
        await Eventually.Until(()=>service.Snapshot().All(r=>r.Attempts>0)); await service.StopAsync();
        Assert.InRange(probe.Maximum,1,32); Assert.Equal(0,probe.Overlap); Assert.Equal(0,probe.Active);
        var ordered=probe.Started.Order().ToArray(); double seconds=(ordered[^1]-ordered[0])/(double)System.Diagnostics.Stopwatch.Frequency;
        Assert.True(ordered.Length <= 128*seconds+10, "TCP rate is limited to 128/s plus a bounded burst.");
        Assert.All(service.Snapshot(),r=> { Assert.Equal("Refused",r.Status); Assert.Null(r.Last); Assert.Equal(100,r.FailurePercent); });
    }
    [Theory]
    [InlineData(PortOutcome.Timeout)][InlineData(PortOutcome.Refused)][InlineData(PortOutcome.Unreachable)][InlineData(PortOutcome.Error)]
    public void FailuresHaveNoInventedTimeAndSuccessfulAverageExcludesFailures(PortOutcome outcome)
    {
        var acc=new PortAccumulator(new("host",443,""),1,2);
        acc.Add(new(PortOutcome.Connected,2,"test")); acc.Add(new(outcome,null,"test")); acc.Add(new(PortOutcome.Connected,4,"test"));
        var row=acc.Snapshot(); Assert.Equal(3,row.Attempts); Assert.Equal(2,row.Connected); Assert.Equal(3,row.Average); Assert.Equal(2,acc.History(100).Count);
        acc.Add(new(outcome,null,"test")); Assert.Null(acc.Snapshot().Last); Assert.Equal(outcome.ToString(),acc.History(1)[0].Status);
    }
    [Theory]
    [Trait("Category","Integration")]
    [InlineData("127.0.0.1")][InlineData("::1")]
    public async Task RealTcpHandshakeAndRefusalAreDistinguished(string literal)
    {
        var address=IPAddress.Parse(literal); var listener=new TcpListener(address,0); listener.Start();
        try
        {
            int port=((IPEndPoint)listener.LocalEndpoint).Port;
            var accept=listener.AcceptTcpClientAsync();
            var result=await new TcpPortProbe().ConnectAsync(address,port,1000,CancellationToken.None);
            using var accepted=await accept.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(PortOutcome.Connected,result.Outcome); Assert.True(result.ConnectMs>=0);
        }
        finally { listener.Stop(); }
        using var reserved=new Socket(address.AddressFamily,SocketType.Stream,ProtocolType.Tcp); reserved.Bind(new IPEndPoint(address,0));
        var refused=await new TcpPortProbe().ConnectAsync(address,((IPEndPoint)reserved.LocalEndPoint!).Port,5000,CancellationToken.None);
        Assert.Equal(PortOutcome.Refused,refused.Outcome); Assert.Null(refused.ConnectMs);
        using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new TcpPortProbe().ConnectAsync(address,443,1000,cancelled.Token));
    }
    [Fact]
    public async Task PreferencesRoundTripPortTemplatesAndLegacySchemaStillLoads()
    {
        string directory=Path.Combine(Path.GetTempPath(),"NetStucked-port-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new UserSettingsStore(directory); store.Preferences.PortTargetText="host:443 ไทย";
            store.Preferences.PortTemplates.Add(new(Guid.NewGuid(),"HTTPS",store.Preferences.PortTargetText));
            store.Preferences.Port=new(){IntervalMs=250,TimeoutMs=500}; await store.SaveAsync();
            var reload=new UserSettingsStore(directory); Assert.Null(reload.LoadError); Assert.Equal(250,reload.Preferences.Port.IntervalMs); Assert.Equal("HTTPS",Assert.Single(reload.Preferences.PortTemplates).Name);
            File.WriteAllText(Path.Combine(directory,"settings.json"),"{}");
            var legacy=new UserSettingsStore(directory); Assert.Null(legacy.LoadError); Assert.Empty(legacy.Preferences.PortTemplates); Assert.Equal(1000,legacy.Preferences.Port.IntervalMs);
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory,true); }
    }
    private sealed class SlowDns : IDnsResolver
    {
        public async Task<IPAddress> ResolveAsync(string host,IpFamily family,CancellationToken token)
        {
            if(host.StartsWith("slow")) await Task.Delay(600,token);
            throw new SocketException((int)SocketError.HostNotFound);
        }
        public Task<string?> ReverseAsync(IPAddress address,CancellationToken token)=>Task.FromResult<string?>(null);
    }
    private sealed class MeasuredTcp : ITcpProbe
    {
        private int _active,_maximum,_overlap;
        private readonly ConcurrentDictionary<string,int> _endpoints=new();
        public int Active=>_active; public int Maximum=>_maximum; public int Overlap=>_overlap;
        public ConcurrentQueue<long> Started { get; }=new();
        public Func<int,CancellationToken,Task<TcpProbeResult>> Handler {get;set;}=(_,_)=>Task.FromResult(new TcpProbeResult(PortOutcome.Connected,1,"test adapter"));
        public async Task<TcpProbeResult> ConnectAsync(IPAddress address,int port,int timeoutMs,CancellationToken token)
        {
            string key=$"{address}:{port}"; if(_endpoints.AddOrUpdate(key,1,(_,v)=>v+1)>1) Interlocked.Increment(ref _overlap);
            int active=Interlocked.Increment(ref _active), previous;
            do {previous=_maximum;} while(active>previous && Interlocked.CompareExchange(ref _maximum,active,previous)!=previous);
            Started.Enqueue(System.Diagnostics.Stopwatch.GetTimestamp());
            try { return await Handler(port,token); }
            finally { Interlocked.Decrement(ref _active); _endpoints.AddOrUpdate(key,0,(_,v)=>v-1); }
        }
    }
}
