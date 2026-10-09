using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using NetStucked.Core;

namespace NetStucked.Tests;

internal sealed class FakeDns(bool fail = false) : IDnsResolver
{
    public Task<IPAddress> ResolveAsync(string host, IpFamily family, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (fail) throw new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.HostNotFound);
        return Task.FromResult(IPAddress.TryParse(host, out var ip) ? ip : IPAddress.Loopback);
    }
    public Task<string?> ReverseAsync(IPAddress address, CancellationToken token) => Task.FromResult<string?>(null);
}

internal sealed class BlockingReverseDns : IDnsResolver
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<IPAddress> ResolveAsync(string host, IpFamily family, CancellationToken token) => Task.FromResult(IPAddress.Loopback);
    public async Task<string?> ReverseAsync(IPAddress address, CancellationToken token) { Entered.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return null; }
}

internal sealed class FakeProbe : IIcmpProbe
{
    public Func<IPAddress, int, CancellationToken, Task<ProbeResult>> Handler { get; set; } = async (address, _, token) => { await Task.Delay(5, token); return new(IPStatus.Success, address, 10, 64); };
    private int _active, _maximum, _overlap, _ttlOverlap;
    private readonly ConcurrentDictionary<string, int> _hosts = new();
    private readonly ConcurrentDictionary<string, int> _hops = new();
    public ConcurrentQueue<int> Ttls { get; } = new();
    public int Active => Volatile.Read(ref _active);
    public int Maximum => Volatile.Read(ref _maximum);
    public int Overlap => Volatile.Read(ref _overlap);
    public int TtlOverlap => Volatile.Read(ref _ttlOverlap);
    public async Task<ProbeResult> SendAsync(IPAddress address, int timeoutMs, int packetSize, int ttl, CancellationToken token)
    {
        string key = address.ToString();
        string hopKey = $"{key}:{ttl}";
        if (_hops.AddOrUpdate(hopKey, 1, (_, value) => value + 1) > 1) Interlocked.Increment(ref _ttlOverlap);
        if (_hosts.AddOrUpdate(key, 1, (_, value) => value + 1) > 1) Interlocked.Increment(ref _overlap);
        int active = Interlocked.Increment(ref _active);
        int previous;
        do { previous = _maximum; } while (active > previous && Interlocked.CompareExchange(ref _maximum, active, previous) != previous);
        Ttls.Enqueue(ttl);
        try { return await Handler(address, ttl, token); }
        finally { Interlocked.Decrement(ref _active); _hosts.AddOrUpdate(key, 0, (_, value) => value - 1); _hops.AddOrUpdate(hopKey, 0, (_, value) => value - 1); }
    }
}

internal static class Eventually
{
    public static async Task Until(Func<bool> predicate, int timeoutMs = 10000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!predicate()) { if (DateTime.UtcNow >= deadline) throw new TimeoutException("Expected condition was not reached."); await Task.Delay(10); }
    }
}
