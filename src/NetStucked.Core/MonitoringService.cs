namespace NetStucked.Core;

public sealed class MonitoringService : IAsyncDisposable
{
    public const int MaximumDevices = 50;
    private readonly IMonitorProbe _probe;
    private readonly IMonitorStore _store;
    private readonly SemaphoreSlim _workers = new(8);
    private readonly SemaphoreSlim _commands = new(1);
    private readonly SemaphoreSlim _saving = new(1);
    private readonly object _gate = new();
    private readonly Dictionary<Guid, MonitorDevice> _devices;
    private readonly Dictionary<Guid, Slot> _slots = [];
    private readonly Dictionary<Guid, MonitorSnapshot> _snapshots = [];
    private readonly List<MonitorIncident> _incidents;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _saver;
    private bool _disposed, _dirty, _suspended;
    private int _generation;
    private DateTimeOffset? _gapStarted;
    public bool Notifications { get; set; }
    public bool Sound { get; set; }
    public string? SaveError { get; private set; }
    public event Action<MonitorIncident>? IncidentChanged;
    public IReadOnlyList<MonitorDevice> Devices { get { lock (_gate) return _devices.Values.ToArray(); } }
    public IReadOnlyList<MonitorSnapshot> Snapshots { get { lock (_gate) return _snapshots.Values.ToArray(); } }
    public IReadOnlyList<MonitorIncident> Incidents { get { lock (_gate) return _incidents.OrderByDescending(i => i.StartedAt).ToArray(); } }
    public int ActiveCount { get { lock (_gate) return _slots.Count; } }
    public MonitoringService(IMonitorProbe probe, IMonitorStore store)
    {
        _probe = probe; _store = store; var data = store.Load();
        _devices = data.Devices.ToDictionary(d => d.Id); _incidents = data.Incidents.ToList(); Notifications = data.Notifications; Sound = data.Sound;
        if (data.LastSavedAt is DateTimeOffset last && _devices.Values.Any(d => d.Started)) _incidents.Add(new(Guid.NewGuid(), Guid.Empty, "Application", "Observation gap", last, DateTimeOffset.UtcNow, "App stopped/asleep; no device state is inferred for this gap."));
        _saver = SaveLoopAsync();
    }
    public async Task ResumeSavedAsync() { foreach (var device in Devices.Where(d => d.Started)) await StartAsync(device.Id).ConfigureAwait(false); }
    public async Task SaveDeviceAsync(MonitorDevice device)
    {
        device.Validate(); await _commands.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_gate) { if (_slots.ContainsKey(device.Id)) throw new InvalidOperationException("Stop this monitor before changing its configuration."); if (!_devices.ContainsKey(device.Id) && _devices.Count >= MaximumDevices) throw new ArgumentException("This release supports up to 50 configured devices."); _devices[device.Id] = device with { Started = false }; _dirty = true; }
            await SaveAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally { _commands.Release(); }
    }
    public async Task RemoveAsync(Guid id)
    {
        await StopAsync(id).ConfigureAwait(false); lock (_gate) { _devices.Remove(id); _snapshots.Remove(id); _dirty = true; } await SaveAsync(CancellationToken.None).ConfigureAwait(false);
    }
    public async Task StartAsync(Guid id)
    {
        await _commands.WaitAsync().ConfigureAwait(false);
        try
        {
            MonitorDevice device;
            lock (_gate) { if (_disposed) throw new ObjectDisposedException(nameof(MonitoringService)); if (_slots.ContainsKey(id)) return; device = _devices[id] with { Started = true }; device.Validate(); _devices[id] = device; _dirty = true; }
            await SaveAsync(CancellationToken.None).ConfigureAwait(false);
            lock (_gate) { var slot = new Slot(CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token), new MonitorPolicy(_incidents.Where(i => i.DeviceId == id))); _slots[id] = slot; slot.Task = RunAsync(device, slot); }
        }
        finally { _commands.Release(); }
    }
    public async Task StopAsync(Guid id, bool preserveIntent = false)
    {
        await _commands.WaitAsync().ConfigureAwait(false);
        try
        {
            Slot? slot; lock (_gate) { _slots.TryGetValue(id, out slot); if (!preserveIntent && _devices.TryGetValue(id, out var device)) { _devices[id] = device with { Started = false }; _dirty = true; } }
            if (slot is not null) { await slot.Cancel.CancelAsync().ConfigureAwait(false); await slot.Task.ConfigureAwait(false); lock (_gate) _slots.Remove(id); slot.Cancel.Dispose(); }
            lock (_gate) if (_snapshots.TryGetValue(id, out var last)) _snapshots[id] = last with { Health = MonitorHealth.Stopped, Evidence = preserveIntent ? "Stopped with auto-resume intent retained" : "Stopped by user" };
            await SaveAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally { _commands.Release(); }
    }
    public async Task StopAllAsync(bool preserveIntent = true) { foreach (Guid id in Devices.Select(d => d.Id)) await StopAsync(id, preserveIntent).ConfigureAwait(false); }
    public async Task SetMaintenanceAsync(Guid id, bool enabled)
    {
        lock (_gate) { _devices[id] = _devices[id] with { Maintenance = enabled }; _dirty = true; }
        await SaveAsync(CancellationToken.None).ConfigureAwait(false);
    }
    public void ObservationGap(string reason)
    {
        lock (_gate) { foreach (var slot in _slots.Values) slot.Policy.ResetCounterBaseline(); _incidents.Add(new(Guid.NewGuid(), Guid.Empty, "Application", "Observation gap", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, reason)); TrimIncidents(); _dirty = true; }
    }
    public void SetSuspended(bool value, string reason)
    {
        lock (_gate)
        {
            _generation++; _suspended = value;
            foreach (var slot in _slots.Values) slot.Policy.ResetCounterBaseline();
            foreach (var key in _snapshots.Keys.ToArray()) _snapshots[key] = _snapshots[key] with { Health = MonitorHealth.Unknown, Evidence = reason };
            if (value) _gapStarted ??= DateTimeOffset.UtcNow;
            else { _incidents.Add(new(Guid.NewGuid(), Guid.Empty, "Application", "Observation gap", _gapStarted ?? DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, reason)); _gapStarted = null; TrimIncidents(); }
            _dirty = true;
        }
    }
    private async Task RunAsync(MonitorDevice initial, Slot slot)
    {
        try
        {
            long nextSnmp = 0;
            while (!slot.Cancel.IsCancellationRequested)
            {
                long began = Environment.TickCount64;
                bool suspended; int generation; lock (_gate) { suspended = _suspended; generation = _generation; }
                if (suspended) { await Task.Delay(1000, slot.Cancel.Token).ConfigureAwait(false); continue; }
                MonitorDevice device; lock (_gate) device = _devices[initial.Id];
                bool readSnmp = device.Snmp && began >= nextSnmp;
                if (readSnmp) nextSnmp = began + device.SnmpIntervalSeconds * 1000L;
                await _workers.WaitAsync(slot.Cancel.Token).ConfigureAwait(false);
                MonitorObservation observation;
                try { observation = await _probe.PollAsync(device, readSnmp, slot.Cancel.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (slot.Cancel.IsCancellationRequested) { throw; }
                catch { observation = new(DateTimeOffset.UtcNow, null, null, null, null, "Collector error; no device state inferred", null); }
                finally { _workers.Release(); }
                slot.Cancel.Token.ThrowIfCancellationRequested();
                IReadOnlyList<MonitorIncident> changes;
                lock (_gate)
                {
                    if (generation != _generation) { nextSnmp = 0; continue; }
                    device = _devices[initial.Id];
                    var result = slot.Policy.Apply(device, observation); _snapshots[device.Id] = result.Snapshot; changes = result.Changes;
                    foreach (var change in changes) { int existing = _incidents.FindIndex(i => i.Id == change.Id); if (existing >= 0) _incidents[existing] = change; else _incidents.Add(change); }
                    TrimIncidents(); _dirty = true;
                }
                foreach (var change in changes) IncidentChanged?.Invoke(change);
                int interval = device.Icmp || device.Tcp ? device.IcmpIntervalSeconds : device.SnmpIntervalSeconds;
                long delay = Math.Max(100, interval * 1000L - (Environment.TickCount64 - began));
                await Task.Delay(TimeSpan.FromMilliseconds(delay), slot.Cancel.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (slot.Cancel.IsCancellationRequested) { }
    }
    private void TrimIncidents() { _incidents.RemoveAll(i => i.EndedAt < DateTimeOffset.UtcNow.AddDays(-7)); while (_incidents.Count > 5000) { int oldest = _incidents.FindIndex(i => i.EndedAt is not null); if (oldest < 0) break; _incidents.RemoveAt(oldest); } }
    public async Task SaveAsync(CancellationToken token)
    {
        await _saving.WaitAsync(token).ConfigureAwait(false);
        try { MonitorData data; lock (_gate) { data = new(1, _devices.Values.ToArray(), _incidents.ToArray(), DateTimeOffset.UtcNow, Notifications, Sound); _dirty = false; }
            try { await _store.SaveAsync(data, token).ConfigureAwait(false); SaveError = null; }
            catch { lock (_gate) _dirty = true; SaveError = "Monitoring settings/history could not be saved."; throw; }
        } finally { _saving.Release(); }
    }
    private async Task SaveLoopAsync()
    {
        try { while (true) { await Task.Delay(5000, _lifetime.Token).ConfigureAwait(false); bool dirty; lock (_gate) dirty = _dirty; if (dirty) try { await SaveAsync(_lifetime.Token).ConfigureAwait(false); } catch when (!_lifetime.IsCancellationRequested) { } } }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }
    public async ValueTask DisposeAsync() { if (_disposed) return; _disposed = true; await StopAllAsync().ConfigureAwait(false); _lifetime.Cancel(); await _saver.ConfigureAwait(false); await SaveAsync(CancellationToken.None).ConfigureAwait(false); _lifetime.Dispose(); _commands.Dispose(); _workers.Dispose(); _saving.Dispose(); }
    private sealed class Slot(CancellationTokenSource cancel, MonitorPolicy policy) { public CancellationTokenSource Cancel { get; } = cancel; public MonitorPolicy Policy { get; } = policy; public Task Task { get; set; } = Task.CompletedTask; }
}
