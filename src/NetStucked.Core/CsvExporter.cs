using System.Globalization;
using System.Text;

namespace NetStucked.Core;

public static class CsvExporter
{
    public static string Cell(object? value)
    {
        string text = value switch { null => "", double number => number.ToString("0.###", CultureInfo.InvariantCulture), DateTimeOffset time => time.ToString("O"), _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" };
        // Treat untrusted descriptions/hostnames as text in spreadsheet software.
        if (text.TrimStart() is { Length: > 0 } trimmed && "=+-@".Contains(trimmed[0])) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
    public static string Rows(IEnumerable<string> headers, IEnumerable<object?[]> rows)
    {
        var output = new StringBuilder();
        output.AppendLine(string.Join(',', headers.Select(Cell)));
        foreach (var row in rows) output.AppendLine(string.Join(',', row.Select(Cell)));
        return output.ToString();
    }
    public static string Ping(IEnumerable<TargetSnapshot> rows) => Rows(["#", "Status", "Host", "Description", "Resolved IP", "Last (ms)", "Avg (ms)", "Min (ms)", "Max (ms)", "Sent", "Recv", "Lost", "Loss %", "Last ping", "Last success", "Reachable since", "Unreachable since", "Result / error", "TTL"],
        rows.Select(r => new object?[] { r.Number, r.Status, r.Host, r.Description, r.ResolvedIp, r.Last, r.Average, r.Minimum, r.Maximum, r.Sent, r.Received, r.Lost, r.LossPercent, r.LastPing, r.LastSuccess, r.ReachableSince, r.UnreachableSince, r.ResultError, r.Ttl }));
    public static string Port(IEnumerable<PortSnapshot> rows) => Rows(["#", "Status", "Host", "Port", "Description", "Resolved IP", "Connect (ms)", "Avg (ms)", "Min (ms)", "Max (ms)", "Attempts", "Connected", "Failure %", "Last test", "Last success", "Result / error"],
        rows.Select(r => new object?[] { r.Number, r.Status, r.Host, r.Port, r.Description, r.ResolvedIp, r.Last, r.Average, r.Minimum, r.Maximum, r.Attempts, r.Connected, r.FailurePercent, r.LastTest, r.LastSuccess, r.Details }));
    public static string Trace(IEnumerable<TraceHopSnapshot> rows) => Rows(["Hop", "Address", "Hostname", "Description", "Status", "Last (ms)", "Best (ms)", "Avg (ms)", "Worst (ms)", "Jitter (ms)", "Sent", "Recv", "Loss %", "Route Changes", "Updated"],
        rows.Select(r => new object?[] { r.Hop, r.Address, r.Hostname, r.Description, r.Status, r.Last, r.Best, r.Average, r.Worst, r.Jitter, r.Sent, r.Received, r.LossPercent, r.RouteChanges, r.Updated }));
}
