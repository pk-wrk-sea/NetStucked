using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed class IpsCoverageCache(string directory) : IIpsCoverageCache
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static string Key(string identity, string cve) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity + "\n" + cve))) + ".json";
    public async Task<IpsCoverageResult?> ReadAsync(string identity, string cve, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            string path = Path.Combine(directory, Key(identity, cve));
            if (!File.Exists(path) || new FileInfo(path).Length > 512 * 1024) return null;
            var data = JsonSerializer.Deserialize<CacheEntry>(await File.ReadAllTextAsync(path, token).ConfigureAwait(false));
            if (data is not { Schema: 1, Result: { } result } || data.Identity != identity || result.Cve != cve || !Enum.IsDefined(result.Vendor) || !Enum.IsDefined(result.State) || result.State is IpsCoverageState.Unknown or IpsCoverageState.NotChecked || result.Signatures.IsDefault || result.Signatures.Length > 1000 || result.CheckedAt is null || result.Source is null || result.Method is null || result.SourceId is null || result.Reason is null || result.Signatures.Any(s => s is null || s.Id is null || s.Name is null || s.Type is null || s.Status is null || s.Release is null || s.ContentVersion is null || s.Limitations is null || s.SnapshotAction is null)) return null;
            return result with { Previous = null };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException) { return null; }
        finally { _gate.Release(); }
    }
    public async Task WriteAsync(string identity, IpsCoverageResult result, CancellationToken token)
    {
        if (result.State is IpsCoverageState.Unknown or IpsCoverageState.NotChecked) return;
        await _gate.WaitAsync(token).ConfigureAwait(false);
        string? temp = null;
        try
        {
            Directory.CreateDirectory(directory); string path = Path.Combine(directory, Key(identity, result.Cve)); temp = path + ".tmp";
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(new CacheEntry(1, identity, result with { Previous = null, FromCache = false }));
            if (json.Length > 512 * 1024) return;
            await File.WriteAllBytesAsync(temp, json, token).ConfigureAwait(false); token.ThrowIfCancellationRequested(); File.Move(temp, path, true);
            var files = new DirectoryInfo(directory).EnumerateFiles("*.json").OrderByDescending(f => f.LastWriteTimeUtc).ToArray(); long size = 0;
            foreach (var (file, i) in files.Select((f, i) => (f, i))) { size += file.Length; if (i >= 4096 || size > 128 * 1024 * 1024) file.Delete(); }
        }
        finally { if (temp is not null && File.Exists(temp)) File.Delete(temp); _gate.Release(); }
    }
    private sealed record CacheEntry(int Schema, string Identity, IpsCoverageResult Result);
}

