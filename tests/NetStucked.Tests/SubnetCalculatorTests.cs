using NetStucked.Core;
using Xunit;

namespace NetStucked.Tests;

public class SubnetCalculatorTests
{
    [Theory]
    [InlineData("10.10.10.10/23")]
    [InlineData("10.10.10.10 /23")]
    [InlineData("10.10.10.10 / 23")]
    [InlineData("10.10.10.10/23.")]
    [InlineData("10.10.10.10 255.255.254.0")]
    [InlineData("10.10.10.10. 255.255.254.0")]
    [InlineData(" 10.10.10.10\t255.255.254.0 ")]
    public void EquivalentAcceptedFormatsProduceTheSameNetwork(string input)
    {
        var evaluation = SubnetCalculator.Evaluate(input);
        Assert.Equal(SubnetInputState.Valid, evaluation.State);
        var subnet = Assert.IsType<Ipv4Subnet>(evaluation.Subnet);
        Assert.Equal("10.10.10.0/23", subnet.NetworkCidr); Assert.Equal("255.255.254.0", subnet.Mask);
        Assert.Equal("0.0.1.255", subnet.Wildcard); Assert.Equal("10.10.11.255", subnet.Broadcast);
        Assert.Equal("10.10.10.1", subnet.FirstUsable); Assert.Equal("10.10.11.254", subnet.LastUsable);
        Assert.Equal(512ul, subnet.TotalAddresses); Assert.Equal(510ul, subnet.UsableAddresses);
    }
    [Theory]
    [InlineData("0.0.0.0/0", "0.0.0.0", "255.255.255.255", 4294967296ul, 4294967294ul)]
    [InlineData("255.255.255.255/32", "255.255.255.255", null, 1ul, 1ul)]
    [InlineData("255.255.255.255/31", "255.255.255.254", null, 2ul, 2ul)]
    [InlineData("10.10.10.10. 255.255.255.0", "10.10.10.0", "10.10.10.255", 256ul, 254ul)]
    public void HandlesBoundaryRangesWithoutOverflow(string input, string network, string? broadcast, ulong total, ulong usable)
    {
        var subnet = Assert.IsType<Ipv4Subnet>(SubnetCalculator.Evaluate(input).Subnet);
        Assert.Equal(network, subnet.Network); Assert.Equal(broadcast, subnet.Broadcast);
        Assert.Equal(total, subnet.TotalAddresses); Assert.Equal(usable, subnet.UsableAddresses);
    }
    [Theory]
    [InlineData("")][InlineData("10")][InlineData("10.10.")][InlineData("10.10.10.10")]
    [InlineData("10.10.10.10 /")][InlineData("10.10.10.10 255.255.")]
    [InlineData("10.10 /23")]
    public void PartialTypingIsIncompleteAndHasNoStaleResult(string input)
    {
        var evaluation = SubnetCalculator.Evaluate(input);
        Assert.Equal(SubnetInputState.Incomplete, evaluation.State); Assert.Null(evaluation.Subnet);
        Assert.StartsWith("Input incomplete:", evaluation.Message);
    }
    [Theory]
    [InlineData("999.10.10.10/23")][InlineData("10.10.10.10/33")][InlineData("10.10.10.10/-1")]
    [InlineData("10.10.10.10/23/24")][InlineData("10.10.10.10 255.0.255.0")]
    [InlineData("010.10.10.10/23")][InlineData("10.10.10.10 255.255.255.256")]
    [InlineData("10.10.10.10 /2 3")][InlineData("2001:db8::1/64")]
    public void DefinitivelyInvalidSyntaxIsAnError(string input)
    {
        var evaluation = SubnetCalculator.Evaluate(input);
        Assert.Equal(SubnetInputState.Invalid, evaluation.State); Assert.Null(evaluation.Subnet);
        Assert.StartsWith("Syntax error:", evaluation.Message);
    }
    [Fact] public void EveryPrefixHasAConsistentMaskAndAddressCount()
    {
        for (int prefix = 0; prefix <= 32; prefix++)
        {
            var subnet = Assert.IsType<Ipv4Subnet>(SubnetCalculator.Evaluate("203.0.113.7/" + prefix).Subnet);
            Assert.Equal(subnet, SubnetCalculator.Evaluate("203.0.113.7 " + subnet.Mask).Subnet);
            Assert.Equal(1ul << (32-prefix), subnet.TotalAddresses);
        }
    }
}
