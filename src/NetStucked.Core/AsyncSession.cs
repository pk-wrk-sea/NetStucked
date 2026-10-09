namespace NetStucked.Core;

/// <summary>Serializes transitions; operation leases make Pause await a real quiescent boundary.</summary>
public abstract class AsyncSession : IAsyncDisposable
{
    private readonly SemaphoreSlim _transition = new(1, 1);
    private readonly object _sync = new();
    private CancellationTokenSource? _lifetime;
    private CancellationTokenSource? _pause;
    private Task? _runner;
    private TaskCompletionSource _gate = CompletedSource();
    private TaskCompletionSource _idle = CompletedSource();
    private int _active;
    private int _state;
    private long _sessionId;
    public long SessionId => Interlocked.Read(ref _sessionId);
    public SessionState State => (SessionState)Volatile.Read(ref _state);
    public string? LastError { get; private set; }

    private static TaskCompletionSource Source() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static TaskCompletionSource CompletedSource() { var source = Source(); source.SetResult(); return source; }
    protected void SetState(SessionState state) => Volatile.Write(ref _state, (int)state);

    protected async Task StartSessionAsync(Action initialize, Func<CancellationToken, Task> run)
    {
        await _transition.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_runner is { IsCompleted: false }) throw new InvalidOperationException("A session is already active.");
            _lifetime?.Dispose(); _pause?.Dispose();
            SetState(SessionState.Starting);
            LastError = null;
            Interlocked.Increment(ref _sessionId);
            initialize();
            _lifetime = new(); _pause = new();
            lock (_sync) _gate = CompletedSource();
            SetState(SessionState.Running);
            var token = _lifetime.Token;
            _runner = Task.Run(async () =>
            {
                try { await run(token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception ex) { LastError = ex.Message; OnSessionError(ex); SetState(SessionState.Error); }
                finally { if (State != SessionState.Error) SetState(SessionState.Stopped); }
            }, CancellationToken.None);
        }
        catch (Exception ex) { LastError = ex.Message; if (_runner is not { IsCompleted: false }) SetState(SessionState.Error); throw; }
        finally { _transition.Release(); }
    }

    protected virtual void OnSessionError(Exception error) { }

    protected async ValueTask<OperationLease> EnterOperationAsync(CancellationToken token)
    {
        while (true)
        {
            Task gate;
            lock (_sync) gate = _gate.Task;
            await gate.WaitAsync(token).ConfigureAwait(false);
            lock (_sync)
            {
                token.ThrowIfCancellationRequested();
                if (State != SessionState.Running) continue;
                if (_active++ == 0) _idle = Source();
                return new(this, CancellationTokenSource.CreateLinkedTokenSource(token, _pause!.Token));
            }
        }
    }

    public async Task PauseAsync()
    {
        await _transition.WaitAsync().ConfigureAwait(false);
        try
        {
            Task idle;
            lock (_sync)
            {
                if (State != SessionState.Running) return;
                SetState(SessionState.Pausing);
                _gate = Source();
                idle = _idle.Task;
            }
            _pause!.Cancel();
            await idle.ConfigureAwait(false);
            if (_runner is { IsCompleted: false }) SetState(SessionState.Paused);
        }
        finally { _transition.Release(); }
    }

    public async Task ResumeAsync()
    {
        await _transition.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_sync)
            {
                if (State != SessionState.Paused) return;
                _pause!.Dispose(); _pause = new();
                SetState(SessionState.Running);
                _gate.TrySetResult();
            }
        }
        finally { _transition.Release(); }
    }

    public async Task StopAsync()
    {
        await _transition.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_runner is null) return;
            if (!_runner.IsCompleted) SetState(SessionState.Stopping);
            _lifetime!.Cancel();
            lock (_sync) _gate.TrySetResult();
            await _runner.ConfigureAwait(false);
            _lifetime.Dispose(); _pause!.Dispose();
            _lifetime = null; _pause = null; _runner = null;
            if (State != SessionState.Error) SetState(SessionState.Stopped);
        }
        finally { _transition.Release(); }
    }

    public async ValueTask DisposeAsync() { await StopAsync().ConfigureAwait(false); GC.SuppressFinalize(this); }

    protected sealed class OperationLease(AsyncSession owner, CancellationTokenSource cancellation) : IDisposable
    {
        public CancellationToken Token => cancellation.Token;
        public void Dispose()
        {
            cancellation.Dispose();
            lock (owner._sync) { if (--owner._active == 0) owner._idle.TrySetResult(); }
        }
    }
}
