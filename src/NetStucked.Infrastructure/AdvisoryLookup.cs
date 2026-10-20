using System.Net;
using System.Text.Json;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

// Metadata only. No automatic applicability and no transmission of device/configuration data.
public sealed class AdvisoryLookup : IAdvisoryLookup, IDisposable
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1);
    private DateTimeOffset _lastRequest;
    public AdvisoryLookup() : this(new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) { }
    public AdvisoryLookup(HttpMessageHandler handler) { _http = new(handler) { Timeout = TimeSpan.FromSeconds(30) }; _http.DefaultRequestHeaders.UserAgent.ParseAdd("NetStucked/0.11 advisory-metadata"); }
    private async Task<JsonDocument> GetAsync(string url, int maxBytes, CancellationToken token)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        if ((int)response.StatusCode is 429 or 403) throw new IOException("Provider quota/access limit. Retry later; cached metadata was retained.");
        response.EnsureSuccessStatusCode(); if (response.Content.Headers.ContentLength > maxBytes) throw new IOException("Provider response exceeds limit.");
        await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false); using var output = new MemoryStream(); byte[] buffer = new byte[8192]; int count;
        while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0) { if (output.Length + count > maxBytes) throw new IOException("Provider response exceeds limit."); output.Write(buffer, 0, count); }
        return JsonDocument.Parse(output.ToArray());
    }
    public async Task<Advisory> FetchAsync(string cve, CancellationToken token)
    {
        EngineeringRules.CveId(cve); await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var remaining = TimeSpan.FromSeconds(6) - (DateTimeOffset.UtcNow - _lastRequest); if (remaining > TimeSpan.Zero) await Task.Delay(remaining, token).ConfigureAwait(false);
            _lastRequest = DateTimeOffset.UtcNow;
            using var json = await GetAsync("https://services.nvd.nist.gov/rest/json/cves/2.0?cveId=" + cve, 2 * 1024 * 1024, token).ConfigureAwait(false);
            var items = json.RootElement.GetProperty("vulnerabilities");
            if (items.GetArrayLength() != 1) throw new IOException("CVE record not found. Existing cache retained.");
            var record = items[0].GetProperty("cve");
            if (record.GetProperty("id").GetString() != cve) throw new IOException("Provider returned a different CVE ID.");
            string description = record.GetProperty("descriptions").EnumerateArray().FirstOrDefault(x => x.GetProperty("lang").GetString() == "en").GetProperty("value").GetString() ?? "";
            string severity = "Unknown";
            if (record.TryGetProperty("metrics", out var metrics)) foreach (string metric in new[] { "cvssMetricV40", "cvssMetricV31", "cvssMetricV30", "cvssMetricV2" })
            { if (metrics.TryGetProperty(metric, out var values) && values.GetArrayLength() > 0) { var first = values[0]; if (first.GetProperty("cvssData").TryGetProperty("baseSeverity", out var s) || first.TryGetProperty("baseSeverity", out s)) severity = s.GetString() ?? "Unknown"; break; } }
            string[] sources = record.GetProperty("references").EnumerateArray().Select(r => r.GetProperty("url").GetString() ?? "").Where(EngineeringRules.CiscoUrl).Distinct().Take(100).ToArray();
            bool? kev = null; string kevSource = "Not refreshed";
            try
            {
                using var catalog = await GetAsync("https://www.cisa.gov/sites/default/files/feeds/known_exploited_vulnerabilities.json", 8 * 1024 * 1024, token).ConfigureAwait(false);
                kev = catalog.RootElement.GetProperty("vulnerabilities").EnumerateArray().Any(v => v.GetProperty("cveID").GetString() == cve);
                kevSource = "CISA KEV " + catalog.RootElement.GetProperty("catalogVersion").GetString() + " · retrieved " + DateTimeOffset.UtcNow.ToString("O");
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or KeyNotFoundException or InvalidOperationException) { kevSource = "CISA KEV unavailable; unknown (NVD metadata retained)"; }
            return new(cve, cve, severity, description.Length > 20000 ? description[..20000] : description, "https://nvd.nist.gov/vuln/detail/" + cve, record.GetProperty("lastModified").GetString() ?? "", DateTimeOffset.UtcNow, sources, kev, kevSource);
        }
        finally { _gate.Release(); }
    }
    public void Dispose() { _http.Dispose(); _gate.Dispose(); }
}
