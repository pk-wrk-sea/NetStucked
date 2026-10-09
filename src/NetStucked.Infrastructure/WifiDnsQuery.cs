using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace NetStucked.Infrastructure;

/// <summary>Small, bounded DNS client for service checks using the selected adapter's actual DNS server.</summary>
public static class WifiDnsQuery
{
    public static byte[] Request(string host, ushort id, ushort type = 1)
    {
        host = new System.Globalization.IdnMapping().GetAscii(host.TrimEnd('.'));
        using var stream = new MemoryStream(); byte[] header = new byte[12]; BinaryPrimitives.WriteUInt16BigEndian(header, id); header[2] = 1; header[5] = 1; stream.Write(header);
        if (host.Length is < 1 or > 253) throw new ArgumentException("Invalid DNS name.");
        foreach (string label in host.Split('.')) { if (label.Length is < 1 or > 63) throw new ArgumentException("Invalid DNS label."); stream.WriteByte((byte)label.Length); stream.Write(Encoding.ASCII.GetBytes(label)); }
        stream.WriteByte(0); stream.WriteByte(0); stream.WriteByte((byte)type); stream.WriteByte(0); stream.WriteByte(1); return stream.ToArray();
    }
    public static IReadOnlyList<IPAddress> Parse(byte[] message, byte[] request)
    {
        if (message.Length < 12 || message.Length > 65535 || BinaryPrimitives.ReadUInt16BigEndian(message) != BinaryPrimitives.ReadUInt16BigEndian(request) || (message[2] & 0x80) == 0 || (message[2] & 0x78) != 0) throw new IOException("Invalid DNS response.");
        if ((message[2] & 2) != 0) throw new IOException("DNS response is truncated; TCP DNS fallback is required.");
        if ((message[3] & 15) != 0) throw new IOException($"DNS server returned RCODE {message[3] & 15}.");
        if (BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(4)) != 1) throw new IOException("DNS question mismatch.");
        int offset = 12;
        string returned = ReadName(message, ref offset); int requestOffset = 12; string asked = ReadName(request, ref requestOffset);
        Require(message, offset, 4);
        if (!StringComparer.OrdinalIgnoreCase.Equals(returned, asked) || !message.AsSpan(offset, 4).SequenceEqual(request.AsSpan(requestOffset, 4))) throw new IOException("DNS question mismatch.");
        ushort expectedType = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(offset)); offset += 4;
        int answers = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(6)); if (answers > 1024) throw new IOException("DNS answer count exceeds limit.");
        var result = new List<IPAddress>(); var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); var addresses = new List<(string Name, IPAddress Address)>();
        for (int i = 0; i < answers; i++)
        {
            string owner = ReadName(message, ref offset); Require(message, offset, 10);
            ushort type = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(offset)), cls = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(offset + 2)), length = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(offset + 8)); offset += 10; Require(message, offset, length);
            if (cls == 1 && type == expectedType && ((type == 1 && length == 4) || (type == 28 && length == 16))) addresses.Add((owner, new IPAddress(message.AsSpan(offset, length))));
            if (cls == 1 && type == 5) { int cname = offset; string alias = ReadName(message, ref cname); if (cname != offset + length) throw new IOException("Invalid DNS CNAME length."); aliases[owner] = alias; }
            offset += length;
        }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { asked }; string name = asked;
        for (int i = 0; i < 16 && aliases.TryGetValue(name, out var alias) && names.Add(alias); i++) name = alias;
        result.AddRange(addresses.Where(a => names.Contains(a.Name)).Select(a => a.Address).Distinct());
        if (result.Count == 0) throw new IOException("DNS returned no requested address records."); return result;
    }
    private static string ReadName(byte[] data, ref int offset)
    {
        int cursor = offset, resume = -1, steps = 0; var labels = new List<string>(); var seen = new HashSet<int>();
        while (true)
        {
            Require(data, cursor, 1); if (++steps > 128 || !seen.Add(cursor)) throw new IOException("Invalid DNS compression loop."); byte length = data[cursor++];
            if (length == 0) { offset = resume < 0 ? cursor : resume; break; }
            if ((length & 0xC0) == 0xC0) { Require(data, cursor, 1); if (resume < 0) resume = cursor + 1; cursor = ((length & 63) << 8) | data[cursor]; continue; }
            if (length > 63) throw new IOException("Invalid DNS label."); Require(data, cursor, length); labels.Add(Encoding.ASCII.GetString(data, cursor, length)); cursor += length;
        }
        string value = string.Join('.', labels); if (value.Length > 253) throw new IOException("DNS name exceeds limit."); return value;
    }
    private static void Require(byte[] data, int offset, int count) { if (offset < 0 || count < 0 || offset > data.Length - count) throw new IOException("Incomplete DNS response."); }
    public static async Task<IReadOnlyList<IPAddress>> QueryAsync(string host, IPEndPoint server, Func<AddressFamily, SocketType, ProtocolType, Socket> createSocket, ushort type, CancellationToken token)
    {
        byte[] request = Request(host, (ushort)RandomNumberGenerator.GetInt32(65536), type);
        using var socket = createSocket(server.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        await socket.ConnectAsync(server, token).ConfigureAwait(false); await socket.SendAsync(request, SocketFlags.None, token).ConfigureAwait(false);
        byte[] buffer = new byte[4096]; int size = await socket.ReceiveAsync(buffer, SocketFlags.None, token).ConfigureAwait(false);
        if (size >= 12 && (buffer[2] & 2) != 0)
        {
            using var tcp = createSocket(server.AddressFamily, SocketType.Stream, ProtocolType.Tcp); await tcp.ConnectAsync(server, token).ConfigureAwait(false);
            using var stream = new NetworkStream(tcp, false); byte[] prefix = new byte[2]; BinaryPrimitives.WriteUInt16BigEndian(prefix, (ushort)request.Length);
            await stream.WriteAsync(prefix, token).ConfigureAwait(false); await stream.WriteAsync(request, token).ConfigureAwait(false); await stream.ReadExactlyAsync(prefix, token).ConfigureAwait(false);
            int length = BinaryPrimitives.ReadUInt16BigEndian(prefix); if (length < 12) throw new IOException("Invalid DNS TCP length."); buffer = new byte[length]; await stream.ReadExactlyAsync(buffer, token).ConfigureAwait(false); return Parse(buffer, request);
        }
        return Parse(buffer[..size], request);
    }
}
