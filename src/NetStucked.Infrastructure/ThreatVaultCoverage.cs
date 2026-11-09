using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed class ThreatVaultProviderFactory : IIpsProviderFactory, IDisposable
{
    private readonly HttpClient _client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
    public IIpsCoverageProvider PaloAlto(string key) => new ThreatVaultCoverageProvider(_client, key);
    public void Dispose() => _client.Dispose();
}

/// <summary>Fixed HTTPS metadata endpoint; never sends asset identities or source report contents.</summary>
public sealed class ThreatVaultCoverageProvider(HttpClient client, string key, TimeProvider? clock = null, TimeSpan? minimumInterval = null) : IIpsCoverageProvider
{
    public const string Endpoint = "https://api.threatvault.paloaltonetworks.com/service/v1/threats/cve-coverage";
    public IpsVendor Vendor => IpsVendor.PaloAlto;
    public IpsCoverageResult Pending(string cve) => IpsCoverageResult.Unchecked(cve, Vendor) with { Source = "Palo Alto Threat Vault", Method = "CVE coverage API", SourceId = "threat-vault-cve-v1" };
    // Separate accounts/keys cannot silently share successful cached lookups.
    public string CacheIdentity { get; } = "threat-vault-cve-v1-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _nextRequest, _rateReset;
    private string? _authFailure;
    private int _requests;
    private IpsCoverageResult Error(string cve, string reason, string failure) => new(cve, Vendor, IpsCoverageState.Unknown, "Palo Alto Threat Vault", "CVE coverage API", "threat-vault-cve-v1", _clock.GetUtcNow(), null, reason, [], AttemptedAt: _clock.GetUtcNow(), Failure: failure);
    public async Task<IpsCoverageResult> LookupAsync(string cve, CancellationToken token)
    {
        if (!CoverageText.ValidCve(cve)) throw new ArgumentException("Invalid CVE.");
        if (string.IsNullOrWhiteSpace(key) || key.Length > 2048 || key.Any(char.IsControl)) return Error(cve, "Configure a valid Threat Vault API key. No request sent.", "Configuration");
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_authFailure is not null) return Error(cve, _authFailure, "Authorization");
            for (int attempt = 0; attempt < 3; attempt++)
            {
                token.ThrowIfCancellationRequested();
                if (_requests >= 2000) return Error(cve, "Per-run 2,000 request ceiling reached (including retries).", "RateLimit");
                var now = _clock.GetUtcNow(); var pause = (_nextRequest > _rateReset ? _nextRequest : _rateReset) - now;
                if (pause > TimeSpan.FromSeconds(10)) return Error(cve, "Threat Vault quota exhausted. Retry after the provider reset; no further requests sent this run.", "RateLimit");
                if (pause > TimeSpan.Zero) await Task.Delay(pause, _clock, token).ConfigureAwait(false);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(15));
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint + "?cve_id=" + Uri.EscapeDataString(cve));
                    request.Headers.Add("X-API-KEY", key); request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    _requests++; _nextRequest = _clock.GetUtcNow() + (minimumInterval ?? TimeSpan.FromMilliseconds(350));
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                    Quota(response);
                    if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    {
                        _authFailure = "Threat Vault rejected authentication/authorization. Check API key and subscription permissions; no automatic auth retry.";
                        return Error(cve, _authFailure, "Authorization");
                    }
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        var delay = RetryDelay(response, attempt); _rateReset = Max(_rateReset, _clock.GetUtcNow() + delay);
                        if (attempt < 2 && delay <= TimeSpan.FromSeconds(10)) continue;
                        return Error(cve, "Threat Vault HTTP 429 / quota limit; bounded retries exhausted or reset is too far away.", "RateLimit");
                    }
                    if ((int)response.StatusCode >= 500 && attempt < 2) { await Task.Delay(TimeSpan.FromMilliseconds(500 * (attempt + 1)), _clock, token).ConfigureAwait(false); continue; }
                    if (!response.IsSuccessStatusCode) return Error(cve, $"Threat Vault HTTP {(int)response.StatusCode}. This is not a signature miss.", "Http");
                    if (response.Content.Headers.ContentLength > 2 * 1024 * 1024) return Error(cve, "Threat Vault response exceeds 2 MiB limit.", "Schema");
                    await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                    using var memory = new MemoryStream(); var buffer = new byte[16384]; int length;
                    while ((length = await stream.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) > 0) { if (memory.Length + length > 2 * 1024 * 1024) return Error(cve, "Threat Vault response exceeds 2 MiB limit.", "Schema"); memory.Write(buffer, 0, length); }
                    string json = Encoding.UTF8.GetString(memory.ToArray()).Replace(key, "[redacted]", StringComparison.Ordinal);
                    return Parse(cve, json, _clock.GetUtcNow());
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { if (attempt == 2) return Error(cve, "Threat Vault response deadline exceeded.", "Timeout"); }
                catch (HttpRequestException) { if (attempt == 2) return Error(cve, "Threat Vault transport failed. No signature verdict available.", "Transport"); }
                catch (IOException) { if (attempt == 2) return Error(cve, "Threat Vault response could not be read.", "Transport"); }
            }
            return Error(cve, "Threat Vault request failed after bounded retries.", "Transport");
        }
        finally { _gate.Release(); }
    }
    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
    private void Quota(HttpResponseMessage response)
    {
        foreach (string window in new[] { "Minute", "Day" })
            if (Header(response, $"X-{window}-RateLimit-Remaining") == "0" && long.TryParse(Header(response, $"X-{window}-RateLimit-Reset"), out long epoch) && epoch is > 0 and < 253402300800)
                _rateReset = Max(_rateReset, DateTimeOffset.FromUnixTimeSeconds(epoch));
    }
    private static string Header(HttpResponseMessage r, string name) => r.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() ?? "" : "";
    private TimeSpan RetryDelay(HttpResponseMessage r, int attempt)
    {
        if (r.Headers.RetryAfter?.Delta is { } delta) return delta > TimeSpan.Zero ? delta : TimeSpan.FromSeconds(1);
        if (r.Headers.RetryAfter?.Date is { } date) return date > _clock.GetUtcNow() ? date - _clock.GetUtcNow() : TimeSpan.FromSeconds(1);
        if (_rateReset > _clock.GetUtcNow()) return _rateReset - _clock.GetUtcNow();
        return TimeSpan.FromSeconds(attempt + 1);
    }
    public static IpsCoverageResult Parse(string requestedCve, string json, DateTimeOffset checkedAt)
    {
        IpsCoverageResult Result(IpsCoverageState state, string reason, ImmutableArray<IpsSignature> signatures = default, string failure = "") => new(requestedCve, IpsVendor.PaloAlto, state, "Palo Alto Threat Vault", "CVE coverage API", "threat-vault-cve-v1", checkedAt, null, reason, signatures.IsDefault ? [] : signatures, Failure: failure);
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 }); var root = document.RootElement;
            if (!root.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True || !root.TryGetProperty("data", out var data)) return Result(IpsCoverageState.Unknown, "Threat Vault response was unsuccessful or incomplete.", failure: "Schema");
            if (root.TryGetProperty("link", out var link) && link.ValueKind == JsonValueKind.Object && link.TryGetProperty("next", out var next) && next.ValueKind is not (JsonValueKind.Null or JsonValueKind.False) && next.ToString().Length > 0) return Result(IpsCoverageState.Unknown, "Threat Vault returned a paginated/incomplete CVE result; review required.", failure: "Schema");
            var entries = data.ValueKind switch { JsonValueKind.Object => new[] { data }, JsonValueKind.Array => data.EnumerateArray().ToArray(), _ => [] };
            if (entries.Length == 0 && data.ValueKind == JsonValueKind.Array && root.TryGetProperty("count", out var count) && count.TryGetInt32(out int n) && n == 0) return Result(IpsCoverageState.NotFoundInSource, "No CVE entry found in the queried Threat Vault source.");
            var matches = entries.Where(x => x.ValueKind == JsonValueKind.Object && Text(x, "cve_id").Equals(requestedCve, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1) return Result(IpsCoverageState.Unknown, "Missing, duplicated or mismatched requested CVE in Threat Vault response.", failure: "Schema");
            var item = matches[0]; string status = Text(item, "cve_status"); var signatures = ImmutableArray.CreateBuilder<IpsSignature>();
            foreach (var (field, type) in new[] { ("vulnerability", "Vulnerability Protection"), ("antivirus", "Antivirus (not IPS)") })
            {
                if (!item.TryGetProperty(field, out var array)) continue;
                if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 1000) return Result(IpsCoverageState.Unknown, "Invalid signature array or signature count exceeds limit.", failure: "Schema");
                foreach (var s in array.EnumerateArray())
                {
                    if (Text(s, "id").Length == 0 || Text(s, "name").Length == 0) return Result(IpsCoverageState.Unknown, "Incomplete signature identity in response.", failure: "Schema");
                    string release = s.TryGetProperty("release", out var rel) ? rel.GetRawText() : "";
                    if (release.Length == 0)
                    {
                        var releaseFields = new[] { "ori_release_version", "latest_release_version", "ori_release_time", "latest_release_time" }.Select(k => (Key: k, Value: Text(s, k))).Where(x => x.Value.Length > 0).ToDictionary(x => x.Key, x => x.Value);
                        if (releaseFields.Count > 0) release = JsonSerializer.Serialize(releaseFields);
                    }
                    string limits = string.Join("; ", new[] { "min_version", "max_version", "description", "details", "default_action", "reference" }.Select(k => k + ": " + Text(s, k)).Where(x => !x.EndsWith(": ", StringComparison.Ordinal)));
                    signatures.Add(new(Text(s, "id"), Text(s, "name"), type, Text(s, "status"), release, Text(s, "latest_release_version"), limits));
                }
            }
            var all = signatures.ToImmutable(); string vendorReason = string.Join("; ", new[] { status, Text(item, "not_cover_reason"), Text(item, "response_text"), Text(item, "release_week") }.Where(x => x.Length > 0));
            if (all.Any(s => s.Type == "Vulnerability Protection" && s.Status is "released" or "active")) return Result(IpsCoverageState.Found, "Found released Vulnerability Protection signature(s) in Threat Vault. Vendor default action is not a firewall action. " + vendorReason, all);
            if (status == "cve_cannot_cover") return Result(IpsCoverageState.VendorCannotCover, "Vendor explicitly states signature coverage limitations: " + vendorReason, all);
            if (status is "cve_under_testing" or "cve_in_development" or "cve_under_development" || all.Any(s => s.Type == "Vulnerability Protection" && s.Status is "under_testing" or "in_development")) return Result(IpsCoverageState.Pending, "Vendor reports development/testing: " + vendorReason, all);
            if (status == "cve_covered" && all.Any(s => s.Type == "Antivirus (not IPS)") && !all.Any(s => s.Type == "Vulnerability Protection")) return Result(IpsCoverageState.NotFoundInSource, "Antivirus signatures found; no released Vulnerability Protection signature in this response. Antivirus is excluded from IPS Found.", all);
            if (status is "cve_not_covered" or "cve_not_found") return Result(IpsCoverageState.NotFoundInSource, "No IPS signature found in the queried source. " + vendorReason, all);
            return Result(IpsCoverageState.Unknown, "No confirmed released IPS signature; vendor status/incomplete response requires review. " + vendorReason, all, "Schema");
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException) { return Result(IpsCoverageState.Unknown, "Threat Vault returned an invalid or incomplete response. No signature verdict inferred.", failure: "Schema"); }
    }
    private static string Text(JsonElement element, string key)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null) return "";
        string text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.GetRawText();
        if (text.Length > 16000) throw new FormatException("Metadata field exceeds limit."); return text;
    }
}
