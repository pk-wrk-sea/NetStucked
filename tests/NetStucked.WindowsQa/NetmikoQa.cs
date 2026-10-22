using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using NetStucked.Core;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;
internal static partial class Program
{
    private sealed class CollectorTrustPrompt : ITerminalHostKeyPrompt
    {
        public int Calls; public bool SawChanged; public TerminalHostKeyDecision Decision = TerminalHostKeyDecision.AcceptAndSave;
        public Task<TerminalHostKeyDecision> ConfirmAsync(TerminalHostKeyChallenge challenge, CancellationToken token)
        { Calls++; SawChanged |= challenge.Changed; return Task.FromResult(Decision); }
    }
    private static async Task<int> CheckNetmikoAsync(string output, string fixture)
    {
        Directory.CreateDirectory(output);
        string runtime = Path.Combine(AppContext.BaseDirectory, "CollectorRuntime");
        var start = new ProcessStartInfo(Path.Combine(runtime, "python.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-I", "-B", "-u", fixture }) start.ArgumentList.Add(arg);
        using var server = Process.Start(start)!; var stderr = server.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        try
        {
            using var ready = JsonDocument.Parse((await server.StandardOutput.ReadLineAsync(deadline.Token))!);
            var ports = ready.RootElement.GetProperty("targets").EnumerateArray().ToDictionary(t => t.GetProperty("mode").GetString()!, t => t.GetProperty("port").GetInt32());
            string pin = ready.RootElement.GetProperty("pin").GetString()!;
            var settings = new UserSettingsStore(Path.Combine(output, "isolated-settings")); var keys = new TerminalKnownHostStore(settings); var prompt = new CollectorTrustPrompt();
            var trust = new TerminalHostTrust(keys, prompt);
            var controller = new CollectorController(new CollectorTransport(), new CollectorCaptureFactory(), new NetmikoCollectorSession(trust));
            CollectorRun Plan(string mode, string password = "fixture-password", string username = "fixture", bool fallback = false, string? command = null) => new(new(CollectorProtocol.Ssh, mode == "huawei" || mode == "huawei-confirm" ? "Huawei VRP (Switch/Router)" : mode == "xr" ? "Cisco IOS XR" : mode == "nxos" ? "Cisco NX-OS" : mode == "xe" ? "Cisco IOS-XE" : "Cisco IOS", output, 3, 5, 5), [new(1, "127.0.0.1", ports[mode], "Owned fixture", null)], [], [command ?? (mode.StartsWith("huawei") ? "display current-configuration" : "show running-config")], new Dictionary<string, string>(), new(username, password, ""), fallback ? new("fixture", "fixture-password", "") : null);
            async Task<JsonElement> Stats()
            {
                await server.StandardInput.WriteLineAsync("stats"); await server.StandardInput.FlushAsync();
                using var json = JsonDocument.Parse((await server.StandardOutput.ReadLineAsync(deadline.Token))!); return json.RootElement.Clone();
            }
            prompt.Decision = TerminalHostKeyDecision.Reject;
            var rejected = (await controller.RunAsync(Plan("ios"), _ => { }, deadline.Token)).Single();
            Require(rejected.State == CollectorDeviceState.Failed && rejected.Detail.Contains("not accepted"), "Netmiko first-use rejection fails before login");
            Require((await Stats()).GetProperty("auth").GetArrayLength() == 0, "Netmiko sends zero credentials before trust approval");
            prompt.Decision = TerminalHostKeyDecision.AcceptAndSave;
            foreach (string mode in new[] { "ios", "xe", "xr", "nxos", "huawei" })
            {
                var result = (await controller.RunAsync(Plan(mode), _ => { }, deadline.Token)).Single();
                Require(result.State == CollectorDeviceState.Completed, "Actual bundled Netmiko driver " + mode + " completes: " + result.Detail);
                Require(result.DeviceHostname.StartsWith("LAB-") && Path.GetFileName(result.TxtPath).StartsWith("127.0.0.1 " + result.DeviceHostname), "Observed hostname/IP drive safe filenames for " + mode);
                string text = await File.ReadAllTextAsync(result.TxtPath);
                Require(text.Contains("ไทย") && !text.Contains("fixture-password") && text.Contains("[REDACTED]"), "Actual streamed output preserves Unicode and redacts password for " + mode);
                Require(File.Exists(result.JsonPath) && result.Sha256.Length == 64, "Actual TXT/JSON/hash finalized for " + mode);
            }
            int calls = prompt.Calls;
            var known = (await controller.RunAsync(Plan("ios"), _ => { }, deadline.Token)).Single();
            Require(known.State == CollectorDeviceState.Completed && prompt.Calls == calls, "Saved matching key reconnects without another approval");
            var saved = (await keys.LoadAsync(default)).Single(k => k.Port == ports["ios"]);
            await keys.SaveAsync(saved with { Fingerprint = "SHA256:" + new string('A', 43) }, saved.Fingerprint, default);
            prompt.Decision = TerminalHostKeyDecision.Reject; int beforeChanged = (await Stats()).GetProperty("auth").GetArrayLength();
            await controller.RunAsync(Plan("ios", fallback: true), _ => { }, deadline.Token);
            Require(prompt.SawChanged && (await Stats()).GetProperty("auth").GetArrayLength() == beforeChanged, "Changed host key explicitly reviewed; rejection sends no AUTH1/AUTH2");
            prompt.Decision = TerminalHostKeyDecision.AcceptAndSave;
            var fallback = (await controller.RunAsync(Plan("xe", "wrong-fixture-password", fallback: true), _ => { }, deadline.Token)).Single();
            Require(fallback.State == CollectorDeviceState.Completed && fallback.AuthSlot == 2, "Actual password rejection permits bounded AUTH2");
            foreach (string user in new[] { "mfa-user", "partial-user" })
            {
                var result = (await controller.RunAsync(Plan("xe", username: user, fallback: true), _ => { }, deadline.Token)).Single();
                Require(result.State == CollectorDeviceState.Failed && result.AuthSlot == 1, "MFA/unsupported login never falls back: " + user);
            }
            var bad = (await controller.RunAsync(Plan("xe", command: "show bad", fallback: true), _ => { }, deadline.Token)).Single();
            Require(bad.State == CollectorDeviceState.Partial && bad.AuthSlot == 1, "Command rejection retains partial capture without credential fallback/replay");
            var huawei = (await controller.RunAsync(Plan("huawei-confirm", fallback: true), _ => { }, deadline.Token)).Single();
            Require(huawei.State == CollectorDeviceState.Failed && huawei.AuthSlot == 1, "Huawei interactive first-login configuration request stops safely");
            foreach (string mode in new[] { "wait", "kex-stall" })
            {
                using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(3)); var watch = Stopwatch.StartNew();
                var result = (await controller.RunAsync(Plan(mode), _ => { }, cancel.Token)).Single();
                Require(result.State == CollectorDeviceState.Cancelled && watch.Elapsed < TimeSpan.FromSeconds(6), $"Worker cancellation drains during {mode}: {result.State}, {watch.ElapsedMilliseconds} ms, {result.Detail}");
            }
            string input = string.Join('\n', new[] { ("ios", "ios"), ("xe", "ios-xe"), ("xr", "ios-xr"), ("nxos", "nxos"), ("huawei", "huawei") }.Select(p => $"127.0.0.1:{ports[p.Item1]} profile={p.Item2}"));
            var batch = new CollectorRun(new(CollectorProtocol.Ssh, "Cisco IOS-XE", output, 3, 5, 5), CollectorInputParser.Parse(input, 22), [], ["show running-config"], new Dictionary<string, string>(), new("fixture", "fixture-password", ""), null);
            for (int drain = 0; drain < 30 && (await Stats()).GetProperty("active").GetInt32() != 0; drain++) await Task.Delay(100, deadline.Token);
            Require((await Stats()).GetProperty("active").GetInt32() == 0, "Owned fixture observes all prior sessions closed before parallel capacity check");
            await server.StandardInput.WriteLineAsync("reset-peak"); await server.StandardInput.FlushAsync();
            var results = await controller.RunAsync(batch, _ => { }, deadline.Token);
            Require(results.Count == 5 && results.All(r => r.State == CollectorDeviceState.Completed), "Mixed-vendor parallel batch completes all five actual workers");
            JsonElement audit = await Stats();
            Require(audit.GetProperty("peak").GetInt32() is >= 2 and <= 3, "Observed concurrent sessions obey selected limit 3");
            Require(!audit.GetProperty("commands").EnumerateArray().Any(c => c.GetProperty("command").GetString() is "Y" or "N" or "system-view" or "configure terminal"), "Owned server observed no configuration changes or automated confirmation answers");
            var telnet = new CollectorRun(new(CollectorProtocol.Telnet, "Cisco IOS-XE", output, 1, 5, 5, AllowTelnet: true), [new(1, "127.0.0.1", ports["telnet"], "Owned acknowledged plaintext fixture", null)], [], ["show running-config"], new Dictionary<string, string>(), new("fixture", "fixture-password", ""), null);
            var telnetResult = (await controller.RunAsync(telnet, _ => { }, deadline.Token)).Single();
            Require(telnetResult.State == CollectorDeviceState.Completed, "Explicitly acknowledged Telnet collects through actual Netmiko driver: " + telnetResult.Detail);
            var large = (await controller.RunAsync(Plan("large"), _ => { }, deadline.Token)).Single();
            Require(large.State == CollectorDeviceState.Completed && large.Bytes > 250000 && large.Preview.Length < 8400, "Actual Netmiko streams large output with bounded preview");
            string largeText = await File.ReadAllTextAsync(large.TxtPath);
            Require(largeText.Split("owned large output ไทย [REDACTED]").Length - 1 == 8192, "All 8192 Unicode lines retained without credential leakage");
            var limitedPlan = Plan("large");
            var limited = (await controller.RunAsync(new(limitedPlan.Options with { MaxCaptureBytes = 4096 }, limitedPlan.Targets, limitedPlan.BasicCommands, limitedPlan.CollectionCommands, new Dictionary<string, string>(), limitedPlan.Auth1, null), _ => { }, deadline.Token)).Single();
            Require(limited.State == CollectorDeviceState.Partial && limited.Bytes <= 4096, "Actual worker/capture output limit stops without exceeding stored byte budget");
            var disconnected = (await controller.RunAsync(Plan("disconnect", fallback: true), _ => { }, deadline.Token)).Single();
            Require(disconnected.State == CollectorDeviceState.Partial && disconnected.AuthSlot == 1, "Session disconnect after command send never reconnects or switches credentials");
            await File.WriteAllTextAsync(Path.Combine(output, "audit.json"), audit.GetRawText(), Encoding.UTF8);
            await File.WriteAllTextAsync(Path.Combine(output, "result.txt"), "PASS: actual bundled Netmiko / five owned software profiles / trust / fallback / cancellation / parallel / capture. Physical vendor and AAA acceptance NOT TESTED.");
            return 0;
        }
        catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(output, "failure.txt"), ex.ToString()); Console.Error.WriteLine(ex); return 1; }
        finally
        {
            if (!server.HasExited) { await server.StandardInput.WriteLineAsync("stop"); server.StandardInput.Close(); try { await server.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } catch (TimeoutException) { server.Kill(true); await server.WaitForExitAsync(); } }
            await File.WriteAllTextAsync(Path.Combine(output, "fixture-stderr.txt"), await stderr);
        }
    }
}
