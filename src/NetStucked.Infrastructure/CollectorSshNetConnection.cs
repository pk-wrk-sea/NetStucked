using System.Net;
using System.Security.Cryptography;
using System.Text;
using NetStucked.Core;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Messages.Transport;
using Renci.SshNet.Messages.Connection;

namespace NetStucked.Infrastructure;

// Collector-specific provider. Terminal deliberately retains its existing provider.
internal sealed class CollectorSshNetConnection : ICollectorConnection
{
    private readonly CancellationTokenSource _stop = new();
    private readonly CollectorSshRelay _relay;
    private readonly SshClient _client;
    private readonly ObservedPassword _password;
    private readonly byte[] _passwordBytes;
    private readonly CollectorSshOutput _output;
    private readonly CollectorShellGuard _guard;
    private readonly SemaphoreSlim _send = new(1, 1);
    private readonly string? _pin;
    private readonly object _closeGate = new();
    private ShellStream? _shell;
    private Session? _session;
    private long _channel = -1;
    private Task _connecting = Task.CompletedTask, _opening = Task.CompletedTask, _writing = Task.CompletedTask;
    private Task? _closing;
    private string? _rejectedPin;
    private CollectorException? _streamFailure;
    private int _stage;
    private enum Stage { Negotiating, Authenticating, OpeningShell, Connected }

