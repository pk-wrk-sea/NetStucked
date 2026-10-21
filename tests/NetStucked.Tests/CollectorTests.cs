using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public sealed class CollectorTests
{
    private static CollectorRun Run(CollectorCredentials? second = null, int? slot = null, int count = 1) => new(
        new(CollectorProtocol.Ssh, "Cisco IOS / IOS XE", Path.GetTempPath()),
        Enumerable.Range(1, count).Select(i => new CollectorTarget(i, "device" + i + ".test", 22, "owned fixture", slot)),
        ["terminal length 0"], ["show running-config", "show version"], new Dictionary<string, string>(), new(@"DOMAIN\fixture", "fixture-password-one", ""), second);
    [Fact] public void ParserPreservesDescriptionsIpv6OverridesAndFirstDuplicate()
    {
        var rows = CollectorInputParser.Parse("# comment\n\nswitch.test:2222 Core auth=2\nSWITCH.test:2222 duplicate\n[::1]:22 Lab auth=1\n10.0.0.1 Access", 22);
        Assert.Equal(3, rows.Count); Assert.Equal("Core", rows[0].Description); Assert.Equal(2, rows[0].AuthSlot); Assert.Equal(2222, rows[0].Port);
        Assert.Equal("[::1]:22", rows[1].Endpoint); Assert.Equal(1, rows[1].AuthSlot); Assert.Equal(22, rows[2].Port);
    }
    [Theory] [InlineData("a:0")] [InlineData("a:65536")] [InlineData("a:not-port")] [InlineData("[::1")] [InlineData("http://a")] [InlineData("a auth=3")] [InlineData("a auth=1 auth=2")] [InlineData("10.0.0.0/24")] [InlineData("# only")]
    public void InvalidInputNeverPlans(string text) => Assert.Throws<ArgumentException>(() => CollectorInputParser.Parse(text, 22));
    [Fact] public void ImmutableSnapshotAndSecretSerialization()
    {
        var list = new List<string> { "show version" }; var target = new List<CollectorTarget> { new(1, "a", 22, "", null) };
        var pins = new Dictionary<string, string>(); var secret = new CollectorCredentials(@"DOMAIN\user", "never-export-password", "never-export-enable");
        var run = new CollectorRun(new(CollectorProtocol.Ssh, "Cisco IOS / IOS XE", Path.GetTempPath()), target, [], list, pins, secret, null);
        list.Clear(); target.Clear(); pins["a:22"] = "changed";
        Assert.Single(run.Targets); Assert.Single(run.CollectionCommands); Assert.Empty(run.HostPins);
        string json = JsonSerializer.Serialize(run); Assert.DoesNotContain("never-export", json); Assert.DoesNotContain("never-export", run.ToString()); Assert.DoesNotContain("never-export", secret.ToString());
        Assert.Equal(@"DOMAIN\user", secret.Username);
    }
    [Theory] [InlineData("configure terminal")] [InlineData("show version; reload")] [InlineData("show version | redirect flash:test")] [InlineData("show tech-support")] [InlineData("show version\rreload")]
    public void RejectsMutationAndInteractiveCommands(string command) => Assert.Throws<ArgumentException>(() => CollectorCommands.Validate("Cisco IOS / IOS XE", [], [command]));
    [Fact] public async Task FirstAuthSuccessNeverUsesSecondAndStreamsSequentially()
    {
        var transport = new FakeTransport(); var capture = new FakeCaptureFactory(); var run = Run(new("fallback", "second-password", ""));
        var results = await new CollectorController(transport, capture).RunAsync(run, _ => { }, default);
        Assert.Equal(CollectorDeviceState.Completed, results.Single().State); Assert.Single(transport.Users); Assert.Equal(@"DOMAIN\fixture", transport.Users[0]);
        Assert.Equal(new[] { "terminal length 0", "show running-config", "show version" }, transport.Connections.Single().Sent);
        Assert.True(transport.Connections.Single().Disposed); Assert.DoesNotContain('\uFEFF', string.Join("", transport.Connections.Single().Sent));
    }
    [Fact] public async Task ExplicitRejectionFallsBackOnce()
    {
        var transport = new FakeTransport { Error = (_, attempt) => attempt == 1 ? CollectorFailure.AuthenticationRejected : null };
        var result = (await new CollectorController(transport, new FakeCaptureFactory()).RunAsync(Run(new("fallback", "second-password", "")), _ => { }, default)).Single();
        Assert.Equal(CollectorDeviceState.Completed, result.State); Assert.Equal(2, result.AuthSlot); Assert.Equal(2, transport.Users.Count);
    }
    [Theory] [InlineData(CollectorFailure.HostKey)] [InlineData(CollectorFailure.Timeout)] [InlineData(CollectorFailure.Transport)] [InlineData(CollectorFailure.UnsupportedAuthentication)] [InlineData(CollectorFailure.Command)]
    public async Task NonAuthenticationFailureNeverChangesCredentials(CollectorFailure failure)
    {
        var transport = new FakeTransport { Error = (_, _) => failure };
        var result = (await new CollectorController(transport, new FakeCaptureFactory()).RunAsync(Run(new("fallback", "second-password", "")), _ => { }, default)).Single();
        Assert.Equal(CollectorDeviceState.Failed, result.State); Assert.Single(transport.Users);
    }
    [Theory] [InlineData(1)] [InlineData(2)] public async Task AuthOverrideDoesNotFallBack(int slot)
    {
        var transport = new FakeTransport { Error = (_, _) => CollectorFailure.AuthenticationRejected };
        var result = (await new CollectorController(transport, new FakeCaptureFactory()).RunAsync(Run(new("second", "second-password", ""), slot), _ => { }, default)).Single();
        Assert.Equal(CollectorDeviceState.Failed, result.State); Assert.Single(transport.Users); Assert.Equal(slot == 1 ? @"DOMAIN\fixture" : "second", transport.Users[0]);
    }
    [Fact] public async Task TransientConnectRetriesBoundedWithSameProfile()
    {
        var transport = new FakeTransport { Error = (_, _) => CollectorFailure.TransientTransport };
        await new CollectorController(transport, new FakeCaptureFactory()).RunAsync(Run(new("fallback", "second-password", "")), _ => { }, default);
        Assert.Equal(2, transport.Users.Count); Assert.All(transport.Users, u => Assert.Equal(@"DOMAIN\fixture", u));
    }
    [Theory] [InlineData("\n% Invalid input detected\nfixture#", "command")] [InlineData("\nContinue? [confirm]", "confirmation")] [InlineData("\nOTP verification code:", "MFA")]
    public async Task CommandFailuresPreservePartialAndNeverReplay(string response, string label)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var transport = new FakeTransport { Response = _ => response };
        var result = (await new CollectorController(transport, new FakeCaptureFactory()).RunAsync(Run(new("fallback", "second-password", "")), _ => { }, default)).Single();
        Assert.Equal(CollectorDeviceState.Partial, result.State); Assert.Single(transport.Users); Assert.Single(transport.Connections.Single().Sent);
        Assert.Contains(label, result.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.True(transport.Connections.Single().Disposed);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), "A waiting live control prompt must fail without waiting for the command timeout.");
    }
    [Fact] public async Task SplitNoNewlinePromptIgnoresHashAndGreaterThanInsideOutput()
    {
        var transport = new FakeTransport { SplitChunks = true, Response = command => command + "\r\nvalue # inside > output\r\nfixture#" };
        var result = (await new CollectorController(transport, new FakeCaptureFactory()).RunAsync(Run(), _ => { }, default)).Single();
        Assert.Equal(CollectorDeviceState.Completed, result.State); Assert.Equal(3, transport.Connections.Single().Sent.Count);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ConfigurationBannerWordsAreDataAndDoNotAbortCollection(bool split)
    {
        // These strings are common configured login banners. They are not live
        // authentication/confirmation prompts in show running-config output.
        var transport = new FakeTransport
        {
            SplitChunks = split,
            Response = command => command + "\r\nbanner motd ^\r\nMFA / OTP is required for access.\r\nOTP verification code:\r\nAccess denied\r\nAccess denied to unauthorized users.\r\nPress enter to continue [confirm]\r\n^\r\nhostname fixture\r\nfixture#"
        };
        var result = (await new CollectorController(transport, new FakeCaptureFactory()).RunAsync(Run(new("fallback", "second-password", "")), _ => { }, default)).Single();
        Assert.Equal(CollectorDeviceState.Completed, result.State);
        Assert.Single(transport.Users);
        Assert.Equal(3, transport.Connections.Single().Sent.Count);
        Assert.Contains("MFA / OTP is required", result.Preview);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task AuthenticatedShellGreetingMayContainBannerWarnings(bool split)
    {
        var transport = new FakeTransport { SplitChunks = split, InitialPrompt = "MFA / OTP required for access.\r\nAccess denied to unauthorized users.\r\nOTP verification code:\r\nPress enter to continue [confirm]\r\nfixture#" };
        var result = (await new CollectorController(transport, new FakeCaptureFactory()).RunAsync(Run(), _ => { }, default)).Single();
        Assert.Equal(CollectorDeviceState.Completed, result.State);
        Assert.Single(transport.Users); Assert.Equal(3, transport.Connections.Single().Sent.Count);
    }
    [Theory]
    [InlineData("OTP verification code:", "MFA")]
    [InlineData("Access denied", "rejected")]
    [InlineData("Continue? [confirm]", "confirmation")]
    public async Task WaitingShellControlPromptStopsBeforeCommandsOrCredentialFallback(string prompt, string label)
    {
        var transport = new FakeTransport { InitialPrompt = prompt };
        var result = (await new CollectorController(transport, new FakeCaptureFactory()).RunAsync(Run(new("fallback", "second-password", "")), _ => { }, default)).Single();
        Assert.Equal(CollectorDeviceState.Failed, result.State); Assert.Contains(label, result.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Single(transport.Users); Assert.Empty(transport.Connections.Single().Sent); Assert.True(transport.Connections.Single().Disposed);
    }
    [Fact] public async Task CancelDuringControlPromptWaitAwaitsReadCleanupBeforeDisposal()
    {
        var transport = new DelayedReadTransport(); using var cancel = new CancellationTokenSource();
        var running = new CollectorController(transport, new FakeCaptureFactory()).RunAsync(Run(), _ => { }, cancel.Token);
        await transport.Connection.PendingReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancel.Cancel();
        var result = await running.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.All(result, row => Assert.Equal(CollectorDeviceState.Cancelled, row.State));
        Assert.True(transport.Connection.Disposed); Assert.False(transport.Connection.DisposedWhileReading);
        Assert.Equal(0, transport.Connection.ActiveReads); Assert.Equal(1, transport.Connection.Writes);
    }
    [Fact] public async Task CancelCommandAndQueuedWorkDrainsAllConnections()
    {
        var transport = new FakeTransport { Response = _ => null }; using var cancel = new CancellationTokenSource();
        var running = new CollectorController(transport, new FakeCaptureFactory()).RunAsync(Run(count: 8), _ => { }, cancel.Token);
        await Task.Delay(100); cancel.Cancel(); var results = await running.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.All(results, r => Assert.Equal(CollectorDeviceState.Cancelled, r.State)); Assert.InRange(transport.Users.Count, 1, 2); Assert.All(transport.Connections, c => Assert.True(c.Disposed));
    }
    [Fact] public async Task CaptureCreationFailureNeverConnects()
    {
        var transport = new FakeTransport();
        var results = await new CollectorController(transport, new FakeCaptureFactory { FailCreate = true }).RunAsync(Run(), _ => { }, default);
        Assert.Equal(CollectorDeviceState.Failed, results.Single().State); Assert.Empty(transport.Users);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task EnableStateMachineWaitsForPasswordAndRejectsWithoutAuthFallback(bool reject)
    {
        var original = Run(new("fallback", "second-password", ""));
        var run = new CollectorRun(original.Options, original.Targets, original.BasicCommands, original.CollectionCommands,
            new Dictionary<string, string>(), new(@"DOMAIN\fixture", "fixture-password-one", "owned-enable-secret"), original.Auth2);
        var transport = new FakeTransport
        {
            InitialPrompt = "fixture>", SplitChunks = true,
            Response = command => command == "enable" ? "\r\nPassword:" : command == "owned-enable-secret" ? reject ? "\r\n% Bad passwords\r\nfixture>" : "\r\nfixture#" : command + "\r\nactual fixture\r\nfixture#"
        };
        var result = (await new CollectorController(transport, new FakeCaptureFactory()).RunAsync(run, _ => { }, default)).Single();
        Assert.Equal(reject ? CollectorDeviceState.Failed : CollectorDeviceState.Completed, result.State);
        Assert.Single(transport.Users); Assert.Equal("enable", transport.Connections[0].Sent[0]); Assert.Equal("owned-enable-secret", transport.Connections[0].Sent[1]);
        Assert.DoesNotContain("owned-enable-secret", result.Preview);
    }
    [Fact] public async Task DisconnectAfterSendNeverReconnectsOrReplays()
    {
        var transport = new FakeTransport { Response = _ => throw new CollectorException(CollectorFailure.Transport, "owned disconnect") };
        var result = (await new CollectorController(transport, new FakeCaptureFactory()).RunAsync(Run(new("fallback", "second-password", "")), _ => { }, default)).Single();
        Assert.Equal(CollectorDeviceState.Partial, result.State); Assert.Single(transport.Users); Assert.Single(transport.Connections[0].Sent); Assert.True(transport.Connections[0].Disposed);
    }
    [Fact] public async Task CancellingDuringLoginDisposesAndNeverAdmitsQueuedDevices()
    {
        var transport = new WaitingTransport(); using var cancel = new CancellationTokenSource(100);
        var result = await new CollectorController(transport, new FakeCaptureFactory()).RunAsync(Run(count: 12), _ => { }, cancel.Token);
        Assert.All(result, r => Assert.Equal(CollectorDeviceState.Cancelled, r.State)); Assert.Equal(0, transport.Active); Assert.InRange(transport.Maximum, 1, 2);
    }
    [Fact] public async Task CancelDuringSavingStillFinalizesAlreadyCompletedEvidence()
    {
        using var cancel = new CancellationTokenSource(); var transport = new FakeTransport();
        var result = (await new CollectorController(transport, new FakeCaptureFactory()).RunAsync(Run(), r => { if (r.State == CollectorDeviceState.Saving) cancel.Cancel(); }, cancel.Token)).Single();
        Assert.Equal(CollectorDeviceState.Completed, result.State); Assert.True(transport.Connections[0].Disposed);
    }
    [Fact] public async Task ProtectedRunFolderHasNoInheritedAclAndDoesNotOverwriteEvidence()
    {
        if (!OperatingSystem.IsWindows()) return;
        string output = Path.Combine(Path.GetTempPath(), "NetStucked-owned-acl-" + Guid.NewGuid().ToString("N"));
        try
        {
            var original = Run(); var run = new CollectorRun(original.Options with { OutputFolder = output }, original.Targets, original.BasicCommands, original.CollectionCommands,
                new Dictionary<string, string>(), original.Auth1, null);
            var factory = new CollectorCaptureFactory(); var files = new List<string>();
            for (int i = 0; i < 2; i++)
            {
                await using var capture = await factory.CreateAsync(run, run.Targets[0], default); await capture.AppendAsync("owned fixture", default);
                var result = await capture.CompleteAsync(new(run.Id, run.Targets[0], CollectorDeviceState.Completed, "owned"), [], default); files.Add(result.TxtPath);
            }
            Assert.NotEqual(files[0], files[1]); Assert.All(files, file => Assert.True(File.Exists(file)));
            var acl = System.IO.FileSystemAclExtensions.GetAccessControl(new DirectoryInfo(Path.GetDirectoryName(files[0])!));
            Assert.True(acl.AreAccessRulesProtected);
            var allowed = acl.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
            Assert.Equal(2, allowed.Count);
        }
        finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }
    [Fact] public void CredentialRedactionSurvivesEveryChunkBoundary()
    {
        const string secret = "split-fixture-secret"; string text = "begin " + secret + " end " + secret;
        for (int split = 0; split <= text.Length; split++)
        {
            var redactor = new CollectorRedactor([secret]);
            string result = redactor.Push(text[..split]) + redactor.Push(text[split..]) + redactor.Push("", true);
            Assert.Equal("begin [REDACTED] end [REDACTED]", result);
        }
    }
    [Fact] public async Task LargeCaptureFullBytesBoundedPreviewUniqueNamesAndJson()
    {
        string folder = Path.Combine(Path.GetTempPath(), "NetStucked-owned-capture-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            var options = Run().Options with { OutputFolder = folder };
            await using var capture = new CollectorCapture(Path.Combine(folder, "owned"), options, ["secret-fixture"]);
            string chunk = new('x', 4096); for (int i = 0; i < 512; i++) await capture.AppendAsync(chunk, default);
            await capture.AppendAsync("secret-", default); await capture.AppendAsync("fixture", default);
            string unicode = "ไทย" + string.Concat(Enumerable.Repeat("🚀", 4096)); await capture.AppendAsync(unicode, default);
            var result = await capture.CompleteAsync(new(Guid.NewGuid(), new(1, "owned.test", 22, "", null), CollectorDeviceState.Completed, "fixture"), [], default);
            Assert.Equal(512 * 4096 + "[REDACTED]".Length + Encoding.UTF8.GetByteCount(unicode), result.Bytes); Assert.InRange(result.Preview.Length, 1, 8300);
            Assert.Equal(result.Bytes, new FileInfo(result.TxtPath).Length);
            string content = await File.ReadAllTextAsync(result.TxtPath); Assert.DoesNotContain("secret-fixture", content); Assert.EndsWith("[REDACTED]" + unicode, content);
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(result.JsonPath));
            Assert.Equal(1, json.RootElement.GetProperty("schema").GetInt32());
            Assert.Equal(content, string.Concat(json.RootElement.GetProperty("outputChunks").EnumerateArray().Select(e => e.GetString())));
            Assert.Equal(64, result.Sha256.Length);
            await using var collision = new CollectorCapture(Path.Combine(folder, "collision"), options, []);
            Assert.Throws<IOException>(() => new CollectorCapture(Path.Combine(folder, "collision"), options, []));
        }
        finally { Directory.Delete(folder, true); }
    }
    [Fact] public async Task CaptureLimitAndNoTxtFormatAreTruthful()
    {
        string folder = Path.Combine(Path.GetTempPath(), "NetStucked-owned-format-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            await using var capture = new CollectorCapture(Path.Combine(folder, "owned"), Run().Options with { WriteTxt = false, MaxCaptureBytes = 1024 }, []);
            await capture.AppendAsync(new string('z', 1000), default);
            await Assert.ThrowsAsync<CollectorException>(() => capture.AppendAsync(new string('z', 100), default));
            var result = await capture.CompleteAsync(new(Guid.NewGuid(), new(1, "owned.test", 22, "", null), CollectorDeviceState.Partial, "limit"), [], default);
            Assert.Empty(result.TxtPath); Assert.True(File.Exists(result.JsonPath)); Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
        }
        finally { Directory.Delete(folder, true); }
    }
    [Fact] public async Task RealOwnedTelnetNegotiatesAndPreservesLiteralUsernameAndUtf8NoBom()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(timeout.Token); var stream = peer.GetStream();
            await stream.WriteAsync(new byte[] { 255, 253 }, timeout.Token); await Task.Delay(10, timeout.Token);
            await stream.WriteAsync(new byte[] { 1 }, timeout.Token); await stream.WriteAsync(Encoding.UTF8.GetBytes("Username:"), timeout.Token);
            var bytes = new List<byte>(); byte[] b = new byte[1];
            while (bytes.Count < 3 || bytes[^1] != 10) { Assert.Equal(1, await stream.ReadAsync(b, timeout.Token)); bytes.Add(b[0]); }
            Assert.Equal(new byte[] { 255, 252, 1 }, bytes.Take(3)); Assert.Equal("DOMAIN\\fixture\r\n", Encoding.UTF8.GetString(bytes.Skip(3).ToArray()));
            await stream.WriteAsync(Encoding.UTF8.GetBytes("Password:"), timeout.Token); bytes.Clear();
            do { Assert.Equal(1, await stream.ReadAsync(b, timeout.Token)); bytes.Add(b[0]); } while (bytes[^1] != 10);
            Assert.Equal("fixture-password-one\r\n", Encoding.UTF8.GetString(bytes.ToArray())); Assert.DoesNotContain((byte)0xEF, bytes);
            await stream.WriteAsync(Encoding.UTF8.GetBytes("\r\nfixture#"), timeout.Token);
        }, timeout.Token);
        await using var connection = await new CollectorTransport().ConnectAsync(new(1, "127.0.0.1", port, "owned loopback", null), Run().Options with { Protocol = CollectorProtocol.Telnet, AllowTelnet = true }, Run().Auth1, null, timeout.Token);
        byte[] prompt = new byte[128]; int length = await connection.ReadAsync(prompt, timeout.Token); Assert.Equal("fixture#", Encoding.UTF8.GetString(prompt, 0, length)); await server;
    }
    [Fact] public void EquivalentIpv6EndpointsDeduplicate()
        => Assert.Single(CollectorInputParser.Parse("[::1]:22 AUTH=1\n[0:0:0:0:0:0:0:1]:22", 22));
    [Fact] public async Task JsonStorageFailureRetainsFinalizedTxtAndRemovesTemporaryFiles()
    {
        string folder = Path.Combine(Path.GetTempPath(), "NetStucked-owned-save-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "owned"); Directory.CreateDirectory(path + ".json");
            CollectorResult result;
            await using (var capture = new CollectorCapture(path, Run().Options, []))
            {
                await capture.AppendAsync("owned fixture", default);
                result = await capture.CompleteAsync(new(Guid.NewGuid(), new(1, "owned.test", 22, "", null), CollectorDeviceState.Completed, "owned"), [], default);
            }
            Assert.Equal(CollectorDeviceState.Partial, result.State); Assert.True(File.Exists(result.TxtPath)); Assert.Empty(result.JsonPath); Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
        }
        finally { Directory.Delete(folder, true); }
    }
    private sealed class FakeTransport : ICollectorTransport
    {
        public List<string> Users { get; } = [];
        public List<FakeConnection> Connections { get; } = [];
        public Func<CollectorTarget, int, CollectorFailure?>? Error { get; init; }
        public Func<string, string?>? Response { get; init; }
        public bool SplitChunks { get; init; }
        public string InitialPrompt { get; init; } = "fixture#";
        public Task<ICollectorConnection> ConnectAsync(CollectorTarget target, CollectorOptions options, CollectorCredentials credentials, string? pin, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); lock (Users)
            {
                Users.Add(credentials.Username);
                if (Error?.Invoke(target, Users.Count) is { } failure) throw new CollectorException(failure, "owned fixture failure");
                var connection = new FakeConnection(Response, SplitChunks, InitialPrompt); Connections.Add(connection); return Task.FromResult<ICollectorConnection>(connection);
            }
        }
    }
    private sealed class FakeConnection : ICollectorConnection
    {
        private readonly Channel<byte[]> _input = Channel.CreateBounded<byte[]>(4096);
        private readonly Func<string, string?>? _response; private readonly bool _split; private byte[] _remaining = [];
        public List<string> Sent { get; } = [];
        public bool Disposed { get; private set; }
        public FakeConnection(Func<string, string?>? response, bool split, string prompt) { _response = response; _split = split; Push(prompt); }
        private void Push(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            if (_split) { foreach (byte b in bytes) Assert.True(_input.Writer.TryWrite([b])); } else Assert.True(_input.Writer.TryWrite(bytes));
        }
        public async ValueTask<int> ReadAsync(Memory<byte> bytes, CancellationToken token)
        {
            if (_remaining.Length == 0) _remaining = await _input.Reader.ReadAsync(token);
            int length = Math.Min(bytes.Length, _remaining.Length); _remaining.AsMemory(0, length).CopyTo(bytes); _remaining = _remaining[length..]; return length;
        }
        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            Assert.False(bytes.Span.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })); string command = Encoding.UTF8.GetString(bytes.Span).TrimEnd('\r', '\n'); Sent.Add(command);
            string? response = _response is null ? command + "\r\nfixture-data\r\nfixture#" : _response(command);
            if (response is not null) Push(response); return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() { Disposed = true; _input.Writer.TryComplete(); return ValueTask.CompletedTask; }
    }
    private sealed class FakeCaptureFactory : ICollectorCaptureFactory
    {
        public bool FailCreate { get; init; }
        public Task<ICollectorCapture> CreateAsync(CollectorRun run, CollectorTarget target, CancellationToken token) => FailCreate ? throw new IOException("owned storage fixture") : Task.FromResult<ICollectorCapture>(new FakeCapture());
    }
    private sealed class FakeCapture : ICollectorCapture
    {
        public string Preview { get; private set; } = "";
        public Task AppendAsync(string text, CancellationToken token) { token.ThrowIfCancellationRequested(); Preview = (Preview + text); if (Preview.Length > 8192) Preview = Preview[^8192..]; return Task.CompletedTask; }
        public Task<CollectorResult> CompleteAsync(CollectorResult result, IReadOnlyList<CollectorCommandEvidence> commands, CancellationToken token) => Task.FromResult(result with { Preview = Preview });
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class WaitingTransport : ICollectorTransport
    {
        public int Active; public int Maximum;
        public async Task<ICollectorConnection> ConnectAsync(CollectorTarget target, CollectorOptions options, CollectorCredentials credentials, string? hostPin, CancellationToken token)
        {
            int active = Interlocked.Increment(ref Active); Maximum = Math.Max(Maximum, active);
            try { await Task.Delay(Timeout.Infinite, token); throw new InvalidOperationException(); }
            finally { Interlocked.Decrement(ref Active); }
        }
    }
    private sealed class DelayedReadTransport : ICollectorTransport
    {
        public DelayedReadConnection Connection { get; } = new();
        public Task<ICollectorConnection> ConnectAsync(CollectorTarget target, CollectorOptions options, CollectorCredentials credentials, string? pin, CancellationToken token) => Task.FromResult<ICollectorConnection>(Connection);
    }
    private sealed class DelayedReadConnection : ICollectorConnection
    {
        private int _read;
        public int ActiveReads { get; private set; }
        public int Writes { get; private set; }
        public bool Disposed { get; private set; }
        public bool DisposedWhileReading { get; private set; }
        public TaskCompletionSource PendingReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
        {
            if (++_read <= 2)
            {
                byte[] text = Encoding.UTF8.GetBytes(_read == 1 ? "fixture#" : "Continue? [confirm]");
                text.CopyTo(buffer); return text.Length;
            }
            ActiveReads++; PendingReadStarted.SetResult();
            try
            {
                try { await Task.Delay(Timeout.Infinite, token); }
                catch (OperationCanceledException) { await Task.Delay(250); throw; }
                return 0;
            }
            finally { ActiveReads--; }
        }
        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token) { Writes++; return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() { DisposedWhileReading = ActiveReads > 0; Disposed = true; return ValueTask.CompletedTask; }
    }
}
