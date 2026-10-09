using NetStucked.Core;
using Xunit;

namespace NetStucked.Tests;

public class StatisticsTests
{
    [Fact]
    public void LossUsesAttemptCountInsteadOfHostCount()
    {
        Assert.Equal(25, Statistics.LossPercent(4, 3));
        Assert.Equal(0, Statistics.LossPercent(0, 0));
    }
}