    private CollectorSshNetConnection(CollectorTarget target, CollectorCredentials credentials, string? pin, CollectorOptions options, CollectorSshRelay relay)
    {
        _pin = pin?.Trim().TrimEnd('=');
        _relay = relay;
        _output = new(_stop.Token); _guard = new(_output);
        _passwordBytes = Encoding.UTF8.GetBytes(credentials.Password);
        try
        {
        _password = new(credentials.Username, _passwordBytes, _stop.Token, ObserveSession);
        var settings = new ConnectionInfo(target.Host, target.Port, credentials.Username,
            ProxyTypes.Socks5, IPAddress.Loopback.ToString(), _relay.Port, _relay.Username, _relay.Nonce, _password)
        {
            Timeout = TimeSpan.FromSeconds(options.ConnectTimeoutSeconds),
            ChannelCloseTimeout = TimeSpan.FromMilliseconds(500), RetryAttempts = 1, MaxSessions = 1, Encoding = new UTF8Encoding(false)
        };
        Keep(settings.Encryptions, ["aes256-gcm@openssh.com", "aes128-gcm@openssh.com", "chacha20-poly1305@openssh.com", "aes256-ctr", "aes192-ctr", "aes128-ctr"]);
        Keep(settings.HmacAlgorithms, ["hmac-sha2-512-etm@openssh.com", "hmac-sha2-256-etm@openssh.com"]);
        Keep(settings.KeyExchangeAlgorithms, ["curve25519-sha256", "curve25519-sha256@libssh.org", "ecdh-sha2-nistp521", "ecdh-sha2-nistp384", "ecdh-sha2-nistp256", "diffie-hellman-group18-sha512", "diffie-hellman-group16-sha512", "diffie-hellman-group14-sha256", "diffie-hellman-group-exchange-sha256"]);
        Keep(settings.HostKeyAlgorithms, ["ssh-ed25519", "ecdsa-sha2-nistp521", "ecdsa-sha2-nistp384", "ecdsa-sha2-nistp256", "rsa-sha2-512", "rsa-sha2-256"]);
        Keep(settings.CompressionAlgorithms, ["none"]);
        _client = new(settings);
        _client.HostKeyReceived += (_, e) =>
        {
            e.CanTrust = false;
            if (_stop.IsCancellationRequested) return;
            string actual = "SHA256:" + Convert.ToBase64String(SHA256.HashData(e.HostKey)).TrimEnd('=');
            bool trusted = _pin is not null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(_pin));
            if (!trusted)
            {
                _rejectedPin = actual;
                _output.Complete(HostKeyFailure(actual));
                Abort();
                return;
            }
            if (_stop.IsCancellationRequested) return;
            e.CanTrust = true; Interlocked.CompareExchange(ref _stage, (int)Stage.Authenticating, (int)Stage.Negotiating);
        };
        _client.ErrorOccurred += (_, _) => { if (!_stop.IsCancellationRequested) _output.Complete(SafeFailure(null)); };
        }
        catch
        {
            _stop.Cancel(); _output.Complete();
            try { _client?.Dispose(); } catch (Exception) { }
            _password?.Dispose(); CryptographicOperations.ZeroMemory(_passwordBytes);
            throw;
        }
    }
    private static void Keep<T>(IDictionary<string, T> dictionary, string[] names)
    {
        var chosen = names.Where(dictionary.ContainsKey).Select(name => (Name: name, Value: dictionary[name])).ToArray();
        dictionary.Clear(); foreach (var item in chosen) dictionary.Add(item.Name, item.Value);
    }
    public static async Task<ICollectorConnection> ConnectAsync(CollectorTarget target, CollectorOptions options, CollectorCredentials credentials, string? pin, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (target.Port is < 1 or > 65535 || Uri.CheckHostName(target.Host) == UriHostNameType.Unknown)
            throw new CollectorException(CollectorFailure.Transport, "Invalid SSH target host/port.");
        var relay = new CollectorSshRelay(target);
        CollectorSshNetConnection? connection = null;
        try { connection = new(target, credentials, pin, options, relay); await connection.StartAsync(options.ConnectTimeoutSeconds, token).ConfigureAwait(false); return connection; }
        catch (Exception ex)
        {
            CollectorException failure = connection?.SafeFailure(ex) ?? new(CollectorFailure.Transport, "SSH client setup failed; no credentials were sent.");
            try { if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false); else await relay.DisposeAsync().ConfigureAwait(false); } catch (Exception) { }
            if (token.IsCancellationRequested) throw new OperationCanceledException(token);
            throw failure;
        }
    }
    private async Task StartAsync(int seconds, CancellationToken token)
    {
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, _stop.Token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
            using var registration = timeout.Token.Register(Abort);
            try
            {
                _connecting = Task.Run(() => _client.ConnectAsync(timeout.Token));
                await _connecting.ConfigureAwait(false);
                timeout.Token.ThrowIfCancellationRequested();
            }
            catch (Exception) when (timeout.IsCancellationRequested && !token.IsCancellationRequested && _rejectedPin is null)
            { throw new CollectorException(CollectorFailure.Timeout, "SSH connect/authentication timed out; no credential fallback."); }
            finally { CryptographicOperations.ZeroMemory(_passwordBytes); }
        }
        // Channel/PTY/shell receive a fresh budget after successful authentication.
        Volatile.Write(ref _stage, (int)Stage.OpeningShell);
        if (_session is null) throw new CollectorException(CollectorFailure.UnsupportedAuthentication, "SSH session ownership was not established by password authentication.");
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, _stop.Token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
            using var registration = timeout.Token.Register(Abort);
            try
            {
                _opening = Task.Run(() =>
                {
                    _shell = _client.CreateShellStream("vt100", 160, 48, 0, 0, 4096, null);
                    _shell.Closed += (_, _) => _output.Complete();
                    _shell.ErrorOccurred += (_, _) => { if (!_stop.IsCancellationRequested) _output.Complete(SafeFailure(null)); };
                    _shell.DataReceived += (_, _) => _guard.Drain();
                    _guard.AttachDrain(() => _shell.DataAvailable, _shell.Read);
                });
                await _opening.ConfigureAwait(false);
                timeout.Token.ThrowIfCancellationRequested();
            }
            catch (Exception) when (timeout.IsCancellationRequested && !token.IsCancellationRequested && _rejectedPin is null)
            { throw new CollectorException(CollectorFailure.Timeout, "SSH channel/PTY/shell setup timed out; authenticated session was closed."); }
        }
        Volatile.Write(ref _stage, (int)Stage.Connected);
    }
    private static CollectorException HostKeyFailure(string actual) => new(CollectorFailure.HostKey, "SSH host key untrusted/mismatched. Independently verify " + actual + " before entering a pin.");
    private CollectorException SafeFailure(Exception? exception)
    {
        if (_rejectedPin is { } rejected) return HostKeyFailure(rejected);
        if (_streamFailure is { } streamFailure) return streamFailure;
        if (exception is CollectorException classified) return classified;
        if (exception is SshOperationTimeoutException) return new(CollectorFailure.Timeout, "SSH operation timed out; no credential fallback.");
        if (_password.Rejected && !_password.PartialSuccess) return new(CollectorFailure.AuthenticationRejected, "SSH password authentication rejected.");
        if (exception is SshAuthenticationException || _password.PartialSuccess || _password.PasswordChangeRequired)
            return new(CollectorFailure.UnsupportedAuthentication, "SSH server requires unsupported authentication/MFA or password change; no fallback.");
        if (_relay.RemoteConnectFailed && !_password.Attempted) return new(CollectorFailure.TransientTransport, "SSH socket connection failed before authentication.");
        if (exception is SshConnectionException disconnected && disconnected.DisconnectReason == DisconnectReason.KeyExchangeFailed)
            return new(CollectorFailure.Transport, "SSH algorithm negotiation failed. Review the device's supported strong algorithms; no credential fallback or algorithm weakening.");
        if ((Stage)Volatile.Read(ref _stage) == Stage.OpeningShell)
        {
            // Match only pinned SDK-generated fixed strings; never expose remote text.
            if (exception?.GetType() == typeof(SshException) && exception.Message.StartsWith("The pseudo-terminal request was not accepted", StringComparison.Ordinal))
                return new(CollectorFailure.Transport, "SSH authenticated, but the device rejected the PTY request.");
            if (exception?.GetType() == typeof(SshException) && exception.Message.StartsWith("The request to start a shell was not accepted", StringComparison.Ordinal))
                return new(CollectorFailure.Transport, "SSH authenticated, but the device rejected the interactive shell request.");
            return new(CollectorFailure.Transport, "SSH authenticated, but channel/PTY/shell setup failed. Review device session/AAA policy.");
        }
        return new(CollectorFailure.Transport, (Stage)Volatile.Read(ref _stage) == Stage.Connected ? "SSH session disconnected; commands were not replayed." : "SSH connection/algorithm negotiation failed before collection; no credential fallback.");
    }
    private void Abort()
    {
        // Unblock bounded SDK stream writers BEFORE touching library disposal.
        _stop.Cancel(); _output.Complete(); _relay.Abort();
    }
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token) => _output.ReadAsync(buffer, token);
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        if (bytes.Length is < 1 or > 4096) throw new CollectorException(CollectorFailure.Command, "SSH input chunk limit exceeded.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token, token);
        await _send.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            using var registration = linked.Token.Register(Abort);
            Task writing;
            lock (_closeGate)
            {
                linked.Token.ThrowIfCancellationRequested();
                byte[] owned = bytes.ToArray();
                writing = _writing = Task.Run(async () =>
                {
                    try
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        await _shell!.WriteAsync(owned, linked.Token).ConfigureAwait(false);
                        await _shell.FlushAsync(linked.Token).ConfigureAwait(false);
                    }
                    finally { CryptographicOperations.ZeroMemory(owned); }
                });
            }
            await writing.ConfigureAwait(false);
        }
        catch (Exception ex) { if (token.IsCancellationRequested) throw new OperationCanceledException(token); throw SafeFailure(ex); }
        finally { _send.Release(); }
    }
    public ValueTask DisposeAsync()
    {
        lock (_closeGate) return new(_closing ??= CloseAsync());
    }
    private async Task CloseAsync()
    {
        Abort();
        try { await _connecting.ConfigureAwait(false); } catch (Exception) { }
        try { await _opening.ConfigureAwait(false); } catch (Exception) { }
        try { await _writing.ConfigureAwait(false); } catch (Exception) { }
        bool cleanupFailed = false;
        try { await _relay.DisposeAsync().ConfigureAwait(false); } catch (Exception) { cleanupFailed = true; }
        await Task.Run(() =>
        {
            // The relay is closed and all start/connect operations have returned.
            // Join the SDK session/reader before disposing shell-owned wait handles.
            try { _client.Dispose(); } catch (Exception) { cleanupFailed = true; }
            try { _shell?.Dispose(); } catch (Exception) { cleanupFailed = true; }
        }).ConfigureAwait(false);
        if (_session is not null)
        {
            _session.ChannelOpenConfirmationReceived -= ChannelOpened;
            _session.ChannelDataReceived -= ChannelData;
            _session.ChannelExtendedDataReceived -= ExtendedData;
            _session.ChannelEofReceived -= ChannelEof;
        }
        try { _password.Dispose(); } catch (Exception) { cleanupFailed = true; }
        CryptographicOperations.ZeroMemory(_passwordBytes);
        if (cleanupFailed) throw new CollectorException(CollectorFailure.Transport, "SSH session cleanup failed after its sockets were closed.");
    }
    private void ObserveSession(Session session)
    {
        if (_session is not null) throw new CollectorException(CollectorFailure.UnsupportedAuthentication, "Repeated SSH authentication is unsupported.");
        _session = session;
        // These handlers are registered before CreateShellStream creates its channel.
        session.ChannelOpenConfirmationReceived += ChannelOpened;
        session.ChannelDataReceived += ChannelData;
        session.ChannelExtendedDataReceived += ExtendedData;
        session.ChannelEofReceived += ChannelEof;
    }
    private void ChannelOpened(object? sender, MessageEventArgs<ChannelOpenConfirmationMessage> e)
    {
        if ((Stage)Volatile.Read(ref _stage) != Stage.OpeningShell || Interlocked.CompareExchange(ref _channel, e.Message.LocalChannelNumber, -1) != -1)
            UnexpectedChannel();
    }
    private bool IsOwnedChannel(uint channel)
    {
        if (Interlocked.Read(ref _channel) == channel) return true;
        UnexpectedChannel(); return false;
    }
    private void UnexpectedChannel()
    {
        var failure = new CollectorException(CollectorFailure.Transport, "Unexpected SSH channel; collection stopped.");
        Interlocked.CompareExchange(ref _streamFailure, failure, null);
        _output.Complete(failure); Abort();
    }
    private void ChannelData(object? sender, MessageEventArgs<ChannelDataMessage> e)
    {
        if (IsOwnedChannel(e.Message.LocalChannelNumber)) Capture(e.Message.Data, e.Message.Offset, e.Message.Size, false);
    }
    private void ExtendedData(object? sender, MessageEventArgs<ChannelExtendedDataMessage> e)
    {
        if (!IsOwnedChannel(e.Message.LocalChannelNumber)) return;
        if (e.Message.DataTypeCode != 1) { UnexpectedChannel(); return; }
        Capture(e.Message.Data, 0, e.Message.Data.Length, true);
    }
    private void Capture(byte[] bytes, int offset, int count, bool error)
    {
        if (_stop.IsCancellationRequested) return;
        try { _guard.Capture(bytes, offset, count, error); }
        catch (CollectorException failure)
        {
            Interlocked.CompareExchange(ref _streamFailure, failure, null); _output.Complete(failure); Abort();
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }
    private void ChannelEof(object? sender, MessageEventArgs<ChannelEofMessage> e) { if (IsOwnedChannel(e.Message.LocalChannelNumber)) _output.Complete(); }
    private sealed class ObservedPassword(string username, byte[] bytes, CancellationToken token, Action<Session> observe) : PasswordAuthenticationMethod(username, bytes)
    {
        public bool Attempted { get; private set; }
        public bool Rejected { get; private set; }
        public bool PartialSuccess { get; private set; }
        public bool PasswordChangeRequired { get; private set; }
        public override AuthenticationResult Authenticate(Session session)
        {
            token.ThrowIfCancellationRequested();
            if (Attempted) throw new CollectorException(CollectorFailure.UnsupportedAuthentication, "Repeated password authentication is unsupported; no credentials were resent.");
            Attempted = true;
            observe(session);
            PasswordExpired += (_, _) => { PasswordChangeRequired = true; throw new CollectorException(CollectorFailure.UnsupportedAuthentication, "SSH password change is unsupported; no fallback."); };
            AuthenticationResult result = base.Authenticate(session);
            Rejected = result == AuthenticationResult.Failure; PartialSuccess = result == AuthenticationResult.PartialSuccess;
            return result;
        }
    }
}
