using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text.Json;
using NetStucked.Core;
using NetStucked.Desktop.ViewModels;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;

internal static partial class Program
{
    private static async Task<int> VerifyPublishedInstallerAsync(string output)
    {
        Directory.CreateDirectory(output);
        try
        {
            using var token = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            using var source = new GitHubReleaseSource();
            var catalog = await source.CheckAsync(token.Token);
            var release = catalog.Releases.First(r => r.Installer is not null);
            var store = new UserSettingsStore(Path.Combine(output, "isolated-user-data"));
            store.Preferences.TargetText = "127.0.0.1 QA ไทย";
            // Seed the authorized 0.4.1 continuous/multiple-target defaults before comparing updater preservation.
            // Opening the real app otherwise intentionally migrates the legacy false mode flags on shutdown.
            store.Preferences.Port = store.Preferences.Port with { Continuous = true };
            store.Preferences.PortMultipleTargets = store.Preferences.PortContinuous = true;
            await store.SaveAsync(token.Token);
            byte[] settings = await File.ReadAllBytesAsync(Path.Combine(store.DirectoryPath, "settings.json"), token.Token);
            using var updater = new WindowsApplicationUpdateService(store);
            var progress = new Progress<UpdateProgress>(p => { if (p.TotalBytes == 0) Console.WriteLine(p.Stage); });
            string current = FileVersionInfo.GetVersionInfo(Path.Combine(AppContext.BaseDirectory, "NetStucked.exe")).ProductVersion!.Split('+')[0];
            var staged = await updater.StageAsync(release, current, true, progress, token.Token);
            try
            {
                var job = await UpdateFiles.ReadAsync<UpdateJob>(staged.JobPath, 16384, token.Token);
                await UpdateFiles.VerifyHashAsync(UpdateFiles.InstallerPath(job), job.Installer, token.Token);
                uint signature = Authenticode.Check(UpdateFiles.InstallerPath(job));
                Require(signature == 0x800B0100, "Actual published test installer is unsigned according to Windows WinVerifyTrust");
                bool rejected = false;
                try { await new WindowsUpdatePlatform().VerifyInstallerAsync(job with { AllowUnsigned = false }, token.Token); }
                catch (InvalidDataException ex) when (ex.Message.Contains("unsigned")) { rejected = true; }
                Require(rejected, "Actual unsigned GitHub installer is rejected without acknowledgement");
                Require((await File.ReadAllTextAsync(UpdateFiles.InstallerPath(job) + ":Zone.Identifier", token.Token)).Contains("ZoneId=3"), "Downloaded installer retains Internet Mark of the Web");
                foreach (string file in new[] { "NetStucked.exe", "NetStucked.dll", "NetStucked.Core.dll", "NetStucked.Infrastructure.dll" })
                {
                    byte[] original = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, file), token.Token);
                    byte[] helper = await File.ReadAllBytesAsync(Path.Combine(Path.GetDirectoryName(staged.JobPath)!, "helper", file), token.Token);
                    Require(SHA256.HashData(original).SequenceEqual(SHA256.HashData(helper)), "Restart helper copies exact application bytes: " + file);
                }
                await CheckNativeHelperReadyAsync(updater, staged, job, token.Token);
                await File.WriteAllTextAsync(Path.Combine(output, "actual-installer-verification.json"), JsonSerializer.Serialize(new
                {
                    Source = "Actual public GitHub release asset and Windows trust APIs; no fixture", Version = release.Version.ToString(), job.Installer,
                    SignatureStatus = $"0x{signature:X8}", HashAndSizeVerified = true, InternetZonePreserved = true,
                    UnsignedRejectedWithoutAcknowledgement = rejected, AcceptedWithAcknowledgement = true,
                    NativeHelperAcknowledged = true, HelperStoppedWhileParentRemainedAlive = true, InstallerExecuted = false
                }, new JsonSerializerOptions { WriteIndented = true }), token.Token);
            }
            finally { await updater.DiscardAsync(staged); }
            Require(!Directory.Exists(Path.GetDirectoryName(staged.JobPath)), "Discard removes only the staged job and helper");
            byte[] finalSettings = await File.ReadAllBytesAsync(Path.Combine(store.DirectoryPath, "settings.json"), token.Token);
            Require(settings.SequenceEqual(finalSettings), "Verification does not change user settings or run an installer");
            Console.WriteLine("PUBLISHED INSTALLER VERIFICATION PASSED; installer execution was not attempted.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex); return 1; }
    }

