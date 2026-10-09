using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

/// <summary>Direct resolver queries with validated questions, bounded wire parsing and TCP fallback.</summary>
public sealed class DnsTestClient : IDnsTestClient
{
    public async Task<DnsTestResult> QueryAsync(DnsTestRequest input, CancellationToken token)
    {
        if (input.TimeoutMs is < 250 or > 60000 || !Enum.IsDefined(input.Type)) throw new ArgumentException("Invalid DNS query settings.");
        string name = DiagnosticInput.DnsName(input.Name, input.Type);
        byte[] request = WifiDnsQuery.Request(name, (ushort)RandomNumberGenerator.GetInt32(65536), (ushort)input.Type);
        var watch = Stopwatch.StartNew(); string transport = "UDP";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(input.TimeoutMs);
        try
        {
            using var udp = new Socket(input.Server.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            await udp.ConnectAsync(input.Server, timeout.Token).ConfigureAwait(false);
            await udp.SendAsync(request, SocketFlags.None, timeout.Token).ConfigureAwait(false);
            byte[] buffer = new byte[65535];
            int count = await udp.ReceiveAsync(buffer, SocketFlags.None, timeout.Token).ConfigureAwait(false);
            byte[] response = buffer[..count];
            ValidateQuestion(response, request);
            if ((response[2] & 2) != 0)
            {
                transport = "TCP fallback";
                using var tcp = new Socket(input.Server.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                await tcp.ConnectAsync(input.Server, timeout.Token).ConfigureAwait(false);
                using var stream = new NetworkStream(tcp, false);
                byte[] prefix = new byte[2]; BinaryPrimitives.WriteUInt16BigEndian(prefix, (ushort)request.Length);
                await stream.WriteAsync(prefix, timeout.Token).ConfigureAwait(false); await stream.WriteAsync(request, timeout.Token).ConfigureAwait(false);
                await stream.ReadExactlyAsync(prefix, timeout.Token).ConfigureAwait(false);
                int size = BinaryPrimitives.ReadUInt16BigEndian(prefix); if (size < 12) throw new IOException("Invalid DNS TCP frame.");
                response = new byte[size]; await stream.ReadExactlyAsync(response, timeout.Token).ConfigureAwait(false);
            }
            var parsed = Parse(response, request);
            return new(DateTimeOffset.Now, name, input.Type.ToString(), input.Server.ToString(), parsed.Status, watch.Elapsed.TotalMilliseconds, transport, parsed.Records, "");
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return Failure("Timeout", "DNS request timed out; no completed DNS response."); }
        catch (Exception ex) when (ex is SocketException or IOException or ArgumentException) { return Failure("Error", ex.Message.Truncate(512)); }
        DnsTestResult Failure(string status, string error) => new(DateTimeOffset.Now, name, input.Type.ToString(), input.Server.ToString(), status, null, transport, [], error);
    }
    public static (string Status, IReadOnlyList<DnsRecord> Records) Parse(byte[] message, byte[] request)
    {
        int offset = ValidateQuestion(message, request);
        if ((message[2] & 2) != 0) throw new IOException("Truncated TCP DNS response.");
        int rcode = message[3] & 15;
        string status = rcode switch { 0 => "PASS", 1 => "FORMERR", 2 => "SERVFAIL", 3 => "NXDOMAIN", 4 => "NOTIMP", 5 => "REFUSED", _ => "RCODE " + rcode };
        var records = new List<DnsRecord>();
        int totalRecords = U16(message, 6) + U16(message, 8) + U16(message, 10);
        if (totalRecords > 128) throw new IOException("DNS response exceeds 128 records.");
        for (int section = 0; section < 3; section++)
        {
            int count = U16(message, 6 + 2 * section);
            if (records.Count + count > 128) throw new IOException("DNS response exceeds 128 records.");
            for (int i = 0; i < count; i++)
            {
                string owner = Name(message, ref offset); Need(message, offset, 10);
                ushort type = U16(message, offset), cls = U16(message, offset + 2); uint ttl = BinaryPrimitives.ReadUInt32BigEndian(message.AsSpan(offset + 4, 4)); int length = U16(message, offset + 8); offset += 10;
                Need(message, offset, length); int end = offset + length, cursor = offset; string value;
                switch (type)
                {
                    case 1 when length == 4: case 28 when length == 16: value = new IPAddress(message.AsSpan(offset, length)).ToString(); cursor = end; break;
                    case 1: case 28: throw new IOException("Invalid DNS address record length.");
                    case 5: case 12: case 2: value = Name(message, ref cursor); break;
                    case 15: Need(message, cursor, 2); value = U16(message, cursor) + " "; cursor += 2; value += Name(message, ref cursor); break;
                    case 33: Need(message, cursor, 6); value = $"{U16(message, cursor)} {U16(message, cursor + 2)} {U16(message, cursor + 4)} "; cursor += 6; value += Name(message, ref cursor); break;
                    case 16:
                        var chunks = new List<string>();
                        while (cursor < end) { int len = message[cursor++]; if (len > end - cursor) throw new IOException("Invalid TXT data."); chunks.Add(Encoding.UTF8.GetString(message, cursor, len)); cursor += len; }
                        value = string.Join(" ", chunks); break;
                    default: value = Convert.ToHexString(message.AsSpan(offset, Math.Min(length, 256))); cursor = end; break;
                }
                if (cursor != end) throw new IOException("Invalid DNS record length.");
                if (cls == 1) records.Add(new(owner, Enum.IsDefined((DnsRecordType)type) ? ((DnsRecordType)type).ToString() : type == 2 ? "NS" : "TYPE" + type, ttl, value.Truncate(4096), section switch { 0 => "Answer", 1 => "Authority", _ => "Additional" }));
                offset = end;
            }
        }
        if (status == "PASS" && !records.Any(r => r.Section == "Answer")) status = "NODATA";
        return (status, records);
    }
    private static int ValidateQuestion(byte[] response, byte[] request)
    {
        Need(response, 0, 12); Need(request, 0, 12);
        if (U16(response, 0) != U16(request, 0) || (response[2] & 0x80) == 0 || (response[2] & 0x78) != 0 || U16(response, 4) != 1) throw new IOException("Invalid DNS response identity.");
        int r = 12, q = 12; string returned = Name(response, ref r), asked = Name(request, ref q); Need(response, r, 4); Need(request, q, 4);
        if (!StringComparer.OrdinalIgnoreCase.Equals(returned, asked) || !response.AsSpan(r, 4).SequenceEqual(request.AsSpan(q, 4))) throw new IOException("DNS question mismatch.");
        return r + 4;
    }
    private static string Name(byte[] data, ref int offset)
    {
        int cursor = offset, resume = -1; var labels = new List<string>(); var seen = new HashSet<int>();
        for (int steps = 0; steps < 128; steps++)
        {
            Need(data, cursor, 1); if (!seen.Add(cursor)) throw new IOException("DNS compression loop."); int len = data[cursor++];
            if (len == 0) { offset = resume < 0 ? cursor : resume; string name = string.Join('.', labels); if (name.Length > 253) throw new IOException("DNS name exceeds limit."); return name; }
            if ((len & 0xC0) == 0xC0) { Need(data, cursor, 1); if (resume < 0) resume = cursor + 1; cursor = ((len & 63) << 8) | data[cursor]; continue; }
            if (len > 63) throw new IOException("Invalid DNS label."); Need(data, cursor, len); labels.Add(Encoding.ASCII.GetString(data, cursor, len)); cursor += len;
        }
        throw new IOException("DNS compression exceeds limit.");
    }
    private static ushort U16(byte[] data, int position) { Need(data, position, 2); return BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position, 2)); }
    private static void Need(byte[] data, int position, int count) { if (position < 0 || count < 0 || position > data.Length - count) throw new IOException("Incomplete DNS response."); }
}
