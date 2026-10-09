using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed class HttpTestClient : IHttpTestClient
{
    public async Task<HttpTestResult> TestAsync(HttpTestRequest input, CancellationToken token)
    {
        if (input.TimeoutMs is < 250 or > 60000 || input.Method is not ("GET" or "HEAD") || input.ExpectedStatus is < 100 or > 599) throw new ArgumentException("Invalid HTTP test settings.");
        Uri current = DiagnosticInput.HttpUrl(input.Url.AbsoluteUri); string remote = "", tls = "", expiry = "", certificate = "", error = "";
        var watch = Stopwatch.StartNew(); var details = new List<string>(); var redirects = new List<string>(); var seen = new HashSet<string>(StringComparer.Ordinal);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(input.TimeoutMs);
        try
        {
            for (int hop = 0; ; hop++)
            {
                token.ThrowIfCancellationRequested();
                if (!seen.Add(current.AbsoluteUri)) throw new IOException("Redirect loop detected.");
                remote = ""; tls = ""; expiry = ""; certificate = ""; error = "";
                using var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false, MaxResponseHeadersLength = 16, AutomaticDecompression = DecompressionMethods.None };
                handler.ConnectCallback = async (context, ct) =>
                {
                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                    try { await socket.ConnectAsync(context.DnsEndPoint, ct).ConfigureAwait(false); var ip = (socket.RemoteEndPoint as IPEndPoint)?.Address; remote = ip is { IsIPv4MappedToIPv6: true } ? ip.MapToIPv4().ToString() : ip?.ToString() ?? ""; return new NetworkStream(socket, true); }
                    catch { socket.Dispose(); throw; }
                };
                handler.SslOptions.RemoteCertificateValidationCallback = (_, cert, chain, errors) =>
                {
                    if (cert is not null)
                    {
                        using var copy = new X509Certificate2(cert);
                        expiry = copy.NotAfter.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'");
                        certificate = $"Certificate subject: {copy.Subject}\nIssuer: {copy.Issuer}\nValid from: {copy.NotBefore.ToUniversalTime():u}\nExpires: {copy.NotAfter.ToUniversalTime():u}\nValidation: {errors}\nChain: {string.Join("; ", chain?.ChainStatus.Select(s => s.Status.ToString()) ?? [])}";
                    }
                    if (errors != SslPolicyErrors.None) error = "TLS certificate validation failed: " + errors;
                    return errors == SslPolicyErrors.None;
                };
                handler.PlaintextStreamFilter = (context, _) => { if (context.PlaintextStream is SslStream ssl) tls = ssl.SslProtocol.ToString() + " / " + ssl.NegotiatedCipherSuite; return ValueTask.FromResult(context.PlaintextStream); };
                using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
                using var request = new HttpRequestMessage(new HttpMethod(input.Method), current) { Version = HttpVersion.Version11, VersionPolicy = HttpVersionPolicy.RequestVersionOrLower };
                request.Headers.UserAgent.ParseAdd("NetStucked/0.6.0");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                int code = (int)response.StatusCode; string location = response.Headers.Location is { } loc ? DiagnosticInput.SafeUrl(loc.IsAbsoluteUri ? loc : new Uri(current, loc)) : "";
                details.Add($"{input.Method} {DiagnosticInput.SafeUrl(current)}\nHTTP/{response.Version} {code} {response.ReasonPhrase}\nRemote IP: {remote}\nTransport: direct connection, no proxy; response headers only\nTLS: {(tls.Length == 0 ? "—" : tls)}\n{certificate}");
                foreach (var h in response.Headers.Concat(response.Content.Headers).Take(40))
                    if (new[] { "Content-Type", "Content-Length", "Content-Encoding", "Cache-Control", "Server", "Date", "ETag", "Last-Modified", "Connection", "Allow", "Retry-After" }.Contains(h.Key, StringComparer.OrdinalIgnoreCase)) details.Add(h.Key + ": " + string.Join(", ", h.Value).Truncate(512));
                bool redirect = code is 301 or 302 or 303 or 307 or 308;
                if (redirect) redirects.Add($"{code} → {location}");
                if (!redirect || !input.FollowRedirects || response.Headers.Location is null)
                    return Result(code == input.ExpectedStatus ? "PASS" : redirect ? "Redirect" : "FAIL", code, watch.Elapsed.TotalMilliseconds, "");
                if (hop >= 5) throw new IOException("Redirect limit exceeded (5).");
                Uri next = DiagnosticInput.HttpUrl((response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(current, response.Headers.Location)).AbsoluteUri);
                if (current.Scheme == "https" && next.Scheme == "http") throw new IOException("HTTPS to HTTP downgrade redirect was blocked.");
                current = next;
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return Result("Timeout", null, null, "HTTP request timed out before completion."); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or SocketException or System.Security.Authentication.AuthenticationException or ArgumentException)
        { return Result("Error", null, null, error.Length > 0 ? error : ex is HttpRequestException ? "HTTP connection/request failed: " + ((HttpRequestException)ex).HttpRequestError : ex.Message.Truncate(512)); }
        HttpTestResult Result(string status, int? code, double? elapsed, string message) => new(DateTimeOffset.Now, DiagnosticInput.SafeUrl(input.Url), status, code, elapsed, remote, string.Join(" | ", redirects).Truncate(2048), tls, expiry, (string.Join("\n\n", details) + (message.Length > 0 ? "\n" + certificate + "\n" + message : "")).Truncate(16384), message);
    }
}
