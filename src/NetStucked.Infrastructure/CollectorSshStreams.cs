using System.Text;
using System.Security.Cryptography;
using System.Threading.Channels;

namespace NetStucked.Infrastructure;

// One shared bounded queue for both output streams. 32 x 4 KiB queued bytes plus
// one producer chunk, incremental decoders and worst-case replacement text stay
// below 256 KiB. Blocking SDK writes apply backpressure off the Dispatcher.
internal sealed class CollectorSshOutput
{
    private sealed record Frame(byte[] Bytes, bool Error);
    private readonly Channel<Frame> _frames = Channel.CreateBounded<Frame>(new BoundedChannelOptions(32) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
    private readonly CancellationToken _stop;
    private readonly Decoder _stdout = Encoding.UTF8.GetDecoder(), _stderr = Encoding.UTF8.GetDecoder();
    private readonly char[] _chars = new char[Encoding.UTF8.GetMaxCharCount(4096) + 8];
    private byte[] _pending = [];
    private int _offset;
    private bool _flushed;
    public CollectorSshOutput(CancellationToken stop) => _stop = stop;
    public Stream Sink(bool error) => new OutputSink(this, error);
    public void Complete(Exception? failure = null) => _frames.Writer.TryComplete(failure);
    private void Write(ReadOnlySpan<byte> data, bool error)
    {
        while (!data.IsEmpty)
        {
            _stop.ThrowIfCancellationRequested();
            int length = Math.Min(4096, data.Length);
            _frames.Writer.WriteAsync(new(data[..length].ToArray(), error), _stop).AsTask().GetAwaiter().GetResult();
            data = data[length..];
        }
    }
    public async ValueTask<int> ReadAsync(Memory<byte> target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        while (_offset == _pending.Length)
        {
            _pending = []; _offset = 0;
            if (await _frames.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                if (!_frames.Reader.TryRead(out var frame)) continue;
                int count = (frame.Error ? _stderr : _stdout).GetChars(frame.Bytes, _chars, false);
                _pending = Encoding.UTF8.GetBytes(_chars.AsSpan(0, count).ToString());
            }
            else
            {
                if (_flushed) return 0;
                _flushed = true;
                int count = _stdout.GetChars([], _chars, true);
                count += _stderr.GetChars([], _chars.AsSpan(count), true);
                _pending = Encoding.UTF8.GetBytes(_chars.AsSpan(0, count).ToString());
            }
        }
        int used = Math.Min(target.Length, _pending.Length - _offset);
        token.ThrowIfCancellationRequested();
        _pending.AsMemory(_offset, used).CopyTo(target); _offset += used; return used;
    }
    private sealed class OutputSink(CollectorSshOutput owner, bool error) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override void Write(byte[] buffer, int offset, int count) => owner.Write(buffer.AsSpan(offset, count), error);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
