namespace NetStucked.Core;

public static class CaptureDiff
{
    // A bounded changed-region comparison, deliberately not a minimum edit script.
    public static string Compare(string before, string after, CancellationToken token)
    {
        static string[] Lines(string s)
        {
            var lines = s.Replace("\r\n", "\n").Split('\n');
            int start = Array.FindIndex(lines, l => l.Trim() == "--- show running-config ---");
            if (start >= 0) { int end = Array.FindIndex(lines, start + 1, l => l.StartsWith("--- show ")); lines = lines[(start + 1)..(end < 0 ? lines.Length : end)]; }
            if (lines.Length > 100000) throw new ArgumentException("Diff limit is 100,000 lines per capture.");
            return lines;
        }
        if (before.Length + after.Length > 16 * 1024 * 1024) throw new ArgumentException("Diff limit is 16 MiB combined.");
        var a = Lines(before); var b = Lines(after); int prefix = 0, suffix = 0;
        while (prefix < Math.Min(a.Length, b.Length) && a[prefix] == b[prefix]) { token.ThrowIfCancellationRequested(); prefix++; }
        while (suffix < Math.Min(a.Length, b.Length) - prefix && a[^(suffix + 1)] == b[^(suffix + 1)]) { token.ThrowIfCancellationRequested(); suffix++; }
        if (prefix == a.Length && prefix == b.Length) return "No textual changes in the compared captured region.";
        var output = new System.Text.StringBuilder($"Changed region after {prefix} matching lines; {suffix} matching trailing lines.\nThis is a captured text comparison, not semantic configuration verification.\n");
        foreach (string line in a.Skip(prefix).Take(a.Length - prefix - suffix).Take(200)) output.AppendLine("- " + line[..Math.Min(line.Length, 1000)]);
        foreach (string line in b.Skip(prefix).Take(b.Length - prefix - suffix).Take(200)) output.AppendLine("+ " + line[..Math.Min(line.Length, 1000)]);
        if (a.Length - prefix - suffix > 200 || b.Length - prefix - suffix > 200) output.AppendLine("Display capped at 200 removed/200 added lines. Inspect original captures for full changes.");
        return output.ToString();
    }
}
