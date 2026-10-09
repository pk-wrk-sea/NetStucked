using System.Text.RegularExpressions;

namespace NetStucked.Core;

public sealed record ReleaseInstaller(long Id, string Name, Uri DownloadUri, string Sha256, long Bytes);
public sealed record UpdateProgress(string Stage, long Bytes = 0, long TotalBytes = 0)
{
    public double Percent => TotalBytes > 0 ? Math.Clamp(Bytes * 100d / TotalBytes, 0, 100) : 0;
}
public sealed record StagedUpdate(string JobPath);
public sealed record SettingsBackupInfo(string Version, DateTimeOffset CreatedAt, string Directory, string Sha256);
public sealed record UpdateResult(string TargetVersion, bool Success, bool RestartRequired, string Message, DateTimeOffset FinishedAt, string? BackupDirectory = null);

public interface IApplicationUpdateService
{
    string InstallationDetails { get; }
    Task<StagedUpdate> StageAsync(PublishedRelease release, string currentVersion, bool allowUnsigned, IProgress<UpdateProgress> progress, CancellationToken token);
    Task StartHelperAsync(StagedUpdate staged, CancellationToken token);
    Task DiscardAsync(StagedUpdate staged);
    Task<IReadOnlyList<SettingsBackupInfo>> GetBackupsAsync(CancellationToken token);
    Task<UpdateResult?> GetLastResultAsync(CancellationToken token);
}

public static partial class InstallerPolicy
{
    public const long MaximumBytes = 256 * 1024 * 1024;
    [GeneratedRegex("^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex HashPattern();
    public static void Validate(ReleaseInstaller installer, SemanticVersion version)
    {
        if (version.PreRelease.Length != 0 || version.Metadata.Length != 0 || installer.Id <= 0 || installer.Bytes is <= 0 or > MaximumBytes || !HashPattern().IsMatch(installer.Sha256))
            throw new InvalidDataException("Installer metadata is incomplete or exceeds the release limits.");
        string name = $"NetStucked-{version}-win-x64-setup.exe";
        if (installer.Name != name || !IsSourceUri(installer.DownloadUri, version, name))
            throw new InvalidDataException("Installer must belong to the selected NetStucked release.");
    }
    public static bool IsSourceUri(Uri uri, SemanticVersion version, string name) =>
        uri.Scheme == "https" && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 &&
        (uri.AbsolutePath == $"/{ReleaseRepository.Owner}/{ReleaseRepository.Name}/releases/download/v{version}/{name}" || uri.AbsolutePath == $"/{ReleaseRepository.Owner}/{ReleaseRepository.Name}/releases/download/{version}/{name}");
    public static bool IsAssetRedirect(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0 &&
        (uri.Host.Equals("release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase));
}
