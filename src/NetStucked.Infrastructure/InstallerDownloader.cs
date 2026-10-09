using System.Net;
using System.Security.Cryptography;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed class InstallerDownloader : IDisposable
{
    private readonly HttpClient _client;
    public InstallerDownloader() : this(new HttpClientHandler { AllowAutoRedirect = false }) { }
    public InstallerDownloader(HttpMessageHandler handler)
    {
        _client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("NetStucked-updater/0.3.0");
    }
    public async Task DownloadAsync(ReleaseInstaller asset, SemanticVersion version, string destination, IProgress<UpdateProgress> progress, CancellationToken token)
    {
        InstallerPolicy.Validate(asset, version);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromMinutes(10)); token = deadline.Token;
        string partial = destination + ".partial";
        if (File.Exists(destination) || File.Exists(partial)) throw new IOException("Update destination already exists.");
        try
        {
            Uri current = asset.DownloadUri;
            using var response = await OpenAsync(current, asset, version, token).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength is long length && length != asset.Bytes) throw new InvalidDataException("Installer size differs from GitHub metadata.");
            await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                byte[] buffer = new byte[65536]; long total = 0; int read;
                long nextProgress = 0;
                while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (total > asset.Bytes || total > InstallerPolicy.MaximumBytes) throw new InvalidDataException("Installer exceeded the allowed size.");
                    await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false); hash.AppendData(buffer, 0, read);
                    if (total >= nextProgress) { progress.Report(new("Downloading installer", total, asset.Bytes)); nextProgress = total + 512 * 1024; }
                }
                if (total != asset.Bytes || !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), Convert.FromHexString(asset.Sha256))) throw new InvalidDataException("Installer SHA-256/size verification failed. Nothing will be executed.");
                await output.FlushAsync(token).ConfigureAwait(false);
                progress.Report(new("SHA-256 verified", total, total));
            }
            token.ThrowIfCancellationRequested(); File.Move(partial, destination);
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }
    private async Task<HttpResponseMessage> OpenAsync(Uri current, ReleaseInstaller asset, SemanticVersion version, CancellationToken token)
    {
        for (int redirects = 0; redirects <= 4; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.Accept.ParseAdd("application/octet-stream");
            var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                Uri? location = response.Headers.Location; response.Dispose();
                if (location is null) throw new InvalidDataException("Installer redirect has no destination.");
                current = location.IsAbsoluteUri ? location : new Uri(current, location);
                if (!InstallerPolicy.IsAssetRedirect(current) && !InstallerPolicy.IsSourceUri(current, version, asset.Name)) throw new InvalidDataException("Installer redirect left GitHub's allowed asset hosts.");
                continue;
            }
            try { response.EnsureSuccessStatusCode(); return response; }
            catch { response.Dispose(); throw; }
        }
        throw new InvalidDataException("Too many installer redirects.");
    }
    public void Dispose() => _client.Dispose();
}
