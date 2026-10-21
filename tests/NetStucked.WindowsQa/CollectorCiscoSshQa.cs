using System.Diagnostics;
using System.IO;
using System.Text.Json;
using NetStucked.Core;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;
internal static partial class Program
{
    private static async Task<int> CheckCollectorCiscoSshAsync(string output, string fixture)
    {
        Directory.CreateDirectory(output);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var cases = new List<object>();
        try
        {
            foreach (string mode in new[] { "ctr", "gcm", "deny-channel", "deny-pty", "deny-shell", "early-close", "rekey", "slow-auth-shell" })
            {
                await using var server = await CompatServer.StartAsync(fixture, mode, Path.Combine(output, mode), deadline.Token);
                var plan = server.Plan();
                var result = (await new CollectorController(new CollectorTransport(), new CollectorCaptureFactory()).RunAsync(plan, _ => { }, deadline.Token)).Single();
                bool expected = mode is "ctr" or "gcm" or "slow-auth-shell";
                Require(expected ? result.State == CollectorDeviceState.Completed : result.State is CollectorDeviceState.Failed or CollectorDeviceState.Partial,
                    "Cisco SSH compatibility " + mode + " reaches expected safe result: " + result.State + " / " + result.Detail);
                Require(!result.Detail.Contains("owned-compat-password") && !result.Detail.Contains("fixture-server-secret"), "Compatibility failure details contain no credentials or server-controlled diagnostics");
                using var audit = await server.AuditAsync(deadline.Token);
                if (expected)
                {
                    Require(audit.RootElement.GetProperty("cipher").GetString() == (mode == "gcm" ? "aes128-gcm@openssh.com" : "aes256-ctr") && audit.RootElement.GetProperty("mac").GetString() == "hmac-sha2-512-etm@openssh.com", "Owned server verifies the actual strong negotiated cipher/MAC");
                    string capture = await File.ReadAllTextAsync(result.TxtPath, deadline.Token);
                    Require(capture.Contains("ไทย") && capture.Contains("owned stderr ") && capture.Contains("ก") && !capture.Contains('\uFFFD'), "Separate incremental stdout/stderr UTF-8 survives interleaved fragments");
                }
                if (mode == "rekey") Require(audit.RootElement.GetProperty("rekey").GetInt32() == 1 && result.Detail.Contains("host key", StringComparison.OrdinalIgnoreCase), "Changed host key on real rekey fails closed without new credentials");
                Require(audit.RootElement.GetProperty("password").GetArrayLength() == 1, "Each compatibility session sends exactly one password attempt without replay/fallback");
                if (mode.StartsWith("deny-", StringComparison.Ordinal)) Require(audit.RootElement.GetProperty("channels").GetArrayLength() == 1 && audit.RootElement.GetProperty("commands").GetArrayLength() == 0, "Rejected setup sends one channel request and no collection commands");
                cases.Add(new { Mode = mode, State = result.State.ToString(), result.Detail });
            }
            foreach (string authCase in new[] { "unknown-pin", "mismatched-pin", "rejected-password", "mfa-only", "partial-mfa", "password-change", "none-success" })
            {
                await using var server = await CompatServer.StartAsync(fixture, authCase is "password-change" or "none-success" ? authCase : "ctr", Path.Combine(output, authCase), deadline.Token);
                var plan = server.Plan(authCase == "mfa-only" ? "mfa-only" : authCase == "partial-mfa" ? "partial-mfa" : "fixture",
                    authCase == "rejected-password" ? "wrong-owned-password" : "owned-compat-password", authCase == "unknown-pin" ? "" : authCase == "mismatched-pin" ? "SHA256:" + new string('A', 43) : null, fallback: true);
                var result = (await new CollectorController(new CollectorTransport(), new CollectorCaptureFactory()).RunAsync(plan, _ => { }, deadline.Token)).Single();
                using var audit = await server.AuditAsync(deadline.Token);
                if (authCase is "unknown-pin" or "mismatched-pin") Require(result.State == CollectorDeviceState.Failed && audit.RootElement.GetProperty("none").GetArrayLength() == 0 && audit.RootElement.GetProperty("password").GetArrayLength() == 0, "Unknown/changed pin rejects before ANY authentication including none");
                else if (authCase == "rejected-password") Require(result.State == CollectorDeviceState.Completed && result.AuthSlot == 2 && audit.RootElement.GetProperty("password").GetArrayLength() == 2, "Only actual password rejection permits exactly AUTH2 fallback");
                else Require(result.State == CollectorDeviceState.Failed && result.AuthSlot == 1 && audit.RootElement.GetProperty("password").GetArrayLength() == (authCase is "mfa-only" or "none-success" ? 0 : 1), "Unsupported/partial MFA, password change and none-only login never permit credential fallback");
                if (authCase == "password-change") Require(audit.RootElement.GetProperty("password_change").GetInt32() == 1, "Real password-change request fails closed without a replacement credential");
                if (authCase == "none-success") Require(audit.RootElement.GetProperty("channels").GetArrayLength() == 0 && result.Detail.Contains("password authentication"), "None-auth success cannot silently bypass the password-only Collector policy");
                cases.Add(new { Mode = authCase, State = result.State.ToString(), result.Detail });
            }
            foreach (string mode in new[] { "stall-kex", "stall-none", "stall-password", "stall-channel", "stall-pty", "stall-shell" })
            {
                await using var server = await CompatServer.StartAsync(fixture, mode, Path.Combine(output, mode), deadline.Token);
                var plan = server.Plan(timeout: 30);
                using var cancel = new CancellationTokenSource();
                Task<IReadOnlyList<CollectorResult>> running = new CollectorController(new CollectorTransport(), new CollectorCaptureFactory()).RunAsync(plan, _ => { }, cancel.Token);
                string field = mode switch { "stall-kex" => "kex", "stall-none" => "none", "stall-password" => "password", "stall-channel" => "channels", "stall-pty" => "pty", _ => "shell" };
                await server.WaitAsync(field, deadline.Token);
                var watch = Stopwatch.StartNew(); cancel.Cancel();
                var result = (await running.WaitAsync(TimeSpan.FromSeconds(3), deadline.Token)).Single();
                Require(result.State == CollectorDeviceState.Cancelled && watch.Elapsed < TimeSpan.FromSeconds(3), "Cancellation drains owned SDK/relay tasks promptly while " + mode);
                await server.WaitAsync("closed", deadline.Token);
                using var audit = await server.AuditAsync(deadline.Token);
                Require(audit.RootElement.GetProperty("password").GetArrayLength() <= 1 && audit.RootElement.GetProperty("commands").GetArrayLength() == 0 && audit.RootElement.GetProperty("closed").GetInt32() > 0, "Cancelled setup closes the peer socket, sends no commands and never retries/falls back");
                cases.Add(new { Mode = mode, State = result.State.ToString(), CancelMilliseconds = watch.ElapsedMilliseconds });
            }
            await using (var server = await CompatServer.StartAsync(fixture, "initial-flood", Path.Combine(output, "initial-flood"), deadline.Token))
            {
                var watch = Stopwatch.StartNew();
                var result = (await new CollectorController(new CollectorTransport(), new CollectorCaptureFactory()).RunAsync(server.Plan(fallback: true, timeout: 30), _ => { }, deadline.Token)).Single();
                using var audit = await server.AuditAsync(deadline.Token);
                Require(result.State == CollectorDeviceState.Failed && result.Detail.Contains("64 KiB setup limit") && watch.Elapsed < TimeSpan.FromSeconds(3), "Oversized pre-shell banner is rejected promptly by the explicit combined setup limit");
                Require(audit.RootElement.GetProperty("password").GetArrayLength() == 1 && audit.RootElement.GetProperty("commands").GetArrayLength() == 0, "Rejected initial output never triggers command execution or AUTH2");
                cases.Add(new { Mode = "initial-flood", State = result.State.ToString(), result.Detail, Milliseconds = watch.ElapsedMilliseconds });
            }
            await using (var server = await CompatServer.StartAsync(fixture, "flood", Path.Combine(output, "flood"), deadline.Token))
            {
                var plan = server.Plan();
                var connection = await new CollectorTransport().ConnectAsync(plan.Targets[0], plan.Options, plan.Auth1, server.Pin, deadline.Token);
                await Task.Delay(250, deadline.Token);
                using var saturated = await server.AuditAsync(deadline.Token);
                Require(saturated.RootElement.GetProperty("flood_bytes").GetInt32() >= 128 * 1024, "Owned post-shell flood exceeds queue capacity while the consumer deliberately does not read");
                var watch = Stopwatch.StartNew();
                await connection.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3), deadline.Token);
                await server.WaitAsync("closed", deadline.Token);
                Require(watch.Elapsed < TimeSpan.FromSeconds(3), "Closing a saturated bounded combined output queue unblocks SDK writer before disposal");
                bool stoppedWrite = false;
                try { await connection.WriteAsync("show version\r"u8.ToArray(), deadline.Token); }
                catch (OperationCanceledException) { stoppedWrite = true; }
                Require(stoppedWrite, "Writing after awaited session disposal is cancelled without enqueueing new SDK work");
                cases.Add(new { Mode = "flood", State = "Disposed", Milliseconds = watch.ElapsedMilliseconds });
            }
            await File.WriteAllTextAsync(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "PASS", Cases = cases, Scope = "Owned loopback software fixtures; no Cisco hardware or working network tested" }, new JsonSerializerOptions { WriteIndented = true }), deadline.Token);
            return 0;
        }
        catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(output, "failure.txt"), ex.ToString()); Console.Error.WriteLine("FAIL " + ex); return 1; }
    }
    private sealed class CompatServer : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly Task<string> _error;
        private readonly string _output;
        public int Port { get; private set; }
        public string Pin { get; private set; } = "";
        private CompatServer(Process process, string output) { _process = process; _error = process.StandardError.ReadToEndAsync(); _output = output; }
        public static async Task<CompatServer> StartAsync(string fixture, string mode, string output, CancellationToken token)
        {
            Directory.CreateDirectory(output);
            var start = new ProcessStartInfo("python") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(fixture); start.ArgumentList.Add(mode);
            var server = new CompatServer(Process.Start(start) ?? throw new IOException("Owned compatibility fixture unavailable."), output);
            try
            {
                using var ready = JsonDocument.Parse(await server._process.StandardOutput.ReadLineAsync(token) ?? throw new IOException("Owned fixture did not become ready."));
                server.Port = ready.RootElement.GetProperty("port").GetInt32(); server.Pin = ready.RootElement.GetProperty("pin").GetString()!;
                return server;
            }
            catch { await server.DisposeAsync(); throw; }
        }
        public CollectorRun Plan(string username = "fixture", string password = "owned-compat-password", string? pin = null, bool fallback = false, int timeout = 5) => new(
            new(CollectorProtocol.Ssh, "Cisco IOS / IOS XE", _output, ConnectTimeoutSeconds: timeout, CommandTimeoutSeconds: 5),
            [new(1, "127.0.0.1", Port, "Owned compatibility software fixture", null)], ["terminal length 0"], ["show running-config", "show version"],
            pin == "" ? new Dictionary<string, string>() : new() { ["127.0.0.1:" + Port] = pin ?? Pin }, new(username, password, ""), fallback ? new("fixture", "owned-compat-password", "") : null);
        public async Task<JsonDocument> AuditAsync(CancellationToken token)
        {
            await _process.StandardInput.WriteLineAsync("stats"); await _process.StandardInput.FlushAsync(token);
            string text = await _process.StandardOutput.ReadLineAsync(token) ?? throw new IOException("Owned fixture stopped before audit.");
            await File.WriteAllTextAsync(Path.Combine(_output, "audit.json"), text, token); return JsonDocument.Parse(text);
        }
        public async Task WaitAsync(string field, CancellationToken token)
        {
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(8))
            {
                using var audit = await AuditAsync(token);
                var value = audit.RootElement.GetProperty(field);
                if (value.ValueKind == JsonValueKind.Number ? value.GetInt32() > 0 : value.GetArrayLength() > 0) return;
                await Task.Delay(25, token);
            }
            throw new TimeoutException("Owned fixture did not reach " + field);
        }
        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
            {
                await _process.StandardInput.WriteLineAsync("stop"); _process.StandardInput.Close();
                try { await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } catch (TimeoutException) { _process.Kill(true); await _process.WaitForExitAsync(); }
            }
            await File.WriteAllTextAsync(Path.Combine(_output, "fixture-stderr.txt"), await _error); _process.Dispose();
        }
    }
}