public sealed class UnavailableIpsProvider(IpsVendor vendor, string reason) : IIpsCoverageProvider
{
    public IpsVendor Vendor => vendor;
    public string CacheIdentity => "unconfigured-" + vendor;
    public IpsCoverageResult Pending(string cve) => IpsCoverageResult.Unchecked(cve, vendor) with { Source = "Not configured / not selected", Method = "None", Reason = "Not checked. " + reason };
    public Task<IpsCoverageResult> LookupAsync(string cve, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); return Task.FromResult(new IpsCoverageResult(cve, vendor, IpsCoverageState.Unknown, "Not configured / not selected", "None", CacheIdentity, DateTimeOffset.UtcNow, null, reason, [], Failure: "Configuration"));
    }
}
public sealed record CheckPointSnapshot(string FileName, string Identity, DateTimeOffset ImportedAt, DateTimeOffset? SourceDate, string Package,
    ImmutableDictionary<string, ImmutableArray<IpsSignature>> ByCve, ImmutableArray<string> Warnings)
{
    public static CheckPointSnapshot Create(CoverageFile file, int headerRow, int cveColumn, int nameColumn, int idColumn, int actionColumn, string package, DateTimeOffset? sourceDate)
    {
        var sheet = file.Sheets[0]; var header = sheet.Rows.SingleOrDefault(r => r.Number == headerRow) ?? throw new ArgumentException("Snapshot header row not found.");
        if (cveColumn < 0 || nameColumn < 0 || cveColumn >= header.Values.Length || nameColumn >= header.Values.Length) throw new ArgumentException("Map Industry Reference/CVE and Protection Name. Protection ID is optional.");
        if (package.Length > 256) throw new ArgumentException("Package/version must be at most 256 characters.");
        var map = new Dictionary<string, List<IpsSignature>>(StringComparer.Ordinal); int missing = 0;
        foreach (var row in sheet.Rows.Where(r => r.Number > headerRow))
        {
            string Field(int c) => c >= 0 && c < row.Values.Length ? row.Values[c].Trim() : "";
            if (row.Values.All(string.IsNullOrWhiteSpace)) continue;
            string reference = Field(cveColumn), name = Field(nameColumn);
            var cves = Regex.Matches(reference, @"\bCVE-[0-9]{4}-[0-9]{4,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Select(m => m.Value.ToUpperInvariant()).Where(CoverageText.ValidCve).Distinct().ToArray();
            if (name.Length == 0 || cves.Length == 0) { missing++; continue; }
            var signature = new IpsSignature(Field(idColumn), name, "IPS", "Imported snapshot", package, package, $"Source CSV row {row.Number}; Industry Reference: {reference}", Field(actionColumn));
            foreach (string cve in cves) { if (!map.TryGetValue(cve, out var list)) map[cve] = list = []; if (list.Count >= 1000) throw new ArgumentException("More than 1,000 protections for one CVE; split the snapshot."); list.Add(signature); }
        }
        string metadata = $"{file.Sha256}|{headerRow}|{cveColumn}|{nameColumn}|{idColumn}|{actionColumn}|{package}|{sourceDate:O}";
        string identity = "checkpoint-csv-v1-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(metadata)));
        var warnings = ImmutableArray.CreateBuilder<string>(); warnings.Add("CSV contains only columns visible at SmartConsole export. No online Check Point lookup was performed.");
        warnings.Add($"CSV SHA-256: {file.Sha256}; imported {file.ImportedAt:O}; header record {headerRow}; mapped columns CVE={cveColumn + 1}, name={nameColumn + 1}, ID={idColumn + 1}, action={actionColumn + 1} (0 means not supplied).");
        if (idColumn < 0) warnings.Add("Protection ID not supplied; name + CVE matches are retained.");
        if (sourceDate is null) warnings.Add("Source/package date unknown; import time is not the source date.");
        else warnings.Add("Source/package date is user supplied, not independently verified against the export.");
        if (package.Length > 0) warnings.Add("Package/version is user supplied.");
        if (missing > 0) warnings.Add($"{missing} rows lacked a CVE reference or Protection Name and could not be indexed.");
        return new(Path.GetFileName(file.Path), identity, file.ImportedAt, sourceDate, package, map.ToImmutableDictionary(x => x.Key, x => x.Value.Distinct().ToImmutableArray(), StringComparer.Ordinal), warnings.ToImmutable());
    }
}
public sealed class CheckPointCoverageProvider(CheckPointSnapshot snapshot, TimeProvider? clock = null) : IIpsCoverageProvider
{
    public IpsVendor Vendor => IpsVendor.CheckPoint;
    public string CacheIdentity => snapshot.Identity;
    public IpsCoverageResult Pending(string cve) => IpsCoverageResult.Unchecked(cve, Vendor) with { Source = snapshot.FileName, Method = "SmartConsole IPS CSV (local)", SourceId = snapshot.Identity, SourceDate = snapshot.SourceDate, Reason = "Not checked in this run. " + string.Join(" ", snapshot.Warnings), Stale = snapshot.SourceDate is null || (clock ?? TimeProvider.System).GetUtcNow() - snapshot.SourceDate > TimeSpan.FromDays(7) };
    public Task<IpsCoverageResult> LookupAsync(string cve, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var signatures = snapshot.ByCve.GetValueOrDefault(cve, []); var now = (clock ?? TimeProvider.System).GetUtcNow();
        return Task.FromResult(new IpsCoverageResult(cve, Vendor, signatures.Length > 0 ? IpsCoverageState.Found : IpsCoverageState.NotFoundInSource,
            snapshot.FileName, "SmartConsole IPS CSV (local)", snapshot.Identity, now, snapshot.SourceDate,
            (signatures.Length > 0 ? "Found IPS protections in this imported snapshot. Profile actions describe the snapshot only." : "Not found in the imported database (ไม่พบในฐานที่นำเข้า). This is not a statement about the full Check Point catalog.") + " " + string.Join(" ", snapshot.Warnings),
            signatures, Stale: snapshot.SourceDate is null || now - snapshot.SourceDate > TimeSpan.FromDays(7)));
    }
}
