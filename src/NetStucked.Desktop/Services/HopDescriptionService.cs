using System.Net;
using System.Windows.Threading;
using NetStucked.Core;
using NetStucked.Infrastructure;

namespace NetStucked.Desktop.Services;

/// <summary>One bounded metadata cache and manual mapping shared by all trace sessions.</summary>
public sealed class HopDescriptionService(UserSettingsStore store, IWanIdentitySource source) : IAsyncDisposable
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly Dictionary<string, (string Value, DateTimeOffset Expiry)> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource _lookupScope = new();
    private readonly SemaphoreSlim _network = new(2, 2);
    private bool _disposed, _suspended;
    public event Action? Changed;
    public string Text => string.Join(Environment.NewLine, store.Preferences.HopDescriptions.Select(p => $"{p.Key} {p.Value}"));
    public bool WanEnabled => store.Preferences.WanDescriptions;
    public async Task SaveAsync(string text, bool wan)
    {
        var parsed = HopDescriptionParser.Parse(text);
        var previous = store.Preferences.HopDescriptions; bool priorWan = WanEnabled;
        store.Preferences.HopDescriptions = new(parsed, StringComparer.OrdinalIgnoreCase); store.Preferences.WanDescriptions = wan;
        try { await store.SaveAsync(); }
        catch { store.Preferences.HopDescriptions = previous; store.Preferences.WanDescriptions = priorWan; throw; }
        if (!wan) await CancelPendingAsync();
        else _suspended = false;
        Changed?.Invoke();
    }
    // Called on the presentation Dispatcher; completions return to the same Dispatcher.
    public string Resolve(string? raw, string fallback = "")
    {
        if (!IPAddress.TryParse(raw, out var address)) return fallback;
        string key = address.ToString();
        if (store.Preferences.HopDescriptions.TryGetValue(key, out var description)) return description;
        if (!WanEnabled || !HopDescriptionParser.IsPublic(address)) return fallback;
        if (_cache.TryGetValue(key, out var cached) && cached.Expiry > DateTimeOffset.UtcNow) return cached.Value.Length == 0 ? fallback : cached.Value;
        // Bounded pending work prevents a rapidly changing route from creating an unbounded task queue.
        if (!_disposed && !_suspended && !_pending.ContainsKey(key) && _pending.Count < 32)
        {
            var work = FetchAsync(key, address, _lookupScope.Token); _pending[key] = work;
        }
        return fallback;
    }
    private async Task FetchAsync(string key, IPAddress address, CancellationToken scope)
    {
        // Yield before completion so Resolve can register this task before it is removed.
        await Task.Yield();
        string value = ""; bool succeeded = false;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, scope);
            await _network.WaitAsync(linked.Token).ConfigureAwait(false);
            try { var result = await source.LookupAsync(address, linked.Token).ConfigureAwait(false); value = result?.Description ?? ""; succeeded = value.Length > 0; }
            finally { _network.Release(); }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { /* Unavailable metadata does not change probe outcomes. */ }
        await _dispatcher.InvokeAsync(() =>
        {
            _pending.Remove(key);
            if (_disposed || scope.IsCancellationRequested) return;
            if (_cache.Count >= 512) _cache.Remove(_cache.OrderBy(p => p.Value.Expiry).First().Key);
            _cache[key] = (value, DateTimeOffset.UtcNow.Add(succeeded ? TimeSpan.FromHours(24) : TimeSpan.FromMinutes(10)));
            Changed?.Invoke();
        });
    }
    public async Task CancelPendingAsync()
    {
        _suspended = true;
        var previous = _lookupScope; _lookupScope = new(); await previous.CancelAsync();
        await Task.WhenAll(_pending.Values.ToArray()); previous.Dispose();
    }
    public void Resume() { if (!_disposed && WanEnabled) _suspended = false; }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true; await _lifetime.CancelAsync(); await Task.WhenAll(_pending.Values.ToArray());
        _network.Dispose(); _lifetime.Dispose(); _lookupScope.Dispose();
    }
}
