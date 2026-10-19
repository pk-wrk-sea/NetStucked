using System.Text;
using System.Text.RegularExpressions;

namespace NetStucked.Core;

public sealed class CollectorPromptRecognizer
{
    private string _tail = "";
    public string Tail => _tail;
    public void Push(string text)
    {
        _tail += text;
        if (_tail.Length > 16384) _tail = _tail[^16384..];
        _tail = Regex.Replace(_tail, @"\x1B\[[0-?]*[ -/]*[@-~]", "");
        // Strip terminal redraw characters without collapsing line boundaries.
        _tail = _tail.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\b", "");
    }
    public string? InitialPrompt()
    {
        string line = _tail[(_tail.LastIndexOf('\n') + 1)..].Trim();
        return Regex.IsMatch(line, @"^([A-Za-z0-9][A-Za-z0-9_.:/()-]{0,100}[#>]|<[A-Za-z0-9_.:/()-]{1,100}>)$") ? line : null;
    }
    public bool AtPrompt(string prompt) => _tail[(_tail.LastIndexOf('\n') + 1)..].Trim() == prompt;
    public bool Password => Regex.IsMatch(_tail, @"(?i)(password|passcode)\s*:\s*$");
    public bool Login => Regex.IsMatch(_tail, @"(?i)(login|username)\s*:\s*$");
    public bool Rejected => Regex.IsMatch(_tail, @"(?im)(authentication failed|login incorrect|access denied|^%\s*(bad passwords|access denied)|permission denied)");
    public bool Mfa => Regex.IsMatch(_tail, @"(?i)(verification code|one.time password|\bOTP\b|\bMFA\b|duo push|authenticator code)");
    public bool Confirmation => Regex.IsMatch(_tail, @"(?i)(\[confirm\]|\[yes/no\]|\(yes/no\)|\[y/n\]|press (any key|enter|return)|--more--|---- more ----)");
    public bool CommandError => Regex.IsMatch(_tail, @"(?im)(^%\s*(invalid|error|unknown|incomplete|ambiguous|authorization)|^error:|^unrecognized command|^invalid input|^permission denied)");
}

public static class CollectorSession
{
    public static async Task<string> RunAsync(ICollectorConnection connection, CollectorRun run, CollectorCredentials credentials,
        ICollectorCapture capture, List<CollectorCommandEvidence> commands, Action<CollectorDeviceState> state, CancellationToken token)
    {
        var reader = new Reader(connection, run.Options.CommandTimeoutSeconds);
        string prompt = await reader.UntilAsync(p => p.InitialPrompt(), null, token).ConfigureAwait(false);
        state(CollectorDeviceState.Preparing);
        if (prompt.EndsWith('>') && credentials.EnableSecret.Length > 0)
        {
            if (run.Options.DeviceProfile == "Huawei VRP") throw new CollectorException(CollectorFailure.EnableRejected, "Huawei privilege escalation is not enabled by this profile; use an account authorized for display commands.");
            await reader.SendAsync("enable", token).ConfigureAwait(false);
            string phase = await reader.UntilAsync(p => p.Password ? "password" : p.InitialPrompt() is { } found ? found : null, null, token).ConfigureAwait(false);
            if (phase == "password")
            {
                await reader.SendAsync(credentials.EnableSecret, token).ConfigureAwait(false);
                try { phase = await reader.UntilAsync(p => p.Password ? throw new CollectorException(CollectorFailure.EnableRejected, "Enable secret rejected.") : p.InitialPrompt(), null, token).ConfigureAwait(false); }
                catch (CollectorException ex) when (ex.Failure == CollectorFailure.AuthenticationRejected) { throw new CollectorException(CollectorFailure.EnableRejected, "Enable secret rejected."); }
            }
            if (!phase.EndsWith('#')) throw new CollectorException(CollectorFailure.EnableRejected, "Privileged prompt was not reached.");
            prompt = phase;
        }
        DateTimeOffset lastPreview = DateTimeOffset.MinValue;
        foreach (string command in run.BasicCommands.Concat(run.CollectionCommands))
        {
            token.ThrowIfCancellationRequested();
            var currentState = run.BasicCommands.Contains(command) ? CollectorDeviceState.Preparing : CollectorDeviceState.Collecting;
            state(currentState);
            DateTimeOffset start = DateTimeOffset.UtcNow;
            // Command is recorded BEFORE sending: a failure after this point may never reconnect/replay.
            commands.Add(new(command, start, start, false));
            await capture.AppendAsync($"\n--- {command} ---\n", token).ConfigureAwait(false);
            try
            {
                await reader.SendAsync(command, token).ConfigureAwait(false);
                await reader.UntilAsync(p => p.AtPrompt(prompt) ? prompt : null, capture, token, () =>
                {
                    if (DateTimeOffset.UtcNow - lastPreview >= TimeSpan.FromMilliseconds(200)) { lastPreview = DateTimeOffset.UtcNow; state(currentState); }
                }).ConfigureAwait(false);
                commands[^1] = new(command, start, DateTimeOffset.UtcNow, true);
            }
            catch { commands[^1] = new(command, start, DateTimeOffset.UtcNow, false); throw; }
        }
        return prompt;
    }

    private sealed class Reader(ICollectorConnection connection, int timeoutSeconds)
    {
        public async Task SendAsync(string text, CancellationToken token)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            try { await connection.WriteAsync(Encoding.UTF8.GetBytes(text + "\n"), timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new CollectorException(CollectorFailure.Timeout, "Terminal write timed out; command was not replayed."); }
        }
        public async Task<string> UntilAsync(Func<CollectorPromptRecognizer, string?> match, ICollectorCapture? capture, CancellationToken token, Action? preview = null)
        {
            var recognizer = new CollectorPromptRecognizer(); byte[] buffer = new byte[4096]; char[] chars = new char[4096]; var decoder = Encoding.UTF8.GetDecoder();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                while (true)
                {
                    int count = await connection.ReadAsync(buffer, timeout.Token).ConfigureAwait(false);
                    if (count == 0) throw new CollectorException(CollectorFailure.Transport, "Device disconnected; commands were not replayed.");
                    int used = decoder.GetChars(buffer, 0, count, chars, 0); string text = new(chars, 0, used);
                    if (capture is not null) { await capture.AppendAsync(text, timeout.Token).ConfigureAwait(false); preview?.Invoke(); }
                    recognizer.Push(text);
                    if (recognizer.Mfa) throw new CollectorException(CollectorFailure.UnsupportedAuthentication, "MFA/OTP requires an interactive login and is unsupported.");
                    if (recognizer.Rejected) throw new CollectorException(capture is null ? CollectorFailure.AuthenticationRejected : CollectorFailure.Command, "Device rejected access in this session.");
                    if (recognizer.Confirmation) throw new CollectorException(CollectorFailure.Confirmation, "Interactive confirmation or pagination detected; collection stopped.");
                    if (capture is not null && recognizer.CommandError) throw new CollectorException(CollectorFailure.Command, "Device reported a command/authorization error; collection stopped.");
                    if (match(recognizer) is { } result) return result;
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new CollectorException(CollectorFailure.Timeout, "Timed out waiting for the device prompt; no command replay."); }
        }
    }
}
