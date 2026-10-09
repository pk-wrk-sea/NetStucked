using System.Security.Cryptography;
using System.Text.Json;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed record UpdateJob(int Schema, Guid Id, string CurrentVersion, string TargetVersion, ReleaseInstaller Installer, Uri ReleasePage,
    string DataDirectory, string InstallDirectory, string SourceExecutable, int ParentId, long ParentStartedUtcTicks, bool AllowUnsigned);
public sealed record BackupManifest(int Schema, string Version, DateTimeOffset CreatedAt, string Sha256);
public sealed record HelperReady(Guid Id, int ParentId, int HelperId);

public static class UpdateFiles
{
    public const string JobFile = "update-job.json";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static string JobDirectory(string data, Guid id) => Path.Combine(data, "updates", "jobs", id.ToString("N"));
    public static string InstallerPath(UpdateJob job) => Path.Combine(JobDirectory(job.DataDirectory, job.Id), job.Installer.Name);
    public static void NoLinks(string path)
    {
        string? cursor = Path.GetFullPath(path);
        while (cursor is not null)
        {
            if ((File.Exists(cursor) || Directory.Exists(cursor)) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Update paths cannot contain symbolic links or junctions.");
            cursor = Path.GetDirectoryName(cursor);
        }
    }
    public static bool SamePath(string left, string right) => Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    public static bool IsUnder(string root, string path) => Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    public static void ValidateJob(UpdateJob job, string path, string expectedDataDirectory)
    {
        if (job.Schema != 1 || job.Id == Guid.Empty || job.ParentId <= 0 || job.ParentStartedUtcTicks <= 0 ||
            !SemanticVersion.TryParse(job.CurrentVersion, out var current) || current!.PreRelease.Length != 0 || current.Metadata.Length != 0 ||
            !SemanticVersion.TryParse(job.TargetVersion, out var target) || target!.PreRelease.Length != 0 || target.Metadata.Length != 0)
            throw new InvalidDataException("Invalid update handoff metadata.");
        InstallerPolicy.Validate(job.Installer, target);
        if (!Path.IsPathFullyQualified(job.DataDirectory) || !Path.IsPathFullyQualified(job.InstallDirectory) || !Path.IsPathFullyQualified(job.SourceExecutable) ||
            !SamePath(job.DataDirectory, expectedDataDirectory) || !SamePath(path, Path.Combine(JobDirectory(expectedDataDirectory, job.Id), JobFile)) ||
            Path.GetFileName(job.SourceExecutable) != "NetStucked.exe" || SamePath(job.InstallDirectory, Path.GetPathRoot(job.InstallDirectory)!) ||
            SamePath(job.DataDirectory, job.InstallDirectory) || IsUnder(job.DataDirectory, job.InstallDirectory) || IsUnder(job.InstallDirectory, job.DataDirectory) ||
            !ReleaseRepository.IsReleasePage(job.ReleasePage) || !(job.ReleasePage.AbsolutePath.EndsWith("/v" + target, StringComparison.Ordinal) || job.ReleasePage.AbsolutePath.EndsWith("/" + target, StringComparison.Ordinal)))
            throw new InvalidDataException("Update paths or release identity are invalid.");
        NoLinks(path); NoLinks(job.InstallDirectory); NoLinks(job.SourceExecutable);
    }
    public static async Task<T> ReadAsync<T>(string path, int maximumBytes, CancellationToken token)
    {
        NoLinks(path);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous);
        if (file.Length > maximumBytes) throw new InvalidDataException("Update metadata exceeds the size limit.");
        return await JsonSerializer.DeserializeAsync<T>(file, cancellationToken: token).ConfigureAwait(false) ?? throw new InvalidDataException("Update metadata is empty.");
    }
    public static async Task WriteAsync<T>(string path, T value, CancellationToken token)
    {
        NoLinks(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, Json), token).ConfigureAwait(false);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static async Task VerifyHashAsync(string path, ReleaseInstaller asset, CancellationToken token)
    {
        NoLinks(path);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (file.Length != asset.Bytes || !CryptographicOperations.FixedTimeEquals(await SHA256.HashDataAsync(file, token).ConfigureAwait(false), Convert.FromHexString(asset.Sha256)))
            throw new InvalidDataException("Installer bytes changed after download. Nothing will be executed.");
    }
}

