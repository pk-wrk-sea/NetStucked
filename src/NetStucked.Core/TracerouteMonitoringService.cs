using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Channels;

namespace NetStucked.Core;

public sealed class TracerouteMonitoringService(IIcmpProbe probe, IDnsResolver dns) : AsyncSession
{
    private readonly object _data = new();
    private readonly SortedDictionary<int, HopAccumulator> _hops = new();
    private readonly SortedDictionary<int, TraceHopSnapshot> _published = new();
    private readonly Queue<TraceEvent> _events = new();
    private readonly Dictionary<int, HashSet<string>> _previousRoute = new();
    private readonly Dictionary<string, (string? Name, long Expires)> _names = new();
    private readonly HashSet<string> _pendingNames = new();
    private readonly EnginePerformance _performance = new();
    private readonly Dictionary<int, long> _pollStart = new();
    private readonly Dictionary<int, HashSet<string>> _pollObserved = new();
    private readonly Dictionary<string, long> _quietEvents = new();
    private string _lastOutcome = "";
    private long _revision, _probeId, _cycleId;
    private int _completedCycles;
    public int CompletedCycles => Volatile.Read(ref _completedCycles);
    public string Outcome { get; private set; } = "Idle";

    public Task StartAsync(string target, TraceSettings settings)
    {
        settings.Validate();
        if (!TargetInputParser.IsValidHost(target) || IPAddress.TryParse(target, out var ip) && ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new ArgumentException("Traceroute requires one IPv4 address or hostname. IPv6 traceroute is not validated in v0.1.0.");
        return StartSessionAsync(() =>
        {
            lock (_data)
            {
                _hops.Clear(); _published.Clear(); _events.Clear(); _previousRoute.Clear(); _names.Clear(); _pendingNames.Clear(); _pollStart.Clear(); _pollObserved.Clear(); _quietEvents.Clear(); _lastOutcome = "";
                _completedCycles = 0; _revision = _probeId = _cycleId = 0; Outcome = "Resolving"; _performance.Reset(SessionId);
                Log("Info", null, $"Trace started — ICMP, {target}");
            }
        }, token => RunAsync(target, settings, token));
    }

    private async Task RunAsync(string target, TraceSettings settings, CancellationToken lifetime)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        var token = cancellation.Token;
        using var rate = new PacketRateBudget(settings.MaxPacketsPerSecond, Math.Min(8, settings.HopConcurrency));
        var names = Channel.CreateBounded<IPAddress>(64);
        var clock = Stopwatch.StartNew();
        async Task NameWorker()
        {
            try
            {
                await foreach (var address in names.Reader.ReadAllAsync(token).ConfigureAwait(false))
                {
                    string key = address.ToString();
                    using var operation = await EnterOperationAsync(token).ConfigureAwait(false);
                    long start = Stopwatch.GetTimestamp(); _performance.Enter();
                    try
                    {
                        string? name = await dns.ReverseAsync(address, operation.Token).ConfigureAwait(false);
                        operation.Token.ThrowIfCancellationRequested();
                        lock (_data)
                        {
                            if (_names.Count >= 512) _names.Remove(_names.MinBy(p => p.Value.Expires).Key);
                            _names[key] = (name, clock.ElapsedMilliseconds + (name is null ? 30000 : 900000));
                            if (name is not null) Log("DNS", null, $"{key} resolved to {name}");
                            foreach (var hop in _hops.Values.Where(h => h.Address == key)) { hop.Hostname = name; Publish(hop); }
                        }
                    }
                    catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) { token.ThrowIfCancellationRequested(); }
                    catch (Exception ex) { lock (_data) { _names[key] = (null, clock.ElapsedMilliseconds + 30000); Log("Error", null, $"Reverse DNS: {ex.Message}"); } }
                    finally { _performance.Dns(Stopwatch.GetElapsedTime(start).TotalMilliseconds); _performance.Leave(); lock (_data) _pendingNames.Remove(key); }
                }
            }
            catch { await cancellation.CancelAsync().ConfigureAwait(false); throw; }
        }
        var workers = settings.ReverseDns ? new[] { NameWorker(), NameWorker() } : [];
        IPAddress? destination = IPAddress.TryParse(target, out var literal) ? literal : null;
        try
        {
            while (!token.IsCancellationRequested)
            {
                int destinationHop = 0, activeHops = 0;
                using (var operation = await EnterOperationAsync(token).ConfigureAwait(false))
                {
                    try
                    {
                        if (destination is null)
                        {
                            long start = Stopwatch.GetTimestamp(); _performance.Enter();
                            try { destination = await dns.ResolveAsync(target, IpFamily.IPv4, operation.Token).ConfigureAwait(false); }
                            finally { _performance.Dns(Stopwatch.GetElapsedTime(start).TotalMilliseconds); _performance.Leave(); }
                            lock (_data) Log("DNS", null, $"{target} resolved to {destination}");
                        }
                        var result = await CycleAsync(destination, settings, rate, names.Writer, clock, null, operation.Token).ConfigureAwait(false);
                        destinationHop = result.DestinationHop;
                        lock (_data) activeHops = _hops.Count == 0 ? 0 : _hops.Keys.Max();
                        if (!settings.Continuous) return;
                    }
                    catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) { token.ThrowIfCancellationRequested(); }
                }
                if (activeHops > 0) await PollHopsAsync(destination!, destinationHop, activeHops, settings, rate, names.Writer, clock, token).ConfigureAwait(false);
            }
        }
        finally
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
            try { await Task.WhenAll(workers).ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        }
    }

    private async Task<(int DestinationHop, bool Changed)> CycleAsync(IPAddress destination, TraceSettings settings,
        PacketRateBudget rate, ChannelWriter<IPAddress> names, Stopwatch clock, int[]? subset, CancellationToken token)
    {
        using var cycleCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        token = cycleCancellation.Token;
        var observed = new Dictionary<int, HashSet<string>>();
        var visited = new List<int>();
        var inFlight = new Dictionary<int, CancellationTokenSource>();
        int reached = int.MaxValue, terminal = int.MaxValue;
        long cycle = Interlocked.Increment(ref _cycleId);
        lock (_data) Outcome = "Tracing";

        async Task Send(int ttl)
        {
            using var ttlCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            lock (_data) { if (ttl > (reached != int.MaxValue ? reached : terminal)) return; inFlight[ttl] = ttlCancellation; }
            long start = 0; bool entered = false, cancelled = false;
            try
            {
                long queued = Stopwatch.GetTimestamp();
                await rate.WaitAsync(ttlCancellation.Token).ConfigureAwait(false);
                start = Stopwatch.GetTimestamp(); _performance.Enter(); entered = true;
                _performance.Queue(Stopwatch.GetElapsedTime(queued, start).TotalMilliseconds);
                long id = Interlocked.Increment(ref _probeId);
                ProbeResult reply;
                try { reply = await probe.SendAsync(destination, settings.TimeoutMs, settings.PacketSize, ttl, ttlCancellation.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { reply = new(IPStatus.Unknown, null, null, Error: ex.Message); }
                ttlCancellation.Token.ThrowIfCancellationRequested();
                CancellationTokenSource[] cancelAboveDestination = [];
                lock (_data)
                {
                    if (ttl > reached) return;
                    RecordHop(ttl, reply, settings, names, clock, cycle, id);
                    if (reply.IsHopReply)
                    {
                        if (!observed.TryGetValue(ttl, out var addresses)) observed[ttl] = addresses = new(StringComparer.Ordinal);
                        addresses.Add(reply.Address!.ToString());
                    }
                    if (reply.IsEchoReply && reply.Address!.Equals(destination))
                    {
                        reached = Math.Min(reached, ttl); _performance.Destination(clock.Elapsed.TotalMilliseconds);
                        // Cancel speculative TTLs above the verified destination; await their cleanup in the window.
                        cancelAboveDestination = inFlight.Where(p => p.Key > reached).Select(p => p.Value).ToArray();
                    }
                    if (reply.IsTerminal) terminal = Math.Min(terminal, ttl);
                }
                // Cancellation can invoke arbitrary API callbacks. Keep those callbacks outside the snapshot lock.
                foreach (var pending in cancelAboveDestination)
                {
                    try { await pending.CancelAsync().ConfigureAwait(false); }
                    catch (ObjectDisposedException) { /* This TTL finished and disposed between capture and cancellation. */ }
                }
            }
            catch (OperationCanceledException) when (ttlCancellation.IsCancellationRequested)
            { cancelled = true; token.ThrowIfCancellationRequested(); }
            finally
            {
                if (entered) { _performance.Probe(Stopwatch.GetElapsedTime(start).TotalMilliseconds, cancelled); _performance.Leave(); }
                lock (_data) inFlight.Remove(ttl);
            }
        }

        var candidates = new Queue<int>(subset ?? Enumerable.Range(1, settings.MaxHops));
        var running = new Dictionary<int, Task>();
        int discoveryConcurrency = Math.Min(4, settings.HopConcurrency);
        try
        {
            while (candidates.Count > 0 || running.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                while (running.Count < discoveryConcurrency && candidates.TryDequeue(out int ttl))
                {
                    if (ttl > Math.Min(reached, terminal)) { candidates.Clear(); break; }
                    visited.Add(ttl); running[ttl] = Send(ttl);
                }
                if (running.Count == 0) break;
                await Task.WhenAny(running.Values).ConfigureAwait(false);
                foreach (int ttl in running.Where(p => p.Value.IsCompleted).Select(p => p.Key).ToArray())
                { await running[ttl].ConfigureAwait(false); running.Remove(ttl); }
            }
        }
        catch { await cycleCancellation.CancelAsync().ConfigureAwait(false); throw; }
        finally { try { await Task.WhenAll(running.Values).ConfigureAwait(false); } catch (OperationCanceledException) when (token.IsCancellationRequested) { } }
        int cutoff = Math.Min(reached, terminal);
        if (reached != int.MaxValue) cutoff = reached;
        visited.RemoveAll(ttl => ttl > cutoff);
        // Discovery finds the destination quickly. Refinement completes the requested samples per visited TTL.
        foreach (var window in visited.Chunk(settings.HopConcurrency))
        {
            await Task.WhenAll(window.Select(async ttl =>
            {
                for (int attempt = 1; attempt < settings.ProbesPerHop; attempt++) await Send(ttl).ConfigureAwait(false);
            })).ConfigureAwait(false);
        }
        token.ThrowIfCancellationRequested();
        lock (_data)
        {
            cutoff = reached != int.MaxValue ? reached : Math.Min(reached, terminal);
            foreach (int hop in _hops.Keys.Where(h => h > cutoff).ToArray()) { _hops.Remove(hop); _published.Remove(hop); _previousRoute.Remove(hop); _revision++; }
            bool changed = false;
            foreach (var (ttl, addresses) in observed.Where(p => p.Key <= cutoff))
            {
                if (_previousRoute.TryGetValue(ttl, out var previous) && addresses.Any(a => !previous.Contains(a)))
                {
                    changed = true; _hops[ttl].RouteChanges++;
                    Log("Route", ttl, $"Observed alternate response: {string.Join(", ", previous.Order())} → {string.Join(", ", addresses.Order())} (ECMP possible)");
                    Publish(_hops[ttl]);
                }
                _previousRoute[ttl] = addresses;
            }
            Interlocked.Increment(ref _completedCycles);
            SetOutcome(reached != int.MaxValue);
            return (reached == int.MaxValue ? 0 : reached, changed);
        }
    }

    public new async Task PauseAsync() { bool running = State == SessionState.Running; await base.PauseAsync().ConfigureAwait(false); if (running && State == SessionState.Paused) { lock (_data) { _pollObserved.Clear(); foreach (int ttl in _pollStart.Keys.ToArray()) _pollStart[ttl] = _hops[ttl].Stats.Sent; Log("Info", null, "Trace paused"); } } }
    public new async Task ResumeAsync() { bool paused = State == SessionState.Paused; await base.ResumeAsync().ConfigureAwait(false); if (paused && State == SessionState.Running) { lock (_data) Log("Info", null, "Trace resumed"); } }
    public new async Task StopAsync() { bool active = State is SessionState.Running or SessionState.Paused or SessionState.Pausing; await base.StopAsync().ConfigureAwait(false); if (active) { lock (_data) Log("Info", null, "Trace stopped"); } }
    protected override void OnSessionError(Exception error) { lock (_data) { Outcome = "Error"; Log("Error", null, error.Message); } }
    private void Publish(HopAccumulator hop, long? cycle = null, long? probe = null)
    {
        _published.TryGetValue(hop.Hop, out var previous);
        _published[hop.Hop] = hop.Snapshot() with { SessionId = SessionId, CycleId = cycle ?? previous?.CycleId ?? _cycleId,
            ProbeId = probe ?? previous?.ProbeId ?? 0, Revision = ++_revision, PublishedTimestamp = Stopwatch.GetTimestamp() };
    }
    private void Log(string type, int? hop, string message)
    {
        if (type is "Error" or "Route" || type == "Info" && message == "Destination replied")
        {
            string key = $"{type}:{hop}:{message}"; long now = Stopwatch.GetTimestamp();
            if (_quietEvents.TryGetValue(key, out long previous) && Stopwatch.GetElapsedTime(previous, now).TotalSeconds < 10) return;
            if (_quietEvents.Count >= 128) _quietEvents.Remove(_quietEvents.MinBy(p => p.Value).Key);
            _quietEvents[key] = now;
        }
        _events.Enqueue(new(DateTimeOffset.Now, type, hop, message) { SessionId = SessionId, CycleId = _cycleId, Sequence = ++_revision });
        while (_events.Count > 1000) _events.Dequeue();
    }

    private void RecordHop(int ttl, ProbeResult reply, TraceSettings settings, ChannelWriter<IPAddress> names, Stopwatch clock, long cycle, long id)
    {
        if (!_hops.TryGetValue(ttl, out var hop)) _hops[ttl] = hop = new(ttl);
        hop.Stats.Add(reply.IsHopReply ? reply.RttMs : null);
        hop.Address = reply.IsHopReply ? reply.Address!.ToString() : null; hop.Hostname = null;
        hop.Status = reply.IsTerminal ? reply.Status.ToString() : reply.IsHopReply ? "Reply" : reply.Status == IPStatus.TimedOut ? "Timeout" : reply.Status.ToString();
        hop.Updated = DateTimeOffset.Now;
        if (reply.Error is not null) Log("Error", ttl, reply.Error);
        if (settings.ReverseDns && reply.IsHopReply)
        {
            string key = reply.Address!.ToString();
            if (_names.TryGetValue(key, out var cached) && cached.Expires > clock.ElapsedMilliseconds) hop.Hostname = cached.Name;
            else if (_pendingNames.Add(key) && !names.TryWrite(reply.Address!)) _pendingNames.Remove(key);
        }
        Publish(hop, cycle, id);
    }

    private void SetOutcome(bool reached)
    {
        Outcome = reached ? "Destination replied" : "No final reply";
        if (_lastOutcome == Outcome) return;
        _lastOutcome = Outcome; Log(reached ? "Info" : "Error", null, Outcome);
    }

    private async Task PollHopsAsync(IPAddress destination, int destinationHop, int activeHops, TraceSettings settings,
        PacketRateBudget rate, ChannelWriter<IPAddress> names, Stopwatch clock, CancellationToken lifetime)
    {
        using var generation = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        using var slots = new SemaphoreSlim(settings.HopConcurrency, settings.HopConcurrency);
        var rescan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_data)
        {
            _pollStart.Clear(); _pollObserved.Clear();
            foreach (int ttl in Enumerable.Range(1, activeHops)) _pollStart[ttl] = _hops[ttl].Stats.Sent;
        }
        async Task Worker(int ttl)
        {
            var token = generation.Token;
            long due = clock.ElapsedMilliseconds + settings.IntervalMs + (long)(ttl - 1) * settings.IntervalMs / activeHops;
            while (true)
            {
                await Task.Delay((int)Math.Clamp(due - clock.ElapsedMilliseconds, 0, int.MaxValue), token).ConfigureAwait(false);
                using var operation = await EnterOperationAsync(token).ConfigureAwait(false);
                try
                {
                    _performance.Schedule(Math.Max(0, clock.ElapsedMilliseconds - due));
                    long queued = Stopwatch.GetTimestamp();
                    await slots.WaitAsync(operation.Token).ConfigureAwait(false);
                    long start = 0; bool entered = false, cancelled = false;
                    try
                    {
                        await rate.WaitAsync(operation.Token).ConfigureAwait(false);
                        start = Stopwatch.GetTimestamp(); _performance.Enter(); entered = true;
                        _performance.Queue(Stopwatch.GetElapsedTime(queued, start).TotalMilliseconds);
                        long id = Interlocked.Increment(ref _probeId);
                        ProbeResult reply;
                        try { reply = await probe.SendAsync(destination, settings.TimeoutMs, settings.PacketSize, ttl, operation.Token).ConfigureAwait(false); }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) { reply = new(IPStatus.Unknown, null, null, Error: ex.Message); }
                        operation.Token.ThrowIfCancellationRequested();
                        lock (_data)
                        {
                            RecordHop(ttl, reply, settings, names, clock, _cycleId + 1, id);
                            if (reply.IsHopReply)
                            {
                                if (!_pollObserved.TryGetValue(ttl, out var addresses)) _pollObserved[ttl] = addresses = new(StringComparer.Ordinal);
                                if (addresses.Count < 8) addresses.Add(reply.Address!.ToString());
                            }
                            if (reply.IsEchoReply && reply.Address!.Equals(destination) && ttl < activeHops) rescan.TrySetResult();
                            if (ttl == destinationHop)
                            {
                                if (reply.IsEchoReply && reply.Address!.Equals(destination)) SetOutcome(true);
                                else if (_hops[ttl].Stats.ConsecutiveFailures >= 3) SetOutcome(false);
                                if (_hops[ttl].Stats.ConsecutiveFailures >= 3) rescan.TrySetResult();
                            }
                            if (_pollStart.All(p => _hops[p.Key].Stats.Sent > p.Value))
                            {
                                foreach (var (hop, addresses) in _pollObserved)
                                {
                                    if (_previousRoute.TryGetValue(hop, out var previous) && addresses.Any(a => !previous.Contains(a)))
                                    {
                                        _hops[hop].RouteChanges++;
                                        Log("Route", hop, $"Observed alternate response: {string.Join(", ", previous.Order())} → {string.Join(", ", addresses.Order())} (ECMP possible)");
                                        Publish(_hops[hop]); rescan.TrySetResult();
                                    }
                                    _previousRoute[hop] = addresses;
                                }
                                _pollObserved.Clear();
                                foreach (int hop in _pollStart.Keys.ToArray()) _pollStart[hop] = _hops[hop].Stats.Sent;
                                Interlocked.Increment(ref _completedCycles); Interlocked.Increment(ref _cycleId);
                            }
                        }
                    }
                    catch (OperationCanceledException) { cancelled = true; throw; }
                    finally { if (entered) { _performance.Probe(Stopwatch.GetElapsedTime(start).TotalMilliseconds, cancelled); _performance.Leave(); } slots.Release(); }
                }
                catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) { token.ThrowIfCancellationRequested(); }
                due += settings.IntervalMs;
                if (due <= clock.ElapsedMilliseconds)
                {
                    long skipped = (clock.ElapsedMilliseconds - due) / settings.IntervalMs + 1;
                    due += skipped * settings.IntervalMs; _performance.Skipped(skipped);
                }
            }
        }
        var tasks = Enumerable.Range(1, activeHops).Select(Worker).ToArray();
        try
        {
            Task completed = await Task.WhenAny(Task.Delay(settings.FullSweepIntervalMs, generation.Token), rescan.Task, Task.WhenAny(tasks)).ConfigureAwait(false);
            if (completed.IsFaulted) await completed.ConfigureAwait(false);
        }
        finally
        {
            await generation.CancelAsync().ConfigureAwait(false);
            try { await Task.WhenAll(tasks).ConfigureAwait(false); }
            catch (OperationCanceledException) when (generation.IsCancellationRequested) { }
            lock (_data) { _pollStart.Clear(); _pollObserved.Clear(); }
        }
        lifetime.ThrowIfCancellationRequested();
    }
    public EngineDiagnostics Diagnostics => _performance.Snapshot();
    public void ReportUiApplied(TraceHopSnapshot row) { if (row.SessionId == SessionId) _performance.Ui(row.PublishedTimestamp); }
    public TraceUpdate ReadUpdates(long session, long revision)
    {
        lock (_data)
        {
            bool reset = session != SessionId;
            if (!reset && revision == _revision) return new(SessionId, _revision, false, [], _hops.Keys.ToArray(), []);
            return new(SessionId, _revision, reset, _published.Values.Where(h => reset || h.Revision > revision).ToArray(),
                _hops.Keys.ToArray(), _events.Where(e => reset || e.Sequence > revision).ToArray());
        }
    }
    public IReadOnlyList<TraceHopSnapshot> Snapshot() { lock (_data) return _published.Values.ToArray(); }
    public IReadOnlyList<TraceEvent> Events() { lock (_data) return _events.ToArray(); }
    public void SetDescription(int hop, string description) { lock (_data) { if (_hops.TryGetValue(hop, out var value)) { value.Description = description; Publish(value); } } }
}
