using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace NetStucked.Core;

public enum IpsCoverageState { NotChecked, Found, Pending, NotFoundInSource, VendorCannotCover, Unknown }
public enum IpsVendor { PaloAlto, CheckPoint }
public sealed record CoverageSourceRow(int Number, ImmutableArray<string> Values);
public sealed record CoverageSheet(string Name, ImmutableArray<CoverageSourceRow> Rows);
public sealed record CoverageFile(string Path, string Sha256, DateTimeOffset ImportedAt, ImmutableArray<CoverageSheet> Sheets);
public sealed record CoverageMapping(int Cve, int Address = -1, int Hostname = -1, int Plugin = -1, int PluginName = -1, int Severity = -1);
public sealed record CoverageIssue(int Row, string Value, string Reason);
public sealed record CveAssociation(int Row, string Cve);
public sealed record CoverageInput(Guid Id, string Path, string Sha256, DateTimeOffset ImportedAt, string Sheet, int HeaderRow,
    ImmutableArray<string> Headers, ImmutableArray<CoverageSourceRow> Rows, CoverageMapping Mapping,
    ImmutableArray<CveAssociation> Associations, ImmutableArray<string> Cves, ImmutableArray<CoverageIssue> Issues)
{
    public string Field(CoverageSourceRow row, int column) => column >= 0 && column < row.Values.Length ? row.Values[column] : "";
    public int AssetCount => Rows.Select(r => Field(r, Mapping.Address).Trim() is { Length: > 0 } ip ? ip : Field(r, Mapping.Hostname).Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    public int PluginCount => Rows.Select(r => Field(r, Mapping.Plugin).Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count();
}
public sealed record IpsSignature(string Id, string Name, string Type, string Status, string Release, string ContentVersion, string Limitations, string SnapshotAction = "");
public sealed record IpsCoverageResult(string Cve, IpsVendor Vendor, IpsCoverageState State, string Source, string Method, string SourceId,
    DateTimeOffset? CheckedAt, DateTimeOffset? SourceDate, string Reason, ImmutableArray<IpsSignature> Signatures,
    bool FromCache = false, bool Stale = false, DateTimeOffset? AttemptedAt = null, string Failure = "", IpsCoverageResult? Previous = null)
{
    public string Status => CoverageText.State(State);
    public bool IsIpsFound => State == IpsCoverageState.Found;
    public static IpsCoverageResult Unchecked(string cve, IpsVendor vendor) => new(cve, vendor, IpsCoverageState.NotChecked, "", "", "", null, null, "Not checked in this run.", []);
}
public sealed record CoverageRun(Guid Id, CoverageInput Input, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt,
    ImmutableArray<IpsCoverageResult> Results, bool Cancelled = false)
{
    public bool Partial => FinishedAt is null || Cancelled || Results.Any(r => r.State == IpsCoverageState.NotChecked);
    public int CompletedJobs => Results.Count(r => r.State != IpsCoverageState.NotChecked);
    public int TotalJobs => Input.Cves.Length * 2;
}
public interface IIpsCoverageProvider
{
    IpsVendor Vendor { get; }
    string CacheIdentity { get; }
    IpsCoverageResult Pending(string cve) => IpsCoverageResult.Unchecked(cve, Vendor);
    Task<IpsCoverageResult> LookupAsync(string cve, CancellationToken token);
}
public interface IIpsCoverageCache
{
    Task<IpsCoverageResult?> ReadAsync(string identity, string cve, CancellationToken token);
    Task WriteAsync(string identity, IpsCoverageResult result, CancellationToken token);
}
public interface IIpsProviderFactory { IIpsCoverageProvider PaloAlto(string key); }
public static partial class CoverageText
{
    public static string State(IpsCoverageState s) => s switch { IpsCoverageState.Found => "Found", IpsCoverageState.Pending => "Pending", IpsCoverageState.NotFoundInSource => "Not found in source", IpsCoverageState.VendorCannotCover => "Vendor states cannot cover", IpsCoverageState.Unknown => "Unknown", _ => "Not checked" };
    public static string Vendor(IpsVendor v) => v == IpsVendor.PaloAlto ? "Palo Alto" : "Check Point";
    [GeneratedRegex(@"^CVE-[0-9]{4}-[0-9]{4,}$", RegexOptions.CultureInvariant)] private static partial Regex CvePattern();
    [GeneratedRegex(@"[,;\s]+", RegexOptions.CultureInvariant)] private static partial Regex Separators();
    public static bool ValidCve(string text) => text.Length <= 64 && CvePattern().IsMatch(text);
    public static (ImmutableArray<string> Cves, ImmutableArray<string> Invalid) Parse(string text)
    {
        var parts = Separators().Split(text.Trim().ToUpperInvariant()).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        return (parts.Where(ValidCve).ToImmutableArray(), parts.Where(x => !ValidCve(x)).ToImmutableArray());
    }
    public static string HeaderKey(string text) => string.Concat(text.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    public static int FindColumn(IReadOnlyList<string> headers, params string[] aliases) => Enumerable.Range(0, headers.Count).FirstOrDefault(i => aliases.Select(HeaderKey).Contains(HeaderKey(headers[i])), -1);
    public static CoverageMapping AutoMap(IReadOnlyList<string> h) => new(FindColumn(h, "CVE", "CVEs", "CVE ID", "CVE IDs", "CVE Identifier", "Industry Reference"),
        FindColumn(h, "IP Address", "IP", "Address", "Host IP"), FindColumn(h, "Hostname", "Host Name", "DNS Name", "NetBIOS Name"),
        FindColumn(h, "Plugin", "Plugin ID", "PluginID"), FindColumn(h, "Plugin Name", "Finding", "Vulnerability Name"), FindColumn(h, "Severity", "Risk", "Risk Factor"));
    public static CoverageInput CreateInput(CoverageFile file, string sheet, int headerRow, CoverageMapping mapping)
    {
        var s = file.Sheets.Single(x => x.Name == sheet); var header = s.Rows.SingleOrDefault(r => r.Number == headerRow) ?? throw new ArgumentException("Header row not found in selected sheet.");
        if (mapping.Cve < 0 || mapping.Cve >= header.Values.Length) throw new ArgumentException("Select a CVE column. Plugin ID to CVE lookup is not supported.");
        var rows = s.Rows.Where(r => r.Number > headerRow && r.Values.Any(v => !string.IsNullOrWhiteSpace(v))).ToImmutableArray();
        if (rows.Any(r => r.Values.Length > header.Values.Length)) throw new ArgumentException("Some source values have no header. Select the correct header row; no columns were discarded.");
        var associations = ImmutableArray.CreateBuilder<CveAssociation>(); var issues = ImmutableArray.CreateBuilder<CoverageIssue>();
        foreach (var r in rows)
        {
            string raw = mapping.Cve < r.Values.Length ? r.Values[mapping.Cve] : ""; var parsed = Parse(raw);
            if (parsed.Cves.Length == 0 && parsed.Invalid.Length == 0) issues.Add(new(r.Number, raw, "CVE is empty."));
            foreach (var bad in parsed.Invalid) issues.Add(new(r.Number, bad, "Invalid CVE format; expected CVE-YYYY-NNNN..."));
            associations.AddRange(parsed.Cves.Select(c => new CveAssociation(r.Number, c)));
            if (associations.Count > 200000) throw new ArgumentException("Report exceeds 200,000 row-to-CVE associations. Split the source report.");
        }
        var links = associations.ToImmutable(); var cves = links.Select(x => x.Cve).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray();
        if (cves.Length > 2000) throw new ArgumentException("Limit is 2,000 unique CVE per run. Split the report to respect provider quotas.");
        return new(Guid.NewGuid(), file.Path, file.Sha256, file.ImportedAt, sheet, headerRow, header.Values, rows, mapping, links, cves, issues.ToImmutable());
    }
}

/// <summary>One logical lookup per unique CVE per provider, preserving immutable run snapshots.</summary>
public sealed class IpsCoverageCoordinator(IIpsCoverageCache cache, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public async Task<CoverageRun> RunAsync(CoverageInput input, IReadOnlyList<IIpsCoverageProvider> providers, bool refresh,
        IProgress<CoverageRun>? progress, CancellationToken token, CoverageRun? retry = null)
    {
        if (providers.Count != 2 || providers.Select(p => p.Vendor).Distinct().Count() != 2) throw new ArgumentException("Provide one adapter for each vendor (unconfigured adapters return Unknown).");
        var results = input.Cves.SelectMany(c => Enum.GetValues<IpsVendor>().Select(v => providers.Single(p => p.Vendor == v).Pending(c))).ToArray();
        if (retry is not null && retry.Input.Id == input.Id)
            for (int i = 0; i < results.Length; i++) results[i] = retry.Results.Single(r => r.Cve == results[i].Cve && r.Vendor == results[i].Vendor);
        var run = new CoverageRun(Guid.NewGuid(), input, _clock.GetUtcNow(), null, results.ToImmutableArray());
        long last = 0;
        try
        {
            for (int i = 0; i < results.Length; i++)
            {
                token.ThrowIfCancellationRequested(); var item = results[i];
                if (retry is not null && item.State is not (IpsCoverageState.Unknown or IpsCoverageState.NotChecked)) continue;
                var provider = providers.Single(p => p.Vendor == item.Vendor);
                IpsCoverageResult? old = await cache.ReadAsync(provider.CacheIdentity, item.Cve, token).ConfigureAwait(false);
                var now = _clock.GetUtcNow();
                if (old is not null && (old.Vendor != item.Vendor || old.Cve != item.Cve)) old = null;
                if (!refresh && old?.CheckedAt is { } checkedAt && checkedAt <= now && now - checkedAt < TimeSpan.FromHours(24))
                    results[i] = old with { FromCache = true, Stale = old.Stale || old.SourceDate is { } sd && now - sd > TimeSpan.FromDays(7) };
                else
                {
                    var fresh = await provider.LookupAsync(item.Cve, token).ConfigureAwait(false);
                    if (fresh.Vendor != item.Vendor || fresh.Cve != item.Cve) throw new InvalidOperationException("Provider returned a mismatched CVE/vendor.");
                    if (fresh.State == IpsCoverageState.Unknown && old is not null)
                        fresh = fresh with { Previous = old with { Previous = null, Stale = true, FromCache = true }, AttemptedAt = now };
                    else if (fresh.State is not (IpsCoverageState.Unknown or IpsCoverageState.NotChecked))
                    {
                        try { await cache.WriteAsync(provider.CacheIdentity, fresh, token).ConfigureAwait(false); }
                        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException) { fresh = fresh with { Reason = fresh.Reason + " Cache could not be saved; this is a fresh source result." }; }
                    }
                    results[i] = fresh;
                }
                long tick = Environment.TickCount64;
                if (tick - last >= 80) { last = tick; progress?.Report(run with { Results = results.ToImmutableArray() }); }
            }
            run = run with { Results = results.ToImmutableArray(), FinishedAt = _clock.GetUtcNow() };
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { run = run with { Results = results.ToImmutableArray(), FinishedAt = _clock.GetUtcNow(), Cancelled = true }; }
        progress?.Report(run); return run;
    }
}
