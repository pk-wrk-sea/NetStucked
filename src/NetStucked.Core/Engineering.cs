using System.Text.RegularExpressions;

namespace NetStucked.Core;

public sealed record Asset(Guid Id, string Hostname, string Address, string Vendor, string Model, string Serial,
    string Site, string Location, string Role, string Contract, DateOnly? MaStart, DateOnly? MaEnd,
    DateTimeOffset ModifiedAt)
{
    public string MaStatus => MaEnd is null ? "Unknown" : MaEnd < DateOnly.FromDateTime(DateTime.UtcNow) ? "Expired" : MaEnd <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(90) ? "Expiring ≤90d" : "Active";
}
public sealed record DeviceFact(string Field, string Value, string Command, int Line, string Confidence);
public sealed record NeighborFact(string Name, string Address, string LocalInterface, string RemoteInterface, string Protocol, int Line);
public sealed record EvidenceDocument(Guid Id, Guid? AssetId, string Host, string Source, string Sha256,
    DateTimeOffset CollectedAt, string Parser, string Family, bool Complete,
    DeviceFact[] Facts, NeighborFact[] Neighbors, string[] Warnings);
public sealed record Advisory(string Cve, string Title, string Severity, string Description, string Source,
    string Revision, DateTimeOffset RetrievedAt, string[] CiscoReferences, bool? Kev, string KevSource);
public enum Applicability { InsufficientData, NeedsReview, Affected, NotAffected }
public sealed record AdvisoryReview(Guid Id, Guid AssetId, Guid EvidenceId, string Cve, string AdvisoryRevision,
    Applicability Decision, string ProductVersionEvidence, string ConfigurationEvidence, string VendorUrl,
    string Engineer, string Reason, DateTimeOffset ReviewedAt);
public sealed record UpgradePlan(Guid Id, Guid AssetId, Guid EvidenceId, string Cve, string TargetVersion,
    string FixedEvidenceUrl, string CompatibilityEvidenceUrl, string Owner, string ChangeId, string Wave,
    string WindowUtc, string Notes, string ApprovedBy, DateTimeOffset? ApprovedAt, DateTimeOffset ModifiedAt,
    Guid? AfterEvidenceId = null, Guid? ReviewId = null, string AdvisoryRevision = "");
public sealed record AssetChange(Guid Id, DateTimeOffset Time, string Actor, Asset? Before, Asset? After);
public sealed record EngineeringData(int Schema, long Revision, Asset[] Assets, EvidenceDocument[] Evidence,
    Advisory[] Advisories, AdvisoryReview[] Reviews, UpgradePlan[] Plans, AssetChange[]? Changes = null, Advisory[]? AdvisoryHistory = null)
{
    public static EngineeringData Empty => new(1, 0, [], [], [], [], []);
}
public interface IEngineeringStore
{
    Task<EngineeringData> LoadAsync(CancellationToken token);
    Task<EngineeringData> SaveAsync(EngineeringData data, long expectedRevision, CancellationToken token);
}
public interface IAdvisoryLookup { Task<Advisory> FetchAsync(string cve, CancellationToken token); }

