using System.Net;
using System.Text;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public sealed class AdvisoryLookupTests
{
    private const string Metadata = """{"vulnerabilities":[{"cve":{"id":"CVE-2023-20198","lastModified":"2026-10-20T00:00:00","descriptions":[{"lang":"en","value":"Owned timeout regression fixture"}],"references":[]}}]}""";

    [Fact]
    public async Task OptionalKevTimeoutRetainsSuccessfullyFetchedNvdMetadata()
    {
        using var lookup = new AdvisoryLookup(new Handler((request, _) => request.RequestUri!.Host == "www.cisa.gov"
            ? Task.FromException<HttpResponseMessage>(new TaskCanceledException("Owned provider timeout"))
            : Task.FromResult(Json(Metadata))));
        var result = await lookup.FetchAsync("CVE-2023-20198", default);
        Assert.Equal("Owned timeout regression fixture", result.Description);
        Assert.Null(result.Kev); Assert.Contains("unavailable; unknown", result.KevSource);
    }

    [Fact]
    public async Task NvdBodyReadHasDeadlineAfterHeadersArrive()
    {
        using var lookup = new AdvisoryLookup(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StreamContent(new UntilCancelledStream()) })), TimeSpan.FromMilliseconds(100));
        var error = await Assert.ThrowsAsync<IOException>(() => lookup.FetchAsync("CVE-2023-20198", default));
        Assert.Contains("timed out", error.Message);
    }

    [Fact]
    public async Task OptionalKevBodyReadDeadlineRetainsNvdMetadata()
    {
        using var lookup = new AdvisoryLookup(new Handler((request, _) => Task.FromResult(request.RequestUri!.Host == "www.cisa.gov"
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new UntilCancelledStream()) }
            : Json(Metadata))), TimeSpan.FromMilliseconds(100));
        var result = await lookup.FetchAsync("CVE-2023-20198", default);
        Assert.Equal("CVE-2023-20198", result.Cve); Assert.Null(result.Kev);
        Assert.Contains("NVD metadata retained", result.KevSource);
    }

    [Fact]
    public async Task UserCancellationDuringOptionalKevIsNotReportedAsSuccessfulFetch()
    {
        using var user = new CancellationTokenSource();
        using var lookup = new AdvisoryLookup(new Handler((request, token) =>
        {
            if (request.RequestUri!.Host != "www.cisa.gov") return Task.FromResult(Json(Metadata));
            user.Cancel(); return Task.FromCanceled<HttpResponseMessage>(token);
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lookup.FetchAsync("CVE-2023-20198", user.Token));
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }
    private sealed class UntilCancelledStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { await Task.Delay(Timeout.Infinite, cancellationToken); return 0; }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
