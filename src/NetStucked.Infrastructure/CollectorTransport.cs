using System.Net.Sockets;
using System.Text;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed class CollectorTransport : ICollectorTransport
{
    public Task<ICollectorConnection> ConnectAsync(CollectorTarget target, CollectorOptions options, CollectorCredentials credentials, string? hostPin, CancellationToken token) =>
        options.Protocol == CollectorProtocol.Telnet ? TelnetCollectorConnection.ConnectAsync(target, options, credentials, token)
            : CollectorSshNetConnection.ConnectAsync(target, options, credentials, hostPin, token);

}

// Telnet parsing is separate from SSH. Explicit selection/acknowledgement is enforced again here.
public sealed class TelnetCollectorConnection : ICollectorConnection
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly Queue<byte> _pending = new();
    private int _iacState; private byte _verb; private int _negotiationBytes;
    private TelnetCollectorConnection(TcpClient client) { _client = client; _stream = client.GetStream(); }
    public static async Task<ICollectorConnection> ConnectRawAsync(string host, int port, bool explicitlyAllowed, CancellationToken token)
    {
        if (!explicitlyAllowed) throw new CollectorException(CollectorFailure.Transport, "Plaintext Telnet was not acknowledged.");
        var client = new TcpClient();
        try { await client.ConnectAsync(host, port, token).ConfigureAwait(false); return new TelnetCollectorConnection(client); }
        catch { client.Dispose(); throw; }
    }
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
