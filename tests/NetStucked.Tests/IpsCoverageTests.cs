using System.Collections.Immutable;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public sealed class IpsCoverageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "NetStucked-IpsTests-" + Guid.NewGuid().ToString("N"));
    public IpsCoverageTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);
    private string PathFor(string name) => Path.Combine(_directory, name);
    private async Task<CoverageInput> Input(int cves = 34)
    {
        string text = "Remarks,Plugin ID,Hostname,Severity,CVE,IP Address,Status tracking\r\n" + string.Join("\r\n", Enumerable.Range(1, 3).Select(i => $"\"ข้อความไทย 😀 =literal, row {i}\",212232,asset{i},High,\"{string.Join(";\n", Enumerable.Range(1, cves).Select(c => $"cve-2024-{49000 + c}"))}\",192.0.2.{i},Open"));
        string path = PathFor(Guid.NewGuid().ToString("N") + ".csv"); await File.WriteAllTextAsync(path, text);
        var file = await CoverageImport.ReadAsync(path, default); return CoverageText.CreateInput(file, "CSV", 1, CoverageText.AutoMap(file.Sheets[0].Rows[0].Values));
    }
    private sealed class MemoryCache : IIpsCoverageCache
    {
        public Dictionary<(string, string), IpsCoverageResult> Values { get; } = [];
        public Task<IpsCoverageResult?> ReadAsync(string identity, string cve, CancellationToken token) => Task.FromResult(Values.GetValueOrDefault((identity, cve)));
        public Task WriteAsync(string identity, IpsCoverageResult result, CancellationToken token) { Values[(identity, result.Cve)] = result; return Task.CompletedTask; }
    }
    private sealed class Provider(IpsVendor vendor, IpsCoverageState state = IpsCoverageState.Found, Func<string, CancellationToken, Task>? wait = null) : IIpsCoverageProvider
    {
        public int Calls;
        public IpsVendor Vendor => vendor;
        public string CacheIdentity => vendor.ToString();
        public async Task<IpsCoverageResult> LookupAsync(string cve, CancellationToken token)
        {
            Calls++; if (wait is not null) await wait(cve, token); token.ThrowIfCancellationRequested();
            return new(cve, vendor, state, "Owned fixture", "Fixture only", CacheIdentity, DateTimeOffset.UtcNow, null, "Fixture result; not live vendor data", state == IpsCoverageState.Found ? [new("fixture-A", "IPS A", "Vulnerability Protection", "released", "fixture", "fixture", ""), new("fixture-B", "IPS B", "Vulnerability Protection", "released", "fixture", "fixture", "")] : [], Failure: state == IpsCoverageState.Unknown ? "Timeout" : "");
        }
    }
    [Fact] public async Task ImportsReorderedHeadersMultilineAndPreservesRawRows()
    {
        var input = await Input(); Assert.Equal(3, input.Rows.Length); Assert.Equal(3, input.AssetCount); Assert.Equal(1, input.PluginCount); Assert.Equal(34, input.Cves.Length); Assert.Equal(102, input.Associations.Length);
        Assert.Equal([2, 3, 4], input.Rows.Select(r => r.Number)); Assert.Equal(4, input.Mapping.Cve);
        Assert.Contains("ข้อความไทย 😀", input.Field(input.Rows[0], 0)); Assert.Equal("Open", input.Field(input.Rows[2], 6));
    }
    [Theory]
    [InlineData("cve-2024-12345,CVE-2024-12345; CVE-2024-9999\r\nCVE-2025-123456", 3, 0)]
    [InlineData("CVE-2024-123 bad CVE-๒๐๒๔-๑๒๓๔ CVE-2024-1234extra", 0, 4)]
    [InlineData("", 0, 0)]
    public void NormalizesValidatesAndDeduplicates(string value, int valid, int invalid)
    { var parsed = CoverageText.Parse(value); Assert.Equal(valid, parsed.Cves.Length); Assert.Equal(invalid, parsed.Invalid.Length); }
    [Fact] public async Task IssuesRetainInvalidAndEmptySourceRowsAndRequireCveMapping()
    {
        string path = PathFor("issues.csv"); await File.WriteAllTextAsync(path, "CVE,Remarks\n,Keep empty\nwrong;CVE-2024-12345,Keep invalid\n");
        var file = await CoverageImport.ReadAsync(path, default); Assert.Throws<ArgumentException>(() => CoverageText.CreateInput(file, "CSV", 1, new(-1)));
        var input = CoverageText.CreateInput(file, "CSV", 1, new(0)); Assert.Equal(2, input.Rows.Length); Assert.Equal(2, input.Issues.Length); Assert.Single(input.Cves); Assert.Equal([2, 3], input.Issues.Select(i => i.Row));
    }
    [Theory]
    [InlineData("CVE;Name\n\"CVE-2024-1234;CVE-2024-1235\";\"a; b\"", 2)]
    [InlineData("CVE\tName\nCVE-2024-1234\t\"a\"\"b\"", 2)]
    public void CsvDelimiterAndQuotingAreDetected(string csv, int columns)
    { var rows = CoverageImport.ReadCsv(csv, default); Assert.Equal(columns, rows[1].Values.Length); }
    [Theory] [InlineData("CVE,Name\n\"unclosed")] [InlineData("CVE,Name\n\"closed\"junk,x")]
    public void MalformedCsvFailsWithoutSilentlyDroppingValues(string csv) => Assert.Throws<IOException>(() => CoverageImport.ReadCsv(csv, default));
    [Fact] public async Task UniqueJobsDoNotMultiplyByAssetAndCacheSkipsRepeatedRequests()
    {
        var input = await Input(); var cache = new MemoryCache(); var pa = new Provider(IpsVendor.PaloAlto); var cp = new Provider(IpsVendor.CheckPoint);
        var runner = new IpsCoverageCoordinator(cache); var run = await runner.RunAsync(input, [pa, cp], false, null, default);
        Assert.Equal(34, pa.Calls); Assert.Equal(34, cp.Calls); Assert.Equal(68, run.TotalJobs); Assert.Equal(68, run.CompletedJobs); Assert.False(run.Partial); Assert.All(run.Results, r => Assert.Equal(2, r.Signatures.Length));
        var second = await runner.RunAsync(input, [pa, cp], false, null, default); Assert.All(second.Results, r => Assert.True(r.FromCache)); Assert.Equal(34, pa.Calls); Assert.NotEqual(run.Id, second.Id);
    }
    [Fact] public async Task FailedRefreshShowsUnknownAndKeepsOriginalCachedEvidence()
    {
        var input = await Input(1); var cache = new MemoryCache(); var runner = new IpsCoverageCoordinator(cache);
        var original = await runner.RunAsync(input, [new Provider(IpsVendor.PaloAlto), new Provider(IpsVendor.CheckPoint)], false, null, default);
        var next = await runner.RunAsync(input, [new Provider(IpsVendor.PaloAlto, IpsCoverageState.Unknown), new Provider(IpsVendor.CheckPoint)], true, null, default);
        var failed = next.Results[0]; Assert.Equal(IpsCoverageState.Unknown, failed.State); Assert.True(failed.Previous!.Stale); Assert.Equal(original.Results[0].CheckedAt, failed.Previous.CheckedAt); Assert.NotNull(failed.AttemptedAt);
        Assert.Equal(IpsCoverageState.Found, cache.Values[("PaloAlto", input.Cves[0])].State);
    }
    [Fact] public async Task CancellationOwnsTransportAndNewRunCannotOverwriteOldSnapshot()
    {
        var input = await Input(2); var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); using var cancel = new CancellationTokenSource();
        var pa = new Provider(IpsVendor.PaloAlto, wait: async (_, token) => { started.SetResult(); await Task.Delay(Timeout.Infinite, token); });
        string csv = PathFor("pending-source.csv"); await File.WriteAllTextAsync(csv, "CVE,Protection\n" + input.Cves[0] + ",Owned fixture");
        var source = await CoverageImport.ReadAsync(csv, default); var checkpoint = new CheckPointCoverageProvider(CheckPointSnapshot.Create(source, 1, 0, 1, -1, -1, "fixture", null));
        var runner = new IpsCoverageCoordinator(new MemoryCache()); var task = runner.RunAsync(input, [pa, checkpoint], false, null, cancel.Token); await started.Task; cancel.Cancel();
        var stopped = await task; Assert.True(stopped.Cancelled); Assert.True(stopped.Partial); Assert.All(stopped.Results, r => Assert.Equal(IpsCoverageState.NotChecked, r.State));
        Assert.All(stopped.Results.Where(r => r.Vendor == IpsVendor.CheckPoint), r => { Assert.Equal("pending-source.csv", r.Source); Assert.Contains(source.Sha256, r.Reason); Assert.Null(r.CheckedAt); });
        var other = await Input(1); var newer = await runner.RunAsync(other, [new Provider(IpsVendor.PaloAlto), new Provider(IpsVendor.CheckPoint)], false, null, default); Assert.Equal(input.Id, stopped.Input.Id); Assert.NotEqual(stopped.Input.Id, newer.Input.Id);
    }
    [Fact] public async Task RetryOnlyCallsUnknownAndUncheckedJobs()
    {
        var input = await Input(2); var runner = new IpsCoverageCoordinator(new MemoryCache());
        var first = await runner.RunAsync(input, [new Provider(IpsVendor.PaloAlto, IpsCoverageState.Unknown), new Provider(IpsVendor.CheckPoint)], false, null, default);
        var pa = new Provider(IpsVendor.PaloAlto); var cp = new Provider(IpsVendor.CheckPoint); await runner.RunAsync(input, [pa, cp], true, null, default, first);
        Assert.Equal(2, pa.Calls); Assert.Equal(0, cp.Calls);
    }
    [Theory]
    [InlineData("cve_under_testing", IpsCoverageState.Pending)]
    [InlineData("cve_under_development", IpsCoverageState.Pending)]
    [InlineData("cve_cannot_cover", IpsCoverageState.VendorCannotCover)]
    [InlineData("cve_being_monitored", IpsCoverageState.Unknown)]
    [InlineData("cve_not_covered", IpsCoverageState.NotFoundInSource)]
    public void ParsesVendorStatusesFromDocumentedArrayShape(string status, IpsCoverageState expected)
    { string json = $$"""{"success":true,"data":[{"cve_id":"CVE-2024-12345","cve_status":"{{status}}","not_cover_reason":"fixture limit"}]}"""; Assert.Equal(expected, ThreatVaultCoverageProvider.Parse("CVE-2024-12345", json, DateTimeOffset.UtcNow).State); }
    private const string Covered = """{"success":true,"data":{"cve_id":"CVE-2024-12345","cve_status":"cve_covered","vulnerability":[{"id":"fixture-1","name":"Owned test signature","status":"released","latest_release_version":"fixture-content","default_action":"reset-both"},{"id":"fixture-2","name":"Second test signature","status":"active"}],"antivirus":[]}}""";
    [Fact] public void ObjectShapePreservesMultipleSignaturesReleaseAndLimitations()
    { var result = ThreatVaultCoverageProvider.Parse("CVE-2024-12345", Covered, DateTimeOffset.UtcNow); Assert.Equal(IpsCoverageState.Found, result.State); Assert.Equal(2, result.Signatures.Length); Assert.Contains("default_action", result.Signatures[0].Limitations); Assert.Equal("fixture-content", result.Signatures[0].ContentVersion); Assert.Contains("latest_release_version", result.Signatures[0].Release); }
    [Fact] public void AntivirusAloneNeverCountsAsIpsFound()
    {
        var result = ThreatVaultCoverageProvider.Parse("CVE-2024-12345", """{"success":true,"data":{"cve_id":"CVE-2024-12345","cve_status":"cve_covered","antivirus":[{"id":"AV-1","name":"Fixture AV","status":"active"}],"vulnerability":[]}}""", DateTimeOffset.UtcNow);
        Assert.Equal(IpsCoverageState.NotFoundInSource, result.State); Assert.False(result.IsIpsFound); Assert.Contains("not IPS", result.Signatures.Single().Type);
    }
    [Theory]
    [InlineData("{}")] [InlineData("{\"success\":false,\"data\":[]}")]
    [InlineData("{\"success\":true,\"data\":{\"cve_id\":\"CVE-2020-1111\",\"cve_status\":\"cve_covered\"}}")]
    [InlineData("{\"success\":true,\"data\":{\"cve_id\":\"CVE-2024-12345\",\"cve_status\":\"cve_covered\"}}")]
    [InlineData("{\"success\":true,\"data\":[],\"count\":1}")]
    public void IncompleteOrMismatchedResponsesAreUnknown(string json) => Assert.Equal(IpsCoverageState.Unknown, ThreatVaultCoverageProvider.Parse("CVE-2024-12345", json, DateTimeOffset.UtcNow).State);
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => action(request, cancellationToken); }
    [Theory] [InlineData(401)] [InlineData(403)]
    public async Task RejectedKeysStopFurtherRequestsAndNeverLeakResponseOrKey(int code)
    {
        int calls = 0; const string secret = "owned-test-key-do-not-log";
        using var client = new HttpClient(new Handler((request, _) => { calls++; Assert.Equal(secret, request.Headers.GetValues("X-API-KEY").Single()); Assert.Equal(ThreatVaultCoverageProvider.Endpoint, request.RequestUri!.GetLeftPart(UriPartial.Path)); Assert.Equal("?cve_id=CVE-2024-12345", request.RequestUri.Query); return Task.FromResult(new HttpResponseMessage((HttpStatusCode)code) { Content = new StringContent(secret) }); }));
        var provider = new ThreatVaultCoverageProvider(client, secret, minimumInterval: TimeSpan.Zero);
        for (int i = 0; i < 3; i++) { var r = await provider.LookupAsync("CVE-2024-12345", default); Assert.Equal(IpsCoverageState.Unknown, r.State); Assert.DoesNotContain(secret, r.Reason); }
        Assert.Equal(1, calls);
    }
    [Fact] public async Task RateLimitHonorsDailyResetAndDoesNotFloodRetries()
    {
        int calls = 0; using var client = new HttpClient(new Handler((_, _) => { calls++; var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests); response.Headers.Add("X-Day-RateLimit-Remaining", "0"); response.Headers.Add("X-Day-RateLimit-Reset", DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds().ToString()); return Task.FromResult(response); }));
        var provider = new ThreatVaultCoverageProvider(client, "fixture-key", minimumInterval: TimeSpan.Zero);
        var first = await provider.LookupAsync("CVE-2024-12345", default); var second = await provider.LookupAsync("CVE-2024-12346", default); Assert.Equal("RateLimit", first.Failure); Assert.Equal("RateLimit", second.Failure); Assert.Equal(1, calls);
    }
    [Fact] public async Task Short429RetriesAreBoundedAndRecover()
    {
        int calls = 0; using var client = new HttpClient(new Handler((_, _) => { calls++; var response = new HttpResponseMessage(calls == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK) { Content = new StringContent(Covered) }; if (calls == 1) response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(5)); return Task.FromResult(response); }));
        var provider = new ThreatVaultCoverageProvider(client, "fixture-key", minimumInterval: TimeSpan.Zero); var result = await provider.LookupAsync("CVE-2024-12345", default); Assert.Equal(IpsCoverageState.Found, result.State); Assert.Equal(2, calls);
    }
    [Fact] public async Task TimeoutIsUnknownAfterThreeAttemptsAndUserCancellationPropagates()
    {
        int calls = 0; using var client = new HttpClient(new Handler((_, _) => { calls++; throw new OperationCanceledException(); }));
        var provider = new ThreatVaultCoverageProvider(client, "fixture-key", minimumInterval: TimeSpan.Zero); Assert.Equal("Timeout", (await provider.LookupAsync("CVE-2024-12345", default)).Failure); Assert.Equal(3, calls);
        using var stop = new CancellationTokenSource(); stop.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.LookupAsync("CVE-2024-12345", stop.Token));
    }
    [Fact] public async Task CheckPointMissingIdsManyCveAndSnapshotActionAreRetained()
    {
        string path = PathFor("ips.csv"); await File.WriteAllTextAsync(path, "Protection,Industry Reference,Profile_Name\nProtection A,\"CVE-2024-12345;CVE-2024-12346\",Detect\nProtection B,CVE-2024-12345,Prevent");
        var file = await CoverageImport.ReadAsync(path, default); var snapshot = CheckPointSnapshot.Create(file, 1, 1, 0, -1, 2, "user version", DateTimeOffset.UtcNow.AddDays(-10));
        var provider = new CheckPointCoverageProvider(snapshot); var result = await provider.LookupAsync("CVE-2024-12345", default); Assert.True(result.Stale); Assert.Equal(2, result.Signatures.Length); Assert.All(result.Signatures, s => Assert.Empty(s.Id)); Assert.Equal("Detect", result.Signatures[0].SnapshotAction);
        var missing = await provider.LookupAsync("CVE-2024-99999", default); Assert.Equal(IpsCoverageState.NotFoundInSource, missing.State); Assert.Contains("imported database", missing.Reason); Assert.Contains(file.Sha256, missing.Reason); Assert.Contains("imported", missing.Reason);
        Assert.Throws<ArgumentException>(() => CheckPointSnapshot.Create(file, 1, -1, 0, -1, -1, "", null));
    }
    [Fact] public async Task CacheIsIsolatedByCsvHashMappingAndMetadataAndDoesNotStoreErrors()
    {
        string path = PathFor("ips.csv"); await File.WriteAllTextAsync(path, "Protection,CVE\nA,CVE-2024-12345");
        var file = await CoverageImport.ReadAsync(path, default); var one = CheckPointSnapshot.Create(file, 1, 1, 0, -1, -1, "first", null); var two = CheckPointSnapshot.Create(file, 1, 1, 0, -1, -1, "second", null); Assert.NotEqual(one.Identity, two.Identity);
        var cache = new IpsCoverageCache(PathFor("cache")); var result = await new CheckPointCoverageProvider(one).LookupAsync("CVE-2024-12345", default); await cache.WriteAsync(one.Identity, result, default);
        Assert.NotNull(await cache.ReadAsync(one.Identity, result.Cve, default)); Assert.Null(await cache.ReadAsync(two.Identity, result.Cve, default));
        await cache.WriteAsync("unknown", result with { State = IpsCoverageState.Unknown }, default); Assert.Null(await cache.ReadAsync("unknown", result.Cve, default));
        await File.AppendAllTextAsync(path, "\nB,CVE-2024-12346"); var changed = await CoverageImport.ReadAsync(path, default); Assert.NotEqual(one.Identity, CheckPointSnapshot.Create(changed, 1, 1, 0, -1, -1, "first", null).Identity);
    }
    [Fact] public async Task ExcelExportsAllRowsLinksSignaturesAndInertTextWithoutOverwriting()
    {
        var input = await Input(); var run = await new IpsCoverageCoordinator(new MemoryCache()).RunAsync(input, [new Provider(IpsVendor.PaloAlto), new Provider(IpsVendor.CheckPoint)], false, null, default);
        string path = PathFor("report.xlsx"); await CoverageWorkbook.WriteAsync(path, run, default);
        using (var zip = ZipFile.OpenRead(path))
        {
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XDocument Xml(string name) { using var stream = zip.GetEntry(name)!.Open(); return XDocument.Load(stream); }
            Assert.Equal(6, Xml("xl/workbook.xml").Descendants(ns + "sheet").Count());
            var findings = Xml("xl/worksheets/sheet3.xml"); Assert.Equal(4, findings.Descendants(ns + "row").Count()); Assert.Contains("ข้อความไทย 😀", findings.ToString()); Assert.Contains("Original.Status tracking", findings.ToString()); Assert.Contains("Original.Remarks", findings.ToString()); Assert.Empty(findings.Descendants(ns + "f")); Assert.NotEmpty(findings.Descendants(ns + "pane")); Assert.NotEmpty(findings.Descendants(ns + "autoFilter"));
            Assert.Equal(103, Xml("xl/worksheets/sheet5.xml").Descendants(ns + "row").Count()); Assert.Equal(137, Xml("xl/worksheets/sheet4.xml").Descendants(ns + "row").Count());
        }
        byte[] before = await File.ReadAllBytesAsync(path); await Assert.ThrowsAsync<IOException>(() => CoverageWorkbook.WriteAsync(path, run, default)); Assert.Equal(before, await File.ReadAllBytesAsync(path));
        await Assert.ThrowsAsync<ArgumentException>(() => CoverageWorkbook.WriteAsync(input.Path, run, default));
    }
    [Fact] public async Task PartialExportExplicitlyIncludesUncheckedCounts()
    {
        var input = await Input(1); var run = new CoverageRun(Guid.NewGuid(), input, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, input.Cves.SelectMany(c => Enum.GetValues<IpsVendor>().Select(v => IpsCoverageResult.Unchecked(c, v))).ToImmutableArray(), true);
        string path = PathFor("partial.xlsx"); await CoverageWorkbook.WriteAsync(path, run, default); using var zip = ZipFile.OpenRead(path); using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()); string xml = await reader.ReadToEndAsync(); Assert.Contains("PARTIAL", xml); Assert.Contains("Not checked", xml);
    }
    [Fact] public async Task CorruptCacheWithMissingResultIsIgnored()
    {
        string directory = PathFor("corrupt-cache"); var cache = new IpsCoverageCache(directory);
        var result = await new Provider(IpsVendor.PaloAlto).LookupAsync("CVE-2024-12345", default); await cache.WriteAsync("scope", result, default);
        string entry = Directory.GetFiles(directory, "*.json").Single(); await File.WriteAllTextAsync(entry, "{\"Schema\":1,\"Identity\":\"scope\",\"Result\":null}");
        Assert.Null(await cache.ReadAsync("scope", result.Cve, default));
    }
    [Fact] public async Task FormulaLikeSourceTextIsExportedAsLiteralText()
    {
        string path = PathFor("formula-text.csv"); await File.WriteAllTextAsync(path, "CVE,Remarks,Status tracking\nCVE-2024-12345,=1+1,@example");
        var file = await CoverageImport.ReadAsync(path, default); var input = CoverageText.CreateInput(file, "CSV", 1, new(0));
        var run = await new IpsCoverageCoordinator(new MemoryCache()).RunAsync(input, [new Provider(IpsVendor.PaloAlto), new Provider(IpsVendor.CheckPoint)], false, null, default);
        string output = PathFor("literal.xlsx"); await CoverageWorkbook.WriteAsync(output, run, default);
        using var zip = ZipFile.OpenRead(output); using var stream = zip.GetEntry("xl/worksheets/sheet3.xml")!.Open(); var xml = XDocument.Load(stream); XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var cell = xml.Descendants(ns + "c").Single(c => (string?)c.Attribute("r") == "D2"); Assert.Equal("inlineStr", (string?)cell.Attribute("t")); Assert.Equal("=1+1", cell.Descendants(ns + "t").Single().Value); Assert.Empty(xml.Descendants(ns + "f"));
    }
    [Fact] public async Task XlsxUsesWorkbookRelationshipsAndSharedStringsForSelectedSheet()
    {
        string path = PathFor("source.xlsx");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            void Entry(string name, string value) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(value); }
            Entry("xl/workbook.xml", "<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Notes' r:id='a'/><sheet name='VA' r:id='b'/></sheets></workbook>");
            Entry("xl/_rels/workbook.xml.rels", "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='a' Target='worksheets/notes.xml'/><Relationship Id='b' Target='/xl/worksheets/actual.xml'/></Relationships>");
            Entry("xl/sharedStrings.xml", "<sst xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><si><t>CVE</t></si><si><t>Remarks</t></si><si><t>CVE-2024-12345</t></si><si><t>ภาษาไทย</t></si></sst>");
            Entry("xl/worksheets/notes.xml", "<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData/></worksheet>");
            Entry("xl/worksheets/actual.xml", "<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData><row r='3'><c r='A3' t='s'><v>0</v></c><c r='B3' t='s'><v>1</v></c></row><row r='4'><c r='A4' t='s'><v>2</v></c><c r='B4' t='s'><v>3</v></c></row></sheetData></worksheet>");
        }
        var file = await CoverageImport.ReadAsync(path, default); var input = CoverageText.CreateInput(file, "VA", 3, new(0)); Assert.Equal(2, file.Sheets.Length); Assert.Single(input.Cves); Assert.Equal(4, input.Rows[0].Number); Assert.Equal("ภาษาไทย", input.Rows[0].Values[1]);
    }
}
