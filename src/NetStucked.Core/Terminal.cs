using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace NetStucked.Core;

public enum TerminalProtocol { Ssh, Telnet }
public enum TerminalAuthentication { Password, PrivateKey }
public enum TerminalState { Disconnected, Connecting, Connected, Disconnecting, Failed }
public sealed record TerminalProfile(Guid Id, string Name, string Host, int Port = 22, TerminalProtocol Protocol = TerminalProtocol.Ssh,
    string Username = "", string HostPin = "", TerminalAuthentication Authentication = TerminalAuthentication.Password, string KeyPath = "", bool AllowTelnet = false);
public sealed class TerminalSecrets(string password, string passphrase)
{
    [JsonIgnore] public string Password { get; } = password;
    [JsonIgnore] public string Passphrase { get; } = passphrase;
    public override string ToString() => "[memory-only terminal secrets]";
}
public sealed class TerminalRequest(TerminalProfile profile, TerminalSecrets secrets, int columns = 100, int rows = 30)
{
    public TerminalProfile Profile { get; } = profile;
    [JsonIgnore] public TerminalSecrets Secrets { get; } = secrets;
    public int Columns { get; } = Math.Clamp(columns, 20, 400);
    public int Rows { get; } = Math.Clamp(rows, 5, 200);
    public void Validate()
    {
        if (Profile is null || Secrets is null || Profile.Host is null || Profile.Name is null || Profile.Username is null || Profile.HostPin is null || Profile.KeyPath is null || !Enum.IsDefined(Profile.Protocol) || !Enum.IsDefined(Profile.Authentication)) throw new ArgumentException("Invalid terminal profile.");
        if (Profile.Host.Length is < 1 or > 253 || Profile.Host.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)) || Profile.Name.Length > 80 || Profile.Name.Any(char.IsControl))
            throw new ArgumentException("Enter one host/IP and a name up to 80 characters.");
        string endpoint = Profile.Host.Contains(':') ? $"[{Profile.Host}]:{Profile.Port}" : $"{Profile.Host}:{Profile.Port}";
        _ = CollectorInputParser.Parse(endpoint, Profile.Port);
        if (Profile.Protocol == TerminalProtocol.Telnet)
        { if (!Profile.AllowTelnet) throw new ArgumentException("Explicitly allow plaintext Telnet before connecting."); return; }
        if (Profile.Username.Length is < 1 or > 256 || Profile.Username.Any(char.IsControl)) throw new ArgumentException("Enter an SSH username without control characters.");
        if (Profile.HostPin.Length > 0) _ = CollectorInputParser.ParsePins(endpoint + " " + Profile.HostPin);
        if (Profile.Authentication == TerminalAuthentication.Password && Secrets.Password.Length is < 1 or > 512) throw new ArgumentException("Enter an SSH password (maximum 512 characters).");
        if (Profile.Authentication == TerminalAuthentication.PrivateKey && !Path.IsPathFullyQualified(Profile.KeyPath)) throw new ArgumentException("Choose an absolute private-key file path.");
        if (Secrets.Passphrase.Length > 512) throw new ArgumentException("Key passphrase exceeds 512 characters.");
    }
    public override string ToString() => "Terminal request (secrets excluded)";
}
public sealed record TerminalStatus(Guid Generation, TerminalState State, string Message);
public sealed record TerminalFrame(Guid Generation, byte[] Bytes);
public interface ITerminalConnection : ICollectorConnection
{ ValueTask ResizeAsync(int columns, int rows, CancellationToken token); }
public interface ITerminalTransport
{ Task<ITerminalConnection> ConnectAsync(TerminalRequest request, CancellationToken token); }
public interface ITerminalProfileStore
{
    Task<IReadOnlyList<TerminalProfile>> LoadAsync(CancellationToken token);
    Task SaveAsync(IReadOnlyList<TerminalProfile> profiles, CancellationToken token);
}
public static class TerminalInputPolicy
{
    public static bool RequiresConfirmation(string text) => text.Contains('\r') || text.Contains('\n');
    public static bool ValidPaste(string text) => text.Length <= 65536 && !text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));
}

