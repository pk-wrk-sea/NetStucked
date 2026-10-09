using System.Net;
using System.Text.Json;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed class RipeWanIdentitySource : IWanIdentitySource, IDisposable
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _lastRequest;
    public RipeWanIdentitySource() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(8) }) { }
    public RipeWanIdentitySource(HttpClient http) => _http = http;
    public async Task<WanIdentity?> LookupAsync(IPAddress address, CancellationToken cancellationToken)
    {
        if (!HopDescriptionParser.IsPublic(address)) return null;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var network = await ReadAsync("network-info", address.ToString(), cancellationToken).ConfigureAwait(false);
            if (!network.RootElement.GetProperty("data").TryGetProperty("prefix", out var prefix) || string.IsNullOrEmpty(prefix.GetString())) return null;
            using var overview = await ReadAsync("prefix-overview", prefix.GetString()!, cancellationToken).ConfigureAwait(false);
            var entries = overview.RootElement.GetProperty("data").GetProperty("asns");
            var identities = new List<string>();
            foreach (var entry in entries.EnumerateArray().Take(4))
            {
                if (!entry.TryGetProperty("asn", out var asn) || !asn.TryGetInt64(out var number) || number <= 0) continue;
                string holder = entry.TryGetProperty("holder", out var value) ? value.GetString() ?? "" : "";
                holder = new string(holder.Where(c => !char.IsControl(c)).Take(240).ToArray());
                identities.Add(string.IsNullOrWhiteSpace(holder) ? $"AS{number}" : $"{holder} · AS{number}");
            }
            return identities.Count == 0 ? null : new(string.Join(" / ", identities), DateTimeOffset.UtcNow);
        }
        finally { _gate.Release(); }
    }
    private async Task<JsonDocument> ReadAsync(string endpoint, string resource, CancellationToken token)
    {
        var wait = TimeSpan.FromSeconds(1) - (DateTimeOffset.UtcNow - _lastRequest);
        if (wait > TimeSpan.Zero) await Task.Delay(wait, token).ConfigureAwait(false);
        _lastRequest = DateTimeOffset.UtcNow;
        using var response = await _http.GetAsync($"https://stat.ripe.net/data/{endpoint}/data.json?resource={Uri.EscapeDataString(resource)}", HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 262144) throw new IOException("WAN metadata response exceeds its limit.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(8));
        await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
        using var output = new MemoryStream(); var buffer = new byte[8192]; int count;
        while ((count = await stream.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) > 0)
        {
            if (output.Length + count > 262144) throw new IOException("WAN metadata response exceeds its limit.");
            output.Write(buffer, 0, count);
        }
        return JsonDocument.Parse(output.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
    }
    public void Dispose() { _http.Dispose(); _gate.Dispose(); }
}
