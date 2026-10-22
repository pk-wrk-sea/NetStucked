using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

[Collection("Exclusive timing and bulk fixtures")]
public sealed class InventoryPerformanceRegressionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NetStucked-inventory-regression-" + Guid.NewGuid().ToString("N"));
    public InventoryPerformanceRegressionTests() => Directory.CreateDirectory(_root);
    private static Asset Component(int i) => new(Guid.NewGuid(), "", "192.0.2.1", "Cisco", "Module", "SERIAL-" + i, "", "", "Line card", "", null, null, DateTimeOffset.UtcNow.AddDays(-1));

    [Fact]
    public async Task UnchangedRoundtripRetainsModifiedTimeAndDoesNotInflateAudit()
    {
        var store = new EngineeringStore(new UserSettingsStore(_root)); var original = Component(1);
        var first = await store.SaveAsync(EngineeringData.Empty with { Assets = [original] }, 0, default);
        string workbook = Path.Combine(_root, "unchanged.xlsx"); await InventoryWorkbook.WriteAsync(workbook, first.Assets, default);
        var preview = await InventoryWorkbook.ReadAsync(workbook, first.Assets, default);
        Assert.Equal(original, Assert.Single(preview).Asset);
        var saved = await store.SaveAsync(first with { Assets = preview.Select(p => p.Asset!).ToArray() }, first.Revision, default);
        Assert.Single(saved.Changes!); Assert.Equal(original.ModifiedAt, saved.Assets[0].ModifiedAt);
    }

    [Fact]
    public async Task ChangedRoundtripStillRecordsActualBeforeAndAfter()
    {
        var store = new EngineeringStore(new UserSettingsStore(_root)); var original = Component(1);
        var first = await store.SaveAsync(EngineeringData.Empty with { Assets = [original] }, 0, default);
        string workbook = Path.Combine(_root, "edited.xlsx"); await InventoryWorkbook.WriteAsync(workbook, [original with { Vendor = "Huawei" }], default);
        var preview = await InventoryWorkbook.ReadAsync(workbook, first.Assets, default); var edited = Assert.Single(preview).Asset!;
        Assert.Equal(original.Id, edited.Id); Assert.Equal("Huawei", edited.Vendor); Assert.True(edited.ModifiedAt > original.ModifiedAt);
        var saved = await store.SaveAsync(first with { Assets = [edited] }, first.Revision, default);
        var change = Assert.Single(saved.Changes!, c => c.Before is not null); Assert.Equal(original, change.Before); Assert.Equal(edited, change.After);
    }

    [Fact]
    public async Task IndexedLargePreviewMaintainsIdentityAndRejectsAllConflictingSerialRows()
    {
        var assets = Enumerable.Range(1, 2000).Select(Component).ToArray();
        string workbook = Path.Combine(_root, "scale.xlsx"); await InventoryWorkbook.WriteAsync(workbook, assets, default);
        var preview = await InventoryWorkbook.ReadAsync(workbook, assets, default);
        Assert.Equal(2000, preview.Length); Assert.All(preview, p => Assert.Empty(p.Error));
        Assert.Equal(assets, preview.Select(p => p.Asset).ToArray());
        await InventoryWorkbook.WriteAsync(workbook, [assets[0] with { Serial = assets[1].Serial }, assets[1]], default);
        preview = await InventoryWorkbook.ReadAsync(workbook, assets, default);
        Assert.All(preview, p => { Assert.Null(p.Asset); Assert.NotEmpty(p.Error); });
    }
    public void Dispose() => Directory.Delete(_root, true);
}
