using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed class EngineeringStore(UserSettingsStore settings) : IEngineeringStore
{
    private readonly string _directory = Path.Combine(settings.DirectoryPath, "engineering");
    private readonly SemaphoreSlim _gate = new(1);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public Task<EngineeringData> LoadAsync(CancellationToken token) => Task.Run(() => LoadCoreAsync(token), token);
    private async Task<EngineeringData> LoadCoreAsync(CancellationToken token)
    { await _gate.WaitAsync(token).ConfigureAwait(false); try { return await ReadAsync(token).ConfigureAwait(false); } finally { _gate.Release(); } }
    private async Task<EngineeringData> ReadAsync(CancellationToken token)
    {
        string path = Path.Combine(_directory, "workspace-v1.json");
        if (!File.Exists(path)) return EngineeringData.Empty;
        if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new IOException("Engineering data exceeds 16 MiB; original retained.");
        await using var input = File.OpenRead(path);
        try { var data = await JsonSerializer.DeserializeAsync<EngineeringData>(input, Json, token).ConfigureAwait(false) ?? throw new JsonException("Empty document."); EngineeringRules.Validate(data); return data; }
        catch (Exception ex) when (ex is JsonException or ArgumentException or NullReferenceException) { throw new IOException("Engineering data is unsupported or invalid; original retained. Restore a verified backup before editing.", ex); }
    }
    public Task<EngineeringData> SaveAsync(EngineeringData data, long expectedRevision, CancellationToken token) => Task.Run(() => SaveCoreAsync(data, expectedRevision, token), token);
    private async Task<EngineeringData> SaveCoreAsync(EngineeringData data, long expectedRevision, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        string temp = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var current = await ReadAsync(token).ConfigureAwait(false);
            if (current.Revision != expectedRevision) throw new IOException("Workspace changed. Reload before saving; no changes were overwritten.");
            EngineeringRules.Validate(data);
            var changes = (current.Changes ?? []).ToList();
            var oldAssets = current.Assets.ToDictionary(a => a.Id); var newAssets = data.Assets.ToDictionary(a => a.Id);
            foreach (var id in oldAssets.Keys.Union(newAssets.Keys))
            {
                oldAssets.TryGetValue(id, out var before); newAssets.TryGetValue(id, out var after);
                if (before != after) changes.Add(new(Guid.NewGuid(), DateTimeOffset.UtcNow, Environment.UserName, before, after));
            }
            var history = (current.AdvisoryHistory ?? []).ToList();
            foreach (var previous in current.Advisories)
                if (data.Advisories.Any(a => a.Cve == previous.Cve && a.Revision != previous.Revision) && !history.Any(a => a.Cve == previous.Cve && a.Revision == previous.Revision)) history.Add(previous);
            var saved = data with { Schema = 1, Revision = checked(expectedRevision + 1), Changes = changes.ToArray(), AdvisoryHistory = history.ToArray() }; EngineeringRules.Validate(saved);
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(saved, Json);
            if (bytes.Length > 16 * 1024 * 1024) throw new IOException("Engineering store exceeds 16 MiB. Export/archive before adding more records.");
            Directory.CreateDirectory(_directory);
            if (OperatingSystem.IsWindows()) ProtectDirectory(_directory);
            await File.WriteAllBytesAsync(temp, bytes, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            string path = Path.Combine(_directory, "workspace-v1.json");
            if (File.Exists(path))
            {
                string backupTemp = temp + ".backup";
                try { File.Copy(path, backupTemp, false); File.Move(backupTemp, Path.Combine(_directory, "workspace-v1.previous.json"), true); }
                finally { if (File.Exists(backupTemp)) File.Delete(backupTemp); }
            }
            File.Move(temp, path, true);
            return saved;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); _gate.Release(); }
    }
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void ProtectDirectory(string path)
    {
        var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false);
        using var user = WindowsIdentity.GetCurrent();
        foreach (var sid in new[] { user.User ?? throw new UnauthorizedAccessException(), new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(acl);
    }
}

public static class EvidenceFile
{
    public static async Task<EvidenceDocument> ReanalyzeAsync(EvidenceDocument original, CancellationToken token)
    {
        string text = await ReadVerifiedTextAsync(original, token).ConfigureAwait(false);
        var revision = await Task.Run(() => CiscoEvidenceParser.Parse(text, original.Source, original.Sha256, original.Host, original.CollectedAt, original.Complete, original.AssetId, token), token).ConfigureAwait(false);
        // This new immutable record is the audit trail. Raw source, collection time and
        // prior parser facts remain unchanged; downstream reviews see a new evidence ID.
        string audit = $"Explicit offline reanalysis of evidence {original.Id} ({original.Parser}) at {DateTimeOffset.UtcNow:O}; original capture and prior facts retained.";
        return revision with { Warnings = revision.Warnings.Append(audit).ToArray() };
    }
    public static async Task<string> ReadVerifiedTextAsync(EvidenceDocument evidence, CancellationToken token)
    {
        if (new FileInfo(evidence.Source).Length > 12 * 1024 * 1024) throw new IOException("Evidence file exceeds analysis limit.");
        byte[] bytes = await File.ReadAllBytesAsync(evidence.Source, token).ConfigureAwait(false);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != evidence.Sha256) throw new IOException("Source file changed since analysis; import it as new evidence before comparing.");
        string text;
        if (Path.GetExtension(evidence.Source).Equals(".json", StringComparison.OrdinalIgnoreCase))
        { using var json = JsonDocument.Parse(bytes); var builder = new StringBuilder(); foreach (var chunk in json.RootElement.GetProperty("outputChunks").EnumerateArray()) { builder.Append(chunk.GetString()); if (builder.Length > 8 * 1024 * 1024) throw new IOException("Decoded source exceeds limit."); } text = builder.ToString(); }
        else text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        if (text.Length > 8 * 1024 * 1024) throw new IOException("Decoded source exceeds limit."); return text;
    }
    // JSON is the Collector's own schema, not an arbitrary serialized object graph. Never follow embedded paths.
    public static async Task<EvidenceDocument> ReadAsync(string path, Guid? assetId, string fallbackHost, CancellationToken token)
    {
        if (new FileInfo(path).Length > 12 * 1024 * 1024) throw new IOException("Evidence file exceeds 12 MiB (8 MiB decoded analysis limit).");
        byte[] bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
        string text, host = fallbackHost; bool complete = false; DateTimeOffset collected = File.GetLastWriteTimeUtc(path);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            using var json = JsonDocument.Parse(bytes); var root = json.RootElement;
            if (root.GetProperty("schema").GetInt32() != 1) throw new IOException("Unsupported Collector JSON schema.");
            var result = root.GetProperty("result"); host = result.GetProperty("Target").GetProperty("Host").GetString() ?? host;
            complete = result.GetProperty("State").GetString() == "Completed";
            collected = result.GetProperty("Finished").GetDateTimeOffset();
            var output = new StringBuilder();
            foreach (var chunk in root.GetProperty("outputChunks").EnumerateArray()) { output.Append(chunk.GetString()); if (output.Length > 8 * 1024 * 1024) throw new IOException("Decoded transcript exceeds 8 MiB."); }
            text = output.ToString();
            string transcriptHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
            if (!transcriptHash.Equals(result.GetProperty("Sha256").GetString(), StringComparison.OrdinalIgnoreCase)) throw new IOException("Collector transcript SHA-256 does not match its metadata.");
        }
        else { text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
        return await Task.Run(() => CiscoEvidenceParser.Parse(text, Path.GetFullPath(path), hash, host, collected, complete, assetId, token), token).ConfigureAwait(false);
    }
}
