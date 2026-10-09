using System.Text;
using System.Text.Json;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed record ColumnPreference(string Key, int Order, double Width, bool Visible, bool Stretch = false);
public sealed record PingTemplate(Guid Id, string Name, string Addresses);
public sealed class UserPreferences
{
    public int SchemaVersion { get; set; } = 1;
    public ProbeSettings Ping { get; set; } = new();
    public TraceSettings Trace { get; set; } = new();
    public string TargetText { get; set; } = "";
    public string TraceTarget { get; set; } = "";
    public Dictionary<string, List<ColumnPreference>> Columns { get; set; } = new();
    public Dictionary<string, string> TraceDescriptions { get; set; } = new();
    public List<PingTemplate> PingTemplates { get; set; } = [];
    public List<string> TraceHistory { get; set; } = [];
    public PortProbeSettings Port { get; set; } = new();
    public string PortTargetText { get; set; } = "";
    public List<PingTemplate> PortTemplates { get; set; } = [];
    public string Theme { get; set; } = "Light";
    public bool SidebarExpanded { get; set; } = true;
    public Dictionary<string, string> HopDescriptions { get; set; } = new();
    public bool WanDescriptions { get; set; } = true;
    public string PortNumbers { get; set; } = "443";
    public PortProtocol PortProtocol { get; set; }
    public bool PortMultipleTargets { get; set; }
    public bool PortContinuous { get; set; }
    public Dictionary<string, string> PortNumberTemplates { get; set; } = new();
}

public sealed class UserSettingsStore
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    public string DirectoryPath { get; }
    public UserPreferences Preferences { get; private set; } = new();
    public string? LoadError { get; private set; }
    public UserSettingsStore(string? directory = null)
    {
        DirectoryPath = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetStucked");
        try
        {
            string path = Path.Combine(DirectoryPath, "settings.json");
            if (!File.Exists(path)) return;
            if (new FileInfo(path).Length > 2_000_000) throw new InvalidDataException("Settings file exceeds 2 MB.");
            var loaded = JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(path));
            if (loaded is null || loaded.SchemaVersion != 1 || loaded.Ping is null || loaded.Trace is null ||
                loaded.TargetText is null || loaded.TraceTarget is null || loaded.Columns is null || loaded.TraceDescriptions is null ||
                loaded.Columns.Any(pair => pair.Value is null || pair.Value.Any(column => column is null || string.IsNullOrEmpty(column.Key))) ||
                loaded.TraceDescriptions.Any(pair => pair.Value is null) || loaded.PingTemplates is null || loaded.TraceHistory is null ||
                loaded.PingTemplates.Count > 100 || loaded.PingTemplates.Any(t => t is null || t.Id == Guid.Empty || string.IsNullOrWhiteSpace(t.Name) || t.Name.Length > 80 || t.Addresses is null || t.Addresses.Length > 1000000) ||
                loaded.PingTemplates.Select(t => t.Id).Distinct().Count() != loaded.PingTemplates.Count ||
                loaded.PingTemplates.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != loaded.PingTemplates.Count ||
                loaded.TraceHistory.Count > 100 || loaded.TraceHistory.Any(t => !TargetInputParser.IsValidHost(t)) ||
                loaded.Port is null || loaded.PortTargetText is null || loaded.PortTargetText.Length > 1000000 || loaded.PortTemplates is null ||
                loaded.PortTemplates.Count > 100 || loaded.PortTemplates.Any(t => t is null || t.Id == Guid.Empty || string.IsNullOrWhiteSpace(t.Name) || t.Name.Length > 80 || t.Addresses is null || t.Addresses.Length > 1000000) ||
                loaded.PortTemplates.Select(t => t.Id).Distinct().Count() != loaded.PortTemplates.Count ||
                loaded.PortTemplates.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != loaded.PortTemplates.Count ||
                loaded.Theme is not ("Light" or "Dark" or "System") || loaded.HopDescriptions is null || loaded.HopDescriptions.Count > 1024 ||
                loaded.HopDescriptions.Any(p => !System.Net.IPAddress.TryParse(p.Key, out _) || p.Value is null || p.Value.Length > 512) ||
                loaded.PortNumbers is null || loaded.PortNumbers.Length > 2048 || !Enum.IsDefined(loaded.PortProtocol) ||
                loaded.PortNumberTemplates is null || loaded.PortNumberTemplates.Count > 100 || loaded.PortNumberTemplates.Any(p => p.Key.Length is < 1 or > 80 || p.Value is null || p.Value.Length > 2048))
                throw new InvalidDataException("Unsupported settings schema.");
            // Older files can have the former 100ms timeout. Preserve other user data while migrating the new lower bound.
            if (loaded.Ping.TimeoutMs is >= 100 and < 500) loaded.Ping = loaded.Ping with { TimeoutMs = 500 };
            if (loaded.Trace.TimeoutMs is >= 100 and < 500) loaded.Trace = loaded.Trace with { TimeoutMs = 500 };
            loaded.Ping.Validate(); loaded.Trace.Validate(); loaded.Port.Validate();
            Preferences = loaded;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidDataException)
        {
            LoadError = $"Preferences were not loaded: {ex.Message}. Defaults are in use; original file is preserved until settings are saved.";
        }
    }

    public async Task SaveAsync(CancellationToken token = default)
    {
        string json = JsonSerializer.Serialize(Preferences, new JsonSerializerOptions { WriteIndented = true });
        if (Encoding.UTF8.GetByteCount(json) > 2_000_000) throw new InvalidDataException("Saved settings and templates exceed 2 MB.");
        await _saveGate.WaitAsync(token).ConfigureAwait(false);
        try { await SaveJsonAsync(json, token).ConfigureAwait(false); }
        finally { _saveGate.Release(); }
    }
    private async Task SaveJsonAsync(string json, CancellationToken token)
    {
        Directory.CreateDirectory(DirectoryPath);
        string path = Path.Combine(DirectoryPath, "settings.json");
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, json, Encoding.UTF8, token).ConfigureAwait(false);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
