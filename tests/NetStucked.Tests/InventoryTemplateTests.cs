using System.IO.Compression;
using System.Text.Json;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public sealed class InventoryTemplateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NetStucked-owned-template-" + Guid.NewGuid().ToString("N"));
    public InventoryTemplateTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private static Asset Blank() => new(Guid.NewGuid(), "", "", "", "", "", "", "", "", "", null, null, DateTimeOffset.UtcNow);

    [Fact] public async Task OptionalFieldsAndRepeatedChassisAddressSurviveExportEditImport()
    {
        var chassis = Blank() with { Hostname = "SW1", Address = "192.0.2.1", Number = "1", Company = "ENCO", Province = "กรุงเทพมหานคร", Building = "A", Floor = "G", Room = "Control Room", Rack = "NW-A-G", Vendor = "Cisco", Model = "C6807-XL", Serial = "OWNED-CHASSIS", Role = "Switch", MaType = "Smartnet", SlaType = "1", Remark = "", Ldos = new(2027, 10, 31) };
        var card = chassis with { Id = Guid.NewGuid(), Number = "2", Serial = "OWNED-CARD", Role = "Line Card", Model = "C6800-16P10G", Ldos = null };
        var spare = Blank() with { Remark = "อะไหล่: ยังไม่มี IP หรือ hostname" };
        string file = Path.Combine(_root, "roundtrip.xlsx");
        await InventoryWorkbook.WriteAsync(file, [chassis, card, spare], default);
        var first = await InventoryWorkbook.ReadAsync(file, [], default);
        Assert.Equal(3, first.Length); Assert.All(first, row => Assert.Empty(row.Error));
        Assert.Equal(chassis.Company, first[0].Asset!.Company); Assert.Equal(chassis.Ldos, first[0].Asset!.Ldos);
        Assert.Equal(card.Id, first[1].Asset!.Id); Assert.Equal(spare.Remark, first[2].Asset!.Remark);
        await InventoryWorkbook.WriteAsync(file, [chassis with { Company = "", Address = "", Remark2 = "แก้ไข" }, card, spare], default);
        var edited = await InventoryWorkbook.ReadAsync(file, [chassis, card, spare], default);
        Assert.All(edited, row => { Assert.Empty(row.Error); Assert.Contains("Update", row.Action); });
        Assert.Equal(chassis.Id, edited[0].Asset!.Id); Assert.Empty(edited[0].Asset!.Company); Assert.Equal("แก้ไข", edited[0].Asset!.Remark2);
    }

    [Fact] public async Task HeaderOnlyUserWorkbookAcceptsImageColumnsAndHumanDates()
    {
        string file = Path.Combine(_root, "plain.xlsx");
        await InventoryWorkbook.WriteTableAsync(file, "unused", ["No", "Company", "SerialNumber", "LDoS", "Remark"], [["1", "ENCO", "", "31-Oct-2027", "optional"]], default);
        using (var zip = ZipFile.Open(file, ZipArchiveMode.Update))
        {
            var part = zip.GetEntry("xl/worksheets/sheet1.xml")!; System.Xml.Linq.XDocument xml;
            using (var stream = part.Open()) xml = System.Xml.Linq.XDocument.Load(stream);
            xml.Root!.Elements().Single().Elements().First().Remove();
            part.Delete(); using var writer = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open()); writer.Write(xml);
        }
        var row = Assert.Single(await InventoryWorkbook.ReadAsync(file, [], default)); Assert.Empty(row.Error);
        Assert.Equal(new DateOnly(2027, 10, 31), row.Asset!.Ldos); Assert.Empty(row.Asset.Address); Assert.Empty(row.Asset.Hostname);
    }

    [Fact] public async Task LegacySchemaOneImportsAndOldJsonGetsEmptyAdditionalFields()
    {
        string file = Path.Combine(_root, "legacy.xlsx"); var id = Guid.NewGuid();
        await InventoryWorkbook.WriteTableAsync(file, "NetStucked Inventory schema 1", InventoryWorkbook.LegacyHeaders, [[id.ToString(), "SW", "192.0.2.1", "Cisco", "model", "serial", "site", "location", "Switch", "contract", "", ""]], default);
        var row = Assert.Single(await InventoryWorkbook.ReadAsync(file, [], default)); Assert.Empty(row.Error); Assert.Equal(id, row.Asset!.Id); Assert.Equal("contract", row.Asset.Contract); Assert.Empty(row.Asset.Company);
        var old = JsonSerializer.Deserialize<Asset>("{\"Id\":\"" + id + "\",\"Hostname\":\"SW\",\"Address\":\"\",\"Vendor\":\"\",\"Model\":\"\",\"Serial\":\"\",\"Site\":\"\",\"Location\":\"\",\"Role\":\"\",\"Contract\":\"\"}")!;
        EngineeringRules.Validate(old); Assert.Empty(old.Company); Assert.Null(old.Ldos);
    }

    [Fact] public async Task SavedCollectorIdentityContainsOnlyExplicitUsernames()
    {
        var store = new UserSettingsStore(_root); store.Preferences.CollectorUsername1 = @"DOMAIN\user"; await store.SaveAsync();
        var loaded = new UserSettingsStore(_root); Assert.Null(loaded.LoadError); Assert.Equal(@"DOMAIN\user", loaded.Preferences.CollectorUsername1);
        string text = await File.ReadAllTextAsync(Path.Combine(_root, "settings.json")); Assert.DoesNotContain("Password", text); Assert.DoesNotContain("EnableSecret", text);
    }

    [Fact] public async Task UniqueSerialPreviewCanUpdateWithoutIdAndMissingColumnsPreserveHumanFields()
    {
        var asset = Blank() with { Serial = "OWNED-SERIAL", Company = "Company retained", Role = "Line Card", Ldos = new(2027, 10, 31) };
        string file = Path.Combine(_root, "serial-edit.xlsx");
        await InventoryWorkbook.WriteTableAsync(file, "NetStucked Inventory schema 2", ["SerialNumber", "Remark"], [["OWNED-SERIAL", "edited"]], default);
        var row = Assert.Single(await InventoryWorkbook.ReadAsync(file, [asset], default)); Assert.Empty(row.Error);
        Assert.Equal(asset.Id, row.Asset!.Id); Assert.Equal(asset.Company, row.Asset.Company); Assert.Equal(asset.Ldos, row.Asset.Ldos); Assert.Equal("edited", row.Asset.Remark); Assert.Contains("unique Serial", row.Action);
    }

    [Fact] public async Task NonemptyUnlabelledCellsCannotBeSilentlyDiscarded()
    {
        string file = Path.Combine(_root, "unlabelled.xlsx");
        await InventoryWorkbook.WriteTableAsync(file, "NetStucked Inventory schema 2", ["Company", ""], [["owned", "must not disappear"]], default);
        Assert.Contains("no recognized column header", Assert.Single(await InventoryWorkbook.ReadAsync(file, [], default)).Error);
    }
}
