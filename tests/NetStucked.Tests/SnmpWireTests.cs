using System.Net;
using System.Net.Sockets;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;
namespace NetStucked.Tests;
public sealed class SnmpWireTests
{
    [Theory] [InlineData(SnmpVersion.V1)] [InlineData(SnmpVersion.V2c)]
    public async Task RealOwnedUdpAgentReturnsSystemInterfacesAndCounters(SnmpVersion version)
    {
        await using var agent=new OwnedAgent();var client=new SharpSnmpClient();var settings=new SnmpSettings { Version=version,Port=agent.Port,CustomOids=["1.3.6.1.4.1.9.2.1.58.0"] };
        var result=await client.ReadAsync("127.0.0.1",settings,new("owned-community","",""),1000,true,CancellationToken.None);
        Assert.Equal("Success",result.Status);Assert.Equal("Owned SNMP fixture",result.Name);Assert.Equal(12300UL,result.UptimeTicks);
        var row=Assert.Single(result.Interfaces);Assert.Equal(1,row.Index);Assert.Equal("Gi1/0/1",row.Name);Assert.Equal(1,row.OperStatus);Assert.Equal(100_000_000UL,row.SpeedBits);Assert.Equal(1000UL,row.InOctets);Assert.Equal(version!=SnmpVersion.V1,row.Counter64);Assert.Equal("12",result.CustomValues["1.3.6.1.4.1.9.2.1.58.0"]);
        Assert.True(agent.Requests>2);Assert.False(agent.SawMutation);Assert.All(agent.Versions,v=>Assert.Equal(version==SnmpVersion.V1?VersionCode.V1:VersionCode.V2,v));
    }
    [Fact] public async Task ExplicitSelectedInterfaceAvoidsDiscoveryAndUnselectedMetrics()
    {
        await using var agent=new OwnedAgent();var result=await new SharpSnmpClient().ReadAsync("127.0.0.1",new() { Port=agent.Port,InterfaceIndexes=[1],Bandwidth=false,Errors=false },new("owned-community","",""),1000,false,CancellationToken.None);
        Assert.True(result.Success);var row=Assert.Single(result.Interfaces);Assert.Null(row.InOctets);Assert.Null(row.InErrors);Assert.Equal(0,agent.NextRequests);
    }
    [Fact] public async Task SinkTimeoutAndCancellationDoNotInventSnmpMeasurements()
    {
        using var sink=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));var settings=new SnmpSettings { Port=((IPEndPoint)sink.Client.LocalEndPoint!).Port };
        var client=new SharpSnmpClient();var result=await client.ReadAsync("127.0.0.1",settings,new("owned-community","",""),500,false,CancellationToken.None);Assert.Equal("Timeout",result.Status);Assert.Null(result.UptimeTicks);Assert.Empty(result.Interfaces);
        using var cancellation=new CancellationTokenSource(50);await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>client.ReadAsync("127.0.0.1",settings,new("owned-community","",""),2000,false,cancellation.Token));
    }
    [Fact] public async Task V3MissingPasswordsFailBeforeSendingAndDiscoveryCanBeCancelled()
    {
        using var sink=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));var settings=new SnmpSettings { Version=SnmpVersion.V3,Username="owned",Port=((IPEndPoint)sink.Client.LocalEndPoint!).Port };
        var result=await new SharpSnmpClient().ReadAsync("127.0.0.1",settings,new("","short","short"),500,false,CancellationToken.None);Assert.Equal("SNMP error",result.Status);Assert.Equal(0,sink.Available);
        using var cancellation=new CancellationTokenSource(50);await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new SharpSnmpClient().ReadAsync("127.0.0.1",settings,new("","owned-auth-password","owned-privacy-password"),2000,false,cancellation.Token));
    }
    private sealed class OwnedAgent:IAsyncDisposable
    {
        private readonly UdpClient _udp=new(new IPEndPoint(IPAddress.Loopback,0));private readonly CancellationTokenSource _stop=new();private readonly Task _loop;
        public int Port=>((IPEndPoint)_udp.Client.LocalEndPoint!).Port;
        public int Requests,NextRequests;public bool SawMutation;public List<VersionCode> Versions=[];
        private readonly Dictionary<string,ISnmpData> _values=new()
        {
            ["1.3.6.1.2.1.1.1.0"]=new OctetString("Owned fixture, no Cisco device queried"),["1.3.6.1.2.1.1.3.0"]=new TimeTicks(12300),["1.3.6.1.2.1.1.5.0"]=new OctetString("Owned SNMP fixture"),
            ["1.3.6.1.2.1.2.2.1.2.1"]=new OctetString("Gi1/0/1"),["1.3.6.1.2.1.2.2.1.5.1"]=new Gauge32(100000000),["1.3.6.1.2.1.2.2.1.7.1"]=new Integer32(1),["1.3.6.1.2.1.2.2.1.8.1"]=new Integer32(1),["1.3.6.1.2.1.2.2.1.9.1"]=new TimeTicks(0),
            ["1.3.6.1.2.1.2.2.1.10.1"]=new Counter32(1000),["1.3.6.1.2.1.2.2.1.16.1"]=new Counter32(2000),["1.3.6.1.2.1.2.2.1.13.1"]=new Counter32(0),["1.3.6.1.2.1.2.2.1.19.1"]=new Counter32(0),["1.3.6.1.2.1.2.2.1.14.1"]=new Counter32(0),["1.3.6.1.2.1.2.2.1.20.1"]=new Counter32(0),
            ["1.3.6.1.2.1.31.1.1.1.6.1"]=new Counter64(1000),["1.3.6.1.2.1.31.1.1.1.10.1"]=new Counter64(2000),["1.3.6.1.2.1.31.1.1.1.15.1"]=new Gauge32(100),["1.3.6.1.2.1.31.1.1.1.19.1"]=new TimeTicks(0),["1.3.6.1.4.1.9.2.1.58.0"]=new Integer32(12)
        };
        public OwnedAgent()=>_loop=Loop();
        private async Task Loop()
        {
            try { while(true) { var packet=await _udp.ReceiveAsync(_stop.Token);var request=MessageFactory.ParseMessages(packet.Buffer,new UserRegistry()).Single();Requests++;Versions.Add(request.Version);SawMutation|=request.TypeCode() is not (SnmpType.GetRequestPdu or SnmpType.GetNextRequestPdu);
                var results=new List<Variable>();foreach(var variable in request.Pdu().Variables) { string key=variable.Id.ToString().TrimStart('.');if(request.TypeCode()==SnmpType.GetNextRequestPdu) { NextRequests++;key=key=="1.3.6.1.2.1.2.2.1.2"?"1.3.6.1.2.1.2.2.1.2.1":"1.3.6.1.2.1.2.2.1.3.1"; }results.Add(new(new ObjectIdentifier(key),_values.GetValueOrDefault(key)??new Integer32(1))); }
                var response=new ResponseMessage(request.Pdu().RequestId.ToInt32(),request.Version,new OctetString("owned-community"),ErrorCode.NoError,0,results);await _udp.SendAsync(response.ToBytes(),packet.RemoteEndPoint,_stop.Token);
            }}catch(OperationCanceledException) when(_stop.IsCancellationRequested){}catch(ObjectDisposedException) when(_stop.IsCancellationRequested){}
        }
        public async ValueTask DisposeAsync(){_stop.Cancel();_udp.Dispose();await _loop;_stop.Dispose();}
    }
}
