using System.Net;
using System.Net.NetworkInformation;
using NetStucked.Core;
using Xunit;

namespace NetStucked.Tests;

public class AccumulatorTests
{
    [Fact]
    public void StatisticsUseOnlyValidRepliesAndJitterSkipsMissingResponses()
    {
        var stats = new SampleStatistics();
        Assert.Null(stats.Average); Assert.Null(stats.Jitter); Assert.Equal(0, stats.LossPercent);
        stats.Add(10); stats.Add(null); stats.Add(30); stats.Add(20);
        Assert.Equal(4, stats.Sent); Assert.Equal(3, stats.Received); Assert.Equal(1, stats.Lost);
        Assert.Equal(25, stats.LossPercent); Assert.Equal(10, stats.Minimum); Assert.Equal(30, stats.Maximum);
        Assert.Equal(20, stats.Average); Assert.Equal(15, stats.Jitter);
        stats.Add(null); Assert.Null(stats.Last); Assert.Equal(20, stats.Average);
    }
    [Fact]
    public void BoundedHistoryIsNewestFirstAndStatusUsesFailureThreshold()
    {
        var target = new TargetAccumulator(new("127.0.0.1", "Local"), 1, 100);
        var settings = new ProbeSettings();
        Assert.Equal("Unknown", target.Snapshot(settings).Status);
        for (int i = 0; i < 600; i++) target.Add(new(IPStatus.Success, IPAddress.Loopback, 5));
        Assert.Equal(100, target.History(500).Count);
        Assert.Equal(600, target.History(100)[0].Sequence);
        Assert.Equal("Up", target.Snapshot(settings).Status);
        target.Add(new(IPStatus.TimedOut, null, null)); Assert.Equal("Warn", target.Snapshot(settings).Status);
        target.Add(new(IPStatus.TimedOut, null, null)); target.Add(new(IPStatus.TimedOut, null, null));
        var snapshot = target.Snapshot(settings);
        Assert.Equal("Unreachable", snapshot.Status); Assert.Null(snapshot.Last); Assert.Null(snapshot.Ttl);
        Assert.Equal(snapshot.Sent, snapshot.Received + snapshot.Lost);
    }
    [Fact]
    public void CsvEscapesUnicodeQuotesCommasNewlinesAndFormulas()
    {
        string csv = CsvExporter.Rows(["Description"], [new object?[] { "ไทย, \"quoted\"\nsecond line" }, new object?[] { "=HYPERLINK(\"bad\")" }, new object?[] { null }]);
        Assert.Contains("\"ไทย, \"\"quoted\"\"\nsecond line\"", csv);
        Assert.Contains("\"'=HYPERLINK(\"\"bad\"\")\"", csv);
        Assert.Equal("\"1.25\"", CsvExporter.Cell(1.25));
    }
}
