using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using NetStucked.Core;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;
internal static partial class Program
{
    private static async Task<int> CheckCollectorSshAsync(string output, string fixture)
    {
        Directory.CreateDirectory(output);
        var start = new ProcessStartInfo("python") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(fixture); using var server = Process.Start(start) ?? throw new IOException("Could not start owned SSH fixture.");
        Task<string> error = server.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            using var ready = JsonDocument.Parse(await server.StandardOutput.ReadLineAsync(deadline.Token) ?? throw new IOException("SSH fixture failed to start."));
            int port = ready.RootElement.GetProperty("port").GetInt32(); string pin = ready.RootElement.GetProperty("pin").GetString()!;
            var target = new CollectorTarget(1, "127.0.0.1", port, "Owned SSH fixture only", null);
            CollectorRun Plan(string password, string? suppliedPin, bool fallback = true, string username = @"DOMAIN\fixture") => new(
                new(CollectorProtocol.Ssh, "Cisco IOS / IOS XE", output), [target], ["terminal length 0"], ["show running-config", "show version"],
                suppliedPin is null ? new Dictionary<string, string>() : new() { [target.Endpoint] = suppliedPin }, new(username, password, ""), fallback ? new(@"DOMAIN\fixture", "owned-fixture-password", "") : null);
            var controller = new CollectorController(new CollectorTransport(), new CollectorCaptureFactory());
            var unknown = (await controller.RunAsync(Plan("owned-fixture-password", null), _ => { }, deadline.Token)).Single();
            Require(unknown.State == CollectorDeviceState.Failed && unknown.Detail.Contains(pin), "Real SSH unknown host key fails closed and reports observed pin");
            var mismatch = (await controller.RunAsync(Plan("owned-fixture-password", "SHA256:" + new string('A', 43)), _ => { }, deadline.Token)).Single();
            Require(mismatch.State == CollectorDeviceState.Failed && mismatch.Detail.Contains("host key"), "Real SSH changed pin fails before credentials");
            var first = (await controller.RunAsync(Plan("owned-fixture-password", pin), _ => { }, deadline.Token)).Single();
            Require(first.State == CollectorDeviceState.Completed && first.AuthSlot == 1, "Real pinned SSH collects using AUTH1");
            Require(first.Bytes > 8 * 1024 * 1024 && first.Preview.Length < 8400, "Real SSH output exceeds 8 MiB with configured 256 KiB flow-control window and bounded preview");
            string text = await File.ReadAllTextAsync(first.TxtPath, deadline.Token);
            Require(text.Contains("ไทย") && !text.Contains("owned-fixture-password") && text.Contains("[REDACTED]"), "Actual SSH UTF-8 output preserved and echoed password redacted");
            Require(text.Split("fixture line # value > ไทย").Length - 1 == 8192 && text.Contains("owned stderr fixture"), "All 8192 large-output lines and extended stderr arrive in the capture without loss");
            var fallback = (await controller.RunAsync(Plan("owned-fixture-wrong-password", pin), _ => { }, deadline.Token)).Single();
            Require(fallback.State == CollectorDeviceState.Completed && fallback.AuthSlot == 2, "Real SSH rejection permits exactly AUTH2 fallback");
            var mfa = (await controller.RunAsync(Plan("owned-fixture-wrong-password", pin, true, "mfa-fixture"), _ => { }, deadline.Token)).Single();
            Require(mfa.State == CollectorDeviceState.Failed && mfa.Detail.Contains("unsupported", StringComparison.OrdinalIgnoreCase), "Real SSH keyboard-interactive/MFA is unsupported without fallback");
            var partialMfa = (await controller.RunAsync(Plan("owned-fixture-password", pin, true, "mfa-after-password"), _ => { }, deadline.Token)).Single();
            Require(partialMfa.State == CollectorDeviceState.Failed && partialMfa.AuthSlot == 1 && partialMfa.Detail.Contains("unsupported", StringComparison.OrdinalIgnoreCase), "Password accepted with an additional MFA factor never triggers AUTH2");
            var wait = Plan("owned-fixture-password", pin);
            var cancelPlan = new CollectorRun(wait.Options, wait.Targets, [], ["show wait"], wait.HostPins.ToDictionary(), wait.Auth1, wait.Auth2);
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var cancelled = (await controller.RunAsync(cancelPlan, row => { if (row.State == CollectorDeviceState.Collecting) cancel.CancelAfter(100); }, cancel.Token)).Single();
            Require(cancelled.State == CollectorDeviceState.Cancelled && File.Exists(cancelled.TxtPath), "Real SSH cancellation drains an active prompt wait and preserves partial evidence");
            await server.StandardInput.WriteLineAsync("stats"); await server.StandardInput.FlushAsync(deadline.Token);
            string audit = await server.StandardOutput.ReadLineAsync(deadline.Token) ?? "{}";
            using var stats = JsonDocument.Parse(audit);
            await File.WriteAllTextAsync(Path.Combine(output, "ssh-fixture-audit.json"), audit, new UTF8Encoding(false), deadline.Token);
            Require(stats.RootElement.GetProperty("domain_literal").GetBoolean() && stats.RootElement.GetProperty("no_bom").GetBoolean(), "Server receives literal DOMAIN backslash and BOM-free command bytes");
            Require(stats.RootElement.GetProperty("auth").GetArrayLength() == 6, "Server sees six bounded attempts across success, fallback, two MFA cases and cancel; host-key failures send no passwords");
            await File.WriteAllTextAsync(Path.Combine(output, "ssh-fixture-audit.json"), audit, new UTF8Encoding(false), deadline.Token);
            await File.WriteAllTextAsync(Path.Combine(output, "result.txt"), "PASS: owned loopback SSH integration. No Cisco/working network tested.", deadline.Token);
            return 0;
        }
        catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(output, "failure.txt"), ex.ToString()); return 1; }
        finally
        {
            if (!server.HasExited) { await server.StandardInput.WriteLineAsync("stop"); server.StandardInput.Close(); try { await server.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } catch (TimeoutException) { server.Kill(true); } }
            await File.WriteAllTextAsync(Path.Combine(output, "fixture-stderr.txt"), await error);
        }
    }
}
