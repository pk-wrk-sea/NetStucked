namespace NetStucked.Core;

public sealed class CollectorController(ICollectorTransport transport, ICollectorCaptureFactory captures)
{
    public async Task<IReadOnlyList<CollectorResult>> RunAsync(CollectorRun run, Action<CollectorResult> progress, CancellationToken token)
    {
        var results = new CollectorResult[run.Targets.Length]; int next = -1;
        foreach (var target in run.Targets) progress(new(run.Id, target, CollectorDeviceState.Queued, "Queued"));
        async Task Worker()
        {
            while (true)
            {
                int index = Interlocked.Increment(ref next); if (index >= run.Targets.Length) return;
                var target = run.Targets[index];
                results[index] = token.IsCancellationRequested ? new(run.Id, target, CollectorDeviceState.Cancelled, "Cancelled before connecting.")
                    : await CollectAsync(run, target, progress, token).ConfigureAwait(false);
                progress(results[index]);
            }
        }
        await Task.WhenAll(Enumerable.Range(0, Math.Min(run.Options.Concurrency, run.Targets.Length)).Select(_ => Worker())).ConfigureAwait(false);
        return results;
    }
    private async Task<CollectorResult> CollectAsync(CollectorRun run, CollectorTarget target, Action<CollectorResult> progress, CancellationToken token)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow; int? slot = null;
        ICollectorCapture? capture = null; var commands = new List<CollectorCommandEvidence>();
        CollectorResult result;
        void State(CollectorDeviceState state) => progress(new(run.Id, target, state, state.ToString(), slot, Preview: capture?.Preview ?? "", Started: start));
        try
        {
            capture = await captures.CreateAsync(run, target, token).ConfigureAwait(false);
            int[] slots = target.AuthSlot is { } selected ? [selected] : run.Auth2 is not null ? [1, 2] : [1];
            ICollectorConnection? connected = null;
            foreach (int candidate in slots)
            {
                slot = candidate; var credentials = candidate == 1 ? run.Auth1 : run.Auth2!;
                try
                {
                    // A transient CONNECT failure can retry once with the SAME credentials, before commands only.
                    for (int retry = 0; ; retry++)
                    {
                        State(CollectorDeviceState.Connecting);
                        try
                        {
                            State(CollectorDeviceState.Authenticating);
                            connected = await transport.ConnectAsync(target, run.Options, credentials, run.HostPins.GetValueOrDefault(target.Endpoint), token).ConfigureAwait(false);
                            break;
                        }
                        catch (CollectorException ex) when (ex.Failure == CollectorFailure.TransientTransport && retry == 0)
                        { await Task.Delay(250, token).ConfigureAwait(false); }
                    }
                    break;
                }
                catch (CollectorException ex) when (ex.Failure == CollectorFailure.AuthenticationRejected && target.AuthSlot is null && candidate == 1 && run.Auth2 is not null) { }
            }
            if (connected is null) throw new CollectorException(CollectorFailure.AuthenticationRejected, "Authentication rejected.");
            string prompt;
            await using (connected.ConfigureAwait(false))
                prompt = await CollectorSession.RunAsync(connected, run, slot == 1 ? run.Auth1 : run.Auth2!, capture, commands, State, token).ConfigureAwait(false);
            State(CollectorDeviceState.Saving);
            result = new(run.Id, target, CollectorDeviceState.Completed, "Collection completed.", slot, Started: start, DevicePrompt: prompt);
        }
        catch (OperationCanceledException) { result = new(run.Id, target, CollectorDeviceState.Cancelled, "Cancelled; completed output retained where available.", slot, Started: start); }
        catch (CollectorException ex) { result = new(run.Id, target, commands.Count > 0 ? CollectorDeviceState.Partial : CollectorDeviceState.Failed, ex.Message, slot, Started: start); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { result = new(run.Id, target, commands.Count > 0 ? CollectorDeviceState.Partial : CollectorDeviceState.Failed, "Output/transport I/O failed; check folder permissions, disk space and connectivity.", slot, Started: start); }
        catch (Exception)
        { result = new(run.Id, target, commands.Count > 0 ? CollectorDeviceState.Partial : CollectorDeviceState.Failed, "Collection failed; no credentials or device exceptions were logged.", slot, Started: start); }
        if (capture is not null)
        {
            try
            {
                // Cancel cannot interrupt evidence finalization forever; a bounded independent grace period preserves partial output.
                using var saveTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                result = await capture.CompleteAsync(result with { Finished = DateTimeOffset.UtcNow }, commands, saveTimeout.Token).ConfigureAwait(false);
            }
            catch (Exception) { result = result with { State = CollectorDeviceState.Failed, Detail = "Capture could not be finalized; check output storage. Any finalized files remain in the run folder." }; }
            finally { await capture.DisposeAsync().ConfigureAwait(false); }
        }
        return result with { Finished = DateTimeOffset.UtcNow };
    }
}