    private static async Task CheckNativeHelperReadyAsync(WindowsApplicationUpdateService updater, StagedUpdate staged, UpdateJob job, CancellationToken token)
    {
        // Exercise real app/helper startup without ever allowing Setup: this verified unsigned file is explicitly disallowed in the handoff.
        var start = new ProcessStartInfo(job.SourceExecutable) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = AppContext.BaseDirectory };
        start.Environment["NETSTUCKED_QA_DATA_DIRECTORY"] = job.DataDirectory;
        start.Environment["DOTNET_ROOT"] = Path.GetDirectoryName(Environment.ProcessPath)!;
        using var parent = Process.Start(start) ?? throw new IOException("QA application did not start.");
        Process? helper = null; string? previousData = Environment.GetEnvironmentVariable("NETSTUCKED_QA_DATA_DIRECTORY");
        string? previousRuntime = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        try
        {
            var timer = Stopwatch.StartNew();
            while (parent.MainWindowHandle == 0)
            {
                if (parent.HasExited || timer.Elapsed > TimeSpan.FromSeconds(15)) throw new IOException("Actual QA application did not open its WPF window.");
                await Task.Delay(100, token); parent.Refresh();
            }
            job = job with { ParentId = parent.Id, ParentStartedUtcTicks = parent.StartTime.ToUniversalTime().Ticks, AllowUnsigned = false };
            await UpdateFiles.WriteAsync(staged.JobPath, job, token);
            Environment.SetEnvironmentVariable("NETSTUCKED_QA_DATA_DIRECTORY", job.DataDirectory);
            Environment.SetEnvironmentVariable("DOTNET_ROOT", Path.GetDirectoryName(Environment.ProcessPath)!);
            await updater.StartHelperAsync(staged, token);
            var ready = await UpdateFiles.ReadAsync<HelperReady>(Path.Combine(Path.GetDirectoryName(staged.JobPath)!, "helper-ready.json"), 4096, token);
            helper = Process.GetProcessById(ready.HelperId);
            Require(UpdateFiles.SamePath(helper.MainModule!.FileName, Path.Combine(Path.GetDirectoryName(staged.JobPath)!, "helper", "NetStucked.exe")), "Real helper acknowledges startup from the isolated application directory");
            await Task.Delay(400, token);
            Require(!parent.HasExited && !helper.HasExited && !File.Exists(Path.Combine(Path.GetDirectoryName(staged.JobPath)!, "installer.log")) && !Directory.Exists(Path.Combine(job.DataDirectory, "updates", "backups")), "Real helper waits for the original application to exit before backup or Setup");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETSTUCKED_QA_DATA_DIRECTORY", previousData);
            Environment.SetEnvironmentVariable("DOTNET_ROOT", previousRuntime);
            // Keep the parent alive until its owned helper has exited. No installer is killed or run by this harness.
            if (helper is not null && !helper.HasExited && !parent.HasExited) { helper.Kill(); await helper.WaitForExitAsync(CancellationToken.None); }
            helper?.Dispose();
            if (!parent.HasExited)
            {
                _ = PostUpdateQaMessage(parent.MainWindowHandle, 0x10, 0, 0);
                try { await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (TimeoutException) { parent.Kill(); await parent.WaitForExitAsync(); throw; }
            }
        }
    }
    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    private static extern bool PostUpdateQaMessage(nint window, uint message, nint wParam, nint lParam);

