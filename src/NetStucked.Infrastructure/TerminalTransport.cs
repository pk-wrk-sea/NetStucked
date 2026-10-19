using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using NetStucked.Core;
using Tmds.Ssh;

namespace NetStucked.Infrastructure;
public sealed class TerminalTransport(ITerminalHostTrust? hostTrust = null) : ITerminalTransport
{
    public async Task<ITerminalConnection> ConnectAsync(TerminalRequest request, CancellationToken token)
    {
        request.Validate(); var profile = request.Profile;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(15));
        if (profile.Protocol == TerminalProtocol.Telnet)
            return new RawTelnet(await TelnetCollectorConnection.ConnectRawAsync(profile.Host, profile.Port, profile.AllowTelnet, deadline.Token).ConfigureAwait(false));
        string? observed = null; CollectorException? trustFailure = null;
        Credential credential;
        char[]? keyData = null;
        if (profile.Authentication == TerminalAuthentication.PrivateKey)
        {
            try
            {
                await using var file = new FileStream(profile.KeyPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                if (file.Length > 1024 * 1024) throw new CollectorException(CollectorFailure.UnsupportedAuthentication, "Private-key file exceeds 1 MiB.");
                using var reader = new StreamReader(file);
                string text = await reader.ReadToEndAsync(deadline.Token).ConfigureAwait(false);
                if (!text.TrimStart().StartsWith("-----BEGIN OPENSSH PRIVATE KEY-----", StringComparison.Ordinal)) throw new CollectorException(CollectorFailure.UnsupportedAuthentication, "Choose an OpenSSH private key (BEGIN OPENSSH PRIVATE KEY). Other key-file formats are unsupported.");
                keyData = text.ToCharArray();
                credential = new PrivateKeyCredential(keyData, request.Secrets.Passphrase);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new CollectorException(CollectorFailure.UnsupportedAuthentication, "Private-key file could not be read. Check its path and file access."); }
        }
        else credential = new PasswordCredential(request.Secrets.Password);
        var settings = new SshClientSettings
        {
            HostName = profile.Host, Port = profile.Port, UserName = profile.Username, Credentials = [credential],
            AutoConnect = false, AutoReconnect = false, BatchMode = hostTrust is null, BannerHandler = null,
            UserKnownHostsFilePaths = [], GlobalKnownHostsFilePaths = [], UpdateKnownHostsFileAfterAuthentication = false,
            DefaultWindowSize = 256 * 1024, ConnectTimeout = TimeSpan.FromSeconds(15),
            HostAuthentication = async (context, callbackToken) =>
            {
                string actual = "SHA256:" + context.ConnectionInfo.ServerKey.Key.SHA256FingerPrint;
                if (hostTrust is not null)
                {
                    context.SuspendConnectTimeout();
                    try
                    {
                        bool accepted = await hostTrust.VerifyAsync(profile.Host, profile.Port, context.ConnectionInfo.ServerKey.Key.Type.ToString(), actual, callbackToken).ConfigureAwait(false);
                        if (!accepted) trustFailure = new CollectorException(CollectorFailure.HostKey, "SSH host key was not accepted. No login credentials were sent.");
                        return accepted;
                    }
                    catch (CollectorException ex) { trustFailure = ex; return false; }
                    finally { context.ResumeConnectTimeout(); }
                }
                bool trusted = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(profile.HostPin.TrimEnd('=')));
                if (!trusted) observed = actual;
                return trusted;
            }
        };
        var client = new SshClient(settings);
        try
        {
            // The library pauses its connect timer only while the operator reviews a host key.
            deadline.CancelAfter(Timeout.InfiniteTimeSpan);
            await client.ConnectAsync(deadline.Token).ConfigureAwait(false);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            var process = await client.ExecuteShellAsync(new ExecuteOptions
            {
                AllocateTerminal = true, TerminalType = "xterm-256color", TerminalSize = new(request.Columns, request.Rows), WindowSize = 256 * 1024,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = new UTF8Encoding(false)
            }, deadline.Token).ConfigureAwait(false);
            return new SshTerminalConnection(client, process);
        }
        catch (Exception ex)
        {
            client.Dispose();
            if (token.IsCancellationRequested) throw new OperationCanceledException(token);
            if (trustFailure is not null) throw trustFailure;
            if (observed is not null) throw new CollectorException(CollectorFailure.HostKey, "SSH host key untrusted/mismatched. Independently verify " + observed + " before entering a pin.");
            if (ex is OperationCanceledException) throw new CollectorException(CollectorFailure.Timeout, "SSH connect/authentication timed out.");
            throw new CollectorException(CollectorFailure.UnsupportedAuthentication, "SSH login failed. Check password/key/passphrase and supported authentication; no fallback or algorithm weakening was attempted.");
        }
        finally { if (keyData is not null) Array.Clear(keyData); }
    }
    private sealed class RawTelnet(ICollectorConnection inner) : ITerminalConnection
    {
        private bool _previousCr;
        public ValueTask<int> ReadAsync(Memory<byte> bytes, CancellationToken token) => inner.ReadAsync(bytes, token);
        public ValueTask ResizeAsync(int columns, int rows, CancellationToken token) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => inner.DisposeAsync();
        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            var normalized = new List<byte>(bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes.Span[i];
                if (b == 10 && _previousCr) { _previousCr = false; continue; }
                _previousCr = b == 13;
                normalized.Add(b == 13 ? (byte)10 : b);
            }
            return inner.WriteAsync(normalized.ToArray(), token);
        }
    }
}