public sealed class UpdateBackups(string dataDirectory)
{
    private string Root => Path.Combine(dataDirectory, "updates", "backups");
    public async Task<SettingsBackupInfo?> CreateAsync(string version, CancellationToken token)
    {
        if (!SemanticVersion.TryParse(version, out _)) throw new InvalidDataException("Invalid backup version.");
        string settings = Path.Combine(dataDirectory, "settings.json");
        if (!File.Exists(settings)) return null;
        UpdateFiles.NoLinks(settings); UpdateFiles.NoLinks(Root);
        byte[] bytes = await ReadSettingsAsync(settings, token).ConfigureAwait(false);
        string directory = Path.Combine(Root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        string sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        await File.WriteAllBytesAsync(Path.Combine(directory, "settings.json"), bytes, token).ConfigureAwait(false);
        var record = new BackupManifest(1, version, DateTimeOffset.UtcNow, sha);
        await UpdateFiles.WriteAsync(Path.Combine(directory, "backup.json"), record, token).ConfigureAwait(false);
        await PruneAsync(token).ConfigureAwait(false);
        return new(version, record.CreatedAt, directory, sha);
    }
    public async Task<IReadOnlyList<SettingsBackupInfo>> ListAsync(CancellationToken token)
    {
        var result = new List<SettingsBackupInfo>();
        UpdateFiles.NoLinks(Root);
        if (!Directory.Exists(Root)) return result;
        foreach (string directory in Directory.EnumerateDirectories(Root).Take(1000))
        {
            token.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) continue;
            try
            {
                var record = await UpdateFiles.ReadAsync<BackupManifest>(Path.Combine(directory, "backup.json"), 4096, token).ConfigureAwait(false);
                byte[] bytes = await ReadSettingsAsync(Path.Combine(directory, "settings.json"), token).ConfigureAwait(false);
                if (record.Schema != 1 || !SemanticVersion.TryParse(record.Version, out _) || record.Sha256 != Convert.ToHexStringLower(SHA256.HashData(bytes))) continue;
                result.Add(new(record.Version, record.CreatedAt, directory, record.Sha256));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException) { /* Never offer damaged backups. */ }
        }
        return result.OrderByDescending(b => b.CreatedAt).ToArray();
    }
    public async Task<bool> RestoreForVersionAsync(string version, CancellationToken token)
    {
        var backup = (await ListAsync(token).ConfigureAwait(false)).FirstOrDefault(b => b.Version == version);
        if (backup is null) return false;
        byte[] bytes = await ReadSettingsAsync(Path.Combine(backup.Directory, "settings.json"), token).ConfigureAwait(false);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != backup.Sha256) throw new InvalidDataException("Recovery settings backup changed.");
        string destination = Path.Combine(dataDirectory, "settings.json"); UpdateFiles.NoLinks(destination);
        string temporary = destination + ".restore-" + Guid.NewGuid().ToString("N");
        try { await File.WriteAllBytesAsync(temporary, bytes, token).ConfigureAwait(false); File.Move(temporary, destination, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return true;
    }
    public static async Task<byte[]> ReadSettingsAsync(string path, CancellationToken token)
    {
        UpdateFiles.NoLinks(path);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous);
        if (stream.Length > 2_000_000) throw new InvalidDataException("Settings backup exceeds 2 MB.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        var json = bytes.AsMemory(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("SchemaVersion", out var schema) || !schema.TryGetInt32(out int number) || number != 1)
            throw new InvalidDataException("Settings schema is incompatible. Installation was not started.");
        return bytes;
    }
    private async Task PruneAsync(CancellationToken token)
    {
        foreach (var old in (await ListAsync(token).ConfigureAwait(false)).Skip(20))
        {
            if (!UpdateFiles.IsUnder(Root, old.Directory) || !Guid.TryParseExact(Path.GetFileName(old.Directory), "N", out _)) throw new IOException("Invalid backup cleanup path.");
            UpdateFiles.NoLinks(old.Directory);
            // Delete only owned files, never recurse through arbitrary directories.
            File.Delete(Path.Combine(old.Directory, "settings.json")); File.Delete(Path.Combine(old.Directory, "backup.json"));
            if (!Directory.EnumerateFileSystemEntries(old.Directory).Any()) Directory.Delete(old.Directory);
        }
    }
}
