using System.Net;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public class PlatformTests
{
    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task ActualLoopbackIcmpEchoAndTtlTraceReturnApiResults(string loopback)
    {
        var probe = new IcmpPingProbe();
        var address = IPAddress.Parse(loopback);
        var result = await probe.SendAsync(address, 1000, 32, 128, CancellationToken.None);
        Assert.True(result.IsEchoReply, result.Error ?? result.Status.ToString()); Assert.Equal(address, result.Address); Assert.NotNull(result.RttMs);
        await using var trace = new TracerouteMonitoringService(probe, new DnsResolver());
        await trace.StartAsync("127.0.0.1", new() { Continuous = false });
        await Eventually.Until(() => trace.State is SessionState.Stopped or SessionState.Error);
        Assert.Equal("Destination replied", trace.Outcome); Assert.Equal(1, Assert.Single(trace.Snapshot()).Hop);
    }
    [Fact]
    public async Task CancelledAdapterDoesNotSendAndSettingsRoundTripUtf8()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new IcmpPingProbe().SendAsync(IPAddress.Loopback, 1000, 32, 128, cancellation.Token));
        string directory = Path.Combine(Path.GetTempPath(), "NetStucked-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new UserSettingsStore(directory); store.Preferences.TargetText = "127.0.0.1 ไทย";
            store.Preferences.Columns["Ping"] = [new("Host", 2, 135, true)]; await store.SaveAsync();
            var reopened = new UserSettingsStore(directory); Assert.Equal(store.Preferences.TargetText, reopened.Preferences.TargetText);
            Assert.Equal(135, reopened.Preferences.Columns["Ping"][0].Width); Assert.Null(reopened.LoadError);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact]
    public void InvalidSettingsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => (new ProbeSettings { Concurrency = 0 }).Validate());
        Assert.Throws<ArgumentException>(() => (new ProbeSettings { WarningRttMs = double.NaN }).Validate());
        Assert.Throws<ArgumentException>(() => (new TraceSettings { MaxHops = 65 }).Validate());
    }
}
