using System.Net;
using System.Text;
using System.Text.Json;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;
namespace NetStucked.Tests;
public sealed class Utility080Tests
{
    [Fact] public void PastedCiscoTablesPreserveMappingsAndIgnoreHeaders()
    {
        var rows = MacTextParser.Parse("e430.22cf.4433    DYNAMIC     Gi1/0/33\nProtocol Address Age (min) Hardware Addr Type Interface\nInternet 10.100.144.23 14 5811.2245.f922 ARPA Vlan30\nInternet 10.100.144.12 - 8875.56b4.a241 ARPA Vlan30");
        Assert.Equal(3, rows.Count); Assert.Equal("E4:30:22:CF:44:33", rows[0].Mac.Address); Assert.Empty(rows[0].IpAddress);
        Assert.Equal("10.100.144.23", rows[1].IpAddress); Assert.Equal("58:11:22:45:F9:22", rows[1].Mac.Address); Assert.Equal(3, rows[1].SourceLine);
        Assert.Equal("10.100.144.12", rows[2].IpAddress);
    }
    [Theory] [InlineData("aa:bb:cc:dd:ee:ff")] [InlineData("AA-BB-CC-DD-EE-FF")] [InlineData("aabb.ccdd.eeff")] [InlineData("aabbccddeeff")]
    public void FindsSupportedFormatAmongWords(string mac) => Assert.Equal("AA:BB:CC:DD:EE:FF", MacTextParser.Parse("MAC=" + mac + " DYNAMIC Gi1/0/33").Single().Mac.Address);
    [Fact] public void RepeatedMacKeepsDifferentIpsAndIpv6MapsWithoutTreatingAgeAsIp()
    {
        var rows = MacTextParser.Parse("10.1.2.3 14 5811.2245.f922 Vlan30\n10.1.2.4 - 5811.2245.f922 Vlan31\n2001:db8::1 02-aa-bb-cc-dd-ee\n9 0011.2233.4455 Gi1/0/2");
        Assert.Equal(4, rows.Count); Assert.Equal(rows[0].Mac.Address, rows[1].Mac.Address); Assert.NotEqual(rows[0].IpAddress, rows[1].IpAddress); Assert.Equal("2001:db8::1", rows[2].IpAddress); Assert.Empty(rows[3].IpAddress);
    }
    [Fact] public void AmbiguousLinesAndInvalidIpDoNotFabricateMapping()
    {
        Assert.StartsWith("Ambiguous", MacTextParser.Parse("10.1.1.1 0011.2233.4455 5811.2245.f922")[0].IpAddress);
        Assert.Empty(MacTextParser.Parse("999.1.1.1 5811.2245.f922")[0].IpAddress);
        Assert.Throws<ArgumentException>(() => MacTextParser.Parse("Age 30 Interface Gi1/0/1"));
        Assert.Throws<ArgumentException>(() => MacTextParser.Parse("f001122334455e"));
    }
    [Fact] public void ActiveDnsIPv6IsOptionalIndependentOfQueryRecord()
    {
        var servers = new[] { IPAddress.Parse("2001:db8::53"), IPAddress.Parse("10.1.1.53"), IPAddress.Parse("10.1.1.53") };
        Assert.Equal(["10.1.1.53"], DnsServerSelection.Filter(servers, false)); Assert.Equal(2, DnsServerSelection.Filter(servers, true).Length);
        Assert.Empty(DnsServerSelection.Filter([servers[0]], false));
    }
    [Fact] public async Task VirusTotalUsesAccountHeaderAndDisplaysExistingReport()
    {
        string? address = null; string? key = null;
        using var http = new HttpClient(new Handler(request => { address = request.RequestUri!.AbsoluteUri; key = request.Headers.GetValues("x-apikey").Single(); return new(HttpStatusCode.OK) { Content = new StringContent("{\"data\":{\"attributes\":{\"last_analysis_stats\":{\"malicious\":2,\"suspicious\":1,\"harmless\":50,\"undetected\":3},\"last_analysis_date\":1700000000}}}") }; }));
        using var client = new VirusTotalUrlLookup(http); var result = await client.LookupAsync(new("https://example.com/"), new string('a', 64), CancellationToken.None);
        Assert.Equal("Malicious detections", result.Status); Assert.Equal(2, result.Malicious); Assert.NotNull(result.LastAnalysis); Assert.Equal(new string('a', 64), key);
        Assert.StartsWith("https://www.virustotal.com/api/v3/urls/", address); string id = address!.Split('/').Last(); Assert.DoesNotContain('=', id);
        Assert.Equal("https://example.com/", Encoding.UTF8.GetString(Convert.FromBase64String(id.Replace('-', '+').Replace('_', '/') + new string('=', (4-id.Length%4)%4))));
    }
    [Theory] [InlineData(404,"No report")] [InlineData(401,"Access denied")] [InlineData(403,"Access denied")] [InlineData(429,"Rate limited")] [InlineData(302,"API error")]
    public async Task ProviderErrorsAreNotSafeVerdicts(int code, string status)
    {
        using var client = new VirusTotalUrlLookup(new HttpClient(new Handler(_ => new((HttpStatusCode)code))));
        var result = await client.LookupAsync(new("https://example.com/"), new string('b',64), CancellationToken.None); Assert.Equal(status,result.Status); Assert.Null(result.Malicious);
    }
    [Theory] [InlineData("not-json")] [InlineData("{\"data\":{\"attributes\":{}}}")]
    public async Task BrokenReportDoesNotInventCounters(string body)
    {
        using var client = new VirusTotalUrlLookup(new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content=new StringContent(body) })));
        var result = await client.LookupAsync(new("https://example.com/"),new string('a',64),CancellationToken.None); Assert.Equal("API error",result.Status); Assert.Null(result.Harmless);
    }
    [Fact] public async Task OversizedReportAndCancelledQuotaWaitAreBounded()
    {
        int calls=0; using var client = new VirusTotalUrlLookup(new HttpClient(new Handler(_ => { calls++; return new(HttpStatusCode.OK) { Content=new StringContent(new string('x',262145)) }; })));
        Assert.Equal("API error",(await client.LookupAsync(new("https://example.com/"),new string('a',64),CancellationToken.None)).Status);
        using var cancel=new CancellationTokenSource(50); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>client.LookupAsync(new("https://example.com/"),new string('a',64),cancel.Token)); Assert.Equal(1,calls);
    }
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> answer) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(answer(request)); } }
}