    private static async Task CheckSelectedBuildAsync(MainViewModel vm, UserSettingsStore store, MeasuredRealProbe icmp, MeasuredRealTcp tcp)
    {
        var fixture = new ReleaseFixture(); var updater = new RecordingUpdater();
        await using var update = new UpdatesViewModel(fixture, new RecordingLinks(), updater);
        SemanticVersion.TryParse(update.CurrentVersion, out var current);
        PublishedRelease Build(SemanticVersion version, bool complete = true)
        {
            var page = new Uri($"https://github.com/pk-wrk-sea/NetStucked/releases/tag/v{version}");
            var asset = new ReleaseInstaller(123, $"NetStucked-{version}-win-x64-setup.exe", new Uri($"https://github.com/pk-wrk-sea/NetStucked/releases/download/v{version}/NetStucked-{version}-win-x64-setup.exe"), new string('a', 64), 1);
            return new(version, "TEST FIXTURE", "Fixture notes only", page, page, DateTimeOffset.UtcNow, complete ? asset : null);
        }
        var newer = Build(new(current!.Major, current.Minor + 1, 0));
        var same = Build(current); var older = Build(new(0, 2, 1)); var incomplete = Build(new(0, 1, 0), false);
        fixture.Result = new([newer, same, older, incomplete], DateTimeOffset.UtcNow);
        int stops = 0; update.StopDiagnosticsAsync = () => { stops++; return Task.CompletedTask; };
        await update.CheckCommand.ExecuteAsync(null);
        Require(update.SelectedBuild == newer && update.InstallLabel == $"Install v{newer.Version}" && update.InstallCommand.CanExecute(null), "Selected-build fixture offers Install for a newer eligible build");
        update.SelectedBuild = same; Require(update.InstallLabel.StartsWith("Reinstall"), "Selecting the running version offers Reinstall");
        update.SelectedBuild = older; Require(update.InstallLabel.StartsWith("Recover"), "Selecting an older build offers Recovery");
        update.SelectedBuild = incomplete; Require(!update.InstallCommand.CanExecute(null), "Missing installer digest metadata disables execution");
        update.SelectedBuild = newer; updater.Block = true;
        var pending = update.InstallCommand.ExecuteAsync(null);
        Require(update.IsInstalling && !update.CanChooseBuild && !update.CheckCommand.CanExecute(null) && !update.InstallCommand.CanExecute(null), "Download preparation prevents selection changes and duplicate commands");
        await update.InstallCommand.ExecuteAsync(null);
        update.CancelInstallCommand.Execute(null); await pending;
        Require(updater.Stages == 1 && updater.Helpers == 0 && stops == 0 && update.InstallStatus.Contains("cancelled"), "Cancel stops preparation without diagnostics shutdown or installer handoff");
        updater.Block = false; updater.FailHandoff = true;
        var locks = new List<bool>(); update.InstallationLockChanged += locks.Add;
        await update.InstallCommand.ExecuteAsync(null);
        Require(updater.Discards == 1 && locks.SequenceEqual(new[] { true, false }) && update.InstallCommand.CanExecute(null), "Failed helper startup discards the staged job, unlocks the window and permits retry");

        using var farm = new LoopbackTcpFarm(2);
        vm.Ping.TargetText = "127.0.0.1 Update shutdown QA"; await vm.Ping.StartCommand.ExecuteAsync(null);
        SetPortFarm(vm.Port, farm.Targets); await vm.Port.StartCommand.ExecuteAsync(null);
        var first = vm.Trace; first.Target = "127.0.1.1"; await first.StartCommand.ExecuteAsync(null);
        vm.TraceWorkspace.AddSessionCommand.Execute(null); var second = vm.Trace; second.Target = "127.0.2.1"; await second.StartCommand.ExecuteAsync(null);
        await Until(() => { vm.Ping.Refresh(); vm.Port.Refresh(); return vm.Ping.Sent > 0 && first.Engine.CompletedCycles > 0 && second.Engine.CompletedCycles > 0 && vm.Port.Sent > 0; });
        update.StopDiagnosticsAsync = vm.PrepareForUpdateAsync;
        updater.FailHandoff = false; bool handedOff = false;
        updater.BeforeHandoff = () => Require(vm.Ping.State == SessionState.Stopped && vm.Port.State == SessionState.Stopped && vm.TraceWorkspace.Sessions.All(s => s.State == SessionState.Stopped) && icmp.Active == 0 && tcp.Active == 0 && File.Exists(Path.Combine(store.DirectoryPath, "settings.json")), "Actual Ping, TCP and two independent trace sessions drain and settings save before helper handoff");
        update.HandoffReady += () => handedOff = true;
        update.SelectedBuild = older; update.AllowUnsignedInstaller = true;
        await update.InstallCommand.ExecuteAsync(null);
        Require(handedOff && updater.Release == older && updater.AllowUnsigned && !update.InstallCommand.CanExecute(null) && !update.CheckCommand.CanExecute(null), "Handoff uses the selected recovery build and explicit unsigned acknowledgement exactly once");
        await vm.TraceWorkspace.RemoveSessionCommand.ExecuteAsync(second);
        vm.TraceWorkspace.SelectedSession = first;
    }

    private sealed class RecordingUpdater : IApplicationUpdateService
    {
        public string InstallationDetails => "Test fixture: no executable is launched";
        public int Stages, Helpers, Discards;
        public bool Block, FailHandoff, AllowUnsigned;
        public PublishedRelease? Release;
        public Action? BeforeHandoff;
        public async Task<StagedUpdate> StageAsync(PublishedRelease release, string currentVersion, bool allowUnsigned, IProgress<UpdateProgress> progress, CancellationToken token)
        {
            Stages++; Release = release; AllowUnsigned = allowUnsigned;
            if (Block) await Task.Delay(Timeout.Infinite, token);
            return new("test-only-job.json");
        }
        public Task StartHelperAsync(StagedUpdate staged, CancellationToken token) { if (FailHandoff) throw new IOException("fixture helper unavailable"); Helpers++; BeforeHandoff?.Invoke(); return Task.CompletedTask; }
        public Task DiscardAsync(StagedUpdate staged) { Discards++; return Task.CompletedTask; }
        public Task<IReadOnlyList<SettingsBackupInfo>> GetBackupsAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<SettingsBackupInfo>>([]);
        public Task<UpdateResult?> GetLastResultAsync(CancellationToken token) => Task.FromResult<UpdateResult?>(null);
    }
}
