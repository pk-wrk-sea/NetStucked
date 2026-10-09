using NetStucked.Core;
using Xunit;

namespace NetStucked.Tests;

public class PingServiceTests
{
    [Fact]
    public async Task Expanded254HostsObeyGlobalLimitAndOneRequestPerHost()
    {
        var fake = new FakeProbe();
        await using var service = new MultiTargetPingService(fake, new FakeDns());
        var hosts = TargetInputParser.Parse("192.0.2.0/24 test adapters only").Targets;
        await service.StartAsync(hosts, new() { Concurrency = 8, IntervalMs = 250 });
        await Eventually.Until(() => service.Snapshot().All(row => row.Sent > 0));
        await service.PauseAsync();
        var snapshot = service.Snapshot();
        Assert.Equal(254, snapshot.Count); Assert.InRange(fake.Maximum, 1, 8); Assert.Equal(0, fake.Overlap); Assert.Equal(0, fake.Active);
        Assert.Equal(SessionState.Paused, service.State);
        long sent = snapshot.Sum(row => row.Sent); await Task.Delay(350); Assert.Equal(sent, service.Snapshot().Sum(row => row.Sent));
        await service.ResumeAsync(); await Eventually.Until(() => service.Snapshot().Sum(row => row.Sent) > sent);
        await service.StopAsync(); Assert.Equal(0, fake.Active); Assert.Equal(SessionState.Stopped, service.State);
        Assert.All(service.Snapshot(), row => Assert.Equal(row.Sent, row.Received + row.Lost));
    }
    [Fact]
    public async Task CancelledRequestsDoNotBecomeSentOrLostAndRapidRestartDoesNotOverlap()
    {
        var fake = new FakeProbe { Handler = async (_, _, token) => { await Task.Delay(Timeout.Infinite, token); throw new InvalidOperationException(); } };
        await using var service = new MultiTargetPingService(fake, new FakeDns());
        for (int run = 0; run < 5; run++)
        {
            await service.StartAsync([new("127.0.0.1", "")], new());
            await Eventually.Until(() => fake.Active == 1);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync([new("127.0.0.1", "")], new()));
            await service.PauseAsync(); Assert.Equal(0, fake.Active); Assert.Equal(0, service.Snapshot()[0].Sent);
            await service.ResumeAsync(); await Eventually.Until(() => fake.Active == 1);
            await service.StopAsync(); Assert.Equal(0, fake.Active); Assert.Equal(0, service.Snapshot()[0].Lost);
        }
        Assert.Equal(0, fake.Overlap);
    }
    [Fact]
    public async Task DnsFailureIsUnknownWithoutIcmpAttempt()
    {
        var fake = new FakeProbe();
        await using var service = new MultiTargetPingService(fake, new FakeDns(true));
        await service.StartAsync([new("missing.example.invalid", "")], new());
        await Eventually.Until(() => service.Snapshot()[0].Error is not null);
        await service.StopAsync();
        var row = service.Snapshot()[0]; Assert.Equal("Unknown", row.Status); Assert.Equal(0, row.Sent); Assert.Equal(0, fake.Maximum);
        Assert.StartsWith("DNS:", row.Error);
    }
    [Fact]
    public async Task AdapterExceptionIsContainedAndRecordedAsCompletedFailure()
    {
        var fake = new FakeProbe { Handler = (_, _, _) => throw new InvalidOperationException("adapter error") };
        await using var service = new MultiTargetPingService(fake, new FakeDns());
        await service.StartAsync([new("127.0.0.1", "")], new() { FailureThreshold = 1 });
        await Eventually.Until(() => service.Snapshot()[0].Sent > 0); await service.StopAsync();
        var row = service.Snapshot()[0]; Assert.Equal("Unreachable", row.Status); Assert.Equal(1, row.Lost); Assert.Equal("adapter error", row.Error); Assert.Null(row.Last);
    }
}
