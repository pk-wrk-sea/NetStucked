using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public sealed class EngineeringWorkflowDefectTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NetStucked-owned-workflow-" + Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public EngineeringWorkflowDefectTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private static EvidenceDocument Parse(string text, string host = "192.0.2.1") => CiscoEvidenceParser.Parse(text, "owned transcript", new string('a', 64), host, Now, true, null);
    private static Asset Asset(string hostname = "SW1", string serial = "") => new(Guid.NewGuid(), hostname, "192.0.2.1", "Cisco", "C9300-24T", serial, "Lab", "Rack 1", "Switch", "", null, null, Now);

    [Fact]
    public void NonNeighborCommandCannotCreateTopologyAdjacency()
    {
        var evidence = Parse("--- show running-config ---\nhostname LOCAL\nbanner motd ^C\nDevice ID: SHAM\nIP address: 192.0.2.99\nInterface: Gi1, Port ID (outgoing port): Gi2\n^C\n--- show version ---\nCisco IOS XE Software, Version 17.9.4a\n");
        Assert.Empty(evidence.Neighbors);
    }

    [Fact]
    public void NeighborRecordCannotInheritAddressFromEarlierCommand()
    {
        var evidence = Parse("--- show ip interface ---\nIP address: 192.0.2.50\n--- show cdp neighbors detail ---\nDevice ID: SW2\nInterface: Gi1, Port ID (outgoing port): Gi2\n");
        Assert.Empty(Assert.Single(evidence.Neighbors).Address);
    }

    [Fact]
    public void UnnamedLldpRecordCannotLeakRemotePortToNextRecord()
    {
        var evidence = Parse("--- show lldp neighbors detail ---\nLocal Intf: Gi1\nPort id: Gi9\nLocal Intf: Gi2\nSystem Name: SW3\n");
        var neighbor = Assert.Single(evidence.Neighbors);
        Assert.Equal("Gi2", neighbor.LocalInterface);
        Assert.Empty(neighbor.RemoteInterface);
    }

    [Fact]
    public void LldpRecordRetainsPortWhenNameFollowsPortAndNextRecordIsIsolated()
    {
        var evidence = Parse("--- show lldp neighbors detail ---\nLocal Intf: Gi1\nPort id: Gi9\nSystem Name: SW2\nManagement Address: 192.0.2.2\nLocal Intf: Gi2\nPort id: Gi8\nSystem Name: SW3\nManagement Address: 192.0.2.3\n");
        Assert.Equal(2, evidence.Neighbors.Length);
        Assert.Equal(new NeighborFact("SW2", "192.0.2.2", "Gi1", "Gi9", "LLDP", 4), evidence.Neighbors[0]);
        Assert.Equal(new NeighborFact("SW3", "192.0.2.3", "Gi2", "Gi8", "LLDP", 8), evidence.Neighbors[1]);
    }

    [Fact]
    public void TopologyDoesNotTreatCaseOnlyPeerNamesAsConflictingIdentities()
    {
        var selected = Parse("--- show cdp neighbors detail ---\nDevice ID: SW2\nInterface: Gi1, Port ID (outgoing port): Gi2\n") with
        { Neighbors = [new("SW2", "192.0.2.2", "Gi1", "Gi2", "CDP", 2), new("sw2", "192.0.2.2", "Gi1", "Gi2", "LLDP", 5)] };
        var peer = Parse("hostname SW2", "192.0.2.2") with { Neighbors = [new("192.0.2.1", "192.0.2.1", "Gi2", "Gi1", "CDP", 1)] };
        Assert.All(TopologyProjection.Build(selected, [selected, peer], Now), link => Assert.StartsWith("Verified", link.Classification));
    }

    [Fact]
    public void ReciprocalPortsDoNotVerifyContradictoryManagementAddress()
    {
        var selected = Parse("--- show cdp neighbors detail ---\nDevice ID: SW2\nIP address: 192.0.2.2\nInterface: Gi1, Port ID (outgoing port): Gi2\n");
        var peer = Parse("hostname SW2", "192.0.2.3") with { Neighbors = [new("192.0.2.1", "192.0.2.1", "Gi2", "Gi1", "CDP", 1)] };
        var link = Assert.Single(TopologyProjection.Build(selected, [selected, peer], Now));
        Assert.StartsWith("Needs review", link.Classification);
        Assert.Contains("address", link.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateWorkbookIdentityRejectsAllAmbiguousRows()
    {
        var asset = Asset("SW1", "LAB001");
        string path = Path.Combine(_root, "duplicate-id.xlsx");
        await InventoryWorkbook.WriteAsync(path, [asset, asset with { Hostname = "OTHER", Serial = "LAB002" }], default);
        var rows = await InventoryWorkbook.ReadAsync(path, [asset], default);
        Assert.Equal(2, rows.Length);
        Assert.All(rows, row => { Assert.Null(row.Asset); Assert.Equal("Rejected", row.Action); Assert.Contains("Duplicate AssetId", row.Error); });
    }

    [Fact]
    public async Task DuplicateWorkbookSerialRejectsAllAmbiguousRows()
    {
        string path = Path.Combine(_root, "duplicate-serial.xlsx");
        await InventoryWorkbook.WriteAsync(path, [Asset("SW1", "LAB001"), Asset("SW2", "lab001")], default);
        var rows = await InventoryWorkbook.ReadAsync(path, [], default);
        Assert.Equal(2, rows.Length);
        Assert.All(rows, row => { Assert.Null(row.Asset); Assert.Equal("Rejected", row.Action); Assert.Contains("Serial conflicts", row.Error); });
    }

    [Fact]
    public async Task LargeUnicodeOwnExportRoundTripsAllHumanFieldsAndDateExtremes()
    {
        string text = new('ก', 256);
        var assets = Enumerable.Range(0, 600).Select(i => Asset("อุปกรณ์ " + i.ToString(CultureInfo.InvariantCulture)) with
        { Vendor = text, Model = text, Serial = "LAB" + i.ToString(CultureInfo.InvariantCulture), Site = text, Location = text, Role = text, Contract = text, MaStart = DateOnly.MinValue, MaEnd = DateOnly.MaxValue }).ToArray();
        string path = Path.Combine(_root, "unicode.xlsx");
        await InventoryWorkbook.WriteAsync(path, assets, default);
        var rows = await InventoryWorkbook.ReadAsync(path, assets, default);
        Assert.Equal(assets.Length, rows.Length);
        for (int i = 0; i < rows.Length; i++) { Assert.Empty(rows[i].Error); Assert.Equal(assets[i], rows[i].Asset! with { ModifiedAt = assets[i].ModifiedAt }); }
    }

    [Fact]
    public async Task Native1904ExcelDatesPreserveWholeDayAndRejectFractionalTime()
    {
        string path = Path.Combine(_root, "date1904.xlsx");
        await InventoryWorkbook.WriteAsync(path, [Asset() with { MaStart = new DateOnly(1904, 1, 1), MaEnd = new DateOnly(2027, 2, 28) }], default);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            Rewrite(zip, "xl/workbook.xml", xml => xml.Root!.AddFirst(new XElement(Ns + "workbookPr", new XAttribute("date1904", "1"))));
            Rewrite(zip, "xl/worksheets/sheet1.xml", xml =>
            {
                SetNumericDate(xml, "K3", "0");
                SetNumericDate(xml, "L3", (new DateTime(2027, 2, 28) - new DateTime(1904, 1, 1)).TotalDays.ToString(CultureInfo.InvariantCulture));
            });
        }
        var row = Assert.Single(await InventoryWorkbook.ReadAsync(path, [], default));
        Assert.Empty(row.Error); Assert.Equal(new DateOnly(1904, 1, 1), row.Asset!.MaStart); Assert.Equal(new DateOnly(2027, 2, 28), row.Asset.MaEnd);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update)) Rewrite(zip, "xl/worksheets/sheet1.xml", xml => SetNumericDate(xml, "K3", "0.5"));
        Assert.Contains("Invalid Excel date", Assert.Single(await InventoryWorkbook.ReadAsync(path, [], default)).Error);
    }

    [Fact]
    public async Task ExplicitSourceReanalysisAppendsTraceableRevisionWithoutChangingCaptureOrHistory()
    {
        var asset = Asset("SW1");
        string transcript = "--- show running-config ---\nhostname SW1\nbanner motd ^C\nDevice ID: SHAM\nInterface: Gi1, Port ID (outgoing port): Gi2\n^C\n--- show version ---\nCisco IOS XE Software, Version 17.9.4a\n";
        string source = Path.Combine(_root, "old-parser-capture.json");
        await WriteCollectorAsync(source, transcript);
        byte[] originalBytes = await File.ReadAllBytesAsync(source);
        var original = await EvidenceFile.ReadAsync(source, asset.Id, "SW1", default);
        original = original with { Parser = "cisco-text/1", Neighbors = [new("SHAM", "", "Gi1", "Gi2", "CDP", 4)] };
        var store = new EngineeringStore(new UserSettingsStore(_root));
        var initial = await store.SaveAsync(EngineeringData.Empty with { Assets = [asset], Evidence = [original] }, 0, default);
        var revision = await EvidenceFile.ReanalyzeAsync(original, default);
        Assert.NotEqual(original.Id, revision.Id);
        Assert.Equal(original.AssetId, revision.AssetId); Assert.Equal(original.Source, revision.Source); Assert.Equal(original.Sha256, revision.Sha256); Assert.Equal(original.CollectedAt, revision.CollectedAt); Assert.Equal(original.Complete, revision.Complete);
        Assert.Equal(CiscoEvidenceParser.Version, revision.Parser); Assert.Empty(revision.Neighbors);
        Assert.Contains(revision.Warnings, w => w.Contains(original.Id.ToString()) && w.Contains("cisco-text/1") && w.Contains("reanalysis"));
        await store.SaveAsync(initial with { Evidence = initial.Evidence.Append(revision).ToArray() }, initial.Revision, default);
        var loaded = await store.LoadAsync(default);
        Assert.Equal(2, loaded.Evidence.Length);
        Assert.Equal("SHAM", Assert.Single(loaded.Evidence[0].Neighbors).Name);
        Assert.Equal("cisco-text/1", loaded.Evidence[0].Parser);
        Assert.Equal(revision.Id, EngineeringRules.Latest(loaded, asset.Id)!.Id);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(source));
        Assert.Single(loaded.Changes!); // No human inventory edit is invented by reanalysis.
    }

    [Fact]
    public async Task ReanalysisRejectsChangedSourceAndCancellationWithoutMutatingPriorEvidence()
    {
        string source = Path.Combine(_root, "retained-capture.json");
        await WriteCollectorAsync(source, "--- show version ---\nCisco IOS XE Software, Version 17.9.4a\n");
        var original = await EvidenceFile.ReadAsync(source, null, "SW1", default);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => EvidenceFile.ReanalyzeAsync(original, cancelled.Token));
        await File.AppendAllTextAsync(source, " ");
        await Assert.ThrowsAsync<IOException>(() => EvidenceFile.ReanalyzeAsync(original, default));
        Assert.Equal("17.9.4a", Assert.Single(original.Facts, f => f.Field == "Version").Value);
    }

    [Fact]
    public void LatestEvidencePrefersAppendedRevisionOnlyWhenCollectionTimeTies()
    {
        var asset = Asset();
        var older = Parse("hostname SW1") with { AssetId = asset.Id, CollectedAt = Now.AddDays(-2) };
        var newest = older with { Id = Guid.NewGuid(), CollectedAt = Now };
        var revisedOlder = older with { Id = Guid.NewGuid(), Parser = CiscoEvidenceParser.Version };
        var revisedNewest = newest with { Id = Guid.NewGuid(), Parser = CiscoEvidenceParser.Version };
        var data = EngineeringData.Empty with { Assets = [asset], Evidence = [older, newest, revisedOlder] };
        Assert.Equal(newest.Id, EngineeringRules.Latest(data, asset.Id)!.Id);
        Assert.Equal(revisedNewest.Id, EngineeringRules.Latest(data with { Evidence = data.Evidence.Append(revisedNewest).ToArray() }, asset.Id)!.Id);
    }

    private static Task WriteCollectorAsync(string path, string transcript) => File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
    {
        schema = 1,
        result = new { Target = new { Host = "192.0.2.1" }, State = "Completed", Finished = Now, Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(transcript))) },
        outputChunks = new[] { transcript }
    }));

    private static void SetNumericDate(XDocument xml, string reference, string serial)
    {
        var cell = xml.Descendants(Ns + "c").Single(c => (string?)c.Attribute("r") == reference);
        cell.SetAttributeValue("t", "n"); cell.RemoveNodes(); cell.Add(new XElement(Ns + "v", serial));
    }
    private static void Rewrite(ZipArchive zip, string name, Action<XDocument> update)
    {
        var entry = zip.GetEntry(name)!; XDocument xml; using (var stream = entry.Open()) xml = XDocument.Load(stream);
        update(xml); entry.Delete(); using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(xml);
    }
}
