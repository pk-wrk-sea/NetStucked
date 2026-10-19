using System.Text.Json;
using NetStucked.Core;

namespace NetStucked.Infrastructure;
public sealed class TerminalProfileStore(UserSettingsStore store) : ITerminalProfileStore
{
    private readonly SemaphoreSlim _write = new(1);
    private string PathName => Path.Combine(store.DirectoryPath, "terminal-profiles.json");
    private bool _corrupt;
    private sealed record Document(int Schema, TerminalProfile[] Profiles);
    public async Task<IReadOnlyList<TerminalProfile>> LoadAsync(CancellationToken token)
    {
        if (!File.Exists(PathName)) return [];
        try
        {
            if (new FileInfo(PathName).Length > 1024 * 1024) throw new JsonException();
            var doc = JsonSerializer.Deserialize<Document>(await File.ReadAllTextAsync(PathName, token).ConfigureAwait(false));
            if (doc is null || doc.Schema != 1 || doc.Profiles is null || doc.Profiles.Length > 100 || doc.Profiles.Any(p => p is null) || doc.Profiles.Select(p => p.Id).Distinct().Count() != doc.Profiles.Length) throw new JsonException();
            foreach (var profile in doc.Profiles) Validate(profile);
            return doc.Profiles;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException) { _corrupt = true; return []; }
    }
    private static void Validate(TerminalProfile p)
    {
        if (p is null || p.Id == Guid.Empty || p.Name is null || p.HostPin is null || p.KeyPath is null || !Enum.IsDefined(p.Protocol) || !Enum.IsDefined(p.Authentication) || p.Name.Length is < 1 or > 80 || p.HostPin.Length > 100 || p.KeyPath.Length > 1000) throw new ArgumentException("Invalid terminal profile.");
        new TerminalRequest(p, new("placeholder-for-nonsecret-profile-validation", "")).Validate();
    }
    public async Task SaveAsync(IReadOnlyList<TerminalProfile> profiles, CancellationToken token)
    {
        if (profiles.Count > 100 || profiles.Any(p => p is null) || profiles.Select(p => p.Id).Distinct().Count() != profiles.Count) throw new ArgumentException("Maximum 100 unique saved profiles.");
        foreach (var p in profiles) Validate(p);
        await _write.WaitAsync(token).ConfigureAwait(false); string temporary = PathName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(store.DirectoryPath);
            if (_corrupt && File.Exists(PathName)) { File.Copy(PathName, PathName + ".corrupt-" + Guid.NewGuid().ToString("N")); _corrupt = false; }
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new Document(1, profiles.ToArray())), token).ConfigureAwait(false);
            await AtomicFileReplace.MoveAsync(temporary, PathName, token).ConfigureAwait(false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); _write.Release(); }
    }
}
