using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;
public sealed class EngineeringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NetStucked-owned-engineering-" + Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static Asset Asset(string name = "SW1", string serial = "") => new(Guid.NewGuid(), name, "192.0.2.1", "Cisco", "", serial, "Lab", "", "Switch", "", null, null, Now);
    private static EvidenceDocument Evidence(Asset a, string host = "SW1", bool complete = true) => CiscoEvidenceParser.Parse("--- show version ---\nCisco IOS XE Software, Version 17.9.4a\nSW1 uptime is 1 day\nModel Number : C9300-24T\nSystem serial number : LAB001\n--- show cdp neighbors detail ---\nDevice ID: SW2\nIP address: 192.0.2.2\nInterface: GigabitEthernet1/0/1, Port ID (outgoing port): GigabitEthernet1/0/2\n", "owned-fixture", new string('a', 64), host, Now, complete, a.Id);
    private static Advisory Advisory() => new("CVE-2023-20198", "Owned fixture metadata", "CRITICAL", "Fixture description; not a production determination", "https://nvd.nist.gov/vuln/detail/CVE-2023-20198", "fixture-1", Now, ["https://sec.cloudapps.cisco.com/security/center/content/CiscoSecurityAdvisory/example"], null, "Unknown");
    public EngineeringTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    [Fact] public void ParserPreservesSourcesAndDoesNotGuessUnknownFamily()
    {
        var a = Asset(); var e = Evidence(a);
        Assert.Equal("IOS XE", e.Family); var version = Assert.Single(e.Facts, f => f.Field == "Version"); Assert.Equal("17.9.4a", version.Value); Assert.Equal("show version", version.Command); Assert.Equal(2, version.Line);
        Assert.Equal("GigabitEthernet1/0/1", Assert.Single(e.Neighbors).LocalInterface);
        var unknown = CiscoEvidenceParser.Parse("Version 1.2.3\ninterface Uplink\n", "unknown", new string('b', 64), "unknown", Now, true, null);
        Assert.Equal("Unknown", unknown.Family); Assert.DoesNotContain(unknown.Facts, f => f.Field == "Version"); Assert.Empty(unknown.Neighbors);
    }
    [Theory] [InlineData("Cisco IOS Software, C3750 Software, Version 12.2(55)SE12", "IOS", "12.2(55)SE12")]
    [InlineData("Cisco Adaptive Security Appliance Software Version 9.18(4)\n", "ASA", "9.18(4)")]
    [InlineData("Cisco Nexus Operating System (NX-OS) Software\nNXOS: version 10.3(4a)", "NX-OS", "10.3(4a)")]
    public void ParserRecognizesOnlyExplicitPlatformVersion(string text, string family, string version)
    { var e = CiscoEvidenceParser.Parse(text, "owned", new string('a', 64), "lab", Now, true, null); Assert.Equal(family, e.Family); Assert.Equal(version, Assert.Single(e.Facts, f => f.Field == "Version").Value); }
    [Fact] public void TopologyRequiresReciprocalFreshUnambiguousEndpoints()
    {
        var a = Asset(); var b = Asset("SW2"); var one = Evidence(a);
        var two = Evidence(b, "SW2") with { Neighbors = [new("SW1", "192.0.2.1", "GigabitEthernet1/0/2", "GigabitEthernet1/0/1", "CDP", 1)] };
        Assert.StartsWith("Discovered", Assert.Single(TopologyProjection.Build(one, [one], Now)).Classification);
        Assert.StartsWith("Verified", Assert.Single(TopologyProjection.Build(one, [one, two], Now)).Classification);
        Assert.DoesNotContain("Verified", Assert.Single(TopologyProjection.Build(one, [one, two], Now.AddDays(2))).Classification);
        Assert.StartsWith("Needs review", Assert.Single(TopologyProjection.Build(one, [one, two, two with { Id = Guid.NewGuid() }], Now)).Classification);
        Assert.Contains("No uplink direction inferred", Assert.Single(TopologyProjection.Build(one, [one], Now)).Detail);
    }
    [Fact] public void CveNeverClassifiesFromVersionAloneAndInvalidatesOldReview()
    {
        var a = Asset(); var e = Evidence(a); var advisory = Advisory();
        Assert.Equal("Needs Review", EngineeringRules.Assessment(null, e, advisory, Now));
        Assert.Equal("Insufficient Data", EngineeringRules.Assessment(null, e with { Complete = false }, advisory, Now));
        var r = new AdvisoryReview(Guid.NewGuid(), a.Id, e.Id, advisory.Cve, advisory.Revision, Applicability.Affected, "product matched", "feature reviewed", advisory.CiscoReferences[0], "Lab engineer", "Owned fixture review", Now);
        Assert.StartsWith("Affected", EngineeringRules.Assessment(r, e, advisory, Now));
        Assert.Equal("Stale Assessment", EngineeringRules.Assessment(r, e with { Id = Guid.NewGuid() }, advisory, Now));
        Assert.Equal("Stale Assessment", EngineeringRules.Assessment(r, e, advisory with { Revision = "changed" }, Now));
        Assert.Equal("Stale Assessment", EngineeringRules.Assessment(r, e, advisory, Now.AddDays(8)));
        Assert.Equal("Insufficient Data", EngineeringRules.Assessment(null, e with { Facts = e.Facts.Append(new("Version", "conflict", "show version", 10, "Observed")).ToArray() }, advisory, Now));
    }
    [Theory] [InlineData("https://cisco.com.evil.invalid/a")] [InlineData("http://www.cisco.com/a")] [InlineData("https://user@cisco.com/a")] [InlineData("https://cisco.com:8443/a")]
    public void VendorEvidenceRejectsLookalikeOrInsecureUrls(string u) => Assert.False(EngineeringRules.CiscoUrl(u));
    [Fact] public async Task StoreIsAtomicRevisionCheckedAuditedAndRejectsCorruption()
    {
        var store = new EngineeringStore(new UserSettingsStore(_root)); var a = Asset();
        var first = await store.SaveAsync(EngineeringData.Empty with { Assets = [a] }, 0, default);
        Assert.Equal(1, first.Revision); Assert.Null(Assert.Single(first.Changes!).Before);
        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(first with { Assets = [] }, 0, default));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(first with { Assets = [] }, 1, cancelled.Token));
        Assert.Single((await store.LoadAsync(default)).Assets);
        var second = await store.SaveAsync(first with { Assets = [a with { Site = "changed" }] }, 1, default); Assert.Equal("Lab", second.Changes![1].Before!.Site);
        string file = Path.Combine(_root, "engineering", "workspace-v1.json"); await File.WriteAllTextAsync(file, "{broken");
        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(second, 2, default)); Assert.Equal("{broken", await File.ReadAllTextAsync(file));
    }
    [Fact] public async Task ExcelRoundTripPreservesIdentityTextUnicodeAndPreviewDoesNotCommit()
    {
        string path = Path.Combine(_root, "owned.xlsx"); var a = Asset("=SUM(1,2) ไทย", "LAB001") with { MaEnd = new DateOnly(2027, 2, 28), Contract = "+untrusted" };
        await InventoryWorkbook.WriteAsync(path, [a], default); var rows = await InventoryWorkbook.ReadAsync(path, [a], default);
        var row = Assert.Single(rows); Assert.Empty(row.Error); Assert.Equal(a.Id, row.Asset!.Id); Assert.Equal(a.Hostname, row.Asset.Hostname); Assert.Equal(a.MaEnd, row.Asset.MaEnd); Assert.StartsWith("Update", row.Action);
        using var zip = ZipFile.OpenRead(path); using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()); string xml = await reader.ReadToEndAsync(); Assert.Contains("inlineStr", xml); Assert.DoesNotContain("<f>", xml);
    }
    [Fact] public async Task AdvisoryRefreshPreservesPreviousMetadataRevision()
    {
        var store = new EngineeringStore(new UserSettingsStore(_root)); var old = Advisory();
        var first = await store.SaveAsync(EngineeringData.Empty with { Advisories = [old] }, 0, default);
        var next = await store.SaveAsync(first with { Advisories = [old with { Revision = "new-revision", Description = "Revised conditions" }] }, 1, default);
        Assert.Equal(old.Description, Assert.Single(next.AdvisoryHistory!).Description); Assert.True(File.Exists(Path.Combine(_root, "engineering", "workspace-v1.previous.json")));
    }
    [Fact] public async Task ExcelRejectsFormulasDuplicatesWrongSchemaAndConflictingSerial()
    {
        string path = Path.Combine(_root, "owned.xlsx"); var a = Asset("SW1", "LAB001");
        await InventoryWorkbook.WriteAsync(path, [a, a], default); var rows = await InventoryWorkbook.ReadAsync(path, [], default); Assert.Contains(rows, r => r.Error.Contains("Duplicate AssetId"));
        await InventoryWorkbook.WriteAsync(path, [a with { Id = Guid.NewGuid() }], default); Assert.Contains(await InventoryWorkbook.ReadAsync(path, [a], default), r => r.Error.Contains("Serial conflicts"));
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update)) { var e = zip.GetEntry("xl/worksheets/sheet1.xml")!; string xml; using (var reader = new StreamReader(e.Open())) xml = reader.ReadToEnd(); e.Delete(); using var writer = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open()); writer.Write(xml.Replace("<is>", "<f>1+1</f><is>")); }
        await Assert.ThrowsAsync<IOException>(() => InventoryWorkbook.ReadAsync(path, [], default));
    }
    [Fact] public async Task ExcelAcceptsNativeDateCellsWithoutExecutingFormulas()
    {
        string path = Path.Combine(_root, "date.xlsx"); var a = Asset() with { MaEnd = new DateOnly(2027, 2, 28) }; await InventoryWorkbook.WriteAsync(path, [a], default);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!; System.Xml.Linq.XDocument xml; using (var stream = entry.Open()) xml = System.Xml.Linq.XDocument.Load(stream);
            System.Xml.Linq.XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var cell = xml.Descendants(ns + "c").Single(c => (string?)c.Attribute("r") == "X3"); cell.SetAttributeValue("t", "n"); cell.RemoveNodes(); cell.Add(new System.Xml.Linq.XElement(ns + "v", new DateTime(2027, 2, 28).ToOADate().ToString(System.Globalization.CultureInfo.InvariantCulture)));
            entry.Delete(); using var writer = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open()); writer.Write(xml);
        }
        var row = Assert.Single(await InventoryWorkbook.ReadAsync(path, [], default)); Assert.Empty(row.Error); Assert.Equal(a.MaEnd, row.Asset!.MaEnd);
    }
    [Fact] public async Task OversizedWorkbookExportPreservesExistingDestination()
    {
        string path = Path.Combine(_root, "retained.xlsx"); await File.WriteAllTextAsync(path, "original destination");
        string value = new('x', 20000);
        await Assert.ThrowsAsync<ArgumentException>(() => InventoryWorkbook.WriteTableAsync(path, "owned report", ["Value"], Enumerable.Repeat(new[] { value }, 300).ToArray(), default));
        Assert.Equal("original destination", await File.ReadAllTextAsync(path));
    }
    [Fact] public async Task CollectorJsonHashMustMatchAndTxtIsAlwaysPartial()
    {
        string path = Path.Combine(_root, "owned.json"); string transcript = "--- show version ---\nCisco IOS XE Software, Version 17.9.4a";
        var json = new { schema = 1, result = new { Target = new { Host = "192.0.2.1" }, State = "Completed", Finished = Now, Sha256 = new string('0', 64) }, outputChunks = new[] { transcript } };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(json)); await Assert.ThrowsAsync<IOException>(() => EvidenceFile.ReadAsync(path, null, "lab", default));
        path = Path.Combine(_root, "owned.txt"); await File.WriteAllTextAsync(path, transcript); var e = await EvidenceFile.ReadAsync(path, null, "lab", default); Assert.False(e.Complete); Assert.NotEmpty(e.Warnings);
    }
    [Fact] public void PlanApprovalNeedsCurrentReviewAndOfficialCompatibilityEvidence()
    {
        var a = Asset(); var e = Evidence(a); var adv = Advisory();
        var plan = new UpgradePlan(Guid.NewGuid(), a.Id, e.Id, adv.Cve, "17.12.4", "", "", "owner", "CHG1", "wave1", "2026-11-01T01:00:00Z", "", "engineer", Now, Now);
        var data = EngineeringData.Empty with { Assets = [a], Evidence = [e], Advisories = [adv], Plans = [plan] };
        Assert.Throws<ArgumentException>(() => EngineeringRules.Validate(data));
        Assert.Equal("Needs review / changed evidence", EngineeringRules.PlanStatus(plan, data, Now));
    }
    [Fact] public async Task AdvisoryFetchOnlySendsCveIdAndKeepsKevUnknownOnProviderFailure()
    {
        var requests = new List<Uri>(); using var lookup = new AdvisoryLookup(new Handler(requests));
        var a = await lookup.FetchAsync("CVE-2023-20198", default); Assert.Null(a.Kev); Assert.Contains("unavailable", a.KevSource); Assert.Single(a.CiscoReferences);
        Assert.Equal("?cveId=CVE-2023-20198", requests[0].Query); Assert.Equal(2, requests.Count);
        Assert.DoesNotContain(requests, u => u.ToString().Contains("192.0.2"));
    }
    [Fact] public void CaptureDiffIsBoundedAndUsesRunningConfigRegionWhenAvailable()
    {
        string before = "--- show running-config ---\nhostname SW1\ninterface Gi1\n description before\n--- show version ---\n17.9.4a";
        string after = before.Replace("before", "after").Replace("17.9.4a", "17.12.4");
        string diff = CaptureDiff.Compare(before, after, default); Assert.Contains("-  description before", diff); Assert.Contains("+  description after", diff); Assert.DoesNotContain("17.12.4", diff);
        Assert.StartsWith("No textual changes", CaptureDiff.Compare(before, before, default));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); Assert.Throws<OperationCanceledException>(() => CaptureDiff.Compare(before, before, cancelled.Token));
    }
    private sealed class Handler(List<Uri> requests) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Add(request.RequestUri!);
            string json = """{"vulnerabilities":[{"cve":{"id":"CVE-2023-20198","lastModified":"2026-10-20T00:00:00","descriptions":[{"lang":"en","value":"Owned metadata fixture"}],"references":[{"url":"https://www.cisco.com/c/en/us/support/docs/example.html"},{"url":"https://cisco.com.evil.invalid/"}],"metrics":{"cvssMetricV31":[{"cvssData":{"baseSeverity":"CRITICAL"}}]}}}]}""";
            return Task.FromResult(new HttpResponseMessage(request.RequestUri!.Host == "www.cisa.gov" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
