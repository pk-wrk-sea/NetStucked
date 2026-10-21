using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

// SSH.NET does not expose its connecting socket during KEX/authentication. This
// per-attempt, authenticated loopback relay gives cancellation an owned socket.
// It forwards SSH ciphertext unchanged; the SSH peer and pin remain the target.
internal sealed class CollectorSshRelay : IAsyncDisposable
{
    private readonly CollectorTarget _target;
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly object _gate = new();
    private readonly byte[] _nonce = RandomNumberGenerator.GetBytes(32);
    private TcpClient? _accepted, _remote;
    private readonly Task _running;
    private int _closed;
    public int Port { get; }
    public string Username => "collector";
    public string Nonce { get; }
    public bool RemoteConnectFailed { get; private set; }
    public Task Completion => _running;

    public CollectorSshRelay(CollectorTarget target)
    {
        _target = target;
        Nonce = Convert.ToHexString(_nonce);
        _listener.Start(1);
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _running = RunAsync();
    }
    public void Abort()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0) return;
        _stop.Cancel(); _listener.Stop();
        lock (_gate) { _accepted?.Dispose(); _remote?.Dispose(); }
    }
    private async Task RunAsync()
    {
        Task? upstream = null, downstream = null;
        try
        {
            var accepted = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
            lock (_gate)
            {
                if (Volatile.Read(ref _closed) != 0) { accepted.Dispose(); return; }
                _accepted = accepted;
            }
            _listener.Stop(); // Exactly one client for this attempt, including rejected clients.
            NetworkStream stream = accepted.GetStream();
            byte[] hello = new byte[2]; await ReadAsync(stream, hello).ConfigureAwait(false);
            if (hello[0] != 5 || hello[1] is < 1 or > 16) return;
            byte[] methods = new byte[hello[1]]; await ReadAsync(stream, methods).ConfigureAwait(false);
            if (!methods.Contains((byte)2)) { await stream.WriteAsync(new byte[] { 5, 255 }, _stop.Token).ConfigureAwait(false); return; }
            await stream.WriteAsync(new byte[] { 5, 2 }, _stop.Token).ConfigureAwait(false);
            byte[] auth = new byte[2]; await ReadAsync(stream, auth).ConfigureAwait(false);
            if (auth[0] != 1 || auth[1] is < 1 or > 64) return;
            byte[] user = new byte[auth[1]]; await ReadAsync(stream, user).ConfigureAwait(false);
            byte[] length = new byte[1]; await ReadAsync(stream, length).ConfigureAwait(false);
            if (length[0] is < 1 or > 128) return;
            byte[] nonce = new byte[length[0]]; await ReadAsync(stream, nonce).ConfigureAwait(false);
            byte[] expected = Encoding.ASCII.GetBytes(Nonce);
            bool authenticated;
            try { authenticated = Encoding.ASCII.GetString(user) == Username && CryptographicOperations.FixedTimeEquals(nonce, expected); }
            finally { CryptographicOperations.ZeroMemory(nonce); CryptographicOperations.ZeroMemory(expected); }
            await stream.WriteAsync(new byte[] { 1, authenticated ? (byte)0 : (byte)1 }, _stop.Token).ConfigureAwait(false);
            if (!authenticated) return;
            byte[] request = new byte[4]; await ReadAsync(stream, request).ConfigureAwait(false);
            if (request[0] != 5 || request[1] != 1 || request[2] != 0) return;
            string host;
            switch (request[3])
            {
                case 1: byte[] v4 = new byte[4]; await ReadAsync(stream, v4).ConfigureAwait(false); host = new IPAddress(v4).ToString(); break;
                case 4: byte[] v6 = new byte[16]; await ReadAsync(stream, v6).ConfigureAwait(false); host = new IPAddress(v6).ToString(); break;
                case 3:
                    await ReadAsync(stream, length).ConfigureAwait(false); if (length[0] == 0) return;
                    byte[] name = new byte[length[0]]; await ReadAsync(stream, name).ConfigureAwait(false); host = new UTF8Encoding(false, true).GetString(name); break;
                default: return;
            }
            byte[] port = new byte[2]; await ReadAsync(stream, port).ConfigureAwait(false);
            bool sameHost = IPAddress.TryParse(host, out var supplied) && IPAddress.TryParse(_target.Host, out var intended)
                ? supplied.Equals(intended) : host.Equals(_target.Host, StringComparison.OrdinalIgnoreCase);
            if (!sameHost || (port[0] << 8 | port[1]) != _target.Port)
            { await stream.WriteAsync(new byte[] { 5, 2, 0, 1, 0, 0, 0, 0, 0, 0 }, _stop.Token).ConfigureAwait(false); return; }
            // No outbound socket or DNS resolution occurs before nonce AND destination validation.
            var remote = new TcpClient();
            lock (_gate)
            {
                if (Volatile.Read(ref _closed) != 0) { remote.Dispose(); return; }
                _remote = remote;
            }
            try { await remote.ConnectAsync(_target.Host, _target.Port, _stop.Token).ConfigureAwait(false); }
            catch (SocketException) { RemoteConnectFailed = true; throw; }
            await stream.WriteAsync(new byte[] { 5, 0, 0, 1, 0, 0, 0, 0, 0, 0 }, _stop.Token).ConfigureAwait(false);
            upstream = ForwardAsync(stream, remote.GetStream()); downstream = ForwardAsync(remote.GetStream(), stream);
            await Task.WhenAny(upstream, downstream).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException or DecoderFallbackException) { }
        finally
        {
            Abort();
            if (upstream is not null && downstream is not null)
            { try { await Task.WhenAll(upstream, downstream).ConfigureAwait(false); } catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { } }
        }
    }
    private Task ReadAsync(NetworkStream stream, Memory<byte> bytes) => stream.ReadExactlyAsync(bytes, _stop.Token).AsTask();
    private async Task ForwardAsync(NetworkStream source, NetworkStream destination)
    {
        byte[] buffer = new byte[4096];
        while (true)
        {
            int count = await source.ReadAsync(buffer, _stop.Token).ConfigureAwait(false);
            if (count == 0) return;
            await destination.WriteAsync(buffer.AsMemory(0, count), _stop.Token).ConfigureAwait(false);
        }
    }
    public async ValueTask DisposeAsync()
    {
        Abort(); await _running.ConfigureAwait(false);
        CryptographicOperations.ZeroMemory(_nonce);
        // Nonce strings are one-use proxy tokens, not user credentials; never persisted/logged.
    }
}
