using System.Net;
using System.Text;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed class MacVendorLookup : IMacVendorLookup, IDisposable
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1);
    private readonly Dictionary<string, MacVendorResult> _cache = new(StringComparer.Ordinal);
    private long _nextRequest;
    private long _blockedUntil;
    public MacVendorLookup() : this(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan }) { }
    public MacVendorLookup(HttpClient http) => _http = http;
    public async Task<MacVendorResult> LookupAsync(MacAddressInfo address, int timeoutMs, CancellationToken token)
    {
        if (timeoutMs is < 250 or > 60000) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        token.ThrowIfCancellationRequested();
        if (!address.CanIdentifyVendor) return new(DateTimeOffset.UtcNow, address.Address, address.Oui, null, address.AddressType, "Not identifiable", "Address bits do not reliably identify a manufacturer; no API request was sent.");
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(address.Address, out var cached) && DateTimeOffset.UtcNow - cached.Time < TimeSpan.FromDays(1)) return cached with { Details = "MACVendors.com · cached lookup, original timestamp retained" };
            if (_blockedUntil > Environment.TickCount64) return Result("Rate limited", null, "Provider cooldown is active; no API request was sent.");
            long wait = _nextRequest - Environment.TickCount64;
            if (wait > 0) await Task.Delay(TimeSpan.FromMilliseconds(wait), token).ConfigureAwait(false);
            _nextRequest = Environment.TickCount64 + 1000;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(timeoutMs);
            try
            {
                using var response = await _http.GetAsync("https://api.macvendors.com/" + Uri.EscapeDataString(address.Address), HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound) return Result("Not found", null, "No registered vendor was returned by MACVendors.com.");
                if ((int)response.StatusCode == 429)
                {
                    var retry = response.Headers.RetryAfter;
                    double delayMs = retry?.Delta?.TotalMilliseconds ?? (retry?.Date is { } date ? (date - DateTimeOffset.UtcNow).TotalMilliseconds : 60000);
                    _blockedUntil = Environment.TickCount64 + (long)Math.Clamp(delayMs, 1000, 3600000);
                    return Result("Rate limited", null, "MACVendors.com quota reached. Provider cooldown applied; this is not a vendor result.");
                }
                if (!response.IsSuccessStatusCode) return Result("API error", null, $"MACVendors.com returned HTTP {(int)response.StatusCode}.");
                await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
                byte[] bytes = new byte[4097]; int length = 0;
                while (length < bytes.Length) { int read = await stream.ReadAsync(bytes.AsMemory(length), deadline.Token).ConfigureAwait(false); if (read == 0) break; length += read; }
                if (length > 4096) return Result("API error", null, "Vendor response exceeded the 4 KiB limit.");
                string vendor = new UTF8Encoding(false, true).GetString(bytes, 0, length).Trim();
                if (vendor.Length is 0 or > 512 || vendor.Any(char.IsControl) || vendor.StartsWith('<') || vendor.StartsWith('{')) return Result("API error", null, "Unexpected vendor response.");
                var result = Result("Found", vendor, "MACVendors.com · manufacturer registration; not proof of device identity");
                if (_cache.Count >= 512) _cache.Remove(_cache.MinBy(entry => entry.Value.Time).Key);
                _cache[address.Address] = result;
                return result;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { return Result("Timeout", null, "Vendor lookup deadline expired."); }
            catch (Exception ex) when (ex is HttpRequestException or IOException or DecoderFallbackException) { return Result("API error", null, "Vendor lookup failed; no vendor inferred."); }
        }
        finally { _gate.Release(); }
        MacVendorResult Result(string status, string? vendor, string details) => new(DateTimeOffset.UtcNow, address.Address, address.Oui, vendor, address.AddressType, status, details);
    }
    public void Dispose() { _http.Dispose(); _gate.Dispose(); }
}