public static partial class EngineeringRules
{
    public static void Validate(Asset a)
    {
        if (a.Id == Guid.Empty || string.IsNullOrWhiteSpace(a.Hostname) && string.IsNullOrWhiteSpace(a.Address)) throw new ArgumentException("Asset needs a stable ID and hostname or address.");
        foreach (string s in new[] { a.Hostname, a.Address, a.Vendor, a.Model, a.Serial, a.Site, a.Location, a.Role, a.Contract }) Text(s, 256);
        if (a.Address.Length > 0 && Uri.CheckHostName(a.Address) == UriHostNameType.Unknown) throw new ArgumentException("Invalid management IP/hostname.");
        if (a.MaStart > a.MaEnd) throw new ArgumentException("MA start must not be after MA end.");
    }
    public static void Text(string s, int limit) { if (s is null || s.Length > limit || s.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'))) throw new ArgumentException("Text is missing, too long or contains control characters."); }
    public static string CveId(string text) => Regex.IsMatch(text, @"^CVE-\d{4}-\d{4,8}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) ? text : throw new ArgumentException("Use CVE-YYYY-NNNN (4–8 sequence digits).");
    public static bool CiscoUrl(string text) => Uri.TryCreate(text, UriKind.Absolute, out var u) && u.Scheme == "https" && u.UserInfo.Length == 0 && u.IsDefaultPort && (u.Host == "cisco.com" || u.Host.EndsWith(".cisco.com", StringComparison.OrdinalIgnoreCase));
    public static void Validate(EngineeringData d)
    {
        if (d.Schema != 1 || d.Revision < 0 || d.Assets is null || d.Evidence is null || d.Advisories is null || d.Reviews is null || d.Plans is null || d.Assets.Length > 10000 || d.Evidence.Length > 1000 || d.Advisories.Length > 2000 || d.Reviews.Length > 5000 || d.Plans.Length > 5000 || d.Changes?.Length > 20000 || d.AdvisoryHistory?.Length > 2000) throw new ArgumentException("Unsupported engineering schema or record limit.");
        foreach (var c in d.Changes ?? []) { Text(c.Actor, 256); if (c.Before is null && c.After is null || c.Before is not null && c.After is not null && c.Before.Id != c.After.Id) throw new ArgumentException("Invalid asset audit entry."); if (c.Before is not null) Validate(c.Before); if (c.After is not null) Validate(c.After); }
        Unique(d.Assets.Select(x => x.Id)); Unique(d.Evidence.Select(x => x.Id)); Unique(d.Reviews.Select(x => x.Id)); Unique(d.Plans.Select(x => x.Id));
        foreach (var a in d.Assets) Validate(a);
        foreach (var e in d.Evidence)
        {
            if (e.CollectedAt > DateTimeOffset.UtcNow.AddMinutes(5)) throw new ArgumentException("Evidence collection time is in the future; verify source clock.");
            if (!Regex.IsMatch(e.Sha256 ?? "", "^[a-f0-9]{64}$") || e.Facts is null || e.Neighbors is null || e.Warnings is null || e.Facts.Length > 4096 || e.Neighbors.Length > 512 || e.Warnings.Length > 100) throw new ArgumentException("Invalid evidence record.");
            Text(e.Host, 253); Text(e.Source, 1024); Text(e.Parser, 80); Text(e.Family, 80);
            if (e.AssetId is { } id && !d.Assets.Any(a => a.Id == id)) throw new ArgumentException("Evidence references a missing asset.");
            foreach (var f in e.Facts) { Text(f.Field, 80); Text(f.Value, 1024); Text(f.Command, 160); Text(f.Confidence, 80); if (f.Line < 1) throw new ArgumentException("Invalid source line."); }
            foreach (var n in e.Neighbors) { foreach (string s in new[] { n.Name, n.Address, n.LocalInterface, n.RemoteInterface, n.Protocol }) Text(s, 256); if (n.Line < 1) throw new ArgumentException("Invalid source line."); }
        }
        if (d.Advisories.Select(a => a.Cve).Distinct().Count() != d.Advisories.Length) throw new ArgumentException("Duplicate advisory ID.");
        foreach (var a in d.Advisories.Concat(d.AdvisoryHistory ?? [])) { CveId(a.Cve); Text(a.Title, 1024); Text(a.Description, 20000); Text(a.Source, 1024); Text(a.Revision, 256); Text(a.Severity, 80); Text(a.KevSource, 1024); if (a.RetrievedAt > DateTimeOffset.UtcNow.AddMinutes(5) || a.CiscoReferences is null || a.CiscoReferences.Length > 100 || a.CiscoReferences.Any(u => !CiscoUrl(u))) throw new ArgumentException("Invalid advisory time/reference."); }
        foreach (var r in d.Reviews)
        {
            CveId(r.Cve); if (!Enum.IsDefined(r.Decision) || !d.Assets.Any(a => a.Id == r.AssetId) || !d.Evidence.Any(e => e.Id == r.EvidenceId && e.AssetId == r.AssetId) || !d.Advisories.Any(a => a.Cve == r.Cve)) throw new ArgumentException("Review requires matching asset, evidence and advisory.");
            foreach (string s in new[] { r.ProductVersionEvidence, r.ConfigurationEvidence, r.Reason }) Text(s, 4000); Text(r.Engineer, 256); Text(r.VendorUrl, 1024); Text(r.AdvisoryRevision, 256);
            if (r.Decision is Applicability.Affected or Applicability.NotAffected && (!CiscoUrl(r.VendorUrl) || new[] { r.Engineer, r.ProductVersionEvidence, r.ConfigurationEvidence, r.Reason }.Any(string.IsNullOrWhiteSpace))) throw new ArgumentException("Affected/Not affected needs explicit engineer review, product/version and configuration evidence, reason and official Cisco source.");
        }
        foreach (var p in d.Plans)
        {
            if (!d.Assets.Any(a => a.Id == p.AssetId) || !d.Evidence.Any(e => e.Id == p.EvidenceId && e.AssetId == p.AssetId) || !d.Advisories.Any(a => a.Cve == p.Cve)) throw new ArgumentException("Plan requires asset, evidence and advisory.");
            if (p.AfterEvidenceId is { } after && !d.Evidence.Any(e => e.Id == after && e.AssetId == p.AssetId)) throw new ArgumentException("Post-change evidence must belong to the same asset.");
            foreach (string s in new[] { p.TargetVersion, p.Owner, p.ChangeId, p.Wave, p.WindowUtc, p.ApprovedBy, p.AdvisoryRevision }) Text(s, 256); Text(p.Notes, 4000); Text(p.FixedEvidenceUrl, 1024); Text(p.CompatibilityEvidenceUrl, 1024);
            if (p.ApprovedAt is not null && (new[] { p.TargetVersion, p.Owner, p.ChangeId, p.Wave, p.ApprovedBy }.Any(string.IsNullOrWhiteSpace) || !DateTimeOffset.TryParse(p.WindowUtc, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var window) || window.Offset != TimeSpan.Zero || !(p.WindowUtc.EndsWith('Z') || p.WindowUtc.EndsWith("+00:00")) || !CiscoUrl(p.FixedEvidenceUrl) || !CiscoUrl(p.CompatibilityEvidenceUrl) || !d.Reviews.Any(r => r.Id == p.ReviewId && r.AssetId == p.AssetId && r.EvidenceId == p.EvidenceId && r.Cve == p.Cve && r.AdvisoryRevision == p.AdvisoryRevision && r.Decision is Applicability.Affected or Applicability.NotAffected))) throw new ArgumentException("Approval requires current engineer assessment, target, owner/change/wave/UTC window, engineer and official fixed/compatibility evidence.");
        }
    }
    private static void Unique(IEnumerable<Guid> ids) { var x = ids.ToArray(); if (x.Contains(Guid.Empty) || x.Distinct().Count() != x.Length) throw new ArgumentException("Invalid/duplicate stable ID."); }
    public static EvidenceDocument? Latest(EngineeringData data, Guid asset) => data.Evidence.Where(e => e.AssetId == asset).OrderByDescending(e => e.CollectedAt).FirstOrDefault();
    public static string Assessment(AdvisoryReview? review, EvidenceDocument? evidence, Advisory? advisory, DateTimeOffset now)
    {
        if (evidence is null || advisory is null) return "Insufficient Data";
        if (now - evidence.CollectedAt > TimeSpan.FromDays(30) || now - advisory.RetrievedAt > TimeSpan.FromDays(7) || review is not null && (review.EvidenceId != evidence.Id || review.AdvisoryRevision != advisory.Revision || now - review.ReviewedAt > TimeSpan.FromDays(7))) return "Stale Assessment";
        if (!evidence.Complete || evidence.Facts.Where(f => f.Field == "Version").Select(f => f.Value).Distinct().Count() != 1 || evidence.Family == "Unknown") return "Insufficient Data";
        return review?.Decision switch { Applicability.Affected => "Affected (engineer reviewed)", Applicability.NotAffected => "Not Affected by evaluated advisory", Applicability.InsufficientData => "Insufficient Data", _ => "Needs Review" };
    }
    public static string PlanStatus(UpgradePlan p, EngineeringData data, DateTimeOffset now)
    {
        var latest = Latest(data, p.AssetId); var advisory = data.Advisories.FirstOrDefault(a => a.Cve == p.Cve);
        var review = data.Reviews.Where(r => r.AssetId == p.AssetId && r.Cve == p.Cve).OrderByDescending(r => r.ReviewedAt).FirstOrDefault();
        if (p.AfterEvidenceId is { } after && latest?.Id == after)
        {
            var versions = latest.Facts.Where(f => f.Field == "Version").Select(f => f.Value).Distinct().ToArray();
            return "Post-change: " + (latest.Complete && latest.Family != "Unknown" && versions.Length == 1 && versions[0] == p.TargetVersion ? "target observed; reassess CVE" : "target mismatch / unknown");
        }
        if (latest?.Id != p.EvidenceId || p.ApprovedAt is not null && (review?.Id != p.ReviewId || advisory?.Revision != p.AdvisoryRevision) || Assessment(review, latest, advisory, now) is "Stale Assessment" or "Insufficient Data" or "Needs Review") return "Needs review / changed evidence";
        return p.ApprovedAt is null ? "Draft" : "Engineer approved plan";
    }
}
