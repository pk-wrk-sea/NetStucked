using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

/// <summary>Public release catalog; download/execution is a separate user-triggered operation.</summary>
public sealed class GitHubReleaseSource : IReleaseSource, IDisposable
{
    private readonly HttpClient _client;
    public GitHubReleaseSource() : this(new HttpClientHandler { AllowAutoRedirect = false }) { }
    public GitHubReleaseSource(HttpMessageHandler handler)
    {
        _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("NetStucked/" + typeof(GitHubReleaseSource).Assembly.GetName().Version?.ToString(3));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }
    public async Task<ReleaseCheck> CheckAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        cancellationToken = deadline.Token;
        using var response = await _client.GetAsync($"https://api.github.com/repos/{ReleaseRepository.Owner}/{ReleaseRepository.Name}/releases?per_page=100", HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new HttpRequestException("The release repository is unavailable or private. Check again after it is accessible.");
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests) throw new HttpRequestException("GitHub denied the request or its API limit was reached. Try again later.");
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream(); var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > 2_000_000) throw new InvalidDataException("GitHub release response exceeds 2 MB.");
            buffer.Write(chunk, 0, read);
        }
        using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("GitHub returned an invalid release list.");
        var releases = new List<PublishedRelease>();
        foreach (var row in document.RootElement.EnumerateArray())
        {
            if (!row.TryGetProperty("draft", out var draft) || draft.ValueKind != JsonValueKind.False ||
                !row.TryGetProperty("prerelease", out var pre) || pre.ValueKind != JsonValueKind.False) continue;
            string tag = Text(row, "tag_name");
            if (tag.StartsWith('v')) tag = tag[1..];
            if (!SemanticVersion.TryParse(tag, out var version) || version!.PreRelease.Length > 0 || version.Metadata.Length > 0) continue;
            if (!Uri.TryCreate(Text(row, "html_url"), UriKind.Absolute, out var page) || !ReleaseRepository.IsReleasePage(page)) continue;
            Uri? installer = null;
            ReleaseInstaller? package = null;
            if (row.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray().Where(a => Text(a, "name") == $"NetStucked-{version}-win-x64-setup.exe"))
                {
                    installer = page;
                    string digest = Text(asset, "digest");
                    if (Text(asset, "state") != "uploaded" || !digest.StartsWith("sha256:", StringComparison.Ordinal) ||
                        !asset.TryGetProperty("id", out var id) || !id.TryGetInt64(out long assetId) ||
                        !asset.TryGetProperty("size", out var size) || !size.TryGetInt64(out long bytes) ||
                        !Uri.TryCreate(Text(asset, "browser_download_url"), UriKind.Absolute, out var download)) continue;
                    var candidate = new ReleaseInstaller(assetId, Text(asset, "name"), download, digest[7..].ToLowerInvariant(), bytes);
                    try { InstallerPolicy.Validate(candidate, version); package = candidate; break; }
                    catch (InvalidDataException) { /* Browser fallback remains available, but execution is disabled. */ }
                }
            }
            string notes = Text(row, "body"); if (notes.Length > 12000) notes = notes[..12000] + "\n… Read the complete notes on GitHub.";
            DateTimeOffset? published = DateTimeOffset.TryParse(Text(row, "published_at"), out var date) ? date : null;
            releases.Add(new(version, Text(row, "name"), notes, page, installer, published, package));
        }
        return new(releases.OrderByDescending(r => r.Version).DistinctBy(r => r.Version.ToString()).ToArray(), DateTimeOffset.Now);
    }
    private static string Text(JsonElement element, string property) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    public void Dispose() => _client.Dispose();
}
