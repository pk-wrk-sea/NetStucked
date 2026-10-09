using NetStucked.Core;
using Xunit;

namespace NetStucked.Tests;

public class ParserTests
{
    [Fact]
    public void PreservesDescriptionsAndDeduplicatesFirstOccurrence()
    {
        var parsed = TargetInputParser.Parse("\n  EXAMPLE.org First description ไทย\nexample.org Later\n192.168.1.0/31 Pair\n192.168.1.1 Duplicate\n::1 IPv6\n");
        Assert.True(parsed.IsValid);
        Assert.Equal(4, parsed.Targets.Count);
        Assert.Equal("First description ไทย", parsed.Targets[0].Description);
        Assert.Equal("Pair", parsed.Targets[2].Description);
        Assert.Equal("192.168.1.0/31", parsed.Targets[2].SourceCidr);
    }
    [Theory]
    [InlineData("192.168.0.0/24", 254, "192.168.0.1", "192.168.0.254")]
    [InlineData("192.168.0.7/24", 254, "192.168.0.1", "192.168.0.254")]
    [InlineData("255.255.255.254/31", 2, "255.255.255.254", "255.255.255.255")]
    [InlineData("255.255.255.255/32", 1, "255.255.255.255", "255.255.255.255")]
    [InlineData("0.0.0.0/32", 1, "0.0.0.0", "0.0.0.0")]
    [InlineData("10.0.0.0/30", 2, "10.0.0.1", "10.0.0.2")]
    public void CidrHostBoundaries(string input, int count, string first, string last)
    {
        var hosts = Ipv4CidrExpander.Expand(input, 1024).ToArray();
        Assert.Equal(count, hosts.Length); Assert.Equal(first, hosts[0]); Assert.Equal(last, hosts[^1]);
    }
    [Theory]
    [InlineData("10.0.0.0/0")][InlineData("10.0.0.0/16")][InlineData("10.0.0.0/33")]
    [InlineData("::1/64")][InlineData("999.1.1.1")][InlineData("1.1.1")][InlineData("1234")]
    [InlineData("https://example.org")][InlineData("bad_host")][InlineData("-bad.org")][InlineData("a..org")]
    [InlineData("010.0.0.1")]
    public void InvalidInputIsReportedWithLineNumber(string input)
    {
        var result = TargetInputParser.Parse("127.0.0.1 Loopback\n" + input);
        Assert.False(result.IsValid); Assert.Equal(2, Assert.Single(result.Errors).Line);
    }
    [Fact]
    public void CapAppliesAfterDeduplicationAndRejectsAdditionalUniqueHosts()
    {
        Assert.True(TargetInputParser.Parse("10.0.0.0/31\n10.0.0.1", 2).IsValid);
        var tooMany = TargetInputParser.Parse("10.0.0.0/31\n10.0.0.2", 2);
        Assert.False(tooMany.IsValid); Assert.Contains("limit", Assert.Single(tooMany.Errors).Message);
        Assert.False(TargetInputParser.Parse("\n  \n").IsValid);
    }

    [Fact]
    public void SupportsWhitespaceAndLineEndingsWithoutJoiningMalformedTargets()
    {
        var parsed = TargetInputParser.Parse("127.0.0.1\u2003Description ไทย\r::1\tIPv6\r\nexample.org. Absolute DNS");
        Assert.True(parsed.IsValid); Assert.Equal(3, parsed.Targets.Count); Assert.Equal("Description ไทย", parsed.Targets[0].Description);
        Assert.False(TargetInputParser.Parse("127.0.\r0.1").IsValid);
        Assert.False(TargetInputParser.Parse("example.org..").IsValid);
        Assert.False(TargetInputParser.Parse("10.0.0.0/+24").IsValid);
        Assert.False(TargetInputParser.Parse("127.0.0.1 " + new string('x', 513)).IsValid);
    }
}
