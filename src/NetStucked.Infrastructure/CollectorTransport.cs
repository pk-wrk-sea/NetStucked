using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using NetStucked.Core;
using Tmds.Ssh;

namespace NetStucked.Infrastructure;

public sealed class CollectorTransport : ICollectorTransport
{
    public async Task<ICollectorConnection> ConnectAsync(CollectorTarget target, CollectorOptions options, CollectorCredentials credentials, string? hostPin, CancellationToken token)
    {
        if (options.Protocol == CollectorProtocol.Telnet) return await TelnetCollectorConnection.ConnectAsync(target, options, credentials, token).ConfigureAwait(false);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.ConnectTimeoutSeconds));
        string? rejectedKey = null; bool passwordSent = false, passwordRejected = false;
        var settings = new SshClientSettings
        {
            HostName = target.Host, Port = target.Port, UserName = credentials.Username,
            ConnectTimeout = TimeSpan.FromSeconds(options.ConnectTimeoutSeconds),
            AutoConnect = false, AutoReconnect = false, BatchMode = true,
            UserKnownHostsFilePaths = [], GlobalKnownHostsFilePaths = [], UpdateKnownHostsFileAfterAuthentication = false,
            DefaultWindowSize = 256 * 1024, BannerHandler = null,
            Credentials = [new PasswordCredential((context, _) =>
            {
                // The library requests attempt 2 only after an explicit SSH password failure.
                // Return no password: never send a second attempt for the same profile.
                if (context.Attempt > 1) { passwordRejected = true; return ValueTask.FromResult<string?>(null); }
                passwordSent = true; return ValueTask.FromResult<string?>(credentials.Password);
            })],
            HostAuthentication = (context, _) =>
            {
                string actual = "SHA256:" + context.ConnectionInfo.ServerKey.Key.SHA256FingerPrint;
                bool trusted = hostPin is not null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(hostPin));
                if (!trusted) rejectedKey = actual;
                return ValueTask.FromResult(trusted);
            }
        };
        var client = new SshClient(settings);
        try
        {
            await client.ConnectAsync(timeout.Token).ConfigureAwait(false);
            var process = await client.ExecuteShellAsync(new ExecuteOptions
            {
                AllocateTerminal = true, TerminalType = "vt100", TerminalSize = new(160, 48), WindowSize = 256 * 1024,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = new UTF8Encoding(false)
            }, timeout.Token).ConfigureAwait(false);
            return new SshCollectorConnection(client, process);
        }
        catch (Exception ex)
        {
            client.Dispose();
            if (rejectedKey is not null) throw new CollectorException(CollectorFailure.HostKey, "SSH host key untrusted/mismatched. Independently verify " + rejectedKey + " before entering a pin.");
            if (token.IsCancellationRequested) throw new OperationCanceledException(token);
            if (ex is OperationCanceledException || ex.Message.Contains(" - Timeout - ", StringComparison.Ordinal)) throw new CollectorException(CollectorFailure.Timeout, "SSH connection timed out; credentials were not changed.");
            // Tmds.Ssh 0.26's reason enum is internal; match its pinned exception category,
            // never forward raw exception messages or server-controlled banner text to UI/logs.
            if (ex is SshConnectionException && ex.Message.Contains(" - AuthenticationFailed - ", StringComparison.Ordinal))
                throw new CollectorException(passwordRejected ? CollectorFailure.AuthenticationRejected : CollectorFailure.UnsupportedAuthentication,
                    passwordRejected ? "SSH password authentication rejected." : "Server requires unsupported authentication/MFA; no fallback.");
            if (!passwordSent && (ex is SocketException || ex.InnerException is SocketException)) throw new CollectorException(CollectorFailure.TransientTransport, "SSH socket connection failed before authentication.");
            throw new CollectorException(CollectorFailure.Transport, "SSH negotiation/session failed; no credential fallback.");
        }
    }
    private sealed class SshCollectorConnection(SshClient client, RemoteProcess process) : ICollectorConnection
    {
        private readonly Decoder _stdout = Encoding.UTF8.GetDecoder(), _stderr = Encoding.UTF8.GetDecoder();
        private readonly byte[] _raw = new byte[4096]; private readonly char[] _chars = new char[4096];
        private byte[] _pending = []; private int _offset; private bool _ended;
        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
        {
            while (_offset == _pending.Length)
            {
                if (_ended) return 0;
                // One channel read drains either stdout or extended stderr. Separate decoders
                // prevent split UTF-8 on one stream being corrupted by the other stream.
                var read = await process.ReadAsync(_raw, _raw, token).ConfigureAwait(false);
                if (read.BytesRead == 0)
                {
                    _ended = true;
                    int first = _stdout.GetChars([], _chars, true);
                    int second = _stderr.GetChars([], _chars.AsSpan(first), true);
                    _pending = Encoding.UTF8.GetBytes(_chars.AsSpan(0, first + second).ToString());
                }
                else
                {
                    int chars = (read.IsError ? _stderr : _stdout).GetChars(_raw, 0, read.BytesRead, _chars, 0);
                    _pending = Encoding.UTF8.GetBytes(_chars.AsSpan(0, chars).ToString());
                }
                _offset = 0;
            }
            int count = Math.Min(buffer.Length, _pending.Length - _offset);
            _pending.AsMemory(_offset, count).CopyTo(buffer); _offset += count; return count;
        }
        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token) => process.WriteAsync(bytes, token);
        public ValueTask DisposeAsync() { process.Dispose(); client.Dispose(); return ValueTask.CompletedTask; }
    }
}

