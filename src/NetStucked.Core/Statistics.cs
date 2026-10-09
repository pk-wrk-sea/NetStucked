namespace NetStucked.Core;

public static class Statistics
{
    public static double LossPercent(long sent, long received) => sent == 0 ? 0 : 100d * (sent - received) / sent;
}
