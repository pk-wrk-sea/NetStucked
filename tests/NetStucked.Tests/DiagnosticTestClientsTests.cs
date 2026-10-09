using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public sealed class DiagnosticTestClientsTests
{
    [Fact] public void HttpTargetsPreserveCaseSensitivePathsAndQueryValues()
    {
        Assert.Equal(3, DiagnosticInput.Lines("https://example.test/Case?q=A\nhttps://example.test/case?q=A\nhttps://example.test/Case?q=a\nhttps://example.test/Case?q=A", 64, caseSensitive: true).Length);
        Assert.Single(DiagnosticInput.Lines("EXAMPLE.test\nexample.test", 128));
    }
    [Theory]
    [InlineData(DnsRecordType.A, "192.0.2.7")]
    [InlineData(DnsRecordType.AAAA, "2001:db8::7")]
    [InlineData(DnsRecordType.CNAME, "alias.example")]
    [InlineData(DnsRecordType.PTR, "host.example")]
    [InlineData(DnsRecordType.MX, "10 mail.example")]
    [InlineData(DnsRecordType.TXT, "hello world")]
    [InlineData(DnsRecordType.SRV, "10 20 443 service.example")]
    public void ReadsRequestedRecordsWithActualTtlAndSection(DnsRecordType type, string expected)
    {
        var query = WifiDnsQuery.Request("test.example", 1234, (ushort)type);
        var parsed = DnsTestClient.Parse(Answer(query, type), query);
        Assert.Equal("PASS", parsed.Status); var record = Assert.Single(parsed.Records);
        Assert.Equal(expected, record.Value); Assert.Equal(60u, record.Ttl); Assert.Equal("Answer", record.Section);
    }
    [Theory] [InlineData(0, "NODATA")] [InlineData(2, "SERVFAIL")] [InlineData(3, "NXDOMAIN")] [InlineData(5, "REFUSED")]
    public void NegativeDnsResponsesAreDistinctFromNetworkTimeout(int code, string expected)
    {
        var query = WifiDnsQuery.Request("test.example", 1234); byte[] response = query.ToArray(); response[2] = 0x81; response[3] = (byte)code;
        var parsed = DnsTestClient.Parse(response, query); Assert.Equal(expected, parsed.Status); Assert.Empty(parsed.Records);
    }
    [Fact] public void RejectsWrongQuestionAndCompressionLoopsAndOversizedAnswers()
    {
        var query = WifiDnsQuery.Request("test.example", 1234);
        byte[] wrong = Answer(query, DnsRecordType.A); wrong[0] ^= 1; Assert.Throws<IOException>(() => DnsTestClient.Parse(wrong, query));
        wrong = Answer(query, DnsRecordType.A); wrong[13] = (byte)'x'; Assert.Throws<IOException>(() => DnsTestClient.Parse(wrong, query));
        wrong = Answer(query, DnsRecordType.A); wrong[query.Length] = 0xc0; wrong[query.Length + 1] = (byte)query.Length; Assert.Throws<IOException>(() => DnsTestClient.Parse(wrong, query));
        wrong = Answer(query, DnsRecordType.A); wrong[7] = 129; Assert.Throws<IOException>(() => DnsTestClient.Parse(wrong, query));
        Assert.Throws<IOException>(() => DnsTestClient.Parse([0, 1], query));
    }
    [Fact] public void PtrSupportsIpv4AndIpv6AndQueryInputIsBounded()
    {
        Assert.Equal("7.2.0.192.in-addr.arpa", DiagnosticInput.DnsName("192.0.2.7", DnsRecordType.PTR));
        Assert.EndsWith(".ip6.arpa", DiagnosticInput.DnsName("2001:db8::7", DnsRecordType.PTR));
        Assert.Equal("_https._tcp.example", DiagnosticInput.DnsName("_https._tcp.example.", DnsRecordType.SRV));
        Assert.Throws<ArgumentException>(() => DiagnosticInput.DnsName("bad name", DnsRecordType.A));
        Assert.Throws<ArgumentException>(() => DiagnosticInput.DnsName(new string('a', 64) + ".example", DnsRecordType.A));
        Assert.Throws<ArgumentException>(() => DiagnosticInput.Lines(string.Join('\n', Enumerable.Range(0, 129)), 128));
        Assert.Equal(5353, DiagnosticInput.DnsServer("[::1]:5353").Port);
    }
    [Fact] public async Task ActualUdpQueryAndTruncatedTcpFallbackUseTheSameQuestion()
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)udp.Client.LocalEndPoint!;
        var listener = new TcpListener(endpoint); listener.Start();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Task server = Task.Run(async () =>
        {
            var packet = await udp.ReceiveAsync(deadline.Token);
            byte[] truncated = packet.Buffer.ToArray(); truncated[2] = 0x83;
            await udp.SendAsync(truncated, packet.RemoteEndPoint, deadline.Token);
            using var connection = await listener.AcceptTcpClientAsync(deadline.Token); using var stream = connection.GetStream();
            byte[] prefix = new byte[2]; await stream.ReadExactlyAsync(prefix, deadline.Token); byte[] question = new byte[BinaryPrimitives.ReadUInt16BigEndian(prefix)]; await stream.ReadExactlyAsync(question, deadline.Token);
            Assert.Equal(packet.Buffer, question); byte[] response = Answer(question, DnsRecordType.A); BinaryPrimitives.WriteUInt16BigEndian(prefix, (ushort)response.Length);
            await stream.WriteAsync(prefix, deadline.Token); await stream.WriteAsync(response, deadline.Token);
        });
        try
        {
            var result = await new DnsTestClient().QueryAsync(new("test.example", DnsRecordType.A, endpoint, 3000), deadline.Token);
            Assert.Equal("PASS", result.Status); Assert.Equal("TCP fallback", result.Transport); Assert.Equal("192.0.2.7", result.Answers); Assert.True(result.ElapsedMs >= 0); await server;
        }
        finally { listener.Stop(); }
    }
    [Fact] public async Task DnsTimeoutHasNoInventedResponseTimeAndCallerCancelPropagates()
    {
        using var sink = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)); var endpoint = (IPEndPoint)sink.Client.LocalEndPoint!;
        var request = new DnsTestRequest("test.example", DnsRecordType.A, endpoint, 250);
        var result = await new DnsTestClient().QueryAsync(request, CancellationToken.None); Assert.Equal("Timeout", result.Status); Assert.Null(result.ElapsedMs); Assert.Empty(result.Records);
        using var stop = new CancellationTokenSource(30); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DnsTestClient().QueryAsync(request, stop.Token));
    }
    [Fact] public async Task HttpReadsOnlyHeadersAndRedactsSensitiveResponseAndUrlData()
    {
        await using var server = new HttpServer();
        var result = await new HttpTestClient().TestAsync(new(new Uri(server.Url + "ok?token=secret"), "GET", 3000, false, 200), CancellationToken.None);
        Assert.Equal("PASS", result.Status); Assert.Equal(200, result.StatusCode); Assert.Equal("127.0.0.1", result.RemoteAddress); Assert.True(result.ElapsedMs >= 0);
        Assert.DoesNotContain("secret", result.Url + result.Details); Assert.Contains("Content-Length", result.Details);
    }
    [Fact] public async Task RedirectIsOptionalAndFollowingItReportsBothResponses()
    {
        await using var server = new HttpServer(); var client = new HttpTestClient(); var input = new HttpTestRequest(new Uri(server.Url + "redirect"), "HEAD", 3000, false, 200);
        var first = await client.TestAsync(input, CancellationToken.None); Assert.Equal(302, first.StatusCode); Assert.Equal("Redirect", first.Status); Assert.DoesNotContain("secret", first.Redirect);
        var followed = await client.TestAsync(input with { FollowRedirects = true }, CancellationToken.None); Assert.Equal("PASS", followed.Status); Assert.Contains("302", followed.Details); Assert.Contains("200", followed.Details);
        var loop = await client.TestAsync(input with { Url = new Uri(server.Url + "loop"), FollowRedirects = true }, CancellationToken.None); Assert.Equal("Error", loop.Status); Assert.Contains("loop", loop.Error);
    }
    [Fact] public async Task HttpExpectedStatusAndTimeoutAndCallerCancelAreDistinct()
    {
        await using var server = new HttpServer(); var client = new HttpTestClient(); var input = new HttpTestRequest(new Uri(server.Url + "missing"), "GET", 3000, false, 200);
        var missing = await client.TestAsync(input, CancellationToken.None); Assert.Equal("FAIL", missing.Status); Assert.Equal(404, missing.StatusCode); Assert.NotNull(missing.ElapsedMs);
        var timeout = await client.TestAsync(input with { Url = new Uri(server.Url + "slow"), TimeoutMs = 250 }, CancellationToken.None); Assert.Equal("Timeout", timeout.Status); Assert.Null(timeout.ElapsedMs);
        using var stop = new CancellationTokenSource(30); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.TestAsync(input with { Url = new Uri(server.Url + "slow") }, stop.Token));
    }
    [Fact] public async Task UntrustedTlsCertificateIsReportedAndNeverAccepted()
    {
        await using var server = new HttpServer(tls: true);
        var result = await new HttpTestClient().TestAsync(new(new Uri(server.Url + "ok"), "GET", 3000, false, 200), CancellationToken.None);
        Assert.Equal("Error", result.Status); Assert.Null(result.StatusCode); Assert.Contains("TLS certificate validation failed", result.Error); Assert.Contains("Certificate subject", result.Details); Assert.NotEmpty(result.CertificateExpiry);
    }
    [Theory] [InlineData("ftp://host/path")] [InlineData("https://user:pass@host/")] [InlineData("not a url")]
    public void HttpRejectsUnsupportedSchemeAndEmbeddedCredentials(string url) => Assert.Throws<ArgumentException>(() => DiagnosticInput.HttpUrl(url));

    internal static byte[] Answer(byte[] query, DnsRecordType type)
    {
        byte[] payload = type switch
        {
            DnsRecordType.A => [192, 0, 2, 7], DnsRecordType.AAAA => IPAddress.Parse("2001:db8::7").GetAddressBytes(),
            DnsRecordType.CNAME => Name("alias.example"), DnsRecordType.PTR => Name("host.example"),
            DnsRecordType.MX => new byte[] { 0, 10 }.Concat(Name("mail.example")).ToArray(),
            DnsRecordType.TXT => new byte[] { 5 }.Concat(Encoding.ASCII.GetBytes("hello")).Concat(new byte[] { 5 }).Concat(Encoding.ASCII.GetBytes("world")).ToArray(),
            DnsRecordType.SRV => new byte[] { 0, 10, 0, 20, 1, 187 }.Concat(Name("service.example")).ToArray(), _ => []
        };
        byte[] result = query.Concat(new byte[] { 0xc0, 0x0c, 0, (byte)type, 0, 1, 0, 0, 0, 60, (byte)(payload.Length >> 8), (byte)payload.Length }).Concat(payload).ToArray(); result[2] = 0x81; result[3] = 0x80; result[7] = 1; return result;
    }
    private static byte[] Name(string name) => name.Split('.').SelectMany(l => new byte[] { (byte)l.Length }.Concat(Encoding.ASCII.GetBytes(l))).Append((byte)0).ToArray();

    private sealed class HttpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly System.Collections.Concurrent.ConcurrentBag<Task> _clients = [];
        private readonly Task _accept;
        private readonly X509Certificate2? _certificate;
        public string Url { get; }
        public HttpServer(bool tls = false)
        {
            if (tls) { using var key = RSA.Create(2048); var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2)); _certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null); }
            _listener.Start(); Url = (tls ? "https" : "http") + "://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port + "/";
            _accept = Accept();
        }
        private async Task Accept()
        {
            try { while (!_stop.IsCancellationRequested) { var client = await _listener.AcceptTcpClientAsync(_stop.Token); _clients.Add(Serve(client)); } } catch (OperationCanceledException) { }
        }
        private async Task Serve(TcpClient client)
        {
            using (client)
            try
            {
                Stream stream = client.GetStream();
                if (_certificate is not null) { var ssl = new SslStream(stream, false); stream = ssl; await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = _certificate }, _stop.Token); }
                await using (stream)
                {
                    byte[] one = new byte[1]; var header = new StringBuilder();
                    while (header.Length < 4096 && !header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal)) { if (await stream.ReadAsync(one, _stop.Token) == 0) return; header.Append((char)one[0]); }
                    string path = header.ToString().Split(' ')[1]; if (path.StartsWith("/slow", StringComparison.Ordinal)) await Task.Delay(1000, _stop.Token);
                    string response = path.StartsWith("/redirect", StringComparison.Ordinal) ? "302 Found\r\nLocation: /ok?token=secret" : path.StartsWith("/loop", StringComparison.Ordinal) ? "302 Found\r\nLocation: /loop" : path.StartsWith("/missing", StringComparison.Ordinal) ? "404 Not Found" : "200 OK";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 " + response + "\r\nContent-Length: 100000000\r\nSet-Cookie: secret\r\nConnection: close\r\n\r\n"), _stop.Token);
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or System.Security.Authentication.AuthenticationException) { }
        }
        public async ValueTask DisposeAsync() { await _stop.CancelAsync(); await _accept; await Task.WhenAll(_clients); _listener.Stop(); _certificate?.Dispose(); _stop.Dispose(); }
    }
}
