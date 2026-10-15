using System.Net;
using System.Text;
using System.Text.Json;
using NetStucked.Core;

namespace NetStucked.Infrastructure;
public sealed class VirusTotalUrlLookup : IUrlReputationLookup, IDisposable
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1);
    private long _next;
    public VirusTotalUrlLookup() : this(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan }) { }
    public VirusTotalUrlLookup(HttpClient http) => _http = http;
    public async Task<UrlReputationResult> LookupAsync(Uri url, string apiKey, CancellationToken token)
    {
        _ = DiagnosticInput.HttpUrl(url.AbsoluteUri);
        if (apiKey.Length is < 16 or > 256 || apiKey.Any(char.IsControl)) throw new ArgumentException("Configure a valid VirusTotal account API key.");
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            long delay = _next - Environment.TickCount64;
            if (delay > 0) await Task.Delay(TimeSpan.FromMilliseconds(delay), token).ConfigureAwait(false);
            _next = Environment.TickCount64 + 15000;
            string identifier = Convert.ToBase64String(Encoding.UTF8.GetBytes(url.AbsoluteUri)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(15000);
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.virustotal.com/api/v3/urls/" + identifier);
            request.Headers.Add("x-apikey", apiKey); request.Headers.Accept.ParseAdd("application/json");
            try
            {
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return Result("Access denied", "The configured account key or API entitlement was rejected.");
                if (response.StatusCode == HttpStatusCode.NotFound) return Result("No report", "No existing URL report was returned. No new scan was submitted; this is not a safe verdict.");
                if ((int)response.StatusCode == 429) { var retry = response.Headers.RetryAfter; double wait = retry?.Delta?.TotalMilliseconds ?? (retry?.Date is { } retryDate ? (retryDate-DateTimeOffset.UtcNow).TotalMilliseconds : 60000); _next = Environment.TickCount64 + (long)Math.Clamp(wait, 15000, 3600000); return Result("Rate limited", "Provider quota reached; retry after cooldown."); }
                if (!response.IsSuccessStatusCode) return Result("API error", $"VirusTotal returned HTTP {(int)response.StatusCode}.");
                await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
                byte[] bytes = new byte[262145]; int length = 0;
                while (length < bytes.Length) { int read = await stream.ReadAsync(bytes.AsMemory(length), deadline.Token).ConfigureAwait(false); if (read == 0) break; length += read; }
                if (length > 262144) return Result("API error", "Provider response exceeded the 256 KiB limit.");
                using var json = JsonDocument.Parse(bytes.AsMemory(0, length), new JsonDocumentOptions { MaxDepth = 32 });
                var attributes = json.RootElement.GetProperty("data").GetProperty("attributes"); var stats = attributes.GetProperty("last_analysis_stats");
                int Count(string name) => stats.TryGetProperty(name, out var value) && value.TryGetInt32(out int number) && number >= 0 ? number : throw new InvalidDataException("Invalid provider counters.");
                int malicious = Count("malicious"), suspicious = Count("suspicious"), harmless = Count("harmless"), undetected = Count("undetected");
                DateTimeOffset? time = attributes.TryGetProperty("last_analysis_date", out var date) ? DateTimeOffset.FromUnixTimeSeconds(date.GetInt64()) : null;
                return new(url.AbsoluteUri, malicious > 0 ? "Malicious detections" : suspicious > 0 ? "Suspicious detections" : "No malicious detections reported", malicious, suspicious, harmless, undetected, time, "VirusTotal existing analysis; undetected does not guarantee safety.");
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { return Result("Timeout", "Provider lookup timed out."); }
            catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or KeyNotFoundException or InvalidOperationException or ArgumentOutOfRangeException) { return Result("API error", "Unable to read a valid provider report; no verdict inferred."); }
        }
        finally { _gate.Release(); }
        UrlReputationResult Result(string status, string details) => new(url.AbsoluteUri, status, Details: details);
    }
    public void Dispose() { _http.Dispose(); _gate.Dispose(); }
}
