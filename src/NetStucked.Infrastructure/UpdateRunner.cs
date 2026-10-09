using NetStucked.Core;

namespace NetStucked.Infrastructure;

public interface IUpdatePlatform
{
    void ValidateInstallation(UpdateJob job);
    Task WaitForParentAsync(UpdateJob job, CancellationToken token);
    Task VerifyInstallerAsync(UpdateJob job, CancellationToken token);
    Task<int> InstallAsync(UpdateJob job, CancellationToken token);
    string InstalledVersion(UpdateJob job);
    void Restart(UpdateJob job, bool installed);
}

public sealed class UpdateRunner(IUpdatePlatform platform)
{
    public async Task<UpdateResult> RunAsync(string jobPath, string expectedDataDirectory, CancellationToken token)
    {
        var job = await UpdateFiles.ReadAsync<UpdateJob>(jobPath, 16384, token).ConfigureAwait(false);
        UpdateFiles.ValidateJob(job, jobPath, expectedDataDirectory);
        platform.ValidateInstallation(job);
        var backups = new UpdateBackups(job.DataDirectory);
        SettingsBackupInfo? backup = null;
        bool installStarted = false;
        bool parentExited = false;
        UpdateResult result;
        try
        {
            await platform.WaitForParentAsync(job, token).ConfigureAwait(false);
            parentExited = true;
            await UpdateFiles.VerifyHashAsync(UpdateFiles.InstallerPath(job), job.Installer, token).ConfigureAwait(false);
            await platform.VerifyInstallerAsync(job, token).ConfigureAwait(false);
            backup = await backups.CreateAsync(job.CurrentVersion, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            // Once Setup starts, never kill it or cancel settings restoration halfway through.
            installStarted = true;
            int code = await platform.InstallAsync(job, CancellationToken.None).ConfigureAwait(false);
            if (code != 0 && code != 3010) throw new IOException($"Installer did not complete (exit {code}). See installer.log. The settings backup is preserved.");
            if (code == 3010)
                result = new(job.TargetVersion, false, true, "Windows restart is required to complete installation. Saved settings were not restored and the app was not restarted.", DateTimeOffset.UtcNow, backup?.Directory);
            else
            {
                string installed = platform.InstalledVersion(job);
                if (installed != job.TargetVersion) throw new IOException("Installed executable version does not match the selected build.");
                SemanticVersion.TryParse(job.CurrentVersion, out var current); SemanticVersion.TryParse(job.TargetVersion, out var target);
                bool restored = target!.CompareTo(current) < 0 && await backups.RestoreForVersionAsync(job.TargetVersion, CancellationToken.None).ConfigureAwait(false);
                result = new(job.TargetVersion, true, false, $"Installed v{job.TargetVersion}. " + (restored ? "Restored that version's verified settings backup." : "Compatible current settings were retained."), DateTimeOffset.UtcNow, backup?.Directory);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            result = new(job.TargetVersion, false, false, (installStarted ? "Installation did not complete. " : "Installation was not started. ") + ex.Message, DateTimeOffset.UtcNow, backup?.Directory);
        }
        await UpdateFiles.WriteAsync(Path.Combine(job.DataDirectory, "updates", "last-result.json"), result, CancellationToken.None).ConfigureAwait(false);
        if (!result.RestartRequired && parentExited)
        {
            try { platform.Restart(job, result.Success); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                result = result with { Success = false, Message = result.Message + " App restart failed: " + ex.Message };
                await UpdateFiles.WriteAsync(Path.Combine(job.DataDirectory, "updates", "last-result.json"), result, CancellationToken.None).ConfigureAwait(false);
            }
        }
        return result;
    }
}
