using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace NetStucked.Core;

public sealed class MultiTargetPortService(ITcpProbe probe, IDnsResolver dns, IUdpProbe? udp = null) : AsyncSession
{
    private readonly object _data = new();
    private readonly EnginePerformance _performance = new();
    private TargetWork[] _targets = [];
    private PortProbeSettings _settings = new();
    private long _revision, _probeId;
    private PortTotals _totals = new(0, 0, 0, 0, 0);

    private sealed class TargetWork(PortAccumulator value)
    {
        public PortAccumulator Value { get; } = value;
        public PortSnapshot Published { get; set; } = null!;
        public IPAddress? Address { get; set; }
        public long ExpiresMs { get; set; }
        public long DueMs { get; set; }
        public long QueuedTimestamp { get; set; }
    }
    private sealed record Completion(TargetWork Work, bool DnsFailed, bool Cancelled);

    public Task StartAsync(IReadOnlyList<PortTarget> targets, PortProbeSettings settings)
    {
        settings.Validate();
        if (targets.Count == 0 || targets.Count > settings.MaxTargets || targets.Any(t => t is null || !TargetInputParser.IsValidHost(t.Host) || t.Port is < 1 or > 65535 || !Enum.IsDefined(t.Protocol) || t.Description is null || t.Description.Length > 512) ||
            targets.Select(t => t.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != targets.Count)
            throw new ArgumentException("Targets must be unique and within the configured cap.");
        if (udp is null && targets.Any(t => t.Protocol == PortProtocol.UDP)) throw new ArgumentException("UDP probe is unavailable.");
        return StartSessionAsync(() =>
        {
            lock (_data)
            {
                _settings = settings; _revision = _probeId = 0;
                _performance.Reset(SessionId);
                _totals = new(targets.Count, 0, 0, 0, 0);
                int retention = Math.Min(500, 100000 / targets.Count);
                _targets = targets.Select((target, index) => new TargetWork(new(target, index + 1, retention))).ToArray();
                foreach (var target in _targets) Publish(target);
            }
        }, token => RunAsync(settings, token));
    }

    private async Task RunAsync(PortProbeSettings settings, CancellationToken lifetime)
    {
        // Each target owns exactly one item across the scheduler, DNS, TCP and completion queues.
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        var token = cancellation.Token;
        using var network = new SemaphoreSlim(settings.Concurrency, settings.Concurrency);
        using var rate = new PacketRateBudget(settings.MaxPacketsPerSecond, Math.Min(8, settings.Concurrency));
        using var wake = new SemaphoreSlim(0);
        int capacity = _targets.Length;
        var connect = Channel.CreateBounded<TargetWork>(capacity);
        var resolve = Channel.CreateBounded<TargetWork>(capacity);
        var completed = Channel.CreateBounded<Completion>(capacity);
        var clock = Stopwatch.StartNew();
        var schedule = new PriorityQueue<TargetWork, long>();
        for (int i = 0; i < capacity; i++)
        {
            var item = _targets[i];
            item.DueMs = (long)i * Math.Min(1000, settings.IntervalMs) / capacity;
            if (IPAddress.TryParse(item.Value.Target.Host, out var literal) && Matches(literal, settings.AddressFamily))
            { item.Address = literal; item.ExpiresMs = long.MaxValue; }
            schedule.Enqueue(item, item.DueMs);
        }

        async Task Own(Func<Task> action)
        {
            try { await action().ConfigureAwait(false); }
            catch { await cancellation.CancelAsync().ConfigureAwait(false); throw; }
        }
        void Complete(TargetWork item, bool dnsFailed, bool cancelled = false)
        {
            if (!completed.Writer.TryWrite(new(item, dnsFailed, cancelled))) throw new InvalidOperationException("Duplicate target completion.");
            wake.Release();
        }

        async Task Scheduler()
        {
            int remaining = capacity;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                using var operation = await EnterOperationAsync(token).ConfigureAwait(false);
                try
                {
                    while (completed.Reader.TryRead(out var done))
                    {
                        if (!settings.Continuous && !done.Cancelled)
                        {
                            if (--remaining == 0) { connect.Writer.TryComplete(); resolve.Writer.TryComplete(); return; }
                            continue;
                        }
                        long interval = done.DnsFailed ? settings.DnsRetrySeconds * 1000L : settings.IntervalMs;
                        long next = done.Work.DueMs + interval;
                        long now = clock.ElapsedMilliseconds;
                        if (next <= now)
                        {
                            long missed = (now - next) / interval + 1;
                            next += missed * interval;
                            if (!done.DnsFailed) _performance.Skipped(missed);
                        }
                        done.Work.DueMs = next;
                        schedule.Enqueue(done.Work, next);
                    }
                    while (wake.Wait(0)) { }
                    while (schedule.TryPeek(out var next, out long due) && due <= clock.ElapsedMilliseconds)
                    {
                        operation.Token.ThrowIfCancellationRequested();
                        schedule.Dequeue();
                        _performance.Schedule(clock.ElapsedMilliseconds - due);
                        next.QueuedTimestamp = Stopwatch.GetTimestamp();
                        // Admission is synchronous so a pause cannot lose a dequeued target.
                        var queue = next.Address is not null && next.ExpiresMs > clock.ElapsedMilliseconds ? connect : resolve;
                        if (!queue.Writer.TryWrite(next)) throw new InvalidOperationException("Target queue exceeded its cap.");
                    }
                    int delay = schedule.TryPeek(out _, out long nextDue) ? (int)Math.Clamp(nextDue - clock.ElapsedMilliseconds, 1, 60000) : 60000;
                    await wake.WaitAsync(delay, operation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) { token.ThrowIfCancellationRequested(); }
            }
        }

        async Task ResolveWorker()
        {
            await foreach (var item in resolve.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                using var operation = await EnterOperationAsync(token).ConfigureAwait(false);
                bool failed = false, paused = false;
                try
                {
                    await network.WaitAsync(operation.Token).ConfigureAwait(false);
                    long start = Stopwatch.GetTimestamp(); _performance.Enter();
                    try
                    {
                        var address = await dns.ResolveAsync(item.Value.Target.Host, settings.AddressFamily, operation.Token).ConfigureAwait(false);
                        operation.Token.ThrowIfCancellationRequested();
                        item.Address = address; item.ExpiresMs = clock.ElapsedMilliseconds + settings.DnsCacheSeconds * 1000L;
                        lock (_data) { item.Value.ResolvedIp = address.ToString(); Publish(item); }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        operation.Token.ThrowIfCancellationRequested();
                        failed = true; item.Address = null;
                        lock (_data) { item.Value.DnsFailure($"DNS: {ex.Message}"); Publish(item); }
                    }
                    finally { _performance.Dns(Stopwatch.GetElapsedTime(start).TotalMilliseconds); _performance.Leave(); network.Release(); }
                    if (!failed)
                    {
                        item.QueuedTimestamp = Stopwatch.GetTimestamp();
                        if (!connect.Writer.TryWrite(item)) throw new InvalidOperationException("Target queue exceeded its cap.");
                    }
                }
                catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) { token.ThrowIfCancellationRequested(); paused = true; }
                if (failed || paused) Complete(item, failed, paused);
            }
        }

        async Task ConnectWorker()
        {
            await foreach (var item in connect.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                using var operation = await EnterOperationAsync(token).ConfigureAwait(false);
                bool paused = false;
                try
                {
                    await network.WaitAsync(operation.Token).ConfigureAwait(false);
                    long start = 0; bool cancelled = false, entered = false;
                    try
                    {
                        // Consume the rate token immediately before send, rather than reserving it while waiting for a slot.
                        await rate.WaitAsync(operation.Token).ConfigureAwait(false);
                        start = Stopwatch.GetTimestamp(); _performance.Enter(); entered = true;
                        _performance.Queue(Stopwatch.GetElapsedTime(item.QueuedTimestamp, start).TotalMilliseconds);
                        Interlocked.Increment(ref _probeId);
                        TcpProbeResult result;
                        try { result = item.Value.Target.Protocol == PortProtocol.UDP
                            ? await udp!.ProbeAsync(item.Address!, item.Value.Target.Port, settings.TimeoutMs, operation.Token).ConfigureAwait(false)
                            : await probe.ConnectAsync(item.Address!, item.Value.Target.Port, settings.TimeoutMs, operation.Token).ConfigureAwait(false); }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) { result = new(PortOutcome.Error, null, ex.Message); }
                        operation.Token.ThrowIfCancellationRequested();
                        lock (_data) { item.Value.ResolvedIp = item.Address!.ToString(); item.Value.Add(result); Publish(item); }
                    }
                    catch (OperationCanceledException) { cancelled = true; throw; }
                    finally { if (entered) { _performance.Probe(Stopwatch.GetElapsedTime(start).TotalMilliseconds, cancelled); _performance.Leave(); } network.Release(); }
                }
                catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) { token.ThrowIfCancellationRequested(); paused = true; }
                Complete(item, false, paused);
            }
        }

        int dnsWorkers = Math.Min(settings.DnsConcurrency, Math.Max(1, settings.Concurrency / 4));
        bool needsDns = _targets.Any(t => t.Address is null);
        int connectWorkers = needsDns && settings.Concurrency > 1 ? settings.Concurrency - dnsWorkers : settings.Concurrency;
        var tasks = new List<Task> { Own(Scheduler) };
        tasks.AddRange(Enumerable.Range(0, dnsWorkers).Select(_ => Own(ResolveWorker)));
        tasks.AddRange(Enumerable.Range(0, connectWorkers).Select(_ => Own(ConnectWorker)));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static bool Matches(IPAddress address, IpFamily family) => family == IpFamily.Auto ||
        address.AddressFamily == (family == IpFamily.IPv4 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6);

    private void Publish(TargetWork item)
    {
        var previous = item.Published;
        var next = item.Value.Snapshot() with { SessionId = SessionId, Revision = ++_revision, PublishedTimestamp = Stopwatch.GetTimestamp() };
        static int Up(PortSnapshot? row) => row?.Status is "Connected" or "Responded" ? 1 : 0;
        static int Down(PortSnapshot? row) => row is not null && row.Status is not ("Connected" or "Responded" or "No response" or "Unknown") ? 1 : 0;
        static int Closed(PortSnapshot? row) => row?.Status is "Closed" or "Refused" ? 1 : 0;
        static int Silent(PortSnapshot? row) => row?.Status == "No response" ? 1 : 0;
        _totals = _totals with { Connected = _totals.Connected + Up(next) - Up(previous), Failed = _totals.Failed + Down(next) - Down(previous),
            Closed = _totals.Closed + Closed(next) - Closed(previous), NoResponse = _totals.NoResponse + Silent(next) - Silent(previous),
            Attempts = _totals.Attempts + next.Attempts - (previous?.Attempts ?? 0), Successful = _totals.Successful + next.Connected - (previous?.Connected ?? 0) };
        item.Published = next;
    }

    public PortUpdate ReadUpdates(long session, long revision)
    {
        lock (_data)
        {
            bool reset = session != SessionId;
            if (!reset && revision == _revision) return new(SessionId, _revision, false, [], _totals);
            return new(SessionId, _revision, reset, _targets.Where(t => reset || t.Published.Revision > revision).Select(t => t.Published).ToArray(), _totals);
        }
    }
    public EngineDiagnostics Diagnostics => _performance.Snapshot();
    public void ReportUiApplied(PortSnapshot row) { if (row.SessionId == SessionId) _performance.Ui(row.PublishedTimestamp); }
    public IReadOnlyList<PortSnapshot> Snapshot() { lock (_data) return _targets.Select(t => t.Published).ToArray(); }
    public IReadOnlyList<PortSample> History(string? key, int limit = 100)
    {
        lock (_data) return _targets.FirstOrDefault(t => string.Equals(t.Value.Target.Key, key, StringComparison.OrdinalIgnoreCase))?.Value.History(Math.Clamp(limit, 1, 500)) ?? [];
    }
}
