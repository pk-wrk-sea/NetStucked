using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

// Only the immutable options/target/result DTOs may be serialized. CollectorRun/credentials never are.
[SupportedOSPlatform("windows")]
public sealed class CollectorCaptureFactory : ICollectorCaptureFactory
{
    private readonly ConcurrentDictionary<Guid, Lazy<string>> _folders = new();
    public Task<ICollectorCapture> CreateAsync(CollectorRun run, CollectorTarget target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string folder = _folders.GetOrAdd(run.Id, _ => new Lazy<string>(() =>
        {
            string path = Path.Combine(run.Options.OutputFolder, "NetStucked-" + run.Id.ToString("N"));
            var security = new DirectorySecurity(); security.SetAccessRuleProtection(true, false);
            using var identity = WindowsIdentity.GetCurrent();
            var current = identity.User ?? throw new UnauthorizedAccessException();
            foreach (var sid in new[] { current, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
                security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(path).Create(security);
            return path;
        })).Value;
        // Limit retained factory bookkeeping; capture objects keep their own directory paths.
        if (_folders.Count > 16) foreach (var id in _folders.Keys.Where(id => id != run.Id).Take(_folders.Count - 16)) _folders.TryRemove(id, out _);
        string name = string.Concat(target.Host.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' ? c : '_'));
        if (name.Length > 64) name = name[..64];
        string path = Path.Combine(folder, name + "-" + target.Port + "-" + Guid.NewGuid().ToString("N"));
        var secrets = new[] { run.Auth1.Username, run.Auth1.Password, run.Auth1.EnableSecret, run.Auth2?.Username ?? "", run.Auth2?.Password ?? "", run.Auth2?.EnableSecret ?? "" };
        return Task.FromResult<ICollectorCapture>(new CollectorCapture(path, run.Options, secrets));
    }
}

public sealed class CollectorRedactor
{
    private readonly string[] _secrets;
    private readonly int _overlap;
    private string _pending = "";
    public CollectorRedactor(IEnumerable<string> secrets)
    { _secrets = secrets.Where(s => s.Length > 0).Distinct().OrderByDescending(s => s.Length).ToArray(); _overlap = _secrets.Length == 0 ? 0 : _secrets.Max(s => s.Length) - 1; }
    public string Push(string text, bool final = false)
    {
        _pending += text; int safe = final ? _pending.Length : Math.Max(0, _pending.Length - _overlap); int index = 0;
        if (!final && safe > 0 && char.IsHighSurrogate(_pending[safe - 1])) safe--;
        var output = new StringBuilder();
        while (index < safe)
        {
            string? match = null;
            foreach (string secret in _secrets) if (_pending.AsSpan(index).StartsWith(secret, StringComparison.Ordinal)) { match = secret; break; }
            if (match is not null) { output.Append("[REDACTED]"); index += match.Length; }
            else output.Append(_pending[index++]);
        }
        _pending = _pending[index..]; return output.ToString();
    }
}

public sealed class CollectorCapture : ICollectorCapture
{
    private readonly string _base;
    private readonly CollectorOptions _options;
    private readonly CollectorRedactor _redactor;
    private readonly FileStream _stream;
    private bool _closed;
    private string _preview = "";
    private long _bytes;
    public string Preview => _preview;
    public CollectorCapture(string path, CollectorOptions options, IEnumerable<string> secrets)
    {
        _base = path; _options = options; _redactor = new(secrets);
        _stream = new FileStream(path + ".capture.tmp", FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
    }
    public Task AppendAsync(string text, CancellationToken token) => WriteAsync(_redactor.Push(text), token);
    private async Task WriteAsync(string text, CancellationToken token)
    {
        byte[] bytes = new UTF8Encoding(false).GetBytes(text);
        if (_bytes + bytes.Length > _options.MaxCaptureBytes) throw new CollectorException(CollectorFailure.OutputLimit, "Capture byte limit reached; partial output retained.");
        await _stream.WriteAsync(bytes, token).ConfigureAwait(false); _bytes += bytes.Length;
        _preview += text;
        if (_preview.Length > 8192) _preview = _preview[^8192..];
    }
    public async Task<CollectorResult> CompleteAsync(CollectorResult result, IReadOnlyList<CollectorCommandEvidence> commands, CancellationToken token)
    {
        if (_closed) throw new InvalidOperationException("Capture already finalized.");
        try { await WriteAsync(_redactor.Push("", true), token).ConfigureAwait(false); }
        catch (CollectorException ex) when (ex.Failure == CollectorFailure.OutputLimit) { result = result with { State = CollectorDeviceState.Partial, Detail = "Capture byte limit reached; final redaction tail omitted." }; }
        await _stream.FlushAsync(token).ConfigureAwait(false); await _stream.DisposeAsync().ConfigureAwait(false); _closed = true;
        string source = _base + ".capture.tmp";
        string sha;
        await using (var input = File.OpenRead(source)) sha = Convert.ToHexString(await SHA256.HashDataAsync(input, token).ConfigureAwait(false)).ToLowerInvariant();
        if (_options.WriteTxt) { File.Move(source, _base + ".txt"); source = _base + ".txt"; }
        result = result with { TxtPath = _options.WriteTxt ? source : "", Bytes = _bytes, Sha256 = sha, Preview = (_bytes > 8192 ? "[Preview is truncated; full retained output is in capture files.]\n" : "") + _preview };
        if (_options.WriteJson)
        {
            try
            {
            string tempJson = _base + ".json.tmp";
            await using (var output = new FileStream(tempJson, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous))
            {
                using var writer = new Utf8JsonWriter(output);
                writer.WriteStartObject(); writer.WriteNumber("schema", 1); writer.WriteString("protocol", _options.Protocol.ToString()); writer.WriteString("deviceProfile", _options.DeviceProfile);
                writer.WriteString("transformation", "UTF-8 terminal transcript; entered credentials redacted; not secret-free configuration");
                writer.WritePropertyName("result"); JsonSerializer.Serialize(writer, result with { Preview = "" }, new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
                writer.WritePropertyName("commands"); JsonSerializer.Serialize(writer, commands);
                writer.WritePropertyName("outputChunks"); writer.WriteStartArray();
                using var reader = new StreamReader(source, new UTF8Encoding(false), false, 4096);
                char[] buffer = new char[4096]; int count; string carry = "";
                while ((count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) > 0)
                {
                    string chunk = carry + new string(buffer, 0, count); carry = "";
                    if (char.IsHighSurrogate(chunk[^1])) { carry = chunk[^1..]; chunk = chunk[..^1]; }
                    writer.WriteStringValue(chunk); await writer.FlushAsync(token).ConfigureAwait(false);
                }
                if (carry.Length > 0) writer.WriteStringValue(carry);
                writer.WriteEndArray(); writer.WriteEndObject(); await writer.FlushAsync(token).ConfigureAwait(false);
            }
            File.Move(tempJson, _base + ".json"); result = result with { JsonPath = _base + ".json" };
            }
            catch (Exception ex) when (_options.WriteTxt && ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            { result = result with { State = CollectorDeviceState.Partial, Detail = "TXT capture retained; JSON finalization failed. Check output storage." }; }
        }
        if (!_options.WriteTxt) File.Delete(source);
        return result;
    }
    public async ValueTask DisposeAsync()
    {
        if (!_closed) { await _stream.DisposeAsync().ConfigureAwait(false); _closed = true; }
        // Failed temporary captures are removed; already finalized evidence is never deleted.
        foreach (string path in new[] { _base + ".capture.tmp", _base + ".json.tmp" })
                try { if (File.Exists(path)) File.Delete(path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
