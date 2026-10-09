using System.Globalization;
using System.Text.RegularExpressions;

namespace NetStucked.Core;

public sealed partial record SemanticVersion(int Major, int Minor, int Patch, string PreRelease = "", string Metadata = "") : IComparable<SemanticVersion>
{
    [GeneratedRegex(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
    public static bool TryParse(string? value, out SemanticVersion? version)
    {
        version = null;
        if (value is null || value.Length > 200) return false;
        var match = Pattern().Match(value);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out int major) || !int.TryParse(match.Groups[2].Value, out int minor) || !int.TryParse(match.Groups[3].Value, out int patch)) return false;
        string pre = match.Groups[4].Value;
        if (pre.Split('.').Any(p => p.Length > 1 && p[0] == '0' && p.All(char.IsAsciiDigit))) return false;
        version = new(major, minor, patch, pre, match.Groups[5].Value); return true;
    }
    public int CompareTo(SemanticVersion? other)
    {
        if (other is null) return 1;
        int number = Major.CompareTo(other.Major); if (number != 0) return number;
        number = Minor.CompareTo(other.Minor); if (number != 0) return number;
        number = Patch.CompareTo(other.Patch); if (number != 0) return number;
        if (PreRelease.Length == 0 || other.PreRelease.Length == 0) return PreRelease.Length == other.PreRelease.Length ? 0 : PreRelease.Length == 0 ? 1 : -1;
        string[] left = PreRelease.Split('.'), right = other.PreRelease.Split('.');
        for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            bool ln = left[i].All(char.IsAsciiDigit), rn = right[i].All(char.IsAsciiDigit);
            number = ln && rn ? left[i].Length.CompareTo(right[i].Length) : ln != rn ? ln ? -1 : 1 : 0;
            if (number == 0) number = string.CompareOrdinal(left[i], right[i]);
            if (number != 0) return number;
        }
        return left.Length.CompareTo(right.Length);
    }
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}{(PreRelease.Length == 0 ? "" : "-" + PreRelease)}{(Metadata.Length == 0 ? "" : "+" + Metadata)}");
}

public sealed record PublishedRelease(SemanticVersion Version, string Name, string Notes, Uri Page, Uri? InstallerPage, DateTimeOffset? PublishedAt);
public sealed record ReleaseCheck(IReadOnlyList<PublishedRelease> Releases, DateTimeOffset CheckedAt);
public interface IReleaseSource
{
    Task<ReleaseCheck> CheckAsync(CancellationToken cancellationToken);
}
public static class ReleaseRepository
{
    public const string Owner = "pk-wrk-sea";
    public const string Name = "NetStucked";
    public static Uri ReleasesPage { get; } = new($"https://github.com/{Owner}/{Name}/releases");
    public static bool IsReleasePage(Uri uri) => uri.Scheme == "https" && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
        uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.AbsolutePath.StartsWith($"/{Owner}/{Name}/releases/tag/", StringComparison.Ordinal) && uri.Fragment.Length == 0 && uri.Query.Length == 0;
}
