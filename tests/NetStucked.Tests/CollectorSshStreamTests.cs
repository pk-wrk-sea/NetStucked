using System.Reflection;
using System.Text;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

// Exercise the internal production byte-stream adapters without SSH, sockets or SDK doubles.
public sealed class CollectorSshStreamTests
{
    [Fact]
    public async Task OutputUsesIndependentIncrementalUtf8DecodersForStdoutAndStderr()
    {
        using var stop = new CancellationTokenSource(); using var output = new OutputProbe(stop.Token);
        using var stdout = output.Sink(false); using var stderr = output.Sink(true);
        stdout.Write([0xe2, 0x82]); // First two bytes of the Euro sign.
        stderr.Write([0xf0, 0x9f]); // First two bytes of the rocket, interleaved on stderr.
        stdout.Write([0xac]); stderr.Write([0x9a, 0x80]); output.Complete();
        Assert.Equal("€🚀", Encoding.UTF8.GetString(await ReadAllAsync(output, 1)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingMalformedUtf8FollowedByFullAsciiFrameDoesNotOverflowCharBuffer(bool error)
    {
        using var stop = new CancellationTokenSource(); using var output = new OutputProbe(stop.Token);
        using var stream = output.Sink(error);
        stream.Write([0xe2]); // Decoder retains an incomplete sequence across frame boundaries.
        stream.Write(Enumerable.Repeat((byte)'x', 4096).ToArray()); output.Complete();
        byte[] result = await ReadAllAsync(output, 17);
        Assert.Equal(4099, result.Length); // UTF-8 replacement character plus 4096 ASCII bytes.
        Assert.Equal("\ufffd" + new string('x', 4096), Encoding.UTF8.GetString(result));
    }

    [Fact]
    public async Task EofFlushesBothIncompleteDecodersExactlyOnce()
    {
        using var stop = new CancellationTokenSource(); using var output = new OutputProbe(stop.Token);
        using var stdout = output.Sink(false); using var stderr = output.Sink(true);
        stdout.Write([0xe2]); stderr.Write([0xf0, 0x9f]); output.Complete();
        Assert.Equal("\ufffd\ufffd", Encoding.UTF8.GetString(await ReadAllAsync(output, 1)));
        Assert.Equal(0, await output.ReadAsync(new byte[8], default));
    }

    [Fact]
    public async Task OutputQueueSharesThirtyTwoFramesAcrossBothStreamsAndCancelsBlockedWriter()
    {
        using var stop = new CancellationTokenSource(); using var output = new OutputProbe(stop.Token);
        using var stdout = output.Sink(false); using var stderr = output.Sink(true);
        byte[] frame = new byte[4096];
        for (int i = 0; i < 32; i++) (i % 2 == 0 ? stdout : stderr).Write(frame);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task blocked = Task.Run(() => { started.SetResult(); stdout.Write(frame); });
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(2)); await Task.Delay(50);
            Assert.False(blocked.IsCompleted, "The 33rd shared 4 KiB frame must wait, rather than grow an unbounded output buffer.");
            stop.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blocked.WaitAsync(TimeSpan.FromSeconds(2)));
        }
        finally { stop.Cancel(); output.Complete(); try { await blocked; } catch (OperationCanceledException) { } }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutputReadHonorsAlreadyCancelledCallerEvenWithPendingBytes(bool buffered)
    {
        using var stop = new CancellationTokenSource(); using var output = new OutputProbe(stop.Token);
        using var sink = output.Sink(false);
        if (buffered) { sink.Write([1, 2]); Assert.Equal(1, await output.ReadAsync(new byte[1], default)); }
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => output.ReadAsync(new byte[1], cancel.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(2)));
    }

    private static async Task<byte[]> ReadAllAsync(OutputProbe output, int bufferSize)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var bytes = new MemoryStream(); byte[] buffer = new byte[bufferSize]; int count;
        while ((count = await output.ReadAsync(buffer, deadline.Token)) > 0) bytes.Write(buffer, 0, count);
        return bytes.ToArray();
    }
    private static Type InternalType(string name) => typeof(CollectorTransport).Assembly.GetType("NetStucked.Infrastructure." + name, true)!;
    private sealed class OutputProbe : IDisposable
    {
        private readonly Func<bool, Stream> _sink;
        private readonly Func<Memory<byte>, CancellationToken, ValueTask<int>> _read;
        private readonly Action<Exception?> _complete;
        public OutputProbe(CancellationToken stop)
        {
            Type type = InternalType("CollectorSshOutput"); object instance = Activator.CreateInstance(type, stop)!;
            _sink = type.GetMethod("Sink", BindingFlags.Instance | BindingFlags.Public)!.CreateDelegate<Func<bool, Stream>>(instance);
            _read = type.GetMethod("ReadAsync", BindingFlags.Instance | BindingFlags.Public)!.CreateDelegate<Func<Memory<byte>, CancellationToken, ValueTask<int>>>(instance);
            _complete = type.GetMethod("Complete", BindingFlags.Instance | BindingFlags.Public)!.CreateDelegate<Action<Exception?>>(instance);
        }
        public Stream Sink(bool error) => _sink(error);
        public ValueTask<int> ReadAsync(Memory<byte> target, CancellationToken token) => _read(target, token);
        public void Complete() => _complete(null);
        public void Dispose() => Complete();
    }
}
