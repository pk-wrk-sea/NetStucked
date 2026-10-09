using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Microsoft.Win32;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

[SupportedOSPlatform("windows")]
public sealed class WindowsApplicationUpdateService(UserSettingsStore store) : IApplicationUpdateService, IDisposable
{
    private readonly InstallerDownloader _downloader = new();
    public string InstallationDetails => $"Install to {WindowsUpdatePlatform.InstallDirectory()}. Windows may request permission. Unsigned builds require the acknowledgement below.";
    public async Task<StagedUpdate> StageAsync(PublishedRelease release, string currentVersion, bool allowUnsigned, IProgress<UpdateProgress> progress, CancellationToken token)
    {
        if (release.Installer is null) throw new InvalidDataException("This build has no installer with complete GitHub SHA-256 metadata.");
        InstallerPolicy.Validate(release.Installer, release.Version);
        if (store.LoadError is not null) throw new InvalidDataException("Resolve the preferences load error before installing another build; the original settings must be preserved.");
        string settingsPath = Path.Combine(store.DirectoryPath, "settings.json");
        if (File.Exists(settingsPath)) _ = await UpdateBackups.ReadSettingsAsync(settingsPath, token).ConfigureAwait(false);
        Guid id = Guid.NewGuid(); string directory = UpdateFiles.JobDirectory(store.DirectoryPath, id);
        UpdateFiles.NoLinks(directory);
        await Task.Run(() => CleanCompletedJobs(store.DirectoryPath), token).ConfigureAwait(false);
        Directory.CreateDirectory(directory);
        try
        {
            using var parent = Process.GetCurrentProcess();
            var job = new UpdateJob(1, id, currentVersion, release.Version.ToString(), release.Installer, release.Page, Path.GetFullPath(store.DirectoryPath),
                WindowsUpdatePlatform.InstallDirectory(), Path.Combine(AppContext.BaseDirectory, "NetStucked.exe"), parent.Id, parent.StartTime.ToUniversalTime().Ticks, allowUnsigned);
            string jobPath = Path.Combine(directory, UpdateFiles.JobFile); UpdateFiles.ValidateJob(job, jobPath, store.DirectoryPath);
            WindowsUpdatePlatform.ValidateSpace(directory, release.Installer.Bytes * 8 + 256 * 1024 * 1024);
            await _downloader.DownloadAsync(release.Installer, release.Version, UpdateFiles.InstallerPath(job), progress, token).ConfigureAwait(false);
            await File.WriteAllTextAsync(UpdateFiles.InstallerPath(job) + ":Zone.Identifier", $"[ZoneTransfer]\r\nZoneId=3\r\nHostUrl={release.Installer.DownloadUri.AbsoluteUri}\r\n", token).ConfigureAwait(false);
            progress.Report(new("Checking Windows signature and installer version"));
            var platform = new WindowsUpdatePlatform();
            await platform.VerifyInstallerAsync(job, token).ConfigureAwait(false);
            progress.Report(new("Preparing restart helper"));
            string helper = Path.Combine(directory, "helper");
            await CopyHelperAsync(AppContext.BaseDirectory, helper, token).ConfigureAwait(false);
            await UpdateFiles.WriteAsync(jobPath, job, token).ConfigureAwait(false);
            progress.Report(new("Ready to stop diagnostics and install"));
            return new(jobPath);
        }
        catch { DeleteOwnedJob(store.DirectoryPath, directory); throw; }
    }
    public async Task StartHelperAsync(StagedUpdate staged, CancellationToken token)
    {
        var job = await UpdateFiles.ReadAsync<UpdateJob>(staged.JobPath, 16384, token).ConfigureAwait(false);
        UpdateFiles.ValidateJob(job, staged.JobPath, store.DirectoryPath);
        string helper = Path.Combine(UpdateFiles.JobDirectory(store.DirectoryPath, job.Id), "helper", "NetStucked.exe");
        var start = new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(helper)! };
        start.ArgumentList.Add("--apply-update"); start.ArgumentList.Add(staged.JobPath);
        token.ThrowIfCancellationRequested();
        using var process = Process.Start(start) ?? throw new IOException("Could not start the update helper.");
        string ready = Path.Combine(UpdateFiles.JobDirectory(store.DirectoryPath, job.Id), "helper-ready.json");
        try
        {
            var timer = Stopwatch.StartNew();
            while (!File.Exists(ready))
            {
                token.ThrowIfCancellationRequested();
                if (process.HasExited) throw new IOException($"Update helper exited before acknowledging the handoff (exit {process.ExitCode}). NetStucked will remain open.");
                if (timer.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("Update helper did not acknowledge startup. NetStucked will remain open.");
                await Task.Delay(100, token).ConfigureAwait(false);
            }
            var acknowledgement = await UpdateFiles.ReadAsync<HelperReady>(ready, 4096, token).ConfigureAwait(false);
            if (acknowledgement.Id != job.Id || acknowledgement.ParentId != job.ParentId || acknowledgement.HelperId != process.Id || process.HasExited) throw new IOException("Invalid update helper acknowledgement.");
        }
        catch
        {
            // Parent is still alive here; the helper cannot yet launch Setup. Never kill an installer.
            if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); }
            throw;
        }
    }
    public Task DiscardAsync(StagedUpdate staged) => Task.Run(() => DeleteOwnedJob(store.DirectoryPath, Path.GetDirectoryName(staged.JobPath)!));
    public Task<IReadOnlyList<SettingsBackupInfo>> GetBackupsAsync(CancellationToken token) => new UpdateBackups(store.DirectoryPath).ListAsync(token);
    public async Task<UpdateResult?> GetLastResultAsync(CancellationToken token)
    {
        string path = Path.Combine(store.DirectoryPath, "updates", "last-result.json");
        return File.Exists(path) ? await UpdateFiles.ReadAsync<UpdateResult>(path, 16384, token).ConfigureAwait(false) : null;
    }
    private static async Task CopyHelperAsync(string source, string destination, CancellationToken token)
    {
        UpdateFiles.NoLinks(source); UpdateFiles.NoLinks(destination); Directory.CreateDirectory(destination);
        // Copy the app and its private .NET runtime so the helper survives replacement of Program Files.
        var safeFiles = new List<string>(); var folders = new Queue<string>(); folders.Enqueue(source); int entries = 0;
        while (folders.TryDequeue(out string? folder))
            foreach (string entry in Directory.EnumerateFileSystemEntries(folder))
            {
                token.ThrowIfCancellationRequested(); UpdateFiles.NoLinks(entry);
                if (++entries > 4096) throw new IOException("Application helper contains too many entries.");
                if (Directory.Exists(entry)) folders.Enqueue(entry); else safeFiles.Add(entry);
            }
        var files = safeFiles.ToArray();
        if (files.Length > 1000 || files.Sum(f => new FileInfo(f).Length) > 1024L * 1024 * 1024) throw new IOException("Application helper payload exceeds the copy limit.");
        foreach (string file in files)
        {
            token.ThrowIfCancellationRequested(); UpdateFiles.NoLinks(file);
            string output = Path.Combine(destination, Path.GetRelativePath(source, file));
            if (!UpdateFiles.IsUnder(destination, output)) throw new IOException("Invalid helper file path.");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
            await using var target = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
            await input.CopyToAsync(target, token).ConfigureAwait(false);
        }
        if (!File.Exists(Path.Combine(destination, "NetStucked.exe"))) throw new IOException("Application executable is missing from the helper.");
    }
    private static void CleanCompletedJobs(string data)
    {
        string root = Path.Combine(data, "updates", "jobs"); UpdateFiles.NoLinks(root);
        if (!Directory.Exists(root)) return;
        var completed = Directory.EnumerateDirectories(root).Take(1000).Where(d => Guid.TryParseExact(Path.GetFileName(d), "N", out _) && File.Exists(Path.Combine(d, "completed.json"))).OrderByDescending(Directory.GetLastWriteTimeUtc).ToArray();
        foreach (string old in completed.Skip(2)) DeleteOwnedJob(data, old);
    }
    private static void DeleteOwnedJob(string data, string directory)
    {
        string root = Path.Combine(data, "updates", "jobs");
        if (!UpdateFiles.IsUnder(root, directory) || !UpdateFiles.SamePath(Path.GetDirectoryName(directory)!, root) || !Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) throw new IOException("Invalid update cleanup path.");
        if (!Directory.Exists(directory)) return;
        UpdateFiles.NoLinks(directory);
        // Check every directory before recursive deletion; never follow a reparse point.
        var folders = new Queue<string>(); folders.Enqueue(directory);
        while (folders.TryDequeue(out var folder))
            foreach (string entry in Directory.EnumerateFileSystemEntries(folder)) { UpdateFiles.NoLinks(entry); if (Directory.Exists(entry)) folders.Enqueue(entry); }
        Directory.Delete(Path.GetFullPath(directory), true);
    }
    public void Dispose() => _downloader.Dispose();
}

