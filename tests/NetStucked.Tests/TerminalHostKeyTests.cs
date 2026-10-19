using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;
public sealed class TerminalHostKeyTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "netstucked-host-keys-" + Guid.NewGuid());
    private TerminalKnownHostStore Store => new(new(_path));
    private static string Pin(char value = 'A') => "SHA256:" + new string(value, 43);
    private static TerminalKnownHost Key => new("fixture.test", 22, "ssh-rsa", Pin(), DateTimeOffset.UtcNow);
    public void Dispose() { if (Directory.Exists(_path)) Directory.Delete(_path, true); }
    [Fact] public async Task UnknownRejectDoesNotSave() {var prompt=new Prompt();Assert.False(await new TerminalHostTrust(Store,prompt).VerifyAsync(Key.Host,22,Key.Algorithm,Key.Fingerprint,default));Assert.Single(prompt.Seen);Assert.Null(prompt.Seen[0].Previous);Assert.Empty(await Store.LoadAsync(default));}
    [Fact] public async Task AcceptOnceIsOnlyForThisHandshake()
    {
        var prompt=new Prompt{Decision=TerminalHostKeyDecision.AcceptOnce};var trust=new TerminalHostTrust(Store,prompt);
        Assert.True(await trust.VerifyAsync(Key.Host,22,Key.Algorithm,Key.Fingerprint,default));Assert.True(await trust.VerifyAsync(Key.Host,22,Key.Algorithm,Key.Fingerprint,default));Assert.Equal(2,prompt.Seen.Count);Assert.Empty(await Store.LoadAsync(default));
    }
    [Fact] public async Task SaveRecognizesHostAcrossPolicyInstancesAndCase()
    {
        var prompt=new Prompt{Decision=TerminalHostKeyDecision.AcceptAndSave};Assert.True(await new TerminalHostTrust(Store,prompt).VerifyAsync("FIXTURE.test.",22,Key.Algorithm,Key.Fingerprint,default));
        var reject=new Prompt();Assert.True(await new TerminalHostTrust(Store,reject).VerifyAsync(Key.Host,22,Key.Algorithm,Key.Fingerprint,default));Assert.Empty(reject.Seen);Assert.Single(await Store.LoadAsync(default));
    }
    [Fact] public async Task ChangedKeyRejectPreservesPreviousAndSaveReplacesOnlyAfterApproval()
    {
        await Store.SaveAsync(Key,null,default);var prompt=new Prompt();var trust=new TerminalHostTrust(Store,prompt);
        Assert.False(await trust.VerifyAsync(Key.Host,22,Key.Algorithm,Pin('B'),default));Assert.True(prompt.Seen[0].Changed);Assert.Equal(Key.Fingerprint,prompt.Seen[0].Previous!.Fingerprint);Assert.Equal(Key.Fingerprint,Assert.Single(await Store.LoadAsync(default)).Fingerprint);
        prompt.Decision=TerminalHostKeyDecision.AcceptAndSave;Assert.True(await trust.VerifyAsync(Key.Host,22,Key.Algorithm,Pin('B'),default));Assert.Equal(Pin('B'),Assert.Single(await Store.LoadAsync(default)).Fingerprint);
    }
    [Fact] public async Task ChangedKeyAcceptOnceDoesNotReplaceSavedKey()
    {
        await Store.SaveAsync(Key,null,default);var prompt=new Prompt{Decision=TerminalHostKeyDecision.AcceptOnce};Assert.True(await new TerminalHostTrust(Store,prompt).VerifyAsync(Key.Host,22,Key.Algorithm,Pin('B'),default));Assert.Equal(Key.Fingerprint,Assert.Single(await Store.LoadAsync(default)).Fingerprint);
    }
    [Fact] public async Task DifferentPortsAreDifferentHostsAndIpv6Normalizes()
    {
        var prompt=new Prompt{Decision=TerminalHostKeyDecision.AcceptAndSave};var trust=new TerminalHostTrust(Store,prompt);Assert.True(await trust.VerifyAsync("[::1]",22,Key.Algorithm,Key.Fingerprint,default));Assert.True(await trust.VerifyAsync("0:0:0:0:0:0:0:1",22,Key.Algorithm,Key.Fingerprint,default));Assert.Single(prompt.Seen);Assert.True(await trust.VerifyAsync("::1",2222,Key.Algorithm,Key.Fingerprint,default));Assert.Equal(2,prompt.Seen.Count);Assert.Equal(2,(await Store.LoadAsync(default)).Count);
    }
    [Fact] public async Task ForgetRequiresNewApprovalAndCannotDeleteNewerReplacement()
    {
        var first=Key;Assert.True(await Store.SaveAsync(first,null,default));Assert.True(await Store.SaveAsync(first with{Fingerprint=Pin('B')},first.Fingerprint,default));Assert.False(await Store.RemoveAsync(first,default));var latest=Assert.Single(await Store.LoadAsync(default));Assert.True(await Store.RemoveAsync(latest,default));var prompt=new Prompt();Assert.False(await new TerminalHostTrust(Store,prompt).VerifyAsync(first.Host,22,first.Algorithm,Pin('B'),default));Assert.Null(Assert.Single(prompt.Seen).Previous);
    }
    [Fact] public async Task CancelWhilePromptOpenDoesNotSave()
    {
        var prompt=new Prompt{Wait=true,Decision=TerminalHostKeyDecision.AcceptAndSave};using var cancel=new CancellationTokenSource();Task<bool> verify=new TerminalHostTrust(Store,prompt).VerifyAsync(Key.Host,22,Key.Algorithm,Key.Fingerprint,cancel.Token);await prompt.Opened.Task.WaitAsync(TimeSpan.FromSeconds(2));cancel.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>verify);Assert.Empty(await Store.LoadAsync(default));
    }
    [Theory] [InlineData("{broken")] [InlineData("{\"Schema\":1,\"Hosts\":null}")] [InlineData("{\"Schema\":1,\"Hosts\":[null]}")] [InlineData("{\"Schema\":2,\"Hosts\":[]}")]
    public async Task CorruptDatabaseFailsClosedWithoutPromptOrOverwrite(string text)
    {
        Directory.CreateDirectory(_path);string path=Path.Combine(_path,"terminal-known-hosts.json");await File.WriteAllTextAsync(path,text);var prompt=new Prompt{Decision=TerminalHostKeyDecision.AcceptAndSave};await Assert.ThrowsAsync<CollectorException>(()=>new TerminalHostTrust(Store,prompt).VerifyAsync(Key.Host,22,Key.Algorithm,Key.Fingerprint,default));Assert.Empty(prompt.Seen);Assert.Equal(text,await File.ReadAllTextAsync(path));
    }
    [Fact] public async Task ConcurrentSameHostApprovalIsSerializedAndSavedOnlyOnce()
    {
        var prompt=new Prompt{Decision=TerminalHostKeyDecision.AcceptAndSave,Delay=true};var trust=new TerminalHostTrust(Store,prompt);bool[] result=await Task.WhenAll(Enumerable.Range(0,5).Select(_=>trust.VerifyAsync(Key.Host,22,Key.Algorithm,Key.Fingerprint,default)));Assert.All(result,Assert.True);Assert.Single(prompt.Seen);Assert.Single(await Store.LoadAsync(default));
    }
    [Fact] public async Task CompareAndSaveRejectsStaleApproval()
    {
        var first=Key;Assert.True(await Store.SaveAsync(first,null,default));Assert.False(await Store.SaveAsync(first with{Fingerprint=Pin('B')},null,default));Assert.Equal(first.Fingerprint,Assert.Single(await Store.LoadAsync(default)).Fingerprint);
    }
    [Fact] public async Task DatabaseContainsOnlyNonsecretHostIdentity()
    {
        await Store.SaveAsync(Key,null,default);string json=await File.ReadAllTextAsync(Path.Combine(_path,"terminal-known-hosts.json"));Assert.DoesNotContain("Password",json);Assert.DoesNotContain("Passphrase",json);Assert.DoesNotContain("Username",json);Assert.Contains(Key.Fingerprint,json);Assert.Equal(Key.Host,Assert.Single(await Store.LoadAsync(default)).Host);
    }
    [Theory] [InlineData("fixture.test spoofed",22)] [InlineData("fixture.test",0)] [InlineData("::1",65536)]
    public async Task InvalidEndpointCannotEnterTrustDatabase(string host,int port)
    { await Assert.ThrowsAsync<InvalidDataException>(()=>Store.SaveAsync(Key with{Host=host,Port=port},null,default));Assert.Empty(await Store.LoadAsync(default)); }
    private sealed class Prompt : ITerminalHostKeyPrompt
    {
        public List<TerminalHostKeyChallenge> Seen {get;}=[];public TerminalHostKeyDecision Decision;public bool Wait,Delay;public TaskCompletionSource Opened {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<TerminalHostKeyDecision> ConfirmAsync(TerminalHostKeyChallenge challenge,CancellationToken token){Seen.Add(challenge);Opened.TrySetResult();if(Wait)await Task.Delay(Timeout.Infinite,token);if(Delay)await Task.Delay(30,token);return Decision;}
    }
}