// Telnet parsing is separate from SSH. Explicit selection/acknowledgement is enforced again here.
public sealed class TelnetCollectorConnection : ICollectorConnection
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly Queue<byte> _pending = new();
    private int _iacState; private byte _verb; private int _negotiationBytes;
    private TelnetCollectorConnection(TcpClient client) { _client = client; _stream = client.GetStream(); }
    public static async Task<ICollectorConnection> ConnectAsync(CollectorTarget target, CollectorOptions options, CollectorCredentials credentials, CancellationToken token)
    {
        if (!options.AllowTelnet) throw new CollectorException(CollectorFailure.Transport, "Plaintext Telnet was not acknowledged.");
        var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(options.ConnectTimeoutSeconds));
        try
        {
            await client.ConnectAsync(target.Host, target.Port, timeout.Token).ConfigureAwait(false);
            var telnet = new TelnetCollectorConnection(client);
            var recognizer = new CollectorPromptRecognizer(); var decoder = Encoding.UTF8.GetDecoder(); byte[] buffer = new byte[2048]; char[] chars = new char[2048];
            bool sentUser = false, sentPassword = false;
            while (true)
            {
                int count = await telnet.ReadAsync(buffer, timeout.Token).ConfigureAwait(false);
                if (count == 0) throw new CollectorException(CollectorFailure.Transport, "Telnet disconnected during login.");
                recognizer.Push(new string(chars, 0, decoder.GetChars(buffer, 0, count, chars, 0)));
                if (recognizer.Mfa) throw new CollectorException(CollectorFailure.UnsupportedAuthentication, "Telnet MFA/OTP is unsupported; no fallback.");
                if (recognizer.Rejected) throw new CollectorException(CollectorFailure.AuthenticationRejected, "Telnet authentication rejected.");
                if (recognizer.Login)
                {
                    if (sentUser) throw new CollectorException(CollectorFailure.AuthenticationRejected, "Telnet returned to login prompt.");
                    sentUser = true; await telnet.WriteAsync(Encoding.UTF8.GetBytes(credentials.Username + "\n"), timeout.Token).ConfigureAwait(false); recognizer = new();
                }
                else if (recognizer.Password)
                {
                    if (sentPassword) throw new CollectorException(CollectorFailure.AuthenticationRejected, "Telnet password rejected.");
                    sentPassword = true; await telnet.WriteAsync(Encoding.UTF8.GetBytes(credentials.Password + "\n"), timeout.Token).ConfigureAwait(false); recognizer = new();
                }
                else if (recognizer.InitialPrompt() is { } prompt)
                {
                    foreach (byte b in Encoding.UTF8.GetBytes(prompt)) telnet._pending.Enqueue(b);
                    return telnet;
                }
            }
        }
        catch (Exception ex)
        {
            client.Dispose();
            if (token.IsCancellationRequested) throw new OperationCanceledException(token);
            if (ex is CollectorException) throw;
            if (ex is OperationCanceledException) throw new CollectorException(CollectorFailure.Timeout, "Telnet login timed out; no credential fallback.");
            throw new CollectorException(ex is SocketException ? CollectorFailure.TransientTransport : CollectorFailure.Transport, "Telnet connection failed.");
        }
    }
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
    {
        if (_pending.Count > 0)
        {
            int count = 0; while (_pending.Count > 0 && count < buffer.Length) buffer.Span[count++] = _pending.Dequeue(); return count;
        }
        byte[] raw = new byte[Math.Min(buffer.Length, 4096)];
        while (true)
        {
            int count = await _stream.ReadAsync(raw, token).ConfigureAwait(false); if (count == 0) return 0;
            int used = 0; var replies = new List<byte>();
            for (int i = 0; i < count; i++)
            {
                byte b = raw[i];
                if (_iacState == 0) { if (b == 255) _iacState = 1; else if (b != 0) buffer.Span[used++] = b; }
                else if (_iacState == 1)
                {
                    if (b == 255) { buffer.Span[used++] = 255; _iacState = 0; }
                    else if (b is 251 or 252 or 253 or 254) { _verb = b; _iacState = 2; }
                    else _iacState = b == 250 ? 3 : 0;
                }
                else if (_iacState == 2)
                {
                    if (_verb == 253) replies.AddRange(new byte[] { 255, 252, b }); // DONT support DO options
                    if (_verb == 251) replies.AddRange(new byte[] { 255, 254, b }); // refuse WILL options
                    _iacState = 0;
                }
                else if (_iacState == 3) { if (b == 255) _iacState = 4; }
                else if (_iacState == 4) _iacState = b == 240 ? 0 : 3;
                if (_iacState != 0 && ++_negotiationBytes > 8192) throw new CollectorException(CollectorFailure.Transport, "Telnet negotiation limit exceeded.");
            }
            if (replies.Count > 0) await _stream.WriteAsync(replies.ToArray(), token).ConfigureAwait(false);
            if (used > 0) return used;
        }
    }
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        // NVT lines use CR LF and literal IAC is doubled; UTF-8 has no BOM.
        byte[] encoded = bytes.ToArray().SelectMany(b => b == 255 ? new byte[] { 255, 255 } : b == 10 ? new byte[] { 13, 10 } : new byte[] { b }).ToArray();
        await _stream.WriteAsync(encoded, token).ConfigureAwait(false);
    }
    public ValueTask DisposeAsync() { _stream.Dispose(); _client.Dispose(); return ValueTask.CompletedTask; }
}
