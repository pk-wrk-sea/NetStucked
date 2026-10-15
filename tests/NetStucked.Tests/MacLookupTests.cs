using System.Net;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public class MacLookupTests
{
    [Theory] [InlineData("00:11:22:33:44:55")][InlineData("00-11-22-33-44-55")][InlineData("0011.2233.4455")][InlineData("001122334455")]
    public void NormalizesCommonFormats(string input)
    {
        var result = MacAddressParser.Parse(input); Assert.Equal("00:11:22:33:44:55", result.Address);
        Assert.Equal("00:11:22", result.Oui); Assert.True(result.CanIdentifyVendor);
    }
    [Theory] [InlineData("00:11:22:33:44")][InlineData("GG:11:22:33:44:55")][InlineData("00:11-22:33:44:55")]
    public void InvalidMacInputNeverBecomesARequest(string input) => Assert.Throws<ArgumentException>(() => MacAddressParser.Parse(input));
    [Theory] [InlineData("02:11:22:33:44:55")][InlineData("01:00:5E:00:00:01")][InlineData("FF:FF:FF:FF:FF:FF")][InlineData("00:00:00:00:00:00")]
    public async Task NonVendorAddressesDoNotCallTheProvider(string input)
    {
        var handler = new VendorHandler(HttpStatusCode.OK, "Example vendor"); using var client = new MacVendorLookup(new HttpClient(handler));
        var result = await client.LookupAsync(MacAddressParser.Parse(input), 1000, CancellationToken.None);
        Assert.Null(result.Vendor); Assert.Equal(0, handler.Calls); Assert.Equal("Not identifiable", result.Status);
    }
    [Fact] public async Task UsesFixedHttpsEndpointAndCachesTheOriginalResultTime()
    {
        var handler = new VendorHandler(HttpStatusCode.OK, "Example vendor"); using var client = new MacVendorLookup(new HttpClient(handler)); var address = MacAddressParser.Parse("00:11:22:33:44:55");
        var first = await client.LookupAsync(address, 1000, CancellationToken.None); var second = await client.LookupAsync(address, 1000, CancellationToken.None);
        Assert.Equal("Found", first.Status); Assert.Equal("Example vendor", first.Vendor); Assert.Equal(first.Time, second.Time); Assert.Equal(1, handler.Calls);
        Assert.StartsWith("https://api.macvendors.com/", handler.Url); Assert.Contains("cached", second.Details);
    }
    [Theory] [InlineData(404, "Not found")][InlineData(429, "Rate limited")][InlineData(403, "API error")]
    public async Task ProviderFailuresAreNotVendorResults(int code, string expected)
    {
        using var client = new MacVendorLookup(new HttpClient(new VendorHandler((HttpStatusCode)code, "No result")));
        var result = await client.LookupAsync(MacAddressParser.Parse("00:11:22:33:44:55"), 1000, CancellationToken.None);
        Assert.Equal(expected, result.Status); Assert.Null(result.Vendor);
    }
    [Theory] [InlineData("<html>Error</html>")][InlineData("")][InlineData("{\"error\":true}")]
    public async Task UnexpectedProviderBodiesAreRejected(string body)
    {
        using var client = new MacVendorLookup(new HttpClient(new VendorHandler(HttpStatusCode.OK, body)));
        Assert.Equal("API error", (await client.LookupAsync(MacAddressParser.Parse("00:11:22:33:44:55"), 1000, CancellationToken.None)).Status);
    }
    [Fact] public async Task RateLimitStopsQueuedRequestsDuringProviderCooldown()
    {
        var handler = new VendorHandler((HttpStatusCode)429, "Rate limit"); using var client = new MacVendorLookup(new HttpClient(handler));
        await client.LookupAsync(MacAddressParser.Parse("00:11:22:33:44:55"), 1000, CancellationToken.None);
        var next = await client.LookupAsync(MacAddressParser.Parse("00:11:22:33:44:66"), 1000, CancellationToken.None);
        Assert.Equal(1, handler.Calls); Assert.Equal("Rate limited", next.Status); Assert.Null(next.Vendor); Assert.Contains("no API request", next.Details);
    }
    [Fact] public async Task CallerCancellationDoesNotIssueARequest()
    {
        var handler = new VendorHandler(HttpStatusCode.OK, "Vendor"); using var client = new MacVendorLookup(new HttpClient(handler)); using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.LookupAsync(MacAddressParser.Parse("00:11:22:33:44:55"), 1000, stop.Token)); Assert.Equal(0, handler.Calls);
    }
    private sealed class VendorHandler(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public int Calls; public string Url = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Calls++; Url = request.RequestUri!.AbsoluteUri; return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) }); }
    }
}
