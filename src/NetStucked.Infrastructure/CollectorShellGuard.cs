using NetStucked.Core;

namespace NetStucked.Infrastructure;

// Capture runs synchronously in the Session data event BEFORE the SDK channel's
// ShellStream handler. Thus even pre-constructor greeting data is backpressured.
// ShellStream's duplicate buffer is drained synchronously once it is available.
internal sealed class CollectorShellGuard(CollectorSshOutput output)
{
    private readonly Stream _stdout = output.Sink(false), _stderr = output.Sink(true);
    private readonly object _drainGate = new();
    private readonly byte[] _discard = new byte[4096];
    private Func<bool>? _available;
    private Func<byte[], int, int, int>? _read;
    private bool _ready;
    private int _initialBytes;
    public void Capture(byte[] data, int offset, int count, bool error)
    {
        lock (_drainGate)
        {
            // Before CreateShellStream returns, no caller can consume output.
            // Keep startup data below the queue capacity so shell-success behind
            // a large banner cannot deadlock the SDK receive thread.
            if (!_ready)
            {
                if (count > 64 * 1024 - _initialBytes)
                    throw new CollectorException(CollectorFailure.Transport, "SSH initial output exceeded the 64 KiB setup limit; collection stopped.");
                _initialBytes += count;
            }
        }
        (error ? _stderr : _stdout).Write(data, offset, count);
    }
    public void AttachDrain(Func<bool> available, Func<byte[], int, int, int> read)
    {
        lock (_drainGate) { _available = available; _read = read; DrainLocked(); _ready = true; }
    }
    public void Drain() { lock (_drainGate) DrainLocked(); }
    private void DrainLocked()
    {
        // A single owner covers BOTH the availability check and Read. Two
        // concurrent observers cannot drain one another and block an empty Read.
        while (_available?.Invoke() == true)
            if (_read!(_discard, 0, _discard.Length) == 0) break;
    }
}
