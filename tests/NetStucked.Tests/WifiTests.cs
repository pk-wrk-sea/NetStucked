using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Xml;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public sealed class WifiTests
{
    private static WifiProfileInput Personal(string key = "secret&<>password", string security = "WPA2-Personal") => new("Customer & lab", "ลูกค้า", security, false, "Nonsecret description", key);
    [Fact] public void PersonalProfileRoundTripsUtf8SsidAndEscapesSecret()
    {
        string xml = WifiProfileXml.Personal(Personal());
        var profile = WifiProfileXml.Describe(xml, 2, 4);
        Assert.Equal("ลูกค้า", profile.Ssid); Assert.Equal(Convert.ToHexString(Encoding.UTF8.GetBytes("ลูกค้า")), profile.SsidHex);
        Assert.Equal("WPA2-Personal", profile.Security); Assert.True(profile.PerUser); Assert.False(profile.AutoConnect); Assert.False(profile.PolicyManaged);
        Assert.DoesNotContain("secret&<>password", xml); Assert.Equal("secret&<>password", WifiProfileXml.Parse(xml).Descendants().Single(e => e.Name.LocalName == "keyMaterial").Value);
        Assert.DoesNotContain("password", profile.ToString()); Assert.DoesNotContain("password", Personal().ToString());
    }
    [Fact] public void ExportRemovesKeyEntirelyAndKeepsNonsecretConfiguration()
    {
        string safe = WifiProfileXml.ExportSafe(WifiProfileXml.Personal(Personal()));
        Assert.DoesNotContain("sharedKey", safe); Assert.DoesNotContain("password", safe); Assert.DoesNotContain("keyMaterial", safe);
        Assert.Equal("ลูกค้า", WifiProfileXml.Describe(safe, 0, 1).Ssid);
        Assert.False(WifiProfileXml.Describe(safe, 0, 1).PerUser);
    }
    [Theory] [InlineData("short", "WPA2-Personal")] [InlineData("invalid\npassword", "WPA2-Personal")] [InlineData("validpassword", "WPA2-Enterprise")]
    public void BadPersonalInputRejected(string key, string security) => Assert.Throws<ArgumentException>(() => WifiProfileXml.Personal(Personal(key, security)));
    [Fact] public void RawPskIsOnlyAcceptedForWpa2()
    {
        string key = new('A', 64); Assert.Contains("networkKey", WifiProfileXml.Personal(Personal(key)));
        Assert.Throws<ArgumentException>(() => WifiProfileXml.Personal(Personal(key, "WPA3-Personal")));
    }
    [Fact] public void AutoConnectEditPreservesEnterpriseTrustConfiguration()
    {
        string xml = Enterprise(); string edited = WifiProfileXml.SetAutoConnect(xml, true);
        Assert.Contains("ServerNames", edited); Assert.Contains("trusted.company.test", edited); Assert.Contains("PerformServerValidation", edited);
        var profile = WifiProfileXml.Describe(edited, 1, 1); Assert.True(profile.PolicyManaged); Assert.True(profile.AutoConnect); Assert.Equal("PEAP / MSCHAPv2", profile.Eap);
        Assert.Contains("trusted.company.test", WifiProfileXml.ExportSafe(edited));
    }
    [Fact] public void XmlRejectsEntitiesAndOversizedProfiles()
    {
        Assert.Throws<XmlException>(() => WifiProfileXml.Parse("<!DOCTYPE foo [<!ENTITY secret SYSTEM 'file:///C:/secret'>]><foo>&secret;</foo>"));
        Assert.Throws<ArgumentException>(() => WifiProfileXml.Parse(new('x', 1_000_001)));
        Assert.Throws<ArgumentException>(() => WifiProfileXml.Parse("<notWlan/>"));
    }
    [Fact] public void PeapCredentialsAreEscapedAndNeverChangeTrustPolicy()
    {
        var input = new EnterpriseCredentials("user<&", "password<&", "DOMAIN", true);
        string xml = WifiProfileXml.PeapCredentials(input);
        Assert.Contains("25", xml); Assert.Contains("26", xml); Assert.Contains("password&lt;&amp;", xml);
        Assert.DoesNotContain("ServerValidation", xml); Assert.DoesNotContain("password", input.ToString());
        Assert.NotEqual(WindowsWifiCredentialVault.Target(Guid.Empty, "Case"), WindowsWifiCredentialVault.Target(Guid.Empty, "case"));
    }
    [Fact] public void AccidentalJsonSerializationCannotEmitCredentialOrProfileKeys()
    {
        string profile = System.Text.Json.JsonSerializer.Serialize(WifiProfileXml.Describe(WifiProfileXml.Personal(Personal()), 2, 1));
        string credentials = System.Text.Json.JsonSerializer.Serialize(new EnterpriseCredentials("User", "private-test-secret", "Domain", true));
        string input = System.Text.Json.JsonSerializer.Serialize(Personal());
        Assert.DoesNotContain("Xml", profile); Assert.DoesNotContain("Password", credentials); Assert.DoesNotContain("Username", credentials); Assert.DoesNotContain("private-test-secret", credentials); Assert.DoesNotContain("Password", input);
    }
    [Fact] public void ReimportWithoutKeyPreservesHiddenSsidAndWindowsConfiguration()
    {
        var document = WifiProfileXml.Parse(WifiProfileXml.ExportSafe(WifiProfileXml.Personal(Personal())));
        var wlan = document.Root!.Name.Namespace;
        document.Root.Element(wlan + "SSIDConfig")!.Add(new System.Xml.Linq.XElement(wlan + "nonBroadcast", "true"));
        document.Root.Add(new System.Xml.Linq.XElement(wlan + "autoSwitch", "false"));
        string original = document.ToString();
        string imported = WifiProfileXml.ReplacePersonalKey(original, "reimport-test-password");
        var restored = WifiProfileXml.Parse(imported);
        Assert.Equal("true", restored.Descendants(wlan + "nonBroadcast").Single().Value);
        Assert.Equal("false", restored.Root!.Element(wlan + "autoSwitch")!.Value);
        Assert.Equal(WifiProfileXml.Describe(original, 2, 1).SsidHex, WifiProfileXml.Describe(imported, 2, 1).SsidHex);
        Assert.Equal("reimport-test-password", restored.Descendants(wlan + "keyMaterial").Single().Value);
    }
    [Fact] public void ChangingPersonalKeyPreservesSsidAndOtherPolicy()
    {
        string original = WifiProfileXml.Personal(Personal());
        string edited = WifiProfileXml.ReplacePersonalKey(original, "new-test-password");
        var before = WifiProfileXml.Describe(original, 2, 1); var after = WifiProfileXml.Describe(edited, 2, 1);
        Assert.Equal(before.SsidHex, after.SsidHex); Assert.Equal(before.AutoConnect, after.AutoConnect); Assert.Equal(before.Security, after.Security);
        Assert.Equal("new-test-password", WifiProfileXml.Parse(edited).Descendants().Single(e => e.Name.LocalName == "keyMaterial").Value);
    }
    [Fact] public void RealWindowsCredentialManagerRoundTripAndRemovalOfOwnedQaEntry()
    {
        Guid id = Guid.NewGuid(); const string profile = "NetStucked owned QA credential"; var vault = new WindowsWifiCredentialVault();
        try
        {
            Assert.Null(vault.Read(id, profile)); vault.Write(id, profile, new("QA test user", "QA temporary password", "QA domain", true));
            var stored = vault.Read(id, profile); Assert.NotNull(stored); Assert.Equal("QA temporary password", stored.Password); Assert.Equal("QA domain", stored.Domain); Assert.Equal("QA test user", stored.Username);
            vault.Delete(id, profile); Assert.Null(vault.Read(id, profile));
        }
        finally { vault.Delete(id, profile); }
    }
    [Fact] public async Task DnsTruncatedUdpFallsBackToRealBoundedTcp()
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)); int port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        using var tcp = new TcpListener(IPAddress.Loopback, port); tcp.Start(); using var deadline = new CancellationTokenSource(5000);
        var server = Task.Run(async () =>
        {
            var query = await udp.ReceiveAsync(deadline.Token); var truncated = DnsReply(query.Buffer); truncated[2] |= 2; await udp.SendAsync(truncated, query.RemoteEndPoint, deadline.Token);
            using var client = await tcp.AcceptTcpClientAsync(deadline.Token); var stream = client.GetStream(); byte[] prefix = new byte[2]; await stream.ReadExactlyAsync(prefix, deadline.Token);
            byte[] request = new byte[BinaryPrimitives.ReadUInt16BigEndian(prefix)]; await stream.ReadExactlyAsync(request, deadline.Token);
            var reply = DnsReply(request); BinaryPrimitives.WriteUInt16BigEndian(prefix, (ushort)reply.Length); await stream.WriteAsync(prefix, deadline.Token); await stream.WriteAsync(reply, deadline.Token);
        });
        var result = await WifiDnsQuery.QueryAsync("company.test", new IPEndPoint(IPAddress.Loopback, port), (f, s, p) => new Socket(f, s, p), 1, deadline.Token);
        Assert.Equal("192.0.2.1", Assert.Single(result).ToString()); await server;
    }
    [Theory] [InlineData("file:///C:/secret")] [InlineData("https://user:secret@company.test")]
    public void TestProfileRejectsNonHttpAndCredentialsInUrl(string url) => Assert.Throws<ArgumentException>(() => (new ServiceTestProfile { HttpUrl = url }).Validate());
    [Fact] public void DnsResponseMatchesTransactionAndQuestion()
    {
        byte[] query = WifiDnsQuery.Request("company.test", 1234); byte[] response = DnsReply(query);
        Assert.Equal(IPAddress.Parse("192.0.2.1"), Assert.Single(WifiDnsQuery.Parse(response, query)));
        response[0] ^= 1; Assert.Throws<IOException>(() => WifiDnsQuery.Parse(response, query));
        response = DnsReply(query); response[13] ^= 1; Assert.Throws<IOException>(() => WifiDnsQuery.Parse(response, query));
    }
    [Fact] public void DnsMalformedCompressionAndTruncationRejected()
    {
        byte[] query = WifiDnsQuery.Request("company.test", 1234); byte[] response = DnsReply(query);
        response[12] = 0xC0; response[13] = 12; Assert.Throws<IOException>(() => WifiDnsQuery.Parse(response, query));
        response = DnsReply(query); response[2] |= 2; Assert.Throws<IOException>(() => WifiDnsQuery.Parse(response, query));
        response = DnsReply(query); Assert.Throws<IOException>(() => WifiDnsQuery.Parse(response[..^2], query));
        response = DnsReply(query); response[3] |= 3; Assert.Throws<IOException>(() => WifiDnsQuery.Parse(response, query));
    }
    [Fact] public async Task RealLoopbackDnsUsesConfiguredServerAndRejectsUnrelatedAnswers()
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)); using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var server = Task.Run(async () => { var query = await udp.ReceiveAsync(stop.Token); await udp.SendAsync(DnsReply(query.Buffer), query.RemoteEndPoint, stop.Token); });
        var result = await WifiDnsQuery.QueryAsync("company.test", (IPEndPoint)udp.Client.LocalEndPoint!, (f, s, p) => new Socket(f, s, p), 1, stop.Token);
        Assert.Equal("192.0.2.1", Assert.Single(result).ToString()); await server;
    }
    [Fact] public async Task RealSourceBoundLoopbackTcpAndHttpProduceMeasuredResults()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var server = Task.Run(async () =>
        {
            var handlers = new List<Task>();
            for (int i = 0; i < 2; i++) { var client = await listener.AcceptTcpClientAsync(stop.Token); handlers.Add(Task.Run(async () => { using (client) { var stream = client.GetStream(); byte[] buffer = new byte[4096]; int read = await stream.ReadAsync(buffer, stop.Token); if (read > 0) await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), stop.Token); } })); }
            await Task.WhenAll(handlers);
        });
        var results = await new WifiServiceTests().RunAsync(LoopbackContext(), new() { TestGateway = false, TcpHost = "127.0.0.1", TcpPort = port, HttpUrl = $"http://127.0.0.1:{port}", TimeoutMs = 3000 }, stop.Token);
        Assert.Equal(2, results.Count); Assert.All(results, r => { Assert.Equal("PASS", r.Status); Assert.NotNull(r.LatencyMs); Assert.True(r.LatencyMs >= 0); }); await server;
    }
    [Fact] public async Task RealLoopbackHttpRedirectIsFailureAndIsNotFollowed()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () => { using var client = await listener.AcceptTcpClientAsync(); var stream = client.GetStream(); byte[] buffer = new byte[4096]; int bytes = await stream.ReadAsync(buffer); Assert.True(bytes > 0); await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:1/\r\nContent-Length: 0\r\n\r\n")); });
        var results = await new WifiServiceTests().RunAsync(LoopbackContext(), new() { TestGateway = false, HttpUrl = $"http://127.0.0.1:{port}", TimeoutMs = 1000 }, CancellationToken.None);
        var result = Assert.Single(results); Assert.Equal("FAIL", result.Status); Assert.NotNull(result.LatencyMs); Assert.Contains("302", result.Message); await server;
    }
    [Fact] public async Task CancelledServiceRunIsAwaitedAndNeverRecordedAsFailed()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var stop = new CancellationTokenSource(); var run = new WifiServiceTests().RunAsync(LoopbackContext(), new() { TestGateway = false, HttpUrl = $"http://127.0.0.1:{port}", TimeoutMs = 10000 }, stop.Token);
        using var client = await listener.AcceptTcpClientAsync(); stop.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await run);
        byte[] buffer = new byte[4096]; using var timeout = new CancellationTokenSource(3000); while (await client.GetStream().ReadAsync(buffer, timeout.Token) > 0) { }
    }
    [Fact] public async Task HttpsRejectsAnUntrustedOwnedLoopbackCertificate()
    {
        using var key = RSA.Create(2048); var request = new CertificateRequest("CN=NetStucked-owned-TLS-QA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder(); names.AddIpAddress(IPAddress.Loopback); request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var stop = new CancellationTokenSource(8000);
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(stop.Token); using var tls = new SslStream(client.GetStream());
            try { await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, stop.Token); }
            catch (Exception ex) when (ex is IOException or System.Security.Authentication.AuthenticationException) { }
        });
        var result = Assert.Single(await new WifiServiceTests().RunAsync(LoopbackContext(), new() { TestGateway = false, HttpUrl = $"https://127.0.0.1:{port}", TimeoutMs = 3000 }, stop.Token));
        Assert.Equal("FAIL", result.Status); Assert.Null(result.LatencyMs); await server;
    }
    [Fact] public async Task GatewayProbeUsesRealSourceBoundIcmp()
    {
        var context = LoopbackContext(); context = context with { Ip = context.Ip with { Gateways = [IPAddress.Loopback] } };
        var result = Assert.Single(await new WifiServiceTests().RunAsync(context, new() { TimeoutMs = 1500 }, CancellationToken.None));
        Assert.Equal("PASS", result.Status); Assert.NotNull(result.LatencyMs); Assert.Contains("source 127.0.0.1", result.Message);
    }
    [Fact] public async Task GatewayProbeSupportsRealSourceBoundIpv6Loopback()
    {
        var context = LoopbackContext(); context = context with { Ip = context.Ip with { Gateways = [IPAddress.IPv6Loopback] } };
        var result = Assert.Single(await new WifiServiceTests().RunAsync(context, new() { TimeoutMs = 1500 }, CancellationToken.None));
        Assert.Equal("PASS", result.Status); Assert.NotNull(result.LatencyMs); Assert.Contains("source ::1", result.Message);
    }
    [Fact] public async Task AdditiveWifiPreferencesRoundTripWithoutSecretsAndRemainSchemaOne()
    {
        string dir = Path.Combine(Path.GetTempPath(), "NetStucked-Wifi-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new UserSettingsStore(dir); store.Preferences.WifiProfiles["test"] = new("Lab"); store.Preferences.WifiTestProfiles.Add(new() { Name = "Company", TcpHost = "127.0.0.1" });
            await store.SaveAsync(); var loaded = new UserSettingsStore(dir); Assert.Null(loaded.LoadError); Assert.Single(loaded.Preferences.WifiTestProfiles); Assert.Equal(1, loaded.Preferences.SchemaVersion);
            string json = await File.ReadAllTextAsync(Path.Combine(dir, "settings.json")); Assert.DoesNotContain("Password", json); Assert.DoesNotContain("Xml", json); Assert.DoesNotContain("Username", json);
        }
        finally { Directory.Delete(dir, true); }
    }
    [Fact] public async Task RecoveryBackupRetainsWifiHistoryAndHopTcpSettingsTogether()
    {
        string dir = Path.Combine(Path.GetTempPath(), "NetStucked-Wifi-Recovery-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new UserSettingsStore(dir);
            store.Preferences.Trace = new() { CheckHopTcp = true, HopTcpPorts = "22,443,8088" };
            store.Preferences.WifiProfiles["lab"] = new("Lab ไทย");
            store.Preferences.WifiTestProfiles.Add(new() { Name = "Lab services", TcpHost = "127.0.0.1" });
            var observed = new WifiEvent(DateTimeOffset.Now, "Owned fixture", "Lab", "Connect", "Success", "Observed fixture state");
            store.Preferences.WifiHistory.Add(observed);
            await store.SaveAsync();
            byte[] original = await File.ReadAllBytesAsync(Path.Combine(dir, "settings.json"));
            var backups = new UpdateBackups(dir);
            Assert.NotNull(await backups.CreateAsync("0.5.0", CancellationToken.None));
            await new UserSettingsStore(dir).SaveAsync();
            await File.WriteAllTextAsync(Path.Combine(dir, "settings.json"), "{\"SchemaVersion\":1}");
            Assert.False(await backups.RestoreForVersionAsync("0.4.2", CancellationToken.None));
            Assert.True(await backups.RestoreForVersionAsync("0.5.0", CancellationToken.None));
            Assert.Equal(original, await File.ReadAllBytesAsync(Path.Combine(dir, "settings.json")));
            var restored = new UserSettingsStore(dir);
            Assert.Null(restored.LoadError); Assert.True(restored.Preferences.Trace.CheckHopTcp);
            Assert.Equal("22,443,8088", restored.Preferences.Trace.HopTcpPorts);
            Assert.Equal("Lab ไทย", restored.Preferences.WifiProfiles["lab"].Description);
            Assert.Equal(observed, Assert.Single(restored.Preferences.WifiHistory));
            Assert.Single(restored.Preferences.WifiTestProfiles);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
    [Fact] public void DhcpApipaAndLinkLocalAreNotServiceReady()
    {
        Assert.False((AdapterIpConfiguration.Empty with { Addresses = [IPAddress.Parse("169.254.1.2"), IPAddress.Parse("fe80::1")] }).HasUsableAddress);
        Assert.True((AdapterIpConfiguration.Empty with { Addresses = [IPAddress.Parse("2001:db8::1")] }).HasUsableAddress);
    }
    public static WifiSnapshot LoopbackContext()
    {
        var loopback = NetworkInterface.GetAllNetworkInterfaces().First(n => n.GetIPProperties().UnicastAddresses.Any(a => a.Address.Equals(IPAddress.Loopback)));
        var properties = loopback.GetIPProperties(); var ip = new AdapterIpConfiguration(properties.GetIPv4Properties().Index, loopback.Supports(NetworkInterfaceComponent.IPv6) ? properties.GetIPv6Properties().Index : 0, [IPAddress.Loopback, IPAddress.IPv6Loopback], [], []);
        return new(DateTimeOffset.Now, new(Guid.NewGuid(), "Owned loopback test fixture", "Connected"), [], [], "Test fixture", "Test fixture", null, ip);
    }
    private static byte[] DnsReply(byte[] request)
    {
        byte[] response = new byte[request.Length + 16]; request.CopyTo(response, 0); response[2] = 0x81; response[3] = 0x80; response[7] = 1;
        new byte[] { 0xC0, 0x0C, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 192, 0, 2, 1 }.CopyTo(response, request.Length); return response;
    }
    private static string Enterprise() => "<WLANProfile xmlns='http://www.microsoft.com/networking/WLAN/profile/v1'><name>Company</name><SSIDConfig><SSID><name>Company</name></SSID></SSIDConfig><connectionMode>manual</connectionMode><MSM><security><authEncryption><authentication>WPA2</authentication><useOneX>true</useOneX></authEncryption><OneX xmlns='http://www.microsoft.com/networking/OneX/v1'><EAPConfig><Type>25</Type><PerformServerValidation>true</PerformServerValidation><ServerNames>trusted.company.test</ServerNames><Inner><Type>26</Type></Inner></EAPConfig></OneX></security></MSM></WLANProfile>";
}