public sealed class TerminalSession(ITerminalTransport transport) : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _operations = new(1);
    private CancellationTokenSource? _cancel;
    private ITerminalConnection? _connection;
    private Channel<TerminalFrame>? _output;
    private Channel<byte[]>? _input;
    private Task _completion = Task.CompletedTask;
    private bool _disposed;
    public Guid Generation { get; private set; }
    public TerminalState State { get; private set; }
    public event Action<TerminalStatus>? Changed;
    private void Publish(TerminalState state, string message) { TerminalStatus status; lock (_gate) { State = state; status = new(Generation, state, message); } Changed?.Invoke(status); }
    public async Task ConnectAsync(TerminalRequest request, CancellationToken token)
    {
        request.Validate(); await _operations.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TerminalSession));
            await StopInternalAsync().ConfigureAwait(false);
            lock (_gate) { Generation = Guid.NewGuid(); _output = Channel.CreateBounded<TerminalFrame>(64); _input = Channel.CreateBounded<byte[]>(64); }
            _cancel = CancellationTokenSource.CreateLinkedTokenSource(token);
            Publish(TerminalState.Connecting, "Connecting; verifying host identity and authenticating…");
            try
            {
                _connection = await transport.ConnectAsync(request, _cancel.Token).ConfigureAwait(false);
                Publish(TerminalState.Connected, request.Profile.Protocol == TerminalProtocol.Ssh ? "SSH host verified · connected" : "Telnet · plaintext · log in interactively in the console");
                _completion = RunAsync(_connection, Generation, _output, _input, _cancel);
            }
            catch (OperationCanceledException) { Publish(TerminalState.Disconnected, "Connection cancelled."); _output.Writer.TryComplete(); }
            catch (CollectorException ex) { Publish(TerminalState.Failed, ex.Message); _output.Writer.TryComplete(); }
            catch (Exception) { Publish(TerminalState.Failed, "Terminal connection failed; check endpoint, credentials/key and SSH compatibility."); _output.Writer.TryComplete(); }
        }
        finally { _operations.Release(); }
    }
    private async Task RunAsync(ITerminalConnection connection, Guid generation, Channel<TerminalFrame> output, Channel<byte[]> input, CancellationTokenSource cancel)
    {
        async Task Read()
        {
            byte[] buffer = new byte[4096]; int count;
            while ((count = await connection.ReadAsync(buffer, cancel.Token).ConfigureAwait(false)) > 0)
                await output.Writer.WriteAsync(new(generation, buffer.AsSpan(0, count).ToArray()), cancel.Token).ConfigureAwait(false);
        }
        async Task Write()
        {
            await foreach (byte[] bytes in input.Reader.ReadAllAsync(cancel.Token).ConfigureAwait(false))
                await connection.WriteAsync(bytes, cancel.Token).ConfigureAwait(false);
        }
        Task read = Read(), write = Write(); bool failed = false;
        try { await await Task.WhenAny(read, write).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        catch (Exception) { failed = true; }
        finally
        {
            await cancel.CancelAsync().ConfigureAwait(false);
            try { await connection.DisposeAsync().ConfigureAwait(false); } catch (Exception) { failed = true; }
            try { await Task.WhenAll(read, write).ConfigureAwait(false); } catch (Exception) { }
            input.Writer.TryComplete(); output.Writer.TryComplete();
            if (Generation == generation) Publish(failed ? TerminalState.Failed : TerminalState.Disconnected, failed ? "Terminal transport closed after an I/O failure." : "Disconnected.");
        }
    }
    public ChannelReader<TerminalFrame>? Output => _output?.Reader;
    public bool TrySend(Guid generation, ReadOnlySpan<byte> bytes)
    {
        lock (_gate)
        {
            if (generation != Generation || State != TerminalState.Connected || bytes.Length is < 1 or > 4096) return false;
            return _input?.Writer.TryWrite(bytes.ToArray()) == true;
        }
    }
    public ValueTask ResizeAsync(int columns, int rows, CancellationToken token) => _connection is { } connection && State == TerminalState.Connected
        ? connection.ResizeAsync(Math.Clamp(columns, 20, 400), Math.Clamp(rows, 5, 200), token) : ValueTask.CompletedTask;
    public async Task DisconnectAsync()
    {
        try { _cancel?.Cancel(); } catch (ObjectDisposedException) { }
        await _operations.WaitAsync().ConfigureAwait(false);
        try { await StopInternalAsync().ConfigureAwait(false); } finally { _operations.Release(); }
    }
    private async Task StopInternalAsync()
    {
        if (_cancel is not null) { Publish(TerminalState.Disconnecting, "Disconnecting; waiting for cleanup…"); await _cancel.CancelAsync().ConfigureAwait(false); await _completion.ConfigureAwait(false); _cancel.Dispose(); _cancel = null; }
        _connection = null; Publish(TerminalState.Disconnected, "Disconnected.");
    }
    public async ValueTask DisposeAsync() { _disposed = true; await DisconnectAsync().ConfigureAwait(false); }
}
