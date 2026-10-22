using System.Reflection;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

[Collection("Exclusive timing and bulk fixtures")]
public sealed class UpdateHelperPayloadTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NetStucked-helper-payload-" + Guid.NewGuid().ToString("N"));
    private string Source => Path.Combine(_root, "source");
    private string Destination => Path.Combine(_root, "destination");
    public UpdateHelperPayloadTests() { Directory.CreateDirectory(Source); File.WriteAllText(Path.Combine(Source, "NetStucked.exe"), "owned fixture executable marker"); }
    private Task Copy(CancellationToken token = default) => (Task)typeof(WindowsApplicationUpdateService).GetMethod("CopyHelperAsync", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [Source, Destination, token])!;
    private void Write(string name, string text = "owned fixture") { string path = Path.Combine(Source, name); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, text); }

    [Fact]
    public async Task BundledCollectorFilesAreExcludedButPrivateDotnetAndAssetsRemain()
    {
        for (int i = 0; i < 2250; i++) Write($"CollectorRuntime/site-packages/template-{i}.txt");
        Write("NetStucked.dll"); Write("NetStucked.runtimeconfig.json"); Write("System.Private.CoreLib.dll"); Write("hostfxr.dll"); Write("Resources/ThirdParty/notice.txt");
        await Copy();
        Assert.False(Directory.Exists(Path.Combine(Destination, "CollectorRuntime")));
        Assert.Equal(6, Directory.GetFiles(Destination, "*", SearchOption.AllDirectories).Length);
        Assert.Equal(File.ReadAllText(Path.Combine(Source, "System.Private.CoreLib.dll")), File.ReadAllText(Path.Combine(Destination, "System.Private.CoreLib.dll")));
        Assert.True(File.Exists(Path.Combine(Source, "CollectorRuntime/site-packages/template-2249.txt")));
    }
    [Fact]
    public async Task OnlyTheRootCollectorDirectoryIsExcluded()
    {
        Write("Resources/CollectorRuntime/required.txt"); Write("CollectorRuntime", "a file with that name is not a directory");
        await Copy();
        Assert.True(File.Exists(Path.Combine(Destination, "Resources/CollectorRuntime/required.txt")));
        Assert.Equal("a file with that name is not a directory", File.ReadAllText(Path.Combine(Destination, "CollectorRuntime")));
    }
    [Fact]
    public async Task ExistingFileCountBoundStillRejectsAnOversizedDotnetPayload()
    {
        for (int i = 0; i < 1000; i++) Write($"required-{i}.dll");
        await Assert.ThrowsAsync<IOException>(() => Copy());
        Assert.Empty(Directory.GetFiles(Destination));
    }
    [Fact]
    public async Task CancellationStillPreventsCopying()
    {
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Copy(stop.Token));
        Assert.Empty(Directory.GetFiles(Destination));
    }
    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
