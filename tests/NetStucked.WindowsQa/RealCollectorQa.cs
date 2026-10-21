using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using NetStucked.Core;
using NetStucked.Desktop;
using NetStucked.Desktop.Services;
using NetStucked.Desktop.ViewModels;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;
internal static partial class Program
{
    private static async Task<int> CheckCollectorBannerSshAsync(string output, string fixture)
    {
        Directory.CreateDirectory(output);
        var start = new ProcessStartInfo("python") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(fixture); start.Environment["NETSTUCKED_QA_BANNER"] = "1";
        using var server = Process.Start(start) ?? throw new IOException("Could not start owned SSH software fixture.");
        Task<string> error = server.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            using var ready = JsonDocument.Parse(await server.StandardOutput.ReadLineAsync(deadline.Token) ?? throw new IOException("Owned SSH fixture failed to start."));
            string pin = ready.RootElement.GetProperty("pin").GetString()!;
            var targets = ready.RootElement.GetProperty("targets").EnumerateArray().Select((t, i) => new CollectorTarget(i + 1, t.GetProperty("host").GetString()!, t.GetProperty("port").GetInt32(), "Owned software banner fixture", null)).ToArray();
            Require(targets.Length == 2 && targets.All(t => t.Host == "127.0.0.1"), "Banner regression admits owned loopback targets only");
            var plan = new CollectorRun(new(CollectorProtocol.Ssh, "Cisco IOS / IOS XE", output), targets, ["terminal length 0"], ["show running-config", "show version"], targets.ToDictionary(t => t.Endpoint, _ => pin), new(@"DOMAIN\fixture", "owned-lab-password", ""), new(@"DOMAIN\fixture", "unused-owned-password", ""));
            var results = await new CollectorController(new CollectorTransport(), new CollectorCaptureFactory()).RunAsync(plan, _ => { }, deadline.Token);
            Require(results.All(r => r.State == CollectorDeviceState.Completed), "Real SSH does not misinterpret configured MFA/OTP/access-denied/confirmation banner text as live prompts: " + string.Join("; ", results.Select(r => r.State + " " + r.Detail)));
            foreach (var result in results)
            {
                string transcript = await File.ReadAllTextAsync(result.TxtPath, deadline.Token);
                Require(transcript.Contains("OTP verification code:") && transcript.Contains("Press enter to continue [confirm]") && transcript.Contains("Cisco IOS XE Software"), "Real fragmented SSH capture preserves configured prompt-looking banner and proceeds to next command");
            }
            await server.StandardInput.WriteLineAsync("stats"); await server.StandardInput.FlushAsync(deadline.Token);
            string auditText = await server.StandardOutput.ReadLineAsync(deadline.Token) ?? "{}";
            await File.WriteAllTextAsync(Path.Combine(output, "owned-ssh-audit.json"), auditText, deadline.Token);
            using var audit = JsonDocument.Parse(auditText);
            Require(audit.RootElement.GetProperty("auth").GetArrayLength() == 2 && audit.RootElement.GetProperty("commands").GetArrayLength() == 6 && audit.RootElement.GetProperty("blank_commands").GetInt32() == 0, "Banner regression performs one login and three read-only CR-terminated commands per device without blank commands, credential fallback or replay");
            await File.WriteAllTextAsync(Path.Combine(output, "result.txt"), "PASS: real owned SSH fragmented configuration banner regression; no working network tested.", deadline.Token);
            return 0;
        }
        catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(output, "failure.txt"), ex.ToString()); Console.Error.WriteLine("FAIL " + ex.Message); return 1; }
        finally
        {
            if (!server.HasExited)
            {
                await server.StandardInput.WriteLineAsync("stop"); server.StandardInput.Close();
                try { await server.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (TimeoutException) { server.Kill(true); }
            }
            await File.WriteAllTextAsync(Path.Combine(output, "fixture-stderr.txt"), await error);
        }
    }

    // No test transport/capture implementation: this exercises WPF -> actual pinned
    // SSH shell -> actual protected streamed TXT/JSON capture, on owned loopback only.
    private static async Task RunRealCollectorOnlyAsync(Application app, string output, string fixture, BindingErrors errors)
    {
        Directory.CreateDirectory(output);
        var start = new ProcessStartInfo("python") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(fixture);
        using var server = Process.Start(start) ?? throw new IOException("Could not start owned SSH software fixture.");
        Task<string> error = server.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(150));
        try
        {
            using var ready = JsonDocument.Parse(await server.StandardOutput.ReadLineAsync(deadline.Token) ?? throw new IOException("Owned SSH fixture failed to start."));
            string pin = ready.RootElement.GetProperty("pin").GetString()!;
            var targets = ready.RootElement.GetProperty("targets").EnumerateArray().Select(t =>
                (Name: t.GetProperty("name").GetString()!, Host: t.GetProperty("host").GetString()!, Port: t.GetProperty("port").GetInt32())).ToArray();
            Require(targets.Length == 2 && targets.All(t => t.Host == "127.0.0.1"), "Real Collector QA admits only its two owned loopback software fixtures");
            var store = new UserSettingsStore(Path.Combine(output, "isolated-user-data"));
            await using var provider = App.ConfigureServices(store, new QaDialogs());
            Require(provider.GetRequiredService<ICollectorTransport>().GetType() == typeof(CollectorTransport) && provider.GetRequiredService<ICollectorCaptureFactory>().GetType() == typeof(CollectorCaptureFactory), "Real Collector WPF uses production SSH transport and protected capture factory");
            provider.GetRequiredService<ThemeService>();
            var vm = provider.GetRequiredService<MainViewModel>();
            try
            {
                var window = provider.GetRequiredService<MainWindow>();
                app.MainWindow = window; window.DataContext = vm; window.ShowInTaskbar = true; window.ShowActivated = false; window.Show();
                vm.NavigateCommand.Execute("Config Collector");
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                var collector = vm.Collector;
                collector.TargetText = string.Join('\n', targets.Select(t => $"{t.Host}:{t.Port} {t.Name} owned software fixture"));
                collector.HostPins = string.Join('\n', targets.Select(t => $"{t.Host}:{t.Port} {pin}"));
                collector.Username1 = @"DOMAIN\fixture";
                collector.OutputFolder = Path.Combine(output, "completed-captures");
                collector.AnalysisCommandsCommand.Execute(null);
                var root = (DependencyObject)window.Content;
                var view = Descendants<NetStucked.Desktop.Views.ConfigCollectorView>(root).Single();
                var password = (PasswordBox)view.FindName("Password1");
                password.Password = "owned-lab-password";
                var startButton = Descendants<Button>(root).Single(b => Equals(b.Content, "Start"));
                startButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(collector.IsRunning && password.Password.Length == 0, "Actual Collector Start button begins real SSH and immediately clears its password input");
                await Until(() => !collector.IsRunning);
                Require(collector.State == CollectorBatchState.Completed && collector.Results.Count == 2 && collector.Results.All(r => r.Result.State == CollectorDeviceState.Completed), "Actual Collector WPF completes both pinned SSH devices: " + collector.Message);
                var manifest = new List<object>();
                foreach (var row in collector.Results)
                {
                    CollectorResult result = row.Result;
                    string transcript = await File.ReadAllTextAsync(result.TxtPath, deadline.Token);
                    Require(transcript.Contains("Owned software fixture; no real Cisco hardware") && !transcript.Contains("owned-lab-password"), "Real capture is explicitly labelled software fixture and contains no password");
                    using var capture = JsonDocument.Parse(await File.ReadAllTextAsync(result.JsonPath, deadline.Token));
                    Require(capture.RootElement.GetProperty("commands").GetArrayLength() == 8 && capture.RootElement.GetProperty("commands").EnumerateArray().All(c => c.GetProperty("Completed").GetBoolean()), "Real capture records eight successful read-only commands with times");
                    EvidenceDocument parsed = CiscoEvidenceParser.Parse(transcript, result.JsonPath, result.Sha256, result.Target.Host, DateTimeOffset.UtcNow, true, null);
                    Require(parsed.Facts.Any(f => f.Field == "Hostname") && parsed.Facts.Any(f => f.Field == "Version") && parsed.Neighbors.Length == 2 && parsed.Neighbors.All(n => n.Line > 0), "Real completed transcript imports into source-line-traceable hostname/version/CDP/LLDP facts");
                    manifest.Add(new { result.Target.Endpoint, result.Target.Description, result.TxtPath, result.JsonPath, result.Sha256, result.Bytes, Fixture = "Owned software fixture; no real Cisco hardware" });
                }
                await File.WriteAllTextAsync(Path.Combine(output, "completed-capture-manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), deadline.Token);
                foreach (string theme in new[] { "Light", "Dark" })
                {
                    vm.Appearance.SelectedTheme = theme;
                    Render(window, output, "Collector-real-ssh-" + theme.ToLowerInvariant(), 1536, 1024, 1);
                }
                vm.Appearance.SelectedTheme = "Dark";
                window.ShowActivated = true; window.Activate();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Console.WriteLine("VISIBLE OWNED COLLECTOR WINDOW: completed two real loopback SSH collections; native screenshot interval 60 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(60), deadline.Token);
                Require(errors.Messages.Count == 0, "Real Collector has no WPF binding errors: " + string.Join("; ", errors.Messages));
                await server.StandardInput.WriteLineAsync("stats"); await server.StandardInput.FlushAsync(deadline.Token);
                string auditText = await server.StandardOutput.ReadLineAsync(deadline.Token) ?? "{}";
                await File.WriteAllTextAsync(Path.Combine(output, "owned-ssh-audit.json"), auditText, deadline.Token);
                using var audit = JsonDocument.Parse(auditText);
                Require(audit.RootElement.GetProperty("auth").GetArrayLength() == 2 && audit.RootElement.GetProperty("commands").GetArrayLength() == 16 && audit.RootElement.GetProperty("blank_commands").GetInt32() == 0, "Owned server confirms exactly two authenticated sessions and sixteen read-only command sends without blanks");
                if (Environment.GetEnvironmentVariable("NETSTUCKED_QA_SSH_CTR") == "1")
                {
                    Require(audit.RootElement.GetProperty("negotiated").GetArrayLength() == 2 && audit.RootElement.GetProperty("negotiated").EnumerateArray().All(n => n.GetProperty("cipher").GetString() == "aes256-ctr" && n.GetProperty("mac").GetString() == "hmac-sha2-512-etm@openssh.com"), "Actual WPF collections negotiate the incident's AES256-CTR / SHA512-ETM pair");
                    Require(audit.RootElement.GetProperty("bare_lf_bytes").GetInt32() == 0 && audit.RootElement.GetProperty("cr_bytes").GetInt32() == 16, "Actual WPF sends exactly one console CR per command and no LF or replay");
                }
                await File.WriteAllTextAsync(Path.Combine(output, "result.txt"), "PASS: actual WPF Collector + production pinned SSH transport + protected capture factory. Two owned loopback software fixtures; no real Cisco/working network tested.", deadline.Token);
            }
            finally { await vm.ShutdownAsync(); }
        }
        finally
        {
            if (!server.HasExited)
            {
                await server.StandardInput.WriteLineAsync("stop"); server.StandardInput.Close();
                try { await server.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (TimeoutException) { server.Kill(true); }
            }
            await File.WriteAllTextAsync(Path.Combine(output, "fixture-stderr.txt"), await error);
        }
    }
}
