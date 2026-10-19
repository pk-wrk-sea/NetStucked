using System.Text;
using NetStucked.Core;
using Tmds.Ssh;

namespace NetStucked.Infrastructure;

internal sealed class SshTerminalConnection(SshClient client, RemoteProcess process) : ITerminalConnection
    {
        private readonly Decoder _stdout = Encoding.UTF8.GetDecoder(), _stderr = Encoding.UTF8.GetDecoder();
        private readonly byte[] _raw = new byte[4096]; private readonly char[] _chars = new char[4096];
        private byte[] _pending = []; private int _offset; private bool _ended;
        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
        {
            while (_offset == _pending.Length)
            {
                if (_ended) return 0;
                // One channel read drains either stdout or extended stderr. Separate decoders
                // prevent split UTF-8 on one stream being corrupted by the other stream.
                var read = await process.ReadAsync(_raw, _raw, token).ConfigureAwait(false);
                if (read.BytesRead == 0)
                {
                    _ended = true;
                    int first = _stdout.GetChars([], _chars, true);
                    int second = _stderr.GetChars([], _chars.AsSpan(first), true);
                    _pending = Encoding.UTF8.GetBytes(_chars.AsSpan(0, first + second).ToString());
                }
                else
                {
                    int chars = (read.IsError ? _stderr : _stdout).GetChars(_raw, 0, read.BytesRead, _chars, 0);
                    _pending = Encoding.UTF8.GetBytes(_chars.AsSpan(0, chars).ToString());
                }
                _offset = 0;
            }
            int count = Math.Min(buffer.Length, _pending.Length - _offset);
            _pending.AsMemory(_offset, count).CopyTo(buffer); _offset += count; return count;
        }
        public ValueTask ResizeAsync(int columns, int rows, CancellationToken token) { token.ThrowIfCancellationRequested(); process.SetTerminalSize(new(columns, rows)); return ValueTask.CompletedTask; }
        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token) => process.WriteAsync(bytes, token);
        public ValueTask DisposeAsync() { process.Dispose(); client.Dispose(); return ValueTask.CompletedTask; }
    }
