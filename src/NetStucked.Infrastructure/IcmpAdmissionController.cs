using System.Diagnostics;

namespace NetStucked.Infrastructure;

/// <summary>Independent reserved ICMP slots and paced send admission; native timeout starts after admission.</summary>
public sealed class IcmpAdmissionController : IDisposable
{
    private readonly SemaphoreSlim _ping, _trace;
    private readonly PacedBudget _pingRate, _traceRate;
    public IcmpAdmissionController(int pingSlots = 32, int traceSlots = 32, int pingRate = 128, int traceRate = 128)
    {
        if (pingSlots is < 1 or > 64 || traceSlots is < 1 or > 64 || pingRate is < 1 or > 512 || traceRate is < 1 or > 512) throw new ArgumentOutOfRangeException(nameof(pingSlots));
        _ping = new(pingSlots, pingSlots); _trace = new(traceSlots, traceSlots);
        _pingRate = new(pingRate); _traceRate = new(traceRate);
    }
    public async ValueTask<IDisposable> EnterAsync(bool trace, CancellationToken token)
    {
        var slots = trace ? _trace : _ping;
        await slots.WaitAsync(token).ConfigureAwait(false);
        try { await (trace ? _traceRate : _pingRate).WaitAsync(token).ConfigureAwait(false); return new Lease(slots); }
        catch { slots.Release(); throw; }
    }
    private sealed class Lease(SemaphoreSlim slots) : IDisposable
    {
        private int _released;
        public void Dispose() { if (Interlocked.Exchange(ref _released, 1) == 0) slots.Release(); }
    }
    private sealed class PacedBudget(int rate) : IDisposable
    {
        private readonly SemaphoreSlim _turn = new(1, 1);
        private long _last;
        public async Task WaitAsync(CancellationToken token)
        {
            await _turn.WaitAsync(token).ConfigureAwait(false);
            try
            {
                while (_last != 0)
                {
                    double remaining = 1000d / rate - Stopwatch.GetElapsedTime(_last).TotalMilliseconds;
                    if (remaining <= 0) break;
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, remaining)), token).ConfigureAwait(false);
                }
                token.ThrowIfCancellationRequested(); _last = Stopwatch.GetTimestamp();
            }
            finally { _turn.Release(); }
        }
        public void Dispose() => _turn.Dispose();
    }
    public void Dispose() { _ping.Dispose(); _trace.Dispose(); _pingRate.Dispose(); _traceRate.Dispose(); }
}
