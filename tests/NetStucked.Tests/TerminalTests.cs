using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Net;
using System.Net.Sockets;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;
public sealed class TerminalTests
{
    private static TerminalProfile Profile => new(Guid.NewGuid(),"Owned fixture","127.0.0.1",Username:@"DOMAIN\fixture");
    private static TerminalRequest Request => new(Profile,new("fixture-password",""));
    [Theory] [InlineData("single line",false,true)] [InlineData("one\rtwo",true,true)] [InlineData("one\ntwo",true,true)] [InlineData("one\r\ntwo",true,true)] [InlineData("\t",false,true)] [InlineData("\0",false,false)] [InlineData("\x1b[2J",false,false)]
    public void ClipboardPolicy(string text,bool confirm,bool valid) {Assert.Equal(confirm,TerminalInputPolicy.RequiresConfirmation(text));Assert.Equal(valid,TerminalInputPolicy.ValidPaste(text));}
    [Fact] public void PasteAndGeometryBounds() {Assert.False(TerminalInputPolicy.ValidPaste(new string('x',65537)));Assert.True(TerminalInputPolicy.ValidPaste(new string('x',65536)));var r=new TerminalRequest(Profile,new("p",""),500,1);Assert.Equal(400,r.Columns);Assert.Equal(5,r.Rows);}
    [Fact] public void SecretsAreExcludedFromJsonAndDiagnostics() {var r=Request;Assert.DoesNotContain("fixture-password",JsonSerializer.Serialize(r));Assert.DoesNotContain("fixture-password",r.ToString());Assert.Equal("{}",JsonSerializer.Serialize(r.Secrets));Assert.DoesNotContain("fixture-password",r.Secrets.ToString());}
    [Theory] [InlineData("host name")] [InlineData("127.0.0.0/24")] [InlineData("https://a")] [InlineData("")]
    public void InvalidHostNeverConnects(string host)=>Assert.Throws<ArgumentException>(()=>new TerminalRequest(Profile with{Host=host},new("p","")).Validate());
    [Fact] public void TelnetRequiresExplicitChoice() {var p=Profile with{Protocol=TerminalProtocol.Telnet};Assert.Throws<ArgumentException>(()=>new TerminalRequest(p,new("","")).Validate());new TerminalRequest(p with{AllowTelnet=true},new("","")).Validate();}
    [Fact] public void KeyPathMustBeAbsolute() => Assert.Throws<ArgumentException>(()=>new TerminalRequest(Profile with{Authentication=TerminalAuthentication.PrivateKey,KeyPath="key.pem"},new("","")).Validate());
    [Fact] public async Task ConnectEchoResizeDisconnectAndNoAutoReconnect()
    {
        var transport=new Transport();await using var session=new TerminalSession(transport);await session.ConnectAsync(Request,default);Guid gen=session.Generation;
        Assert.Equal(TerminalState.Connected,session.State);Assert.True(session.TrySend(gen,Encoding.UTF8.GetBytes("typed\r\x03")));
        Assert.Equal("typed\r\x03",Encoding.UTF8.GetString(await transport.Last!.Written.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2))));
        await transport.Last.Reads.Writer.WriteAsync(Encoding.UTF8.GetBytes("\x1b[31mไทย\x1b[0m"));var frame=await session.Output!.ReadAsync();Assert.Equal(gen,frame.Generation);Assert.Contains("ไทย",Encoding.UTF8.GetString(frame.Bytes));
        await session.ResizeAsync(500,1,default);Assert.Equal((400,5),transport.Last.Size);
        transport.Last.Reads.Writer.Complete();await Until(()=>session.State==TerminalState.Disconnected);Assert.True(transport.Last.Disposed);Assert.Equal(1,transport.Calls);Assert.False(session.TrySend(gen,[3]));
    }
    [Fact] public async Task CancellationUnblocksConnectAndCannotSendStaleGeneration()
    {
        var transport=new Transport{WaitConnect=true};await using var session=new TerminalSession(transport);Task connect=session.ConnectAsync(Request,default);await Until(()=>transport.Calls==1);await session.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(2));await connect;
        Assert.Equal(TerminalState.Disconnected,session.State);transport.WaitConnect=false;await session.ConnectAsync(Request,default);Guid gen=session.Generation;await session.DisconnectAsync();await session.ConnectAsync(Request,default);Assert.NotEqual(gen,session.Generation);Assert.False(session.TrySend(gen,[3]));Assert.True(session.TrySend(session.Generation,[3]));
    }
    [Fact] public async Task OutputBackpressureIsBoundedAndStopDrainsBlockedRead()
    {
        var transport=new Transport();await using var session=new TerminalSession(transport);await session.ConnectAsync(Request,default);
        for(int i=0;i<100;i++)await transport.Last!.Reads.Writer.WriteAsync(new byte[4096]);await Task.Delay(100);
        Assert.InRange(transport.Last!.ReadCount,64,65);await session.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(2));Assert.True(transport.Last.Disposed);
    }
    [Fact] public async Task InputBoundsAndSaturationAreReportedWithoutAcceptingStaleBytes()
    {
        var transport=new Transport{BlockWrites=true};await using var session=new TerminalSession(transport);await session.ConnectAsync(Request,default);Guid gen=session.Generation;
        Assert.False(session.TrySend(gen,new byte[4097]));Assert.False(session.TrySend(gen,[]));
        int accepted=0;for(int i=0;i<100;i++)if(session.TrySend(gen,[3]))accepted++;Assert.InRange(accepted,64,65);await session.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(2));
    }
    [Fact] public async Task FailureDoesNotRevealRemoteExceptionOrRetryCredentials()
    {
        var t=new Transport{Failure=new Exception("password=secret-device-text")};await using var session=new TerminalSession(t);TerminalStatus? last=null;session.Changed+=s=>last=s;await session.ConnectAsync(Request,default);Assert.Equal(TerminalState.Failed,session.State);Assert.DoesNotContain("secret-device-text",last!.Message);Assert.Equal(1,t.Calls);
    }
    [Fact] public async Task ProfileRoundtripNeverPersistsSecretsAndPreservesPreferences()
    {
        string path=Path.Combine(Path.GetTempPath(),"netstucked-terminal-"+Guid.NewGuid());try {var settings=new UserSettingsStore(path);settings.Preferences.TraceTarget="preserved.test";await settings.SaveAsync();byte[] before=await File.ReadAllBytesAsync(Path.Combine(path,"settings.json"));var store=new TerminalProfileStore(settings);var p=Profile;await store.SaveAsync([p],default);Assert.Equal(p,Assert.Single(await store.LoadAsync(default)));Assert.Equal(before,await File.ReadAllBytesAsync(Path.Combine(path,"settings.json")));Assert.DoesNotContain("Password",await File.ReadAllTextAsync(Path.Combine(path,"terminal-profiles.json")));}finally{Directory.Delete(path,true);}
    }
    [Fact] public async Task RawTelnetUsesInteractiveLoginAndEscapesIacWithoutDuplicatingSplitCrLf()
    {
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();using var deadline=new CancellationTokenSource(5000);
        try
        {
            Task<TcpClient> accept=listener.AcceptTcpClientAsync(deadline.Token).AsTask();
            await using var connection=await new TerminalTransport().ConnectAsync(new(Profile with{Port=((IPEndPoint)listener.LocalEndpoint).Port,Protocol=TerminalProtocol.Telnet,AllowTelnet=true},new("must-not-auto-send","")),deadline.Token);
            using var peer=await accept;var stream=peer.GetStream();Assert.False(stream.DataAvailable);
            await stream.WriteAsync(new byte[]{255,251,1,(byte)'L',(byte)'o',(byte)'g',(byte)'i',(byte)'n',(byte)':'},deadline.Token);
            byte[] buffer=new byte[64];int count=await connection.ReadAsync(buffer,deadline.Token);Assert.Equal("Login:",Encoding.UTF8.GetString(buffer,0,count));
            byte[] negotiation=new byte[3];await stream.ReadExactlyAsync(negotiation,deadline.Token);Assert.Equal(new byte[]{255,254,1},negotiation);
            await connection.WriteAsync(new byte[]{(byte)'u',13},deadline.Token);await connection.WriteAsync(new byte[]{10,255},deadline.Token);
            byte[] typed=new byte[5];await stream.ReadExactlyAsync(typed,deadline.Token);Assert.Equal(new byte[]{(byte)'u',13,10,255,255},typed);Assert.False(stream.DataAvailable);
        }
        finally{listener.Stop();}
    }
    [Theory] [InlineData("{broken")] [InlineData("{\"Schema\":1,\"Profiles\":null}")] [InlineData("{\"Schema\":1,\"Profiles\":[null]}")] [InlineData("{\"Schema\":99,\"Profiles\":[]}")]
    public async Task CorruptProfilesArePreservedBeforeSaving(string text)
    {
        string path=Path.Combine(Path.GetTempPath(),"netstucked-terminal-"+Guid.NewGuid());Directory.CreateDirectory(path);try {string file=Path.Combine(path,"terminal-profiles.json");await File.WriteAllTextAsync(file,text);var store=new TerminalProfileStore(new(path));Assert.Empty(await store.LoadAsync(default));await store.SaveAsync([Profile],default);Assert.Equal(text,await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(path,"*.corrupt-*"))));Assert.Single(await store.LoadAsync(default));}finally{Directory.Delete(path,true);}
    }
    private static async Task Until(Func<bool> test) {using var c=new CancellationTokenSource(3000);while(!test())await Task.Delay(10,c.Token);}
    private sealed class Transport : ITerminalTransport
    {
        public int Calls;public bool WaitConnect,BlockWrites;public Exception? Failure;public Connection? Last;
        public async Task<ITerminalConnection> ConnectAsync(TerminalRequest request,CancellationToken token) {Interlocked.Increment(ref Calls);if(WaitConnect)await Task.Delay(Timeout.Infinite,token);if(Failure is not null)throw Failure;return Last=new(){BlockWrites=BlockWrites};}
    }
    private sealed class Connection : ITerminalConnection
    {
        public Channel<byte[]> Reads {get;}=Channel.CreateUnbounded<byte[]>();public Channel<byte[]> Written {get;}=Channel.CreateUnbounded<byte[]>();public bool Disposed,BlockWrites;public int ReadCount;public (int,int) Size;
        public async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token) {if(!await Reads.Reader.WaitToReadAsync(token))return 0;byte[] bytes=await Reads.Reader.ReadAsync(token);Interlocked.Increment(ref ReadCount);bytes.CopyTo(buffer);return bytes.Length;}
        public async ValueTask WriteAsync(ReadOnlyMemory<byte> data,CancellationToken token) {if(BlockWrites)await Task.Delay(Timeout.Infinite,token);await Written.Writer.WriteAsync(data.ToArray(),token);}
        public ValueTask ResizeAsync(int columns,int rows,CancellationToken token){Size=(columns,rows);return ValueTask.CompletedTask;}
        public ValueTask DisposeAsync(){Disposed=true;Reads.Writer.TryComplete();Written.Writer.TryComplete();return ValueTask.CompletedTask;}
    }
}
