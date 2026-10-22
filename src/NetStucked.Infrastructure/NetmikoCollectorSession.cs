using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

/// <summary>One isolated, cancellation-owned worker per device; no secret argv, files or stderr forwarding.</summary>
public sealed class NetmikoCollectorSession(ITerminalHostTrust trust) : ICollectorSessionRunner
{
    public async Task<CollectorSessionIdentity> RunAsync(CollectorRun run, CollectorTarget target, CollectorCredentials credentials,
        ICollectorCapture capture, List<CollectorCommandEvidence> commands, Action<CollectorDeviceState> state, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string runtime = Path.Combine(AppContext.BaseDirectory, "CollectorRuntime");
        string python = Path.Combine(runtime, "python.exe"), worker = Path.Combine(runtime, "collector_worker.py");
        if (!File.Exists(python) || !File.Exists(worker)) throw new CollectorException(CollectorFailure.Transport, "Bundled Netmiko runtime is missing. Reinstall or recover the complete application package.");
        var start = new ProcessStartInfo(python) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8, WorkingDirectory = runtime };
        foreach (string arg in new[] { "-I", "-B", "-u", worker }) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new CollectorException(CollectorFailure.Transport, "Netmiko worker could not start.");
        void Kill() { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { } }
        // Bounds every phase, even if an upstream driver blocks outside its configured timeout.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(run.Options.ConnectTimeoutSeconds * 3 + run.Options.CommandTimeoutSeconds * (run.BasicCommands.Length + run.CollectionCommands.Length + 8) + 600));
        using var cancelled = deadline.Token.Register(Kill);
        var discardError = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
        var identity = new CollectorSessionIdentity("", "", target.Host);
        DateTimeOffset lastPreview = DateTimeOffset.MinValue;
        bool complete = false, active = false;
        int next = 0;
        string profile = target.DeviceProfile.Length > 0 ? target.DeviceProfile : run.Options.DeviceProfile;
        string[] basic = target.DeviceProfile.Length > 0 ? CollectorCommands.Lines(CollectorCommands.Basic(profile)) : run.BasicCommands.ToArray();
        string[] collection = target.DeviceProfile.Length > 0 ? CollectorCommands.Lines(CollectorCommands.Collection(profile)) : run.CollectionCommands.ToArray();
        string[] expected = basic.Concat(collection).ToArray();
        var frames = new FrameReader(process.StandardOutput);
        try
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
            {
                schema = 1, protocol = run.Options.Protocol.ToString(), driver = CollectorCommands.NetmikoType(profile),
                host = target.Host, port = target.Port, username = credentials.Username, password = credentials.Password, enable = credentials.EnableSecret,
                connectTimeout = run.Options.ConnectTimeoutSeconds, commandTimeout = run.Options.CommandTimeoutSeconds,
                maxBytes = run.Options.MaxCaptureBytes, basic, commands = collection
            }).AsMemory(), deadline.Token).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(deadline.Token).ConfigureAwait(false);
            while (await frames.ReadAsync(deadline.Token).ConfigureAwait(false) is { } line)
            {
                using var document = JsonDocument.Parse(line); var e = document.RootElement;
                switch (e.GetProperty("kind").GetString())
                {
                    case "hostkey":
                        bool accept = await trust.VerifyAsync(target.Host, target.Port, e.GetProperty("algorithm").GetString()!, e.GetProperty("fingerprint").GetString()!, deadline.Token).ConfigureAwait(false);
                        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { accept }).AsMemory(), deadline.Token).ConfigureAwait(false);
                        await process.StandardInput.FlushAsync(deadline.Token).ConfigureAwait(false);
                        break;
                    case "state":
                        if (Enum.TryParse<CollectorDeviceState>(e.GetProperty("state").GetString(), out var phase)) state(phase);
                        break;
                    case "identity":
                        identity = new(e.GetProperty("prompt").GetString()!, e.GetProperty("hostname").GetString()!, e.GetProperty("address").GetString()!);
                        break;
                    case "command":
                        string command = e.GetProperty("command").GetString()!;
                        if (active || next >= expected.Length || expected[next++] != command) throw new InvalidDataException("Invalid worker command sequence.");
                        active = true; var now = DateTimeOffset.UtcNow; commands.Add(new(command, now, now, false));
                        state(basic.Contains(command) ? CollectorDeviceState.Preparing : CollectorDeviceState.Collecting);
                        await capture.AppendAsync($"\n--- {command} ---\n", deadline.Token).ConfigureAwait(false);
                        break;
                    case "output":
                        if (!active) throw new InvalidDataException("Unexpected worker output.");
                        await capture.AppendAsync(e.GetProperty("text").GetString()!, deadline.Token).ConfigureAwait(false);
                        if (DateTimeOffset.UtcNow - lastPreview > TimeSpan.FromMilliseconds(200)) { lastPreview = DateTimeOffset.UtcNow; state(basic.Contains(commands[^1].Command) ? CollectorDeviceState.Preparing : CollectorDeviceState.Collecting); }
                        break;
                    case "command_done":
                        if (!active) throw new InvalidDataException("Unexpected command completion.");
                        commands[^1] = commands[^1] with { Finished = DateTimeOffset.UtcNow, Completed = true }; active = false;
                        break;
                    case "done": complete = !active && next == expected.Length; break;
                    case "error":
                        var code = Enum.TryParse<CollectorFailure>(e.GetProperty("code").GetString(), out var failure) ? failure : CollectorFailure.Transport;
                        throw new CollectorException(code, SafeMessage(code));
                    default: throw new InvalidDataException("Unsupported worker event.");
                }
            }
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            if (!complete || process.ExitCode != 0) throw new CollectorException(CollectorFailure.Transport, "Netmiko worker stopped before collection completed; commands were not replayed.");
            return identity;
        }
        catch (Exception ex) when (deadline.IsCancellationRequested && ex is IOException or OperationCanceledException or InvalidOperationException or CollectorException or JsonException)
        {
            token.ThrowIfCancellationRequested();
            throw new CollectorException(CollectorFailure.Timeout, "Netmiko worker exceeded the bounded run deadline; stopped without credential fallback.");
        }
        finally
        {
            if (active && commands.Count > 0) commands[^1] = commands[^1] with { Finished = DateTimeOffset.UtcNow };
            Kill();
            await process.WaitForExitAsync().ConfigureAwait(false);
            await discardError.ConfigureAwait(false);
        }
    }
    private sealed class FrameReader(StreamReader reader)
    {
        private readonly char[] _buffer = new char[4096]; private int _offset, _count;
        public async Task<string?> ReadAsync(CancellationToken token)
        {
            var line = new StringBuilder();
            while (true)
            {
                if (_offset == _count) { _count = await reader.ReadAsync(_buffer, token).ConfigureAwait(false); _offset = 0; if (_count == 0) break; }
                char ch = _buffer[_offset++]; if (ch == '\n') return line.ToString();
                line.Append(ch); if (line.Length > 65536) throw new InvalidDataException("Worker frame limit exceeded.");
            }
            if (line.Length > 0) throw new InvalidDataException("Incomplete worker frame.");
            return null;
        }
    }
    private static string SafeMessage(CollectorFailure failure) => failure switch
    {
        CollectorFailure.HostKey => "SSH host key was not accepted. Review the device identity before starting again.",
        CollectorFailure.AuthenticationRejected => "Device explicitly rejected the password.",
        CollectorFailure.UnsupportedAuthentication => "Login did not complete with password authentication; no credential fallback for MFA, unsupported methods or ambiguous failures.",
        CollectorFailure.Timeout => "Netmiko timed out waiting for connection, login or device prompt; no credential fallback or command replay.",
        CollectorFailure.Command => "Device rejected a collection command or command authorization; partial output retained.",
        CollectorFailure.EnableRejected => "Device did not grant the requested privileged mode; verify the enable secret and account privilege.",
        CollectorFailure.Confirmation => "Device/driver requested an unapproved command or interactive confirmation; stopped without answering it.",
        CollectorFailure.OutputLimit => "Device output exceeded the bounded capture limit; partial output retained.",
        _ => "Netmiko SSH/session failed. Check reachability, supported algorithms and device profile; no credential fallback."
    };
}
