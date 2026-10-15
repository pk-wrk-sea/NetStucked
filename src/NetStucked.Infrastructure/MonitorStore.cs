using System.Text.Json;
using NetStucked.Core;
namespace NetStucked.Infrastructure;
public sealed class MonitorStore(UserSettingsStore settings) : IMonitorStore
{
    private readonly SemaphoreSlim _gate = new(1);
    public string? LoadError { get; private set; }
    private string PathName => Path.Combine(settings.DirectoryPath, "monitoring.json");
    public MonitorData Load()
    {
        try
        {
            if (!File.Exists(PathName)) return new(1, [], [], null);
            if (new FileInfo(PathName).Length > 8_000_000) throw new InvalidDataException("Monitoring file exceeds 8 MB.");
            var data = JsonSerializer.Deserialize<MonitorData>(File.ReadAllText(PathName)) ?? throw new InvalidDataException();
            if (data.SchemaVersion != 1 || data.Devices is null || data.Incidents is null || data.Devices.Count > MonitoringService.MaximumDevices || data.Incidents.Count > 5000 || data.Devices.Any(d => d is null) || data.Devices.Select(d => d.Id).Distinct().Count() != data.Devices.Count || data.Incidents.Any(i => i is null || i.DeviceName is null || i.Kind is null || i.Details is null || i.Details.Length > 2048)) throw new InvalidDataException("Unsupported monitoring data.");
            foreach (var device in data.Devices) { if (device.SnmpSettings is null) throw new InvalidDataException(); device.Validate(); }
            return data;
        }
        catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or InvalidOperationException or NullReferenceException)
        { LoadError = "Monitoring data could not be loaded; auto-start was disabled. The original file will be preserved."; return new(1, [], [], null); }
    }
    public async Task SaveAsync(MonitorData data, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        string temporary = PathName + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(settings.DirectoryPath);
            if (LoadError is not null && File.Exists(PathName)) { File.Copy(PathName, PathName + ".corrupt-" + Guid.NewGuid().ToString("N")); LoadError = null; }
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(data, new JsonSerializerOptions { WriteIndented = true });
            if (bytes.Length > 8_000_000) throw new InvalidDataException("Monitoring data exceeds 8 MB.");
            await File.WriteAllBytesAsync(temporary, bytes, token); await AtomicFileReplace.MoveAsync(temporary, PathName, token);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); _gate.Release(); }
    }
}
