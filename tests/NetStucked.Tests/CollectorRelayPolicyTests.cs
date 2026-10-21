using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public sealed class CollectorRelayPolicyTests
{
    [Fact]
    public async Task ActualCollectorKexOffersStrongCtrEtmAndCancelsOwnedSocketDuringKexStall()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(1);
        using var cancellation = new CancellationTokenSource();
        var target = new CollectorTarget(1, "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, "Owned stalled SSH handshake", null);
        var options = new CollectorOptions(CollectorProtocol.Ssh, "Cisco IOS / IOS XE", Path.GetTempPath(), ConnectTimeoutSeconds: 15);
        Task<ICollectorConnection> connection = new CollectorTransport().ConnectAsync(target, options, new("owned-qa", "owned-ephemeral-password", ""), "SHA256:" + new string('A', 43), cancellation.Token);
        TcpClient? peer = null;
        try
        {
            peer = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(4));
            using var fixtureDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            NetworkStream wire = peer.GetStream();
            await wire.WriteAsync(Encoding.ASCII.GetBytes("SSH-2.0-owned-stalled-kex-fixture\r\n"), fixtureDeadline.Token);
            string identification = await ReadLineAsync(wire, fixtureDeadline.Token);
            Assert.StartsWith("SSH-2.0-", identification);
            var names = await ReadKexNamesAsync(wire, fixtureDeadline.Token);
            Assert.Contains("aes256-ctr", names[2]); Assert.Contains("aes256-ctr", names[3]);
            var strongCiphers = new[] { "aes128-ctr", "aes192-ctr", "aes256-ctr", "aes128-gcm@openssh.com", "aes256-gcm@openssh.com", "chacha20-poly1305@openssh.com" };
            foreach (int direction in new[] { 2, 3 }) Assert.All(names[direction], cipher => Assert.Contains(cipher, strongCiphers));
            foreach (int direction in new[] { 4, 5 })
            {
                Assert.Contains("hmac-sha2-512-etm@openssh.com", names[direction]);
                Assert.All(names[direction], mac => Assert.Contains(mac, new[] { "hmac-sha2-256-etm@openssh.com", "hmac-sha2-512-etm@openssh.com" }));
            }
            Assert.All(names[0], kex => { Assert.DoesNotContain("sha1", kex, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("group1-", kex, StringComparison.OrdinalIgnoreCase); });
            Assert.DoesNotContain(names[1], host => host.StartsWith("ssh-rsa", StringComparison.Ordinal) || host.StartsWith("ssh-dss", StringComparison.Ordinal));
            Assert.Equal(new[] { "none" }, names[6]); Assert.Equal(new[] { "none" }, names[7]);

            // Actual KEXINIT was observed, so this does not merely cancel DNS/version exchange.
            await Task.Delay(100); var watch = Stopwatch.StartNew(); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await connection.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), "Closing the owned relay must unblock the provider's untokened KEX wait promptly.");
            byte[] remaining = new byte[4096]; int read;
            do { read = await wire.ReadAsync(remaining, fixtureDeadline.Token); } while (read > 0);
            Assert.Equal(0, read);
        }
        finally
        {
            cancellation.Cancel(); peer?.Dispose(); listener.Stop();
            try { if (await connection.WaitAsync(TimeSpan.FromSeconds(5)) is { } value) await value.DisposeAsync(); } catch (Exception) { }
        }
    }

    [Fact]
    public async Task AlreadyCancelledCollectorOperationNeverDialsTheOwnedTarget()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(1);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var target = new CollectorTarget(1, "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, "Owned no-dial fixture", null);
        Task<TcpClient> accepted = listener.AcceptTcpClientAsync();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CollectorTransport().ConnectAsync(target, new(CollectorProtocol.Ssh, "Cisco IOS / IOS XE", Path.GetTempPath()), new("owned-qa", "owned-ephemeral-password", ""), "SHA256:" + new string('A', 43), cancellation.Token));
            await Task.Delay(100); Assert.False(accepted.IsCompletedSuccessfully);
        }
        finally { listener.Stop(); try { (await accepted).Dispose(); } catch (SocketException) { } }
    }

    private static async Task<string> ReadLineAsync(Stream stream, CancellationToken token)
    {
        var bytes = new List<byte>(); byte[] one = new byte[1];
        while (bytes.Count < 255) { await stream.ReadExactlyAsync(one, token); bytes.Add(one[0]); if (one[0] == 10) return Encoding.ASCII.GetString(bytes.ToArray()); }
        throw new IOException("Owned fixture received an oversized SSH identification.");
    }
    private static async Task<string[][]> ReadKexNamesAsync(Stream stream, CancellationToken token)
    {
        byte[] header = new byte[4]; await stream.ReadExactlyAsync(header, token); int length = BinaryPrimitives.ReadInt32BigEndian(header);
        Assert.InRange(length, 32, 68532); byte[] packet = new byte[length]; await stream.ReadExactlyAsync(packet, token);
        Assert.Equal(20, packet[1]); int offset = 18; var names = new string[10][];
        for (int i = 0; i < names.Length; i++)
        {
            Assert.InRange(offset + 4, 0, packet.Length); int count = BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(offset, 4)); offset += 4;
            Assert.InRange(count, 0, packet.Length - offset); names[i] = Encoding.ASCII.GetString(packet, offset, count).Split(',', StringSplitOptions.RemoveEmptyEntries); offset += count;
        }
        return names;
    }
}
