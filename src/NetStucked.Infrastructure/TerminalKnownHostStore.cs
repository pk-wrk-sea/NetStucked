using System.Net;
using System.Text.Json;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed class TerminalKnownHostStore(UserSettingsStore settings) : ITerminalKnownHostStore
{
    private readonly SemaphoreSlim _gate = new(1);
    private string FileName => Path.Combine(settings.DirectoryPath, "terminal-known-hosts.json");
    private sealed record Document(int Schema, TerminalKnownHost[] Hosts);
    public static string NormalizeHost(string host) => IPAddress.TryParse(host.Trim('[', ']'), out var ip) ? ip.ToString() : host.TrimEnd('.').ToLowerInvariant();
    private static void Validate(TerminalKnownHost host)
    {
        if (host is null || host.Host is null || host.Algorithm is null || host.Fingerprint is null || host.AcceptedAt == default || host.Host.Length is < 1 or > 253 || host.Host.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)) || host.Algorithm.Length is < 1 or > 80 || host.Algorithm.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '@' or '.')) || host.Fingerprint.Length > 100)
            throw new InvalidDataException("Invalid saved SSH host key.");
        string endpoint = host.Host.Contains(':') ? $"[{host.Host}]:{host.Port}" : $"{host.Host}:{host.Port}";
        try { _ = CollectorInputParser.Parse(endpoint, host.Port); _ = CollectorInputParser.ParsePins(endpoint + " " + host.Fingerprint); }
        catch (ArgumentException) { throw new InvalidDataException("Invalid saved SSH host key."); }
    }
    private async Task<TerminalKnownHost[]> ReadAsync(CancellationToken token)
    {
        if (!File.Exists(FileName)) return [];
        await using var stream = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        if (stream.Length > 2 * 1024 * 1024) throw new InvalidDataException("Saved SSH host keys exceed the size limit.");
        try
        {
            var document = await JsonSerializer.DeserializeAsync<Document>(stream, cancellationToken: token).ConfigureAwait(false);
            if (document is null || document.Schema != 1 || document.Hosts is null || document.Hosts.Length > 1000) throw new InvalidDataException("Invalid saved SSH host-key database.");
            foreach (var host in document.Hosts) Validate(host);
            var rows = document.Hosts.Select(h => h with { Host = NormalizeHost(h.Host) }).ToArray();
            if (rows.Select(h => (h.Host, h.Port)).Distinct().Count() != rows.Length) throw new InvalidDataException("Duplicate saved SSH host keys.");
            return rows;
        }
        catch (JsonException) { throw new InvalidDataException("Saved SSH host keys could not be read. Restore the known-hosts file from a trusted backup."); }
    }
    public async Task<IReadOnlyList<TerminalKnownHost>> LoadAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try { return await ReadAsync(token).ConfigureAwait(false); } finally { _gate.Release(); }
    }
    public async Task<bool> SaveAsync(TerminalKnownHost host, string? expectedPreviousFingerprint, CancellationToken token)
    {
        Validate(host); host = host with { Host = NormalizeHost(host.Host) };
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var rows = (await ReadAsync(token).ConfigureAwait(false)).ToList();
            var previous = rows.SingleOrDefault(h => h.Host == host.Host && h.Port == host.Port);
            if (previous?.Fingerprint != expectedPreviousFingerprint) return false;
            if (previous is null && rows.Count >= 1000) throw new InvalidDataException("Maximum 1000 saved SSH hosts. Remove unused keys before saving another.");
            rows.RemoveAll(h => h.Host == host.Host && h.Port == host.Port); rows.Add(host);
            await WriteAsync(rows, token).ConfigureAwait(false); return true;
        }
        finally { _gate.Release(); }
    }
    public async Task<bool> RemoveAsync(TerminalKnownHost host, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var rows = (await ReadAsync(token).ConfigureAwait(false)).ToList();
            int removed = rows.RemoveAll(h => h.Host == NormalizeHost(host.Host) && h.Port == host.Port && h.Fingerprint == host.Fingerprint);
            if (removed == 0) return false;
            await WriteAsync(rows, token).ConfigureAwait(false); return true;
        }
        finally { _gate.Release(); }
    }
    private async Task WriteAsync(IReadOnlyList<TerminalKnownHost> hosts, CancellationToken token)
    {
        Directory.CreateDirectory(settings.DirectoryPath); string temporary = FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new Document(1, hosts.ToArray())), token).ConfigureAwait(false);
            await AtomicFileReplace.MoveAsync(temporary, FileName, token).ConfigureAwait(false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public sealed class TerminalHostTrust(ITerminalKnownHostStore store, ITerminalHostKeyPrompt prompt) : ITerminalHostTrust
{
    private readonly SemaphoreSlim _promptGate = new(1);
    public async Task<bool> VerifyAsync(string host, int port, string algorithm, string fingerprint, CancellationToken token)
    {
        string normalized = TerminalKnownHostStore.NormalizeHost(host);
        try
        {
            TerminalKnownHost? Find(IReadOnlyList<TerminalKnownHost> rows) => rows.SingleOrDefault(h => h.Host == normalized && h.Port == port);
            if (Find(await store.LoadAsync(token).ConfigureAwait(false)) is { } known && known.Fingerprint == fingerprint) return true;
            await _promptGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var previous = Find(await store.LoadAsync(token).ConfigureAwait(false));
                if (previous?.Fingerprint == fingerprint) return true;
                var presented = new TerminalKnownHost(normalized, port, algorithm, fingerprint, DateTimeOffset.UtcNow);
                var decision = await prompt.ConfirmAsync(new(presented, previous), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (decision == TerminalHostKeyDecision.AcceptOnce) return true;
                if (decision != TerminalHostKeyDecision.AcceptAndSave) return false;
                if (!await store.SaveAsync(presented with { AcceptedAt = DateTimeOffset.UtcNow }, previous?.Fingerprint, token).ConfigureAwait(false))
                    throw new CollectorException(CollectorFailure.HostKey, "Saved host key changed while confirmation was open. Connect again to review the current key.");
                token.ThrowIfCancellationRequested();
                return true;
            }
            finally { _promptGate.Release(); }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        { throw new CollectorException(CollectorFailure.HostKey, "Saved SSH host keys could not be read or saved. Check access to terminal-known-hosts.json; connection stopped before authentication."); }
    }
}