[SupportedOSPlatform("windows")]
public sealed class WindowsUpdatePlatform : IUpdatePlatform
{
    public static string InstallDirectory()
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{82CA4B18-541C-4D04-9A09-E676AB21F504}_is1");
        string path = key?.GetValue("InstallLocation") as string ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NetStucked");
        path = Path.GetFullPath(path.TrimEnd('\\'));
        if (path.StartsWith(@"\\", StringComparison.Ordinal) || UpdateFiles.SamePath(path, Path.GetPathRoot(path)!) || UpdateFiles.IsUnder(Environment.GetFolderPath(Environment.SpecialFolder.Windows), path) || UpdateFiles.SamePath(path, Environment.GetFolderPath(Environment.SpecialFolder.Windows)) || UpdateFiles.SamePath(path, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)))
            throw new IOException("Registered installation directory is unsafe.");
        UpdateFiles.NoLinks(path); return path;
    }
    public void ValidateInstallation(UpdateJob job)
    {
        if (!UpdateFiles.SamePath(job.InstallDirectory, InstallDirectory())) throw new IOException("Installation directory changed after preparing the update.");
        if (NormalizeVersion(FileVersionInfo.GetVersionInfo(job.SourceExecutable).ProductVersion) != job.CurrentVersion) throw new IOException("Source executable version differs from the handoff.");
    }
    public async Task WaitForParentAsync(UpdateJob job, CancellationToken token)
    {
        try
        {
            using var parent = Process.GetProcessById(job.ParentId);
            if (parent.StartTime.ToUniversalTime().Ticks == job.ParentStartedUtcTicks)
            {
                if (!UpdateFiles.SamePath(parent.MainModule!.FileName, job.SourceExecutable)) throw new IOException("Handoff parent is not the expected NetStucked application.");
                await parent.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(60), token).ConfigureAwait(false);
            }
        }
        catch (ArgumentException) { /* The acknowledged parent already exited. */ }
        foreach (var process in Process.GetProcessesByName("NetStucked"))
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId || process.HasExited) continue;
                try { if (UpdateFiles.SamePath(process.MainModule!.FileName, Path.Combine(job.InstallDirectory, "NetStucked.exe"))) throw new IOException("Another NetStucked instance is still using the installation. Close it and retry."); }
                catch (Win32Exception) { throw new IOException("Could not verify whether another NetStucked instance is using the installation."); }
            }
        }
    }
    public async Task VerifyInstallerAsync(UpdateJob job, CancellationToken token)
    {
        // Recheck the public catalog in the helper too; local handoff edits cannot supply an arbitrary executable/hash.
        using var source = new GitHubReleaseSource();
        var catalog = await source.CheckAsync(token).ConfigureAwait(false);
        var published = catalog.Releases.FirstOrDefault(r => r.Version.ToString() == job.TargetVersion);
        if (published?.Installer is null || published.Installer != job.Installer || published.Page != job.ReleasePage)
            throw new InvalidDataException("Selected installer metadata no longer matches the public GitHub release.");
        await Task.Run(() =>
        {
        token.ThrowIfCancellationRequested(); string path = UpdateFiles.InstallerPath(job);
        var info = FileVersionInfo.GetVersionInfo(path);
        if (info.ProductName?.Trim() != "NetStucked" || NormalizeVersion(info.ProductVersion) != job.TargetVersion) throw new InvalidDataException("Installer product/version does not match NetStucked and the selected build.");
        uint trust = Authenticode.Check(path);
        if (trust == 0x800B0100 && !job.AllowUnsigned) throw new InvalidDataException("This installer is unsigned. Acknowledge unsigned installers before retrying. Windows permission prompts remain enabled.");
        if (trust != 0 && trust != 0x800B0100) throw new InvalidDataException($"Windows rejected the installer signature (0x{trust:X8}). Nothing will be executed.");
        token.ThrowIfCancellationRequested();
        }, token).ConfigureAwait(false);
    }
    public async Task<int> InstallAsync(UpdateJob job, CancellationToken token)
    {
        ValidateSpace(job.InstallDirectory, Math.Max(job.Installer.Bytes * 8, 256 * 1024 * 1024));
        // Keep the verified file locked against writes/deletion through installer execution.
        await using var guard = new FileStream(UpdateFiles.InstallerPath(job), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (!CryptographicOperations.FixedTimeEquals(await SHA256.HashDataAsync(guard, token).ConfigureAwait(false), Convert.FromHexString(job.Installer.Sha256))) throw new InvalidDataException("Installer changed immediately before execution.");
        var start = new ProcessStartInfo(UpdateFiles.InstallerPath(job)) { UseShellExecute = true, Verb = "runas", WorkingDirectory = Path.GetDirectoryName(UpdateFiles.InstallerPath(job))! };
        foreach (string argument in new[] { "/SILENT", "/SUPPRESSMSGBOXES", "/SP-", "/NORESTART", "/RESTARTEXITCODE=3010", "/NOCLOSEAPPLICATIONS", "/NOFORCECLOSEAPPLICATIONS", "/NORESTARTAPPLICATIONS", "/DIR=" + job.InstallDirectory, "/LOG=" + Path.Combine(UpdateFiles.JobDirectory(job.DataDirectory, job.Id), "installer.log") }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Windows did not start the installer.");
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        return process.ExitCode;
    }
    public string InstalledVersion(UpdateJob job) => NormalizeVersion(FileVersionInfo.GetVersionInfo(Path.Combine(job.InstallDirectory, "NetStucked.exe")).ProductVersion);
    public void Restart(UpdateJob job, bool installed)
    {
        string executable = installed ? Path.Combine(job.InstallDirectory, "NetStucked.exe") : job.SourceExecutable;
        if (!File.Exists(executable)) throw new IOException("Application executable is unavailable; use the preserved installer/log and settings backup.");
        if (!installed)
            foreach (string file in new[] { "NetStucked.exe", "NetStucked.dll", "NetStucked.Core.dll", "NetStucked.Infrastructure.dll" })
            {
                string original = Path.Combine(Path.GetDirectoryName(job.SourceExecutable)!, file), copy = Path.Combine(AppContext.BaseDirectory, file);
                if (!File.Exists(original) || !File.Exists(copy) || !SHA256.HashData(File.ReadAllBytes(original)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(copy))))
                    throw new IOException("Original application bytes changed during the failed install. Use Recovery/manual installation before restarting.");
            }
        // Helper retains the original user's token; only Setup is elevated.
        using var process = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! });
        if (process is null) throw new IOException("Windows did not restart NetStucked.");
    }
    internal static string NormalizeVersion(string? text)
    {
        string value = (text ?? "").Split('+')[0].Trim();
        if (Version.TryParse(value, out var number) && number.Build >= 0 && number.Revision is -1 or 0) return $"{number.Major}.{number.Minor}.{number.Build}";
        return "";
    }
    internal static void ValidateSpace(string directory, long required)
    {
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(directory))!);
        if (drive.AvailableFreeSpace < required) throw new IOException($"Insufficient free space for the update ({required / 1024 / 1024} MB required).");
    }
}

[SupportedOSPlatform("windows")]
public static class Authenticode
{
    public static uint Check(string path)
    {
        var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = path };
        nint pointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        Marshal.StructureToPtr(file, pointer, false);
        var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), Ui = 2, UnionChoice = 1, File = pointer, StateAction = 1, Flags = 0x1000 | 0x80 };
        Guid action = new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
        try { return WinVerifyTrust(new nint(-1), ref action, ref data); }
        finally { data.StateAction = 2; _ = WinVerifyTrust(new nint(-1), ref action, ref data); Marshal.DestroyStructure<TrustFile>(pointer); Marshal.FreeHGlobal(pointer); }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TrustFile { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public nint Handle; public nint Subject; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData { public uint Size; public nint Policy; public nint Sip; public uint Ui; public uint Revocation; public uint UnionChoice; public nint File; public uint StateAction; public nint State; public nint Url; public uint Flags; public uint Context; public nint Signature; }
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern uint WinVerifyTrust(nint window, ref Guid action, ref TrustData data);
}
