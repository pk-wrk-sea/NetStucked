using System.ComponentModel;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public sealed class UpdateTests
{
    private static readonly SemanticVersion Version = new(0, 2, 1);
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("installer test fixture; never executed");
    private static ReleaseInstaller Asset => new(123, "NetStucked-0.2.1-win-x64-setup.exe", new("https://github.com/pk-wrk-sea/NetStucked/releases/download/v0.2.1/NetStucked-0.2.1-win-x64-setup.exe"), Convert.ToHexStringLower(SHA256.HashData(Payload)), Payload.Length);
    [Theory]
    [InlineData("https://evil.invalid/file.exe")]
    [InlineData("http://github.com/pk-wrk-sea/NetStucked/releases/download/v0.2.1/NetStucked-0.2.1-win-x64-setup.exe")]
    [InlineData("https://github.com/other/NetStucked/releases/download/v0.2.1/NetStucked-0.2.1-win-x64-setup.exe")]
    [InlineData("https://github.com/pk-wrk-sea/NetStucked/releases/download/v0.2.0/NetStucked-0.2.1-win-x64-setup.exe")]
    [InlineData("https://user@github.com/pk-wrk-sea/NetStucked/releases/download/v0.2.1/NetStucked-0.2.1-win-x64-setup.exe")]
    public void InstallerSourceMustBeExactSelectedProjectBuild(string url) => Assert.Throws<InvalidDataException>(() => InstallerPolicy.Validate(Asset with { DownloadUri = new(url) }, Version));
    [Fact]
    public void RejectsMissingHashSizeForeignNameAndPrerelease()
    {
        foreach (var asset in new[] { Asset with { Sha256 = "" }, Asset with { Sha256 = new string('z', 64) }, Asset with { Bytes = 0 }, Asset with { Bytes = InstallerPolicy.MaximumBytes + 1 }, Asset with { Name = "other.exe" }, Asset with { Id = 0 } })
            Assert.Throws<InvalidDataException>(() => InstallerPolicy.Validate(asset, Version));
        Assert.Throws<InvalidDataException>(() => InstallerPolicy.Validate(Asset, Version with { PreRelease = "rc.1" }));
    }
    [Fact]
    public async Task DownloaderFollowsOnlyGitHubAssetRedirectsAndVerifiesActualBytes()
    {
        using var temp = new Temp();
        var handler = new DownloadHandler(request => request.RequestUri!.Host == "github.com" ? Redirect("https://release-assets.githubusercontent.com/owned/asset?token=temporary") : Content(Payload));
        using var downloader = new InstallerDownloader(handler);
        string path = Path.Combine(temp.Path, Asset.Name); var progress = new RecordedProgress();
        await downloader.DownloadAsync(Asset, Version, path, progress, CancellationToken.None);
        Assert.Equal(Payload, await File.ReadAllBytesAsync(path)); Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.False(r.Authorization)); Assert.Contains(progress.Items, p => p.Percent == 100);
        Assert.False(File.Exists(path + ".partial"));
    }
    [Theory]
    [InlineData("https://evil.invalid/payload")]
    [InlineData("http://release-assets.githubusercontent.com/payload")]
    [InlineData("https://github.com/other/project/releases/download/v1.0.0/file.exe")]
    [InlineData("https://release-assets.githubusercontent.com.evil.invalid/payload")]
    public async Task RejectsUnsafeRedirectBeforeFollowingIt(string target)
    {
        using var temp = new Temp(); var handler = new DownloadHandler(_ => Redirect(target)); using var downloader = new InstallerDownloader(handler);
        string path = Path.Combine(temp.Path, Asset.Name);
        await Assert.ThrowsAsync<InvalidDataException>(() => downloader.DownloadAsync(Asset, Version, path, new RecordedProgress(), CancellationToken.None));
        Assert.Single(handler.Requests); Assert.Empty(Directory.GetFiles(temp.Path));
    }
    [Theory]
    [InlineData("wrong-hash")][InlineData("truncated")][InlineData("oversized")][InlineData("redirect-loop")][InlineData("server-error")]
    public async Task IncompleteOrUnverifiedDownloadsNeverBecomeExecutableArtifacts(string failure)
    {
        using var temp = new Temp();
        var handler = new DownloadHandler(_ => failure switch
        {
            "truncated" => Content(Payload[..^1]), "oversized" => Content([.. Payload, 1]),
            "redirect-loop" => Redirect(Asset.DownloadUri.AbsoluteUri), "server-error" => new(HttpStatusCode.InternalServerError), _ => Content(Payload)
        });
        using var downloader = new InstallerDownloader(handler); string path = Path.Combine(temp.Path, Asset.Name);
        var asset = failure == "wrong-hash" ? Asset with { Sha256 = new string('0', 64) } : Asset;
        await Assert.ThrowsAnyAsync<Exception>(() => downloader.DownloadAsync(asset, Version, path, new RecordedProgress(), CancellationToken.None));
        Assert.False(File.Exists(path)); Assert.False(File.Exists(path + ".partial")); Assert.True(handler.Requests.Count <= 5);
    }
    [Fact]
    public async Task DownloadCancellationIsAwaitedAndPartialFileRemoved()
    {
        using var temp = new Temp(); using var cancellation = new CancellationTokenSource();
        using var downloader = new InstallerDownloader(new DownloadHandler(_ => Content(Payload)));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloader.DownloadAsync(Asset, Version, Path.Combine(temp.Path, Asset.Name), new RecordedProgress(), cancellation.Token));
        Assert.Empty(Directory.GetFiles(temp.Path));
    }
    [Fact]
    public async Task CancellationAfterWritingBytesRemovesPartialAndNeverPublishesInstaller()
    {
        using var temp = new Temp(); using var cancellation = new CancellationTokenSource();
        using var downloader = new InstallerDownloader(new DownloadHandler(_ => Content(Payload)));
        string path = Path.Combine(temp.Path, Asset.Name); bool partialWritten = false;
        var progress = new CallbackProgress(value =>
        {
            if (value.Bytes > 0) { partialWritten = File.Exists(path + ".partial"); cancellation.Cancel(); }
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloader.DownloadAsync(Asset, Version, path, progress, cancellation.Token));
        Assert.True(partialWritten); Assert.Empty(Directory.GetFiles(temp.Path));
    }
    [Fact]
    public async Task CatalogOffersExecutionOnlyWhenAssetDigestAndIdentityAreComplete()
    {
        object Row(string version, object asset) => new { tag_name = "v" + version, draft = false, prerelease = false, html_url = $"https://github.com/pk-wrk-sea/NetStucked/releases/tag/v{version}", name = version, assets = new[] { asset } };
        object valid = new { id = Asset.Id, name = Asset.Name, state = "uploaded", size = Asset.Bytes, digest = "sha256:" + Asset.Sha256, browser_download_url = Asset.DownloadUri };
        object incomplete = new { name = "NetStucked-0.2.0-win-x64-setup.exe" };
        using var source = new GitHubReleaseSource(new DownloadHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new[] { Row("0.2.0", incomplete), Row("0.2.1", valid) })) }));
        var result = await source.CheckAsync(CancellationToken.None);
        Assert.Equal(Asset, result.Releases[0].Installer); Assert.Null(result.Releases[1].Installer); Assert.NotNull(result.Releases[1].InstallerPage);
    }
    [Fact]
    public async Task BackupRetainsExactBomUnicodeBytesAndRestoresOnlyMatchingVersion()
    {
        using var temp = new Temp(); var backups = new UpdateBackups(temp.Path);
        byte[] original = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("{\"SchemaVersion\":1,\"TargetText\":\"localhost ไทย\"}")];
        await File.WriteAllBytesAsync(Path.Combine(temp.Path, "settings.json"), original);
        var backup = await backups.CreateAsync("0.2.0", CancellationToken.None); Assert.NotNull(backup);
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "settings.json"), "{\"SchemaVersion\":1,\"TargetText\":\"new\"}");
        Assert.False(await backups.RestoreForVersionAsync("0.2.1", CancellationToken.None));
        Assert.True(await backups.RestoreForVersionAsync("0.2.0", CancellationToken.None));
        Assert.Equal(original, await File.ReadAllBytesAsync(Path.Combine(temp.Path, "settings.json")));
        await File.AppendAllTextAsync(Path.Combine(backup!.Directory, "settings.json"), " ");
        Assert.Empty(await backups.ListAsync(CancellationToken.None));
    }
    [Theory]
    [InlineData("{\"SchemaVersion\":2}")][InlineData("{}")] [InlineData("[]")]
    public async Task IncompatiblePreferencesBlockBackupAndInstallation(string json)
    {
        using var temp = new Temp(); await File.WriteAllTextAsync(Path.Combine(temp.Path, "settings.json"), json);
        await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateBackups(temp.Path).CreateAsync("0.3.0", CancellationToken.None));
    }
    [Fact]
    public async Task RetentionBoundsVerifiedBackupsAndPreservesUnrelatedFiles()
    {
        using var temp = new Temp(); var backups = new UpdateBackups(temp.Path);
        string foreign = Path.Combine(temp.Path, "updates", "backups", "user-documents"); Directory.CreateDirectory(foreign);
        await File.WriteAllTextAsync(Path.Combine(foreign, "keep.txt"), "user file");
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "settings.json"), "{\"SchemaVersion\":1}");
        for (int i = 0; i < 23; i++) await backups.CreateAsync("0.3.0", CancellationToken.None);
        Assert.Equal(20, (await backups.ListAsync(CancellationToken.None)).Count); Assert.True(File.Exists(Path.Combine(foreign, "keep.txt")));
    }
    [Fact]
    public async Task RunnerWaitsForExitVerifiesBeforeInstallRestoresMatchingSettingsAndRestarts()
    {
        using var temp = new Temp(); var job = await PrepareJob(temp, "0.3.0", "0.2.1");
        var backups = new UpdateBackups(temp.Path); await backups.CreateAsync("0.2.1", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "settings.json"), "{\"SchemaVersion\":1,\"TargetText\":\"current\"}");
        var platform = new Platform { Version = "0.2.1" };
        var result = await new UpdateRunner(platform).RunAsync(JobPath(job), temp.Path, CancellationToken.None);
        Assert.True(result.Success); Assert.Equal(new[] { "validate", "wait", "verify", "install", "version", "restart-target" }, platform.Calls);
        Assert.Contains("fixture", await File.ReadAllTextAsync(Path.Combine(temp.Path, "settings.json")));
        Assert.Equal(2, (await backups.ListAsync(CancellationToken.None)).Count); Assert.Contains("Restored", result.Message);
    }
    [Fact]
    public async Task ModifiedInstallerOrTrustFailureCannotLaunchInstaller()
    {
        using var temp = new Temp(); var job = await PrepareJob(temp);
        await File.AppendAllTextAsync(UpdateFiles.InstallerPath(job), "tampered"); var platform = new Platform();
        var result = await new UpdateRunner(platform).RunAsync(JobPath(job), temp.Path, CancellationToken.None);
        Assert.False(result.Success); Assert.DoesNotContain("install", platform.Calls); Assert.Null(result.BackupDirectory);
        await File.WriteAllBytesAsync(UpdateFiles.InstallerPath(job), Payload); platform = new Platform { RejectTrust = true };
        result = await new UpdateRunner(platform).RunAsync(JobPath(job), temp.Path, CancellationToken.None);
        Assert.False(result.Success); Assert.DoesNotContain("install", platform.Calls);
    }
    [Theory]
    [InlineData(1)][InlineData(2)][InlineData(4)][InlineData(5)][InlineData(8)]
    public async Task InstallerFailuresPreserveBackupAndDoNotClaimSuccessfulRecovery(int exit)
    {
        using var temp = new Temp(); var job = await PrepareJob(temp); var platform = new Platform { ExitCode = exit };
        var result = await new UpdateRunner(platform).RunAsync(JobPath(job), temp.Path, CancellationToken.None);
        Assert.False(result.Success); Assert.NotNull(result.BackupDirectory); Assert.Contains($"exit {exit}", result.Message);
        Assert.DoesNotContain("version", platform.Calls); Assert.Contains("restart-source", platform.Calls);
    }
    [Fact]
    public async Task UacCancellationPreservesSettingsAndReturnsFailure()
    {
        using var temp = new Temp(); var job = await PrepareJob(temp); var platform = new Platform { CancelUac = true };
        var result = await new UpdateRunner(platform).RunAsync(JobPath(job), temp.Path, CancellationToken.None);
        Assert.False(result.Success); Assert.NotNull(result.BackupDirectory); Assert.Contains("restart-source", platform.Calls);
    }
    [Fact]
    public async Task RebootRequiredAndWrongInstalledVersionNeverReportSuccess()
    {
        using var temp = new Temp(); var job = await PrepareJob(temp); var platform = new Platform { ExitCode = 3010 };
        var result = await new UpdateRunner(platform).RunAsync(JobPath(job), temp.Path, CancellationToken.None);
        Assert.False(result.Success); Assert.True(result.RestartRequired); Assert.DoesNotContain(platform.Calls, c => c.StartsWith("restart"));
        platform = new Platform { Version = "9.9.9" }; result = await new UpdateRunner(platform).RunAsync(JobPath(job), temp.Path, CancellationToken.None);
        Assert.False(result.Success); Assert.Contains("does not match", result.Message);
    }
    [Fact]
    public async Task ForgedJobDirectoryAndForeignDataRootAreRejected()
    {
        using var temp = new Temp(); var job = await PrepareJob(temp); var platform = new Platform();
        await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateRunner(platform).RunAsync(JobPath(job), temp.Path + "-foreign", CancellationToken.None));
        var forged = job with { InstallDirectory = job.DataDirectory }; await UpdateFiles.WriteAsync(JobPath(job), forged, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateRunner(platform).RunAsync(JobPath(job), temp.Path, CancellationToken.None));
        Assert.Empty(platform.Calls);
    }
    [Fact]
    public async Task ParentExitFailureCannotStartInstallerOrDuplicateTheRunningApplication()
    {
        using var temp = new Temp(); var job = await PrepareJob(temp); var platform = new Platform { ParentTimeout = true };
        var result = await new UpdateRunner(platform).RunAsync(JobPath(job), temp.Path, CancellationToken.None);
        Assert.False(result.Success); Assert.DoesNotContain("install", platform.Calls); Assert.DoesNotContain(platform.Calls, c => c.StartsWith("restart"));
    }
    private static string JobPath(UpdateJob job) => Path.Combine(UpdateFiles.JobDirectory(job.DataDirectory, job.Id), UpdateFiles.JobFile);
    private static async Task<UpdateJob> PrepareJob(Temp temp, string current = "0.3.0", string target = "0.2.1")
    {
        var job = new UpdateJob(1, Guid.NewGuid(), current, target, Asset, new("https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.2.1"), temp.Path,
            temp.Path + "-install", Path.Combine(temp.Path, "source", "NetStucked.exe"), 1234, DateTime.UtcNow.Ticks, false);
        Directory.CreateDirectory(UpdateFiles.JobDirectory(job.DataDirectory, job.Id));
        await File.WriteAllBytesAsync(UpdateFiles.InstallerPath(job), Payload);
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "settings.json"), "{\"SchemaVersion\":1,\"TargetText\":\"fixture\"}");
        await UpdateFiles.WriteAsync(JobPath(job), job, CancellationToken.None); return job;
    }
    private static HttpResponseMessage Redirect(string url) { var r = new HttpResponseMessage(HttpStatusCode.Found); r.Headers.Location = new(url); return r; }
    private static HttpResponseMessage Content(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    private sealed class DownloadHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(Uri Url, bool Authorization)> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { token.ThrowIfCancellationRequested(); Requests.Add((request.RequestUri!, request.Headers.Authorization is not null)); return Task.FromResult(respond(request)); }
    }
    private sealed class RecordedProgress : IProgress<UpdateProgress> { public List<UpdateProgress> Items { get; } = []; public void Report(UpdateProgress value) => Items.Add(value); }
    private sealed class CallbackProgress(Action<UpdateProgress> report) : IProgress<UpdateProgress> { public void Report(UpdateProgress value) => report(value); }
    private sealed class Platform : IUpdatePlatform
    {
        public List<string> Calls { get; } = []; public int ExitCode; public bool CancelUac, RejectTrust, ParentTimeout; public string Version = "0.2.1";
        public void ValidateInstallation(UpdateJob job) => Calls.Add("validate");
        public Task WaitForParentAsync(UpdateJob job, CancellationToken token) { Calls.Add("wait"); token.ThrowIfCancellationRequested(); if (ParentTimeout) throw new TimeoutException("parent still running"); return Task.CompletedTask; }
        public Task VerifyInstallerAsync(UpdateJob job, CancellationToken token) { Calls.Add("verify"); if (RejectTrust) throw new InvalidDataException("signature rejected"); return Task.CompletedTask; }
        public Task<int> InstallAsync(UpdateJob job, CancellationToken token) { Calls.Add("install"); if (CancelUac) throw new Win32Exception(1223); return Task.FromResult(ExitCode); }
        public string InstalledVersion(UpdateJob job) { Calls.Add("version"); return Version; }
        public void Restart(UpdateJob job, bool installed) => Calls.Add(installed ? "restart-target" : "restart-source");
    }
    private sealed class Temp : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NetStucked-update-tests-" + Guid.NewGuid().ToString("N"));
        public Temp() => Directory.CreateDirectory(Path);
        public void Dispose() { UpdateFiles.NoLinks(Path); if (Directory.Exists(Path)) Directory.Delete(System.IO.Path.GetFullPath(Path), true); }
    }
}
