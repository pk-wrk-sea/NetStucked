using System.Security.Cryptography;
using System.Text.Json;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public sealed class EngineeringStoreContentionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NetStucked-owned-store-lock-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private string CurrentPath => Path.Combine(_root, "engineering", "workspace-v1.json");
    private async Task<(EngineeringStore Store, EngineeringData Before, EngineeringData Changed)> SeedAsync()
    {
        var store = new EngineeringStore(new UserSettingsStore(_root));
        var asset = new Asset(Guid.NewGuid(), "Owned store fixture", "192.0.2.1", "Cisco", "", "", "initial", "", "", "", null, null, DateTimeOffset.UtcNow);
        var initial = await store.SaveAsync(EngineeringData.Empty with { Assets = [asset] }, 0, default);
        var before = await store.SaveAsync(initial with { Assets = [asset with { Site = "before" }] }, initial.Revision, default);
        return (store, before, before with { Assets = [asset with { Site = "after" }] });
    }
    private static FileStream HoldRename(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    private async Task AwaitWriteAttemptAsync(Task save)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!save.IsCompleted && !Directory.EnumerateFiles(Path.Combine(_root, "engineering"), "*.tmp").Any())
            await Task.Delay(5, deadline.Token);
    }

    [Theory]
    [InlineData("workspace-v1.json")]
    [InlineData("workspace-v1.previous.json")]
    public async Task TemporaryRenameLockRetriesWithoutDuplicatingRevisionOrAudit(string lockedFile)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (store, before, changed) = await SeedAsync();
        var held = HoldRename(Path.Combine(_root, "engineering", lockedFile));
        var saving = store.SaveAsync(changed, before.Revision, default);
        try { await AwaitWriteAttemptAsync(saving); await Task.Delay(150); }
        finally { held.Dispose(); }
        var saved = await saving.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(before.Revision + 1, saved.Revision);
        Assert.Equal(before.Changes!.Length + 1, saved.Changes!.Length);
        Assert.Equal("after", Assert.Single(saved.Assets).Site);
        Assert.Equal(saved.Revision, (await store.LoadAsync(default)).Revision);
        var backup = JsonSerializer.Deserialize<EngineeringData>(await File.ReadAllTextAsync(Path.Combine(_root, "engineering", "workspace-v1.previous.json")))!;
        Assert.Equal(before.Revision, backup.Revision); Assert.Equal("before", Assert.Single(backup.Assets).Site);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, "engineering"), "*.tmp*"));
    }

    [Theory]
    [InlineData("workspace-v1.json")]
    [InlineData("workspace-v1.previous.json")]
    public async Task PermanentRenameLockFailsBoundedlyWithoutOverwritingAndGateRemainsUsable(string lockedFile)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (store, before, changed) = await SeedAsync();
        byte[] originalHash = SHA256.HashData(await File.ReadAllBytesAsync(CurrentPath));
        using (var held = HoldRename(Path.Combine(_root, "engineering", lockedFile)))
        {
            var error = await Record.ExceptionAsync(() => store.SaveAsync(changed, before.Revision, default).WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.True(error is IOException or UnauthorizedAccessException, "Permanent rename denial must fail within its bounded retry window: " + error?.GetType().Name);
            Assert.Equal(originalHash, SHA256.HashData(await File.ReadAllBytesAsync(CurrentPath)));
            Assert.Equal(before.Revision, (await store.LoadAsync(default).WaitAsync(TimeSpan.FromSeconds(2))).Revision);
        }
        var saved = await store.SaveAsync(changed, before.Revision, default).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(before.Revision + 1, saved.Revision);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, "engineering"), "*.tmp*"));
    }

    [Theory]
    [InlineData("workspace-v1.json")]
    [InlineData("workspace-v1.previous.json")]
    public async Task CancellationDuringRenameRetryRetainsOriginalAndReleasesGate(string lockedFile)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (store, before, changed) = await SeedAsync();
        byte[] originalHash = SHA256.HashData(await File.ReadAllBytesAsync(CurrentPath));
        using var cancel = new CancellationTokenSource();
        using (var held = HoldRename(Path.Combine(_root, "engineering", lockedFile)))
        {
            var saving = store.SaveAsync(changed, before.Revision, cancel.Token);
            await AwaitWriteAttemptAsync(saving); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => saving.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.Equal(originalHash, SHA256.HashData(await File.ReadAllBytesAsync(CurrentPath)));
            Assert.Equal(before.Revision, (await store.LoadAsync(default).WaitAsync(TimeSpan.FromSeconds(2))).Revision);
        }
        var saved = await store.SaveAsync(changed, before.Revision, default).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(before.Revision + 1, saved.Revision);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, "engineering"), "*.tmp*"));
    }
}
